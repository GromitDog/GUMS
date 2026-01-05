using FluentAssertions;
using GUMS.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GUMS.Tests.Services;

public class DatabaseEncryptionServiceTests : IDisposable
{
    private readonly Mock<ILogger<DatabaseEncryptionService>> _mockLogger;
    private readonly DatabaseEncryptionService _sut; // System Under Test
    private readonly string _testKeyFilePath;

    public DatabaseEncryptionServiceTests()
    {
        _mockLogger = new Mock<ILogger<DatabaseEncryptionService>>();
        _sut = new DatabaseEncryptionService(_mockLogger.Object);

        // Get the key file path that the service will use
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GUMS");
        _testKeyFilePath = Path.Combine(appDataPath, ".dbkey");
    }

    public void Dispose()
    {
        // Clean up test key file if it exists
        if (File.Exists(_testKeyFilePath))
        {
            try
            {
                File.Delete(_testKeyFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region HasEncryptionKey Tests

    [Fact]
    public void HasEncryptionKey_ShouldReturnFalse_WhenKeyFileDoesNotExist()
    {
        // Arrange - Ensure key file doesn't exist
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act
        var result = _sut.HasEncryptionKey();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasEncryptionKey_ShouldReturnTrue_WhenKeyFileExists()
    {
        // Arrange - Create a key first
        var key = _sut.GetOrCreateEncryptionKey();

        // Act
        var result = _sut.HasEncryptionKey();

        // Assert
        result.Should().BeTrue();
        File.Exists(_testKeyFilePath).Should().BeTrue();
    }

    #endregion

    #region GetOrCreateEncryptionKey Tests

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldGenerateNewKey_WhenKeyDoesNotExist()
    {
        // Arrange - Ensure no key exists
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act
        var key = _sut.GetOrCreateEncryptionKey();

        // Assert
        key.Should().NotBeNullOrWhiteSpace();
        key.Length.Should().BeGreaterThan(0);

        // Should be base64 encoded (only contains valid base64 characters)
        var isBase64 = IsValidBase64String(key);
        isBase64.Should().BeTrue("the key should be base64 encoded");

        // Key file should now exist
        File.Exists(_testKeyFilePath).Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldCreateKeyFile_WhenKeyDoesNotExist()
    {
        // Arrange - Ensure no key exists
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act
        _sut.GetOrCreateEncryptionKey();

        // Assert
        File.Exists(_testKeyFilePath).Should().BeTrue();

        // Key file should have content
        var fileInfo = new FileInfo(_testKeyFilePath);
        fileInfo.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldReturnSameKey_WhenCalledMultipleTimes()
    {
        // Arrange - Ensure no key exists
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act
        var key1 = _sut.GetOrCreateEncryptionKey();
        var key2 = _sut.GetOrCreateEncryptionKey();
        var key3 = _sut.GetOrCreateEncryptionKey();

        // Assert
        key1.Should().Be(key2);
        key2.Should().Be(key3);
        key1.Should().Be(key3);
    }

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldGenerateDifferentKeys_ForDifferentInstances()
    {
        // Arrange - Start fresh
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act - Create first key
        var key1 = _sut.GetOrCreateEncryptionKey();

        // Delete key file to simulate new instance
        File.Delete(_testKeyFilePath);

        // Create second key
        var key2 = _sut.GetOrCreateEncryptionKey();

        // Assert - Keys should be different (random generation)
        key1.Should().NotBe(key2, "each generated key should be unique");
    }

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldGenerateKeyOfCorrectLength()
    {
        // Arrange - Ensure no key exists
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act
        var key = _sut.GetOrCreateEncryptionKey();
        var keyBytes = Convert.FromBase64String(key);

        // Assert
        // Should be 256 bits = 32 bytes
        keyBytes.Length.Should().Be(32, "encryption key should be 256 bits (32 bytes)");
    }

    #endregion

    #region Key Persistence Tests

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldPersistKeyAcrossSessions()
    {
        // Arrange - Clean start
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act - Create key with first service instance
        var mockLogger1 = new Mock<ILogger<DatabaseEncryptionService>>();
        var service1 = new DatabaseEncryptionService(mockLogger1.Object);
        var key1 = service1.GetOrCreateEncryptionKey();

        // Create a new service instance (simulating app restart)
        var mockLogger2 = new Mock<ILogger<DatabaseEncryptionService>>();
        var service2 = new DatabaseEncryptionService(mockLogger2.Object);
        var key2 = service2.GetOrCreateEncryptionKey();

        // Assert - Same key should be retrieved
        key2.Should().Be(key1, "the persisted key should be retrievable across service instances");
    }

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldEncryptKeyFile()
    {
        // Arrange - Clean start
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act
        var key = _sut.GetOrCreateEncryptionKey();
        var keyFileBytes = File.ReadAllBytes(_testKeyFilePath);
        var keyFileAsString = System.Text.Encoding.UTF8.GetString(keyFileBytes);

        // Assert - The key file should NOT contain the plain text key
        // (It should be encrypted with DPAPI)
        keyFileAsString.Should().NotContain(key,
            "the key file should be encrypted, not plain text");

        // The encrypted file should not be valid base64 text
        var isPlainBase64 = IsValidBase64String(keyFileAsString);
        isPlainBase64.Should().BeFalse(
            "the key file should be binary encrypted data, not base64 text");
    }

    #endregion

    #region Security Tests

    [Fact]
    public void GetOrCreateEncryptionKey_ShouldGenerateStrongRandomKey()
    {
        // Arrange - Clean start
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Act - Generate key
        var key = _sut.GetOrCreateEncryptionKey();
        var keyBytes = Convert.FromBase64String(key);

        // Assert - Check for randomness (no all-zeros, no repeating patterns)
        var allZeros = keyBytes.All(b => b == 0);
        allZeros.Should().BeFalse("key should not be all zeros");

        var allSame = keyBytes.All(b => b == keyBytes[0]);
        allSame.Should().BeFalse("key should not have all same bytes");

        // Check entropy (should have good distribution of byte values)
        var uniqueBytes = keyBytes.Distinct().Count();
        uniqueBytes.Should().BeGreaterThan(20,
            "key should have good entropy with many different byte values");
    }

    #endregion

    #region Directory Creation Tests

    [Fact]
    public void DatabaseEncryptionService_ShouldCreateAppDataDirectory_IfNotExists()
    {
        // Arrange
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GUMS");

        // Act - Creating service should ensure directory exists
        var mockLogger = new Mock<ILogger<DatabaseEncryptionService>>();
        var service = new DatabaseEncryptionService(mockLogger.Object);

        // Assert
        Directory.Exists(appDataPath).Should().BeTrue(
            "service should create GUMS directory in AppData if it doesn't exist");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void DatabaseEncryptionService_ShouldHandleCompleteWorkflow()
    {
        // Clean start
        if (File.Exists(_testKeyFilePath))
        {
            File.Delete(_testKeyFilePath);
        }

        // Step 1: Check no key exists
        var hasKey1 = _sut.HasEncryptionKey();
        hasKey1.Should().BeFalse();

        // Step 2: Generate key
        var key1 = _sut.GetOrCreateEncryptionKey();
        key1.Should().NotBeNullOrWhiteSpace();

        // Step 3: Verify key exists now
        var hasKey2 = _sut.HasEncryptionKey();
        hasKey2.Should().BeTrue();

        // Step 4: Get key again (should be same)
        var key2 = _sut.GetOrCreateEncryptionKey();
        key2.Should().Be(key1);

        // Step 5: Simulate app restart with new service instance
        var mockLogger2 = new Mock<ILogger<DatabaseEncryptionService>>();
        var service2 = new DatabaseEncryptionService(mockLogger2.Object);

        // Step 6: Verify key persists
        var hasKey3 = service2.HasEncryptionKey();
        hasKey3.Should().BeTrue();

        var key3 = service2.GetOrCreateEncryptionKey();
        key3.Should().Be(key1);
    }

    #endregion

    #region Helper Methods

    private static bool IsValidBase64String(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;

        try
        {
            Convert.FromBase64String(s);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
