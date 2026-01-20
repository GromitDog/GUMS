using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

/// <summary>
/// Represents a debit or credit entry within a transaction.
/// </summary>
public class TransactionLine
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the parent transaction
    /// </summary>
    [Required]
    public int TransactionId { get; set; }

    /// <summary>
    /// Foreign key to the account being affected
    /// </summary>
    [Required]
    public int AccountId { get; set; }

    /// <summary>
    /// Debit amount (increases asset accounts, decreases income accounts)
    /// </summary>
    [Range(0, 1000000)]
    public decimal Debit { get; set; }

    /// <summary>
    /// Credit amount (decreases asset accounts, increases income accounts)
    /// </summary>
    [Range(0, 1000000)]
    public decimal Credit { get; set; }

    /// <summary>
    /// Navigation property for the parent transaction
    /// </summary>
    public Transaction Transaction { get; set; } = null!;

    /// <summary>
    /// Navigation property for the account
    /// </summary>
    public Account Account { get; set; } = null!;
}
