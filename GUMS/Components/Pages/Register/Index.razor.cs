using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GUMS.Components.Pages.Register;

public partial class Index
{
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private List<Person> _members = [];
    private string _searchTerm = string.Empty;
    private string _filterType = string.Empty;
    private string _filterStatus = "active";
    private bool _isLoading = true;

    private bool _showEmailModal;
    private string _emailFilter = "all";
    private string _copyMessage = string.Empty;
    private ElementReference _emailTextarea;

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

    private async Task OpenEmailList()
    {
        // Always pull the full active membership — ignore the current filters/search on screen
        _activeMembers = await PersonService.GetActiveAsync();
        _emailFilter = "all";
        _copyMessage = string.Empty;
        _showEmailModal = true;
    }

    private void CloseEmailList()
    {
        _showEmailModal = false;
        _copyMessage = string.Empty;
    }

    private void SetEmailFilter(string filter)
    {
        _emailFilter = filter;
        _copyMessage = string.Empty;
    }

    private List<Person> _activeMembers = [];

    private IEnumerable<string> GetEmailsFor(string filter)
    {
        var emails = new List<string>();

        if (filter is "all" or "leaders")
        {
            emails.AddRange(_activeMembers
                .Where(m => m.PersonType == PersonType.Leader && !m.IsDataRemoved && !string.IsNullOrWhiteSpace(m.Email))
                .Select(m => m.Email!.Trim()));
        }

        if (filter is "all" or "parents")
        {
            emails.AddRange(_activeMembers
                .Where(m => m.PersonType == PersonType.Girl && !m.IsDataRemoved)
                .SelectMany(m => m.EmergencyContacts)
                .Where(ec => !string.IsNullOrWhiteSpace(ec.Email))
                .Select(ec => ec.Email!.Trim()));
        }

        return emails
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase);
    }

    private int GetEmailCount(string filter) => GetEmailsFor(filter).Count();

    private string GetEmailText() => string.Join("; ", GetEmailsFor(_emailFilter));

    private async Task CopyEmails()
    {
        var text = GetEmailText();
        if (string.IsNullOrEmpty(text)) return;

        var ok = await JS.InvokeAsync<bool>("copyTextToClipboard", text);
        _copyMessage = ok
            ? $"Copied {GetEmailCount(_emailFilter)} address(es) to clipboard."
            : "Couldn't copy automatically — select the text and press Ctrl+C.";
    }
}
