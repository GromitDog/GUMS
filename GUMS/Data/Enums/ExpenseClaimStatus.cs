namespace GUMS.Data.Enums;

/// <summary>
/// Status of a reimbursement expense claim
/// </summary>
public enum ExpenseClaimStatus
{
    /// <summary>
    /// Leader is still adding receipts
    /// </summary>
    Draft,

    /// <summary>
    /// Ready for settlement
    /// </summary>
    Submitted,

    /// <summary>
    /// Leader has been paid back
    /// </summary>
    Settled
}
