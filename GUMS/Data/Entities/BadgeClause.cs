using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class BadgeClause
{
    public int Id { get; set; }

    [Required]
    public int BadgeDefinitionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    // Navigation properties
    public BadgeDefinition BadgeDefinition { get; set; } = null!;
}
