using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class Patrol
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Section Section { get; set; }

    [Required]
    public int EmblemBadgeDefinitionId { get; set; }

    public BadgeDefinition EmblemBadge { get; set; } = null!;

    public List<Person> Members { get; set; } = new();
}
