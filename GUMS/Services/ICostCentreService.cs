using GUMS.Data.Entities;

namespace GUMS.Services;

public interface ICostCentreService
{
    Task<List<CostCentre>> GetAllAsync(bool activeOnly = true);
    Task<CostCentre?> GetByIdAsync(int id);
    Task<(bool Success, string ErrorMessage, CostCentre? CostCentre)> CreateAsync(string name);
    Task<(bool Success, string ErrorMessage)> UpdateAsync(int id, string name);
    Task<(bool Success, string ErrorMessage)> DeactivateAsync(int id);
    Task<(bool Success, string ErrorMessage)> ReactivateAsync(int id);
    Task<int> GetUsageCountAsync(int id);
}
