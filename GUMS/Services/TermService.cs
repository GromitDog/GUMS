using GUMS.Data;
using GUMS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

/// <summary>
/// Service for managing term configuration and operations.
/// </summary>
public class TermService : ITermService
{
    private readonly ApplicationDbContext _context;

    public TermService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<List<Term>> GetAllAsync()
    {
        return await _context.Terms
            .AsNoTracking()
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Term?> GetByIdAsync(int id)
    {
        return await _context.Terms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <inheritdoc/>
    public async Task<Term?> GetCurrentTermAsync()
    {
        var today = DateTime.Today;
        return await _context.Terms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.StartDate <= today && t.EndDate >= today);
    }

    /// <inheritdoc/>
    public async Task<List<Term>> GetFutureTermsAsync()
    {
        var today = DateTime.Today;
        return await _context.Terms
            .AsNoTracking()
            .Where(t => t.StartDate > today)
            .OrderBy(t => t.StartDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Term>> GetPastTermsAsync()
    {
        var today = DateTime.Today;
        return await _context.Terms
            .AsNoTracking()
            .Where(t => t.EndDate < today)
            .OrderByDescending(t => t.EndDate)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> CreateAsync(Term term)
    {
        // Validate basic rules
        if (term.EndDate <= term.StartDate)
        {
            return (false, "End date must be after start date.");
        }

        if (term.SubsAmount < 0)
        {
            return (false, "Subscription amount cannot be negative.");
        }

        // Validate no overlap with existing terms
        if (!await ValidateNoOverlapAsync(term))
        {
            return (false, "This term overlaps with an existing term. Please choose different dates.");
        }

        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> UpdateAsync(Term term)
    {
        var existingTerm = await _context.Terms.FindAsync(term.Id);
        if (existingTerm == null)
        {
            return (false, "Term not found.");
        }

        // Validate basic rules
        if (term.EndDate <= term.StartDate)
        {
            return (false, "End date must be after start date.");
        }

        if (term.SubsAmount < 0)
        {
            return (false, "Subscription amount cannot be negative.");
        }

        // Validate no overlap with other terms (excluding this one)
        if (!await ValidateNoOverlapAsync(term, term.Id))
        {
            return (false, "This term overlaps with an existing term. Please choose different dates.");
        }

        // Update properties
        existingTerm.Name = term.Name;
        existingTerm.StartDate = term.StartDate;
        existingTerm.EndDate = term.EndDate;
        existingTerm.SubsAmount = term.SubsAmount;

        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, string ErrorMessage)> DeleteAsync(int id)
    {
        var term = await _context.Terms.FindAsync(id);
        if (term == null)
        {
            return (false, "Term not found.");
        }

        // Check if any meetings exist within this term's date range
        var hasMeetings = await _context.Meetings
            .AnyAsync(m => m.Date >= term.StartDate && m.Date <= term.EndDate);

        if (hasMeetings)
        {
            return (false, "Cannot delete this term because meetings exist within its date range. Please delete the meetings first.");
        }

        // Check if any payments are linked to this term
        var hasPayments = await _context.Payments
            .AnyAsync(p => p.TermId == id);

        if (hasPayments)
        {
            return (false, "Cannot delete this term because payments are linked to it.");
        }

        _context.Terms.Remove(term);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateNoOverlapAsync(Term term, int? excludeTermId = null)
    {
        // A term overlaps if:
        // 1. Its start date is within another term's range, OR
        // 2. Its end date is within another term's range, OR
        // 3. It completely contains another term

        var query = _context.Terms.AsQueryable();

        // Exclude the term being updated
        if (excludeTermId.HasValue)
        {
            query = query.Where(t => t.Id != excludeTermId.Value);
        }

        var overlaps = await query.AnyAsync(t =>
            // New term starts within existing term
            (term.StartDate >= t.StartDate && term.StartDate <= t.EndDate) ||
            // New term ends within existing term
            (term.EndDate >= t.StartDate && term.EndDate <= t.EndDate) ||
            // New term completely contains existing term
            (term.StartDate <= t.StartDate && term.EndDate >= t.EndDate)
        );

        return !overlaps;
    }
}
