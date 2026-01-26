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

    [Required]
    [MaxLength(200)]
    public string LocationName { get; set; } = string.Empty;

    public string? LocationAddress { get; set; }

    [Range(0, 10000)]
    public decimal? CostPerAttendee { get; set; }

    public DateTime? PaymentDeadline { get; set; }

    /// <summary>
    /// End date for multi-day events (camps, sleepovers).
    /// Null indicates a single-day meeting.
    /// </summary>
    public DateTime? EndDate { get; set; }

    // Navigation properties
    public List<Activity> Activities { get; set; } = new();
    public List<Attendance> Attendances { get; set; } = new();
}
