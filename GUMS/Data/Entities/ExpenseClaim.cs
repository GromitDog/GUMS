using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

/// <summary>
/// Represents a reimbursement claim submitted by a leader.
/// </summary>
public class ExpenseClaim
{
    public int Id { get; set; }

    /// <summary>
    /// Name of the leader who incurred the expenses
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ClaimedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date the claim was submitted
    /// </summary>
    [Required]
    public DateTime SubmittedDate { get; set; }

    /// <summary>
    /// Current status of the claim
    /// </summary>
    [Required]
    public ExpenseClaimStatus Status { get; set; }

    /// <summary>
    /// Additional notes about the claim
    /// </summary>
    public string? Notes { get; set; }

    // Settlement fields (populated when status = Settled)

    /// <summary>
    /// Date the claim was settled
    /// </summary>
    public DateTime? SettledDate { get; set; }

    /// <summary>
    /// Asset account used to pay the claim
    /// </summary>
    public int? PaidFromAccountId { get; set; }

    /// <summary>
    /// How the claim was paid
    /// </summary>
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>
    /// Link to accounting entry created on settlement
    /// </summary>
    public int? TransactionId { get; set; }

    /// <summary>
    /// Computed total of all expenses in this claim
    /// </summary>
    public decimal TotalAmount => Expenses?.Sum(e => e.Amount) ?? 0;

    // Navigation properties
    public List<Expense> Expenses { get; set; } = new();
    public Account? PaidFromAccount { get; set; }
    public Transaction? Transaction { get; set; }
}
