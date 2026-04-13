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
    [Inject] public required IBadgeService BadgeService { get; set; }
    [Inject] public required IAccountingService AccountingService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private readonly Meeting _meeting = new();
    private readonly List<MeetingActivity> _activities = [];
    private List<BadgeClause> _availableClauses = [];
    private List<UmaDefinition> _availableUmas = [];
    private List<BadgeDefinition> _availableFunBadges = [];
    private List<Account> _incomeAccounts = [];
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

        // Try to find the next available meeting date within current or future term
        var currentTerm = await TermService.GetCurrentTermAsync();
        List<DateTime> suggestedDates = [];
        if (currentTerm != null)
        {
            suggestedDates = await MeetingService.GetSuggestedMeetingDatesForTermAsync(currentTerm.Id);
        }

        if (!suggestedDates.Any())
        {
            var futureTerms = await TermService.GetFutureTermsAsync();
            foreach (var term in futureTerms)
            {
                suggestedDates = await MeetingService.GetSuggestedMeetingDatesForTermAsync(term.Id);
                if (suggestedDates.Any())
                    break;
            }
        }

        _meeting.Date = suggestedDates.Any() ? suggestedDates.First() : DateTime.Today;

        _availableClauses = await BadgeService.SearchClausesAsync(string.Empty);
        _availableUmas = await BadgeService.SearchUmasAsync(string.Empty);
        _availableFunBadges = await BadgeService.GetBadgesByFilterAsync(badgeType: BadgeType.FunBadge);
        _incomeAccounts = await AccountingService.GetAccountsByTypeAsync(Data.Enums.AccountType.Income);
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