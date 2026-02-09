using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class TermBalance
{
    [Inject] public required IProgrammeService ProgrammeService { get; set; }
    [Inject] public required ITermService TermService { get; set; }

    private List<Term> _terms = new();
    private int _selectedTermId;
    private GUMS.Services.TermBalance? _balance;
    private bool _isLoading = true;

    private Theme? _filterTheme;
    private string _filterType = "All"; // "All", "UMAs", "Badges"

    protected override async Task OnInitializedAsync()
    {
        _terms = await TermService.GetAllAsync();

        var currentTerm = await TermService.GetCurrentTermAsync();
        if (currentTerm != null)
        {
            _selectedTermId = currentTerm.Id;
            await LoadBalance();
        }
        else if (_terms.Any())
        {
            _selectedTermId = _terms.First().Id;
            await LoadBalance();
        }

        _isLoading = false;
    }

    private async Task OnTermChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var termId))
        {
            _selectedTermId = termId;
            await LoadBalance();
        }
    }

    private void OnThemeFilterChanged(ChangeEventArgs e)
    {
        _filterTheme = Enum.TryParse<Theme>(e.Value?.ToString(), out var theme) ? theme : null;
    }

    private void OnTypeFilterChanged(ChangeEventArgs e)
    {
        _filterType = e.Value?.ToString() ?? "All";
    }

    private async Task LoadBalance()
    {
        _isLoading = true;
        try
        {
            _balance = await ProgrammeService.GetTermBalanceAsync(_selectedTermId);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private IEnumerable<Theme> GetFilteredThemes()
    {
        if (_filterTheme.HasValue)
            return new[] { _filterTheme.Value };
        return Enum.GetValues<Theme>();
    }

    private int GetFilteredMinutes(ThemeBalance? tb)
    {
        if (tb == null) return 0;
        return _filterType switch
        {
            "UMAs" => tb.UmaMinutesPlanned,
            "Badges" => tb.MinutesPlanned - tb.UmaMinutesPlanned,
            _ => tb.MinutesPlanned
        };
    }

    private int GetFilteredTotalMinutes()
    {
        if (_balance == null) return 0;

        var themes = GetFilteredThemes();
        return themes.Sum(t =>
        {
            var tb = _balance.ThemeBalances.GetValueOrDefault(t);
            return GetFilteredMinutes(tb);
        });
    }

    private int GetFilteredTotalUmaMinutes()
    {
        if (_balance == null) return 0;

        var themes = GetFilteredThemes();
        return themes.Sum(t =>
        {
            var tb = _balance.ThemeBalances.GetValueOrDefault(t);
            return tb?.UmaMinutesPlanned ?? 0;
        });
    }

    private int GetFilteredTotalBadges()
    {
        if (_balance == null) return 0;

        if (_filterType == "UMAs") return 0;

        var themes = GetFilteredThemes();
        return themes.Sum(t =>
        {
            var tb = _balance.ThemeBalances.GetValueOrDefault(t);
            return tb?.BadgesWorkedOn ?? 0;
        });
    }

    private double GetFilteredPercentage(ThemeBalance? tb)
    {
        if (tb == null) return 0;
        var totalFiltered = GetFilteredTotalMinutes();
        if (totalFiltered == 0) return 0;
        return (double)GetFilteredMinutes(tb) / totalFiltered * 100;
    }
}
