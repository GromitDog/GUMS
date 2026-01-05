using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class Attendance
{
    public int Id { get; set; }

    [Required]
    public int MeetingId { get; set; }

    // CRITICAL: Uses MembershipNumber (string) NOT PersonId (int FK)
    // This allows attendance records to persist after member data is removed
    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    public bool Attended { get; set; } = false;

    public bool SignedUp { get; set; } = false;

    public bool ConsentEmailReceived { get; set; } = false;

    public DateTime? ConsentEmailDate { get; set; }

    public bool ConsentFormReceived { get; set; } = false;

    public DateTime? ConsentFormDate { get; set; }

    public string? Notes { get; set; }

    // Navigation property
    public Meeting Meeting { get; set; } = null!;
}
