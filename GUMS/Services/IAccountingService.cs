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

    /// <summary>
    /// Voids a transaction, reversing its account balance effects.
    /// Cannot void payment-linked or reconciled transactions.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> VoidTransactionAsync(int transactionId);

    /// <summary>
    /// Updates the date on a transaction.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateTransactionDateAsync(int transactionId, DateTime newDate);

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
        DateTime date,
        int? incomeAccountId = null);

    /// <summary>
    /// Records the accounting entries for a payment refund.
    /// Creates debit to income account, credit to asset account (reverse of RecordPaymentEntryAsync).
    /// </summary>
    Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordRefundEntryAsync(
        int paymentId,
        decimal amount,
        PaymentMethod refundMethod,
        PaymentType paymentType,
        string description,
        DateTime date,
        int? incomeAccountId = null);

    // ===== Credit Operations =====

    /// <summary>
    /// Records the accounting entries when a payment is converted to credit.
    /// Debit income account, Credit member credits liability account.
    /// </summary>
    Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordConvertToCreditEntryAsync(
        int paymentId,
        decimal amount,
        PaymentType paymentType,
        string description,
        DateTime date,
        int? incomeAccountId = null);

    /// <summary>
    /// Records the accounting entries when credit is applied to a pending payment.
    /// Debit member credits liability account, Credit income account.
    /// </summary>
    Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordApplyCreditEntryAsync(
        int targetPaymentId,
        decimal amount,
        PaymentType paymentType,
        string description,
        DateTime date,
        int? incomeAccountId = null);

    /// <summary>
    /// Records the accounting entries when credit is refunded as cash.
    /// Debit member credits liability account, Credit asset account.
    /// </summary>
    Task<(bool Success, string ErrorMessage, int? TransactionId)> RecordRefundCreditEntryAsync(
        decimal amount,
        PaymentMethod refundMethod,
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

    // ===== General Account Management =====

    /// <summary>
    /// Gets all accounts of a given type, including their transaction lines.
    /// </summary>
    Task<List<Account>> GetAccountsByTypeAsync(AccountType type);

    /// <summary>
    /// Creates a new account with auto-assigned code in the correct range.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Account? Account)> CreateAccountAsync(string name, AccountType type);

    /// <summary>
    /// Updates the name of a non-system account.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateAccountAsync(int accountId, string name);

    /// <summary>
    /// Deletes a non-system account (only if no transactions exist).
    /// </summary>
    Task<(bool Success, string ErrorMessage)> DeleteAccountAsync(int accountId);

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

    // ===== Direct Income Recording =====

    /// <summary>
    /// Records ad-hoc income (donations, visitor fees, refunds) directly into transaction history.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Transaction? Transaction)> RecordDirectIncomeAsync(
        decimal amount,
        int creditAccountId,
        int receivedIntoAccountId,
        string description,
        DateTime date,
        string? reference = null,
        string? notes = null);

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

    /// <summary>
    /// Gets the year-end accounts report for the specified financial year end date.
    /// Includes all income/expense accounts active in that year or the prior year,
    /// plus asset balances brought forward and at year end.
    /// </summary>
    Task<YearEndAccountsReport> GetYearEndReportAsync(DateTime yearEnd);

    /// <summary>
    /// Builds a preview of the closing journal that would be posted for the given year end,
    /// without actually posting anything.
    /// </summary>
    Task<YearClosingPreview> GetYearClosingPreviewAsync(DateTime yearEnd);

    /// <summary>
    /// Posts the year-end closing journal and locks the period.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> FinaliseYearEndAsync(DateTime yearEnd);
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
    public decimal SubsIncomeThisYear { get; set; }
    public decimal ActivityIncomeThisYear { get; set; }
    public decimal TotalIncomeThisYear => SubsIncomeThisYear + ActivityIncomeThisYear;
    public decimal TotalExpensesThisYear { get; set; }
    public decimal NetIncomeThisYear => TotalIncomeThisYear - TotalExpensesThisYear;
    public DateTime FinancialYearStart { get; set; }
    public DateTime FinancialYearEnd { get; set; }
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
    public decimal TotalCreditApplied { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalAmountDue { get; set; }
    public decimal NetPosition => TotalIncome + TotalCreditApplied - TotalExpenses;
    public int PaymentCount { get; set; }
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    public List<EventIncomeBreakdown> IncomeBreakdown { get; set; } = new();
    public List<EventExpenseBreakdown> ExpenseBreakdown { get; set; } = new();
}

public class EventIncomeBreakdown
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal CreditApplied { get; set; }
    public decimal Outstanding { get; set; }
    public PaymentStatus Status { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
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

/// <summary>
/// Year-end accounts report comparing this year and the prior year.
/// </summary>
public class YearEndAccountsReport
{
    public DateTime ThisYearStart { get; set; }
    public DateTime ThisYearEnd { get; set; }
    public DateTime LastYearStart { get; set; }
    public DateTime LastYearEnd { get; set; }

    public List<YearAccountRow> IncomeRows { get; set; } = new();
    public List<YearAccountRow> ExpenseRows { get; set; } = new();
    public List<YearAssetRow> BroughtForwardRows { get; set; } = new();
    public List<YearAssetRow> AtYearEndRows { get; set; } = new();

    public decimal TotalIncomeThisYear => IncomeRows.Sum(r => r.ThisYear ?? 0);
    public decimal TotalIncomeLastYear => IncomeRows.Sum(r => r.LastYear ?? 0);
    public decimal TotalExpenseThisYear => ExpenseRows.Sum(r => r.ThisYear ?? 0);
    public decimal TotalExpenseLastYear => ExpenseRows.Sum(r => r.LastYear ?? 0);
    public decimal SurplusThisYear => TotalIncomeThisYear - TotalExpenseThisYear;
    public decimal SurplusLastYear => TotalIncomeLastYear - TotalExpenseLastYear;
    public decimal TotalBroughtForwardThisYear => BroughtForwardRows.Sum(r => r.ThisYear);
    public decimal TotalBroughtForwardLastYear => BroughtForwardRows.Sum(r => r.LastYear);
    public decimal TotalAtYearEndThisYear => AtYearEndRows.Sum(r => r.ThisYear);
    public decimal TotalAtYearEndLastYear => AtYearEndRows.Sum(r => r.LastYear);
}

/// <summary>Row in income or expense section. Null = no activity (shown as blank).</summary>
public class YearAccountRow
{
    public string Name { get; set; } = string.Empty;
    public decimal? ThisYear { get; set; }
    public decimal? LastYear { get; set; }
}

/// <summary>Asset account balance row.</summary>
public class YearAssetRow
{
    public string Name { get; set; } = string.Empty;
    public decimal ThisYear { get; set; }
    public decimal LastYear { get; set; }
}

/// <summary>
/// Preview of the closing journal entries that will be posted at year-end close.
/// </summary>
public class YearClosingPreview
{
    public DateTime YearStart { get; set; }
    public DateTime YearEnd { get; set; }
    public bool AlreadyFinalised { get; set; }
    public List<YearClosingLine> JournalLines { get; set; } = new();
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetSurplus => TotalIncome - TotalExpenses;
}

/// <summary>A single debit or credit line in the year-end closing journal preview.</summary>
public class YearClosingLine
{
    public string AccountName { get; set; } = string.Empty;
    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
}
