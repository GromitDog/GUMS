using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

/// <summary>
/// One-tap route to today's register, behind the phone header's Register button. Rendered
/// statically so the redirect happens on the server before any page is drawn.
/// </summary>
public partial class TodayRegister
{
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var todaysMeetings = await MeetingService.GetMeetingsHappeningOnAsync(DateTime.Today);

        // No meeting today, or more than one: let the leader pick from the meetings list
        var target = todaysMeetings.Count == 1
            ? $"/Meetings/Attendance/{todaysMeetings[0].Id}"
            : "/Meetings";

        NavigationManager.NavigateTo(target, replace: true);
    }
}
