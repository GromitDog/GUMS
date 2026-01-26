using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Reports;

public partial class NightsAway
{
    [Inject] public required IAttendanceService AttendanceService { get; set; }

    private List<MemberNightsAwaySummary> _summaries = new();
    private bool _isLoading = true;

    private DateTime? _fromDate;
    private DateTime? _toDate;
    private string _personTypeFilter = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;

        try
        {
            var summaries = await AttendanceService.GetNightsAwaySummaryAsync(_fromDate, _toDate);

            // Apply person type filter
            if (!string.IsNullOrEmpty(_personTypeFilter))
            {
                summaries = summaries.Where(s => s.PersonType == _personTypeFilter).ToList();
            }

            _summaries = summaries;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading nights away data: {ex.Message}");
            _summaries = new List<MemberNightsAwaySummary>();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ClearFilters()
    {
        _fromDate = null;
        _toDate = null;
        _personTypeFilter = string.Empty;
        await LoadData();
    }
}
