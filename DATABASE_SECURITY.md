# GUMS Database Security

**Last Updated:** 2026-01-05
**Status:** Implemented

---

## Security Approach

GUMS uses **Windows file-level security** to protect the SQLite database containing sensitive member information.

### Why File-Level Security?

During Phase 1 implementation, we attempted to use **SQLCipher** (AES-256 database encryption) but encountered significant technical challenges with Entity Framework Core integration. After extensive troubleshooting, we made the pragmatic decision to use Windows file permissions instead, which provides:

✅ **Adequate protection** for a single-user desktop application
✅ **Reliable implementation** without complex dependencies
✅ **Zero performance overhead**
✅ **Easy to understand and maintain**

---

## How It Works

### 1. Database Location

The database is stored in the user's protected application data folder:
```
%APPDATA%\GUMS\gums.db
```

On Windows, this typically resolves to:
```
C:\Users\<username>\AppData\Roaming\GUMS\gums.db
```

### 2. File Permissions

On application startup, GUMS automatically:

1. **Disables permission inheritance** on the GUMS directory
2. **Removes all existing access rules**
3. **Grants Full Control ONLY to the current Windows user**

This means:
- ❌ Other users on the same computer **cannot** access the database
- ❌ Network users **cannot** access the database
- ✅ Only the Windows user who runs GUMS can read/write the database

### 3. Implementation

The security is applied in `Program.cs` during startup:

```csharp
var dbDirectory = Path.GetDirectoryName(dbPath)!;
Directory.CreateDirectory(dbDirectory);
DatabaseSecurityService.SecureDatabaseDirectory(dbDirectory);
```

The `DatabaseSecurityService` uses Windows ACLs (Access Control Lists) to restrict access.

---

## What Data Is Protected

The database contains sensitive personal information including:

- **Member names and dates of birth**
- **Emergency contact details** (phone numbers, emails, addresses)
- **Medical information** (allergies, disabilities)
- **Payment records**
- **Attendance records**

---

## Additional Security Measures

### 1. Authentication

- The application requires login with ASP.NET Core Identity
- Strong password requirements enforced (8+ chars, mixed case, digits, special characters)
- Session timeout after 1 hour of inactivity

### 2. Data Removal (GDPR)

- When members leave, personal data is removed
- Only membership number retained for audit trail
- Historical attendance and payment records preserved

### 3. Physical Security

Since this is a desktop application:
- **Windows login** protects access to the computer
- **Full disk encryption** (BitLocker) adds another layer if enabled
- **Screen lock** when computer is unattended

---

## Security Best Practices

### For Unit Leaders

1. **Use a strong Windows password** - your Windows account protects access to GUMS
2. **Enable BitLocker** (Windows full disk encryption) if available
3. **Lock your computer** when stepping away (Windows+L)
4. **Don't share your Windows account** with other leaders
5. **Regular backups** - keep encrypted backups of `%APPDATA%\GUMS\` on external media

### Multi-User Scenarios

If multiple leaders need access to GUMS:

**Option 1: Shared Windows Account (Simple)**
- Create a dedicated Windows user account for GUMS
- Share the password with authorized leaders only
- All leaders log into this account to use GUMS

**Option 2: Individual Accounts with Shared Database (Complex)**
- This requires manual configuration of file permissions
- Not recommended for Phase 1

---

## Comparison to SQLCipher

| Feature | Windows File Security | SQLCipher Encryption |
|---------|----------------------|---------------------|
| Protection from other Windows users | ✅ Yes | ✅ Yes |
| Protection if disk is stolen | ❌ No (use BitLocker) | ✅ Yes |
| Complexity | ⭐ Simple | ⭐⭐⭐⭐ Complex |
| Performance overhead | ✅ None | ⚠️ Some |
| EF Core compatibility | ✅ Perfect | ❌ Problematic |
| Suitable for GUMS? | ✅ Yes | ⚠️ Overkill |

---

## Future Enhancements

If database encryption becomes a requirement in future phases, we could:

1. **Add BitLocker requirement** - Simplest solution, Windows built-in
2. **Implement EFS** (Encrypting File System) on the GUMS folder
3. **Revisit SQLCipher** with a custom ADO.NET provider (significant effort)
4. **Move to SQL Server** with Transparent Data Encryption

---

## Troubleshooting

### "Access Denied" when running GUMS

**Cause:** The GUMS folder may have been created by a different Windows user.

**Solution:**
1. Close GUMS
2. Delete `%APPDATA%\GUMS\` folder
3. Restart GUMS (it will recreate the folder with correct permissions)

### Multiple leaders need access

**Solution:** Use Option 1 from "Multi-User Scenarios" above.

### Database backup and restore

**Backup:**
1. Close GUMS
2. Copy entire `%APPDATA%\GUMS\` folder to secure location
3. Encrypt the backup (use 7-Zip with AES-256 or Windows EFS)

**Restore:**
1. Close GUMS
2. Delete existing `%APPDATA%\GUMS\` folder
3. Copy backed-up folder to `%APPDATA%\GUMS\`
4. GUMS will automatically set correct permissions on startup

---

## Technical Details

### Windows ACL Configuration

The `DatabaseSecurityService` applies these security settings:

```csharp
// Disable inheritance
directorySecurity.SetAccessRuleProtection(true, false);

// Remove all existing rules
foreach (FileSystemAccessRule rule in existingRules)
{
    directorySecurity.RemoveAccessRule(rule);
}

// Add full control for current user only
var accessRule = new FileSystemAccessRule(
    currentUserSid,
    FileSystemRights.FullControl,
    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
    PropagationFlags.None,
    AccessControlType.Allow);
```

### Verification

To verify permissions are set correctly:

1. Right-click `%APPDATA%\GUMS\` folder
2. Properties → Security tab
3. Should show ONLY your Windows username with Full Control
4. Advanced → Inheritance should be "Disabled"

---

## Compliance

### GDPR

- ✅ Data minimization - only essential data collected
- ✅ Right to be forgotten - implemented via data removal
- ✅ Access control - Windows user permissions
- ✅ Audit trail - membership numbers retained

### Girl Guiding Policies

This security approach meets Girl Guiding's requirements for:
- Protecting member personal data
- Secure storage of emergency contact information
- Controlled access to sensitive information

---

**Summary:** GUMS uses Windows file permissions to secure the database. This provides adequate protection for a single-user desktop application while avoiding the complexity and compatibility issues of database-level encryption.
