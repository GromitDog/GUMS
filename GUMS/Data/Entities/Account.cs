using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

/// <summary>
/// Represents an account in the chart of accounts for cash accounting.
/// </summary>
public class Account
{
    public int Id { get; set; }

    /// <summary>
    /// Account code (e.g., "1001", "4001")
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Account name (e.g., "Cash on Hand", "Bank Account")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of account (Asset or Income)
    /// </summary>
    [Required]
    public AccountType Type { get; set; }

    /// <summary>
    /// True for system accounts that cannot be deleted
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Running balance for the account
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Navigation property for transaction lines
    /// </summary>
    public List<TransactionLine> TransactionLines { get; set; } = new();
}
