using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class RecordAttendance
{
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required IAttendanceService AttendanceService { get; set; }
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required IProgrammeService ProgrammeService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Parameter]
    public int MeetingId { get; set; }

    private Meeting? meeting;
    private List<Attendance> attendanceRecords = new();
    private Dictionary<string, Person> memberLookup = new();
    private List<MeetingActivity> _meetingActivities = new();
    private Dictionary<(int ActivityId, string MembershipNumber), bool> _completions = new();
    private bool requiresConsent = false;
    private bool isMultiDayMeeting = false;
    private int defaultNightsAway = 0;

    private bool isLoading = true;
    private bool isSaving = false;
    private string errorMessage = string.Empty;
    private string successMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        isLoading = true;
        errorMessage = string.Empty;

        try
        {
            meeting = await MeetingService.GetByIdAsync(MeetingId);
            if (meeting == null) return;

            requiresConsent = await AttendanceService.MeetingRequiresConsentAsync(MeetingId);

            isMultiDayMeeting = meeting.EndDate.HasValue && meeting.EndDate.Value > meeting.Date;
            if (isMultiDayMeeting)
            {
                defaultNightsAway = MeetingService.CalculateNightsForMeeting(meeting.Date, meeting.EndDate);
            }

            var activeMembers = await PersonService.GetActiveAsync();
            memberLookup = activeMembers.ToDictionary(m => m.MembershipNumber);

            var existingAttendance = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);

            if (!existingAttendance.Any())
            {
                await AttendanceService.InitializeAttendanceForMeetingAsync(MeetingId);
                existingAttendance = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);
            }

            attendanceRecords = existingAttendance
                .Where(a => memberLookup.ContainsKey(a.MembershipNumber))
                .ToList();

            // Load meeting activities and existing completions
            _meetingActivities = await MeetingService.GetActivitiesForMeetingAsync(MeetingId);
            var existingCompletions = await ProgrammeService.GetCompletionsForMeetingAsync(MeetingId);

            _completions.Clear();
            foreach (var c in existingCompletions)
            {
                _completions[(c.MeetingActivityId, c.MembershipNumber)] = c.Completed;
            }

            // Default: all attendees completed all linked activities (if no existing completions)
            if (!existingCompletions.Any())
            {
                foreach (var activity in _meetingActivities.Where(a => a.BadgeClauseId.HasValue || a.UmaDefinitionId.HasValue))
                {
                    foreach (var record in attendanceRecords.Where(a => a.Attended && memberLookup.ContainsKey(a.MembershipNumber) && memberLookup[a.MembershipNumber].PersonType == PersonType.Girl))
                    {
                        _completions[(activity.Id, record.MembershipNumber)] = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private bool GetCompletion(int activityId, string membershipNumber)
    {
        return _completions.GetValueOrDefault((activityId, membershipNumber), false);
    }

    private void SetCompletion(int activityId, string membershipNumber, bool completed)
    {
        _completions[(activityId, membershipNumber)] = completed;
    }

    private void ToggleAttendance(Attendance record, bool attended)
    {
        record.Attended = attended;

        if (isMultiDayMeeting)
        {
            if (attended && !record.NightsAway.HasValue)
            {
                record.NightsAway = defaultNightsAway;
            }
            else if (!attended)
            {
                record.NightsAway = null;
            }
        }

        // Default completions for linked activities when marking a girl as attended
        if (memberLookup.TryGetValue(record.MembershipNumber, out var person) && person.PersonType == PersonType.Girl)
        {
            foreach (var activity in _meetingActivities.Where(a => a.BadgeClauseId.HasValue || a.UmaDefinitionId.HasValue))
            {
                var key = (activity.Id, record.MembershipNumber);
                if (attended)
                {
                    if (!_completions.ContainsKey(key))
                        _completions[key] = true;
                }
                else
                {
                    _completions.Remove(key);
                }
            }
        }
    }

    private void UpdateNightsAway(Attendance record, int? nights)
    {
        record.NightsAway = nights;
    }

    private void ToggleConsentEmail(Attendance record, bool received)
    {
        record.ConsentEmailReceived = received;
        if (received && !record.ConsentEmailDate.HasValue)
        {
            record.ConsentEmailDate = DateTime.Today;
        }
        else if (!received)
        {
            record.ConsentEmailDate = null;
        }
    }

    private void ToggleConsentForm(Attendance record, bool received)
    {
        record.ConsentFormReceived = received;
        if (received && !record.ConsentFormDate.HasValue)
        {
            record.ConsentFormDate = DateTime.Today;
        }
        else if (!received)
        {
            record.ConsentFormDate = null;
        }
    }

    private void MarkAllPresent()
    {
        foreach (var record in attendanceRecords)
        {
            ToggleAttendance(record, true);
        }
    }

    private void MarkAllAbsent()
    {
        foreach (var record in attendanceRecords)
        {
            ToggleAttendance(record, false);
        }
    }

    private async Task SaveAttendance()
    {
        isSaving = true;
        errorMessage = string.Empty;
        successMessage = string.Empty;

        try
        {
            var result = await AttendanceService.SaveBulkAttendanceAsync(MeetingId, attendanceRecords);

            if (result.Success)
            {
                // Save activity completions
                var completionRecords = _completions
                    .Select(kvp => new CompletionRecord
                    {
                        MeetingActivityId = kvp.Key.ActivityId,
                        MembershipNumber = kvp.Key.MembershipNumber,
                        Completed = kvp.Value
                    })
                    .ToList();

                if (completionRecords.Any())
                {
                    await ProgrammeService.SaveCompletionsAsync(MeetingId, completionRecords);
                }

                NavigationManager.NavigateTo($"/Meetings/View/{MeetingId}?success=attendance");
                return;
            }
            else
            {
                errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error saving attendance: {ex.Message}";
        }
        finally
        {
            isSaving = false;
        }
    }

    private void ClearError() => errorMessage = string.Empty;
    private void ClearSuccess() => successMessage = string.Empty;
}
