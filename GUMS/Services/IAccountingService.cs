using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

/// <summary>
/// Service for managing accounting records (cash accounting basis).
/// </summary>
public interface IAccountingService
{
    // ===== Account Operations =====

    /// <summary>
    /// Gets all accounts with their current balances.
    /// </summary>
    Task<List<Account>> GetAccountsAsync();

    /// <summary>
    /// Gets an account by its ID.
    /// </summary>
    Task<Account?> GetAccountByIdAsync(int id);

    /// <summary>
    /// Gets an account by its code.
    /// </summary>
    Task<Account?> GetAccountByCodeAsync(string code);

    /// <summary>
    /// Gets the current cash on hand balance.
    /// </summary>
    Task<decimal> GetCashOnHandAsync();

    /// <summary>
    /// Gets the current bank balance.
    /// </summary>
    Task<decimal> GetBankBalanceAsync();

    /// <summary>
    /// Gets the current cheques pending balance.
    /// </summary>
    Task<decimal> GetChequesPendingAsync();

    // ===== Transaction Operations =====

    /// <summary>
    /// Creates a transaction (journal entry) with the specified lines.
    /// Automatically updates account balances.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Transaction? Transaction)> CreateTransactionAsync(Transaction transaction);

    /// <summary>
    /// Gets transactions within a date range.
    /// </summary>
    Task<List<Transaction>> GetTransactionsAsync(DateTime? dateFrom = null, DateTime? dateTo = null);

    /// <summary>
    /// Gets a transaction by its ID.
    /// </summary>
    Task<Transaction?> GetTransactionByIdAsync(int id);

    /// <summary>
    /// Gets transactions for a specific payment.
    /// </summary>
    Task<List<Transaction>> GetTransactionsForPaymentAsync(int paymentId);

    // ===== Payment Recording Integration =====

    /// <summary>
    /// Records the accounting entries for a payment.
    /// Creates debit to asset account, credit to income account.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> RecordPaymentEntryAsync(
        int paymentId,
        decimal amount,
        PaymentMethod paymentMethod,
        PaymentType paymentType,
        string description,
        DateTime date);

    // ===== Banking Operations =====

    /// <summary>
    /// Records a bank deposit (moving cash and/or cheques to the bank account).
    /// </summary>
    Task<(bool Success, string ErrorMessage)> BankDepositAsync(
        decimal cashAmount,
        decimal chequeAmount,
        DateTime date,
        string? notes = null);

    // ===== Expense Account Management =====

    /// <summary>
    /// Gets all expense accounts.
    /// </summary>
    Task<List<Account>> GetExpenseAccountsAsync();

    /// <summary>
    /// Creates a new expense account with auto-assigned code.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Account? Account)> CreateExpenseAccountAsync(string name);

    /// <summary>
    /// Updates the name of an expense account.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateExpenseAccountAsync(int accountId, string name);

    /// <summary>
    /// Deletes an expense account (only if no transactions exist).
    /// </summary>
    Task<(bool Success, string ErrorMessage)> DeleteExpenseAccountAsync(int accountId);

    // ===== Direct Expense Recording =====

    /// <summary>
    /// Records a direct expense paid from unit funds.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Expense? Expense)> RecordDirectExpenseAsync(Expense expense);

    /// <summary>
    /// Deletes a direct expense and reverses its transaction.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> DeleteDirectExpenseAsync(int expenseId);

    // ===== Expense Queries =====

    /// <summary>
    /// Gets expenses with optional filters.
    /// </summary>
    Task<List<Expense>> GetExpensesAsync(DateTime? dateFrom = null, DateTime? dateTo = null, int? expenseAccountId = null, int? meetingId = null);

    /// <summary>
    /// Gets an expense by its ID.
    /// </summary>
    Task<Expense?> GetExpenseByIdAsync(int id);

    // ===== Reimbursement Claims =====

    /// <summary>
    /// Creates a new expense claim in Draft status.
    /// </summary>
    Task<(bool Success, string ErrorMessage, ExpenseClaim? Claim)> CreateExpenseClaimAsync(ExpenseClaim claim);

    /// <summary>
    /// Adds an expense to an existing draft claim.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> AddExpenseToClaimAsync(int claimId, Expense expense);

    /// <summary>
    /// Removes an expense from a draft claim.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> RemoveExpenseFromClaimAsync(int expenseId);

    /// <summary>
    /// Settles an expense claim, creating accounting entries.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> SettleExpenseClaimAsync(int claimId, int paidFromAccountId, PaymentMethod paymentMethod, DateTime settledDate);

    /// <summary>
    /// Gets expense claims with optional status filter.
    /// </summary>
    Task<List<ExpenseClaim>> GetExpenseClaimsAsync(ExpenseClaimStatus? status = null);

    /// <summary>
    /// Gets an expense claim by its ID.
    /// </summary>
    Task<ExpenseClaim?> GetExpenseClaimByIdAsync(int id);

    /// <summary>
    /// Deletes an expense claim (only if not settled).
    /// </summary>
    Task<(bool Success, string ErrorMessage)> DeleteExpenseClaimAsync(int claimId);

    // ===== Event-Level Reporting =====

    /// <summary>
    /// Gets the financial summary for a specific event/meeting.
    /// </summary>
    Task<EventFinancialSummary> GetEventFinancialSummaryAsync(int meetingId);

    // ===== Reporting =====

    /// <summary>
    /// Gets an income report for a date range.
    /// </summary>
    Task<IncomeReport> GetIncomeReportAsync(DateTime dateFrom, DateTime dateTo);

    /// <summary>
    /// Gets an expense report for a date range.
    /// </summary>
    Task<ExpenseReport> GetExpenseReportAsync(DateTime dateFrom, DateTime dateTo);

    /// <summary>
    /// Gets accounting dashboard statistics.
    /// </summary>
    Task<AccountingDashboardStats> GetDashboardStatsAsync();

    // ===== Setup =====

    /// <summary>
    /// Ensures default accounts exist. Called on application startup.
    /// </summary>
    Task EnsureDefaultAccountsAsync();
}

