using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GUMS.Components.Pages.Meetings;

public partial class LeadersTimetable
{
    [Inject] private IMeetingService MeetingService { get; set; } = default!;
    [Inject] private ITermService TermService { get; set; } = default!;
    [Inject] private IConfigurationService ConfigurationService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public int TermId { get; set; }

    private List<Term> _allTerms = new();
    private Term? _term;
    private UnitConfiguration? _config;
    private List<LeadersTimetableRow> _rows = new();
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _allTerms = await TermService.GetAllAsync();
        _config = await ConfigurationService.GetConfigurationAsync();

        if (TermId == 0)
        {
            var current = await TermService.GetCurrentTermAsync();
            _term = current ?? _allTerms.OrderByDescending(t => t.StartDate).FirstOrDefault();
        }
        else
        {
            _term = await TermService.GetByIdAsync(TermId);
        }

        if (_term != null)
            await BuildRows();

        _isLoading = false;
    }

    private async Task OnTermChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var id) && id > 0)
        {
            _term = await TermService.GetByIdAsync(id);
            if (_term != null)
                await BuildRows();
        }
    }

    private async Task Print()
    {
        await JS.InvokeVoidAsync("window.print");
    }

    private async Task BuildRows()
    {
        if (_term == null || _config == null) return;

        _rows = new List<LeadersTimetableRow>();
        var sectionName = GetSectionName(_config.UnitType);

        // Find first expected slot >= term start
        var slotDate = _term.StartDate.Date;
        while (slotDate.DayOfWeek != _config.MeetingDayOfWeek)
            slotDate = slotDate.AddDays(1);

        var termMeetings = await MeetingService.GetByDateRangeAsync(_term.StartDate, _term.EndDate);

        // Walk week by week through the term
        var current = slotDate;
        while (current <= _term.EndDate)
        {
            var weekEnd = current.AddDays(6);
            var weekMeetings = termMeetings
                .Where(m => m.Date.Date >= current && m.Date.Date <= weekEnd)
                .OrderBy(m => m.Date)
                .ToList();

            if (weekMeetings.Any())
            {
                foreach (var m in weekMeetings)
                {
                    var isNonStandard = m.Date.DayOfWeek != _config.MeetingDayOfWeek || m.EndDate.HasValue;
                    _rows.Add(new LeadersTimetableRow(
                        m.Date, false, isNonStandard, false, false,
                        m.Title, BuildOtherDetails(m), m.EndDate, m));
                }
            }
            else
            {
                _rows.Add(new LeadersTimetableRow(current, true, false, false, false,
                    $"No {sectionName}", string.Empty, null, null));
            }

            current = current.AddDays(7);
        }

        // First regular slot after term end (grey — leaders know no meeting)
        var postTermSlot = _term.EndDate.Date.AddDays(1);
        while (postTermSlot.DayOfWeek != _config.MeetingDayOfWeek)
            postTermSlot = postTermSlot.AddDays(1);
        _rows.Add(new LeadersTimetableRow(postTermSlot, true, false, true, false,
            $"No {sectionName}", string.Empty, null, null));

        // Any meetings in the holiday gap before the next term starts
        var nextTerm = _allTerms
            .Where(t => t.StartDate > _term.EndDate)
            .OrderBy(t => t.StartDate)
            .FirstOrDefault();

        var holidayEnd = nextTerm?.StartDate.AddDays(-1) ?? _term.EndDate.AddDays(84);
        var holidayMeetings = await MeetingService.GetByDateRangeAsync(
            _term.EndDate.AddDays(1), holidayEnd);

        foreach (var m in holidayMeetings.OrderBy(m => m.Date))
        {
            var isNonStandard = m.Date.DayOfWeek != _config.MeetingDayOfWeek || m.EndDate.HasValue;
            _rows.Add(new LeadersTimetableRow(
                m.Date, false, isNonStandard, false, false,
                m.Title, BuildOtherDetails(m), m.EndDate, m));
        }

        // First meeting of next term (diary date)
        if (nextTerm != null)
        {
            var nextTermMeetings = await MeetingService.GetByDateRangeAsync(
                nextTerm.StartDate, nextTerm.EndDate);
            var firstMeeting = nextTermMeetings.OrderBy(m => m.Date).FirstOrDefault();

            if (firstMeeting != null)
            {
                var isNonStandard = firstMeeting.Date.DayOfWeek != _config.MeetingDayOfWeek || firstMeeting.EndDate.HasValue;
                _rows.Add(new LeadersTimetableRow(
                    firstMeeting.Date, false, isNonStandard, false, true,
                    firstMeeting.Title, "First meeting back", firstMeeting.EndDate, firstMeeting));
            }
            else
            {
                var firstSlot = nextTerm.StartDate.Date;
                while (firstSlot.DayOfWeek != _config.MeetingDayOfWeek)
                    firstSlot = firstSlot.AddDays(1);
                _rows.Add(new LeadersTimetableRow(
                    firstSlot, false, false, false, true,
                    nextTerm.Name, "First meeting back", null, null));
            }
        }
    }

    private string BuildOtherDetails(Meeting m)
    {
        if (_config == null) return string.Empty;

        var parts = new List<string>();

        if (m.EndDate.HasValue)
        {
            parts.Add(Timetable.FormatOvernightRange(m.Date, m.StartTime, m.EndDate.Value, m.EndTime));
        }
        else
        {
            var timeDiffers = m.StartTime != _config.DefaultMeetingStartTime
                           || m.EndTime != _config.DefaultMeetingEndTime;
            if (timeDiffers)
                parts.Add($"{Timetable.FormatTime(m.StartTime)}-{Timetable.FormatTime(m.EndTime)}");
        }

        var locationDiffers = !string.Equals(
            m.LocationName, _config.DefaultLocationName,
            StringComparison.OrdinalIgnoreCase);
        if (locationDiffers)
            parts.Add(m.LocationName);

        // Leaders' version deliberately omits ProgrammeNotes here — the full notes
        // appear in the expanded detail row below, so we don't want to duplicate.

        return string.Join(" ", parts);
    }

    private static string GetSectionName(Section s) => s switch
    {
        Section.Rainbow => "Rainbows",
        Section.Brownie => "Brownies",
        Section.Guide  => "Guides",
        Section.Ranger => "Rangers",
        _              => s.ToString()
    };
}

public record LeadersTimetableRow(
    DateTime Date,
    bool IsMissingWeek,
    bool IsNonStandardDay,
    bool IsPostTerm,
    bool IsNextTerm,
    string Title,
    string OtherDetails,
    DateTime? EndDate,
    Meeting? Meeting);
