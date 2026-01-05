using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ApplicationDbContext _context;
    private UnitConfiguration? _cachedConfiguration;

    public ConfigurationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UnitConfiguration> GetConfigurationAsync()
    {
        // Return cached configuration if available
        if (_cachedConfiguration != null)
        {
            return _cachedConfiguration;
        }

        // Get from database (should only be one row)
        _cachedConfiguration = await _context.UnitConfigurations.FirstOrDefaultAsync();

        // If no configuration exists, create default
        if (_cachedConfiguration == null)
        {
            await EnsureDefaultConfigurationAsync();
            _cachedConfiguration = await _context.UnitConfigurations.FirstAsync();
        }

        return _cachedConfiguration;
    }

    public async Task<UnitConfiguration> UpdateConfigurationAsync(UnitConfiguration configuration)
    {
        var existing = await _context.UnitConfigurations.FirstOrDefaultAsync();

        if (existing == null)
        {
            // No existing configuration, add new
            _context.UnitConfigurations.Add(configuration);
        }
        else
        {
            // Update existing configuration
            existing.UnitName = configuration.UnitName;
            existing.UnitType = configuration.UnitType;
            existing.MeetingDayOfWeek = configuration.MeetingDayOfWeek;
            existing.DefaultMeetingStartTime = configuration.DefaultMeetingStartTime;
            existing.DefaultMeetingEndTime = configuration.DefaultMeetingEndTime;
            existing.DefaultLocationName = configuration.DefaultLocationName;
            existing.DefaultLocationAddress = configuration.DefaultLocationAddress;
            existing.DefaultSubsAmount = configuration.DefaultSubsAmount;
            existing.PaymentTermDays = configuration.PaymentTermDays;

            _context.UnitConfigurations.Update(existing);
        }

        await _context.SaveChangesAsync();

        // Clear cache to force reload
        _cachedConfiguration = null;

        return await GetConfigurationAsync();
    }

    public async Task<bool> IsConfiguredAsync()
    {
        return await _context.UnitConfigurations.AnyAsync();
    }

    public async Task EnsureDefaultConfigurationAsync()
    {
        if (await IsConfiguredAsync())
        {
            return; // Already configured
        }

        var defaultConfig = new UnitConfiguration
        {
            UnitName = "My Unit",
            UnitType = Section.Brownie,
            MeetingDayOfWeek = DayOfWeek.Monday,
            DefaultMeetingStartTime = new TimeOnly(18, 30), // 6:30 PM
            DefaultMeetingEndTime = new TimeOnly(19, 45),   // 7:45 PM
            DefaultLocationName = "Village Hall",
            DefaultLocationAddress = null,
            DefaultSubsAmount = 20.00m,
            PaymentTermDays = 14
        };

        _context.UnitConfigurations.Add(defaultConfig);
        await _context.SaveChangesAsync();
    }
}
