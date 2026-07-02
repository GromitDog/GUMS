using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class Meeting
{
    public int Id { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [Required]
    public MeetingType MeetingType { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Notes for the printed programme visible to parents/girls (e.g. "wear old clothes").
    /// </summary>
    public string? ProgrammeNotes { get; set; }

    /// <summary>
    /// Internal notes for leaders only (e.g. "no hall", "adult won't be present").
    /// </summary>
    public string? LeaderNotes { get; set; }

    [Required]
    [MaxLength(200)]
    public string LocationName { get; set; } = string.Empty;

    public string? LocationAddress { get; set; }

    [Range(0, 10000)]
    public decimal? CostPerAttendee { get; set; }

    /// <summary>
    /// Cost charged to leaders attending this event.
    /// Null or 0 means leaders don't pay. If set, used instead of CostPerAttendee for payment generation.
    /// </summary>
    [Range(0, 10000)]
    public decimal? CostPerLeader { get; set; }

    public DateTime? PaymentDeadline { get; set; }

    /// <summary>
    /// Income account for event payments. When set, payments credit this account.
    /// </summary>
    public int? IncomeAccountId { get; set; }

    /// <summary>
    /// End date for multi-day events (camps, sleepovers).
    /// Null indicates a single-day meeting.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Default cost centre for this event. Expenses/payments auto-inherit this but can be overridden.
    /// </summary>
    public int? CostCentreId { get; set; }

    // Navigation properties
    public Account? IncomeAccount { get; set; }
    public CostCentre? CostCentre { get; set; }
    public List<MeetingActivity> MeetingActivities { get; set; } = new();
    public List<Attendance> Attendances { get; set; } = new();
}
