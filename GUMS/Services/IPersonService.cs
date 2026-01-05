using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

public interface IPersonService
{
    // CRUD operations
    Task<Person> GetByIdAsync(int id);
    Task<Person?> GetByMembershipNumberAsync(string membershipNumber);
    Task<List<Person>> GetAllAsync();
    Task<List<Person>> GetActiveAsync();
    Task<List<Person>> GetInactiveAsync();
    Task<List<Person>> GetByTypeAsync(PersonType type, bool activeOnly = true);
    Task<List<Person>> GetBySectionAsync(Section section, bool activeOnly = true);
    Task<Person> AddAsync(Person person);
    Task<Person> UpdateAsync(Person person);
    Task DeleteAsync(int id); // Soft delete

    // Search
    Task<List<Person>> SearchAsync(string searchTerm, bool activeOnly = true);

    // Data removal (right to be forgotten)
    Task<string> ExportMemberDataAsync(int personId);
    Task RemoveMemberDataAsync(int personId, string removedBy, bool dataExported = false);

    // Validation
    Task<bool> IsMembershipNumberUniqueAsync(string membershipNumber, int? excludePersonId = null);
}
