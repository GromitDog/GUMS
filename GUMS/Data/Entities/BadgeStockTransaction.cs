using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class BadgeStockTransaction
{
    public int Id { get; set; }

    public int BadgeStockItemId { get; set; }
    public BadgeStockItem BadgeStockItem { get; set; } = default!;

    public DateTime TransactionDate { get; set; }

    /// <summary>Positive = stock in, negative = stock out.</summary>
    public int Quantity { get; set; }

    [Required]
    public StockTransactionType TransactionType { get; set; }

    public decimal? UnitCost { get; set; }

    /// <summary>UnitCost × |Quantity|, stored for audit.</summary>
    public decimal? TotalCost { get; set; }

    public int? AwardedBadgeId { get; set; }

    public int? AwardedThemeAwardId { get; set; }

    public int? ExpenseClaimId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
