using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GUMS.Tests.Services;

public class InventoryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _sut = new InventoryService(_context, new Mock<IAccountingService>().Object);
    }

    public void Dispose() => _context?.Dispose();

    private async Task<(BadgeDefinition Badge, BadgeStockItem Stock)> SetupBadgeWithStock(int initialQuantity)
    {
        var badge = new BadgeDefinition
        {
            Name = "Test Badge",
            BadgeType = BadgeType.InterestBadge,
            Section = Section.Brownie,
            RequiredCompletions = 1
        };
        _context.BadgeDefinitions.Add(badge);
        await _context.SaveChangesAsync();

        var stock = new BadgeStockItem
        {
            Name = "Test Badge",
            StockType = BadgeStockType.InterestBadge,
            BadgeDefinitionId = badge.Id,
            CurrentQuantity = initialQuantity,
            IsActive = true
        };
        _context.BadgeStockItems.Add(stock);
        await _context.SaveChangesAsync();

        return (badge, stock);
    }

    [Fact]
    public async Task TryIncrementForBadgeAsync_RestoresStock_WhenPriorAwardTxnExists()
    {
        var (badge, stock) = await SetupBadgeWithStock(5);

        // Simulate an award decrement.
        await _sut.TryDecrementForBadgeAsync(badge.Id, awardedBadgeId: 42);

        (await _context.BadgeStockItems.FindAsync(stock.Id))!.CurrentQuantity.Should().Be(4);

        // Now reverse it.
        await _sut.TryIncrementForBadgeAsync(badge.Id, awardedBadgeId: 42);

        var after = await _context.BadgeStockItems.FindAsync(stock.Id);
        after!.CurrentQuantity.Should().Be(5);

        var txns = await _context.BadgeStockTransactions
            .Where(t => t.BadgeStockItemId == stock.Id)
            .OrderBy(t => t.Id)
            .ToListAsync();
        txns.Should().HaveCount(2);
        txns[0].TransactionType.Should().Be(StockTransactionType.Award);
        txns[0].Quantity.Should().Be(-1);
        txns[1].TransactionType.Should().Be(StockTransactionType.Adjustment);
        txns[1].Quantity.Should().Be(1);
        txns[1].AwardedBadgeId.Should().Be(42);
    }

    [Fact]
    public async Task TryIncrementForBadgeAsync_IsNoOp_WhenNoPriorAwardTxn()
    {
        var (badge, stock) = await SetupBadgeWithStock(3);

        await _sut.TryIncrementForBadgeAsync(badge.Id, awardedBadgeId: 99);

        var after = await _context.BadgeStockItems.FindAsync(stock.Id);
        after!.CurrentQuantity.Should().Be(3);

        (await _context.BadgeStockTransactions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task TryIncrementForBadgeAsync_IsNoOp_WhenStockItemMissing()
    {
        var badge = new BadgeDefinition
        {
            Name = "No Stock Badge",
            BadgeType = BadgeType.FunBadge,
            Section = Section.Guide,
            RequiredCompletions = 1
        };
        _context.BadgeDefinitions.Add(badge);
        await _context.SaveChangesAsync();

        await _sut.TryIncrementForBadgeAsync(badge.Id, awardedBadgeId: 1);

        (await _context.BadgeStockTransactions.AnyAsync()).Should().BeFalse();
    }
}
