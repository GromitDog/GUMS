using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class EventBudget
{
    public int Id { get; set; }

    [Required]
    public int MeetingId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Whether leaders are assumed to pay towards this event when projecting income and
    /// break-even charges. Null means "not chosen" — the default is derived from the
    /// meeting's <see cref="Meeting.CostPerLeader"/> (0/null → leaders don't pay).
    /// </summary>
    public bool? LeadersPay { get; set; }

    /// <summary>
    /// Planned number of adults attending, used for per-adult and per-head cost splits.
    /// Null means "use the current active leader count".
    /// </summary>
    public int? PlannedAdultCount { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Meeting Meeting { get; set; } = null!;
    public List<EventBudgetItem> Items { get; set; } = new();
}
