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
    private string _filterType = "All"; // "All", "UMAs", "SkillsBuilders", "InterestBadges", "FunBadges"
    private string _sortBy = "Progress"; // "Progress", "Name", "TimeInGuides"
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

    private void OnSortChanged(ChangeEventArgs e)
    {
        _sortBy = e.Value?.ToString() ?? "Progress";
    }

    private int GetAverageProgress(GirlOverviewRow girl)
    {
        if (_filterType == "FunBadges")
            return girl.CompletedFunBadges.Count;

        var themes = Enum.GetValues<Theme>();
        var total = 0;
        foreach (var theme in themes)
        {
            var summary = girl.Themes.GetValueOrDefault(theme);
            if (summary == null) continue;
            var sbPct = Math.Min(summary.SkillsBuilderPercent, 100);
            var ibPct = Math.Min(summary.InterestBadgePercent, 100);
            var umaPct = summary.UmaMinutesRequired > 0
                ? (int)Math.Min(summary.UmaMinutes * 100 / summary.UmaMinutesRequired, 100)
                : 0;
            total += _filterType switch
            {
                "UMAs" => umaPct,
                "SkillsBuilders" => sbPct,
                "InterestBadges" => ibPct,
                _ => (sbPct + ibPct + umaPct) / 3
            };
        }
        return themes.Length > 0 ? total / themes.Length : 0;
    }

    private IEnumerable<GirlOverviewRow> GetSortedGirls()
    {
        if (_overview == null) return Enumerable.Empty<GirlOverviewRow>();
        return _sortBy switch
        {
            "Name" => _overview.Girls.OrderBy(g => g.Name),
            "TimeInGuides" => _overview.Girls.OrderBy(g => g.DateJoined), // earliest joined first = longest serving
            _ => _overview.Girls.OrderByDescending(g => GetAverageProgress(g))
        };
    }

    private bool ShowSkillsBuilders => _filterType is "All" or "SkillsBuilders";
    private bool ShowInterestBadges => _filterType is "All" or "InterestBadges";
    private bool ShowUmas => _filterType is "All" or "UMAs";
    private bool ShowFunBadges => _filterType == "FunBadges";
}
