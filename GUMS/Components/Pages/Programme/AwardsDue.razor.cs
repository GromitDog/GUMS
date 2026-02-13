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
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            switch (award.AwardType)
            {
                case "Badge" when award.BadgeDefinitionId.HasValue:
                    await ProgrammeService.MarkBadgeAwardedAsync(award.MembershipNumber, award.BadgeDefinitionId.Value);
                    break;
                case "ThemeAward" when award.Theme.HasValue:
                    await ProgrammeService.MarkThemeAwardedAsync(award.MembershipNumber, award.Theme.Value);
                    break;
                case "Bronze":
                case "Silver":
                case "Gold":
                    await ProgrammeService.MarkLevelAwardedAsync(award.MembershipNumber, award.AwardType);
                    break;
            }
            _awards = await ProgrammeService.GetAwardsDueAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }
}
