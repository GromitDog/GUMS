using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class UnitBudget
{
    public int Id { get; set; }

    [Required]
    public DateTime FinancialYearEnd { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<UnitBudgetItem> Items { get; set; } = new();
}
