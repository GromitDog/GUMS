using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class Person
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    // Nullable - removed when member leaves
    [MaxLength(200)]
    public string? FullName { get; set; }

    // Nullable - removed when member leaves
    public DateTime? DateOfBirth { get; set; }

    [Required]
    public PersonType PersonType { get; set; }

    // Only for girls
    public Section? Section { get; set; }

    // Only for leaders - contact details
    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [Required]
    public DateTime DateJoined { get; set; }

    public DateTime? DateLeft { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDataRemoved { get; set; } = false;

    // Nullable - removed when member leaves
    public string? Allergies { get; set; }

    // Nullable - removed when member leaves
    public string? Disabilities { get; set; }

    // Nullable - removed when member leaves
    public string? Notes { get; set; }

    public PhotoPermission PhotoPermission { get; set; } = PhotoPermission.None;

    // Navigation properties
    public List<EmergencyContact> EmergencyContacts { get; set; } = new();
}
