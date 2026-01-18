using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class Index
{
    [Inject] private IMeetingService MeetingService { get; set; } = default!;
    [Inject] private IAttendanceService AttendanceService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Meeting> upcomingMeetings = new();
    private List<Meeting> pastMeetings = new();
    private DateTime? nextMeetingDate;
    private Dictionary<int, AttendanceStats> attendanceStatsCache = new();

    private bool isLoading = true;
    private bool isDeleting;
    private bool showPastMeetings;
    private bool showDeleteConfirm;
    private Meeting? meetingToDelete;
    private string successMessage = string.Empty;
    private string errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Check for success message from navigation state
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=created"))
        {
            successMessage = "Meeting created successfully!";
        }
        else if (uri.Query.Contains("success=updated"))
        {
            successMessage = "Meeting updated successfully!";
        }
        else if (uri.Query.Contains("success=deleted"))
        {
            successMessage = "Meeting deleted successfully!";
        }

        await LoadMeetings();
    }

    private async Task LoadMeetings()
    {
        isLoading = true;

        try
        {
            upcomingMeetings = await MeetingService.GetUpcomingAsync();
            pastMeetings = await MeetingService.GetPastAsync();
            nextMeetingDate = await MeetingService.GetNextMeetingDateAsync();

            // Load attendance stats for recent past meetings
            foreach (var meeting in pastMeetings.Take(20))
            {
                var stats = await AttendanceService.GetMeetingAttendanceStatsAsync(meeting.Id);
                attendanceStatsCache[meeting.Id] = stats;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading meetings: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void TogglePastMeetings()
    {
        showPastMeetings = !showPastMeetings;
    }

    private void ClearSuccess()
    {
        successMessage = string.Empty;
    }

    private void ClearError()
    {
        errorMessage = string.Empty;
    }

    private void ShowDeleteConfirm(Meeting meeting)
    {
        meetingToDelete = meeting;
        showDeleteConfirm = true;
    }

    private void CancelDelete()
    {
        meetingToDelete = null;
        showDeleteConfirm = false;
    }

    private async Task DeleteMeeting()
    {
        if (meetingToDelete == null) return;

        isDeleting = true;
        errorMessage = string.Empty;

        try
        {
            var result = await MeetingService.DeleteAsync(meetingToDelete.Id);

            if (result.Success)
            {
                successMessage = $"Meeting '{meetingToDelete.Title}' deleted successfully!";
                showDeleteConfirm = false;
                meetingToDelete = null;
                await LoadMeetings();
            }
            else
            {
                errorMessage = result.ErrorMessage;
                showDeleteConfirm = false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"An error occurred: {ex.Message}";
            showDeleteConfirm = false;
        }
        finally
        {
            isDeleting = false;
        }
    }
}
