using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class AwardedBadge
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    [Required]
    public int BadgeDefinitionId { get; set; }

    public DateTime DateAwarded { get; set; }

    // Navigation property
    public BadgeDefinition BadgeDefinition { get; set; } = null!;
}
