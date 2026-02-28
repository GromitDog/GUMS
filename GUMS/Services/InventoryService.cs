using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IAccountingService _accounting;

    public InventoryService(ApplicationDbContext context, IAccountingService accounting)
    {
        _context = context;
        _accounting = accounting;
    }

    public async Task<List<BadgeStockItem>> GetAllStockItemsAsync()
    {
        return await _context.BadgeStockItems
            .AsNoTracking()
            .Include(i => i.BadgeDefinition)
            .Where(i => i.IsActive)
            .OrderBy(i => i.StockType)
            .ThenBy(i => i.ThemeAwardLevel)
            .ThenBy(i => i.NightsAwayTier)
            .ThenBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<BadgeStockItem?> GetStockItemByIdAsync(int id)
    {
        return await _context.BadgeStockItems
            .AsNoTracking()
            .Include(i => i.BadgeDefinition)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<BadgeStockItem?> GetStockItemForBadgeAsync(int badgeDefinitionId)
    {
        return await _context.BadgeStockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.BadgeDefinitionId == badgeDefinitionId && i.IsActive);
    }

    public async Task<BadgeStockItem?> GetStockItemForThemeAwardLevelAsync(ThemeAwardLevel level)
    {
        return await _context.BadgeStockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.StockType == BadgeStockType.ThemeAward
                                   && i.ThemeAwardLevel == level
                                   && i.IsActive);
    }

    public async Task<BadgeStockItem?> GetStockItemForNightsAwayAsync(int milestone)
    {
        return await _context.BadgeStockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.StockType == BadgeStockType.NightsAway
                                   && i.NightsAwayTier == milestone
                                   && i.IsActive);
    }

    public async Task<(bool Success, string ErrorMessage, BadgeStockItem? Item)> CreateStockItemAsync(BadgeStockItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            return (false, "Name is required.", null);

        // Enforce uniqueness constraints
        if (item.BadgeDefinitionId.HasValue)
        {
            var duplicate = await _context.BadgeStockItems
                .AnyAsync(i => i.BadgeDefinitionId == item.BadgeDefinitionId && i.IsActive);
            if (duplicate)
                return (false, "A stock item already exists for this badge.", null);
        }

        if (item.StockType == BadgeStockType.ThemeAward && item.ThemeAwardLevel.HasValue)
        {
            var duplicate = await _context.BadgeStockItems
                .AnyAsync(i => i.StockType == BadgeStockType.ThemeAward
                             && i.ThemeAwardLevel == item.ThemeAwardLevel
                             && i.IsActive);
            if (duplicate)
                return (false, $"A stock item already exists for the {item.ThemeAwardLevel} award.", null);
        }

        if (item.StockType == BadgeStockType.NightsAway && item.NightsAwayTier.HasValue)
        {
            var duplicate = await _context.BadgeStockItems
                .AnyAsync(i => i.StockType == BadgeStockType.NightsAway
                             && i.NightsAwayTier == item.NightsAwayTier
                             && i.IsActive);
            if (duplicate)
                return (false, $"A stock item already exists for the {item.NightsAwayTier}-night badge.", null);
        }

        item.CurrentQuantity = 0;
        _context.BadgeStockItems.Add(item);
        await _context.SaveChangesAsync();

        return (true, string.Empty, item);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateStockItemAsync(BadgeStockItem item)
    {
        var existing = await _context.BadgeStockItems.FindAsync(item.Id);
        if (existing == null)
            return (false, "Stock item not found.");

        if (string.IsNullOrWhiteSpace(item.Name))
            return (false, "Name is required.");

        existing.Name = item.Name;
        existing.UnitCost = item.UnitCost;
        existing.ReorderThreshold = item.ReorderThreshold;
        existing.Notes = item.Notes;
        existing.IsActive = item.IsActive;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<List<BadgeStockTransaction>> GetTransactionsForItemAsync(int stockItemId)
    {
        return await _context.BadgeStockTransactions
            .AsNoTracking()
            .Where(t => t.BadgeStockItemId == stockItemId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage)> RecordTransactionAsync(BadgeStockTransaction txn)
    {
        var item = await _context.BadgeStockItems.FindAsync(txn.BadgeStockItemId);
        if (item == null)
            return (false, "Stock item not found.");

        var notesRequired = txn.TransactionType is StockTransactionType.ManualIn
            or StockTransactionType.ManualOut
            or StockTransactionType.VisitorAward
            or StockTransactionType.Adjustment;

        if (notesRequired && string.IsNullOrWhiteSpace(txn.Notes))
            return (false, "Notes are required for this transaction type.");

        var newQty = item.CurrentQuantity + txn.Quantity;
        if (newQty < 0)
            return (false, $"This would take stock to {newQty}. Reduce the quantity or first record a Purchase or ManualIn to add stock.");

        if (txn.UnitCost.HasValue)
            txn.TotalCost = txn.UnitCost.Value * Math.Abs(txn.Quantity);

        txn.CreatedDate = DateTime.UtcNow;
        item.CurrentQuantity = newQty;

        _context.BadgeStockTransactions.Add(txn);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> RecordPurchaseAsync(
        BadgeStockTransaction txn, PurchaseExpenseOptions? expenseOptions)
    {
        var item = await _context.BadgeStockItems.FindAsync(txn.BadgeStockItemId);
        if (item == null)
            return (false, "Stock item not found.");

        if (txn.Quantity <= 0)
            return (false, "Quantity must be greater than zero for a purchase.");

        var effectiveCost = txn.UnitCost ?? item.UnitCost;
        txn.TransactionType = StockTransactionType.Purchase;
        txn.UnitCost = effectiveCost;
        if (effectiveCost.HasValue)
            txn.TotalCost = effectiveCost.Value * txn.Quantity;
        txn.CreatedDate = DateTime.UtcNow;

        item.CurrentQuantity += txn.Quantity;
        _context.BadgeStockTransactions.Add(txn);

        // Create expense record if requested
        if (expenseOptions != null && effectiveCost.HasValue)
        {
            var description = $"{txn.Quantity} × {item.Name} (@ £{effectiveCost.Value:N2})";
            var expense = new Expense
            {
                Date = txn.TransactionDate,
                Amount = effectiveCost.Value * txn.Quantity,
                ExpenseAccountId = expenseOptions.ExpenseAccountId,
                Description = description,
                Notes = txn.Notes
            };

            int claimId;
            if (expenseOptions.AddToClaimId.HasValue)
            {
                var addResult = await _accounting.AddExpenseToClaimAsync(expenseOptions.AddToClaimId.Value, expense);
                if (!addResult.Success)
                    return (false, addResult.ErrorMessage);
                claimId = expenseOptions.AddToClaimId.Value;
            }
            else
            {
                var newClaim = new ExpenseClaim
                {
                    ClaimedBy = expenseOptions.ClaimedBy,
                    SubmittedDate = DateTime.Today,
                    Status = ExpenseClaimStatus.Draft
                };
                var claimResult = await _accounting.CreateExpenseClaimAsync(newClaim);
                if (!claimResult.Success)
                    return (false, claimResult.ErrorMessage);
                claimId = claimResult.Claim!.Id;

                var addResult = await _accounting.AddExpenseToClaimAsync(claimId, expense);
                if (!addResult.Success)
                    return (false, addResult.ErrorMessage);
            }

            // Link the claim back to the stock transaction
            txn.ExpenseClaimId = claimId;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task TryDecrementForBadgeAsync(int badgeDefinitionId, int awardedBadgeId)
    {
        var item = await _context.BadgeStockItems
            .FirstOrDefaultAsync(i => i.BadgeDefinitionId == badgeDefinitionId && i.IsActive);

        if (item == null || item.CurrentQuantity <= 0)
            return;

        item.CurrentQuantity--;
        _context.BadgeStockTransactions.Add(new BadgeStockTransaction
        {
            BadgeStockItemId = item.Id,
            TransactionDate = DateTime.Today,
            Quantity = -1,
            TransactionType = StockTransactionType.Award,
            AwardedBadgeId = awardedBadgeId,
            CreatedDate = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task TryDecrementForLevelAsync(string level)
    {
        if (!Enum.TryParse<ThemeAwardLevel>(level, out var themeLevel))
            return;

        var item = await _context.BadgeStockItems
            .FirstOrDefaultAsync(i => i.StockType == BadgeStockType.ThemeAward
                                   && i.ThemeAwardLevel == themeLevel
                                   && i.IsActive);

        if (item == null || item.CurrentQuantity <= 0)
            return;

        item.CurrentQuantity--;
        _context.BadgeStockTransactions.Add(new BadgeStockTransaction
        {
            BadgeStockItemId = item.Id,
            TransactionDate = DateTime.Today,
            Quantity = -1,
            TransactionType = StockTransactionType.Award,
            Notes = $"{level} Award",
            CreatedDate = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task TryDecrementForNightsAwayAsync(int milestone)
    {
        var item = await _context.BadgeStockItems
            .FirstOrDefaultAsync(i => i.StockType == BadgeStockType.NightsAway
                                   && i.NightsAwayTier == milestone
                                   && i.IsActive);

        if (item == null || item.CurrentQuantity <= 0)
            return;

        item.CurrentQuantity--;
        _context.BadgeStockTransactions.Add(new BadgeStockTransaction
        {
            BadgeStockItemId = item.Id,
            TransactionDate = DateTime.Today,
            Quantity = -1,
            TransactionType = StockTransactionType.Award,
            Notes = $"Nights away milestone: {milestone} nights",
            CreatedDate = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task<List<LowStockAlert>> GetLowStockAlertsAsync()
    {
        var items = await _context.BadgeStockItems
            .AsNoTracking()
            .Where(i => i.IsActive && i.ReorderThreshold.HasValue
                     && i.CurrentQuantity < i.ReorderThreshold.Value)
            .ToListAsync();

        return items.Select(i => new LowStockAlert
        {
            StockItemId = i.Id,
            Name = i.Name,
            CurrentQuantity = i.CurrentQuantity,
            ReorderThreshold = i.ReorderThreshold,
            IsShortfall = i.CurrentQuantity == 0
        }).ToList();
    }
}
