using GUMS.Data.Enums;

namespace GUMS.Components.Pages;

public partial class Home
{
    private bool _isLoading = true;

    // Member counts
    private int _leaderCount = 0;
    private int _rainbowCount = 0;
    private int _brownieCount = 0;
    private int _guideCount = 0;
    private int _rangerCount = 0;
    private int _totalCount = 0;

    // Meeting info
    private DateTime? _nextMeetingDate;
    private int _upcomingMeetingCount = 0;

    // Attendance alerts
    private string? _currentTermName;
    private int _fullTermAbsenceCount = 0;
    private int _lowAttendanceCount = 0;
    private int _alertCount = 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        _isLoading = true;
        try
        {
            // Load member counts
            var activeMembers = await PersonService.GetActiveAsync();
            _totalCount = activeMembers.Count;
            _leaderCount = activeMembers.Count(m => m.PersonType == PersonType.Leader);
            _rainbowCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Rainbow });
            _brownieCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Brownie });
            _guideCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Guide });
            _rangerCount = activeMembers.Count(m => m is { PersonType: PersonType.Girl, Section: Section.Ranger });

            // Load meeting info
            _nextMeetingDate = await MeetingService.GetNextMeetingDateAsync();
            var upcomingMeetings = await MeetingService.GetUpcomingAsync();
            _upcomingMeetingCount = upcomingMeetings.Count;

            // Load attendance alerts
            var currentTerm = await TermService.GetCurrentTermAsync();
            if (currentTerm != null)
            {
                _currentTermName = currentTerm.Name;

                var fullTermAbsences = await AttendanceService.GetFullTermAbsencesAsync(currentTerm.Id);
                _fullTermAbsenceCount = fullTermAbsences.Count;

                var lowAttendance = await AttendanceService.GetLowAttendanceAlertsAsync(currentTerm.Id, 25);
                _lowAttendanceCount = lowAttendance.Count;

                _alertCount = _fullTermAbsenceCount + _lowAttendanceCount;
            }
        }
        finally
        {
            _isLoading = false;
        }
    }
}