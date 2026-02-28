using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

public interface IInventoryService
{
    // Stock item management
    Task<List<BadgeStockItem>> GetAllStockItemsAsync();
    Task<BadgeStockItem?> GetStockItemByIdAsync(int id);
    Task<BadgeStockItem?> GetStockItemForBadgeAsync(int badgeDefinitionId);
    Task<BadgeStockItem?> GetStockItemForThemeAwardLevelAsync(ThemeAwardLevel level);
    Task<BadgeStockItem?> GetStockItemForNightsAwayAsync(int milestone);
    Task<(bool Success, string ErrorMessage, BadgeStockItem? Item)> CreateStockItemAsync(BadgeStockItem item);
    Task<(bool Success, string ErrorMessage)> UpdateStockItemAsync(BadgeStockItem item);

    // Transactions
    Task<List<BadgeStockTransaction>> GetTransactionsForItemAsync(int stockItemId);
    Task<(bool Success, string ErrorMessage)> RecordTransactionAsync(BadgeStockTransaction txn);

    // Purchase with optional expense claim
    Task<(bool Success, string ErrorMessage)> RecordPurchaseAsync(
        BadgeStockTransaction txn, PurchaseExpenseOptions? expenseOptions);

    // Award hooks — auto-decrement, floor at 0
    Task TryDecrementForBadgeAsync(int badgeDefinitionId, int awardedBadgeId);
    Task TryDecrementForLevelAsync(string level);
    Task TryDecrementForNightsAwayAsync(int milestone);

    // Alerts
    Task<List<LowStockAlert>> GetLowStockAlertsAsync();
}

public class PurchaseExpenseOptions
{
    public int ExpenseAccountId { get; set; }
    /// <summary>Null = create a new draft claim. Set to an existing draft claim ID to append.</summary>
    public int? AddToClaimId { get; set; }
    /// <summary>Required when AddToClaimId is null (creating a new claim).</summary>
    public string ClaimedBy { get; set; } = string.Empty;
}

public class LowStockAlert
{
    public int StockItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int? ReorderThreshold { get; set; }
    public bool IsShortfall { get; set; }
}
