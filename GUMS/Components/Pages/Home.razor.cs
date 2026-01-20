using GUMS.Data.Enums;

namespace GUMS.Components.Pages;

public partial class Home
{
    private bool _isLoading = true;

    // Unit info
    private string? _unitName;

    // Member counts
    private int _leaderCount;
    private int _rainbowCount;
    private int _brownieCount;
    private int _guideCount;
    private int _rangerCount;
    private int _totalCount;

    // Meeting info
    private DateTime? _nextMeetingDate;
    private int _upcomingMeetingCount;

    // Attendance alerts
    private string? _currentTermName;
    private int _fullTermAbsenceCount;
    private int _lowAttendanceCount;
    private int _alertCount;

    // Payment stats
    private int _pendingPaymentCount;
    private int _overduePaymentCount;
    private decimal _totalOutstanding;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        _isLoading = true;
        try
        {
            // Load unit configuration
            var config = await ConfigService.GetConfigurationAsync();
            _unitName = config.UnitName;

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

            // Load payment stats
            var paymentStats = await PaymentService.GetDashboardStatsAsync();
            _pendingPaymentCount = paymentStats.PendingCount;
            _overduePaymentCount = paymentStats.OverdueCount;
            _totalOutstanding = paymentStats.TotalOutstanding;
        }
        finally
        {
            _isLoading = false;
        }
    }
}