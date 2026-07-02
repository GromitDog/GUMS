using GUMS.Data.Entities;

namespace GUMS.Services;

public interface IBudgetService
{
    Task<EventBudget?> GetBudgetForMeetingAsync(int meetingId);
    Task<(bool Success, string ErrorMessage, EventBudget? Budget)> CreateBudgetAsync(int meetingId, string? notes);
    Task<(bool Success, string ErrorMessage)> AddBudgetItemAsync(EventBudgetItem item);
    Task<(bool Success, string ErrorMessage)> UpdateBudgetItemAsync(EventBudgetItem item);
    Task<(bool Success, string ErrorMessage)> DeleteBudgetItemAsync(int itemId);
    Task<BudgetEstimate?> GetBudgetEstimateAsync(int meetingId, bool? leadersPayOverride = null, int? adultCountOverride = null);

    /// <summary>
    /// Persists the "what-if" planning options (whether leaders pay, planned adult count) on the budget.
    /// Pass null for either value to reset it to the derived default.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateBudgetPlanningAsync(int meetingId, bool? leadersPay, int? plannedAdultCount);

    /// <summary>Adds a grant/fundraising income line to the budget.</summary>
    Task<(bool Success, string ErrorMessage)> AddBudgetIncomeAsync(EventBudgetIncome income);

    /// <summary>Removes a grant/fundraising income line.</summary>
    Task<(bool Success, string ErrorMessage)> DeleteBudgetIncomeAsync(int incomeId);

    /// <summary>
    /// Writes the chosen attendee charges back to the meeting (CostPerAttendee / CostPerLeader) and
    /// remembers the planned turnout on the budget. A null leader charge means leaders don't pay.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> SaveEventChargesAsync(
        int meetingId, decimal costPerGirl, decimal? costPerLeader, int plannedGirlCount, int plannedAdultCount, bool leadersPay);

    Task<BudgetVsActual?> GetBudgetVsActualAsync(int meetingId);
}

public class BudgetEstimate
{
    public int MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;

    /// <summary>Total active girls on the roll (the "full turnout" figure).</summary>
    public int GirlCount { get; set; }

    /// <summary>Adults used for the projection (planned adult count, or active leaders by default).</summary>
    public int AdultCount { get; set; }

    /// <summary>Current active leader count — the default/reset value for <see cref="AdultCount"/>.</summary>
    public int ActiveLeaderCount { get; set; }

    /// <summary>Effective "leaders pay towards this event" flag applied to these figures.</summary>
    public bool LeadersPay { get; set; }

    /// <summary>The charge currently set on the meeting for girls (drives projected income).</summary>
    public decimal? CostPerAttendee { get; set; }

    /// <summary>The charge currently set on the meeting for leaders (drives projected income when they pay).</summary>
    public decimal? CostPerLeader { get; set; }

    /// <summary>True when the meeting has a charge that will generate projected income.</summary>
    public bool HasCharge => (CostPerAttendee ?? 0) > 0 || (LeadersPay && (CostPerLeader ?? 0) > 0);

    public List<BudgetScenario> Scenarios { get; set; } = new();
}

public class BudgetScenario
{
    public string Label { get; set; } = string.Empty;
    public int Girls { get; set; }
    public int Adults { get; set; }
    public decimal TotalCost { get; set; }

    /// <summary>Break-even charge per girl. When leaders don't pay this covers the adults' share too.</summary>
    public decimal CostPerGirl { get; set; }

    /// <summary>Break-even charge per adult. Zero when leaders don't pay.</summary>
    public decimal CostPerAdult { get; set; }

    /// <summary>Projected income at the charges currently set on the meeting.</summary>
    public decimal ProjectedIncome { get; set; }

    /// <summary>Projected income minus total cost. Negative means a shortfall.</summary>
    public decimal Surplus => ProjectedIncome - TotalCost;
}

public class BudgetVsActual
{
    public int MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;

    // Income
    public decimal? CostPerAttendee { get; set; }
    public decimal? CostPerLeader { get; set; }
    public int ConsentedGirlCount { get; set; }
    public int ConsentedAdultCount { get; set; }
    public int TotalConsented => ConsentedGirlCount + ConsentedAdultCount;
    public decimal ExpectedIncome { get; set; }
    public int PaidCount { get; set; }
    public decimal ActualIncome { get; set; }
    public decimal IncomeVariance => ActualIncome - ExpectedIncome;
    public bool HasIncome => (CostPerAttendee.HasValue && CostPerAttendee > 0)
        || (CostPerLeader.HasValue && CostPerLeader > 0)
        || PaidCount > 0;

    // Costs
    public decimal TotalBudgeted { get; set; }
    public decimal TotalActual { get; set; }
    public decimal TotalVariance => TotalBudgeted - TotalActual;
    public List<BudgetVsActualLine> Lines { get; set; } = new();
}

public class BudgetVsActualLine
{
    public string Category { get; set; } = string.Empty;
    public int? ExpenseAccountId { get; set; }
    public decimal Budgeted { get; set; }
    public decimal Actual { get; set; }
    public decimal Variance => Budgeted - Actual;
}
