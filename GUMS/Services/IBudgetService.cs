using GUMS.Data.Entities;

namespace GUMS.Services;

public interface IBudgetService
{
    Task<EventBudget?> GetBudgetForMeetingAsync(int meetingId);
    Task<(bool Success, string ErrorMessage, EventBudget? Budget)> CreateBudgetAsync(int meetingId, string? notes);
    Task<(bool Success, string ErrorMessage)> AddBudgetItemAsync(EventBudgetItem item);
    Task<(bool Success, string ErrorMessage)> UpdateBudgetItemAsync(EventBudgetItem item);
    Task<(bool Success, string ErrorMessage)> DeleteBudgetItemAsync(int itemId);
    Task<BudgetEstimate?> GetBudgetEstimateAsync(int meetingId);
    Task<BudgetVsActual?> GetBudgetVsActualAsync(int meetingId);
}

public class BudgetEstimate
{
    public int MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
    public int GirlCount { get; set; }
    public int AdultCount { get; set; }
    public decimal HighTotal { get; set; }
    public decimal MidTotal { get; set; }
    public decimal LowTotal { get; set; }
    public decimal HighPerPerson { get; set; }
    public decimal MidPerPerson { get; set; }
    public decimal LowPerPerson { get; set; }
}

public class BudgetVsActual
{
    public int MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
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
