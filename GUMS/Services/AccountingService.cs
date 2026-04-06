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
    private readonly IConfigurationService _configurationService;

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
    public const string MemberCreditsCode = "2001";
    public const string OpeningBalancesCode = "3001";

    public AccountingService(ApplicationDbContext context, ITermService termService, IConfigurationService configurationService)
    {
        _context = context;
        _termService = termService;
        _configurationService = configurationService;
    }

    private static (DateTime Start, DateTime End) GetCurrentFinancialYear(int endMonth, int endDay)
    {
        var today = DateTime.Today;
        var yearEndThisCalYear = new DateTime(today.Year, endMonth, endDay);
        var yearEnd = today <= yearEndThisCalYear
            ? yearEndThisCalYear
            : new DateTime(today.Year + 1, endMonth, endDay);
        var yearStart = yearEnd.AddYears(-1).AddDays(1);
        return (yearStart, yearEnd);
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

        // Period lock check
        var config = await _configurationService.GetConfigurationAsync();
        if (config.AccountsLockedUntil.HasValue && transaction.Date <= config.AccountsLockedUntil.Value)
        {
            return (false, $"This period is locked. Transactions dated on or before {config.AccountsLockedUntil.Value:d MMMM yyyy} cannot be posted.", null);
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

            // For Asset/Expense accounts: Debit increases, Credit decreases
            // For Income/Liability/Equity accounts: Credit increases, Debit decreases
            if (account.Type == AccountType.Asset || account.Type == AccountType.Expense)
            {
                account.Balance += line.Debit - line.Credit;
            }
            else // Income, Liability, Equity
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

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> VoidTransactionAsync(int transactionId)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
        {
            return (false, "Transaction not found.");
        }

        if (transaction.IsVoided)
        {
            return (false, "Transaction is already voided.");
        }

        // Period lock check
        var lockConfig = await _configurationService.GetConfigurationAsync();
        if (lockConfig.AccountsLockedUntil.HasValue && transaction.Date <= lockConfig.AccountsLockedUntil.Value)
        {
            return (false, $"This period is locked. Transactions dated on or before {lockConfig.AccountsLockedUntil.Value:d MMMM yyyy} cannot be voided.");
        }

        // Check if any line has been reconciled
        if (transaction.Lines.Any(l => l.BankReconciliationId.HasValue))
        {
            return (false, "Cannot void a reconciled transaction. One or more lines have been included in a bank reconciliation.");
        }

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
            else // Income, Liability, Equity
            {
                account.Balance -= line.Credit - line.Debit;
            }
        }

        // If linked to a payment, reverse the AmountPaid and reset status
        if (transaction.PaymentId.HasValue)
        {
            var payment = await _context.Payments.FindAsync(transaction.PaymentId.Value);
            if (payment != null)
            {
                var transactionAmount = transaction.TotalDebits;
                payment.AmountPaid -= transactionAmount;
                if (payment.AmountPaid < 0) payment.AmountPaid = 0;

                if (payment.Status == PaymentStatus.Paid)
                {
                    payment.Status = PaymentStatus.Pending;
                }
            }
        }

        // Remove any Expense entity that references this transaction
        var linkedExpense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.TransactionId == transactionId);
        if (linkedExpense != null)
        {
            _context.Expenses.Remove(linkedExpense);
        }

        transaction.IsVoided = true;
        transaction.VoidedDate = DateTime.Now;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateTransactionDateAsync(int transactionId, DateTime newDate)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
            return (false, "Transaction not found.");

        if (transaction.IsVoided)
            return (false, "Cannot edit a voided transaction.");

        transaction.Date = newDate;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Payment Recording Integration =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> RecordPaymentEntryAsync(
        int paymentId,
        decimal amount,
        PaymentMethod paymentMethod,
        PaymentType paymentType,
        string description,
        DateTime date,
        int? incomeAccountId = null)
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

        var assetAccount = await GetAccountByCodeAsync(assetAccountCode);

        // Determine income account: use provided ID if available, otherwise look up by payment type
        Account? incomeAccount;
        if (incomeAccountId.HasValue)
        {
            incomeAccount = await GetAccountByIdAsync(incomeAccountId.Value);
        }
        else
        {
            var incomeAccountCode = paymentType switch
            {
                PaymentType.Subs => SubsIncomeCode,
                PaymentType.Activity => ActivityIncomeCode,
                PaymentType.Other => (string?)null,
                _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
            };

            if (incomeAccountCode == null)
            {
                return (false, "An income account must be selected for 'Other' payment types.");
            }

            incomeAccount = await GetAccountByCodeAsync(incomeAccountCode);
        }

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

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordRefundEntryAsync(
        int paymentId,
        decimal amount,
        PaymentMethod refundMethod,
        PaymentType paymentType,
        string description,
        DateTime date,
        int? incomeAccountId = null)
    {
        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.", null);
        }

        // Determine asset account based on refund method
        var assetAccountCode = refundMethod switch
        {
            PaymentMethod.Cash => CashOnHandCode,
            PaymentMethod.Cheque => ChequesPendingCode,
            PaymentMethod.BankTransfer => BankAccountCode,
            _ => throw new ArgumentException($"Unknown payment method: {refundMethod}")
        };

        var assetAccount = await GetAccountByCodeAsync(assetAccountCode);

        // Determine income account: use provided ID if available, otherwise look up by payment type
        Account? incomeAccount;
        if (incomeAccountId.HasValue)
        {
            incomeAccount = await GetAccountByIdAsync(incomeAccountId.Value);
        }
        else
        {
            var incomeAccountCode = paymentType switch
            {
                PaymentType.Subs => SubsIncomeCode,
                PaymentType.Activity => ActivityIncomeCode,
                PaymentType.Other => (string?)null,
                _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
            };

            if (incomeAccountCode == null)
            {
                return (false, "An income account must be selected for 'Other' payment types.", null);
            }

            incomeAccount = await GetAccountByCodeAsync(incomeAccountCode);
        }

        if (assetAccount == null || incomeAccount == null)
        {
            return (false, "Required accounts not found. Please ensure default accounts have been created.", null);
        }

        // Reverse of payment entry: Debit income, Credit asset
        var transaction = new Transaction
        {
            Date = date,
            Description = description,
            PaymentId = paymentId,
            Lines = new List<TransactionLine>
            {
                new TransactionLine
                {
                    AccountId = incomeAccount.Id,
                    Debit = amount,
                    Credit = 0
                },
                new TransactionLine
                {
                    AccountId = assetAccount.Id,
                    Debit = 0,
                    Credit = amount
                }
            }
        };

        var result = await CreateTransactionAsync(transaction);
        if (!result.Success)
        {
            return (false, result.ErrorMessage, null);
        }

        return (true, string.Empty, result.Transaction!.Id);
    }

    // ===== Credit Operations =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordConvertToCreditEntryAsync(
        int paymentId,
        decimal amount,
        PaymentType paymentType,
        string description,
        DateTime date,
        int? incomeAccountId = null)
    {
        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.", null);
        }

        var creditsAccount = await GetAccountByCodeAsync(MemberCreditsCode);

        // Determine income account
        Account? incomeAccount;
        if (incomeAccountId.HasValue)
        {
            incomeAccount = await GetAccountByIdAsync(incomeAccountId.Value);
        }
        else
        {
            var incomeAccountCode = paymentType switch
            {
                PaymentType.Subs => SubsIncomeCode,
                PaymentType.Activity => ActivityIncomeCode,
                PaymentType.Other => (string?)null,
                _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
            };

            if (incomeAccountCode == null)
            {
                return (false, "An income account must be selected for 'Other' payment types.", null);
            }

            incomeAccount = await GetAccountByCodeAsync(incomeAccountCode);
        }

        if (creditsAccount == null || incomeAccount == null)
        {
            return (false, "Required accounts not found. Please ensure default accounts have been created.", null);
        }

        // Debit income (reverse original income), Credit liability (owe parent)
        var transaction = new Transaction
        {
            Date = date,
            Description = description,
            PaymentId = paymentId,
            Lines = new List<TransactionLine>
            {
                new TransactionLine
                {
                    AccountId = incomeAccount.Id,
                    Debit = amount,
                    Credit = 0
                },
                new TransactionLine
                {
                    AccountId = creditsAccount.Id,
                    Debit = 0,
                    Credit = amount
                }
            }
        };

        var result = await CreateTransactionAsync(transaction);
        if (!result.Success)
        {
            return (false, result.ErrorMessage, null);
        }

        return (true, string.Empty, result.Transaction!.Id);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordApplyCreditEntryAsync(
        int targetPaymentId,
        decimal amount,
        PaymentType paymentType,
        string description,
        DateTime date,
        int? incomeAccountId = null)
    {
        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.", null);
        }

        var creditsAccount = await GetAccountByCodeAsync(MemberCreditsCode);

        // Determine income account
        Account? incomeAccount;
        if (incomeAccountId.HasValue)
        {
            incomeAccount = await GetAccountByIdAsync(incomeAccountId.Value);
        }
        else
        {
            var incomeAccountCode = paymentType switch
            {
                PaymentType.Subs => SubsIncomeCode,
                PaymentType.Activity => ActivityIncomeCode,
                PaymentType.Other => (string?)null,
                _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
            };

            if (incomeAccountCode == null)
            {
                return (false, "An income account must be selected for 'Other' payment types.", null);
            }

            incomeAccount = await GetAccountByCodeAsync(incomeAccountCode);
        }

        if (creditsAccount == null || incomeAccount == null)
        {
            return (false, "Required accounts not found. Please ensure default accounts have been created.", null);
        }

        // Debit liability (reduce what we owe), Credit income (recognise the income)
        var transaction = new Transaction
        {
            Date = date,
            Description = description,
            PaymentId = targetPaymentId,
            Lines = new List<TransactionLine>
            {
                new TransactionLine
                {
                    AccountId = creditsAccount.Id,
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
        if (!result.Success)
        {
            return (false, result.ErrorMessage, null);
        }

        return (true, string.Empty, result.Transaction!.Id);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordRefundCreditEntryAsync(
        decimal amount,
        PaymentMethod refundMethod,
        string description,
        DateTime date)
    {
        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.", null);
        }

        var creditsAccount = await GetAccountByCodeAsync(MemberCreditsCode);

        var assetAccountCode = refundMethod switch
        {
            PaymentMethod.Cash => CashOnHandCode,
            PaymentMethod.Cheque => ChequesPendingCode,
            PaymentMethod.BankTransfer => BankAccountCode,
            _ => throw new ArgumentException($"Unknown payment method: {refundMethod}")
        };

        var assetAccount = await GetAccountByCodeAsync(assetAccountCode);

        if (creditsAccount == null || assetAccount == null)
        {
            return (false, "Required accounts not found. Please ensure default accounts have been created.", null);
        }

        // Debit liability (reduce what we owe), Credit asset (cash goes out)
        var transaction = new Transaction
        {
            Date = date,
            Description = description,
            Lines = new List<TransactionLine>
            {
                new TransactionLine
                {
                    AccountId = creditsAccount.Id,
                    Debit = amount,
                    Credit = 0
                },
                new TransactionLine
                {
                    AccountId = assetAccount.Id,
                    Debit = 0,
                    Credit = amount
                }
            }
        };

        var result = await CreateTransactionAsync(transaction);
        if (!result.Success)
        {
            return (false, result.ErrorMessage, null);
        }

        return (true, string.Empty, result.Transaction!.Id);
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

    // ===== General Account Management =====

    /// <inheritdoc/>
    public async Task<List<Account>> GetAccountsByTypeAsync(AccountType type)
    {
        return await _context.Accounts
            .AsNoTracking()
            .Include(a => a.TransactionLines)
            .Where(a => a.Type == type)
            .OrderBy(a => a.Code)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, Account? Account)> CreateAccountAsync(string name, AccountType type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Account name is required.", null);
        }

        var (rangeStart, rangeEnd) = GetCodeRangeForType(type);

        var usedCodes = await _context.Accounts
            .Where(a => a.Type == type)
            .Select(a => a.Code)
            .ToListAsync();

        var nextNumber = rangeStart + 1; // e.g. 1001 for Asset
        if (usedCodes.Any())
        {
            var usedNumbers = usedCodes
                .Select(c => int.TryParse(c, out var n) ? n : 0)
                .Where(n => n >= rangeStart && n < rangeEnd)
                .OrderBy(n => n)
                .ToList();

            if (usedNumbers.Any())
            {
                nextNumber = usedNumbers.Max() + 1;
            }
        }

        if (nextNumber >= rangeEnd)
        {
            return (false, $"No more account codes available in the {type} range.", null);
        }

        var account = new Account
        {
            Code = nextNumber.ToString(),
            Name = name,
            Type = type,
            IsSystem = false
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return (true, string.Empty, account);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateAccountAsync(int accountId, string name)
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

        if (account.IsSystem)
        {
            return (false, "System accounts cannot be modified.");
        }

        account.Name = name;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteAccountAsync(int accountId)
    {
        var account = await _context.Accounts
            .Include(a => a.TransactionLines)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
        {
            return (false, "Account not found.");
        }

        if (account.IsSystem)
        {
            return (false, "System accounts cannot be deleted.");
        }

        if (account.TransactionLines.Any())
        {
            return (false, "Cannot delete account with existing transactions.");
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    private static (int Start, int End) GetCodeRangeForType(AccountType type)
    {
        return type switch
        {
            AccountType.Asset => (1000, 2000),
            AccountType.Liability => (2000, 3000),
            AccountType.Equity => (3000, 4000),
            AccountType.Income => (4000, 5000),
            AccountType.Expense => (5000, 6000),
            _ => throw new ArgumentException($"Unknown account type: {type}")
        };
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

    // ===== Direct Income Recording =====

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, Transaction? Transaction)> RecordDirectIncomeAsync(
        decimal amount,
        int creditAccountId,
        int receivedIntoAccountId,
        string description,
        DateTime date,
        string? reference = null,
        string? notes = null)
    {
        if (amount <= 0)
        {
            return (false, "Amount must be greater than zero.", null);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return (false, "Description is required.", null);
        }

        var creditAccount = await _context.Accounts.FindAsync(creditAccountId);
        if (creditAccount == null || (creditAccount.Type != AccountType.Income && creditAccount.Type != AccountType.Expense))
        {
            return (false, "Invalid income/expense account.", null);
        }

        var assetAccount = await _context.Accounts.FindAsync(receivedIntoAccountId);
        if (assetAccount == null || assetAccount.Type != AccountType.Asset)
        {
            return (false, "Invalid asset account.", null);
        }

        var descriptionPrefix = creditAccount.Type == AccountType.Expense ? "Refund" : "Income";
        var fullDescription = $"{descriptionPrefix}: {description}";
        if (!string.IsNullOrWhiteSpace(reference))
        {
            fullDescription += $" (Ref: {reference})";
        }
        if (!string.IsNullOrWhiteSpace(notes))
        {
            fullDescription += $" - {notes}";
        }

        var transaction = new Transaction
        {
            Date = date,
            Description = fullDescription,
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
                    AccountId = creditAccount.Id,
                    Debit = 0,
                    Credit = amount
                }
            }
        };

        return await CreateTransactionAsync(transaction);
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
                    // Reverse: Asset/Expense use debit-credit, Income/Liability/Equity use credit-debit
                    if (account.Type == AccountType.Asset || account.Type == AccountType.Expense)
                    {
                        account.Balance -= line.Debit - line.Credit;
                    }
                    else // Income, Liability, Equity
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

        // Get income: payments linked to this meeting where any amount has been received
        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.MeetingId == meetingId && p.AmountPaid > 0)
            .ToListAsync();

        // Look up member names for the income breakdown
        var membershipNumbers = payments.Select(p => p.MembershipNumber).Distinct().ToList();
        var members = await _context.Persons
            .AsNoTracking()
            .Where(p => membershipNumbers.Contains(p.MembershipNumber))
            .ToDictionaryAsync(p => p.MembershipNumber, p => p.FullName);

        foreach (var payment in payments.OrderBy(p => members.GetValueOrDefault(p.MembershipNumber) ?? p.MembershipNumber))
        {
            var name = members.GetValueOrDefault(payment.MembershipNumber) ?? payment.MembershipNumber;
            summary.IncomeBreakdown.Add(new EventIncomeBreakdown
            {
                Description = name,
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

        // Get financial year date range from unit configuration
        var config = await _configurationService.GetConfigurationAsync();
        var (fyStart, fyEnd) = GetCurrentFinancialYear(config.FinancialYearEndMonth, config.FinancialYearEndDay);
        stats.FinancialYearStart = fyStart;
        stats.FinancialYearEnd = fyEnd;

        var incomeReport = await GetIncomeReportAsync(fyStart, fyEnd);
        stats.SubsIncomeThisYear = incomeReport.SubsIncome;
        stats.ActivityIncomeThisYear = incomeReport.ActivityIncome;

        var expenseReport = await GetExpenseReportAsync(fyStart, fyEnd);
        stats.TotalExpensesThisYear = expenseReport.TotalExpenses;

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
            new Account { Code = OtherExpensesCode, Name = "Other Expenses", Type = AccountType.Expense, IsSystem = false },
            new Account { Code = MemberCreditsCode, Name = "Member Credits", Type = AccountType.Liability, IsSystem = true },
            new Account { Code = OpeningBalancesCode, Name = "Opening Balances", Type = AccountType.Equity, IsSystem = true }
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

    /// <inheritdoc/>
    public async Task<YearEndAccountsReport> GetYearEndReportAsync(DateTime yearEnd)
    {
        var thisYearEnd   = yearEnd.Date;
        var thisYearStart = thisYearEnd.AddYears(-1).AddDays(1);
        var lastYearEnd   = thisYearStart.AddDays(-1);
        var lastYearStart = lastYearEnd.AddYears(-1).AddDays(1);

        // Load all non-voided transaction lines once
        var allLines = await _context.TransactionLines
            .Include(l => l.Transaction)
            .Include(l => l.Account)
            .AsNoTracking()
            .Where(l => !l.Transaction.IsVoided)
            .ToListAsync();

        var thisYearLines = allLines
            .Where(l => l.Transaction.Date >= thisYearStart && l.Transaction.Date <= thisYearEnd)
            .ToList();
        var lastYearLines = allLines
            .Where(l => l.Transaction.Date >= lastYearStart && l.Transaction.Date <= lastYearEnd)
            .ToList();

        // Income accounts — Credit increases income, Debit decreases it
        var incomeAccounts = await _context.Accounts
            .Where(a => a.Type == AccountType.Income)
            .OrderBy(a => a.Code)
            .AsNoTracking()
            .ToListAsync();

        var incomeRows = new List<YearAccountRow>();
        foreach (var account in incomeAccounts)
        {
            var thisAmt = thisYearLines
                .Where(l => l.AccountId == account.Id)
                .Sum(l => l.Credit - l.Debit);
            var lastAmt = lastYearLines
                .Where(l => l.AccountId == account.Id)
                .Sum(l => l.Credit - l.Debit);

            if (thisAmt != 0 || lastAmt != 0)
                incomeRows.Add(new YearAccountRow { Name = account.Name, ThisYear = thisAmt, LastYear = lastAmt });
        }

        // Expense accounts — Debit increases expenses, Credit decreases
        var expenseAccounts = await _context.Accounts
            .Where(a => a.Type == AccountType.Expense)
            .OrderBy(a => a.Code)
            .AsNoTracking()
            .ToListAsync();

        var expenseRows = new List<YearAccountRow>();
        foreach (var account in expenseAccounts)
        {
            var thisAmt = thisYearLines
                .Where(l => l.AccountId == account.Id)
                .Sum(l => l.Debit - l.Credit);
            var lastAmt = lastYearLines
                .Where(l => l.AccountId == account.Id)
                .Sum(l => l.Debit - l.Credit);

            if (thisAmt != 0 || lastAmt != 0)
                expenseRows.Add(new YearAccountRow
                {
                    Name = account.Name,
                    ThisYear = thisAmt != 0 ? thisAmt : null,
                    LastYear = lastAmt != 0 ? lastAmt : null
                });
        }

        // Asset accounts — point-in-time balances from transaction lines
        var assetAccounts = await _context.Accounts
            .Where(a => a.Type == AccountType.Asset)
            .OrderBy(a => a.Code)
            .AsNoTracking()
            .ToListAsync();

        var broughtForwardRows = new List<YearAssetRow>();
        var atYearEndRows      = new List<YearAssetRow>();

        foreach (var account in assetAccounts)
        {
            var linesForAccount = allLines.Where(l => l.AccountId == account.Id).ToList();

            // Brought forward = balance at the start of each year (all lines BEFORE the year starts)
            var bfThis = linesForAccount
                .Where(l => l.Transaction.Date < thisYearStart)
                .Sum(l => l.Debit - l.Credit);
            var bfLast = linesForAccount
                .Where(l => l.Transaction.Date < lastYearStart)
                .Sum(l => l.Debit - l.Credit);

            // At year end = balance of all lines up to and including the year end date
            var yeThis = linesForAccount
                .Where(l => l.Transaction.Date <= thisYearEnd)
                .Sum(l => l.Debit - l.Credit);
            var yeLast = linesForAccount
                .Where(l => l.Transaction.Date <= lastYearEnd)
                .Sum(l => l.Debit - l.Credit);

            broughtForwardRows.Add(new YearAssetRow { Name = account.Name, ThisYear = bfThis, LastYear = bfLast });
            atYearEndRows.Add(new YearAssetRow { Name = account.Name, ThisYear = yeThis, LastYear = yeLast });
        }

        return new YearEndAccountsReport
        {
            ThisYearStart       = thisYearStart,
            ThisYearEnd         = thisYearEnd,
            LastYearStart       = lastYearStart,
            LastYearEnd         = lastYearEnd,
            IncomeRows          = incomeRows,
            ExpenseRows         = expenseRows,
            BroughtForwardRows  = broughtForwardRows,
            AtYearEndRows       = atYearEndRows
        };
    }

    /// <inheritdoc/>
    public async Task<YearClosingPreview> GetYearClosingPreviewAsync(DateTime yearEnd)
    {
        var thisYearEnd   = yearEnd.Date;
        var thisYearStart = thisYearEnd.AddYears(-1).AddDays(1);

        var config = await _configurationService.GetConfigurationAsync();
        var alreadyFinalised = config.AccountsLockedUntil.HasValue
            && config.AccountsLockedUntil.Value >= thisYearEnd;

        // Load non-voided lines for this year
        var yearLines = await _context.TransactionLines
            .Include(l => l.Transaction)
            .Include(l => l.Account)
            .AsNoTracking()
            .Where(l => !l.Transaction.IsVoided
                     && l.Transaction.Date >= thisYearStart
                     && l.Transaction.Date <= thisYearEnd)
            .ToListAsync();

        var journalLines = new List<YearClosingLine>();
        decimal totalIncome = 0;
        decimal totalExpenses = 0;

        // Income accounts: Dr to zero (net credit activity → we debit it)
        var incomeAccounts = await _context.Accounts
            .Where(a => a.Type == AccountType.Income)
            .OrderBy(a => a.Code)
            .AsNoTracking()
            .ToListAsync();

        foreach (var account in incomeAccounts)
        {
            var net = yearLines
                .Where(l => l.AccountId == account.Id)
                .Sum(l => l.Credit - l.Debit); // positive = income earned

            if (net != 0)
            {
                journalLines.Add(new YearClosingLine
                {
                    AccountName = account.Name,
                    Debit = net > 0 ? net : null,
                    Credit = net < 0 ? -net : null
                });
                totalIncome += net;
            }
        }

        // Expense accounts: Cr to zero (net debit activity → we credit it)
        var expenseAccounts = await _context.Accounts
            .Where(a => a.Type == AccountType.Expense)
            .OrderBy(a => a.Code)
            .AsNoTracking()
            .ToListAsync();

        foreach (var account in expenseAccounts)
        {
            var net = yearLines
                .Where(l => l.AccountId == account.Id)
                .Sum(l => l.Debit - l.Credit); // positive = expense incurred

            if (net != 0)
            {
                journalLines.Add(new YearClosingLine
                {
                    AccountName = account.Name,
                    Debit = net < 0 ? -net : null,
                    Credit = net > 0 ? net : null
                });
                totalExpenses += net;
            }
        }

        // Net surplus/deficit → Opening Balances equity account
        var netSurplus = totalIncome - totalExpenses;
        if (netSurplus != 0)
        {
            journalLines.Add(new YearClosingLine
            {
                AccountName = "Opening Balances (Equity)",
                Debit = netSurplus < 0 ? -netSurplus : null,
                Credit = netSurplus > 0 ? netSurplus : null
            });
        }

        return new YearClosingPreview
        {
            YearStart      = thisYearStart,
            YearEnd        = thisYearEnd,
            AlreadyFinalised = alreadyFinalised,
            JournalLines   = journalLines,
            TotalIncome    = totalIncome,
            TotalExpenses  = totalExpenses
        };
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> FinaliseYearEndAsync(DateTime yearEnd)
    {
        var preview = await GetYearClosingPreviewAsync(yearEnd);

        if (preview.AlreadyFinalised)
        {
            return (false, "This financial year has already been finalised.");
        }

        if (!preview.JournalLines.Any())
        {
            // Nothing to close — still set the lock
            await _configurationService.SetAccountsLockedUntilAsync(yearEnd.Date);
            return (true, string.Empty);
        }

        // Build the closing journal transaction
        var openingBalancesAccount = await GetAccountByCodeAsync(OpeningBalancesCode);
        if (openingBalancesAccount == null)
        {
            return (false, "Opening Balances account (3001) not found. Please ensure default accounts have been created.");
        }

        var incomeAccounts = await _context.Accounts
            .Where(a => a.Type == AccountType.Income)
            .AsNoTracking()
            .ToListAsync();
        var expenseAccounts = await _context.Accounts
            .Where(a => a.Type == AccountType.Expense)
            .AsNoTracking()
            .ToListAsync();

        var allPnlAccounts = incomeAccounts.Concat(expenseAccounts)
            .ToDictionary(a => a.Name);

        var lines = new List<TransactionLine>();

        foreach (var jl in preview.JournalLines)
        {
            int accountId;
            if (jl.AccountName == "Opening Balances (Equity)")
            {
                accountId = openingBalancesAccount.Id;
            }
            else if (allPnlAccounts.TryGetValue(jl.AccountName, out var acct))
            {
                accountId = acct.Id;
            }
            else
            {
                return (false, $"Account '{jl.AccountName}' not found.");
            }

            lines.Add(new TransactionLine
            {
                AccountId = accountId,
                Debit     = jl.Debit ?? 0,
                Credit    = jl.Credit ?? 0
            });
        }

        var transaction = new Transaction
        {
            Date        = yearEnd.Date,
            Description = $"Year end close – {yearEnd:d MMMM yyyy}",
            Lines       = lines
        };

        // Temporarily bypass the lock check by setting lock after posting
        var result = await CreateTransactionAsync(transaction);
        if (!result.Success)
        {
            return (false, result.ErrorMessage);
        }

        // Set the lock
        await _configurationService.SetAccountsLockedUntilAsync(yearEnd.Date);

        return (true, string.Empty);
    }
}
