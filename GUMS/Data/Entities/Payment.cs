using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class Payment
{
    public int Id { get; set; }

    // CRITICAL: Uses MembershipNumber (string) NOT PersonId (int FK)
    // This allows payment records to persist after member data is removed
    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    [Required]
    [Range(0, 10000)]
    public decimal Amount { get; set; }

    [Required]
    public PaymentType PaymentType { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    [Required]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [Range(0, 10000)]
    public decimal AmountPaid { get; set; } = 0;

    public DateTime? PaymentDate { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reference { get; set; } = string.Empty;

    // Nullable FKs for linking to Term or Meeting
    public int? MeetingId { get; set; }
    public int? TermId { get; set; }

    public string? Notes { get; set; }

    // Computed property
    public decimal OutstandingBalance => Amount - AmountPaid;

    // Navigation properties
    public Meeting? Meeting { get; set; }
    public Term? Term { get; set; }
}
