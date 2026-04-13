using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

/// <summary>
/// A cost centre groups transactions by purpose (e.g. "Spring Camp", "Regular Meetings")
/// to enable reporting across both nature (account) and purpose dimensions.
/// </summary>
public class CostCentre
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public List<TransactionLine> TransactionLines { get; set; } = new();
}
