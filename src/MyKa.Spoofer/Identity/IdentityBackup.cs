using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using MyKa.Spoofer.Accounts;

namespace MyKa.Spoofer.Identity;

internal enum IdentitySetting { MachineGuid, AdapterAddress, HardwareProfileGuid, ComputerHardwareId }
internal sealed record SettingChange(IdentitySetting Setting, string? AdapterId, string? Before, string After);
internal sealed record IdentityOptions(bool MachineGuid, bool AdapterAddresses, bool FlushDns, bool RenewDhcp, bool Cache, bool PlayerRegistry,
    bool HardwareProfileGuid = false, bool ComputerHardwareId = false)
{
    public bool Any => MachineGuid || AdapterAddresses || FlushDns || RenewDhcp || Cache || PlayerRegistry || HardwareProfileGuid || ComputerHardwareId;
    public bool NeedsAdmin => MachineGuid || AdapterAddresses || FlushDns || RenewDhcp || HardwareProfileGuid || ComputerHardwareId;
}
internal sealed record RegistryValueBackup(string Name, RegistryValueKind Kind, string[] Data);
internal sealed record RegistryTreeBackup(RegistryValueBackup[] Values, Dictionary<string, RegistryTreeBackup> Children);
internal sealed record IdentityBackup(int SchemaVersion, string Id, string OwnerSid, string MachineGuidBefore,
    IdentitySnapshot Before, IdentityOptions Options, SettingChange[] Settings, string[] Caches, RegistryTreeBackup? PlayerRegistry);

