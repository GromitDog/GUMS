using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class GirlProgress
{
    [Inject] public required IProgrammeService ProgrammeService { get; set; }
    [Inject] public required IBadgeService BadgeService { get; set; }

    [Parameter] public string MembershipNumber { get; set; } = string.Empty;

    private GUMS.Services.GirlProgress? _progress;
    private List<AwardedBadgeSummary> _awardedBadges = new();
    private bool _isLoading = true;
    private bool _isSaving;

    private Dictionary<Theme, List<UmaDefinition>> _umasByTheme = new();
    private HashSet<int> _completedUmaIds = new();
    private HashSet<int> _standaloneCompletedUmaIds = new();

    // Modal state
    private Theme? _modalTheme;
    private HashSet<int> _expandedBadges = new();
    private GirlThemeProgress? ModalThemeProgress =>
        _modalTheme.HasValue
            ? _progress?.ThemeProgress.FirstOrDefault(t => t.Theme == _modalTheme.Value)
            : null;

    // (ThemeEnum, ThemeName, BadgeName, Completed, Required, IsInterest)
    private List<(Theme ThemeEnum, string ThemeName, string BadgeName, int Completed, int Required, bool IsInterest)> _nearCompleteBadges = new();
    // (ThemeEnum, ThemeName, MinutesCompleted, MinutesRequired)
    private List<(Theme ThemeEnum, string ThemeName, int MinutesCompleted, int MinutesRequired)> _nearCompleteUmaThemes = new();

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        try
        {
            _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
            _awardedBadges = await ProgrammeService.GetAwardedBadgesForGirlAsync(MembershipNumber);
            await LoadUmaData();
            ComputeNearCompleteItems();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task UndoAward(AwardedBadgeSummary summary)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            await ProgrammeService.UnmarkBadgeAwardedAsync(summary.MembershipNumber, summary.BadgeDefinitionId);
            _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
            _awardedBadges = await ProgrammeService.GetAwardedBadgesForGirlAsync(MembershipNumber);
            ComputeNearCompleteItems();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task LoadUmaData()
    {
        var allUmas = await BadgeService.GetAllUmasAsync();
        _umasByTheme = allUmas.GroupBy(u => u.Theme)
            .ToDictionary(g => g.Key, g => g.ToList());

        _completedUmaIds = await ProgrammeService.GetAllCompletedUmaIdsAsync(MembershipNumber);
        _standaloneCompletedUmaIds = await ProgrammeService.GetStandaloneCompletedUmaIdsAsync(MembershipNumber);
    }

    private void ComputeNearCompleteItems()
    {
        _nearCompleteBadges.Clear();
        _nearCompleteUmaThemes.Clear();

        if (_progress == null) return;

        foreach (var theme in _progress.ThemeProgress)
        {
            foreach (var badge in theme.SkillsBuilders)
            {
                if (!badge.IsComplete && badge.RequiredCompletions > 0 && badge.ClausesCompleted > 0 &&
                    (decimal)badge.ClausesCompleted / badge.RequiredCompletions >= 0.75m)
                    _nearCompleteBadges.Add((theme.Theme, theme.ThemeDisplayName, badge.BadgeName, badge.ClausesCompleted, badge.RequiredCompletions, false));
            }
            foreach (var badge in theme.InterestBadges)
            {
                if (!badge.IsComplete && badge.RequiredCompletions > 0 && badge.ClausesCompleted > 0 &&
                    (decimal)badge.ClausesCompleted / badge.RequiredCompletions >= 0.75m)
                    _nearCompleteBadges.Add((theme.Theme, theme.ThemeDisplayName, badge.BadgeName, badge.ClausesCompleted, badge.RequiredCompletions, true));
            }

            if (!theme.HasRequiredUmaMinutes && theme.UmaMinutesRequired > 0 && theme.UmaMinutesCompleted > 0 &&
                (decimal)theme.UmaMinutesCompleted / theme.UmaMinutesRequired >= 0.75m)
                _nearCompleteUmaThemes.Add((theme.Theme, theme.ThemeDisplayName, theme.UmaMinutesCompleted, theme.UmaMinutesRequired));
        }
    }

    private string NextStepsMessage
    {
        get
        {
            if (_progress == null) return string.Empty;
            var a = _progress.Awards;
            if (a.GoldEarned) return "Gold Award earned — well done!";
            if (a.ThemeAwardsEarned >= 6) return "All 6 theme awards complete — just the Gold Challenge remaining for Gold!";
            var forGold = 6 - a.ThemeAwardsEarned;
            if (a.SilverEarned) return $"Silver earned. {forGold} more theme award{(forGold != 1 ? "s" : "")} + the Gold Challenge to reach Gold.";
            if (a.BronzeEarned)
            {
                var forSilver = 4 - a.ThemeAwardsEarned;
                return $"Bronze earned. {forSilver} more theme award{(forSilver != 1 ? "s" : "")} for Silver, {forGold} for Gold.";
            }
            if (a.ThemeAwardsEarned == 0) return "No awards yet. Complete 2 theme awards for Bronze, 6 for Gold.";
            var forBronze = 2 - a.ThemeAwardsEarned;
            return $"{forBronze} more theme award{(forBronze != 1 ? "s" : "")} for Bronze, {forGold} for Gold.";
        }
    }

    private bool IsThemeInProgress(GirlThemeProgress theme) =>
        !theme.ThemeAwardEarned && (
            theme.SkillsBuilders.Any(b => b.ClausesCompleted > 0) ||
            theme.InterestBadges.Any(b => b.ClausesCompleted > 0) ||
            theme.UmaMinutesCompleted > 0);

    private string GetThemePipIcon(GirlThemeProgress theme) =>
        theme.ThemeAwardEarned ? "bi-check-circle-fill" :
        IsThemeInProgress(theme) ? "bi-circle-half" : "bi-circle";

    private string GetThemePipStyle(GirlThemeProgress theme)
    {
        var color = Badges.GetThemeColor(theme.Theme);
        if (theme.ThemeAwardEarned)
            return $"background-color: {color}; color: white; border: 2px solid {color}; border-radius: 8px;";
        if (IsThemeInProgress(theme))
            return $"background-color: white; color: {color}; border: 2px solid {color}; border-radius: 8px;";
        return "background-color: #f0f0f0; color: #888; border: 2px solid #dee2e6; border-radius: 8px;";
    }

    private void OpenModal(Theme theme)
    {
        _modalTheme = theme;
        _expandedBadges.Clear();
    }

    private void CloseModal()
    {
        _modalTheme = null;
        _expandedBadges.Clear();
    }

    private void ToggleBadgeExpanded(int badgeDefinitionId)
    {
        if (!_expandedBadges.Remove(badgeDefinitionId))
            _expandedBadges.Add(badgeDefinitionId);
    }

    private bool IsBadgeExpanded(int badgeDefinitionId) => _expandedBadges.Contains(badgeDefinitionId);

    private async Task ToggleGoldChallenge(ChangeEventArgs e)
    {
        var complete = (bool)(e.Value ?? false);
        await ProgrammeService.SetGoldChallengeCompleteAsync(MembershipNumber, complete);
        _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
        ComputeNearCompleteItems();
    }

    private async Task ToggleClauseCompletion(int badgeClauseId, bool completed)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            await ProgrammeService.SaveStandaloneCompletionAsync(
                MembershipNumber, badgeClauseId, null, completed);
            _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
            ComputeNearCompleteItems();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ToggleUmaCompletion(int umaDefinitionId, bool completed)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            await ProgrammeService.SaveStandaloneCompletionAsync(
                MembershipNumber, null, umaDefinitionId, completed);
            _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
            await LoadUmaData();
            ComputeNearCompleteItems();
        }
        finally
        {
            _isSaving = false;
        }
    }
}
