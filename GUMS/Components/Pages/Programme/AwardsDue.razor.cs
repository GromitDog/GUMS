using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class AwardsDue
{
    [Inject] public required IProgrammeService ProgrammeService { get; set; }
    [Inject] public required IInventoryService InventoryService { get; set; }
    [Inject] public required IPatrolService PatrolService { get; set; }

    private List<AwardDue> _awards = new();
    private List<AwardDue> _projectedAwards = new();
    private List<BadgeStockItem> _stockItems = new();
    private List<StockSummaryRow> _stockSummary = new();
    private List<AwardedBadgeSummary> _recentAwards = new();
    private bool _isLoading = true;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        await LoadAwards();
    }

    private async Task LoadAwards()
    {
        _isLoading = true;
        try
        {
            var awardsTask = ProgrammeService.GetAwardsDueAsync();
            var projectedTask = ProgrammeService.GetProjectedAwardsForCurrentTermAsync();
            var stockTask = InventoryService.GetAllStockItemsAsync();
            var patrolAwardsTask = PatrolService.GetPatrolAwardsDueAsync();
            var recentTask = ProgrammeService.GetRecentAwardsAsync(30);

            await Task.WhenAll(awardsTask, projectedTask, stockTask, patrolAwardsTask, recentTask);

            _awards = awardsTask.Result.Concat(patrolAwardsTask.Result).ToList();
            _projectedAwards = projectedTask.Result;
            _stockItems = stockTask.Result;
            _recentAwards = recentTask.Result;

            BuildStockSummary();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BuildStockSummary()
    {
        var summary = new Dictionary<string, StockSummaryRow>();

        foreach (var award in _awards)
        {
            var key = AwardKey(award);
            if (!summary.ContainsKey(key))
                summary[key] = CreateRow(award);
            summary[key].CurrentlyDue++;
        }

        foreach (var award in _projectedAwards)
        {
            var key = AwardKey(award);
            if (!summary.ContainsKey(key))
                summary[key] = CreateRow(award);
            summary[key].ProjectedAdditional++;
        }

        foreach (var row in summary.Values)
        {
            var item = FindStockItem(row);
            if (item != null)
            {
                row.StockItemId = item.Id;
                row.InStock = item.CurrentQuantity;
                row.IsTracked = true;
            }
        }

        _stockSummary = summary.Values
            .OrderByDescending(r => r.IsTracked && r.Shortfall > 0)
            .ThenBy(r => r.AwardName)
            .ToList();
    }

    private static string AwardKey(AwardDue a) =>
        $"{a.AwardType}|{a.BadgeDefinitionId}|{(int?)a.Theme}|{a.Milestone}";

    private static StockSummaryRow CreateRow(AwardDue award) => new()
    {
        AwardName = award.AwardName,
        AwardType = award.AwardType,
        BadgeDefinitionId = award.BadgeDefinitionId,
        NightsAwayMilestone = award.Milestone,
        AwardTheme = award.Theme
    };

    private BadgeStockItem? FindStockItem(StockSummaryRow row) => row.AwardType switch
    {
        "Badge" when row.BadgeDefinitionId.HasValue =>
            _stockItems.FirstOrDefault(s => s.BadgeDefinitionId == row.BadgeDefinitionId),
        "FunBadge" when row.BadgeDefinitionId.HasValue =>
            _stockItems.FirstOrDefault(s => s.BadgeDefinitionId == row.BadgeDefinitionId),
        "PatrolBadge" when row.BadgeDefinitionId.HasValue =>
            _stockItems.FirstOrDefault(s => s.BadgeDefinitionId == row.BadgeDefinitionId),
        "ThemeAward" when row.AwardTheme.HasValue =>
            _stockItems.FirstOrDefault(s => s.StockType == BadgeStockType.ThemeAward && s.AwardTheme == row.AwardTheme),
        "Bronze" =>
            _stockItems.FirstOrDefault(s => s.StockType == BadgeStockType.ThemeAward && s.ThemeAwardLevel == ThemeAwardLevel.Bronze),
        "Silver" =>
            _stockItems.FirstOrDefault(s => s.StockType == BadgeStockType.ThemeAward && s.ThemeAwardLevel == ThemeAwardLevel.Silver),
        "Gold" =>
            _stockItems.FirstOrDefault(s => s.StockType == BadgeStockType.ThemeAward && s.ThemeAwardLevel == ThemeAwardLevel.Gold),
        "NightsAway" when row.NightsAwayMilestone.HasValue =>
            _stockItems.FirstOrDefault(s => s.NightsAwayTier == row.NightsAwayMilestone),
        _ => null
    };

    private StockSummaryRow? GetStockForAward(AwardDue award) =>
        _stockSummary.FirstOrDefault(r =>
            r.AwardType == award.AwardType &&
            r.BadgeDefinitionId == award.BadgeDefinitionId &&
            r.NightsAwayMilestone == award.Milestone &&
            r.AwardTheme == award.Theme);

    private async Task MarkAwarded(AwardDue award)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            switch (award.AwardType)
            {
                case "Badge" when award.BadgeDefinitionId.HasValue:
                case "FunBadge" when award.BadgeDefinitionId.HasValue:
                case "PatrolBadge" when award.BadgeDefinitionId.HasValue:
                    await ProgrammeService.MarkBadgeAwardedAsync(award.MembershipNumber, award.BadgeDefinitionId.Value);
                    break;
                case "ThemeAward" when award.Theme.HasValue:
                    await ProgrammeService.MarkThemeAwardedAsync(award.MembershipNumber, award.Theme.Value);
                    break;
                case "NightsAway" when award.Milestone.HasValue:
                    await ProgrammeService.MarkNightsAwayBadgeAwardedAsync(award.MembershipNumber, award.Milestone.Value);
                    break;
                case "Bronze":
                case "Silver":
                case "Gold":
                    await ProgrammeService.MarkLevelAwardedAsync(award.MembershipNumber, award.AwardType);
                    break;
            }
            await LoadAwards();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task UndoAward(AwardedBadgeSummary summary)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            await ProgrammeService.UnmarkBadgeAwardedAsync(summary.MembershipNumber, summary.BadgeDefinitionId);
            await LoadAwards();
        }
        finally
        {
            _isSaving = false;
        }
    }
}

public class StockSummaryRow
{
    public string AwardName { get; set; } = string.Empty;
    public string AwardType { get; set; } = string.Empty;
    public int? BadgeDefinitionId { get; set; }
    public int? NightsAwayMilestone { get; set; }
    public Theme? AwardTheme { get; set; }
    public int CurrentlyDue { get; set; }
    public int ProjectedAdditional { get; set; }
    public int TotalNeeded => CurrentlyDue + ProjectedAdditional;
    public int? StockItemId { get; set; }
    public int InStock { get; set; }
    public bool IsTracked { get; set; }
    public int Shortfall => IsTracked ? Math.Max(0, TotalNeeded - InStock) : 0;
}