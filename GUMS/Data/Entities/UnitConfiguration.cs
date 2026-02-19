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

    [Required]
    [Range(0, 10000)]
    public decimal JoiningFeeAmount { get; set; }

    /// <summary>Day of month the financial year ends (e.g. 31 for 31st July).</summary>
    [Required]
    [Range(1, 31)]
    public int FinancialYearEndDay { get; set; } = 31;

    /// <summary>Month the financial year ends (e.g. 7 for July).</summary>
    [Required]
    [Range(1, 12)]
    public int FinancialYearEndMonth { get; set; } = 7;

    /// <summary>
    /// When set, no transactions may be posted on or before this date (year-end lock).
    /// </summary>
    public DateTime? AccountsLockedUntil { get; set; }
}
