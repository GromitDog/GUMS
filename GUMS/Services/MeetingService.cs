using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class MeetingService : IMeetingService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfigurationService _configService;
    private readonly ITermService _termService;

    public MeetingService(
        ApplicationDbContext context,
        IConfigurationService configService,
        ITermService termService)
    {
        _context = context;
        _configService = configService;
        _termService = termService;
    }

    // ===== Meeting CRUD Operations =====

    public async Task<List<Meeting>> GetAllAsync()
    {
        return await _context.Meetings
            .Include(m => m.MeetingActivities.OrderBy(a => a.SortOrder))
            .AsNoTracking()
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<Meeting?> GetByIdAsync(int id)
    {
        return await _context.Meetings
            .Include(m => m.MeetingActivities.OrderBy(a => a.SortOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<Meeting>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Meetings
            .Include(m => m.MeetingActivities.OrderBy(a => a.SortOrder))
            .AsNoTracking()
            .Where(m => m.Date >= startDate && m.Date <= endDate)
            .OrderBy(m => m.Date)
            .ToListAsync();
    }

    public async Task<List<Meeting>> GetUpcomingAsync(int? limit = null)
    {
        // Keep meetings visible for 3 weeks after meeting date before moving to past
        var cutoff = DateTime.Today.AddDays(-21);
        var query = _context.Meetings
            .Include(m => m.MeetingActivities.OrderBy(a => a.SortOrder))
            .AsNoTracking()
            .Where(m => m.Date >= cutoff)
            .OrderBy(m => m.Date);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync();
        }

        return await query.ToListAsync();
    }

    public async Task<List<Meeting>> GetPastAsync(int? limit = null)
    {
        // Meetings move to past 3 weeks after their date (matches GetUpcomingAsync cutoff)
        var cutoff = DateTime.Today.AddDays(-21);
        var query = _context.Meetings
            .Include(m => m.MeetingActivities.OrderBy(a => a.SortOrder))
            .AsNoTracking()
            .Where(m => m.Date < cutoff)
            .OrderByDescending(m => m.Date);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync();
        }

        return await query.ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage, Meeting? Meeting)> CreateAsync(Meeting meeting)
    {
        var isMultiDay = meeting.EndDate.HasValue && meeting.EndDate.Value.Date > meeting.Date.Date;
        if (!isMultiDay && meeting.EndTime <= meeting.StartTime)
        {
            return (false, "End time must be after start time.", null);
        }

        if (meeting.CostPerAttendee.HasValue && meeting.CostPerAttendee.Value < 0)
        {
            return (false, "Cost per attendee cannot be negative.", null);
        }

        if (meeting.CostPerAttendee.HasValue && meeting.CostPerAttendee.Value > 0 && !meeting.PaymentDeadline.HasValue)
        {
            return (false, "Payment deadline is required when meeting has a cost.", null);
        }

        if (meeting.EndDate.HasValue && meeting.EndDate.Value.Date < meeting.Date.Date)
        {
            return (false, "End date must be on or after the start date.", null);
        }

        for (int i = 0; i < meeting.MeetingActivities.Count; i++)
        {
            meeting.MeetingActivities[i].SortOrder = i;
        }

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();

        return (true, string.Empty, meeting);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateAsync(Meeting meeting)
    {
        var existingMeeting = await _context.Meetings
            .Include(m => m.MeetingActivities)
            .FirstOrDefaultAsync(m => m.Id == meeting.Id);

        if (existingMeeting == null)
        {
            return (false, "Meeting not found.");
        }

        var isMultiDay = meeting.EndDate.HasValue && meeting.EndDate.Value.Date > meeting.Date.Date;
        if (!isMultiDay && meeting.EndTime <= meeting.StartTime)
        {
            return (false, "End time must be after start time.");
        }

        if (meeting.CostPerAttendee.HasValue && meeting.CostPerAttendee.Value < 0)
        {
            return (false, "Cost per attendee cannot be negative.");
        }

        if (meeting.CostPerAttendee.HasValue && meeting.CostPerAttendee.Value > 0 && !meeting.PaymentDeadline.HasValue)
        {
            return (false, "Payment deadline is required when meeting has a cost.");
        }

        if (meeting.EndDate.HasValue && meeting.EndDate.Value.Date < meeting.Date.Date)
        {
            return (false, "End date must be on or after the start date.");
        }

        existingMeeting.Date = meeting.Date;
        existingMeeting.StartTime = meeting.StartTime;
        existingMeeting.EndTime = meeting.EndTime;
        existingMeeting.MeetingType = meeting.MeetingType;
        existingMeeting.Title = meeting.Title;
        existingMeeting.Description = meeting.Description;
        existingMeeting.LocationName = meeting.LocationName;
        existingMeeting.LocationAddress = meeting.LocationAddress;
        existingMeeting.CostPerAttendee = meeting.CostPerAttendee;
        existingMeeting.PaymentDeadline = meeting.PaymentDeadline;
        existingMeeting.EndDate = meeting.EndDate;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteAsync(int id)
    {
        var meeting = await _context.Meetings
            .Include(m => m.Attendances)
            .Include(m => m.MeetingActivities)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (meeting == null)
        {
            return (false, "Meeting not found.");
        }

        if (meeting.Attendances.Any(a => a.Attended))
        {
            return (false, "Cannot delete this meeting because attendance has been recorded. Please remove attendance records first.");
        }

        if (meeting.Attendances.Any())
        {
            _context.Attendances.RemoveRange(meeting.Attendances);
        }

        _context.MeetingActivities.RemoveRange(meeting.MeetingActivities);
        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Activity Management =====

    public async Task<List<MeetingActivity>> GetActivitiesForMeetingAsync(int meetingId)
    {
        return await _context.MeetingActivities
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage, MeetingActivity? Activity)> AddActivityAsync(MeetingActivity activity)
    {
        var meetingExists = await _context.Meetings.AnyAsync(m => m.Id == activity.MeetingId);
        if (!meetingExists)
        {
            return (false, "Meeting not found.", null);
        }

        var maxSortOrder = await _context.MeetingActivities
            .Where(a => a.MeetingId == activity.MeetingId)
            .MaxAsync(a => (int?)a.SortOrder) ?? -1;

        activity.SortOrder = maxSortOrder + 1;

        _context.MeetingActivities.Add(activity);
        await _context.SaveChangesAsync();

        return (true, string.Empty, activity);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateActivityAsync(MeetingActivity activity)
    {
        var existingActivity = await _context.MeetingActivities.FindAsync(activity.Id);
        if (existingActivity == null)
        {
            return (false, "Activity not found.");
        }

        existingActivity.Name = activity.Name;
        existingActivity.Description = activity.Description;
        existingActivity.RequiresConsent = activity.RequiresConsent;
        existingActivity.SortOrder = activity.SortOrder;
        existingActivity.BadgeClauseId = activity.BadgeClauseId;
        existingActivity.UmaDefinitionId = activity.UmaDefinitionId;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteActivityAsync(int activityId)
    {
        var activity = await _context.MeetingActivities.FindAsync(activityId);
        if (activity == null)
        {
            return (false, "Activity not found.");
        }

        _context.MeetingActivities.Remove(activity);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    // ===== Meeting Generation =====

    public async Task<List<DateTime>> GetSuggestedMeetingDatesForTermAsync(int termId)
    {
        var term = await _termService.GetByIdAsync(termId);
        if (term == null)
        {
            return new List<DateTime>();
        }

        var config = await _configService.GetConfigurationAsync();
        var meetingDay = config.MeetingDayOfWeek;
        var today = DateTime.Today;

        var suggestedDates = new List<DateTime>();
        var currentDate = term.StartDate < today ? today : term.StartDate;

        while (currentDate <= term.EndDate && currentDate.DayOfWeek != meetingDay)
        {
            currentDate = currentDate.AddDays(1);
        }

        var existingMeetingDates = await _context.Meetings
            .Where(m => m.Date >= currentDate && m.Date <= term.EndDate)
            .Select(m => m.Date.Date)
            .ToListAsync();

        while (currentDate <= term.EndDate)
        {
            if (!existingMeetingDates.Contains(currentDate.Date))
            {
                suggestedDates.Add(currentDate);
            }
            currentDate = currentDate.AddDays(7);
        }

        return suggestedDates;
    }

    public async Task<(bool Success, string ErrorMessage, int MeetingsCreated)> GenerateRegularMeetingsForTermAsync(int termId, string? title = null)
    {
        var term = await _termService.GetByIdAsync(termId);
        if (term == null)
        {
            return (false, "Term not found.", 0);
        }

        var config = await _configService.GetConfigurationAsync();
        var suggestedDates = await GetSuggestedMeetingDatesForTermAsync(termId);

        if (!suggestedDates.Any())
        {
            return (false, "No meeting dates found for this term.", 0);
        }

        var meetingsCreated = 0;

        foreach (var date in suggestedDates)
        {
            var exists = await MeetingExistsOnDateAsync(date);
            if (exists)
            {
                continue;
            }

            var meeting = new Meeting
            {
                Date = date,
                StartTime = config.DefaultMeetingStartTime,
                EndTime = config.DefaultMeetingEndTime,
                MeetingType = MeetingType.Regular,
                Title = string.IsNullOrEmpty(title) ? "Weekly Meeting" : $"{title}",
                LocationName = config.DefaultLocationName,
                LocationAddress = config.DefaultLocationAddress,
                CostPerAttendee = null
            };

            _context.Meetings.Add(meeting);
            meetingsCreated++;
        }

        if (meetingsCreated > 0)
        {
            await _context.SaveChangesAsync();
        }

        return (true, string.Empty, meetingsCreated);
    }

    // ===== Multi-Day Meeting Support =====

    public int CalculateNightsForMeeting(DateTime startDate, DateTime? endDate)
    {
        if (!endDate.HasValue)
        {
            return 0;
        }

        var nights = (endDate.Value.Date - startDate.Date).Days;
        return nights > 0 ? nights : 0;
    }

    public async Task<List<Meeting>> GetMultiDayMeetingsAsync(int? limit = null)
    {
        var query = _context.Meetings
            .Include(m => m.MeetingActivities.OrderBy(a => a.SortOrder))
            .AsNoTracking()
            .Where(m => m.EndDate != null)
            .OrderByDescending(m => m.Date);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync();
        }

        return await query.ToListAsync();
    }

    // ===== Query Helpers =====

    public async Task<bool> MeetingExistsOnDateAsync(DateTime date)
    {
        return await _context.Meetings.AnyAsync(m => m.Date.Date == date.Date);
    }

    public async Task<DateTime?> GetNextMeetingDateAsync()
    {
        var today = DateTime.Today;
        var nextMeeting = await _context.Meetings
            .Where(m => m.Date >= today)
            .OrderBy(m => m.Date)
            .FirstOrDefaultAsync();

        return nextMeeting?.Date;
    }

    public async Task<int> GetMeetingCountInRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Meetings
            .CountAsync(m => m.Date >= startDate && m.Date <= endDate);
    }
}
