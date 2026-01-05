using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class DataRemovalLog
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PersonName { get; set; } = string.Empty;

    [Required]
    public DateTime RemovalDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string RemovedBy { get; set; } = string.Empty;

    public bool DataExported { get; set; } = false;

    public string? Notes { get; set; }
}
