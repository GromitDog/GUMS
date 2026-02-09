namespace GUMS.Data.Enums;

/// <summary>
/// Type of accounting account
/// </summary>
public enum AccountType
{
    /// <summary>
    /// Asset accounts (Cash on Hand, Bank Account, Cheques Pending)
    /// </summary>
    Asset,

    /// <summary>
    /// Income accounts (Subscription Income, Activity Income)
    /// </summary>
    Income,

    /// <summary>
    /// Expense accounts (Supplies, Equipment, etc.) - Debit increases balance (same as Asset)
    /// </summary>
    Expense,

    /// <summary>
    /// Liability accounts (amounts owed). Credit increases, Debit decreases.
    /// </summary>
    Liability,

    /// <summary>
    /// Equity accounts (Opening Balances, etc.). Credit increases, Debit decreases.
    /// </summary>
    Equity
}
