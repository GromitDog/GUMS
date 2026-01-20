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

            // For asset accounts: Debit increases, Credit decreases
            // For income accounts: Credit increases, Debit decreases
            if (account.Type == AccountType.Asset)
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
    public async Task<AccountingDashboardStats> GetDashboardStatsAsync()
    {
        var stats = new AccountingDashboardStats
        {
            CashOnHand = await GetCashOnHandAsync(),
            ChequesPending = await GetChequesPendingAsync(),
            BankBalance = await GetBankBalanceAsync()
        };

        // Get current term for income calculation
        var currentTerm = await _termService.GetCurrentTermAsync();
        if (currentTerm != null)
        {
            var incomeReport = await GetIncomeReportAsync(currentTerm.StartDate, currentTerm.EndDate);
            stats.SubsIncomeThisTerm = incomeReport.SubsIncome;
            stats.ActivityIncomeThisTerm = incomeReport.ActivityIncome;
        }

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
            new Account { Code = ActivityIncomeCode, Name = "Activity Income", Type = AccountType.Income, IsSystem = true }
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
