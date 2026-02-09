using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class BadgeService : IBadgeService
{
    private readonly ApplicationDbContext _context;

    public BadgeService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ===== Badge Definition CRUD =====

    public async Task<List<BadgeDefinition>> GetAllBadgesAsync()
    {
        return await _context.BadgeDefinitions
            .Include(b => b.Clauses.OrderBy(c => c.SortOrder))
            .AsNoTracking()
            .OrderBy(b => b.Section)
            .ThenBy(b => b.Theme)
            .ThenBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<BadgeDefinition?> GetBadgeByIdAsync(int id)
    {
        return await _context.BadgeDefinitions
            .Include(b => b.Clauses.OrderBy(c => c.SortOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<List<BadgeDefinition>> GetBadgesByFilterAsync(Theme? theme = null, Section? section = null, BadgeType? badgeType = null)
    {
        var query = _context.BadgeDefinitions
            .Include(b => b.Clauses.OrderBy(c => c.SortOrder))
            .AsNoTracking()
            .AsQueryable();

        if (theme.HasValue)
            query = query.Where(b => b.Theme == theme.Value);
        if (section.HasValue)
            query = query.Where(b => b.Section == section.Value);
        if (badgeType.HasValue)
            query = query.Where(b => b.BadgeType == badgeType.Value);

        return await query
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage, BadgeDefinition? Badge)> CreateBadgeAsync(BadgeDefinition badge)
    {
        if (string.IsNullOrWhiteSpace(badge.Name))
            return (false, "Badge name is required.", null);

        if (badge.BadgeType == BadgeType.FunBadge)
        {
            badge.RequiredCompletions = 1;
            badge.Clauses = new List<BadgeClause>
            {
                new() { Name = badge.Name, SortOrder = 0 }
            };
        }
        else if (badge.BadgeType == BadgeType.SkillsBuilder)
        {
            badge.RequiredCompletions = badge.RequiredCompletions <= 0 ? 4 : badge.RequiredCompletions;
        }
        else
        {
            badge.RequiredCompletions = badge.RequiredCompletions <= 0 ? 3 : badge.RequiredCompletions;
        }

        for (int i = 0; i < badge.Clauses.Count; i++)
            badge.Clauses[i].SortOrder = i;

        _context.BadgeDefinitions.Add(badge);
        await _context.SaveChangesAsync();

        return (true, string.Empty, badge);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateBadgeAsync(BadgeDefinition badge)
    {
        var existing = await _context.BadgeDefinitions.FindAsync(badge.Id);
        if (existing == null)
            return (false, "Badge not found.");

        existing.Name = badge.Name;
        existing.Theme = badge.Theme;
        existing.BadgeType = badge.BadgeType;
        existing.Section = badge.Section;
        existing.Stage = badge.Stage;
        existing.SkillsBuilderName = badge.SkillsBuilderName;
        existing.RequiredCompletions = badge.RequiredCompletions;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteBadgeAsync(int id)
    {
        var badge = await _context.BadgeDefinitions
            .Include(b => b.Clauses)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (badge == null)
            return (false, "Badge not found.");

        // Check if any clauses are linked to meeting activities
        var clauseIds = badge.Clauses.Select(c => c.Id).ToList();
        var inUse = await _context.MeetingActivities
            .AnyAsync(a => a.BadgeClauseId.HasValue && clauseIds.Contains(a.BadgeClauseId.Value));

        if (inUse)
            return (false, "Cannot delete this badge because its clauses are linked to meeting activities.");

        _context.BadgeDefinitions.Remove(badge);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ===== Badge Clause CRUD =====

    public async Task<List<BadgeClause>> GetClausesForBadgeAsync(int badgeDefinitionId)
    {
        return await _context.BadgeClauses
            .AsNoTracking()
            .Where(c => c.BadgeDefinitionId == badgeDefinitionId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage, BadgeClause? Clause)> AddClauseAsync(BadgeClause clause)
    {
        if (string.IsNullOrWhiteSpace(clause.Name))
            return (false, "Clause name is required.", null);

        var badgeExists = await _context.BadgeDefinitions.AnyAsync(b => b.Id == clause.BadgeDefinitionId);
        if (!badgeExists)
            return (false, "Badge not found.", null);

        var maxSort = await _context.BadgeClauses
            .Where(c => c.BadgeDefinitionId == clause.BadgeDefinitionId)
            .MaxAsync(c => (int?)c.SortOrder) ?? -1;
        clause.SortOrder = maxSort + 1;

        _context.BadgeClauses.Add(clause);
        await _context.SaveChangesAsync();

        return (true, string.Empty, clause);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateClauseAsync(BadgeClause clause)
    {
        var existing = await _context.BadgeClauses.FindAsync(clause.Id);
        if (existing == null)
            return (false, "Clause not found.");

        existing.Name = clause.Name;
        existing.Description = clause.Description;
        existing.SortOrder = clause.SortOrder;
        existing.EstimatedMinutes = clause.EstimatedMinutes;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteClauseAsync(int clauseId)
    {
        var clause = await _context.BadgeClauses.FindAsync(clauseId);
        if (clause == null)
            return (false, "Clause not found.");

        var inUse = await _context.MeetingActivities
            .AnyAsync(a => a.BadgeClauseId == clauseId);
        if (inUse)
            return (false, "Cannot delete this clause because it is linked to meeting activities.");

        _context.BadgeClauses.Remove(clause);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ===== UMA Definition CRUD =====

    public async Task<List<UmaDefinition>> GetAllUmasAsync()
    {
        return await _context.UmaDefinitions
            .AsNoTracking()
            .OrderBy(u => u.Theme)
            .ThenBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<UmaDefinition?> GetUmaByIdAsync(int id)
    {
        return await _context.UmaDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<UmaDefinition>> GetUmasByThemeAsync(Theme theme)
    {
        return await _context.UmaDefinitions
            .AsNoTracking()
            .Where(u => u.Theme == theme)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<(bool Success, string ErrorMessage, UmaDefinition? Uma)> CreateUmaAsync(UmaDefinition uma)
    {
        if (string.IsNullOrWhiteSpace(uma.Name))
            return (false, "UMA name is required.", null);

        if (uma.Minutes <= 0)
            return (false, "Minutes must be greater than zero.", null);

        _context.UmaDefinitions.Add(uma);
        await _context.SaveChangesAsync();

        return (true, string.Empty, uma);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateUmaAsync(UmaDefinition uma)
    {
        var existing = await _context.UmaDefinitions.FindAsync(uma.Id);
        if (existing == null)
            return (false, "UMA not found.");

        existing.Name = uma.Name;
        existing.Description = uma.Description;
        existing.Theme = uma.Theme;
        existing.Minutes = uma.Minutes;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteUmaAsync(int id)
    {
        var uma = await _context.UmaDefinitions.FindAsync(id);
        if (uma == null)
            return (false, "UMA not found.");

        var inUse = await _context.MeetingActivities
            .AnyAsync(a => a.UmaDefinitionId == id);
        if (inUse)
            return (false, "Cannot delete this UMA because it is linked to meeting activities.");

        _context.UmaDefinitions.Remove(uma);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ===== Search =====

    public async Task<List<BadgeClause>> SearchClausesAsync(string searchTerm, Section? section = null)
    {
        var query = _context.BadgeClauses
            .Include(c => c.BadgeDefinition)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Description != null && c.Description.ToLower().Contains(term)) ||
                c.BadgeDefinition.Name.ToLower().Contains(term));
        }

        if (section.HasValue)
            query = query.Where(c => c.BadgeDefinition.Section == section.Value);

        return await query
            .OrderBy(c => c.BadgeDefinition.Name)
            .ThenBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task<List<UmaDefinition>> SearchUmasAsync(string searchTerm)
    {
        var query = _context.UmaDefinitions
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(u =>
                u.Name.ToLower().Contains(term) ||
                (u.Description != null && u.Description.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(u => u.Theme)
            .ThenBy(u => u.Name)
            .ToListAsync();
    }
}
