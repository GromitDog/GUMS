using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

/// <summary>
/// Service for managing accounting records (cash accounting basis).
/// </summary>
public class AccountingService : IAccountingService
{
    private readonly ApplicationDbContext _context;
    private readonly ITermService _termService;

    // Default account codes
    public const string CashOnHandCode = "1001";
    public const string ChequesPendingCode = "1002";
    public const string BankAccountCode = "1003";
    public const string SubsIncomeCode = "4001";
    public const string ActivityIncomeCode = "4002";
    public const string SuppliesExpenseCode = "5001";
    public const string EquipmentExpenseCode = "5002";
    public const string VenueHireExpenseCode = "5003";
    public const string ActivitiesEventsExpenseCode = "5004";
    public const string BadgesAwardsExpenseCode = "5005";
    public const string OtherExpensesCode = "5099";

    public AccountingService(ApplicationDbContext context, ITermService termService)
    {
        _context = context;
        _termService = termService;
    }

    // ===== Account Operations =====

    /// <inheritdoc/>
    public async Task<List<Account>> GetAccountsAsync()
    {
        return await _context.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Code)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Account?> GetAccountByIdAsync(int id)
    {
        return await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <inheritdoc/>
    public async Task<Account?> GetAccountByCodeAsync(string code)
    {
        return await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == code);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetCashOnHandAsync()
    {
        var account = await GetAccountByCodeAsync(CashOnHandCode);
        return account?.Balance ?? 0;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetBankBalanceAsync()
    {
        var account = await GetAccountByCodeAsync(BankAccountCode);
        return account?.Balance ?? 0;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetChequesPendingAsync()
    {
        var account = await GetAccountByCodeAsync(ChequesPendingCode);
        return account?.Balance ?? 0;
    }

    // ===== Transaction Operations =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, Transaction? Transaction)> CreateTransactionAsync(Transaction transaction)
    {
        if (string.IsNullOrWhiteSpace(transaction.Description))
        {
            return (false, "Transaction description is required.", null);
        }

        if (transaction.Lines == null || !transaction.Lines.Any())
        {
            return (false, "Transaction must have at least one line.", null);
        }

        // Validate debits = credits
        var totalDebits = transaction.Lines.Sum(l => l.Debit);
        var totalCredits = transaction.Lines.Sum(l => l.Credit);

        if (totalDebits != totalCredits)
        {
            return (false, $"Transaction is not balanced. Debits ({totalDebits:C}) must equal credits ({totalCredits:C}).", null);
        }

        // Validate all accounts exist
        var accountIds = transaction.Lines.Select(l => l.AccountId).Distinct().ToList();
        var existingAccounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToListAsync();

        if (existingAccounts.Count != accountIds.Count)
        {
            return (false, "One or more accounts do not exist.", null);
        }

        // Create transaction
        _context.Transactions.Add(transaction);

        // Update account balances
        foreach (var line in transaction.Lines)
        {
            var account = existingAccounts.First(a => a.Id == line.AccountId);

            // For asset/expense accounts: Debit increases, Credit decreases
            // For income accounts: Credit increases, Debit decreases
            if (account.Type == AccountType.Asset || account.Type == AccountType.Expense)
            {
                account.Balance += line.Debit - line.Credit;
            }
            else // Income
            {
                account.Balance += line.Credit - line.Debit;
            }
        }

        await _context.SaveChangesAsync();

        return (true, string.Empty, transaction);
    }

    /// <inheritdoc/>
    public async Task<List<Transaction>> GetTransactionsAsync(DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Lines)
            .ThenInclude(tl => tl.Account)
            .Include(t => t.Payment)
            .AsQueryable();

        if (dateFrom.HasValue)
        {
            query = query.Where(t => t.Date >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(t => t.Date <= dateTo.Value);
        }

        return await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Transaction?> GetTransactionByIdAsync(int id)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Lines)
            .ThenInclude(tl => tl.Account)
            .Include(t => t.Payment)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <inheritdoc/>
    public async Task<List<Transaction>> GetTransactionsForPaymentAsync(int paymentId)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Lines)
            .ThenInclude(tl => tl.Account)
            .Where(t => t.PaymentId == paymentId)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    // ===== Payment Recording Integration =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> RecordPaymentEntryAsync(
        int paymentId,
        decimal amount,
        PaymentMethod paymentMethod,
        PaymentType paymentType,
        string description,
        DateTime date)
    {
        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.");
        }

        // Determine asset account based on payment method
        var assetAccountCode = paymentMethod switch
        {
            PaymentMethod.Cash => CashOnHandCode,
            PaymentMethod.Cheque => ChequesPendingCode,
            PaymentMethod.BankTransfer => BankAccountCode,
            _ => throw new ArgumentException($"Unknown payment method: {paymentMethod}")
        };

        // Determine income account based on payment type
        var incomeAccountCode = paymentType switch
        {
            PaymentType.Subs => SubsIncomeCode,
            PaymentType.Activity => ActivityIncomeCode,
            _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
        };

        var assetAccount = await GetAccountByCodeAsync(assetAccountCode);
        var incomeAccount = await GetAccountByCodeAsync(incomeAccountCode);

        if (assetAccount == null || incomeAccount == null)
        {
            return (false, "Required accounts not found. Please ensure default accounts have been created.");
        }

        var transaction = new Transaction
        {
            Date = date,
            Description = description,
            PaymentId = paymentId,
            Lines = new List<TransactionLine>
            {
                new TransactionLine
                {
                    AccountId = assetAccount.Id,
                    Debit = amount,
                    Credit = 0
                },
                new TransactionLine
                {
                    AccountId = incomeAccount.Id,
                    Debit = 0,
                    Credit = amount
                }
            }
        };

        var result = await CreateTransactionAsync(transaction);
        return (result.Success, result.ErrorMessage);
    }

