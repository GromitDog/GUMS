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
    [Inject] public required IPersonService PersonService { get; set; }
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

    private bool HasEventCost =>
        meeting != null
        && (((meeting.CostPerAttendee ?? 0) > 0) || ((meeting.CostPerLeader ?? 0) > 0));

    private async Task LoadEventPaymentInfo()
    {
        if (!HasEventCost || meeting == null) return;

        attendees = await AttendanceService.GetAttendanceForMeetingAsync(MeetingId);
        var activeMembers = (await PersonService.GetActiveAsync())
            .ToDictionary(p => p.MembershipNumber);

        var girlsWithConsent = attendees
            .Where(a => a.ConsentFormReceived || a.ConsentEmailReceived)
            .Where(a => activeMembers.TryGetValue(a.MembershipNumber, out var p) && p.PersonType == PersonType.Girl)
            .ToList();
        var leadersPlanning = attendees
            .Where(a => a.PlanningToAttend)
            .Where(a => activeMembers.TryGetValue(a.MembershipNumber, out var p) && p.PersonType == PersonType.Leader)
            .ToList();

        // Only members who should actually have a payment given their cost
        var members = new List<Attendance>();
        if ((meeting.CostPerAttendee ?? 0) > 0) members.AddRange(girlsWithConsent);
        if ((meeting.CostPerLeader ?? 0) > 0) members.AddRange(leadersPlanning);

        eventPaymentsCount = 0;
        consentedWithoutPayment = 0;

        foreach (var a in members)
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
            var activeMembers = (await PersonService.GetActiveAsync())
                .ToDictionary(p => p.MembershipNumber);

            // Girls with consent pay at CostPerAttendee; leaders planning to attend pay at CostPerLeader
            var girlsWithConsent = attendees
                .Where(a => a.ConsentFormReceived || a.ConsentEmailReceived)
                .Where(a => activeMembers.TryGetValue(a.MembershipNumber, out var p) && p.PersonType == PersonType.Girl)
                .ToList();
            var leadersPlanning = attendees
                .Where(a => a.PlanningToAttend)
                .Where(a => activeMembers.TryGetValue(a.MembershipNumber, out var p) && p.PersonType == PersonType.Leader)
                .ToList();

            var members = new List<Attendance>();
            if ((meeting.CostPerAttendee ?? 0) > 0) members.AddRange(girlsWithConsent);
            if ((meeting.CostPerLeader ?? 0) > 0) members.AddRange(leadersPlanning);

            var created = 0;
            foreach (var a in members)
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
