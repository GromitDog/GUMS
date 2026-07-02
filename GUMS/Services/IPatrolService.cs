using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

public interface IPatrolService
{
    /// <summary>Seed the four role badges (Sixer, Brownie Seconder, Patrol Leader, Guide Seconder) if missing.</summary>
    Task EnsureDefaultPatrolBadgesAsync();

    /// <summary>Patrols in a section, including members, ordered by name.</summary>
    Task<List<Patrol>> GetPatrolsAsync(Section section);

    /// <summary>Single patrol with its members loaded.</summary>
    Task<Patrol?> GetPatrolAsync(int patrolId);

    /// <summary>Active girls in a section with no patrol assigned.</summary>
    Task<List<Person>> GetUnassignedGirlsAsync(Section section);

    /// <summary>Map of MembershipNumber → set of awarded BadgeDefinitionIds, for rendering "received" ticks.</summary>
    Task<Dictionary<string, HashSet<int>>> GetAwardedPatrolBadgeMapAsync(Section section);

    /// <summary>Creates patrol and its matching "{name} Emblem" BadgeDefinition.</summary>
    Task<(bool Success, string ErrorMessage, Patrol? Patrol)> CreatePatrolAsync(string name, Section section);

    /// <summary>Renames the patrol and its linked emblem BadgeDefinition (and any stock item).</summary>
    Task<(bool Success, string ErrorMessage)> RenamePatrolAsync(int patrolId, string newName);

    /// <summary>Deletes patrol. Blocked if members present. Emblem badge deleted only if no awards reference it.</summary>
    Task<(bool Success, string ErrorMessage)> DeletePatrolAsync(int patrolId);

    /// <summary>Assigns girl to patrol. Validates section match and resets role to Member.</summary>
    Task<(bool Success, string ErrorMessage)> AssignGirlToPatrolAsync(string membershipNumber, int patrolId);

    /// <summary>Clears patrol + role from the girl.</summary>
    Task<(bool Success, string ErrorMessage)> RemoveGirlFromPatrolAsync(string membershipNumber);

    /// <summary>Sets role on girl. Demotes existing Leader/Seconder of the same patrol to Member first.</summary>
    Task<(bool Success, string ErrorMessage)> SetRoleAsync(string membershipNumber, PatrolRole role);

    /// <summary>BadgeDefinitionId for the seeded role badge matching (section, role). Returns 0 if role is Member.</summary>
    Task<int> GetRoleBadgeDefinitionIdAsync(Section section, PatrolRole role);

    /// <summary>
    /// Awards due arising from patrols: emblems for members who haven't received them,
    /// and role badges for current Leaders/Seconders who haven't received them.
    /// </summary>
    Task<List<AwardDue>> GetPatrolAwardsDueAsync();
}
