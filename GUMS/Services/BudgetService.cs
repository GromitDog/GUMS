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
            .Include(b => b.IncomeItems)
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
    public async Task<BudgetEstimate?> GetBudgetEstimateAsync(int meetingId, bool? leadersPayOverride = null, int? adultCountOverride = null)
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

        var activeLeaderCount = await _context.Persons
            .CountAsync(p => p.IsActive && !p.IsDataRemoved && p.PersonType == PersonType.Leader);

        // Leaders pay: explicit override → stored choice → derive from the meeting's leader charge.
        var leadersPay = leadersPayOverride
            ?? budget.LeadersPay
            ?? (budget.Meeting.CostPerLeader ?? 0) > 0;

        // Adults for the split: explicit override → stored planned count → current active leaders.
        var adultCount = adultCountOverride
            ?? budget.PlannedAdultCount
            ?? activeLeaderCount;
        if (adultCount < 0) adultCount = 0;

        var estimate = new BudgetEstimate
        {
            MeetingId = meetingId,
            MeetingTitle = budget.Meeting.Title,
            GirlCount = girlCount,
            AdultCount = adultCount,
            ActiveLeaderCount = activeLeaderCount,
            LeadersPay = leadersPay,
            CostPerAttendee = budget.Meeting.CostPerAttendee,
            CostPerLeader = budget.Meeting.CostPerLeader
        };

        var scenarios = new[]
        {
            ("Full turnout", girlCount),
            ("Likely (75%)", (int)Math.Round(girlCount * 0.75m)),
            ("Low (50%)", (int)Math.Round(girlCount * 0.5m))
        };

        foreach (var (label, girls) in scenarios)
        {
            var total = CalculateScenarioTotal(budget.Items, girls, adultCount);
            var (perGirl, perAdult) = CalculateBreakEven(budget.Items, girls, adultCount, leadersPay);

            var income = (budget.Meeting.CostPerAttendee ?? 0) * girls;
            if (leadersPay)
                income += (budget.Meeting.CostPerLeader ?? 0) * adultCount;

            estimate.Scenarios.Add(new BudgetScenario
            {
                Label = label,
                Girls = girls,
                Adults = adultCount,
                TotalCost = total,
                CostPerGirl = perGirl,
                CostPerAdult = perAdult,
                ProjectedIncome = income
            });
        }

        return estimate;
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateBudgetPlanningAsync(int meetingId, bool? leadersPay, int? plannedAdultCount)
    {
        var budget = await _context.EventBudgets.FirstOrDefaultAsync(b => b.MeetingId == meetingId);
        if (budget == null)
            return (false, "Budget not found.");

        if (plannedAdultCount is < 0)
            return (false, "Adult count cannot be negative.");

        budget.LeadersPay = leadersPay;
        budget.PlannedAdultCount = plannedAdultCount;
        budget.LastModifiedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> AddBudgetIncomeAsync(EventBudgetIncome income)
    {
        var budget = await _context.EventBudgets.FindAsync(income.EventBudgetId);
        if (budget == null)
            return (false, "Budget not found.");

        if (string.IsNullOrWhiteSpace(income.Description))
            return (false, "Description is required.");

        if (income.Amount < 0)
            return (false, "Amount cannot be negative.");

        _context.EventBudgetIncomes.Add(income);
        budget.LastModifiedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteBudgetIncomeAsync(int incomeId)
    {
        var income = await _context.EventBudgetIncomes
            .Include(i => i.EventBudget)
            .FirstOrDefaultAsync(i => i.Id == incomeId);

        if (income == null)
            return (false, "Income line not found.");

        income.EventBudget.LastModifiedDate = DateTime.UtcNow;
        _context.EventBudgetIncomes.Remove(income);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> SaveEventChargesAsync(
        int meetingId, decimal costPerGirl, decimal? costPerLeader, int plannedGirlCount, int plannedAdultCount, bool leadersPay)
    {
        if (costPerGirl < 0 || (costPerLeader ?? 0) < 0)
            return (false, "Charges cannot be negative.");
        if (plannedGirlCount < 0 || plannedAdultCount < 0)
            return (false, "Attendance cannot be negative.");

        var meeting = await _context.Meetings.FindAsync(meetingId);
        if (meeting == null)
            return (false, "Meeting not found.");

        meeting.CostPerAttendee = costPerGirl;
        // Null leader charge means "leaders don't pay" for payment generation.
        meeting.CostPerLeader = leadersPay ? costPerLeader : null;

        var budget = await _context.EventBudgets.FirstOrDefaultAsync(b => b.MeetingId == meetingId);
        if (budget != null)
        {
            budget.LeadersPay = leadersPay;
            budget.PlannedGirlCount = plannedGirlCount;
            budget.PlannedAdultCount = plannedAdultCount;
            budget.LastModifiedDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    private static (decimal PerGirl, decimal PerAdult) CalculateBreakEven(
        List<EventBudgetItem> items, int girls, int adults, bool leadersPay)
        => BudgetPlanner.CalculateBreakEven(items, girls, adults, leadersPay);

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
        var costPerLeader = budget.Meeting.CostPerLeader;
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
            CostPerLeader = costPerLeader,
            ConsentedGirlCount = girlCount,
            ConsentedAdultCount = adultCount,
            ExpectedIncome = (costPerAttendee ?? 0) * girlCount + (costPerLeader ?? 0) * adultCount,
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
        => BudgetPlanner.CalculateCost(items, girlCount, adultCount);
}
