using GUMS.Data.Entities;

namespace GUMS.Services;

/// <summary>
/// Service for managing meetings and their activities.
/// </summary>
public interface IMeetingService
{
    // ===== Meeting CRUD Operations =====

    Task<List<Meeting>> GetAllAsync();
    Task<Meeting?> GetByIdAsync(int id);
    Task<List<Meeting>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<Meeting>> GetUpcomingAsync(int? limit = null);
    Task<List<Meeting>> GetPastAsync(int? limit = null);
    Task<(bool Success, string ErrorMessage, Meeting? Meeting)> CreateAsync(Meeting meeting);
    Task<(bool Success, string ErrorMessage)> UpdateAsync(Meeting meeting);
    Task<(bool Success, string ErrorMessage)> DeleteAsync(int id);

    // ===== Activity Management =====

    Task<List<MeetingActivity>> GetActivitiesForMeetingAsync(int meetingId);
    Task<(bool Success, string ErrorMessage, MeetingActivity? Activity)> AddActivityAsync(MeetingActivity activity);
    Task<(bool Success, string ErrorMessage)> UpdateActivityAsync(MeetingActivity activity);
    Task<(bool Success, string ErrorMessage)> DeleteActivityAsync(int activityId);

    // ===== Meeting Generation =====

    Task<List<DateTime>> GetSuggestedMeetingDatesForTermAsync(int termId);
    Task<(bool Success, string ErrorMessage, int MeetingsCreated)> GenerateRegularMeetingsForTermAsync(int termId, string? title = null);

    // ===== Multi-Day Meeting Support =====

    int CalculateNightsForMeeting(DateTime startDate, DateTime? endDate);
    Task<List<Meeting>> GetMultiDayMeetingsAsync(int? limit = null);

    // ===== Query Helpers =====

    Task<bool> MeetingExistsOnDateAsync(DateTime date);
    Task<DateTime?> GetNextMeetingDateAsync();
    Task<int> GetMeetingCountInRangeAsync(DateTime startDate, DateTime endDate);
}
