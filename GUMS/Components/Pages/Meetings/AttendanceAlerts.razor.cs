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
        
    [Inject] public required ITermService TermService { get; set; }
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    
    private Term? currentTerm;
    private List<MemberAttendanceAlert> fullTermAbsences = new();
    private List<MemberAttendanceAlert> lowAttendanceAlerts = new();
    private Dictionary<string, string> alertNotes = new();
    private int totalMeetingsInTerm = 0;
    private int termProgress = 0;

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
            currentTerm = await TermService.GetCurrentTermAsync();

            if (currentTerm != null)
            {
                // Calculate term progress
                var totalDays = (currentTerm.EndDate - currentTerm.StartDate).Days;
                var daysPassed = (DateTime.Today - currentTerm.StartDate).Days;
                termProgress = totalDays > 0 ? Math.Min(100, Math.Max(0, (int)((double)daysPassed / totalDays * 100))) : 0;

                // Get alerts
                fullTermAbsences = await AttendanceService.GetFullTermAbsencesAsync(currentTerm.Id);
                lowAttendanceAlerts = await AttendanceService.GetLowAttendanceAlertsAsync(currentTerm.Id, 25);

                // Get total meetings count
                if (fullTermAbsences.Any())
                {
                    totalMeetingsInTerm = fullTermAbsences.First().TotalMeetings;
                }
                else if (lowAttendanceAlerts.Any())
                {
                    totalMeetingsInTerm = lowAttendanceAlerts.First().TotalMeetings;
                }
            }
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