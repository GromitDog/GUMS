using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class AwardsDue
{
    [Inject] public required IProgrammeService ProgrammeService { get; set; }

    private List<AwardDue> _awards = new();
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
            _awards = await ProgrammeService.GetAwardsDueAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task MarkAwarded(AwardDue award)
    {
        if (_isSaving || !award.BadgeDefinitionId.HasValue) return;
        _isSaving = true;
        try
        {
            await ProgrammeService.MarkBadgeAwardedAsync(award.MembershipNumber, award.BadgeDefinitionId.Value);
            _awards = await ProgrammeService.GetAwardsDueAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }
}
