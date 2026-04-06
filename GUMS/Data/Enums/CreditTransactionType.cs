namespace GUMS.Data.Enums;

public enum CreditTransactionType
{
    /// <summary>
    /// Payment converted to credit (positive amount)
    /// </summary>
    Deposit,

    /// <summary>
    /// Credit applied against a pending payment (negative amount)
    /// </summary>
    Applied,

    /// <summary>
    /// Credit refunded as cash (negative amount)
    /// </summary>
    Refunded
}
