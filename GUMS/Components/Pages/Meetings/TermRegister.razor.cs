using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class TermRegister
{
    [Inject] private ITermService TermService { get; set; } = default!;
    [Inject] private IMeetingService MeetingService { get; set; } = default!;
    [Inject] private IAttendanceService AttendanceService { get; set; } = default!;
    [Inject] private IPersonService PersonService { get; set; } = default!;

    private List<Term> _terms = new();
    private int? _selectedTermId;
    private Term? _selectedTerm;

    private List<Person> _girls = new();
    private List<Meeting> _meetings = new();

    // membershipNumber → meetingId → attended (true/false/null if no record)
    private Dictionary<string, Dictionary<int, bool?>> _matrix = new();

    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _terms = await TermService.GetAllAsync();
        _girls = (await PersonService.GetByTypeAsync(PersonType.Girl, activeOnly: true))
            .OrderBy(g => g.FullName)
            .ToList();

        var currentTerm = await TermService.GetCurrentTermAsync();
        if (currentTerm != null)
        {
            _selectedTermId = currentTerm.Id;
        }
        else if (_terms.Any())
        {
            _selectedTermId = _terms.First().Id;
        }

        if (_selectedTermId.HasValue)
        {
            await LoadTermData();
        }

        _isLoading = false;
    }

    private async Task OnTermChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var termId))
        {
            _selectedTermId = termId;
            await LoadTermData();
        }
    }

    private async Task LoadTermData()
    {
        if (!_selectedTermId.HasValue) return;

        _selectedTerm = _terms.FirstOrDefault(t => t.Id == _selectedTermId.Value);
        if (_selectedTerm == null) return;

        _meetings = await MeetingService.GetByDateRangeAsync(_selectedTerm.StartDate, _selectedTerm.EndDate);

        // Build the attendance matrix
        _matrix.Clear();

        // Initialize all girls with empty dictionaries
        foreach (var girl in _girls)
        {
            _matrix[girl.MembershipNumber] = new Dictionary<int, bool?>();
            foreach (var meeting in _meetings)
            {
                _matrix[girl.MembershipNumber][meeting.Id] = null;
            }
        }

        // Load attendance for each meeting
        foreach (var meeting in _meetings)
        {
            var attendance = await AttendanceService.GetAttendanceForMeetingAsync(meeting.Id);
            foreach (var record in attendance)
            {
                if (_matrix.ContainsKey(record.MembershipNumber))
                {
                    _matrix[record.MembershipNumber][meeting.Id] = record.Attended;
                }
            }
        }
    }

    private bool IsFutureMeeting(Meeting meeting) => meeting.Date > DateTime.Today;

    private bool HasAttendanceData(Meeting meeting)
    {
        if (IsFutureMeeting(meeting)) return false;
        // Check if any girl has a non-null record for this meeting
        return _matrix.Values.Any(m => m.ContainsKey(meeting.Id) && m[meeting.Id].HasValue);
    }

    private (int attended, int totalPast) GetGirlSummary(string membershipNumber)
    {
        if (!_matrix.ContainsKey(membershipNumber)) return (0, 0);

        var pastMeetings = _meetings.Where(m => !IsFutureMeeting(m) && HasAttendanceData(m)).ToList();
        var attended = pastMeetings.Count(m =>
            _matrix[membershipNumber].ContainsKey(m.Id) && _matrix[membershipNumber][m.Id] == true);

        return (attended, pastMeetings.Count);
    }
}
