using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

/// <summary>
/// Represents an expense (either direct or part of a reimbursement claim).
/// </summary>
public class Expense
{
    public int Id { get; set; }

    /// <summary>
    /// Date of purchase
    /// </summary>
    [Required]
    public DateTime Date { get; set; }

    /// <summary>
    /// Amount of the expense
    /// </summary>
    [Required]
    [Range(0.01, 100000)]
    public decimal Amount { get; set; }

    /// <summary>
    /// FK to expense category account
    /// </summary>
    [Required]
    public int ExpenseAccountId { get; set; }

    /// <summary>
    /// What was purchased
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Receipt/invoice number
    /// </summary>
    [MaxLength(100)]
    public string? Reference { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional event link for event P&L
    /// </summary>
    public int? MeetingId { get; set; }

    // Direct expense fields

    /// <summary>
    /// Asset account expense was paid from - null if reimbursement
    /// </summary>
    public int? PaidFromAccountId { get; set; }

    /// <summary>
    /// Link to accounting entry for direct expenses
    /// </summary>
    public int? TransactionId { get; set; }

    // Reimbursement fields

    /// <summary>
    /// FK to expense claim - null if direct expense
    /// </summary>
    public int? ExpenseClaimId { get; set; }

    // Navigation properties
    public Account ExpenseAccount { get; set; } = null!;
    public Account? PaidFromAccount { get; set; }
    public Meeting? Meeting { get; set; }
    public Transaction? Transaction { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }
}
