using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Programme;

public partial class Overview
{
    [Inject] public required IProgrammeService ProgrammeService { get; set; }

    private UnitOverview? _overview;
    private Section? _filterSection;
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
}
