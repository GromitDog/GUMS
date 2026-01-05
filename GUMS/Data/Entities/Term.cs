using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class Term
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [Range(0, 10000)]
    public decimal SubsAmount { get; set; }

    // Computed property
    public bool IsActive => DateTime.Now >= StartDate && DateTime.Now <= EndDate;

    // Navigation properties
    public List<Payment> Payments { get; set; } = new();
}
