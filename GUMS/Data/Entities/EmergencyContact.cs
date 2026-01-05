using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class EmergencyContact
{
    public int Id { get; set; }

    [Required]
    public int PersonId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ContactName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PrimaryPhone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? SecondaryPhone { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    public string? Notes { get; set; }

    public int SortOrder { get; set; } = 0;

    // Navigation property
    public Person Person { get; set; } = null!;
}
