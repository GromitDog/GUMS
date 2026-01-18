using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class ViewMeeting
{
    [Inject] public required IMeetingService MeetingService { get; set; } 
    [Inject] public required IAttendanceService AttendanceService { get; set; } 
    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Parameter]
    public int MeetingId { get; set; }

    private Meeting? meeting;
    private List<Activity> activities = new();
    private AttendanceStats? attendanceStats;
    private bool requiresConsent = false;

    private bool isLoading = true;
    private bool isDeleting = false;
    private bool showDeleteConfirm = false;
    private string successMessage = string.Empty;
    private string errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Check for success message from navigation state
        var uri = new Uri(NavigationManager.Uri);
        if (uri.Query.Contains("success=updated"))
        {
            successMessage = "Meeting updated successfully!";
        }
        else if (uri.Query.Contains("success=attendance"))
        {
            successMessage = "Attendance saved successfully!";
        }

        await LoadMeeting();
    }

    private async Task LoadMeeting()
    {
        isLoading = true;

        try
        {
            meeting = await MeetingService.GetByIdAsync(MeetingId);
            if (meeting != null)
            {
                activities = await MeetingService.GetActivitiesForMeetingAsync(MeetingId);
                attendanceStats = await AttendanceService.GetMeetingAttendanceStatsAsync(MeetingId);
                requiresConsent = await AttendanceService.MeetingRequiresConsentAsync(MeetingId);
            }
        }
        catch (Exception ex)
        {
            // In production, log this error
            Console.WriteLine($"Error loading meeting: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ClearSuccess()
    {
        successMessage = string.Empty;
    }

    private void ClearError()
    {
        errorMessage = string.Empty;
    }

    private void ShowDeleteConfirm()
    {
        showDeleteConfirm = true;
    }

    private void CancelDelete()
    {
        showDeleteConfirm = false;
    }

    private async Task DeleteMeeting()
    {
        if (meeting == null) return;

        isDeleting = true;
        errorMessage = string.Empty;

        try
        {
            var result = await MeetingService.DeleteAsync(MeetingId);

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Meetings?success=deleted");
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
