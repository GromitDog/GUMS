using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Register;

public partial class Index
{
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private List<Person> _members = [];
    private string _searchTerm = string.Empty;
    private string _filterType = string.Empty;
    private string _filterStatus = "active";
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadMembers();
    }

    private async Task LoadMembers()
    {
        _isLoading = true;

        try
        {
            List<Person> allMembers;
            if (_filterStatus == "active")
            {
                allMembers = await PersonService.GetActiveAsync();
            }
            else if (_filterStatus == "inactive")
            {
                allMembers = await PersonService.GetInactiveAsync();
            }
            else
            {
                allMembers = await PersonService.GetAllAsync();
            }

            if (!string.IsNullOrEmpty(_filterType) && Enum.TryParse<PersonType>(_filterType, out var type))
            {
                allMembers = allMembers.Where(m => m.PersonType == type).ToList();
            }

            if (!string.IsNullOrEmpty(_searchTerm))
            {
                allMembers = allMembers.Where(m =>
                    m.MembershipNumber.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (m.FullName != null && m.FullName.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            _members = allMembers;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleSearch()
    {
        await LoadMembers();
    }
}
