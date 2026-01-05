using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class UnitConfiguration
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string UnitName { get; set; } = string.Empty;

    [Required]
    public Section UnitType { get; set; }

    [Required]
    public DayOfWeek MeetingDayOfWeek { get; set; }

    [Required]
    public TimeOnly DefaultMeetingStartTime { get; set; }

    [Required]
    public TimeOnly DefaultMeetingEndTime { get; set; }

    [Required]
    [MaxLength(200)]
    public string DefaultLocationName { get; set; } = string.Empty;

    public string? DefaultLocationAddress { get; set; }

    [Required]
    [Range(0, 10000)]
    public decimal DefaultSubsAmount { get; set; }

    [Required]
    [Range(1, 365)]
    public int PaymentTermDays { get; set; } = 14;
}
