using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

public interface IBadgeService
{
    // ===== Badge Definition CRUD =====
    Task<List<BadgeDefinition>> GetAllBadgesAsync();
    Task<BadgeDefinition?> GetBadgeByIdAsync(int id);
    Task<List<BadgeDefinition>> GetBadgesByFilterAsync(Theme? theme = null, Section? section = null, BadgeType? badgeType = null);
    Task<(bool Success, string ErrorMessage, BadgeDefinition? Badge)> CreateBadgeAsync(BadgeDefinition badge);
    Task<(bool Success, string ErrorMessage)> UpdateBadgeAsync(BadgeDefinition badge);
    Task<(bool Success, string ErrorMessage)> DeleteBadgeAsync(int id);

    // ===== Badge Clause CRUD =====
    Task<List<BadgeClause>> GetClausesForBadgeAsync(int badgeDefinitionId);
    Task<(bool Success, string ErrorMessage, BadgeClause? Clause)> AddClauseAsync(BadgeClause clause);
    Task<(bool Success, string ErrorMessage)> UpdateClauseAsync(BadgeClause clause);
    Task<(bool Success, string ErrorMessage)> DeleteClauseAsync(int clauseId);

    // ===== UMA Definition CRUD =====
    Task<List<UmaDefinition>> GetAllUmasAsync();
    Task<UmaDefinition?> GetUmaByIdAsync(int id);
    Task<List<UmaDefinition>> GetUmasByThemeAsync(Theme theme);
    Task<(bool Success, string ErrorMessage, UmaDefinition? Uma)> CreateUmaAsync(UmaDefinition uma);
    Task<(bool Success, string ErrorMessage)> UpdateUmaAsync(UmaDefinition uma);
    Task<(bool Success, string ErrorMessage)> DeleteUmaAsync(int id);

    // ===== Search =====
    Task<List<BadgeClause>> SearchClausesAsync(string searchTerm, Section? section = null);
    Task<List<UmaDefinition>> SearchUmasAsync(string searchTerm);
}
