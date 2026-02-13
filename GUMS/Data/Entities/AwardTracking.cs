using System.ComponentModel.DataAnnotations;

namespace GUMS.Data.Entities;

public class AwardTracking
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    public bool GoldChallengeComplete { get; set; }

    public DateTime? GoldChallengeDate { get; set; }

    public DateTime? BronzeAwardedDate { get; set; }

    public DateTime? SilverAwardedDate { get; set; }

    public DateTime? GoldAwardedDate { get; set; }
}
