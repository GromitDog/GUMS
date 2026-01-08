using GUMS.Data.Enums;

namespace GUMS.Components.Pages;

public partial class Home 
{
    private bool _isLoading = true;
    private int _leaderCount = 0;
    private int _rainbowCount = 0;
    private int _brownieCount = 0;
    private int _guideCount = 0;
    private int _rangerCount = 0;
    private int _totalCount = 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadMembershipSummary();
    }

    private async Task LoadMembershipSummary()
    {
        _isLoading = true;
        try
        {
            var activeMembers = await PersonService.GetActiveAsync();
            _totalCount = activeMembers.Count;
            
            // Reset counts and iterate once through the collection
            _leaderCount = activeMembers.Count(m => m.PersonType == PersonType.Leader);
            _rainbowCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Rainbow });
            _brownieCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Brownie });
            _guideCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Guide });
            _rangerCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Ranger});
        }
        finally
        {
            _isLoading = false;
        }
    }
}