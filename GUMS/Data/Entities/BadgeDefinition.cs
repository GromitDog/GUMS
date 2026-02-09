using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class BadgeDefinition
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Theme? Theme { get; set; }

    [Required]
    public BadgeType BadgeType { get; set; }

    [Required]
    public Section Section { get; set; }

    public int? Stage { get; set; }

    [MaxLength(100)]
    public string? SkillsBuilderName { get; set; }

    public int RequiredCompletions { get; set; } = 4;

    // Navigation properties
    public List<BadgeClause> Clauses { get; set; } = new();
}
