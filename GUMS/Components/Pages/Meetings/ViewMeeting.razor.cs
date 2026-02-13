using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class ViewMeeting
{
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required IAttendanceService AttendanceService { get; set; }
    [Inject] public required IPaymentService PaymentService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Parameter]
    public int MeetingId { get; set; }

    private Meeting? meeting;
    private List<MeetingActivity> activities = new();
    private AttendanceStats? attendanceStats;
    private bool requiresConsent = false;

    private bool isLoading = true;
    private bool isDeleting = false;
    private bool isGeneratingPayments = false;
    private bool showDeleteConfirm = false;
    private bool showGeneratePaymentsConfirm = false;
    private string successMessage = string.Empty;
    private string errorMessage = string.Empty;

    // Event payment tracking
    private int eventPaymentsCount = 0;
    private int consentedWithoutPayment = 0;
    private List<Attendance> attendees = new();

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
                await LoadEventPaymentInfo();
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

    // ===== Event Payments =====

    private bool HasEventCost => meeting?.CostPerAttendee.HasValue == true && meeting.CostPerAttendee.Value > 0;

    private async Task LoadEventPaymentInfo()
    {
        if (!HasEventCost) return;

        attendees = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);
        var consentedMembers = attendees.Where(a => a.ConsentFormReceived).ToList();

        eventPaymentsCount = 0;
        consentedWithoutPayment = 0;

        foreach (var a in consentedMembers)
        {
            if (await PaymentService.HasActivityPaymentAsync(MeetingId, a.MembershipNumber))
                eventPaymentsCount++;
            else
                consentedWithoutPayment++;
        }
    }

    private void ShowGeneratePaymentsConfirm()
    {
        showGeneratePaymentsConfirm = true;
    }

    private void CancelGeneratePayments()
    {
        showGeneratePaymentsConfirm = false;
    }

    private async Task GenerateEventPayments()
    {
        if (meeting == null) return;

        isGeneratingPayments = true;
        errorMessage = string.Empty;

        try
        {
            var consentedMembers = attendees.Where(a => a.ConsentFormReceived).ToList();
            var created = 0;

            foreach (var a in consentedMembers)
            {
                var result = await PaymentService.CreateActivityPaymentAsync(MeetingId, a.MembershipNumber);
                if (result.Success) created++;
            }

            showGeneratePaymentsConfirm = false;
            successMessage = $"Generated {created} payment record{(created != 1 ? "s" : "")} for {meeting.Title}.";
            await LoadEventPaymentInfo();
        }
        catch (Exception ex)
        {
            errorMessage = $"An error occurred: {ex.Message}";
            showGeneratePaymentsConfirm = false;
        }
        finally
        {
            isGeneratingPayments = false;
        }
    }
}
