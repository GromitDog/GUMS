using GUMS.Data.Entities;

namespace GUMS.Services;

/// <summary>
/// Service for managing attendance records and consent tracking.
/// </summary>
public interface IAttendanceService
{
    // ===== Attendance CRUD Operations =====

    /// <summary>
    /// Gets all attendance records for a specific meeting.
    /// </summary>
    Task<List<Attendance>> GetAttendanceForMeetingAsync(int meetingId);

    /// <summary>
    /// Gets a single attendance record by meeting and membership number.
    /// </summary>
    Task<Attendance?> GetAttendanceRecordAsync(int meetingId, string membershipNumber);

    /// <summary>
    /// Gets all attendance records for a member across all meetings.
    /// </summary>
    Task<List<Attendance>> GetAttendanceHistoryForMemberAsync(string membershipNumber);

    /// <summary>
    /// Gets attendance records for a member within a date range.
    /// </summary>
    Task<List<Attendance>> GetAttendanceForMemberInRangeAsync(string membershipNumber, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Creates or updates an attendance record for a single member at a meeting.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Attendance? Attendance)> SaveAttendanceRecordAsync(Attendance attendance);

    /// <summary>
    /// Saves attendance records for multiple members at a meeting (bulk operation).
    /// This will create new records or update existing ones.
    /// </summary>
    Task<(bool Success, string ErrorMessage, int RecordsSaved)> SaveBulkAttendanceAsync(int meetingId, List<Attendance> attendanceRecords);

    /// <summary>
    /// Deletes an attendance record.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> DeleteAttendanceRecordAsync(int attendanceId);

    // ===== Sign-up Tracking (for extra meetings) =====

    /// <summary>
    /// Marks a member as signed up for a meeting (extra meetings/events).
    /// Creates attendance record with SignedUp = true if it doesn't exist.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> SignUpMemberForMeetingAsync(int meetingId, string membershipNumber);

    /// <summary>
    /// Removes a member's sign-up for a meeting.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> RemoveSignUpAsync(int meetingId, string membershipNumber);

    /// <summary>
    /// Gets all members signed up for a meeting.
    /// </summary>
    Task<List<Attendance>> GetSignedUpMembersAsync(int meetingId);

    // ===== Consent Tracking =====

    /// <summary>
    /// Updates consent email status for a member at a meeting.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateConsentEmailStatusAsync(
        int meetingId,
        string membershipNumber,
        bool received,
        DateTime? dateReceived = null);

    /// <summary>
    /// Updates consent form (physical) status for a member at a meeting.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateConsentFormStatusAsync(
        int meetingId,
        string membershipNumber,
        bool received,
        DateTime? dateReceived = null);

    /// <summary>
    /// Gets all members with outstanding consent forms for a meeting.
    /// Returns members who have email confirmation but no physical form.
    /// </summary>
    Task<List<Attendance>> GetMembersWithOutstandingConsentAsync(int meetingId);

    /// <summary>
    /// Gets all members needing consent for a meeting (email not yet received).
    /// </summary>
    Task<List<Attendance>> GetMembersNeedingConsentAsync(int meetingId);

    // ===== Attendance Statistics =====

    /// <summary>
    /// Gets attendance statistics for a meeting.
    /// </summary>
    Task<AttendanceStats> GetMeetingAttendanceStatsAsync(int meetingId);

    /// <summary>
    /// Gets attendance statistics for a member in a specific term.
    /// </summary>
    Task<MemberAttendanceStats> GetMemberAttendanceStatsAsync(string membershipNumber, int termId);

    /// <summary>
    /// Gets members with full-term absences (attended 0 meetings in the term).
    /// Only includes members who were active at the start of the term.
    /// </summary>
    Task<List<MemberAttendanceAlert>> GetFullTermAbsencesAsync(int termId);

    /// <summary>
    /// Gets members with low attendance (below threshold %) in a term.
    /// </summary>
    Task<List<MemberAttendanceAlert>> GetLowAttendanceAlertsAsync(int termId, int thresholdPercent = 25);

    // ===== Query Helpers =====

    /// <summary>
    /// Checks if attendance has been recorded for a meeting.
    /// </summary>
    Task<bool> HasAttendanceBeenRecordedAsync(int meetingId);

    /// <summary>
    /// Gets count of members who attended a meeting.
    /// </summary>
    Task<int> GetAttendanceCountAsync(int meetingId);

    /// <summary>
    /// Checks if a meeting requires consent (has activities with RequiresConsent = true).
    /// </summary>
    Task<bool> MeetingRequiresConsentAsync(int meetingId);

    /// <summary>
    /// Initializes attendance records for all active members for a meeting.
    /// Used when opening attendance for a meeting for the first time.
    /// </summary>
    Task<(bool Success, string ErrorMessage, int RecordsCreated)> InitializeAttendanceForMeetingAsync(int meetingId);
}

/// <summary>
/// Attendance statistics for a single meeting.
/// </summary>
public class AttendanceStats
{
    public int MeetingId { get; set; }
    public int TotalMembers { get; set; }
    public int Attended { get; set; }
    public int NotAttended { get; set; }
    public int SignedUp { get; set; }
    public int ConsentEmailReceived { get; set; }
    public int ConsentFormReceived { get; set; }
    public int OutstandingConsent { get; set; }
    public bool HasBeenRecorded { get; set; }

    public double AttendancePercent => TotalMembers > 0 ? (double)Attended / TotalMembers * 100 : 0;
}

/// <summary>
/// Attendance statistics for a member in a term.
/// </summary>
public class MemberAttendanceStats
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public int TermId { get; set; }
    public int TotalMeetings { get; set; }
    public int MeetingsAttended { get; set; }
    public int MeetingsMissed { get; set; }

    public double AttendancePercent => TotalMeetings > 0 ? (double)MeetingsAttended / TotalMeetings * 100 : 0;
}

/// <summary>
/// Alert for a member with attendance issues.
/// </summary>
public class MemberAttendanceAlert
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public int TermId { get; set; }
    public string? TermName { get; set; }
    public int TotalMeetings { get; set; }
    public int MeetingsAttended { get; set; }
    public double AttendancePercent { get; set; }
    public string AlertType { get; set; } = string.Empty; // "FullTermAbsence" or "LowAttendance"
    public string? Notes { get; set; }
    public bool IsAcknowledged { get; set; }
}
