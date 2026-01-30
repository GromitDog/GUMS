using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class EventBudget
{
    public int Id { get; set; }

    [Required]
    public int MeetingId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Meeting Meeting { get; set; } = null!;
    public List<EventBudgetItem> Items { get; set; } = new();
}
