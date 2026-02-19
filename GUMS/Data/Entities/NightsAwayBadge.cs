using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class NightsAwayBadge
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    public int Milestone { get; set; } // 1, 5, 10, 15, 20, 25, 30

    public DateTime DateAwarded { get; set; }
}
