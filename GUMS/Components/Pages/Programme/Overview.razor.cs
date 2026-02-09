using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class Overview
{
    [Inject] public required IProgrammeService ProgrammeService { get; set; }

    private UnitOverview? _overview;
    private Section? _filterSection;
    private Theme? _filterTheme;
    private string _filterType = "All"; // "All", "UMAs", "Badges"
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadOverview();
    }

    private async Task LoadOverview()
    {
        _isLoading = true;
        try
        {
            _overview = await ProgrammeService.GetUnitOverviewAsync(_filterSection);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnSectionChanged(ChangeEventArgs e)
    {
        _filterSection = Enum.TryParse<Section>(e.Value?.ToString(), out var section) ? section : null;
        await LoadOverview();
    }

    private void OnThemeFilterChanged(ChangeEventArgs e)
    {
        _filterTheme = Enum.TryParse<Theme>(e.Value?.ToString(), out var theme) ? theme : null;
    }

    private void OnTypeFilterChanged(ChangeEventArgs e)
    {
        _filterType = e.Value?.ToString() ?? "All";
    }

    private IEnumerable<Theme> GetFilteredThemes()
    {
        if (_filterTheme.HasValue)
            return new[] { _filterTheme.Value };
        return Enum.GetValues<Theme>();
    }

    private bool ShowBadges => _filterType is "All" or "Badges";
    private bool ShowUmas => _filterType is "All" or "UMAs";
}
