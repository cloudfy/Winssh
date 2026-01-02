using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;

namespace WinSsh.Profiles;
internal static class SshKeyManager
{
    internal static string ImportKey(string fileName)
    {
        var localFolder = ApplicationData.Current.LocalFolder.Path;
        var keyName = Guid.NewGuid().ToString();
        var filePath = Path.Combine(localFolder, "keys", keyName);

        Directory.CreateDirectory(Path.Combine(localFolder, "keys"));

        File.Copy(fileName, filePath);
        SetCredentials(filePath);
        return filePath;
    }

    internal static async Task<string?> DuplicateKeyAsync(string? keyReference)
    {
        if (string.IsNullOrWhiteSpace(keyReference))
            return null;

        var localFolder = ApplicationData.Current.LocalFolder.Path;
        var keysFolder = Path.Combine(localFolder, "keys");
        var sourcePath = Path.Combine(keysFolder, keyReference);

        if (!File.Exists(sourcePath))
            return null;

        var newKeyName = Guid.NewGuid().ToString() + Path.GetExtension(keyReference);
        var destPath = Path.Combine(keysFolder, newKeyName);

        await Task.Run(() =>
        {
            File.Copy(sourcePath, destPath);
            SetCredentials(destPath);
        });

        return newKeyName;
    }

    internal static async Task DeleteKeyAsync(string keyReference)
    {
        if (string.IsNullOrWhiteSpace(keyReference))
            return;

        var localFolder = ApplicationData.Current.LocalFolder.Path;
        var keysFolder = Path.Combine(localFolder, "keys");
        var keyPath = Path.Combine(keysFolder, keyReference);

        if (File.Exists(keyPath))
        {
            await Task.Run(() => File.Delete(keyPath));
        }
    }

    private static void SetCredentials(string keyPath)
    {
        // Current user (the one running the program)
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser == null)
        {
            Console.WriteLine("Could not determine current user SID.");
            return;
        }

        // Get existing ACL
        var fileInfo = new FileInfo(keyPath);
        var security = fileInfo.GetAccessControl(AccessControlSections.Access);

        // Disable inheritance and optionally remove inherited rules
        // (true = protect from inheritance, false = do NOT preserve inherited rules => removes them)
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        // Remove ALL existing explicit rules
        // (this clears out stuff like UNKNOWN SIDs too)
        AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        foreach (FileSystemAccessRule rule in rules)
        {
            security.RemoveAccessRule(rule);
        }

        // Grant current user read access (SSH generally wants read only)
        var readRule = new FileSystemAccessRule(
            currentUser,
            FileSystemRights.Read,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Allow
        );

        security.AddAccessRule(readRule);

        // Apply ACL
        fileInfo.SetAccessControl(security);
    }
}