internal static class IdentityBackupFiles
{
    public static string DirectoryPath => Path.Combine(AccountStore.DefaultDirectory, "identity-backups");

    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static string OwnerSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? throw new IOException("The current Windows user is unavailable.");
    }

    public static FileStream Lock()
    {
        PrivateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, "operation.lock");
        RejectLinks(path);
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new IOException("Another identity operation may be open. Close it before continuing."); }
    }

    public static void PrivateDirectory(string path)
    {
        RejectLinks(path);
        var info = Directory.CreateDirectory(path);
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { new SecurityIdentifier(OwnerSid()), new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(acl);
    }

    public static void WriteNew<T>(string path, T value)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value, IdentitySnapshots.JsonOptions);
        if (data.Length > IdentitySnapshots.MaxFileBytes) throw new InvalidDataException("The backup exceeds its size limit. No operation was applied.");
        var target = Path.GetFullPath(path);
        RejectLinks(target);
        var parent = Path.GetDirectoryName(target) ?? throw new ArgumentException("Choose a backup file inside a directory.");
        if (!Directory.Exists(parent)) throw new IOException("The backup directory does not exist.");
        var temporary = Path.Combine(parent, ".myka-" + Guid.NewGuid().ToString("N") + ".tmp");
        var acl = new FileSecurity();
        acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { new SecurityIdentifier(OwnerSid()), new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        try
        {
            using (var stream = new FileInfo(temporary).Create(FileMode.CreateNew, FileSystemRights.Write,
                FileShare.None, 4096, FileOptions.None, acl))
            {
                // Create with its private ACL; identifiers are never written under inherited public permissions.
                stream.Write(data);
                stream.Flush(true);
            }
            File.Move(temporary, target, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static IdentityBackup Read(string path)
    {
        try
        {
            RejectLinks(Path.GetFullPath(path));
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > IdentitySnapshots.MaxFileBytes) throw new InvalidDataException("The backup is too large.");
            var backup = JsonSerializer.Deserialize<IdentityBackup>(stream, IdentitySnapshots.JsonOptions)
                ?? throw new InvalidDataException("The backup is empty.");
            Validate(backup);
            return backup;
        }
        catch (JsonException) { throw new InvalidDataException("The backup JSON is invalid or unsupported."); }
    }

    public static void Validate(IdentityBackup backup)
    {
        if (backup.SchemaVersion != 1 || !Guid.TryParseExact(backup.Id, "N", out _) || backup.Before is null
            || backup.Options is null || !backup.Options.Any || !Guid.TryParse(backup.MachineGuidBefore, out _)
            || !IdentitySnapshots.Text(backup.OwnerSid, 256) || backup.Settings is null || backup.Settings.Length > 512
            || backup.Caches is null || backup.Caches.Length > 2)
            throw new InvalidDataException("Invalid identity backup.");
        IdentitySnapshots.Validate(backup.Before);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in backup.Settings)
        {
            if (change is null || change.After is null || change.Before is { Length: > 256 }
                || (change.Before is not null && change.Before.Any(char.IsControl))) throw new InvalidDataException("Invalid setting in backup.");
            if (change.Setting == IdentitySetting.MachineGuid)
            {
                if (!backup.Options.MachineGuid || change.AdapterId is not null || change.Before != backup.MachineGuidBefore
                    || !Guid.TryParse(change.After, out _) || change.Before == change.After || !targets.Add("machine-guid"))
                    throw new InvalidDataException("Invalid MachineGuid backup.");
            }
            else if (change.Setting == IdentitySetting.AdapterAddress)
            {
                if (!backup.Options.AdapterAddresses || !Guid.TryParse(change.AdapterId, out var id)
                    || !MacAddress.ValidOverride(change.After) || (change.Before is { Length: > 0 }
                        && (change.Before.Length != 12 || !change.Before.All(char.IsAsciiHexDigit))) || !targets.Add(id.ToString("D")))
                    throw new InvalidDataException("Invalid adapter backup.");
            }
            else if (change.Setting is IdentitySetting.HardwareProfileGuid or IdentitySetting.ComputerHardwareId)
            {
                if (!(change.Setting == IdentitySetting.HardwareProfileGuid ? backup.Options.HardwareProfileGuid : backup.Options.ComputerHardwareId)
                    || change.AdapterId is not null || !Guid.TryParse(change.Before, out _) || !Guid.TryParse(change.After, out _)
                    || change.Before == change.After || !targets.Add(change.Setting.ToString())) throw new InvalidDataException("Invalid stored GUID backup.");
            }
            else throw new InvalidDataException("Unsupported setting in backup.");
        }
        if (backup.Caches.Distinct(StringComparer.Ordinal).Count() != backup.Caches.Length
            || backup.Caches.Any(c => c is not ("logs" or "http")) || (!backup.Options.Cache && backup.Caches.Length > 0)
            || (!backup.Options.PlayerRegistry && backup.PlayerRegistry is not null)) throw new InvalidDataException("Invalid cleanup backup.");
        if (backup.PlayerRegistry is not null) RegistryTree.Validate(backup.PlayerRegistry);
    }

    internal static void RejectLinks(string path)
    {
        // Verify both the final object and every existing ancestor before moving or writing.
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked identity files or directories are not supported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}

internal static class MacAddress
{
    public static bool ValidOverride(string? value) => value is { Length: 12 }
        && value.All(char.IsAsciiHexDigit) && (Convert.ToByte(value[..2], 16) & 3) == 2;

    public static string Create()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(6);
        bytes[0] = (byte)((bytes[0] & 0xfc) | 2); // Locally administered, unicast; never a vendor's assigned identity.
        return Convert.ToHexString(bytes);
    }
}

internal static class RegistryTree
{
    public static RegistryTreeBackup Capture(RegistryKey key, int depth = 0)
    {
        var nodes = 0;
        var bytes = 0;
        var result = CaptureBounded(key, depth, ref nodes, ref bytes);
        Validate(result);
        return result;
    }

