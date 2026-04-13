using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

[Authorize]
public partial class AttendanceAlerts
{

    [Inject] public required IAttendanceService AttendanceService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private List<MemberAttendanceAlert> _alerts = new();
    private Dictionary<string, string> alertNotes = new();

    private bool isLoading = true;
    private string successMessage = string.Empty;

    // Modal state
    private bool showNoteModal;
    private MemberAttendanceAlert? selectedAlert;
    private string noteText = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadAlerts();
    }

    private async Task LoadAlerts()
    {
        isLoading = true;

        try
        {
            _alerts = await AttendanceService.GetConsecutiveAbsenceAlertsAsync(5);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading alerts: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private string GetAlertNote(string membershipNumber)
    {
        return alertNotes.GetValueOrDefault(membershipNumber, string.Empty);
    }

    private void OpenNoteModal(MemberAttendanceAlert alert)
    {
        selectedAlert = alert;
        noteText = GetAlertNote(alert.MembershipNumber);
        showNoteModal = true;
    }

    private void CloseNoteModal()
    {
        showNoteModal = false;
        selectedAlert = null;
        noteText = string.Empty;
    }

    private void SaveNote()
    {
        if (selectedAlert != null)
        {
            if (string.IsNullOrWhiteSpace(noteText))
            {
                alertNotes.Remove(selectedAlert.MembershipNumber);
            }
            else
            {
                alertNotes[selectedAlert.MembershipNumber] = noteText.Trim();
            }

            successMessage = $"Note saved for {selectedAlert.MemberName}";
        }

        CloseNoteModal();
    }

    private void ClearSuccess() => successMessage = string.Empty;
}
