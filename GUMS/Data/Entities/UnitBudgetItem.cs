using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class UnitBudgetItem
{
    public int Id { get; set; }

    [Required]
    public int UnitBudgetId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public BudgetFrequency Frequency { get; set; }

    [Required]
    public BudgetAllocation Allocation { get; set; }

    [Required]
    [Range(0, 1000000)]
    public decimal Amount { get; set; }

    public int? ExpenseAccountId { get; set; }

    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    // Navigation properties
    public UnitBudget UnitBudget { get; set; } = null!;
    public Account? ExpenseAccount { get; set; }
}
