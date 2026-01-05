using GUMS.Data.Entities;

namespace GUMS.Services;

public interface IConfigurationService
{
    Task<UnitConfiguration> GetConfigurationAsync();
    Task<UnitConfiguration> UpdateConfigurationAsync(UnitConfiguration configuration);
    Task<bool> IsConfiguredAsync();
    Task EnsureDefaultConfigurationAsync();
}
