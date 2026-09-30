using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

/// <summary>
/// Meeting row for phones. Tapping the row opens the register (where consent forms are also
/// recorded for future events); a small info button opens the meeting plan.
/// </summary>
public partial class MobileMeetingRow
{
    [Parameter, EditorRequired] public Meeting Meeting { get; set; } = default!;
    [Parameter] public AttendanceStats? Stats { get; set; }
    [Parameter] public bool IsToday { get; set; }

    private string _statusIcon = string.Empty;
    private string _statusText = string.Empty;
    private string _statusClass = string.Empty;

    protected override void OnParametersSet()
    {
        (_statusIcon, _statusText, _statusClass) = GetStatus();
    }

    private (string Icon, string Text, string CssClass) GetStatus()
    {
        var isFuture = !IsToday && Meeting.Date.Date > DateTime.Today;
        if (isFuture)
        {
            if (Meeting.MeetingActivities.Any(a => a.RequiresConsent))
            {
                var formsIn = Stats?.ConsentFormReceived ?? 0;
                var text = formsIn == 0
                    ? "No consent forms in"
                    : $"{formsIn} consent form{(formsIn == 1 ? "" : "s")} in";
                return ("bi-file-earmark-check", text, "text-warning");
            }

            return ("bi-clock", $"{Meeting.StartTime:HH:mm}-{Meeting.EndTime:HH:mm}", "text-muted");
        }

        if (Stats is { Attended: > 0 })
        {
            return ("bi-check-circle-fill", $"{Stats.Attended} present", "text-success");
        }

        return IsToday
            ? ("bi-clipboard-check", "Take register", "mobile-meeting-cta")
            : ("bi-exclamation-circle", "Register not taken", "text-warning");
    }
}
