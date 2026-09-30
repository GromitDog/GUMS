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

    // Phones show one section at a time; desktop shows them all
    private enum RegisterSection { Register, Consent, Availability }
    private RegisterSection _activeSection = RegisterSection.Register;

    // Set by the phone save bar, which stays on the page; cleared by the next change
    private DateTime? _savedAt;

    private bool IsFutureMeeting => meeting != null && meeting.Date.Date > DateTime.Today;

    private bool HasLeaders => attendanceRecords.Any(a =>
        memberLookup.TryGetValue(a.MembershipNumber, out var person) && person.PersonType == PersonType.Leader);

    // Leader availability is for planning, so on the day it would only be mistaken for the register
    private bool OffersAvailabilitySection => IsFutureMeeting && HasLeaders;

    private bool HasSectionChoice => requiresConsent || OffersAvailabilitySection;

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

            // Before an event that needs consent the job is recording returned forms;
            // on the day, or afterwards, it's ticking off who is here
            _activeSection = requiresConsent && IsFutureMeeting
                ? RegisterSection.Consent
                : RegisterSection.Register;

            isMultiDayMeeting = meeting.EndDate.HasValue && meeting.EndDate.Value > meeting.Date;
            if (isMultiDayMeeting)
            {
                defaultNightsAway = MeetingService.CalculateNightsForMeeting(meeting.Date, meeting.EndDate);
            }

            var activeMembers = await PersonService.GetActiveAsync();
            memberLookup = activeMembers.ToDictionary(m => m.MembershipNumber);

            // Always initialize to pick up any new members since last visit
            await AttendanceService.InitializeAttendanceForMeetingAsync(MeetingId);
            var existingAttendance = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);

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
                foreach (var activity in _meetingActivities.Where(a => a.BadgeClauseId.HasValue || a.UmaDefinitionId.HasValue || a.BadgeDefinitionId.HasValue))
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

    private void ShowSection(RegisterSection section)
    {
        _activeSection = section;
    }

    // Sections other than the active one are hidden on phones only
    private string PhoneSectionClass(RegisterSection section) =>
        section == _activeSection ? string.Empty : "d-none d-md-flex";

    private bool GetCompletion(int activityId, string membershipNumber)
    {
        return _completions.GetValueOrDefault((activityId, membershipNumber), false);
    }

    private void SetCompletion(int activityId, string membershipNumber, bool completed)
    {
        _completions[(activityId, membershipNumber)] = completed;
        _savedAt = null;
    }

    private void ToggleAttendance(Attendance record, bool attended)
    {
        record.Attended = attended;
        _savedAt = null;

        if (isMultiDayMeeting)
        {
            if (attended && !record.NightsAway.HasValue)
            {
                record.NightsAway = record.IsDayCamper ? 0 : defaultNightsAway;
            }
            else if (!attended)
            {
                record.NightsAway = null;
            }
        }

        // Default completions for linked activities when marking a girl as attended
        if (memberLookup.TryGetValue(record.MembershipNumber, out var person) && person.PersonType == PersonType.Girl)
        {
            foreach (var activity in _meetingActivities.Where(a => a.BadgeClauseId.HasValue || a.UmaDefinitionId.HasValue || a.BadgeDefinitionId.HasValue))
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
        _savedAt = null;
    }

    private void TogglePlanningToAttend(Attendance record, bool planning)
    {
        record.PlanningToAttend = planning;
        _savedAt = null;
    }

    private void ToggleDayCamper(Attendance record, bool isDayCamper)
    {
        record.IsDayCamper = isDayCamper;
        _savedAt = null;
        if (record.Attended)
        {
            // Day campers go home each night; switching back restores the full stay
            record.NightsAway = isDayCamper ? 0 : defaultNightsAway;
        }
    }

    private void ToggleConsentEmail(Attendance record, bool received)
    {
        record.ConsentEmailReceived = received;
        _savedAt = null;
        if (received)
        {
            if (!record.ConsentEmailDate.HasValue)
                record.ConsentEmailDate = DateTime.Today;
            // Clearing Declined — they're on the yes path
            record.ConsentDeclined = false;
            record.ConsentDeclinedDate = null;
        }
        else
        {
            record.ConsentEmailDate = null;
        }
    }

    private void ToggleConsentForm(Attendance record, bool received)
    {
        record.ConsentFormReceived = received;
        _savedAt = null;
        if (received)
        {
            if (!record.ConsentFormDate.HasValue)
                record.ConsentFormDate = DateTime.Today;
            // Clearing Declined — form received means yes
            record.ConsentDeclined = false;
            record.ConsentDeclinedDate = null;
        }
        else
        {
            record.ConsentFormDate = null;
        }
    }

    private void ToggleConsentDeclined(Attendance record, bool declined)
    {
        record.ConsentDeclined = declined;
        _savedAt = null;
        if (declined)
        {
            if (!record.ConsentDeclinedDate.HasValue)
                record.ConsentDeclinedDate = DateTime.Today;
            // Clear yes-path fields — they're not coming
            record.ConsentEmailReceived = false;
            record.ConsentEmailDate = null;
            record.ConsentFormReceived = false;
            record.ConsentFormDate = null;
            record.IsDayCamper = false;
        }
        else
        {
            record.ConsentDeclinedDate = null;
        }
    }

    private List<Attendance> GetVisibleAttendanceRecords()
    {
        return attendanceRecords
            .Where(a => memberLookup.ContainsKey(a.MembershipNumber)
                && (!requiresConsent || a.ConsentFormReceived || memberLookup[a.MembershipNumber].PersonType == PersonType.Leader))
            .ToList();
    }

    private void MarkAllPresent()
    {
        foreach (var record in GetVisibleAttendanceRecords())
        {
            ToggleAttendance(record, true);
        }
    }

    private void MarkAllAbsent()
    {
        foreach (var record in GetVisibleAttendanceRecords())
        {
            ToggleAttendance(record, false);
        }
    }

    // Desktop saves return to the meeting page. The phone save bar stays on the register so a
    // leader can save as girls arrive and keep ticking off late arrivals.
    private Task SaveAttendance() => SaveAsync(stayOnPage: false);

    private Task SaveAndStay() => SaveAsync(stayOnPage: true);

    private async Task SaveAsync(bool stayOnPage)
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

                if (stayOnPage)
                {
                    _savedAt = DateTime.Now;
                    return;
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