/// <summary>
/// Income report showing income by category.
/// </summary>
public class IncomeReport
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal SubsIncome { get; set; }
    public decimal ActivityIncome { get; set; }
    public decimal TotalIncome => SubsIncome + ActivityIncome;
    public List<IncomeReportLine> Lines { get; set; } = new();
}

/// <summary>
/// Line item in an income report.
/// </summary>
public class IncomeReportLine
{
    public string AccountName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// Dashboard statistics for accounting overview.
/// </summary>
public class AccountingDashboardStats
{
    public decimal CashOnHand { get; set; }
    public decimal ChequesPending { get; set; }
    public decimal BankBalance { get; set; }
    public decimal TotalAssets => CashOnHand + ChequesPending + BankBalance;
    public decimal SubsIncomeThisTerm { get; set; }
    public decimal ActivityIncomeThisTerm { get; set; }
    public decimal TotalIncomeThisTerm => SubsIncomeThisTerm + ActivityIncomeThisTerm;
    public decimal TotalExpensesThisTerm { get; set; }
    public decimal NetIncomeThisTerm => TotalIncomeThisTerm - TotalExpensesThisTerm;
    public int PendingClaimsCount { get; set; }
    public decimal PendingClaimsAmount { get; set; }
}

/// <summary>
/// Financial summary for a specific event/meeting.
/// </summary>
public class EventFinancialSummary
{
    public int MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetPosition => TotalIncome - TotalExpenses;
    public List<EventIncomeBreakdown> IncomeBreakdown { get; set; } = new();
    public List<EventExpenseBreakdown> ExpenseBreakdown { get; set; } = new();
}

public class EventIncomeBreakdown
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class EventExpenseBreakdown
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// Expense report showing expenses by category for a date range.
/// </summary>
public class ExpenseReport
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal TotalExpenses { get; set; }
    public List<ExpenseReportLine> Lines { get; set; } = new();
}

/// <summary>
/// Line item in an expense report.
/// </summary>
public class ExpenseReportLine
{
    public string AccountName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}
