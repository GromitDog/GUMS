using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class PatrolService : IPatrolService
{
    private readonly ApplicationDbContext _context;

    public PatrolService(ApplicationDbContext context)
    {
        _context = context;
    }

    private static readonly (Section Section, PatrolRole Role, string Name)[] RoleBadgeSeeds =
    {
        (Section.Brownie, PatrolRole.Leader, "Sixer"),
        (Section.Brownie, PatrolRole.Seconder, "Brownie Seconder"),
        (Section.Guide, PatrolRole.Leader, "Patrol Leader"),
        (Section.Guide, PatrolRole.Seconder, "Guide Seconder")
    };

    public async Task EnsureDefaultPatrolBadgesAsync()
    {
        var existing = await _context.BadgeDefinitions
            .Where(b => b.BadgeType == BadgeType.PatrolBadge)
            .Select(b => new { b.Section, b.Name })
            .ToListAsync();

        var existingSet = existing.Select(x => (x.Section, x.Name)).ToHashSet();

        foreach (var seed in RoleBadgeSeeds)
        {
            if (existingSet.Contains((seed.Section, seed.Name)))
                continue;

            _context.BadgeDefinitions.Add(new BadgeDefinition
            {
                Name = seed.Name,
                BadgeType = BadgeType.PatrolBadge,
                Section = seed.Section,
                Theme = null,
                RequiredCompletions = 0
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<Patrol>> GetPatrolsAsync(Section section)
    {
        return await _context.Patrols
            .AsNoTracking()
            .Include(p => p.EmblemBadge)
            .Include(p => p.Members.Where(m => m.IsActive && !m.IsDataRemoved))
            .Where(p => p.Section == section)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Patrol?> GetPatrolAsync(int patrolId)
    {
        return await _context.Patrols
            .AsNoTracking()
            .Include(p => p.EmblemBadge)
            .Include(p => p.Members.Where(m => m.IsActive && !m.IsDataRemoved))
            .FirstOrDefaultAsync(p => p.Id == patrolId);
    }

    public async Task<List<Person>> GetUnassignedGirlsAsync(Section section)
    {
        return await _context.Persons
            .AsNoTracking()
            .Where(p => p.PersonType == PersonType.Girl
                     && p.Section == section
                     && p.IsActive
                     && !p.IsDataRemoved
                     && p.PatrolId == null)
            .OrderBy(p => p.FullName)
            .ToListAsync();
    }

    public async Task<Dictionary<string, HashSet<int>>> GetAwardedPatrolBadgeMapAsync(Section section)
    {
        var patrolBadgeIds = await _context.BadgeDefinitions
            .AsNoTracking()
            .Where(b => b.BadgeType == BadgeType.PatrolBadge && b.Section == section)
            .Select(b => b.Id)
            .ToListAsync();

        var rows = await _context.AwardedBadges
            .AsNoTracking()
            .Where(a => patrolBadgeIds.Contains(a.BadgeDefinitionId))
            .Select(a => new { a.MembershipNumber, a.BadgeDefinitionId })
            .ToListAsync();

        return rows
            .GroupBy(r => r.MembershipNumber)
            .ToDictionary(g => g.Key, g => g.Select(r => r.BadgeDefinitionId).ToHashSet());
    }

    public async Task<(bool Success, string ErrorMessage, Patrol? Patrol)> CreatePatrolAsync(string name, Section section)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Patrol name is required.", null);

        if (section != Section.Brownie && section != Section.Guide)
            return (false, "Patrols are only available for Brownies and Guides.", null);

        var trimmed = name.Trim();
        if (trimmed.Length > 100)
            return (false, "Patrol name must be 100 characters or fewer.", null);

        var duplicate = await _context.Patrols
            .AnyAsync(p => p.Section == section && p.Name == trimmed);
        if (duplicate)
            return (false, $"A patrol named \"{trimmed}\" already exists in {section}.", null);

        var emblem = new BadgeDefinition
        {
            Name = $"{trimmed} Emblem",
            BadgeType = BadgeType.PatrolBadge,
            Section = section,
            Theme = null,
            RequiredCompletions = 0
        };
        _context.BadgeDefinitions.Add(emblem);
        await _context.SaveChangesAsync();

        var patrol = new Patrol
        {
            Name = trimmed,
            Section = section,
            EmblemBadgeDefinitionId = emblem.Id
        };
        _context.Patrols.Add(patrol);
        await _context.SaveChangesAsync();

        return (true, string.Empty, patrol);
    }

    public async Task<(bool Success, string ErrorMessage)> RenamePatrolAsync(int patrolId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return (false, "Patrol name is required.");

        var trimmed = newName.Trim();
        if (trimmed.Length > 100)
            return (false, "Patrol name must be 100 characters or fewer.");

        var patrol = await _context.Patrols
            .Include(p => p.EmblemBadge)
            .FirstOrDefaultAsync(p => p.Id == patrolId);
        if (patrol == null)
            return (false, "Patrol not found.");

        if (patrol.Name == trimmed)
            return (true, string.Empty);

        var duplicate = await _context.Patrols
            .AnyAsync(p => p.Section == patrol.Section && p.Name == trimmed && p.Id != patrolId);
        if (duplicate)
            return (false, $"A patrol named \"{trimmed}\" already exists in {patrol.Section}.");

        patrol.Name = trimmed;
        patrol.EmblemBadge.Name = $"{trimmed} Emblem";

        var stockItem = await _context.BadgeStockItems
            .FirstOrDefaultAsync(s => s.BadgeDefinitionId == patrol.EmblemBadgeDefinitionId);
        if (stockItem != null)
            stockItem.Name = $"{trimmed} Emblem";

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeletePatrolAsync(int patrolId)
    {
        var patrol = await _context.Patrols.FindAsync(patrolId);
        if (patrol == null)
            return (false, "Patrol not found.");

        var hasMembers = await _context.Persons.AnyAsync(p => p.PatrolId == patrolId);
        if (hasMembers)
            return (false, "Cannot delete a patrol that has members. Remove members first.");

        var emblemId = patrol.EmblemBadgeDefinitionId;
        var emblemAwarded = await _context.AwardedBadges.AnyAsync(a => a.BadgeDefinitionId == emblemId);

        _context.Patrols.Remove(patrol);

        if (emblemAwarded)
        {
            // Preserve history — deactivate the stock item so it disappears from active lists.
            var stockItem = await _context.BadgeStockItems
                .FirstOrDefaultAsync(s => s.BadgeDefinitionId == emblemId);
            if (stockItem != null)
                stockItem.IsActive = false;
        }
        else
        {
            var stockItem = await _context.BadgeStockItems
                .FirstOrDefaultAsync(s => s.BadgeDefinitionId == emblemId);
            if (stockItem != null)
                _context.BadgeStockItems.Remove(stockItem);

            var emblem = await _context.BadgeDefinitions.FindAsync(emblemId);
            if (emblem != null)
                _context.BadgeDefinitions.Remove(emblem);
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> AssignGirlToPatrolAsync(string membershipNumber, int patrolId)
    {
        var girl = await _context.Persons.FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);
        if (girl == null)
            return (false, "Member not found.");

        if (girl.PersonType != PersonType.Girl)
            return (false, "Only girls can be assigned to patrols.");

        var patrol = await _context.Patrols.FindAsync(patrolId);
        if (patrol == null)
            return (false, "Patrol not found.");

        if (girl.Section != patrol.Section)
            return (false, $"Member is in {girl.Section}; patrol is in {patrol.Section}.");

        girl.PatrolId = patrolId;
        girl.PatrolRole = PatrolRole.Member;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> RemoveGirlFromPatrolAsync(string membershipNumber)
    {
        var girl = await _context.Persons.FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);
        if (girl == null)
            return (false, "Member not found.");

        // Clear role before clearing patrol to satisfy the check constraint in a single SaveChanges.
        girl.PatrolRole = PatrolRole.Member;
        girl.PatrolId = null;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> SetRoleAsync(string membershipNumber, PatrolRole role)
    {
        var girl = await _context.Persons.FirstOrDefaultAsync(p => p.MembershipNumber == membershipNumber);
        if (girl == null)
            return (false, "Member not found.");

        if (role != PatrolRole.Member && girl.PatrolId == null)
            return (false, "Member must be in a patrol before a role can be assigned.");

        if (role != PatrolRole.Member)
        {
            var existing = await _context.Persons
                .Where(p => p.PatrolId == girl.PatrolId
                         && p.PatrolRole == role
                         && p.MembershipNumber != membershipNumber)
                .ToListAsync();
            foreach (var holder in existing)
                holder.PatrolRole = PatrolRole.Member;
        }

        girl.PatrolRole = role;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<int> GetRoleBadgeDefinitionIdAsync(Section section, PatrolRole role)
    {
        if (role == PatrolRole.Member)
            return 0;

        var seed = RoleBadgeSeeds.FirstOrDefault(s => s.Section == section && s.Role == role);
        if (seed.Name == null)
            return 0;

        return await _context.BadgeDefinitions
            .Where(b => b.BadgeType == BadgeType.PatrolBadge
                     && b.Section == section
                     && b.Name == seed.Name)
            .Select(b => b.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AwardDue>> GetPatrolAwardsDueAsync()
    {
        var girls = await _context.Persons
            .AsNoTracking()
            .Include(p => p.Patrol)
                .ThenInclude(pa => pa!.EmblemBadge)
            .Where(p => p.PersonType == PersonType.Girl
                     && p.IsActive
                     && !p.IsDataRemoved
                     && p.PatrolId != null)
            .ToListAsync();

        if (!girls.Any())
            return new List<AwardDue>();

        var patrolBadgeIds = await _context.BadgeDefinitions
            .AsNoTracking()
            .Where(b => b.BadgeType == BadgeType.PatrolBadge)
            .Select(b => new { b.Id, b.Section, b.Name })
            .ToListAsync();

        var roleBadgeLookup = new Dictionary<(Section, PatrolRole), (int Id, string Name)>();
        foreach (var seed in RoleBadgeSeeds)
        {
            var match = patrolBadgeIds.FirstOrDefault(b => b.Section == seed.Section && b.Name == seed.Name);
            if (match != null)
                roleBadgeLookup[(seed.Section, seed.Role)] = (match.Id, match.Name);
        }

        var membershipNumbers = girls.Select(g => g.MembershipNumber).ToList();
        var awarded = await _context.AwardedBadges
            .AsNoTracking()
            .Where(a => membershipNumbers.Contains(a.MembershipNumber))
            .Select(a => new { a.MembershipNumber, a.BadgeDefinitionId })
            .ToListAsync();

        var awardedByGirl = awarded
            .GroupBy(a => a.MembershipNumber)
            .ToDictionary(g => g.Key, g => g.Select(a => a.BadgeDefinitionId).ToHashSet());

        var results = new List<AwardDue>();

        foreach (var girl in girls)
        {
            if (girl.Patrol == null) continue;

            var awardedSet = awardedByGirl.GetValueOrDefault(girl.MembershipNumber, new HashSet<int>());

            // Emblem badge
            var emblemId = girl.Patrol.EmblemBadgeDefinitionId;
            if (!awardedSet.Contains(emblemId))
            {
                results.Add(new AwardDue
                {
                    MembershipNumber = girl.MembershipNumber,
                    Name = girl.FullName,
                    AwardName = girl.Patrol.EmblemBadge.Name,
                    AwardType = "PatrolBadge",
                    BadgeDefinitionId = emblemId
                });
            }

            // Role badge
            if (girl.PatrolRole != PatrolRole.Member
                && roleBadgeLookup.TryGetValue((girl.Patrol.Section, girl.PatrolRole), out var roleBadge)
                && !awardedSet.Contains(roleBadge.Id))
            {
                results.Add(new AwardDue
                {
                    MembershipNumber = girl.MembershipNumber,
                    Name = girl.FullName,
                    AwardName = roleBadge.Name,
                    AwardType = "PatrolBadge",
                    BadgeDefinitionId = roleBadge.Id
                });
            }
        }

        return results;
    }
}
