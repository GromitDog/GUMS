using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class Activity
{
    public int Id { get; set; }

    [Required]
    public int MeetingId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool RequiresConsent { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    // Navigation property
    public Meeting Meeting { get; set; } = null!;
}
