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
    private GUMS.Services.TermBalance? _combinedPreviousBalance;
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
            await LoadCombinedPreviousBalance();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadCombinedPreviousBalance()
    {
        // _terms is ordered most recent first, so previous terms are at higher indices
        var selectedIndex = _terms.FindIndex(t => t.Id == _selectedTermId);
        if (selectedIndex < 0)
        {
            _combinedPreviousBalance = null;
            return;
        }

        var previousTerms = _terms.Skip(selectedIndex + 1).Take(3).ToList();
        if (!previousTerms.Any())
        {
            _combinedPreviousBalance = null;
            return;
        }

        var combined = new GUMS.Services.TermBalance
        {
            TermName = previousTerms.Count == 1
                ? previousTerms[0].Name
                : $"{previousTerms.Count} Terms ({previousTerms.Last().Name} - {previousTerms.First().Name})"
        };

        foreach (var theme in Enum.GetValues<Theme>())
        {
            combined.ThemeBalances[theme] = new ThemeBalance { Theme = theme };
        }

        foreach (var term in previousTerms)
        {
            var balance = await ProgrammeService.GetTermBalanceAsync(term.Id);
            combined.TotalMinutesPlanned += balance.TotalMinutesPlanned;
            combined.TotalUmaMinutesPlanned += balance.TotalUmaMinutesPlanned;
            combined.TotalBadgesWorkedOn += balance.TotalBadgesWorkedOn;
            combined.NightsAwayOffered += balance.NightsAwayOffered;

            foreach (var (theme, tb) in balance.ThemeBalances)
            {
                var ctb = combined.ThemeBalances[theme];
                ctb.MinutesPlanned += tb.MinutesPlanned;
                ctb.UmaMinutesPlanned += tb.UmaMinutesPlanned;
                ctb.BadgesWorkedOn += tb.BadgesWorkedOn;
            }
        }

        // Recalculate percentages
        if (combined.TotalMinutesPlanned > 0)
        {
            foreach (var tb in combined.ThemeBalances.Values)
            {
                tb.PercentageOfTotal = (double)tb.MinutesPlanned / combined.TotalMinutesPlanned * 100;
            }
        }

        _combinedPreviousBalance = combined;
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

    private int GetFilteredTotalMinutes(GUMS.Services.TermBalance? balance = null)
    {
        balance ??= _balance;
        if (balance == null) return 0;

        var themes = GetFilteredThemes();
        return themes.Sum(t =>
        {
            var tb = balance.ThemeBalances.GetValueOrDefault(t);
            return GetFilteredMinutes(tb);
        });
    }

    private int GetFilteredTotalUmaMinutes(GUMS.Services.TermBalance? balance = null)
    {
        balance ??= _balance;
        if (balance == null) return 0;

        var themes = GetFilteredThemes();
        return themes.Sum(t =>
        {
            var tb = balance.ThemeBalances.GetValueOrDefault(t);
            return tb?.UmaMinutesPlanned ?? 0;
        });
    }

    private int GetFilteredTotalBadges(GUMS.Services.TermBalance? balance = null)
    {
        balance ??= _balance;
        if (balance == null) return 0;

        if (_filterType == "UMAs") return 0;

        var themes = GetFilteredThemes();
        return themes.Sum(t =>
        {
            var tb = balance.ThemeBalances.GetValueOrDefault(t);
            return tb?.BadgesWorkedOn ?? 0;
        });
    }

    private double GetFilteredPercentage(ThemeBalance? tb, GUMS.Services.TermBalance? balance = null)
    {
        balance ??= _balance;
        var totalFiltered = GetFilteredTotalMinutes(balance);
        if (totalFiltered == 0) return 0;
        return (double)GetFilteredMinutes(tb) / totalFiltered * 100;
    }
}
