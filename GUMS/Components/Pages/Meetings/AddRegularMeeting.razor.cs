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
    [Inject] public required IBadgeService BadgeService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private readonly Meeting _meeting = new();
    private readonly List<MeetingActivity> _activities = new();
    private List<BadgeClause> _availableClauses = [];
    private List<UmaDefinition> _availableUmas = [];
    private List<BadgeDefinition> _availableFunBadges = [];
    private DateTime _startTime = DateTime.Today;
    private DateTime _endTime = DateTime.Today;
    private List<DateTime> _suggestedDates = [];
    private bool _suggestedDatesUsed;
    private Term? _activeTerm;

    private bool _isSaving;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Load configuration defaults
        var config = await ConfigService.GetConfigurationAsync();
        _meeting.MeetingType = MeetingType.Regular;
        _meeting.Title = "Weekly Meeting";
        _meeting.LocationName = config.DefaultLocationName;
        _meeting.LocationAddress = config.DefaultLocationAddress;

        _startTime = DateTime.Today.Add(config.DefaultMeetingStartTime.ToTimeSpan());
        _endTime = DateTime.Today.Add(config.DefaultMeetingEndTime.ToTimeSpan());

        // Default date to next unplanned meeting day within current or future term
        _activeTerm = await TermService.GetCurrentTermAsync();
        if (_activeTerm != null)
        {
            _suggestedDates = await MeetingService.GetSuggestedMeetingDatesForTermAsync(_activeTerm.Id);
        }

        // If no current term or no available dates, try future terms
        if (!_suggestedDates.Any())
        {
            var futureTerms = await TermService.GetFutureTermsAsync();
            foreach (var term in futureTerms)
            {
                _suggestedDates = await MeetingService.GetSuggestedMeetingDatesForTermAsync(term.Id);
                if (_suggestedDates.Any())
                {
                    _activeTerm = term;
                    break;
                }
            }
        }

        _meeting.Date = _suggestedDates.Any() ? _suggestedDates.First() : DateTime.Today;

        _availableClauses = await BadgeService.SearchClausesAsync(string.Empty);
        _availableUmas = await BadgeService.SearchUmasAsync(string.Empty);
        _availableFunBadges = await BadgeService.GetBadgesByFilterAsync(badgeType: BadgeType.FunBadge);
    }

    private void UseSuggestedDate(DateTime date)
    {
        _meeting.Date = date;
        _suggestedDatesUsed = true;
    }

    private void AddActivity()
    {
        _activities.Add(new MeetingActivity
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

            // Validate: activities with a linked badge/UMA but no name
            var unnamed = _activities.Where(a =>
                string.IsNullOrWhiteSpace(a.Name) &&
                (a.BadgeClauseId.HasValue || a.UmaDefinitionId.HasValue || a.BadgeDefinitionId.HasValue)).ToList();
            if (unnamed.Any())
            {
                _errorMessage = "All activities must have a name.";
                return;
            }

            // Filter out completely empty activities (no name, no links)
            _meeting.MeetingActivities = _activities
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
