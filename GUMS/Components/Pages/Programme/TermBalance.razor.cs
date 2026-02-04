using GUMS.Data.Entities;
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
}
