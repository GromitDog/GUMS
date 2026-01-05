using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Tests.Services;

public class ConfigurationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ConfigurationService _sut; // System Under Test

    public ConfigurationServiceTests()
    {
        // Arrange - Create in-memory database for each test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _sut = new ConfigurationService(_context);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region GetConfigurationAsync Tests

    [Fact]
    public async Task GetConfigurationAsync_ShouldReturnDefaultConfiguration_WhenNoneExists()
    {
        // Act
        var result = await _sut.GetConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.UnitName.Should().Be("My Unit");
        result.UnitType.Should().Be(Section.Brownie);
        result.MeetingDayOfWeek.Should().Be(DayOfWeek.Monday);
        result.DefaultMeetingStartTime.Should().Be(new TimeOnly(18, 30));
        result.DefaultMeetingEndTime.Should().Be(new TimeOnly(19, 45));
        result.DefaultLocationName.Should().Be("Village Hall");
        result.DefaultSubsAmount.Should().Be(20.00m);
        result.PaymentTermDays.Should().Be(14);
    }

    [Fact]
    public async Task GetConfigurationAsync_ShouldReturnCachedValue_OnSecondCall()
    {
        // Arrange
        var firstCall = await _sut.GetConfigurationAsync();

        // Act - Modify database directly to verify cache is being used
        var dbConfig = await _context.UnitConfigurations.FirstAsync();
        dbConfig.UnitName = "Modified In Database";
        await _context.SaveChangesAsync();

        var secondCall = await _sut.GetConfigurationAsync();

        // Assert - Second call should return cached value, not modified database value
        secondCall.UnitName.Should().Be("My Unit"); // Original cached value
        secondCall.Should().BeSameAs(firstCall); // Same object reference
    }

    [Fact]
    public async Task GetConfigurationAsync_ShouldReturnExistingConfiguration_WhenOneExists()
    {
        // Arrange
        var existingConfig = new UnitConfiguration
        {
            UnitName = "Test Unit",
            UnitType = Section.Guide,
            MeetingDayOfWeek = DayOfWeek.Wednesday,
            DefaultMeetingStartTime = new TimeOnly(19, 0),
            DefaultMeetingEndTime = new TimeOnly(20, 30),
            DefaultLocationName = "Community Centre",
            DefaultLocationAddress = "123 Test Street",
            DefaultSubsAmount = 25.00m,
            PaymentTermDays = 21
        };
        _context.UnitConfigurations.Add(existingConfig);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.UnitName.Should().Be("Test Unit");
        result.UnitType.Should().Be(Section.Guide);
        result.MeetingDayOfWeek.Should().Be(DayOfWeek.Wednesday);
        result.DefaultLocationName.Should().Be("Community Centre");
        result.DefaultSubsAmount.Should().Be(25.00m);
    }

    #endregion

    #region UpdateConfigurationAsync Tests

    [Fact]
    public async Task UpdateConfigurationAsync_ShouldAddNewConfiguration_WhenNoneExists()
    {
        // Arrange
        var newConfig = new UnitConfiguration
        {
            UnitName = "New Unit",
            UnitType = Section.Rainbow,
            MeetingDayOfWeek = DayOfWeek.Tuesday,
            DefaultMeetingStartTime = new TimeOnly(16, 0),
            DefaultMeetingEndTime = new TimeOnly(17, 15),
            DefaultLocationName = "School Hall",
            DefaultLocationAddress = "456 New Road",
            DefaultSubsAmount = 15.00m,
            PaymentTermDays = 7
        };

        // Act
        var result = await _sut.UpdateConfigurationAsync(newConfig);

        // Assert
        result.Should().NotBeNull();
        result.UnitName.Should().Be("New Unit");
        result.UnitType.Should().Be(Section.Rainbow);
        result.DefaultSubsAmount.Should().Be(15.00m);

        // Verify it was saved to database
        var dbConfig = await _context.UnitConfigurations.FirstAsync();
        dbConfig.UnitName.Should().Be("New Unit");
        dbConfig.PaymentTermDays.Should().Be(7);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_ShouldUpdateExistingConfiguration_WhenOneExists()
    {
        // Arrange - Create initial configuration
        var initialConfig = new UnitConfiguration
        {
            UnitName = "Initial Unit",
            UnitType = Section.Brownie,
            MeetingDayOfWeek = DayOfWeek.Monday,
            DefaultMeetingStartTime = new TimeOnly(18, 0),
            DefaultMeetingEndTime = new TimeOnly(19, 0),
            DefaultLocationName = "Old Hall",
            DefaultLocationAddress = "Old Address",
            DefaultSubsAmount = 10.00m,
            PaymentTermDays = 10
        };
        _context.UnitConfigurations.Add(initialConfig);
        await _context.SaveChangesAsync();
        var initialId = initialConfig.Id;

        // Act - Update with new values
        var updatedConfig = new UnitConfiguration
        {
            UnitName = "Updated Unit",
            UnitType = Section.Guide,
            MeetingDayOfWeek = DayOfWeek.Friday,
            DefaultMeetingStartTime = new TimeOnly(19, 30),
            DefaultMeetingEndTime = new TimeOnly(21, 0),
            DefaultLocationName = "New Hall",
            DefaultLocationAddress = "New Address",
            DefaultSubsAmount = 30.00m,
            PaymentTermDays = 28
        };

        var result = await _sut.UpdateConfigurationAsync(updatedConfig);

        // Assert
        result.Should().NotBeNull();
        result.UnitName.Should().Be("Updated Unit");
        result.UnitType.Should().Be(Section.Guide);
        result.MeetingDayOfWeek.Should().Be(DayOfWeek.Friday);
        result.DefaultMeetingStartTime.Should().Be(new TimeOnly(19, 30));
        result.DefaultMeetingEndTime.Should().Be(new TimeOnly(21, 0));
        result.DefaultLocationName.Should().Be("New Hall");
        result.DefaultLocationAddress.Should().Be("New Address");
        result.DefaultSubsAmount.Should().Be(30.00m);
        result.PaymentTermDays.Should().Be(28);

        // Verify only one configuration exists in database
        var configCount = await _context.UnitConfigurations.CountAsync();
        configCount.Should().Be(1);

        // Verify the existing record was updated (same ID)
        var dbConfig = await _context.UnitConfigurations.FirstAsync();
        dbConfig.Id.Should().Be(initialId);
        dbConfig.UnitName.Should().Be("Updated Unit");
    }

    [Fact]
    public async Task UpdateConfigurationAsync_ShouldClearCache_AfterUpdate()
    {
        // Arrange - Get configuration to populate cache
        var initialConfig = await _sut.GetConfigurationAsync();
        initialConfig.UnitName.Should().Be("My Unit"); // Default value

        // Act - Update configuration
        var updatedConfig = new UnitConfiguration
        {
            UnitName = "Changed Unit",
            UnitType = Section.Ranger,
            MeetingDayOfWeek = DayOfWeek.Saturday,
            DefaultMeetingStartTime = new TimeOnly(10, 0),
            DefaultMeetingEndTime = new TimeOnly(12, 0),
            DefaultLocationName = "Scout Hut",
            DefaultSubsAmount = 35.00m,
            PaymentTermDays = 30
        };

        await _sut.UpdateConfigurationAsync(updatedConfig);

        // Get configuration again to verify cache was cleared
        var refreshedConfig = await _sut.GetConfigurationAsync();

        // Assert - Should return updated values, not cached old values
        refreshedConfig.UnitName.Should().Be("Changed Unit");
        refreshedConfig.UnitType.Should().Be(Section.Ranger);
        refreshedConfig.Should().NotBeSameAs(initialConfig); // Different object reference
    }

    #endregion

    #region IsConfiguredAsync Tests

    [Fact]
    public async Task IsConfiguredAsync_ShouldReturnFalse_WhenNoConfigurationExists()
    {
        // Act
        var result = await _sut.IsConfiguredAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsConfiguredAsync_ShouldReturnTrue_WhenConfigurationExists()
    {
        // Arrange
        var config = new UnitConfiguration
        {
            UnitName = "Test Unit",
            UnitType = Section.Brownie,
            MeetingDayOfWeek = DayOfWeek.Monday,
            DefaultMeetingStartTime = new TimeOnly(18, 0),
            DefaultMeetingEndTime = new TimeOnly(19, 0),
            DefaultLocationName = "Hall",
            DefaultSubsAmount = 20.00m,
            PaymentTermDays = 14
        };
        _context.UnitConfigurations.Add(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.IsConfiguredAsync();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region EnsureDefaultConfigurationAsync Tests

    [Fact]
    public async Task EnsureDefaultConfigurationAsync_ShouldCreateDefaultConfiguration_WhenNoneExists()
    {
        // Act
        await _sut.EnsureDefaultConfigurationAsync();

        // Assert
        var config = await _context.UnitConfigurations.FirstOrDefaultAsync();
        config.Should().NotBeNull();
        config!.UnitName.Should().Be("My Unit");
        config.UnitType.Should().Be(Section.Brownie);
        config.MeetingDayOfWeek.Should().Be(DayOfWeek.Monday);
        config.DefaultMeetingStartTime.Should().Be(new TimeOnly(18, 30));
        config.DefaultMeetingEndTime.Should().Be(new TimeOnly(19, 45));
        config.DefaultLocationName.Should().Be("Village Hall");
        config.DefaultLocationAddress.Should().BeNull();
        config.DefaultSubsAmount.Should().Be(20.00m);
        config.PaymentTermDays.Should().Be(14);
    }

    [Fact]
    public async Task EnsureDefaultConfigurationAsync_ShouldNotCreateDuplicate_WhenConfigurationAlreadyExists()
    {
        // Arrange - Create existing configuration
        var existingConfig = new UnitConfiguration
        {
            UnitName = "Existing Unit",
            UnitType = Section.Guide,
            MeetingDayOfWeek = DayOfWeek.Wednesday,
            DefaultMeetingStartTime = new TimeOnly(19, 0),
            DefaultMeetingEndTime = new TimeOnly(20, 30),
            DefaultLocationName = "Church Hall",
            DefaultSubsAmount = 25.00m,
            PaymentTermDays = 21
        };
        _context.UnitConfigurations.Add(existingConfig);
        await _context.SaveChangesAsync();

        // Act
        await _sut.EnsureDefaultConfigurationAsync();

        // Assert
        var configCount = await _context.UnitConfigurations.CountAsync();
        configCount.Should().Be(1); // Should not have added a second configuration

        var config = await _context.UnitConfigurations.FirstAsync();
        config.UnitName.Should().Be("Existing Unit"); // Original configuration unchanged
        config.UnitType.Should().Be(Section.Guide);
    }

    [Fact]
    public async Task EnsureDefaultConfigurationAsync_ShouldBeIdempotent_WhenCalledMultipleTimes()
    {
        // Act - Call multiple times
        await _sut.EnsureDefaultConfigurationAsync();
        await _sut.EnsureDefaultConfigurationAsync();
        await _sut.EnsureDefaultConfigurationAsync();

        // Assert - Should only create one configuration
        var configCount = await _context.UnitConfigurations.CountAsync();
        configCount.Should().Be(1);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task ConfigurationService_ShouldHandleCompleteWorkflow()
    {
        // Test complete workflow: check if configured, ensure default, get, update

        // Step 1: Check if configured (should be false initially)
        var isConfigured = await _sut.IsConfiguredAsync();
        isConfigured.Should().BeFalse();

        // Step 2: Ensure default configuration
        await _sut.EnsureDefaultConfigurationAsync();
        isConfigured = await _sut.IsConfiguredAsync();
        isConfigured.Should().BeTrue();

        // Step 3: Get configuration
        var config = await _sut.GetConfigurationAsync();
        config.Should().NotBeNull();
        config.UnitName.Should().Be("My Unit");

        // Step 4: Update configuration
        config.UnitName = "Updated Unit Name";
        config.DefaultSubsAmount = 50.00m;
        var updatedConfig = await _sut.UpdateConfigurationAsync(config);

        // Step 5: Verify update persisted
        updatedConfig.UnitName.Should().Be("Updated Unit Name");
        updatedConfig.DefaultSubsAmount.Should().Be(50.00m);

        // Step 6: Get again to verify cache was cleared
        var finalConfig = await _sut.GetConfigurationAsync();
        finalConfig.UnitName.Should().Be("Updated Unit Name");
        finalConfig.DefaultSubsAmount.Should().Be(50.00m);
    }

    #endregion
}
