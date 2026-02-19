using System.ComponentModel.DataAnnotations;
using GUMS.Data.Enums;

namespace GUMS.Data.Entities;

public class AwardedThemeAward
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string MembershipNumber { get; set; } = string.Empty;

    [Required]
    public Theme Theme { get; set; }

    public DateTime DateAwarded { get; set; }
}
