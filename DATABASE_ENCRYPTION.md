# Database Encryption Implementation

## Overview

GUMS now uses **SQLCipher** to encrypt the SQLite database at rest, protecting sensitive personal information about children and volunteers.

## Security Features

### What's Protected

The entire database file is encrypted using AES-256 encryption, protecting:
- Children's full names and dates of birth
- Emergency contact information (names, phone numbers, emails)
- Medical information (allergies, disabilities, additional needs)
- Addresses and location data
- All attendance and payment records
- Any other sensitive personal data

### How It Works

1. **Automatic Key Generation**
   - On first run, GUMS generates a strong random 256-bit encryption key
   - This key is automatically created without user intervention

2. **Secure Key Storage**
   - The encryption key is protected using Windows DPAPI (Data Protection API)
   - The key is encrypted and bound to your Windows user account
   - Stored at: `%APPDATA%\GUMS\.dbkey`

3. **Access Protection**
   - Only the Windows user who created the database can decrypt it
   - The encrypted database file cannot be opened with standard SQLite tools
   - Copying the database to another computer won't allow access without the key
   - Even with the key file, it can only be decrypted by the original Windows user

## Implementation Details

### Files Created

**Services:**
- `Services/IDatabaseEncryptionService.cs` - Interface for encryption service
- `Services/DatabaseEncryptionService.cs` - Implementation using DPAPI

**Configuration:**
- Updated `Program.cs` to initialize SQLCipher and use encrypted connections

**Packages Added:**
- `SQLitePCLRaw.bundle_e_sqlcipher` (already installed)
- `System.Security.Cryptography.ProtectedData` (newly added)

### Database Location

**Database File:** `%APPDATA%\GUMS\gums.db`
- Windows Example: `C:\Users\YourName\AppData\Roaming\GUMS\gums.db`

**Encryption Key File:** `%APPDATA%\GUMS\.dbkey`
- Hidden file (starts with `.`)
- Binary encrypted data
- Cannot be used on different computers or by different users

## Migration from Unencrypted Database

### IMPORTANT: Old Databases Won't Work

If you previously ran GUMS with an unencrypted database:

1. **Stop the application** (if running)

2. **Delete the old unencrypted database:**
   ```
   Delete: C:\Users\YourName\AppData\Roaming\GUMS\gums.db
   ```

3. **Rebuild and run the application:**
   ```bash
   dotnet build
   dotnet run --project GUMS/GUMS.csproj
   ```

4. **A new encrypted database will be created automatically**

### Why Can't We Convert the Old Database?

SQLCipher requires the database to be created as encrypted from the start. You cannot "upgrade" a plain SQLite database to an encrypted one without exporting and reimporting all data, which is complex and error-prone.

Since this is Phase 1 development and you likely don't have production data yet, starting fresh with an encrypted database is the cleanest approach.

## Backup and Recovery

### Backing Up Your Data

To backup your encrypted database, you need BOTH files:

1. `%APPDATA%\GUMS\gums.db` - The encrypted database
2. `%APPDATA%\GUMS\.dbkey` - The encrypted key file

**Important:**
- Both files must be backed up together
- The `.dbkey` file is hidden, so make sure your backup tool includes hidden files
- The backup can ONLY be restored on the same Windows user account

### Recovery Limitations

The encryption is tied to your Windows user account for security. This means:

- ✅ You can restore from backup on the same computer, same user
- ✅ You can move to a new computer if you transfer your entire Windows user profile
- ❌ You cannot share the database with other users
- ❌ You cannot easily move to a different operating system
- ❌ If you reinstall Windows without preserving the user profile, the key cannot be decrypted

### Disaster Recovery

For production use, consider:

1. **Regular Exports**
   - Implement data export functionality (planned for future phases)
   - Export to encrypted ZIP files as backup
   - Store exports securely

2. **Multiple Administrators**
   - Future enhancement: Support multiple user accounts
   - Each admin would have their own encryption key

## Compliance

This implementation helps meet:

- **GDPR Requirements:** Data protection at rest
- **UK DPA 2018:** Appropriate technical measures
- **Girl Guiding Policies:** Safeguarding children's personal information
- **Specification Requirements:** "Security First: Sensitive personal data must be encrypted"

## Security Considerations

### What This Protects Against

✅ Someone copying the database file and opening it with SQLite Browser
✅ Unauthorized access to the database on a shared computer (if using different Windows accounts)
✅ Data exposure if the computer is stolen or lost (if disk encryption is also used)
✅ Accidental data leaks from backup files

### What This Does NOT Protect Against

❌ Someone with access to your Windows user account (they can run the application)
❌ Malware running under your user account
❌ Physical access to a running/unlocked computer
❌ Keyloggers or screen capture malware

### Additional Security Recommendations

1. **Use Windows BitLocker** for full disk encryption
2. **Lock your computer** when away from it
3. **Use strong Windows account password**
4. **Keep regular encrypted backups** in a secure location
5. **Run antivirus software** to prevent malware
6. **Only install GUMS on trusted computers**

## Testing the Encryption

To verify encryption is working:

1. **Stop the application**

2. **Try to open the database with a SQLite tool:**
   ```bash
   sqlite3 %APPDATA%\GUMS\gums.db
   ```

3. **You should see an error like:**
   ```
   Error: file is not a database
   ```
   OR
   ```
   Error: file is encrypted or is not a database
   ```

4. **If you see table names or data, the encryption is NOT working**

## Troubleshooting

### Error: "Unable to decrypt the database encryption key"

This means the `.dbkey` file was created by a different Windows user.

**Solutions:**
- Log in as the original Windows user
- Or delete both `gums.db` and `.dbkey` to start fresh

### Error: "file is encrypted or is not a database"

This is actually GOOD - it means your database is properly encrypted!

This error appears when trying to open the database with standard (non-encrypted) SQLite tools.

### Database Connection Fails on Startup

Check the logs for the specific error. Common causes:
- Corrupted `.dbkey` file (delete and recreate)
- Missing `SQLitePCLRaw.bundle_e_sqlcipher` package
- Database file permissions issue

## Technical Details

### Encryption Algorithm

- **Algorithm:** AES-256
- **Mode:** CBC (Cipher Block Chaining)
- **Key Size:** 256 bits (32 bytes)
- **Key Derivation:** PBKDF2 (handled by SQLCipher)
- **Key Protection:** Windows DPAPI with CurrentUser scope

### Connection String Format

```csharp
$"Data Source={dbPath};Password={encryptionKey}"
```

Where `encryptionKey` is the base64-encoded 256-bit key retrieved from DPAPI.

### DPAPI (Data Protection API)

Windows DPAPI provides:
- Automatic key derivation from user credentials
- Integration with Windows user profile
- Protection against key extraction
- Tamper detection

The encryption key is protected using the user's Windows login credentials, making it very difficult to extract without those credentials.

---

**Last Updated:** 2026-01-05
**Status:** Implemented in Phase 1
**Next Steps:** Test thoroughly before using with production data
