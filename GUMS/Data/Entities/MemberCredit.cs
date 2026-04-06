using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class MemberCredit
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    [Range(0, 100000)]
    public decimal Balance { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
