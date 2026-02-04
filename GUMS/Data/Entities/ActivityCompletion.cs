using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class ActivityCompletion
{
    public int Id { get; set; }

    [Required]
    public int MeetingActivityId { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    public bool Completed { get; set; }

    // Navigation property
    public MeetingActivity MeetingActivity { get; set; } = null!;
}