    // ===== Banking Operations =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> BankDepositAsync(
        decimal cashAmount,
        decimal chequeAmount,
        DateTime date,
        string? notes = null)
    {
        if (cashAmount < 0 || chequeAmount < 0)
        {
            return (false, "Amounts cannot be negative.");
        }

        if (cashAmount == 0 && chequeAmount == 0)
        {
            return (false, "At least one amount must be greater than zero.");
        }

        var cashAccount = await GetAccountByCodeAsync(CashOnHandCode);
        var chequeAccount = await GetAccountByCodeAsync(ChequesPendingCode);
        var bankAccount = await GetAccountByCodeAsync(BankAccountCode);

        if (cashAccount == null || chequeAccount == null || bankAccount == null)
        {
            return (false, "Required accounts not found. Please ensure default accounts have been created.");
        }

        // Validate sufficient balances
        if (cashAmount > 0 && cashAccount.Balance < cashAmount)
        {
            return (false, $"Insufficient cash on hand. Available: {cashAccount.Balance:C}");
        }

        if (chequeAmount > 0 && chequeAccount.Balance < chequeAmount)
        {
            return (false, $"Insufficient cheques pending. Available: {chequeAccount.Balance:C}");
        }

        var totalDeposit = cashAmount + chequeAmount;
        var description = notes ?? $"Bank deposit - Cash: {cashAmount:C}, Cheques: {chequeAmount:C}";

        var lines = new List<TransactionLine>
        {
            // Debit Bank Account (increase)
            new TransactionLine
            {
                AccountId = bankAccount.Id,
                Debit = totalDeposit,
                Credit = 0
            }
        };

        // Credit Cash on Hand (decrease) if applicable
        if (cashAmount > 0)
        {
            lines.Add(new TransactionLine
            {
                AccountId = cashAccount.Id,
                Debit = 0,
                Credit = cashAmount
            });
        }

        // Credit Cheques Pending (decrease) if applicable
        if (chequeAmount > 0)
        {
            lines.Add(new TransactionLine
            {
                AccountId = chequeAccount.Id,
                Debit = 0,
                Credit = chequeAmount
            });
        }

        var transaction = new Transaction
        {
            Date = date,
            Description = description,
            Lines = lines
        };

        var result = await CreateTransactionAsync(transaction);
        return (result.Success, result.ErrorMessage);
    }

    // ===== Expense Account Management =====

    /// <inheritdoc/>
    public async Task<List<Account>> GetExpenseAccountsAsync()
    {
        return await _context.Accounts
            .AsNoTracking()
            .Where(a => a.Type == AccountType.Expense)
            .OrderBy(a => a.Code)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, Account? Account)> CreateExpenseAccountAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Account name is required.", null);
        }

        // Auto-assign next available code in 5xxx range
        var maxCode = await _context.Accounts
            .Where(a => a.Type == AccountType.Expense)
            .Select(a => a.Code)
            .ToListAsync();

        var nextNumber = 5001;
        if (maxCode.Any())
        {
            var usedNumbers = maxCode
                .Select(c => int.TryParse(c, out var n) ? n : 0)
                .Where(n => n >= 5000 && n < 6000)
                .OrderBy(n => n)
                .ToList();

            if (usedNumbers.Any())
            {
                nextNumber = usedNumbers.Max() + 1;
                // Skip 5099 range gap if needed
                if (nextNumber == 5099)
                    nextNumber = 5100;
            }
        }

        var account = new Account
        {
            Code = nextNumber.ToString(),
            Name = name,
            Type = AccountType.Expense,
            IsSystem = false
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return (true, string.Empty, account);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateExpenseAccountAsync(int accountId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Account name is required.");
        }

        var account = await _context.Accounts.FindAsync(accountId);
        if (account == null)
        {
            return (false, "Account not found.");
        }

        if (account.Type != AccountType.Expense)
        {
            return (false, "Only expense accounts can be updated.");
        }

        account.Name = name;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteExpenseAccountAsync(int accountId)
    {
        var account = await _context.Accounts
            .Include(a => a.TransactionLines)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
        {
            return (false, "Account not found.");
        }

        if (account.Type != AccountType.Expense)
        {
            return (false, "Only expense accounts can be deleted.");
        }

        if (account.TransactionLines.Any())
        {
            return (false, "Cannot delete account with existing transactions.");
        }

        // Also check if any expenses reference this account
        var hasExpenses = await _context.Expenses.AnyAsync(e => e.ExpenseAccountId == accountId);
        if (hasExpenses)
        {
            return (false, "Cannot delete account with existing expenses.");
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Direct Expense Recording =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, Expense? Expense)> RecordDirectExpenseAsync(Expense expense)
    {
        if (expense.Amount <= 0)
        {
            return (false, "Amount must be greater than zero.", null);
        }

        if (string.IsNullOrWhiteSpace(expense.Description))
        {
            return (false, "Description is required.", null);
        }

        if (!expense.PaidFromAccountId.HasValue)
        {
            return (false, "Paid from account is required for direct expenses.", null);
        }

        var expenseAccount = await _context.Accounts.FindAsync(expense.ExpenseAccountId);
        if (expenseAccount == null || expenseAccount.Type != AccountType.Expense)
        {
            return (false, "Invalid expense category.", null);
        }

        var assetAccount = await _context.Accounts.FindAsync(expense.PaidFromAccountId.Value);
        if (assetAccount == null || assetAccount.Type != AccountType.Asset)
        {
            return (false, "Invalid payment account.", null);
        }

        // Create the accounting transaction: Debit Expense, Credit Asset
        var transaction = new Transaction
        {
            Date = expense.Date,
            Description = $"Expense: {expense.Description}",
            Lines = new List<TransactionLine>
            {
                new TransactionLine
                {
                    AccountId = expenseAccount.Id,
                    Debit = expense.Amount,
                    Credit = 0
                },
                new TransactionLine
                {
                    AccountId = assetAccount.Id,
                    Debit = 0,
                    Credit = expense.Amount
                }
            }
        };

        var txResult = await CreateTransactionAsync(transaction);
        if (!txResult.Success)
        {
            return (false, txResult.ErrorMessage, null);
        }

        expense.TransactionId = txResult.Transaction!.Id;
        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        return (true, string.Empty, expense);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteDirectExpenseAsync(int expenseId)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
        {
            return (false, "Expense not found.");
        }

        if (expense.ExpenseClaimId.HasValue)
        {
            return (false, "Cannot delete an expense that is part of a claim. Remove it from the claim first.");
        }

        // Reverse the transaction
        if (expense.TransactionId.HasValue)
        {
            var transaction = await _context.Transactions
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.Id == expense.TransactionId.Value);

            if (transaction != null)
            {
                // Reverse account balances
                var accountIds = transaction.Lines.Select(l => l.AccountId).Distinct().ToList();
                var accounts = await _context.Accounts
                    .Where(a => accountIds.Contains(a.Id))
                    .ToListAsync();

                foreach (var line in transaction.Lines)
                {
                    var account = accounts.First(a => a.Id == line.AccountId);
                    if (account.Type == AccountType.Asset || account.Type == AccountType.Expense)
                    {
                        account.Balance -= line.Debit - line.Credit;
                    }
                    else
                    {
                        account.Balance -= line.Credit - line.Debit;
                    }
                }

                _context.Transactions.Remove(transaction);
            }
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Expense Queries =====

    /// <inheritdoc/>
    public async Task<List<Expense>> GetExpensesAsync(DateTime? dateFrom = null, DateTime? dateTo = null, int? expenseAccountId = null, int? meetingId = null)
    {
        var query = _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseAccount)
            .Include(e => e.PaidFromAccount)
            .Include(e => e.Meeting)
            .Include(e => e.ExpenseClaim)
            .AsQueryable();

        if (dateFrom.HasValue)
            query = query.Where(e => e.Date >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(e => e.Date <= dateTo.Value);

        if (expenseAccountId.HasValue)
            query = query.Where(e => e.ExpenseAccountId == expenseAccountId.Value);

        if (meetingId.HasValue)
            query = query.Where(e => e.MeetingId == meetingId.Value);

        return await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Expense?> GetExpenseByIdAsync(int id)
    {
        return await _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseAccount)
            .Include(e => e.PaidFromAccount)
            .Include(e => e.Meeting)
            .Include(e => e.ExpenseClaim)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    // ===== Reimbursement Claims =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, ExpenseClaim? Claim)> CreateExpenseClaimAsync(ExpenseClaim claim)
    {
        if (string.IsNullOrWhiteSpace(claim.ClaimedBy))
        {
            return (false, "Claimant name is required.", null);
        }

        claim.Status = ExpenseClaimStatus.Draft;
        _context.ExpenseClaims.Add(claim);
        await _context.SaveChangesAsync();

        return (true, string.Empty, claim);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> AddExpenseToClaimAsync(int claimId, Expense expense)
    {
        var claim = await _context.ExpenseClaims.FindAsync(claimId);
        if (claim == null)
        {
            return (false, "Expense claim not found.");
        }

        if (claim.Status != ExpenseClaimStatus.Draft)
        {
            return (false, "Can only add expenses to draft claims.");
        }

        if (expense.Amount <= 0)
        {
            return (false, "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(expense.Description))
        {
            return (false, "Description is required.");
        }

        var expenseAccount = await _context.Accounts.FindAsync(expense.ExpenseAccountId);
        if (expenseAccount == null || expenseAccount.Type != AccountType.Expense)
        {
            return (false, "Invalid expense category.");
        }

        expense.ExpenseClaimId = claimId;
        expense.PaidFromAccountId = null; // Reimbursement - not paid from unit funds yet
        expense.TransactionId = null;

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> RemoveExpenseFromClaimAsync(int expenseId)
    {
        var expense = await _context.Expenses
            .Include(e => e.ExpenseClaim)
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
        {
            return (false, "Expense not found.");
        }

        if (!expense.ExpenseClaimId.HasValue)
        {
            return (false, "Expense is not part of a claim.");
        }

        if (expense.ExpenseClaim?.Status != ExpenseClaimStatus.Draft)
        {
            return (false, "Can only remove expenses from draft claims.");
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> SettleExpenseClaimAsync(int claimId, int paidFromAccountId, PaymentMethod paymentMethod, DateTime settledDate)
    {
        var claim = await _context.ExpenseClaims
            .Include(ec => ec.Expenses)
            .ThenInclude(e => e.ExpenseAccount)
            .FirstOrDefaultAsync(ec => ec.Id == claimId);

        if (claim == null)
        {
            return (false, "Expense claim not found.");
        }

        if (claim.Status == ExpenseClaimStatus.Settled)
        {
            return (false, "Claim has already been settled.");
        }

        if (!claim.Expenses.Any())
        {
            return (false, "Cannot settle a claim with no expenses.");
        }

        var assetAccount = await _context.Accounts.FindAsync(paidFromAccountId);
        if (assetAccount == null || assetAccount.Type != AccountType.Asset)
        {
            return (false, "Invalid payment account.");
        }

        var totalAmount = claim.Expenses.Sum(e => e.Amount);

        // Create multi-line transaction: Debit each expense category, Credit asset account
        var lines = new List<TransactionLine>();

        // Group expenses by category for cleaner transaction
        var groupedExpenses = claim.Expenses
            .GroupBy(e => e.ExpenseAccountId)
            .ToList();

        foreach (var group in groupedExpenses)
        {
            lines.Add(new TransactionLine
            {
                AccountId = group.Key,
                Debit = group.Sum(e => e.Amount),
                Credit = 0
            });
        }

        lines.Add(new TransactionLine
        {
            AccountId = paidFromAccountId,
            Debit = 0,
            Credit = totalAmount
        });

        var transaction = new Transaction
        {
            Date = settledDate,
            Description = $"Expense claim settlement - {claim.ClaimedBy}",
            Lines = lines
        };

        var txResult = await CreateTransactionAsync(transaction);
        if (!txResult.Success)
        {
            return (false, txResult.ErrorMessage);
        }

        // Update claim
        claim.Status = ExpenseClaimStatus.Settled;
        claim.SettledDate = settledDate;
        claim.PaidFromAccountId = paidFromAccountId;
        claim.PaymentMethod = paymentMethod;
        claim.TransactionId = txResult.Transaction!.Id;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<List<ExpenseClaim>> GetExpenseClaimsAsync(ExpenseClaimStatus? status = null)
    {
        var query = _context.ExpenseClaims
            .AsNoTracking()
            .Include(ec => ec.Expenses)
            .Include(ec => ec.PaidFromAccount)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(ec => ec.Status == status.Value);
        }

        return await query
            .OrderByDescending(ec => ec.SubmittedDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<ExpenseClaim?> GetExpenseClaimByIdAsync(int id)
    {
        return await _context.ExpenseClaims
            .AsNoTracking()
            .Include(ec => ec.Expenses)
            .ThenInclude(e => e.ExpenseAccount)
            .Include(ec => ec.Expenses)
            .ThenInclude(e => e.Meeting)
            .Include(ec => ec.PaidFromAccount)
            .Include(ec => ec.Transaction)
            .FirstOrDefaultAsync(ec => ec.Id == id);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteExpenseClaimAsync(int claimId)
    {
        var claim = await _context.ExpenseClaims
            .Include(ec => ec.Expenses)
            .FirstOrDefaultAsync(ec => ec.Id == claimId);

        if (claim == null)
        {
            return (false, "Expense claim not found.");
        }

        if (claim.Status == ExpenseClaimStatus.Settled)
        {
            return (false, "Cannot delete a settled claim.");
        }

        // Remove all expenses in the claim
        _context.Expenses.RemoveRange(claim.Expenses);
        _context.ExpenseClaims.Remove(claim);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Event-Level Reporting =====

    /// <inheritdoc/>
    public async Task<EventFinancialSummary> GetEventFinancialSummaryAsync(int meetingId)
    {
        var meeting = await _context.Meetings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        var summary = new EventFinancialSummary
        {
            MeetingId = meetingId,
            MeetingTitle = meeting?.Title ?? "Unknown"
        };

        // Get income: payments linked to this meeting
        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.MeetingId == meetingId && p.Status == PaymentStatus.Paid)
            .ToListAsync();

        foreach (var payment in payments)
        {
            summary.IncomeBreakdown.Add(new EventIncomeBreakdown
            {
                Description = $"Payment - {payment.Reference}",
                Amount = payment.AmountPaid
            });
        }
        summary.TotalIncome = payments.Sum(p => p.AmountPaid);

        // Get expenses linked to this meeting
        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseAccount)
            .Where(e => e.MeetingId == meetingId)
            .ToListAsync();

        var groupedExpenses = expenses
            .GroupBy(e => e.ExpenseAccount.Name)
            .ToList();

        foreach (var group in groupedExpenses)
        {
            summary.ExpenseBreakdown.Add(new EventExpenseBreakdown
            {
                CategoryName = group.Key,
                Amount = group.Sum(e => e.Amount)
            });
        }
        summary.TotalExpenses = expenses.Sum(e => e.Amount);

        return summary;
    }

    // ===== Reporting =====

    /// <inheritdoc/>
    public async Task<IncomeReport> GetIncomeReportAsync(DateTime dateFrom, DateTime dateTo)
    {
        var transactions = await GetTransactionsAsync(dateFrom, dateTo);

        var subsAccount = await GetAccountByCodeAsync(SubsIncomeCode);
        var activityAccount = await GetAccountByCodeAsync(ActivityIncomeCode);

        decimal subsIncome = 0;
        decimal activityIncome = 0;

        foreach (var transaction in transactions)
        {
            foreach (var line in transaction.Lines)
            {
                if (line.AccountId == subsAccount?.Id)
                {
                    subsIncome += line.Credit - line.Debit;
                }
                else if (line.AccountId == activityAccount?.Id)
                {
                    activityIncome += line.Credit - line.Debit;
                }
            }
        }

        return new IncomeReport
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            SubsIncome = subsIncome,
            ActivityIncome = activityIncome,
            Lines = new List<IncomeReportLine>
            {
                new IncomeReportLine
                {
                    AccountCode = SubsIncomeCode,
                    AccountName = "Subscription Income",
                    Amount = subsIncome
                },
                new IncomeReportLine
                {
                    AccountCode = ActivityIncomeCode,
                    AccountName = "Activity Income",
                    Amount = activityIncome
                }
            }
        };
    }

    /// <inheritdoc/>
    public async Task<ExpenseReport> GetExpenseReportAsync(DateTime dateFrom, DateTime dateTo)
    {
        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseAccount)
            .Where(e => e.Date >= dateFrom && e.Date <= dateTo)
            // Only count expenses that have been accounted for (direct or settled claims)
            .Where(e => e.TransactionId.HasValue || (e.ExpenseClaim != null && e.ExpenseClaim.Status == ExpenseClaimStatus.Settled))
            .ToListAsync();

        var lines = expenses
            .GroupBy(e => new { e.ExpenseAccount.Code, e.ExpenseAccount.Name })
            .Select(g => new ExpenseReportLine
            {
                AccountCode = g.Key.Code,
                AccountName = g.Key.Name,
                Amount = g.Sum(e => e.Amount),
                TransactionCount = g.Count()
            })
            .OrderBy(l => l.AccountCode)
            .ToList();

        return new ExpenseReport
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            TotalExpenses = lines.Sum(l => l.Amount),
            Lines = lines
        };
    }

    /// <inheritdoc/>
    public async Task<AccountingDashboardStats> GetDashboardStatsAsync()
    {
        var stats = new AccountingDashboardStats
        {
            CashOnHand = await GetCashOnHandAsync(),
            ChequesPending = await GetChequesPendingAsync(),
            BankBalance = await GetBankBalanceAsync()
        };

        // Get current term for income and expense calculation
        var currentTerm = await _termService.GetCurrentTermAsync();
        if (currentTerm != null)
        {
            var incomeReport = await GetIncomeReportAsync(currentTerm.StartDate, currentTerm.EndDate);
            stats.SubsIncomeThisTerm = incomeReport.SubsIncome;
            stats.ActivityIncomeThisTerm = incomeReport.ActivityIncome;

            var expenseReport = await GetExpenseReportAsync(currentTerm.StartDate, currentTerm.EndDate);
            stats.TotalExpensesThisTerm = expenseReport.TotalExpenses;
        }

        // Get pending claims
        var pendingClaims = await _context.ExpenseClaims
            .AsNoTracking()
            .Include(ec => ec.Expenses)
            .Where(ec => ec.Status == ExpenseClaimStatus.Draft || ec.Status == ExpenseClaimStatus.Submitted)
            .ToListAsync();

        stats.PendingClaimsCount = pendingClaims.Count;
        stats.PendingClaimsAmount = pendingClaims.Sum(c => c.Expenses.Sum(e => e.Amount));

        return stats;
    }

    // ===== Setup =====

    /// <inheritdoc/>
    public async Task EnsureDefaultAccountsAsync()
    {
        var existingCodes = await _context.Accounts
            .Select(a => a.Code)
            .ToListAsync();

        var defaultAccounts = new List<Account>
        {
            new Account { Code = CashOnHandCode, Name = "Cash on Hand", Type = AccountType.Asset, IsSystem = true },
            new Account { Code = ChequesPendingCode, Name = "Cheques Pending", Type = AccountType.Asset, IsSystem = true },
            new Account { Code = BankAccountCode, Name = "Bank Account", Type = AccountType.Asset, IsSystem = true },
            new Account { Code = SubsIncomeCode, Name = "Subscription Income", Type = AccountType.Income, IsSystem = true },
            new Account { Code = ActivityIncomeCode, Name = "Activity Income", Type = AccountType.Income, IsSystem = true },
            new Account { Code = SuppliesExpenseCode, Name = "Supplies", Type = AccountType.Expense, IsSystem = false },
            new Account { Code = EquipmentExpenseCode, Name = "Equipment", Type = AccountType.Expense, IsSystem = false },
            new Account { Code = VenueHireExpenseCode, Name = "Venue Hire", Type = AccountType.Expense, IsSystem = false },
            new Account { Code = ActivitiesEventsExpenseCode, Name = "Activities & Events", Type = AccountType.Expense, IsSystem = false },
            new Account { Code = BadgesAwardsExpenseCode, Name = "Badges & Awards", Type = AccountType.Expense, IsSystem = false },
            new Account { Code = OtherExpensesCode, Name = "Other Expenses", Type = AccountType.Expense, IsSystem = false }
        };

        foreach (var account in defaultAccounts)
        {
            if (!existingCodes.Contains(account.Code))
            {
                _context.Accounts.Add(account);
            }
        }

        await _context.SaveChangesAsync();
    }
}
