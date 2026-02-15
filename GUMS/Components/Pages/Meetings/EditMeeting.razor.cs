using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Meetings;

public partial class EditMeeting
{
    [Inject] public required IMeetingService MeetingService { get; set; }
    [Inject] public required IBadgeService BadgeService { get; set; }
    [Inject] public required IAccountingService AccountingService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    [Parameter]
    public int MeetingId { get; set; }

    private Meeting? _meeting;
    private List<MeetingActivity> _activities = [];
    private List<BadgeClause> _availableClauses = [];
    private List<UmaDefinition> _availableUmas = [];
    private List<BadgeDefinition> _availableFunBadges = [];
    private List<Account> _incomeAccounts = [];
    private DateTime _startTime;
    private DateTime _endTime;

    private bool _isLoading = true;
    private bool _isSaving;
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

            _availableClauses = await BadgeService.SearchClausesAsync(string.Empty);
            _availableUmas = await BadgeService.SearchUmasAsync(string.Empty);
            _availableFunBadges = await BadgeService.GetBadgesByFilterAsync(badgeType: BadgeType.FunBadge);
            _incomeAccounts = await AccountingService.GetAccountsByTypeAsync(Data.Enums.AccountType.Income);
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
        _activities.Add(new MeetingActivity
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
            // Validate: activities with a linked badge/UMA but no name
            var unnamed = _activities.Where(a =>
                string.IsNullOrWhiteSpace(a.Name) &&
                (a.BadgeClauseId.HasValue || a.UmaDefinitionId.HasValue || a.BadgeDefinitionId.HasValue)).ToList();
            if (unnamed.Any())
            {
                _errorMessage = "All activities must have a name.";
                return;
            }

            _meeting.StartTime = TimeOnly.FromDateTime(_startTime);
            _meeting.EndTime = TimeOnly.FromDateTime(_endTime);

            var result = await MeetingService.UpdateAsync(_meeting);

            if (result.Success)
            {
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

        foreach (var existing in existingActivities)
        {
            if (_activities.All(a => a.Id != existing.Id))
            {
                await MeetingService.DeleteActivityAsync(existing.Id);
            }
        }

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

    private void SetBadgeClauseId(int activityIndex, ChangeEventArgs e)
    {
        var activity = _activities[activityIndex];
        activity.BadgeClauseId = int.TryParse(e.Value?.ToString(), out var v) ? v : null;
        if (activity.BadgeClauseId.HasValue && string.IsNullOrWhiteSpace(activity.Name))
        {
            var clause = _availableClauses.FirstOrDefault(c => c.Id == activity.BadgeClauseId.Value);
            if (clause != null)
                activity.Name = $"{clause.BadgeDefinition?.Name} - {clause.Name}";
        }
    }

    private void SetUmaDefinitionId(int activityIndex, ChangeEventArgs e)
    {
        var activity = _activities[activityIndex];
        activity.UmaDefinitionId = int.TryParse(e.Value?.ToString(), out var v) ? v : null;
        if (activity.UmaDefinitionId.HasValue && string.IsNullOrWhiteSpace(activity.Name))
        {
            var uma = _availableUmas.FirstOrDefault(u => u.Id == activity.UmaDefinitionId.Value);
            if (uma != null)
                activity.Name = uma.Name;
        }
    }

    private void SetBadgeDefinitionId(int activityIndex, ChangeEventArgs e)
    {
        var activity = _activities[activityIndex];
        activity.BadgeDefinitionId = int.TryParse(e.Value?.ToString(), out var v) ? v : null;
        if (activity.BadgeDefinitionId.HasValue && string.IsNullOrWhiteSpace(activity.Name))
        {
            var badge = _availableFunBadges.FirstOrDefault(b => b.Id == activity.BadgeDefinitionId.Value);
            if (badge != null)
                activity.Name = badge.Name;
        }
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }
}
