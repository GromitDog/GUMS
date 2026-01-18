using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class AddRegularMeeting
{
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required IConfigurationService ConfigService { get; set; }
    [Inject] public required ITermService TermService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private readonly Meeting _meeting = new();
    private readonly List<Activity> _activities = new();
    private DateTime _startTime = DateTime.Today;
    private DateTime _endTime = DateTime.Today;
    private List<DateTime> _suggestedDates = [];
    private bool _suggestedDatesUsed;
    private Term? _currentTerm;

    private bool _isSaving;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Load configuration defaults
        var config = await ConfigService.GetConfigurationAsync();
        _meeting.Date = DateTime.Today;
        _meeting.MeetingType = MeetingType.Regular;
        _meeting.Title = "Weekly Meeting";
        _meeting.LocationName = config.DefaultLocationName;
        _meeting.LocationAddress = config.DefaultLocationAddress;

        _startTime = DateTime.Today.Add(config.DefaultMeetingStartTime.ToTimeSpan());
        _endTime = DateTime.Today.Add(config.DefaultMeetingEndTime.ToTimeSpan());

        // Load current term and suggested dates
        _currentTerm = await TermService.GetCurrentTermAsync();
        if (_currentTerm != null)
        {
            _suggestedDates = await MeetingService.GetSuggestedMeetingDatesForTermAsync(_currentTerm.Id);
        }
    }

    private void UseSuggestedDate(DateTime date)
    {
        _meeting.Date = date;
        _suggestedDatesUsed = true;
    }

    private void AddActivity()
    {
        _activities.Add(new Activity
        {
            Name = string.Empty,
            RequiresConsent = false,
            SortOrder = _activities.Count
        });
    }

    private void RemoveActivity(int index)
    {
        _activities.RemoveAt(index);
        // Update sort orders
        for (int i = 0; i < _activities.Count; i++)
        {
            _activities[i].SortOrder = i;
        }
    }

    private async Task SaveMeeting()
    {
        _isSaving = true;
        ClearError();

        try
        {
            // Set times
            _meeting.StartTime = TimeOnly.FromDateTime(_startTime);
            _meeting.EndTime = TimeOnly.FromDateTime(_endTime);

            // Filter out empty activities
            _meeting.Activities = _activities
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .ToList();

            var result = await MeetingService.CreateAsync(_meeting);

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Meetings?success=created");
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }
}
