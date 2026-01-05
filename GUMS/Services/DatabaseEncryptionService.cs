using System.Security.Cryptography;
using System.Text;

namespace GUMS.Services;

/// <summary>
/// Manages database encryption keys using Windows Data Protection API (DPAPI)
/// to securely store the encryption key on the local machine.
/// </summary>
public class DatabaseEncryptionService : IDatabaseEncryptionService
{
    private readonly string _keyFilePath;
    private readonly ILogger<DatabaseEncryptionService> _logger;

    public DatabaseEncryptionService(ILogger<DatabaseEncryptionService> logger)
    {
        _logger = logger;

        // Store the encrypted key file in the same directory as the database
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GUMS");

        Directory.CreateDirectory(appDataPath);
        _keyFilePath = Path.Combine(appDataPath, ".dbkey");
    }

    public string GetOrCreateEncryptionKey()
    {
        if (HasEncryptionKey())
        {
            return ReadEncryptedKey();
        }

        return CreateAndStoreEncryptionKey();
    }

    public bool HasEncryptionKey()
    {
        return File.Exists(_keyFilePath);
    }

    private string CreateAndStoreEncryptionKey()
    {
        _logger.LogInformation("Generating new database encryption key");

        // Generate a strong random key (256 bits = 32 bytes)
        var keyBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }

        // Convert to base64 for use as SQLite password
        var key = Convert.ToBase64String(keyBytes);

        // Encrypt the key using DPAPI (Windows Data Protection API)
        // This binds the key to the current Windows user account
        var keyBytesToProtect = Encoding.UTF8.GetBytes(key);
        var encryptedKeyBytes = ProtectedData.Protect(
            keyBytesToProtect,
            null, // Optional additional entropy
            DataProtectionScope.CurrentUser // Only this Windows user can decrypt
        );

        // Store the encrypted key
        File.WriteAllBytes(_keyFilePath, encryptedKeyBytes);

        _logger.LogInformation("Database encryption key generated and stored securely at {KeyPath}", _keyFilePath);

        return key;
    }

    private string ReadEncryptedKey()
    {
        try
        {
            // Read the encrypted key file
            var encryptedKeyBytes = File.ReadAllBytes(_keyFilePath);

            // Decrypt using DPAPI (must be same Windows user who encrypted it)
            var decryptedKeyBytes = ProtectedData.Unprotect(
                encryptedKeyBytes,
                null,
                DataProtectionScope.CurrentUser
            );

            var key = Encoding.UTF8.GetString(decryptedKeyBytes);

            _logger.LogDebug("Database encryption key retrieved successfully");

            return key;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt database key. The key may have been created by a different user.");
            throw new InvalidOperationException(
                "Unable to decrypt the database encryption key. This application may have been configured by a different Windows user account.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading database encryption key");
            throw;
        }
    }
}
