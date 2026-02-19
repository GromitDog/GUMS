using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class BankReconciliation
{
    public int Id { get; set; }

    [Required]
    public DateTime StatementDate { get; set; }

    public decimal StatementBalance { get; set; }

    [Required]
    public ReconciliationStatus Status { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Book balance at the time of completion, stored for audit purposes
    /// </summary>
    public decimal? ReconciledBookBalance { get; set; }

    /// <summary>
    /// Transaction lines cleared in this reconciliation session
    /// </summary>
    public List<TransactionLine> ReconciledLines { get; set; } = new();
}
