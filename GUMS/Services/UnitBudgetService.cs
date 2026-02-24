using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class UnitBudgetService : IUnitBudgetService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfigurationService _configurationService;

    public UnitBudgetService(ApplicationDbContext context, IConfigurationService configurationService)
    {
        _context = context;
        _configurationService = configurationService;
    }

    /// <inheritdoc/>
    public async Task<UnitBudget?> GetBudgetForYearAsync(DateTime yearEnd)
    {
        return await _context.UnitBudgets
            .AsNoTracking()
            .Include(b => b.Items)
                .ThenInclude(i => i.ExpenseAccount)
            .FirstOrDefaultAsync(b => b.FinancialYearEnd.Date == yearEnd.Date);
    }

    /// <inheritdoc/>
    public async Task<UnitBudget> GetOrCreateBudgetForYearAsync(DateTime yearEnd)
    {
        var existing = await _context.UnitBudgets
            .Include(b => b.Items)
                .ThenInclude(i => i.ExpenseAccount)
            .FirstOrDefaultAsync(b => b.FinancialYearEnd.Date == yearEnd.Date);

        if (existing != null)
            return existing;

        var budget = new UnitBudget
        {
            FinancialYearEnd = yearEnd.Date,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        _context.UnitBudgets.Add(budget);
        await _context.SaveChangesAsync();

        return budget;
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> AddItemAsync(UnitBudgetItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Description))
            return (false, "Description is required.");

        var budget = await _context.UnitBudgets.FindAsync(item.UnitBudgetId);
        if (budget == null)
            return (false, "Budget not found.");

        _context.UnitBudgetItems.Add(item);
        budget.LastModifiedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateItemAsync(UnitBudgetItem item)
    {
        var existing = await _context.UnitBudgetItems
            .Include(i => i.UnitBudget)
            .FirstOrDefaultAsync(i => i.Id == item.Id);

        if (existing == null)
            return (false, "Budget item not found.");

        if (string.IsNullOrWhiteSpace(item.Description))
            return (false, "Description is required.");

        existing.Description = item.Description;
        existing.Frequency = item.Frequency;
        existing.Allocation = item.Allocation;
        existing.Amount = item.Amount;
        existing.ExpenseAccountId = item.ExpenseAccountId;
        existing.Notes = item.Notes;
        existing.SortOrder = item.SortOrder;
        existing.UnitBudget.LastModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteItemAsync(int itemId)
    {
        var item = await _context.UnitBudgetItems
            .Include(i => i.UnitBudget)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
            return (false, "Budget item not found.");

        item.UnitBudget.LastModifiedDate = DateTime.UtcNow;
        _context.UnitBudgetItems.Remove(item);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<UnitBudgetSummary> GetBudgetSummaryAsync(DateTime yearEnd)
    {
        var yearEndDate = yearEnd.Date;
        var yearStart = yearEndDate.AddYears(-1).AddDays(1);

        // Headcounts
        var girlCount = await _context.Persons
            .CountAsync(p => p.IsActive && !p.IsDataRemoved && p.PersonType == PersonType.Girl);

        var personCount = await _context.Persons
            .CountAsync(p => p.IsActive && !p.IsDataRemoved);

        // Terms overlapping the financial year
        var terms = await _context.Terms
            .AsNoTracking()
            .Where(t => t.StartDate < yearEndDate && t.EndDate >= yearStart)
            .ToListAsync();

        var termCount = terms.Count;
        var meetingWeeks = terms.Sum(t => (int)Math.Floor((t.EndDate - t.StartDate).TotalDays / 7));

        // Budget
        var budget = await _context.UnitBudgets
            .AsNoTracking()
            .Include(b => b.Items)
                .ThenInclude(i => i.ExpenseAccount)
            .FirstOrDefaultAsync(b => b.FinancialYearEnd.Date == yearEndDate);

        var summary = new UnitBudgetSummary
        {
            YearStart = yearStart,
            YearEnd = yearEndDate,
            ActiveGirlCount = girlCount,
            ActivePersonCount = personCount,
            TermCount = termCount,
            MeetingWeeks = meetingWeeks
        };

        if (budget == null)
            return summary;

        // Calculate per-line results
        foreach (var item in budget.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
        {
            var repeatMultiplier = item.Frequency switch
            {
                BudgetFrequency.Yearly => 1,
                BudgetFrequency.Termly => termCount,
                BudgetFrequency.Weekly => meetingWeeks,
                _ => 1
            };

            var headcount = item.Allocation switch
            {
                BudgetAllocation.Fixed => 1,
                BudgetAllocation.PerGirl => girlCount,
                BudgetAllocation.PerPerson => personCount,
                _ => 1
            };

            var annualTotal = item.Amount * repeatMultiplier * headcount;
            var perGirlContribution = girlCount > 0 ? annualTotal / girlCount : 0;

            summary.Lines.Add(new UnitBudgetLineResult
            {
                Id = item.Id,
                Description = item.Description,
                Frequency = item.Frequency,
                Allocation = item.Allocation,
                UnitAmount = item.Amount,
                AnnualTotal = annualTotal,
                PerGirlContribution = perGirlContribution,
                ExpenseAccountName = item.ExpenseAccount?.Name
            });
        }

        summary.TotalAnnualCost = summary.Lines.Sum(l => l.AnnualTotal);
        summary.TotalPerGirlAnnual = girlCount > 0 ? summary.TotalAnnualCost / girlCount : 0;
        summary.SuggestedTermlySubs = girlCount > 0 && termCount > 0
            ? Math.Ceiling(summary.TotalAnnualCost / girlCount / termCount)
            : 0;

        // Budget vs actual (only for items linked to an expense account)
        var itemsWithAccounts = budget.Items.Where(i => i.ExpenseAccountId.HasValue).ToList();
        if (itemsWithAccounts.Any())
        {
            var accountIds = itemsWithAccounts
                .Select(i => i.ExpenseAccountId!.Value)
                .Distinct()
                .ToList();

            var actualLines = await _context.TransactionLines
                .AsNoTracking()
                .Include(l => l.Transaction)
                .Where(l => accountIds.Contains(l.AccountId)
                    && !l.Transaction.IsVoided
                    && l.Transaction.Date >= yearStart
                    && l.Transaction.Date <= yearEndDate)
                .ToListAsync();

            foreach (var accountId in accountIds)
            {
                var accountItems = itemsWithAccounts.Where(i => i.ExpenseAccountId == accountId).ToList();
                var budgeted = summary.Lines
                    .Where(l => accountItems.Any(i => i.Id == l.Id))
                    .Sum(l => l.AnnualTotal);
                var actual = actualLines
                    .Where(l => l.AccountId == accountId)
                    .Sum(l => l.Debit - l.Credit);
                var accountName = accountItems.First().ExpenseAccount?.Name ?? "Unknown";

                summary.ActualComparison.Add(new UnitBudgetVsActualLine
                {
                    Description = accountName,
                    ExpenseAccountId = accountId,
                    Budgeted = budgeted,
                    Actual = actual
                });
            }
        }

        return summary;
    }
}
