using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class BudgetService : IBudgetService
{
    private readonly ApplicationDbContext _context;

    public BudgetService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<EventBudget?> GetBudgetForMeetingAsync(int meetingId)
    {
        return await _context.EventBudgets
            .AsNoTracking()
            .Include(b => b.Meeting)
            .Include(b => b.Items)
                .ThenInclude(i => i.ExpenseAccount)
            .FirstOrDefaultAsync(b => b.MeetingId == meetingId);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage, EventBudget? Budget)> CreateBudgetAsync(int meetingId, string? notes)
    {
        var meeting = await _context.Meetings.FindAsync(meetingId);
        if (meeting == null)
            return (false, "Meeting not found.", null);

        var existing = await _context.EventBudgets.AnyAsync(b => b.MeetingId == meetingId);
        if (existing)
            return (false, "A budget already exists for this meeting.", null);

        var budget = new EventBudget
        {
            MeetingId = meetingId,
            Notes = notes,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _context.EventBudgets.Add(budget);
        await _context.SaveChangesAsync();

        return (true, string.Empty, budget);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> AddBudgetItemAsync(EventBudgetItem item)
    {
        var budget = await _context.EventBudgets.FindAsync(item.EventBudgetId);
        if (budget == null)
            return (false, "Budget not found.");

        if (string.IsNullOrWhiteSpace(item.Description))
            return (false, "Description is required.");

        _context.EventBudgetItems.Add(item);
        budget.LastModifiedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateBudgetItemAsync(EventBudgetItem item)
    {
        var existing = await _context.EventBudgetItems
            .Include(i => i.EventBudget)
            .FirstOrDefaultAsync(i => i.Id == item.Id);

        if (existing == null)
            return (false, "Budget item not found.");

        existing.Description = item.Description;
        existing.CostType = item.CostType;
        existing.Amount = item.Amount;
        existing.CostStatus = item.CostStatus;
        existing.ExpenseAccountId = item.ExpenseAccountId;
        existing.EventBudget.LastModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteBudgetItemAsync(int itemId)
    {
        var item = await _context.EventBudgetItems
            .Include(i => i.EventBudget)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
            return (false, "Budget item not found.");

        item.EventBudget.LastModifiedDate = DateTime.UtcNow;
        _context.EventBudgetItems.Remove(item);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<BudgetEstimate?> GetBudgetEstimateAsync(int meetingId)
    {
        var budget = await _context.EventBudgets
            .AsNoTracking()
            .Include(b => b.Meeting)
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.MeetingId == meetingId);

        if (budget == null)
            return null;

        var girlCount = await _context.Persons
            .CountAsync(p => p.IsActive && !p.IsDataRemoved && p.PersonType == PersonType.Girl);

        var adultCount = await _context.Persons
            .CountAsync(p => p.IsActive && !p.IsDataRemoved && p.PersonType == PersonType.Leader);

        var highGirls = girlCount;
        var midGirls = (int)Math.Round(girlCount * 0.75m);
        var lowGirls = (int)Math.Round(girlCount * 0.5m);

        var estimate = new BudgetEstimate
        {
            MeetingId = meetingId,
            MeetingTitle = budget.Meeting.Title,
            GirlCount = girlCount,
            AdultCount = adultCount,
            HighGirls = highGirls,
            MidGirls = midGirls,
            LowGirls = lowGirls
        };

        estimate.HighTotal = CalculateScenarioTotal(budget.Items, highGirls, adultCount);
        estimate.MidTotal = CalculateScenarioTotal(budget.Items, midGirls, adultCount);
        estimate.LowTotal = CalculateScenarioTotal(budget.Items, lowGirls, adultCount);

        var highHeadcount = highGirls + adultCount;
        var midHeadcount = midGirls + adultCount;
        var lowHeadcount = lowGirls + adultCount;

        estimate.HighPerPerson = highHeadcount > 0 ? estimate.HighTotal / highHeadcount : 0;
        estimate.MidPerPerson = midHeadcount > 0 ? estimate.MidTotal / midHeadcount : 0;
        estimate.LowPerPerson = lowHeadcount > 0 ? estimate.LowTotal / lowHeadcount : 0;

        estimate.HighPerGirl = highGirls > 0 ? estimate.HighTotal / highGirls : 0;
        estimate.MidPerGirl = midGirls > 0 ? estimate.MidTotal / midGirls : 0;
        estimate.LowPerGirl = lowGirls > 0 ? estimate.LowTotal / lowGirls : 0;

        return estimate;
    }

    /// <inheritdoc/>
    public async Task<BudgetVsActual?> GetBudgetVsActualAsync(int meetingId)
    {
        var budget = await _context.EventBudgets
            .AsNoTracking()
            .Include(b => b.Meeting)
            .Include(b => b.Items)
                .ThenInclude(i => i.ExpenseAccount)
            .FirstOrDefaultAsync(b => b.MeetingId == meetingId);

        if (budget == null)
            return null;

        // Girls: count those with consent forms received (meaningful before the register is done)
        // Leaders: count those marked as planning to attend
        var attendees = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .Join(_context.Persons,
                a => a.MembershipNumber,
                p => p.MembershipNumber,
                (a, p) => new { a.ConsentFormReceived, a.PlanningToAttend, p.PersonType })
            .ToListAsync();

        var girlCount = attendees.Count(a => a.PersonType == PersonType.Girl && a.ConsentFormReceived);
        var adultCount = attendees.Count(a => a.PersonType == PersonType.Leader && a.PlanningToAttend);

        var actualExpenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.ExpenseAccount)
            .Where(e => e.MeetingId == meetingId)
            .ToListAsync();

        // Income: expected vs received — include Activity and Other payments linked to this meeting
        var costPerAttendee = budget.Meeting.CostPerAttendee;
        var activityPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.MeetingId == meetingId
                     && p.Status != PaymentStatus.Cancelled
                     && p.Status != PaymentStatus.Refunded)
            .ToListAsync();

        var result = new BudgetVsActual
        {
            MeetingId = meetingId,
            MeetingTitle = budget.Meeting.Title,
            CostPerAttendee = costPerAttendee,
            ConsentedGirlCount = girlCount,
            ConsentedAdultCount = adultCount,
            ExpectedIncome = costPerAttendee.HasValue ? costPerAttendee.Value * (girlCount + adultCount) : 0,
            PaidCount = activityPayments.Count(p => p.Status == PaymentStatus.Paid),
            ActualIncome = activityPayments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.AmountPaid)
        };

        // Group budget items by expense account
        var allAccountIds = budget.Items
            .Where(i => i.ExpenseAccountId.HasValue)
            .Select(i => i.ExpenseAccountId!.Value)
            .Union(actualExpenses.Select(e => e.ExpenseAccountId))
            .Distinct()
            .ToList();

        foreach (var accountId in allAccountIds)
        {
            var budgetItems = budget.Items.Where(i => i.ExpenseAccountId == accountId).ToList();
            var budgeted = CalculateScenarioTotal(budgetItems, girlCount, adultCount);
            var actual = actualExpenses.Where(e => e.ExpenseAccountId == accountId).Sum(e => e.Amount);
            var categoryName = budgetItems.FirstOrDefault()?.ExpenseAccount?.Name
                ?? actualExpenses.FirstOrDefault(e => e.ExpenseAccountId == accountId)?.ExpenseAccount?.Name
                ?? "Unknown";

            result.Lines.Add(new BudgetVsActualLine
            {
                Category = categoryName,
                ExpenseAccountId = accountId,
                Budgeted = budgeted,
                Actual = actual
            });
        }

        // Budget items without an expense account
        var uncategorizedBudgetItems = budget.Items.Where(i => !i.ExpenseAccountId.HasValue).ToList();
        if (uncategorizedBudgetItems.Any())
        {
            result.Lines.Add(new BudgetVsActualLine
            {
                Category = "Uncategorised",
                Budgeted = CalculateScenarioTotal(uncategorizedBudgetItems, girlCount, adultCount),
                Actual = 0
            });
        }

        result.TotalBudgeted = result.Lines.Sum(l => l.Budgeted);
        result.TotalActual = result.Lines.Sum(l => l.Actual);

        return result;
    }

    private static decimal CalculateScenarioTotal(List<EventBudgetItem> items, int girlCount, int adultCount)
    {
        decimal total = 0;
        foreach (var item in items)
        {
            total += item.CostType switch
            {
                BudgetCostType.PerGirl => item.Amount * girlCount,
                BudgetCostType.PerAdult => item.Amount * adultCount,
                BudgetCostType.PerPerson => item.Amount * (girlCount + adultCount),
                BudgetCostType.FixedTotal => item.Amount,
                _ => 0
            };
        }
        return total;
    }
}
