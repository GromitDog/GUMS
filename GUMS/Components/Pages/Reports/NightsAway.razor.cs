using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Reports;

public partial class NightsAway
{
    [Inject] public required IAttendanceService AttendanceService { get; set; }
    [Inject] public required IProgrammeService ProgrammeService { get; set; }
    [Inject] public required IPersonService PersonService { get; set; }

    private List<MemberNightsAwaySummary> _girlSummaries = new();
    private List<MemberNightsAwaySummary> _leaderSummaries = new();
    private Dictionary<string, HashSet<int>> _awardedMilestones = new();
    private bool _isLoading = true;

    private DateTime? _fromDate;
    private DateTime? _toDate;

    private string? _editingMembershipNumber;
    private int _editExtraNights;

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

            _girlSummaries = summaries
                .Where(s => s.PersonType == "Girl")
                .OrderByDescending(s => s.TotalNightsAway)
                .ToList();

            _leaderSummaries = summaries
                .Where(s => s.PersonType == "Leader")
                .OrderByDescending(s => s.DisplayTotal)
                .ToList();

            // Load awarded milestones for girls
            var girlMembershipNumbers = _girlSummaries.Select(s => s.MembershipNumber).ToList();
            _awardedMilestones = girlMembershipNumbers.Any()
                ? await ProgrammeService.GetAwardedNightsAwayMilestonesAsync(girlMembershipNumbers)
                : new Dictionary<string, HashSet<int>>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading nights away data: {ex.Message}");
            _girlSummaries = new List<MemberNightsAwaySummary>();
            _leaderSummaries = new List<MemberNightsAwaySummary>();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string GetMilestoneBadgeClass(string membershipNumber, int milestone, int totalNights)
    {
        _awardedMilestones.TryGetValue(membershipNumber, out var awarded);
        if (awarded != null && awarded.Contains(milestone))
            return "bg-success"; // green = awarded
        if (totalNights >= milestone)
            return "bg-warning text-dark"; // amber = due but not awarded
        return "bg-light text-muted border"; // grey = not reached
    }

    private void StartEditExtraNights(MemberNightsAwaySummary summary)
    {
        _editingMembershipNumber = summary.MembershipNumber;
        _editExtraNights = summary.ExtraNightsAway;
    }

    private async Task SaveExtraNights(MemberNightsAwaySummary summary)
    {
        await PersonService.UpdateExtraNightsAwayAsync(summary.MembershipNumber, _editExtraNights);
        summary.ExtraNightsAway = _editExtraNights;
        _editingMembershipNumber = null;
    }

    private void CancelEditExtraNights()
    {
        _editingMembershipNumber = null;
    }

    private async Task ClearFilters()
    {
        _fromDate = null;
        _toDate = null;
        await LoadData();
    }
}
