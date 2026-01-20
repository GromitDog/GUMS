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
    /// <param name="paymentId">The payment ID.</param>
    /// <param name="amount">The amount being paid.</param>
    /// <param name="paymentMethod">How the payment was received.</param>
    /// <param name="paymentType">Type of payment (Subs or Activity).</param>
    /// <param name="description">Description for the journal entry.</param>
    /// <param name="date">Date of the transaction.</param>
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
    /// <param name="cashAmount">Amount of cash to deposit.</param>
    /// <param name="chequeAmount">Amount of cheques to deposit.</param>
    /// <param name="date">Date of deposit.</param>
    /// <param name="notes">Optional notes about the deposit.</param>
    Task<(bool Success, string ErrorMessage)> BankDepositAsync(
        decimal cashAmount,
        decimal chequeAmount,
        DateTime date,
        string? notes = null);

    // ===== Reporting =====

    /// <summary>
    /// Gets an income report for a date range.
    /// </summary>
    Task<IncomeReport> GetIncomeReportAsync(DateTime dateFrom, DateTime dateTo);

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
}
