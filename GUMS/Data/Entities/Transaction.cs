using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

/// <summary>
/// Represents a journal entry in the accounting system.
/// </summary>
public class Transaction
{
    public int Id { get; set; }

    /// <summary>
    /// Date of the transaction
    /// </summary>
    [Required]
    public DateTime Date { get; set; }

    /// <summary>
    /// Description of the transaction (e.g., "Payment from Jane Smith - Spring Term Subs")
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional link to the payment record that created this transaction
    /// </summary>
    public int? PaymentId { get; set; }

    /// <summary>
    /// Navigation property for related payment
    /// </summary>
    public Payment? Payment { get; set; }

    /// <summary>
    /// Transaction lines (debits and credits)
    /// </summary>
    public List<TransactionLine> Lines { get; set; } = new();

    /// <summary>
    /// Calculated total debits - should equal total credits
    /// </summary>
    public decimal TotalDebits => Lines.Sum(l => l.Debit);

    /// <summary>
    /// Calculated total credits - should equal total debits
    /// </summary>
    public decimal TotalCredits => Lines.Sum(l => l.Credit);

    /// <summary>
    /// Indicates if the transaction is balanced (debits = credits)
    /// </summary>
    public bool IsBalanced => TotalDebits == TotalCredits;
}
