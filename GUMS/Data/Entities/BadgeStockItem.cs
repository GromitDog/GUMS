using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class BadgeStockItem
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public BadgeStockType StockType { get; set; }

    public int? BadgeDefinitionId { get; set; }
    public BadgeDefinition? BadgeDefinition { get; set; }

    public ThemeAwardLevel? ThemeAwardLevel { get; set; }

    public int? NightsAwayTier { get; set; }

    public decimal? UnitCost { get; set; }

    public int? ReorderThreshold { get; set; }

    public int CurrentQuantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public List<BadgeStockTransaction> Transactions { get; set; } = new();
}
