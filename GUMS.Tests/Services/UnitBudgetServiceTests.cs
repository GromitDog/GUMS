using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GUMS.Tests.Services;

public class UnitBudgetServiceTests : IDisposable
{
    private static readonly DateTime LastYearEnd = new(2025, 8, 31);
    private static readonly DateTime ThisYearEnd = new(2026, 8, 31);

    private readonly ApplicationDbContext _context;
    private readonly UnitBudgetService _sut;

    public UnitBudgetServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _sut = new UnitBudgetService(_context, new Mock<IConfigurationService>().Object);
    }

    public void Dispose() => _context?.Dispose();

    // ---- Helpers ---------------------------------------------------------

    private async Task<Account> AddExpenseAccount()
    {
        var account = new Account
        {
            Code = "5001",
            Name = "Hall Hire",
            Type = AccountType.Expense
        };
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    private async Task<UnitBudget> AddBudget(DateTime yearEnd, params UnitBudgetItem[] items)
    {
        var budget = new UnitBudget { FinancialYearEnd = yearEnd };
        _context.UnitBudgets.Add(budget);
        await _context.SaveChangesAsync();

        foreach (var item in items)
        {
            item.UnitBudgetId = budget.Id;
            _context.UnitBudgetItems.Add(item);
        }
        await _context.SaveChangesAsync();
        return budget;
    }

    // ---- GetBudgetSummaryAsync -------------------------------------------

    [Fact]
    public async Task GetBudgetSummaryAsync_ActualSpend_ExcludesYearEndClosingJournal()
    {
        var account = await AddExpenseAccount();
        var cash = new Account { Code = "1001", Name = "Cash on Hand", Type = AccountType.Asset };
        var equity = new Account { Code = "3001", Name = "Opening Balances", Type = AccountType.Equity };
        _context.Accounts.AddRange(cash, equity);
        await _context.SaveChangesAsync();

        await AddBudget(ThisYearEnd,
            new UnitBudgetItem
            {
                Description = "Hall hire",
                Frequency = BudgetFrequency.Yearly,
                Allocation = BudgetAllocation.Fixed,
                Amount = 300m,
                ExpenseAccountId = account.Id
            });

        // Real spend during the year
        _context.Transactions.Add(new Transaction
        {
            Date = new DateTime(2026, 3, 1),
            Description = "Hall hire",
            Lines =
            {
                new TransactionLine { AccountId = account.Id, Debit = 120m },
                new TransactionLine { AccountId = cash.Id, Credit = 120m }
            }
        });

        // Year-end closing journal that zeroes the expense account on the year-end date
        _context.Transactions.Add(new Transaction
        {
            Date = ThisYearEnd,
            Description = "Year end close",
            IsYearEndClose = true,
            Lines =
            {
                new TransactionLine { AccountId = equity.Id, Debit = 120m },
                new TransactionLine { AccountId = account.Id, Credit = 120m }
            }
        });
        await _context.SaveChangesAsync();

        var summary = await _sut.GetBudgetSummaryAsync(ThisYearEnd);

        var line = summary.ActualComparison.Single(l => l.ExpenseAccountId == account.Id);
        line.Budgeted.Should().Be(300m);
        line.Actual.Should().Be(120m);
    }

    // ---- CopyBudgetItemsAsync --------------------------------------------

    [Fact]
    public async Task CopyBudgetItemsAsync_CopiesAllItemFields_IntoNewBudget()
    {
        var account = await AddExpenseAccount();
        await AddBudget(LastYearEnd,
            new UnitBudgetItem
            {
                Description = "Hall hire",
                Frequency = BudgetFrequency.Weekly,
                Allocation = BudgetAllocation.Fixed,
                Amount = 25m,
                ExpenseAccountId = account.Id,
                Notes = "Scout hut",
                SortOrder = 1
            },
            new UnitBudgetItem
            {
                Description = "Badges",
                Frequency = BudgetFrequency.Termly,
                Allocation = BudgetAllocation.PerGirl,
                Amount = 3.50m,
                SortOrder = 2
            });

        var result = await _sut.CopyBudgetItemsAsync(LastYearEnd, ThisYearEnd);

        result.Success.Should().BeTrue();
        result.ItemsCopied.Should().Be(2);

        var target = await _context.UnitBudgets
            .AsNoTracking()
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.FinancialYearEnd == ThisYearEnd);
        target.Should().NotBeNull();
        target!.Items.Should().HaveCount(2);

        var hall = target.Items.Single(i => i.Description == "Hall hire");
        hall.Frequency.Should().Be(BudgetFrequency.Weekly);
        hall.Allocation.Should().Be(BudgetAllocation.Fixed);
        hall.Amount.Should().Be(25m);
        hall.ExpenseAccountId.Should().Be(account.Id);
        hall.Notes.Should().Be("Scout hut");
        hall.SortOrder.Should().Be(1);

        var badges = target.Items.Single(i => i.Description == "Badges");
        badges.Frequency.Should().Be(BudgetFrequency.Termly);
        badges.Allocation.Should().Be(BudgetAllocation.PerGirl);
        badges.Amount.Should().Be(3.50m);
        badges.ExpenseAccountId.Should().BeNull();
    }

    [Fact]
    public async Task CopyBudgetItemsAsync_LeavesSourceBudgetUnchanged()
    {
        await AddBudget(LastYearEnd,
            new UnitBudgetItem { Description = "Craft supplies", Frequency = BudgetFrequency.Yearly, Allocation = BudgetAllocation.Fixed, Amount = 100m });

        await _sut.CopyBudgetItemsAsync(LastYearEnd, ThisYearEnd);

        var source = await _context.UnitBudgets
            .AsNoTracking()
            .Include(b => b.Items)
            .FirstAsync(b => b.FinancialYearEnd == LastYearEnd);
        source.Items.Should().ContainSingle(i => i.Description == "Craft supplies");
    }

    [Fact]
    public async Task CopyBudgetItemsAsync_Fails_WhenNoSourceBudget()
    {
        var result = await _sut.CopyBudgetItemsAsync(LastYearEnd, ThisYearEnd);

        result.Success.Should().BeFalse();
        result.ItemsCopied.Should().Be(0);
        (await _context.UnitBudgetItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CopyBudgetItemsAsync_Fails_WhenSourceBudgetHasNoItems()
    {
        await AddBudget(LastYearEnd);

        var result = await _sut.CopyBudgetItemsAsync(LastYearEnd, ThisYearEnd);

        result.Success.Should().BeFalse();
        result.ItemsCopied.Should().Be(0);
    }

    [Fact]
    public async Task CopyBudgetItemsAsync_Fails_WhenTargetAlreadyHasItems()
    {
        await AddBudget(LastYearEnd,
            new UnitBudgetItem { Description = "Hall hire", Frequency = BudgetFrequency.Weekly, Allocation = BudgetAllocation.Fixed, Amount = 25m });
        await AddBudget(ThisYearEnd,
            new UnitBudgetItem { Description = "Existing item", Frequency = BudgetFrequency.Yearly, Allocation = BudgetAllocation.Fixed, Amount = 10m });

        var result = await _sut.CopyBudgetItemsAsync(LastYearEnd, ThisYearEnd);

        result.Success.Should().BeFalse();
        result.ItemsCopied.Should().Be(0);

        var target = await _context.UnitBudgets
            .AsNoTracking()
            .Include(b => b.Items)
            .FirstAsync(b => b.FinancialYearEnd == ThisYearEnd);
        target.Items.Should().ContainSingle(i => i.Description == "Existing item");
    }
}
