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
    private bool _isLoading = true;
    private bool _isSaving;
    private Dictionary<int, bool> _expandedBadges = new();
    private Dictionary<Theme, List<UmaDefinition>> _umasByTheme = new();
    private HashSet<int> _completedUmaIds = new();

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        try
        {
            _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
            await LoadUmaData();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadUmaData()
    {
        var allUmas = await BadgeService.GetAllUmasAsync();
        _umasByTheme = allUmas.GroupBy(u => u.Theme)
            .ToDictionary(g => g.Key, g => g.ToList());

        var standaloneCompletions = await ProgrammeService.GetStandaloneCompletionsAsync(MembershipNumber);
        _completedUmaIds = standaloneCompletions
            .Where(c => c.UmaDefinitionId.HasValue)
            .Select(c => c.UmaDefinitionId!.Value)
            .ToHashSet();
    }

    private async Task ToggleGoldChallenge(ChangeEventArgs e)
    {
        var complete = (bool)(e.Value ?? false);
        await ProgrammeService.SetGoldChallengeCompleteAsync(MembershipNumber, complete);
        _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
    }

    private void ToggleBadgeExpanded(int badgeDefinitionId)
    {
        if (_expandedBadges.ContainsKey(badgeDefinitionId))
            _expandedBadges.Remove(badgeDefinitionId);
        else
            _expandedBadges[badgeDefinitionId] = true;
    }

    private bool IsBadgeExpanded(int badgeDefinitionId) =>
        _expandedBadges.ContainsKey(badgeDefinitionId);

    private async Task ToggleClauseCompletion(int badgeClauseId, bool completed)
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            await ProgrammeService.SaveStandaloneCompletionAsync(
                MembershipNumber, badgeClauseId, null, completed);
            _progress = await ProgrammeService.GetGirlProgressAsync(MembershipNumber);
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
        }
        finally
        {
            _isSaving = false;
        }
    }
}
