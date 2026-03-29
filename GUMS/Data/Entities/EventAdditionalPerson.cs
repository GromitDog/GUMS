using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class EventAdditionalPerson
{
    public int Id { get; set; }

    [Required]
    public int MeetingId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Role { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(50)]
    public string? EmergencyContactPhone { get; set; }

    [MaxLength(100)]
    public string? EmergencyContactRelationship { get; set; }

    public string? Notes { get; set; }

    // Navigation property
    public Meeting Meeting { get; set; } = null!;
}
