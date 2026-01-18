using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;
public partial class AddExtraMeeting
{
    
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required IConfigurationService ConfigService { get; set; }
    [Inject] public required ITermService TermService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    
    private readonly Meeting _meeting = new();
    private readonly List<Activity> _activities = [];
    private DateTime _startTime = DateTime.Today.AddHours(10);
    private DateTime _endTime = DateTime.Today.AddHours(15);

    private bool _isSaving;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Set defaults for extra meeting
        _meeting.MeetingType = MeetingType.Extra;
        _meeting.Title = string.Empty;
        _meeting.LocationName = string.Empty;
        _meeting.CostPerAttendee = 0;

        // Try to find the next available meeting date within current term
        var currentTerm = await TermService.GetCurrentTermAsync();
        if (currentTerm != null)
        {
            var suggestedDates = await MeetingService.GetSuggestedMeetingDatesForTermAsync(currentTerm.Id);
            if (suggestedDates.Any())
            {
                _meeting.Date = suggestedDates.First();
                return;
            }
        }

        // Fallback to today if no term or no available dates
        _meeting.Date = DateTime.Today;
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
        for (var i = 0; i < _activities.Count; i++)
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