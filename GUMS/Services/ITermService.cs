using GUMS.Data.Entities;

namespace GUMS.Services;

/// <summary>
/// Service for managing term configuration and operations.
/// </summary>
public interface ITermService
{
    /// <summary>
    /// Gets all terms ordered by start date (most recent first).
    /// </summary>
    Task<List<Term>> GetAllAsync();

    /// <summary>
    /// Gets a term by its ID.
    /// </summary>
    Task<Term?> GetByIdAsync(int id);

    /// <summary>
    /// Gets the current term based on today's date.
    /// Returns null if no term is active.
    /// </summary>
    Task<Term?> GetCurrentTermAsync();

    /// <summary>
    /// Gets future terms (start date after today).
    /// </summary>
    Task<List<Term>> GetFutureTermsAsync();

    /// <summary>
    /// Gets past terms (end date before today).
    /// </summary>
    Task<List<Term>> GetPastTermsAsync();

    /// <summary>
    /// Creates a new term after validating dates don't overlap with existing terms.
    /// </summary>
    /// <param name="term">The term to create.</param>
    /// <returns>True if created successfully, false if validation fails.</returns>
    Task<(bool Success, string ErrorMessage)> CreateAsync(Term term);

    /// <summary>
    /// Updates an existing term after validating dates don't overlap.
    /// </summary>
    /// <param name="term">The term to update.</param>
    /// <returns>True if updated successfully, false if validation fails.</returns>
    Task<(bool Success, string ErrorMessage)> UpdateAsync(Term term);

    /// <summary>
    /// Deletes a term if it has no associated meetings.
    /// </summary>
    /// <param name="id">The ID of the term to delete.</param>
    /// <returns>True if deleted successfully, false if term has meetings or doesn't exist.</returns>
    Task<(bool Success, string ErrorMessage)> DeleteAsync(int id);

    /// <summary>
    /// Validates that a term's dates don't overlap with existing terms.
    /// </summary>
    /// <param name="term">The term to validate.</param>
    /// <param name="excludeTermId">Optional term ID to exclude from overlap check (for updates).</param>
    /// <returns>True if valid, false if dates overlap.</returns>
    Task<bool> ValidateNoOverlapAsync(Term term, int? excludeTermId = null);
}
