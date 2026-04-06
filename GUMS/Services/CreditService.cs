using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

/// <summary>
/// Service for managing member credit balances.
/// </summary>
public class CreditService : ICreditService
{
    private readonly ApplicationDbContext _context;
    private readonly IAccountingService? _accountingService;
    private readonly IPaymentService _paymentService;

    public CreditService(
        ApplicationDbContext context,
        IPaymentService paymentService,
        IAccountingService? accountingService = null)
    {
        _context = context;
        _paymentService = paymentService;
        _accountingService = accountingService;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetCreditBalanceAsync(string membershipNumber)
    {
        var credit = await _context.MemberCredits
            .AsNoTracking()
            .FirstOrDefaultAsync(mc => mc.MembershipNumber == membershipNumber);

        return credit?.Balance ?? 0;
    }

    /// <inheritdoc/>
    public async Task<List<CreditTransaction>> GetCreditHistoryAsync(string membershipNumber)
    {
        return await _context.CreditTransactions
            .Include(ct => ct.SourcePayment)
            .Include(ct => ct.TargetPayment)
            .AsNoTracking()
            .Where(ct => ct.MembershipNumber == membershipNumber)
            .OrderByDescending(ct => ct.Date)
            .ThenByDescending(ct => ct.Id)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<MemberCreditSummary>> GetMembersWithCreditAsync()
    {
        var credits = await _context.MemberCredits
            .AsNoTracking()
            .Where(mc => mc.Balance > 0)
            .ToListAsync();

        var membershipNumbers = credits.Select(mc => mc.MembershipNumber).ToList();
        var persons = await _context.Persons
            .AsNoTracking()
            .Where(p => membershipNumbers.Contains(p.MembershipNumber))
            .ToDictionaryAsync(p => p.MembershipNumber, p => p.FullName);

        return credits.Select(mc => new MemberCreditSummary
        {
            MembershipNumber = mc.MembershipNumber,
            MemberName = persons.GetValueOrDefault(mc.MembershipNumber),
            CreditBalance = mc.Balance,
            LastUpdated = mc.LastUpdated
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> ConvertPaymentToCreditAsync(
        int paymentId, decimal amount, string reason)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null)
        {
            return (false, "Payment not found.");
        }

        if (payment.Status != PaymentStatus.Paid)
        {
            return (false, "Only paid payments can be converted to credit.");
        }

        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.");
        }

        if (amount > payment.AmountPaid)
        {
            return (false, $"Amount ({amount:C}) exceeds amount paid ({payment.AmountPaid:C}).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "A reason is required.");
        }

        // Create accounting entry
        int? transactionId = null;
        if (_accountingService != null)
        {
            var member = await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MembershipNumber == payment.MembershipNumber);
            var memberName = member?.FullName ?? payment.MembershipNumber;

            var description = $"Convert to credit - {memberName} - {payment.Reference}";

            var result = await _accountingService.RecordConvertToCreditEntryAsync(
                payment.Id,
                amount,
                payment.PaymentType,
                description,
                DateTime.Today,
                payment.IncomeAccountId);

            if (!result.Success)
            {
                return (false, $"Failed to create accounting entry: {result.ErrorMessage}");
            }

            transactionId = result.TransactionId;
        }

        // Mark payment as refunded (money leaves the payment into credit pool)
        payment.Status = PaymentStatus.Refunded;
        payment.RefundAmount = amount;
        payment.RefundDate = DateTime.Today;

        var note = $"CONVERTED TO CREDIT ({DateTime.Today:d}): {reason}";
        payment.Notes = string.IsNullOrWhiteSpace(payment.Notes)
            ? note
            : $"{payment.Notes}\n{note}";

        // Create or update member credit balance
        var memberCredit = await _context.MemberCredits
            .FirstOrDefaultAsync(mc => mc.MembershipNumber == payment.MembershipNumber);

        if (memberCredit == null)
        {
            memberCredit = new MemberCredit
            {
                MembershipNumber = payment.MembershipNumber,
                Balance = amount,
                LastUpdated = DateTime.Now
            };
            _context.MemberCredits.Add(memberCredit);
        }
        else
        {
            memberCredit.Balance += amount;
            memberCredit.LastUpdated = DateTime.Now;
        }

        // Record credit transaction for audit trail
        _context.CreditTransactions.Add(new CreditTransaction
        {
            MembershipNumber = payment.MembershipNumber,
            Amount = amount,
            Type = CreditTransactionType.Deposit,
            SourcePaymentId = payment.Id,
            TransactionId = transactionId,
            Date = DateTime.Today,
            Notes = $"Converted from {payment.Reference}: {reason}"
        });

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> ApplyCreditToPaymentAsync(
        string membershipNumber, int targetPaymentId, decimal amount)
    {
        var memberCredit = await _context.MemberCredits
            .FirstOrDefaultAsync(mc => mc.MembershipNumber == membershipNumber);

        if (memberCredit == null || memberCredit.Balance <= 0)
        {
            return (false, "No credit balance available.");
        }

        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.");
        }

        if (amount > memberCredit.Balance)
        {
            return (false, $"Amount ({amount:C}) exceeds available credit ({memberCredit.Balance:C}).");
        }

        var payment = await _context.Payments.FindAsync(targetPaymentId);
        if (payment == null)
        {
            return (false, "Target payment not found.");
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return (false, "Credit can only be applied to pending payments.");
        }

        if (payment.MembershipNumber != membershipNumber)
        {
            return (false, "Credit can only be applied to the same member's payments.");
        }

        var outstanding = payment.OutstandingBalance;
        if (amount > outstanding)
        {
            return (false, $"Amount ({amount:C}) exceeds outstanding balance ({outstanding:C}).");
        }

        // Create accounting entry
        int? transactionId = null;
        if (_accountingService != null)
        {
            var member = await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);
            var memberName = member?.FullName ?? membershipNumber;

            var description = $"Credit applied - {memberName} - {payment.Reference}";

            var result = await _accountingService.RecordApplyCreditEntryAsync(
                payment.Id,
                amount,
                payment.PaymentType,
                description,
                DateTime.Today,
                payment.IncomeAccountId);

            if (!result.Success)
            {
                return (false, $"Failed to create accounting entry: {result.ErrorMessage}");
            }

            transactionId = result.TransactionId;
        }

        // Apply credit to payment
        payment.AmountPaid += amount;
        payment.CreditApplied += amount;

        var creditNote = $"Credit applied: {amount:C} ({DateTime.Today:d})";
        payment.Notes = string.IsNullOrWhiteSpace(payment.Notes)
            ? creditNote
            : $"{payment.Notes}\n{creditNote}";

        // Auto-complete if fully paid
        if (payment.AmountPaid >= payment.Amount)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaymentDate = DateTime.Today;
        }

        // Reduce credit balance
        memberCredit.Balance -= amount;
        memberCredit.LastUpdated = DateTime.Now;

        // Record credit transaction
        _context.CreditTransactions.Add(new CreditTransaction
        {
            MembershipNumber = membershipNumber,
            Amount = -amount,
            Type = CreditTransactionType.Applied,
            TargetPaymentId = payment.Id,
            TransactionId = transactionId,
            Date = DateTime.Today,
            Notes = $"Applied to {payment.Reference}"
        });

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> RefundCreditAsync(
        string membershipNumber, decimal amount, PaymentMethod method, DateTime refundDate, string reason)
    {
        var memberCredit = await _context.MemberCredits
            .FirstOrDefaultAsync(mc => mc.MembershipNumber == membershipNumber);

        if (memberCredit == null || memberCredit.Balance <= 0)
        {
            return (false, "No credit balance available.");
        }

        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.");
        }

        if (amount > memberCredit.Balance)
        {
            return (false, $"Amount ({amount:C}) exceeds available credit ({memberCredit.Balance:C}).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "A reason is required.");
        }

        // Create accounting entry
        int? transactionId = null;
        if (_accountingService != null)
        {
            var member = await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);
            var memberName = member?.FullName ?? membershipNumber;

            var description = $"Credit refund - {memberName}: {reason}";

            var result = await _accountingService.RecordRefundCreditEntryAsync(
                amount, method, description, refundDate);

            if (!result.Success)
            {
                return (false, $"Failed to create accounting entry: {result.ErrorMessage}");
            }

            transactionId = result.TransactionId;
        }

        // Reduce credit balance
        memberCredit.Balance -= amount;
        memberCredit.LastUpdated = DateTime.Now;

        // Record credit transaction
        _context.CreditTransactions.Add(new CreditTransaction
        {
            MembershipNumber = membershipNumber,
            Amount = -amount,
            Type = CreditTransactionType.Refunded,
            TransactionId = transactionId,
            Date = refundDate,
            Notes = $"Refunded via {method}: {reason}"
        });

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, List<BulkCreditResult> Results)> BulkConvertMeetingToCreditAsync(
        int meetingId, bool autoApplyToSubs, string reason)
    {
        var results = new List<BulkCreditResult>();

        // Get all paid activity payments for this meeting
        var payments = await _context.Payments
            .Where(p => p.MeetingId == meetingId && p.Status == PaymentStatus.Paid)
            .ToListAsync();

        if (!payments.Any())
        {
            return (false, "No paid payments found for this meeting.", results);
        }

        foreach (var payment in payments)
        {
            var member = await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MembershipNumber == payment.MembershipNumber);
            var memberName = member?.FullName ?? payment.MembershipNumber;

            var result = new BulkCreditResult
            {
                MembershipNumber = payment.MembershipNumber,
                MemberName = memberName,
                CreditAmount = payment.AmountPaid
            };

            // Convert to credit
            var convertResult = await ConvertPaymentToCreditAsync(payment.Id, payment.AmountPaid, reason);
            if (!convertResult.Success)
            {
                result.Success = false;
                result.Message = convertResult.ErrorMessage;
                result.RemainingCredit = payment.AmountPaid;
                results.Add(result);
                continue;
            }

            result.RemainingCredit = payment.AmountPaid;

            // Optionally auto-apply to oldest pending subs
            if (autoApplyToSubs)
            {
                var pendingSubs = await _context.Payments
                    .Where(p => p.MembershipNumber == payment.MembershipNumber
                        && p.Status == PaymentStatus.Pending
                        && p.PaymentType == PaymentType.Subs)
                    .OrderBy(p => p.DueDate)
                    .FirstOrDefaultAsync();

                if (pendingSubs != null)
                {
                    var applyAmount = Math.Min(payment.AmountPaid, pendingSubs.OutstandingBalance);
                    if (applyAmount > 0)
                    {
                        var applyResult = await ApplyCreditToPaymentAsync(
                            payment.MembershipNumber, pendingSubs.Id, applyAmount);

                        if (applyResult.Success)
                        {
                            result.AppliedToSubs = applyAmount;
                            result.RemainingCredit = payment.AmountPaid - applyAmount;
                        }
                    }
                }
            }

            result.Success = true;
            result.Message = result.AppliedToSubs > 0
                ? $"Converted {result.CreditAmount:C} to credit, {result.AppliedToSubs:C} applied to subs"
                : $"Converted {result.CreditAmount:C} to credit";

            results.Add(result);
        }

        return (true, string.Empty, results);
    }
}
