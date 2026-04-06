using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class CreditTransaction
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    /// <summary>
    /// Positive = credit added, Negative = credit used/refunded
    /// </summary>
    [Required]
    public decimal Amount { get; set; }

    [Required]
    public CreditTransactionType Type { get; set; }

    /// <summary>
    /// The payment that was converted to credit (for Deposit type)
    /// </summary>
    public int? SourcePaymentId { get; set; }
    public Payment? SourcePayment { get; set; }

    /// <summary>
    /// The payment that credit was applied to (for Applied type)
    /// </summary>
    public int? TargetPaymentId { get; set; }
    public Payment? TargetPayment { get; set; }

    /// <summary>
    /// The accounting journal entry for this credit movement
    /// </summary>
    public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    [Required]
    public DateTime Date { get; set; } = DateTime.Today;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
