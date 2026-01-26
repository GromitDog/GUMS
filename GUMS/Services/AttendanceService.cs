using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

/// <summary>
/// Service for managing attendance records and consent tracking.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ITermService _termService;
    private readonly IMeetingService _meetingService;

    public AttendanceService(ApplicationDbContext context, ITermService termService, IMeetingService meetingService)
    {
        _context = context;
        _termService = termService;
        _meetingService = meetingService;
    }

    // ===== Attendance CRUD Operations =====

    public async Task<List<Attendance>> GetAttendanceForMeetingAsync(int meetingId)
    {
        return await _context.Attendances
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .OrderBy(a => a.MembershipNumber)
            .ToListAsync();
    }

    public async Task<Attendance?> GetAttendanceRecordAsync(int meetingId, string membershipNumber)
    {
        return await _context.Attendances
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.MembershipNumber == membershipNumber);
    }

    public async Task<List<Attendance>> GetAttendanceHistoryForMemberAsync(string membershipNumber)
    {
        return await _context.Attendances
            .Include(a => a.Meeting)
            .AsNoTracking()
            .Where(a => a.MembershipNumber == membershipNumber)
            .OrderByDescending(a => a.Meeting.Date)
            .ToListAsync();
    }

    public async Task<List<Attendance>> GetAttendanceForMemberInRangeAsync(string membershipNumber, DateTime startDate, DateTime endDate)
    {
        return await _context.Attendances
            .Include(a => a.Meeting)
            .AsNoTracking()
            .Where(a => a.MembershipNumber == membershipNumber
                && a.Meeting.Date >= startDate
                && a.Meeting.Date <= endDate)
            .OrderBy(a => a.Meeting.Date)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage, Attendance? Attendance)> SaveAttendanceRecordAsync(Attendance attendance)
    {
        // Validate meeting exists and get meeting details for nights away calculation
        var meeting = await _context.Meetings.FindAsync(attendance.MeetingId);
        if (meeting == null)
        {
            return (false, "Meeting not found.", null);
        }

        // Validate membership number is not empty
        if (string.IsNullOrWhiteSpace(attendance.MembershipNumber))
        {
            return (false, "Membership number is required.", null);
        }

        // Auto-calculate NightsAway for multi-day meetings if not already set
        if (attendance.Attended && meeting.EndDate.HasValue && !attendance.NightsAway.HasValue)
        {
            attendance.NightsAway = _meetingService.CalculateNightsForMeeting(meeting.Date, meeting.EndDate);
        }
        else if (!attendance.Attended)
        {
            // If not attended, clear nights away
            attendance.NightsAway = null;
        }

        // Check if record already exists
        var existingRecord = await _context.Attendances
            .FirstOrDefaultAsync(a => a.MeetingId == attendance.MeetingId
                && a.MembershipNumber == attendance.MembershipNumber);

        if (existingRecord != null)
        {
            // Update existing record
            existingRecord.Attended = attendance.Attended;
            existingRecord.SignedUp = attendance.SignedUp;
            existingRecord.ConsentEmailReceived = attendance.ConsentEmailReceived;
            existingRecord.ConsentEmailDate = attendance.ConsentEmailDate;
            existingRecord.ConsentFormReceived = attendance.ConsentFormReceived;
            existingRecord.ConsentFormDate = attendance.ConsentFormDate;
            existingRecord.Notes = attendance.Notes;
            existingRecord.NightsAway = attendance.NightsAway;

            await _context.SaveChangesAsync();
            return (true, string.Empty, existingRecord);
        }
        else
        {
            // Create new record
            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();
            return (true, string.Empty, attendance);
        }
    }

    public async Task<(bool Success, string ErrorMessage, int RecordsSaved)> SaveBulkAttendanceAsync(int meetingId, List<Attendance> attendanceRecords)
    {
        // Validate meeting exists and get meeting details for nights away calculation
        var meeting = await _context.Meetings.FindAsync(meetingId);
        if (meeting == null)
        {
            return (false, "Meeting not found.", 0);
        }

        if (attendanceRecords == null || !attendanceRecords.Any())
        {
            return (false, "No attendance records provided.", 0);
        }

        // Calculate default nights for multi-day meetings
        var defaultNights = meeting.EndDate.HasValue
            ? _meetingService.CalculateNightsForMeeting(meeting.Date, meeting.EndDate)
            : (int?)null;

        // Get all existing records for this meeting
        var existingRecords = await _context.Attendances
            .Where(a => a.MeetingId == meetingId)
            .ToDictionaryAsync(a => a.MembershipNumber);

        var recordsSaved = 0;

        foreach (var record in attendanceRecords)
        {
            if (string.IsNullOrWhiteSpace(record.MembershipNumber))
            {
                continue; // Skip invalid records
            }

            record.MeetingId = meetingId; // Ensure meeting ID is set

            // Auto-calculate NightsAway for multi-day meetings if not already set
            if (record.Attended && defaultNights.HasValue && !record.NightsAway.HasValue)
            {
                record.NightsAway = defaultNights;
            }
            else if (!record.Attended)
            {
                record.NightsAway = null;
            }

            if (existingRecords.TryGetValue(record.MembershipNumber, out var existing))
            {
                // Update existing record
                existing.Attended = record.Attended;
                existing.SignedUp = record.SignedUp;
                existing.ConsentEmailReceived = record.ConsentEmailReceived;
                existing.ConsentEmailDate = record.ConsentEmailDate;
                existing.ConsentFormReceived = record.ConsentFormReceived;
                existing.ConsentFormDate = record.ConsentFormDate;
                existing.Notes = record.Notes;
                existing.NightsAway = record.NightsAway;
            }
            else
            {
                // Add new record
                _context.Attendances.Add(record);
            }

            recordsSaved++;
        }

        if (recordsSaved > 0)
        {
            await _context.SaveChangesAsync();
        }

        return (true, string.Empty, recordsSaved);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteAttendanceRecordAsync(int attendanceId)
    {
        var record = await _context.Attendances.FindAsync(attendanceId);
        if (record == null)
        {
            return (false, "Attendance record not found.");
        }

        _context.Attendances.Remove(record);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Sign-up Tracking (for extra meetings) =====

    public async Task<(bool Success, string ErrorMessage)> SignUpMemberForMeetingAsync(int meetingId, string membershipNumber)
    {
        if (string.IsNullOrWhiteSpace(membershipNumber))
        {
            return (false, "Membership number is required.");
        }

        var meetingExists = await _context.Meetings.AnyAsync(m => m.Id == meetingId);
        if (!meetingExists)
        {
            return (false, "Meeting not found.");
        }

        var existingRecord = await _context.Attendances
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.MembershipNumber == membershipNumber);

        if (existingRecord != null)
        {
            existingRecord.SignedUp = true;
        }
        else
        {
            _context.Attendances.Add(new Attendance
            {
                MeetingId = meetingId,
                MembershipNumber = membershipNumber,
                SignedUp = true,
                Attended = false
            });
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> RemoveSignUpAsync(int meetingId, string membershipNumber)
    {
        var record = await _context.Attendances
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.MembershipNumber == membershipNumber);

        if (record == null)
        {
            return (false, "Sign-up record not found.");
        }

        // If the record only has sign-up info (no attendance recorded), delete it
        if (!record.Attended && !record.ConsentEmailReceived && !record.ConsentFormReceived)
        {
            _context.Attendances.Remove(record);
        }
        else
        {
            // Otherwise just clear the sign-up flag
            record.SignedUp = false;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<List<Attendance>> GetSignedUpMembersAsync(int meetingId)
    {
        return await _context.Attendances
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId && a.SignedUp)
            .OrderBy(a => a.MembershipNumber)
            .ToListAsync();
    }

    // ===== Consent Tracking =====

    public async Task<(bool Success, string ErrorMessage)> UpdateConsentEmailStatusAsync(
        int meetingId,
        string membershipNumber,
        bool received,
        DateTime? dateReceived = null)
    {
        if (string.IsNullOrWhiteSpace(membershipNumber))
        {
            return (false, "Membership number is required.");
        }

        var record = await _context.Attendances
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.MembershipNumber == membershipNumber);

        if (record == null)
        {
            // Create new record with consent info
            record = new Attendance
            {
                MeetingId = meetingId,
                MembershipNumber = membershipNumber,
                ConsentEmailReceived = received,
                ConsentEmailDate = received ? (dateReceived ?? DateTime.Today) : null
            };
            _context.Attendances.Add(record);
        }
        else
        {
            record.ConsentEmailReceived = received;
            record.ConsentEmailDate = received ? (dateReceived ?? DateTime.Today) : null;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateConsentFormStatusAsync(
        int meetingId,
        string membershipNumber,
        bool received,
        DateTime? dateReceived = null)
    {
        if (string.IsNullOrWhiteSpace(membershipNumber))
        {
            return (false, "Membership number is required.");
        }

        var record = await _context.Attendances
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.MembershipNumber == membershipNumber);

        if (record == null)
        {
            // Create new record with consent info
            record = new Attendance
            {
                MeetingId = meetingId,
                MembershipNumber = membershipNumber,
                ConsentFormReceived = received,
                ConsentFormDate = received ? (dateReceived ?? DateTime.Today) : null
            };
            _context.Attendances.Add(record);
        }
        else
        {
            record.ConsentFormReceived = received;
            record.ConsentFormDate = received ? (dateReceived ?? DateTime.Today) : null;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<List<Attendance>> GetMembersWithOutstandingConsentAsync(int meetingId)
    {
        return await _context.Attendances
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId
                && a.ConsentEmailReceived
                && !a.ConsentFormReceived)
            .OrderBy(a => a.MembershipNumber)
            .ToListAsync();
    }

    public async Task<List<Attendance>> GetMembersNeedingConsentAsync(int meetingId)
    {
        return await _context.Attendances
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId
                && !a.ConsentEmailReceived)
            .OrderBy(a => a.MembershipNumber)
            .ToListAsync();
    }

    // ===== Attendance Statistics =====

    public async Task<AttendanceStats> GetMeetingAttendanceStatsAsync(int meetingId)
    {
        var records = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .ToListAsync();

        return new AttendanceStats
        {
            MeetingId = meetingId,
            TotalMembers = records.Count,
            Attended = records.Count(r => r.Attended),
            NotAttended = records.Count(r => !r.Attended),
            SignedUp = records.Count(r => r.SignedUp),
            ConsentEmailReceived = records.Count(r => r.ConsentEmailReceived),
            ConsentFormReceived = records.Count(r => r.ConsentFormReceived),
            OutstandingConsent = records.Count(r => r.ConsentEmailReceived && !r.ConsentFormReceived),
            HasBeenRecorded = records.Any()
        };
    }

    public async Task<MemberAttendanceStats> GetMemberAttendanceStatsAsync(string membershipNumber, int termId)
    {
        var term = await _termService.GetByIdAsync(termId);
        if (term == null)
        {
            return new MemberAttendanceStats
            {
                MembershipNumber = membershipNumber,
                TermId = termId
            };
        }

        // Get all meetings in the term
        var meetingsInTerm = await _context.Meetings
            .AsNoTracking()
            .Where(m => m.Date >= term.StartDate && m.Date <= term.EndDate)
            .Select(m => m.Id)
            .ToListAsync();

        // Get attendance records for this member
        var attendanceRecords = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.MembershipNumber == membershipNumber && meetingsInTerm.Contains(a.MeetingId))
            .ToListAsync();

        // Get member name (if still active)
        var person = await _context.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber && !p.IsDataRemoved);

        var totalMeetings = meetingsInTerm.Count;
        var attended = attendanceRecords.Count(a => a.Attended);

        return new MemberAttendanceStats
        {
            MembershipNumber = membershipNumber,
            MemberName = person?.FullName,
            TermId = termId,
            TotalMeetings = totalMeetings,
            MeetingsAttended = attended,
            MeetingsMissed = totalMeetings - attended
        };
    }

    public async Task<List<MemberAttendanceAlert>> GetFullTermAbsencesAsync(int termId)
    {
        var term = await _termService.GetByIdAsync(termId);
        if (term == null)
        {
            return new List<MemberAttendanceAlert>();
        }

        // Get all past meetings in the term (only count past meetings)
        var today = DateTime.Today;
        var pastMeetingsInTerm = await _context.Meetings
            .AsNoTracking()
            .Where(m => m.Date >= term.StartDate && m.Date <= term.EndDate && m.Date < today)
            .Select(m => m.Id)
            .ToListAsync();

        if (!pastMeetingsInTerm.Any())
        {
            return new List<MemberAttendanceAlert>(); // No meetings yet
        }

        // Get all active girls (not leaders) who joined before the term started
        var activeMembers = await _context.Persons
            .AsNoTracking()
            .Where(p => p.IsActive
                && !p.IsDataRemoved
                && p.PersonType == PersonType.Girl
                && p.DateJoined <= term.StartDate)
            .ToListAsync();

        var alerts = new List<MemberAttendanceAlert>();

        foreach (var member in activeMembers)
        {
            // Get attendance for this member
            var attendedCount = await _context.Attendances
                .CountAsync(a => a.MembershipNumber == member.MembershipNumber
                    && pastMeetingsInTerm.Contains(a.MeetingId)
                    && a.Attended);

            if (attendedCount == 0)
            {
                alerts.Add(new MemberAttendanceAlert
                {
                    MembershipNumber = member.MembershipNumber,
                    MemberName = member.FullName,
                    TermId = termId,
                    TermName = term.Name,
                    TotalMeetings = pastMeetingsInTerm.Count,
                    MeetingsAttended = 0,
                    AttendancePercent = 0,
                    AlertType = "FullTermAbsence"
                });
            }
        }

        return alerts;
    }

    public async Task<List<MemberAttendanceAlert>> GetLowAttendanceAlertsAsync(int termId, int thresholdPercent = 25)
    {
        var term = await _termService.GetByIdAsync(termId);
        if (term == null)
        {
            return new List<MemberAttendanceAlert>();
        }

        // Get all past meetings in the term
        var today = DateTime.Today;
        var pastMeetingsInTerm = await _context.Meetings
            .AsNoTracking()
            .Where(m => m.Date >= term.StartDate && m.Date <= term.EndDate && m.Date < today)
            .Select(m => m.Id)
            .ToListAsync();

        if (!pastMeetingsInTerm.Any())
        {
            return new List<MemberAttendanceAlert>();
        }

        // Get all active girls who joined before the term started
        var activeMembers = await _context.Persons
            .AsNoTracking()
            .Where(p => p.IsActive
                && !p.IsDataRemoved
                && p.PersonType == PersonType.Girl
                && p.DateJoined <= term.StartDate)
            .ToListAsync();

        var alerts = new List<MemberAttendanceAlert>();
        var totalMeetings = pastMeetingsInTerm.Count;

        foreach (var member in activeMembers)
        {
            var attendedCount = await _context.Attendances
                .CountAsync(a => a.MembershipNumber == member.MembershipNumber
                    && pastMeetingsInTerm.Contains(a.MeetingId)
                    && a.Attended);

            var attendancePercent = totalMeetings > 0 ? (double)attendedCount / totalMeetings * 100 : 0;

            if (attendancePercent < thresholdPercent && attendedCount > 0) // > 0 excludes full absences (handled separately)
            {
                alerts.Add(new MemberAttendanceAlert
                {
                    MembershipNumber = member.MembershipNumber,
                    MemberName = member.FullName,
                    TermId = termId,
                    TermName = term.Name,
                    TotalMeetings = totalMeetings,
                    MeetingsAttended = attendedCount,
                    AttendancePercent = attendancePercent,
                    AlertType = "LowAttendance"
                });
            }
        }

        return alerts.OrderBy(a => a.AttendancePercent).ToList();
    }

    // ===== Nights Away Tracking =====

    public async Task<int> GetTotalNightsAwayAsync(string membershipNumber)
    {
        return await _context.Attendances
            .Where(a => a.MembershipNumber == membershipNumber && a.Attended && a.NightsAway.HasValue)
            .SumAsync(a => a.NightsAway ?? 0);
    }

    public async Task<int> GetNightsAwayInRangeAsync(string membershipNumber, DateTime startDate, DateTime endDate)
    {
        return await _context.Attendances
            .Include(a => a.Meeting)
            .Where(a => a.MembershipNumber == membershipNumber
                && a.Attended
                && a.NightsAway.HasValue
                && a.Meeting.Date >= startDate
                && a.Meeting.Date <= endDate)
            .SumAsync(a => a.NightsAway ?? 0);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateNightsAwayAsync(int attendanceId, int? nightsAway)
    {
        var record = await _context.Attendances.FindAsync(attendanceId);
        if (record == null)
        {
            return (false, "Attendance record not found.");
        }

        if (nightsAway.HasValue && nightsAway.Value < 0)
        {
            return (false, "Nights away cannot be negative.");
        }

        record.NightsAway = nightsAway;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<List<MemberNightsAwaySummary>> GetNightsAwaySummaryAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Attendances
            .Include(a => a.Meeting)
            .Where(a => a.Attended && a.NightsAway.HasValue && a.NightsAway > 0);

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.Meeting.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.Meeting.Date <= toDate.Value);
        }

        // Group by membership number and calculate totals
        var grouped = await query
            .GroupBy(a => a.MembershipNumber)
            .Select(g => new
            {
                MembershipNumber = g.Key,
                TotalNightsAway = g.Sum(a => a.NightsAway ?? 0),
                MultiDayEventsAttended = g.Count()
            })
            .ToListAsync();

        // Get member details
        var membershipNumbers = grouped.Select(g => g.MembershipNumber).ToList();
        var members = await _context.Persons
            .AsNoTracking()
            .Where(p => membershipNumbers.Contains(p.MembershipNumber) && !p.IsDataRemoved)
            .ToDictionaryAsync(p => p.MembershipNumber);

        return grouped.Select(g =>
        {
            members.TryGetValue(g.MembershipNumber, out var person);
            return new MemberNightsAwaySummary
            {
                MembershipNumber = g.MembershipNumber,
                MemberName = person?.FullName,
                PersonType = person?.PersonType.ToString(),
                TotalNightsAway = g.TotalNightsAway,
                MultiDayEventsAttended = g.MultiDayEventsAttended
            };
        })
        .OrderByDescending(s => s.TotalNightsAway)
        .ToList();
    }

    // ===== Query Helpers =====

    public async Task<bool> HasAttendanceBeenRecordedAsync(int meetingId)
    {
        return await _context.Attendances.AnyAsync(a => a.MeetingId == meetingId);
    }

    public async Task<int> GetAttendanceCountAsync(int meetingId)
    {
        return await _context.Attendances
            .CountAsync(a => a.MeetingId == meetingId && a.Attended);
    }

    public async Task<bool> MeetingRequiresConsentAsync(int meetingId)
    {
        return await _context.Activities
            .AnyAsync(a => a.MeetingId == meetingId && a.RequiresConsent);
    }

    public async Task<(bool Success, string ErrorMessage, int RecordsCreated)> InitializeAttendanceForMeetingAsync(int meetingId)
    {
        var meeting = await _context.Meetings.FindAsync(meetingId);
        if (meeting == null)
        {
            return (false, "Meeting not found.", 0);
        }

        // Get all active members (both girls and leaders)
        var activeMembers = await _context.Persons
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsDataRemoved)
            .Select(p => p.MembershipNumber)
            .ToListAsync();

        // Get existing attendance records for this meeting
        var existingRecords = await _context.Attendances
            .Where(a => a.MeetingId == meetingId)
            .Select(a => a.MembershipNumber)
            .ToHashSetAsync();

        var recordsCreated = 0;

        foreach (var membershipNumber in activeMembers)
        {
            if (existingRecords.Contains(membershipNumber))
            {
                continue; // Already has a record
            }

            _context.Attendances.Add(new Attendance
            {
                MeetingId = meetingId,
                MembershipNumber = membershipNumber,
                Attended = false,
                SignedUp = false
            });

            recordsCreated++;
        }

        if (recordsCreated > 0)
        {
            await _context.SaveChangesAsync();
        }

        return (true, string.Empty, recordsCreated);
    }
}
