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
    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Parameter]
    public int MeetingId { get; set; }

    private Meeting? meeting;
    private List<Attendance> attendanceRecords = new();
    private Dictionary<string, Person> memberLookup = new();
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
            // Load meeting
            meeting = await MeetingService.GetByIdAsync(MeetingId);
            if (meeting == null)
            {
                return;
            }

            // Check if meeting requires consent
            requiresConsent = await AttendanceService.MeetingRequiresConsentAsync(MeetingId);

            // Check if this is a multi-day meeting
            isMultiDayMeeting = meeting.EndDate.HasValue && meeting.EndDate.Value > meeting.Date;
            if (isMultiDayMeeting)
            {
                defaultNightsAway = MeetingService.CalculateNightsForMeeting(meeting.Date, meeting.EndDate);
            }

            // Load all active members
            var activeMembers = await PersonService.GetActiveAsync();
            memberLookup = activeMembers.ToDictionary(m => m.MembershipNumber);

            // Check if attendance has been initialized for this meeting
            var existingAttendance = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);

            if (!existingAttendance.Any())
            {
                // Initialize attendance records for all active members
                await AttendanceService.InitializeAttendanceForMeetingAsync(MeetingId);
                existingAttendance = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);
            }

            // Build attendance records list with member info
            attendanceRecords = existingAttendance
                .Where(a => memberLookup.ContainsKey(a.MembershipNumber))
                .ToList();
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

    private void ToggleAttendance(Attendance record, bool attended)
    {
        record.Attended = attended;

        // Auto-set NightsAway for multi-day meetings
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
            record.Attended = true;
            if (isMultiDayMeeting && !record.NightsAway.HasValue)
            {
                record.NightsAway = defaultNightsAway;
            }
        }
    }

    private void MarkAllAbsent()
    {
        foreach (var record in attendanceRecords)
        {
            record.Attended = false;
            if (isMultiDayMeeting)
            {
                record.NightsAway = null;
            }
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
                // Navigate back to meeting view with success message
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
