using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class EventBudgetItem
{
    public int Id { get; set; }

    [Required]
    public int EventBudgetId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public BudgetCostType CostType { get; set; }

    [Required]
    [Range(0, 1000000)]
    public decimal Amount { get; set; }

    [Required]
    public BudgetCostStatus CostStatus { get; set; } = BudgetCostStatus.Estimate;

    public int? ExpenseAccountId { get; set; }

    // Navigation properties
    public EventBudget EventBudget { get; set; } = null!;
    public Account? ExpenseAccount { get; set; }
}
