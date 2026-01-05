namespace GUMS.Services;

/// <summary>
/// Service for managing database encryption keys
/// </summary>
public interface IDatabaseEncryptionService
{
    /// <summary>
    /// Gets the database encryption key. Generates and stores one if it doesn't exist.
    /// </summary>
    /// <returns>The encryption key for the database</returns>
    string GetOrCreateEncryptionKey();

    /// <summary>
    /// Checks if an encryption key has been configured
    /// </summary>
    /// <returns>True if a key exists, false otherwise</returns>
    bool HasEncryptionKey();
}
