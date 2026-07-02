using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

/// <summary>
/// A non-charge income line on an event budget — e.g. a grant or fundraising takings.
/// These offset the total cost before working out what to charge each attendee.
/// </summary>
public class EventBudgetIncome
{
    public int Id { get; set; }

    [Required]
    public int EventBudgetId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0, 1000000)]
    public decimal Amount { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public EventBudget EventBudget { get; set; } = null!;
}
