using GUMS.Data.Entities;

namespace GUMS.Services;

/// <summary>
/// Service for managing meetings and their activities.
/// </summary>
public interface IMeetingService
{
    // ===== Meeting CRUD Operations =====

    /// <summary>
    /// Gets all meetings ordered by date descending (most recent first).
    /// </summary>
    Task<List<Meeting>> GetAllAsync();

    /// <summary>
    /// Gets a meeting by its ID, including all activities.
    /// </summary>
    Task<Meeting?> GetByIdAsync(int id);

    /// <summary>
    /// Gets meetings within a specific date range, ordered by date ascending.
    /// </summary>
    Task<List<Meeting>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets upcoming meetings (from today onwards), ordered by date ascending.
    /// </summary>
    /// <param name="limit">Optional limit on number of meetings to return.</param>
    Task<List<Meeting>> GetUpcomingAsync(int? limit = null);

    /// <summary>
    /// Gets past meetings (before today), ordered by date descending.
    /// </summary>
    /// <param name="limit">Optional limit on number of meetings to return.</param>
    Task<List<Meeting>> GetPastAsync(int? limit = null);

    /// <summary>
    /// Creates a new meeting.
    /// </summary>
    /// <param name="meeting">The meeting to create.</param>
    /// <returns>The created meeting with ID assigned.</returns>
    Task<(bool Success, string ErrorMessage, Meeting? Meeting)> CreateAsync(Meeting meeting);

    /// <summary>
    /// Updates an existing meeting.
    /// </summary>
    /// <param name="meeting">The meeting to update.</param>
    Task<(bool Success, string ErrorMessage)> UpdateAsync(Meeting meeting);

    /// <summary>
    /// Deletes a meeting if it has no attendance records.
    /// </summary>
    /// <param name="id">The ID of the meeting to delete.</param>
    Task<(bool Success, string ErrorMessage)> DeleteAsync(int id);

    // ===== Activity Management =====

    /// <summary>
    /// Gets all activities for a specific meeting.
    /// </summary>
    Task<List<Activity>> GetActivitiesForMeetingAsync(int meetingId);

    /// <summary>
    /// Adds an activity to a meeting.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Activity? Activity)> AddActivityAsync(Activity activity);

    /// <summary>
    /// Updates an activity.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateActivityAsync(Activity activity);

    /// <summary>
    /// Deletes an activity.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> DeleteActivityAsync(int activityId);

    // ===== Meeting Generation =====

    /// <summary>
    /// Generates suggested meeting dates for a term based on the configured meeting day.
    /// Does not create meetings in the database - just returns suggested dates.
    /// </summary>
    /// <param name="termId">The term ID to generate meetings for.</param>
    /// <returns>List of suggested meeting dates.</returns>
    Task<List<DateTime>> GetSuggestedMeetingDatesForTermAsync(int termId);

    /// <summary>
    /// Creates regular meetings for a term based on the suggested dates.
    /// Uses default configuration for times and location.
    /// </summary>
    /// <param name="termId">The term ID to generate meetings for.</param>
    /// <param name="title">Optional title prefix (e.g., "Weekly Meeting"). Will append date if provided.</param>
    /// <returns>Number of meetings created.</returns>
    Task<(bool Success, string ErrorMessage, int MeetingsCreated)> GenerateRegularMeetingsForTermAsync(int termId, string? title = null);

    // ===== Multi-Day Meeting Support =====

    /// <summary>
    /// Calculates the number of nights for a meeting.
    /// For a meeting from Jan 5-7, returns 2 (nights of 5th and 6th).
    /// Returns 0 for single-day meetings.
    /// </summary>
    int CalculateNightsForMeeting(DateTime startDate, DateTime? endDate);

    /// <summary>
    /// Gets all multi-day meetings (camps, sleepovers) where EndDate is set.
    /// </summary>
    Task<List<Meeting>> GetMultiDayMeetingsAsync(int? limit = null);

    // ===== Query Helpers =====

    /// <summary>
    /// Checks if a meeting exists on a specific date.
    /// </summary>
    Task<bool> MeetingExistsOnDateAsync(DateTime date);

    /// <summary>
    /// Gets the next upcoming meeting date (or null if none scheduled).
    /// </summary>
    Task<DateTime?> GetNextMeetingDateAsync();

    /// <summary>
    /// Gets count of meetings within a date range.
    /// </summary>
    Task<int> GetMeetingCountInRangeAsync(DateTime startDate, DateTime endDate);
}
