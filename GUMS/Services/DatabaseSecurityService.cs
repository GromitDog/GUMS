using System.Security.AccessControl;
using System.Security.Principal;

namespace GUMS.Services;

/// <summary>
/// Service for setting Windows file-level security on the database directory.
/// </summary>
public static class DatabaseSecurityService
{
    /// <summary>
    /// Sets restrictive file permissions on the database directory,
    /// allowing only the current user to access it.
    /// </summary>
    public static void SecureDatabaseDirectory(string directoryPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            // File permissions only apply on Windows
            return;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            if (!directoryInfo.Exists)
            {
                return;
            }

            // Get current user
            var currentUser = WindowsIdentity.GetCurrent();
            var currentUserSid = currentUser.User;

            // Get directory security
            var directorySecurity = directoryInfo.GetAccessControl();

            // Disable inheritance
            directorySecurity.SetAccessRuleProtection(true, false);

            // Remove all existing rules
            var existingRules = directorySecurity.GetAccessRules(true, true, typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in existingRules)
            {
                directorySecurity.RemoveAccessRule(rule);
            }

            // Add full control for current user only
            var accessRule = new FileSystemAccessRule(
                currentUserSid!,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            directorySecurity.AddAccessRule(accessRule);

            // Apply the security settings
            directoryInfo.SetAccessControl(directorySecurity);
        }
        catch (Exception ex)
        {
            // Log but don't fail - security is best-effort
            Console.WriteLine($"Warning: Could not set restrictive permissions on database directory: {ex.Message}");
        }
    }
}
