using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

/// <summary>
/// Service for managing payments, termly subscriptions, and activity payments.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ITermService _termService;
    private readonly IConfigurationService _configurationService;
    private readonly IAccountingService? _accountingService;

    public PaymentService(
        ApplicationDbContext context,
        ITermService termService,
        IConfigurationService configurationService,
        IAccountingService? accountingService = null)
    {
        _context = context;
        _termService = termService;
        _configurationService = configurationService;
        _accountingService = accountingService;
    }

    // ===== CRUD Operations =====

    /// <inheritdoc/>
    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc/>
    public async Task<List<Payment>> GetAllAsync()
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, Payment? Payment)> CreateAsync(Payment payment)
    {
        if (string.IsNullOrWhiteSpace(payment.MembershipNumber))
        {
            return (false, "Membership number is required.", null);
        }

        if (payment.Amount <= 0)
        {
            return (false, "Amount must be greater than zero.", null);
        }

        if (string.IsNullOrWhiteSpace(payment.Reference))
        {
            return (false, "Reference is required.", null);
        }

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return (true, string.Empty, payment);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateAsync(Payment payment)
    {
        var existingPayment = await _context.Payments.FindAsync(payment.Id);
        if (existingPayment == null)
        {
            return (false, "Payment not found.");
        }

        // Don't allow updating if already paid or cancelled
        if (existingPayment.Status == PaymentStatus.Paid)
        {
            return (false, "Cannot update a payment that has been fully paid.");
        }

        if (existingPayment.Status == PaymentStatus.Cancelled)
        {
            return (false, "Cannot update a cancelled payment.");
        }

        existingPayment.Amount = payment.Amount;
        existingPayment.DueDate = payment.DueDate;
        existingPayment.Reference = payment.Reference;
        existingPayment.Notes = payment.Notes;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteAsync(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null)
        {
            return (false, "Payment not found.");
        }

        // Don't allow deleting if any amount has been paid
        if (payment.AmountPaid > 0)
        {
            return (false, "Cannot delete a payment that has received any payment. Cancel it instead.");
        }

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Query Methods =====

    /// <inheritdoc/>
    public async Task<List<Payment>> GetByStatusAsync(PaymentStatus status)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Payment>> GetByMembershipNumberAsync(string membershipNumber)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .Where(p => p.MembershipNumber == membershipNumber)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Payment>> GetByTermAsync(int termId)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .Where(p => p.TermId == termId)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Payment>> GetByMeetingAsync(int meetingId)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .Where(p => p.MeetingId == meetingId)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Payment>> GetOverduePaymentsAsync()
    {
        var today = DateTime.Today;
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .Where(p => p.Status == PaymentStatus.Pending && p.DueDate < today)
            .OrderBy(p => p.DueDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Payment>> GetPendingPaymentsForMemberAsync(string membershipNumber)
    {
        return await _context.Payments
            .AsNoTracking()
            .Include(p => p.Term)
            .Include(p => p.Meeting)
            .Where(p => p.MembershipNumber == membershipNumber && p.Status == PaymentStatus.Pending)
            .OrderBy(p => p.DueDate)
            .ToListAsync();
    }

    // ===== Termly Subscriptions =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, int PaymentsCreated)> GenerateTermlySubsAsync(int termId)
    {
        var term = await _termService.GetByIdAsync(termId);
        if (term == null)
        {
            return (false, "Term not found.", 0);
        }

        if (term.SubsAmount <= 0)
        {
            return (false, "Term subscription amount must be greater than zero.", 0);
        }

        // Get all active girls (not leaders)
        var activeGirls = await _context.Persons
            .AsNoTracking()
            .Where(p => p.IsActive && p.PersonType == PersonType.Girl && !p.IsDataRemoved)
            .ToListAsync();

        if (!activeGirls.Any())
        {
            return (false, "No active girls found to generate subscriptions for.", 0);
        }

        // Get configuration for payment term days
        var config = await _configurationService.GetConfigurationAsync();
        var dueDate = term.StartDate.AddDays(config.PaymentTermDays);

        // Get existing subs payments for this term to avoid duplicates
        var existingMembershipNumbers = await _context.Payments
            .Where(p => p.TermId == termId && p.PaymentType == PaymentType.Subs)
            .Select(p => p.MembershipNumber)
            .ToListAsync();

        var paymentsCreated = 0;

        foreach (var girl in activeGirls)
        {
            // Skip if payment already exists
            if (existingMembershipNumbers.Contains(girl.MembershipNumber))
            {
                continue;
            }

            var payment = new Payment
            {
                MembershipNumber = girl.MembershipNumber,
                Amount = term.SubsAmount,
                PaymentType = PaymentType.Subs,
                DueDate = dueDate,
                Status = PaymentStatus.Pending,
                Reference = $"{term.Name} Subs - {girl.FullName ?? girl.MembershipNumber}",
                TermId = termId
            };

            _context.Payments.Add(payment);
            paymentsCreated++;
        }

        if (paymentsCreated > 0)
        {
            await _context.SaveChangesAsync();
        }

        return (true, string.Empty, paymentsCreated);
    }

    /// <inheritdoc/>
    public async Task<bool> HasTermlySubsBeenGeneratedAsync(int termId)
    {
        return await _context.Payments
            .AnyAsync(p => p.TermId == termId && p.PaymentType == PaymentType.Subs);
    }

    /// <inheritdoc/>
    public async Task<int> GetEligibleMembersCountForTermAsync(int termId)
    {
        // Get existing subs payments for this term
        var existingMembershipNumbers = await _context.Payments
            .Where(p => p.TermId == termId && p.PaymentType == PaymentType.Subs)
            .Select(p => p.MembershipNumber)
            .ToListAsync();

        // Count active girls who don't already have a payment
        return await _context.Persons
            .CountAsync(p => p.IsActive &&
                           p.PersonType == PersonType.Girl &&
                           !p.IsDataRemoved &&
                           !existingMembershipNumbers.Contains(p.MembershipNumber));
    }

    // ===== Activity Payments =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, Payment? Payment)> CreateActivityPaymentAsync(
        int meetingId,
        string membershipNumber)
    {
        var meeting = await _context.Meetings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
        {
            return (false, "Meeting not found.", null);
        }

        if (!meeting.CostPerAttendee.HasValue || meeting.CostPerAttendee.Value <= 0)
        {
            return (false, "Meeting has no cost defined.", null);
        }

        // Check if payment already exists
        if (await HasActivityPaymentAsync(meetingId, membershipNumber))
        {
            return (false, "Activity payment already exists for this member and meeting.", null);
        }

        // Get member name for reference
        var member = await _context.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);

        var memberName = member?.FullName ?? membershipNumber;

        // Determine due date
        var dueDate = meeting.PaymentDeadline ?? meeting.Date;

        var payment = new Payment
        {
            MembershipNumber = membershipNumber,
            Amount = meeting.CostPerAttendee.Value,
            PaymentType = PaymentType.Activity,
            DueDate = dueDate,
            Status = PaymentStatus.Pending,
            Reference = $"{meeting.Title} - {memberName}",
            MeetingId = meetingId
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return (true, string.Empty, payment);
    }

    /// <inheritdoc/>
    public async Task<bool> HasActivityPaymentAsync(int meetingId, string membershipNumber)
    {
        return await _context.Payments
            .AnyAsync(p => p.MeetingId == meetingId &&
                          p.MembershipNumber == membershipNumber &&
                          p.PaymentType == PaymentType.Activity);
    }

    // ===== Payment Recording =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> RecordPaymentAsync(
        int paymentId,
        decimal amount,
        PaymentMethod paymentMethod,
        DateTime? paymentDate = null,
        string? notes = null)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null)
        {
            return (false, "Payment not found.");
        }

        if (payment.Status == PaymentStatus.Cancelled)
        {
            return (false, "Cannot record payment against a cancelled payment.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return (false, "This payment has already been fully paid.");
        }

        if (amount <= 0)
        {
            return (false, "Payment amount must be greater than zero.");
        }

        var outstanding = payment.OutstandingBalance;
        if (amount > outstanding)
        {
            return (false, $"Payment amount ({amount:C}) exceeds outstanding balance ({outstanding:C}).");
        }

        // Record the payment
        payment.AmountPaid += amount;
        payment.PaymentMethod = paymentMethod;
        payment.PaymentDate = paymentDate ?? DateTime.Today;

        // Append notes if provided
        if (!string.IsNullOrWhiteSpace(notes))
        {
            payment.Notes = string.IsNullOrWhiteSpace(payment.Notes)
                ? notes
                : $"{payment.Notes}\n{paymentDate ?? DateTime.Today:d}: {notes}";
        }

        // Auto-set status to Paid when fully paid
        if (payment.AmountPaid >= payment.Amount)
        {
            payment.Status = PaymentStatus.Paid;
        }

        await _context.SaveChangesAsync();

        // Create accounting entry if accounting service is available
        if (_accountingService != null)
        {
            // Get member name for description
            var member = await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MembershipNumber == payment.MembershipNumber);
            var memberName = member?.FullName ?? payment.MembershipNumber;

            var description = $"Payment from {memberName} - {payment.Reference}";

            await _accountingService.RecordPaymentEntryAsync(
                payment.Id,
                amount,
                paymentMethod,
                payment.PaymentType,
                description,
                paymentDate ?? DateTime.Today);
        }

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> CancelPaymentAsync(int paymentId, string reason)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null)
        {
            return (false, "Payment not found.");
        }

        if (payment.Status == PaymentStatus.Cancelled)
        {
            return (false, "Payment is already cancelled.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return (false, "Cannot cancel a fully paid payment.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "Cancellation reason is required.");
        }

        payment.Status = PaymentStatus.Cancelled;

        // Append cancellation reason to notes
        var cancellationNote = $"CANCELLED ({DateTime.Now:d}): {reason}";
        payment.Notes = string.IsNullOrWhiteSpace(payment.Notes)
            ? cancellationNote
            : $"{payment.Notes}\n{cancellationNote}";

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Statistics =====

    /// <inheritdoc/>
    public async Task<PaymentTermStats> GetTermPaymentStatsAsync(int termId)
    {
        var term = await _termService.GetByIdAsync(termId);

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.TermId == termId)
            .ToListAsync();

        var today = DateTime.Today;
        var overdueCount = payments.Count(p => p.Status == PaymentStatus.Pending && p.DueDate < today);

        return new PaymentTermStats
        {
            TermId = termId,
            TermName = term?.Name,
            TotalPayments = payments.Count,
            PendingPayments = payments.Count(p => p.Status == PaymentStatus.Pending),
            PaidPayments = payments.Count(p => p.Status == PaymentStatus.Paid),
            CancelledPayments = payments.Count(p => p.Status == PaymentStatus.Cancelled),
            OverduePayments = overdueCount,
            TotalAmount = payments.Where(p => p.Status != PaymentStatus.Cancelled).Sum(p => p.Amount),
            TotalPaid = payments.Sum(p => p.AmountPaid),
            TotalOutstanding = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.OutstandingBalance)
        };
    }

    /// <inheritdoc/>
    public async Task<MemberPaymentSummary> GetMemberPaymentSummaryAsync(string membershipNumber)
    {
        var member = await _context.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.MembershipNumber == membershipNumber)
            .ToListAsync();

        var today = DateTime.Today;

        return new MemberPaymentSummary
        {
            MembershipNumber = membershipNumber,
            MemberName = member?.FullName,
            TotalPayments = payments.Count,
            PendingPayments = payments.Count(p => p.Status == PaymentStatus.Pending),
            OverduePayments = payments.Count(p => p.Status == PaymentStatus.Pending && p.DueDate < today),
            TotalOwed = payments.Where(p => p.Status != PaymentStatus.Cancelled).Sum(p => p.Amount),
            TotalPaid = payments.Sum(p => p.AmountPaid),
            TotalOutstanding = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.OutstandingBalance)
        };
    }

    /// <inheritdoc/>
    public async Task<PaymentDashboardStats> GetDashboardStatsAsync()
    {
        var today = DateTime.Today;

        // Get current term
        var currentTerm = await _termService.GetCurrentTermAsync();

        var allPendingPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Pending)
            .ToListAsync();

        var currentTermPayments = currentTerm != null
            ? await _context.Payments
                .AsNoTracking()
                .Where(p => p.TermId == currentTerm.Id && p.Status == PaymentStatus.Paid)
                .ToListAsync()
            : new List<Payment>();

        return new PaymentDashboardStats
        {
            PendingCount = allPendingPayments.Count,
            OverdueCount = allPendingPayments.Count(p => p.DueDate < today),
            PaidThisTermCount = currentTermPayments.Count,
            TotalOutstanding = allPendingPayments.Sum(p => p.OutstandingBalance),
            TotalPaidThisTerm = currentTermPayments.Sum(p => p.AmountPaid)
        };
    }
}
