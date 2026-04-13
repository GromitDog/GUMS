using GUMS.Data;
using GUMS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class CostCentreService : ICostCentreService
{
    private readonly ApplicationDbContext _context;

    public CostCentreService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CostCentre>> GetAllAsync(bool activeOnly = true)
    {
        var query = _context.CostCentres.AsNoTracking();

        if (activeOnly)
            query = query.Where(cc => cc.IsActive);

        return await query.OrderBy(cc => cc.Name).ToListAsync();
    }

    public async Task<CostCentre?> GetByIdAsync(int id)
    {
        return await _context.CostCentres
            .AsNoTracking()
            .FirstOrDefaultAsync(cc => cc.Id == id);
    }

    public async Task<(bool Success, string ErrorMessage, CostCentre? CostCentre)> CreateAsync(string name)
    {
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Name is required.", null);

        var exists = await _context.CostCentres
            .AnyAsync(cc => cc.Name == name);

        if (exists)
            return (false, $"A cost centre named '{name}' already exists.", null);

        var costCentre = new CostCentre { Name = name };
        _context.CostCentres.Add(costCentre);
        await _context.SaveChangesAsync();

        return (true, string.Empty, costCentre);
    }

    public async Task<(bool Success, string ErrorMessage)> UpdateAsync(int id, string name)
    {
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Name is required.");

        var costCentre = await _context.CostCentres.FindAsync(id);
        if (costCentre == null)
            return (false, "Cost centre not found.");

        var duplicate = await _context.CostCentres
            .AnyAsync(cc => cc.Name == name && cc.Id != id);

        if (duplicate)
            return (false, $"A cost centre named '{name}' already exists.");

        costCentre.Name = name;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> DeactivateAsync(int id)
    {
        var costCentre = await _context.CostCentres.FindAsync(id);
        if (costCentre == null)
            return (false, "Cost centre not found.");

        costCentre.IsActive = false;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> ReactivateAsync(int id)
    {
        var costCentre = await _context.CostCentres.FindAsync(id);
        if (costCentre == null)
            return (false, "Cost centre not found.");

        costCentre.IsActive = true;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<int> GetUsageCountAsync(int id)
    {
        return await _context.TransactionLines
            .CountAsync(tl => tl.CostCentreId == id);
    }
}
