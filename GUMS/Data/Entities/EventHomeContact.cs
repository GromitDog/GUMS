using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class EventHomeContact
{
    public int Id { get; set; }

    [Required]
    public int MeetingId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Phone { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int SortOrder { get; set; } = 0;

    // Navigation property
    public Meeting Meeting { get; set; } = null!;
}
