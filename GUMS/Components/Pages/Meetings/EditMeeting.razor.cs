using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class EditMeeting
{
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Parameter]
    public int MeetingId { get; set; }

    private Meeting? _meeting;
    private List<Activity> _activities = [];
    private DateTime _startTime;
    private DateTime _endTime;

    private bool _isLoading = true;
    private bool _isSaving ;
    private bool _showDeleteConfirm;
    private bool _isDeleting;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadMeeting();
    }

    private async Task LoadMeeting()
    {
        _isLoading = true;

        try
        {
            _meeting = await MeetingService.GetByIdAsync(MeetingId);
            if (_meeting != null)
            {
                _startTime = DateTime.Today.Add(_meeting.StartTime.ToTimeSpan());
                _endTime = DateTime.Today.Add(_meeting.EndTime.ToTimeSpan());
                _activities = await MeetingService.GetActivitiesForMeetingAsync(MeetingId);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading meeting: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void AddActivity()
    {
        _activities.Add(new Activity
        {
            MeetingId = MeetingId,
            Name = string.Empty,
            RequiresConsent = false,
            SortOrder = _activities.Count
        });
    }

    private void RemoveActivity(int index)
    {
        _activities.RemoveAt(index);
        for (int i = 0; i < _activities.Count; i++)
        {
            _activities[i].SortOrder = i;
        }
    }

    private async Task SaveMeeting()
    {
        if (_meeting == null) return;

        _isSaving = true;
        ClearError();

        try
        {
            // Set times
            _meeting.StartTime = TimeOnly.FromDateTime(_startTime);
            _meeting.EndTime = TimeOnly.FromDateTime(_endTime);

            var result = await MeetingService.UpdateAsync(_meeting);

            if (result.Success)
            {
                // Update activities separately
                await UpdateActivities();
                NavigationManager.NavigateTo($"/Meetings/View/{MeetingId}?success=updated");
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

    private async Task UpdateActivities()
    {
        var existingActivities = await MeetingService.GetActivitiesForMeetingAsync(MeetingId);

        // Delete removed activities
        foreach (var existing in existingActivities)
        {
            if (_activities.All(a => a.Id != existing.Id))
            {
                await MeetingService.DeleteActivityAsync(existing.Id);
            }
        }

        // Update or add activities
        foreach (var activity in _activities.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
        {
            if (activity.Id > 0)
            {
                await MeetingService.UpdateActivityAsync(activity);
            }
            else
            {
                activity.MeetingId = MeetingId;
                await MeetingService.AddActivityAsync(activity);
            }
        }
    }

    private void ShowDeleteConfirm()
    {
        _showDeleteConfirm = true;
    }

    private void CancelDelete()
    {
        _showDeleteConfirm = false;
    }

    private async Task DeleteMeeting()
    {
        if (_meeting == null) return;

        _isDeleting = true;

        try
        {
            var result = await MeetingService.DeleteAsync(MeetingId);

            if (result.Success)
            {
                NavigationManager.NavigateTo("/Meetings?success=deleted");
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showDeleteConfirm = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showDeleteConfirm = false;
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }
}