    private static RegistryTreeBackup CaptureBounded(RegistryKey key, int depth, ref int nodes, ref int bytes)
    {
        if (++nodes > 512 || depth > 8 || key.ValueCount > 256 || key.SubKeyCount > 64) throw new InvalidDataException("The Player settings tree is too large to back up safely.");
        var values = key.GetValueNames().Select(name =>
        {
            var kind = key.GetValueKind(name);
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            var data = kind switch
            {
                RegistryValueKind.String or RegistryValueKind.ExpandString when value is string text => new[] { text },
                RegistryValueKind.MultiString when value is string[] texts => texts,
                RegistryValueKind.DWord when value is int number => [unchecked((uint)number).ToString("X8")],
                RegistryValueKind.QWord when value is long number => [unchecked((ulong)number).ToString("X16")],
                RegistryValueKind.Binary when value is byte[] bytes => [Convert.ToBase64String(bytes)],
                _ => throw new InvalidDataException("The Player settings contain an unsupported registry value type."),
            };
            return new RegistryValueBackup(name, kind, data);
        }).ToArray();
        foreach (var value in values)
        {
            bytes = checked(bytes + value.Name.Length * 2 + value.Data.Sum(s => checked(s.Length * 2)));
            if (bytes > IdentitySnapshots.MaxFileBytes / 2) throw new InvalidDataException("Player settings exceed the backup size limit.");
        }
        var children = new Dictionary<string, RegistryTreeBackup>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in key.GetSubKeyNames())
        {
            using var child = key.OpenSubKey(name, false) ?? throw new IOException("Player settings changed while reading.");
            children.Add(name, CaptureBounded(child, depth + 1, ref nodes, ref bytes));
        }
        var result = new RegistryTreeBackup(values, children);
        return result;
    }

    public static void Validate(RegistryTreeBackup tree)
    {
        var nodes = 0;
        long bytes = 0;
        void Visit(RegistryTreeBackup current, int depth)
        {
            if (current is null || ++nodes > 512 || depth > 8 || current.Values is null || current.Children is null
                || current.Values.Length > 256 || current.Children.Count > 64) throw new InvalidDataException("Invalid registry backup tree.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in current.Values)
            {
                if (value is null || !Name(value.Name, true) || !names.Add(value.Name) || value.Data is null
                    || value.Data.Length > 4096 || value.Data.Any(s => s is null || s.Length > 65536 || s.Contains('\0')))
                    throw new InvalidDataException("Invalid registry backup value.");
                _ = Decode(value);
                bytes += value.Name.Length * 2L + value.Data.Sum(s => s.Length * 2L);
                if (bytes > IdentitySnapshots.MaxFileBytes / 2) throw new InvalidDataException("Registry backup exceeds the size limit.");
            }
            names.Clear();
            foreach (var (name, child) in current.Children)
            {
                if (!Name(name, false) || !names.Add(name)) throw new InvalidDataException("Invalid registry backup subkey.");
                Visit(child, depth + 1);
            }
        }
        Visit(tree, 0);
    }

    private static bool Name(string? name, bool allowEmpty) => name is not null && (allowEmpty || name.Length > 0)
        && name.Length <= 255 && !name.Contains('\\') && !name.Any(char.IsControl);

    internal static object Decode(RegistryValueBackup value)
    {
        try
        {
            return value.Kind switch
            {
                RegistryValueKind.String or RegistryValueKind.ExpandString when value.Data.Length == 1 => value.Data[0],
                RegistryValueKind.MultiString => value.Data,
                RegistryValueKind.DWord when value.Data.Length == 1 && value.Data[0].Length == 8 => unchecked((int)Convert.ToUInt32(value.Data[0], 16)),
                RegistryValueKind.QWord when value.Data.Length == 1 && value.Data[0].Length == 16 => unchecked((long)Convert.ToUInt64(value.Data[0], 16)),
                RegistryValueKind.Binary when value.Data.Length == 1 => Convert.FromBase64String(value.Data[0]),
                _ => throw new InvalidDataException("Unsupported registry backup value."),
            };
        }
        catch (Exception e) when (e is FormatException or OverflowException) { throw new InvalidDataException("Invalid registry backup encoding."); }
    }

    public static void Restore(RegistryKey key, RegistryTreeBackup tree)
    {
        foreach (var value in tree.Values) key.SetValue(value.Name, Decode(value), value.Kind);
        foreach (var (name, child) in tree.Children)
        {
            using var subkey = key.CreateSubKey(name, true);
            Restore(subkey, child);
        }
        key.Flush();
    }
}
