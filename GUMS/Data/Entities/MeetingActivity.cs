using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class MeetingActivity
{
    public int Id { get; set; }

    public int? MeetingId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? BadgeClauseId { get; set; }

    public int? BadgeDefinitionId { get; set; }

    public int? UmaDefinitionId { get; set; }

    public bool RequiresConsent { get; set; }

    public int SortOrder { get; set; }

    // Navigation properties
    public Meeting? Meeting { get; set; }
    public BadgeClause? BadgeClause { get; set; }
    public BadgeDefinition? BadgeDefinition { get; set; }
    public UmaDefinition? UmaDefinition { get; set; }
    public List<ActivityCompletion> Completions { get; set; } = new();
}
