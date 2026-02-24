using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

public interface IUnitBudgetService
{
    Task<UnitBudget?> GetBudgetForYearAsync(DateTime yearEnd);
    Task<UnitBudget> GetOrCreateBudgetForYearAsync(DateTime yearEnd);
    Task<(bool Success, string ErrorMessage)> AddItemAsync(UnitBudgetItem item);
    Task<(bool Success, string ErrorMessage)> UpdateItemAsync(UnitBudgetItem item);
    Task<(bool Success, string ErrorMessage)> DeleteItemAsync(int itemId);
    Task<UnitBudgetSummary> GetBudgetSummaryAsync(DateTime yearEnd);
}

public class UnitBudgetSummary
{
    public DateTime YearStart { get; set; }
    public DateTime YearEnd { get; set; }
    public int ActiveGirlCount { get; set; }
    public int ActivePersonCount { get; set; }
    public int TermCount { get; set; }
    public int MeetingWeeks { get; set; }
    public List<UnitBudgetLineResult> Lines { get; set; } = new();
    public decimal TotalAnnualCost { get; set; }
    public decimal TotalPerGirlAnnual { get; set; }
    public decimal SuggestedTermlySubs { get; set; }
    public List<UnitBudgetVsActualLine> ActualComparison { get; set; } = new();
}

public class UnitBudgetLineResult
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public BudgetFrequency Frequency { get; set; }
    public BudgetAllocation Allocation { get; set; }
    public decimal UnitAmount { get; set; }
    public decimal AnnualTotal { get; set; }
    public decimal PerGirlContribution { get; set; }
    public string? ExpenseAccountName { get; set; }
}

public class UnitBudgetVsActualLine
{
    public string Description { get; set; } = string.Empty;
    public int ExpenseAccountId { get; set; }
    public decimal Budgeted { get; set; }
    public decimal Actual { get; set; }
    public decimal Variance => Budgeted - Actual;
}
