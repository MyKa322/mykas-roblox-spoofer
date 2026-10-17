using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.Win32;

namespace MyKa.Spoofer.Identity;

internal sealed record IdentityOperationResult(IdentitySnapshot Before, IdentitySnapshot? After, string BackupPath, IReadOnlyList<string> Messages);

/// <summary>Fixed Windows operations with a durable recovery record written before the first change.</summary>
internal static class IdentityOperations
{
    public static async Task<IdentityOperationResult?> Apply(IdentityOptions options, Func<string, bool> confirm, CancellationToken token)
    {
        if (!options.Any) throw new ArgumentException("Select at least one operation.");
        if (options.NeedsAdmin && !IdentityBackupFiles.IsAdministrator)
            throw new InvalidOperationException("The selected Windows operations require an elevated terminal. Close this window and run the application as administrator.");
        using var operationLock = IdentityBackupFiles.Lock();
        token.ThrowIfCancellationRequested();
        var before = await WindowsIdentityInspection.Read(token);
        var notes = new List<string>();
        var backup = Prepare(before, options, notes);
        var preview = string.Join(Environment.NewLine, Describe(backup).Concat(notes));
        if (!confirm(preview)) return null;
        token.ThrowIfCancellationRequested();
        var path = Path.Combine(IdentityBackupFiles.DirectoryPath, backup.Id + ".json");
        IdentityBackupFiles.WriteNew(path, backup);
        notes.Clear();
        notes.Add("Recovery backup saved before changes: " + path);
        foreach (var change in backup.Settings)
        {
            if (token.IsCancellationRequested) break;
            Step(notes, Label(change), () =>
            {
                using var key = OpenSetting(change, true);
                ChangeSetting(change, false, () => ReadSetting(key, change), value => WriteSetting(key, change, value));
                return "configured and read back; effective device values may require restart/reboot";
            });
        }
        if (!token.IsCancellationRequested && options.Cache)
        {
            foreach (var cache in backup.Caches)
            {
                if (token.IsCancellationRequested) break;
                Step(notes, "Roblox " + cache + " cleanup", () =>
                {
                    EnsurePlayerClosed();
                    var source = CachePath(cache);
                    var target = QuarantinePath(backup.Id, cache);
                    CheckMove(source, target);
                    Directory.Move(source, target);
                    return "moved to recovery quarantine";
                });
            }
        }
        if (!token.IsCancellationRequested && options.PlayerRegistry && backup.PlayerRegistry is not null)
            Step(notes, "RobloxPlayer registry cleanup", () =>
            {
                EnsurePlayerClosed();
                using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                using (var current = user.OpenSubKey(WindowsIdentityInspection.PlayerSettingsPath, false))
                {
                    if (current is null || !RegistryEqual(RegistryTree.Capture(current), backup.PlayerRegistry))
                        throw new InvalidOperationException("Player settings changed after backup.");
                }
                user.DeleteSubKeyTree(WindowsIdentityInspection.PlayerSettingsPath, throwOnMissingSubKey: true);
                return "removed the backed-up HKCU RobloxPlayer settings tree";
            });
        if (!token.IsCancellationRequested && options.FlushDns)
        {
            try
            {
                var result = await WindowsPowerShell.Run("$ErrorActionPreference = 'Stop'\nClear-DnsClientCache\n'completed'", token);
                notes.Add(result?.Trim() == "completed" ? "DNS resolver cache: flush completed."
                    : "DNS resolver cache: Windows did not confirm completion. It may already have been flushed.");
            }
            catch (OperationCanceledException) { notes.Add("DNS flush interrupted; the request may already have taken effect."); }
        }
        if (!token.IsCancellationRequested && options.RenewDhcp)
        {
            try
            {
                var result = await WindowsPowerShell.Run(DhcpScript, token);
                notes.Add(DescribeDhcp(result));
            }
            catch (OperationCanceledException) { notes.Add("DHCP renewal interrupted; the request may already have taken effect."); }
        }
        IdentitySnapshot? after = null;
        if (token.IsCancellationRequested) notes.Add("Remaining operations cancelled. The recovery backup is available; capture After to inspect the current state.");
        else after = await ReadAfter(notes, token);
        SaveResults(backup.Id, notes);
        return new(before, after, path, notes);
    }

    internal static IdentityBackup Prepare(IdentitySnapshot before, IdentityOptions options, List<string> notes)
    {
        var machineGuid = WindowsIdentityInspection.ReadMachineGuid();
        if (!Guid.TryParse(machineGuid, out _)) throw new InvalidOperationException("MachineGuid cannot be read. A recovery backup cannot be bound to this Windows installation.");
        var settings = new List<SettingChange>();
        foreach (var (setting, selected) in new[] { (IdentitySetting.MachineGuid, options.MachineGuid),
            (IdentitySetting.HardwareProfileGuid, options.HardwareProfileGuid), (IdentitySetting.ComputerHardwareId, options.ComputerHardwareId) })
        {
            if (!selected) continue;
            var old = RegistryIdentifiers.ReadGuid(setting);
            settings.Add(new(setting, null, old, Guid.NewGuid().ToString(old.StartsWith('{') ? "B" : "D")));
        }
        if (options.AdapterAddresses)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var adapter in adapters) used.Add(Convert.ToHexString(adapter.GetPhysicalAddress().GetAddressBytes()));
            foreach (var adapter in adapters)
            {
                if (!Guid.TryParse(adapter.Id, out _) || adapter.GetPhysicalAddress().GetAddressBytes().Length != 6)
                { notes.Add(IdentitySnapshots.Display(adapter.Name) + ": skipped; no eligible six-byte adapter address."); continue; }
                using var key = WindowsIdentityInspection.OpenAdapter(adapter.Id, false);
                using var support = key?.OpenSubKey(@"Ndi\Params\NetworkAddress", false);
                if (key is null || support is null)
                { notes.Add(IdentitySnapshots.Display(adapter.Name) + ": skipped; the driver does not advertise NetworkAddress support."); continue; }
                var change = new SettingChange(IdentitySetting.AdapterAddress, adapter.Id, null, "");
                var old = ReadSetting(key, change);
                if (old is not null) used.Add(old);
                string next;
                do { next = MacAddress.Create(); } while (!used.Add(next));
                settings.Add(change with { Before = old, After = next });
            }
            if (settings.All(s => s.Setting != IdentitySetting.AdapterAddress)) notes.Add("No driver-supported MAC override was found.");
        }
        var caches = new List<string>();
        RegistryTreeBackup? player = null;
        if (options.Cache || options.PlayerRegistry) EnsurePlayerClosed();
        if (options.Cache)
        {
            foreach (var name in new[] { "logs", "http" })
            {
                var source = CachePath(name);
                IdentityBackupFiles.RejectLinks(source);
                if (Directory.Exists(source)) caches.Add(name);
                else notes.Add("Roblox " + name + ": no folder to quarantine.");
            }
        }
        if (options.PlayerRegistry)
        {
            using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = user.OpenSubKey(WindowsIdentityInspection.PlayerSettingsPath, false);
            if (key is not null) player = RegistryTree.Capture(key);
            else notes.Add("No HKCU RobloxPlayer settings tree to clean.");
        }
        var backup = new IdentityBackup(1, Guid.NewGuid().ToString("N"), IdentityBackupFiles.OwnerSid(), machineGuid!, before,
            options, settings.ToArray(), caches.ToArray(), player);
        IdentityBackupFiles.Validate(backup);
        return backup;
    }

    public static async Task<IdentityOperationResult?> Restore(string backupPath, Func<string, bool> confirm, CancellationToken token)
    {
        using var operationLock = IdentityBackupFiles.Lock();
        var backup = IdentityBackupFiles.Read(backupPath);
        if (backup.OwnerSid != IdentityBackupFiles.OwnerSid() || !string.Equals(backup.Before.Computer, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("This backup belongs to a different Windows user or computer.");
        var currentGuid = WindowsIdentityInspection.ReadMachineGuid();
        var newGuid = backup.Settings.SingleOrDefault(s => s.Setting == IdentitySetting.MachineGuid)?.After;
        if (!Same(currentGuid, backup.MachineGuidBefore) && !Same(currentGuid, newGuid))
            throw new InvalidDataException("MachineGuid no longer matches this backup. Automatic restore was refused.");
        if (backup.Settings.Length > 0 && !IdentityBackupFiles.IsAdministrator)
            throw new InvalidOperationException("Restoring Windows settings requires an elevated terminal.");
        var before = await WindowsIdentityInspection.Read(token);
        if (!confirm("Restore the backed-up stored GUID/MAC settings, quarantined folders, and RobloxPlayer preferences where still eligible?\nExisting folders or changed settings are preserved. DHCP leases and DNS cache cannot be restored.\n\nBackup ID: " + backup.Id)) return null;
        var messages = new List<string>();
        foreach (var change in backup.Settings)
        {
            if (token.IsCancellationRequested) break;
            Step(messages, Label(change) + " restore", () =>
            {
                using var key = OpenSetting(change, true);
                if (!ChangeSetting(change, true, () => ReadSetting(key, change), value => WriteSetting(key, change, value)))
                    return "already at the backed-up value";
                return "restored and read back; an adapter restart/reboot may still be required";
            });
        }
        foreach (var cache in backup.Caches)
        {
            if (token.IsCancellationRequested) break;
            Step(messages, "Roblox " + cache + " restore", () =>
            {
                EnsurePlayerClosed();
                var source = QuarantinePath(backup.Id, cache);
                var target = CachePath(cache);
                IdentityBackupFiles.RejectLinks(source);
                if (!Directory.Exists(source)) return "no quarantined folder; already restored or never moved";
                CheckMove(source, target);
                Directory.Move(source, target);
                return "restored from quarantine";
            });
        }
        if (!token.IsCancellationRequested && backup.PlayerRegistry is not null)
            Step(messages, "RobloxPlayer registry restore", () =>
            {
                EnsurePlayerClosed();
                using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                using (var current = user.OpenSubKey(WindowsIdentityInspection.PlayerSettingsPath, false))
                {
                    if (current is not null)
                    {
                        if (RegistryEqual(RegistryTree.Capture(current), backup.PlayerRegistry)) return "already at the backed-up values";
                        throw new InvalidOperationException("A Player settings tree already exists; it was preserved.");
                    }
                }
                using var key = user.CreateSubKey(WindowsIdentityInspection.PlayerSettingsPath, true);
                RegistryTree.Restore(key, backup.PlayerRegistry);
                return "restored from backup";
            });
        IdentitySnapshot? after = null;
        if (token.IsCancellationRequested) messages.Add("Restore interrupted; some settings may already have been restored. The backup remains available.");
        else after = await ReadAfter(messages, token);
        SaveResults(backup.Id, messages);
        return new(before, after, backupPath, messages);
    }

    private static IEnumerable<string> Describe(IdentityBackup backup)
    {
        yield return "Review the operations for this Windows installation:";
        foreach (var change in backup.Settings) yield return Label(change) + ": " + (change.Before ?? "<not configured>") + " -> " + change.After;
        if (backup.Options.HardwareProfileGuid || backup.Options.ComputerHardwareId)
            yield return "Stored hardware GUID changes can affect Windows/software configuration. They do not change firmware-derived hardware IDs.";
        if (backup.Options.AdapterAddresses) yield return "MAC overrides are configured only. Restart adapters or reboot yourself; Compare checks effective MAC separately.";
        if (backup.Options.MachineGuid) yield return "MachineGuid changes can affect installed software. This does not change firmware UUID or board/drive serials.";
        foreach (var cache in backup.Caches) yield return "Quarantine %LOCALAPPDATA%\\Roblox\\" + cache;
        if (backup.PlayerRegistry is not null) yield return "Back up and clear HKCU\\" + WindowsIdentityInspection.PlayerSettingsPath;
        if (backup.Options.FlushDns) yield return "Flush the Windows DNS resolver cache (not reversible).";
        if (backup.Options.RenewDhcp) yield return "Request IPv4 DHCP lease renewal on active DHCP adapters (may disrupt connectivity). A different local/public IP is not guaranteed.";
        yield return "An automatic recovery backup is written before changes. Close RobloxPlayer/Studio before cleanup. Continue?";
    }

    private static bool RegistryEqual(RegistryTreeBackup a, RegistryTreeBackup b)
    {
        if (a.Values.Length != b.Values.Length || a.Children.Count != b.Children.Count) return false;
        foreach (var v in a.Values)
        {
            var other = b.Values.SingleOrDefault(x => string.Equals(x.Name, v.Name, StringComparison.OrdinalIgnoreCase));
            if (other is null || other.Kind != v.Kind || !v.Data.SequenceEqual(other.Data, StringComparer.Ordinal)) return false;
        }
        return a.Children.All(child => b.Children.TryGetValue(child.Key, out var other) && RegistryEqual(child.Value, other));
    }

    internal static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>The same compare-before-write invariant governs apply and restore.</summary>
    internal static bool ChangeSetting(SettingChange change, bool restore, Func<string?> read, Action<string?> write)
    {
        var current = read();
        var wanted = restore ? change.Before : change.After;
        if (restore && Same(current, wanted)) return false;
        var expected = restore ? change.After : change.Before;
        if (!Same(current, expected)) throw new InvalidOperationException("The setting changed outside this recorded operation; it was preserved.");
        write(wanted);
        if (!Same(read(), wanted)) throw new IOException("The setting write could not be verified. Inspect After before retrying.");
        return true;
    }

    private static void WriteSetting(RegistryKey key, SettingChange change, string? value)
    {
        if (value is null) key.DeleteValue(ValueName(change), throwOnMissingValue: false);
        else key.SetValue(ValueName(change), value, RegistryValueKind.String);
        key.Flush();
    }
    private static string Label(SettingChange change) => change.Setting == IdentitySetting.AdapterAddress
        ? "MAC override for adapter " + change.AdapterId : RegistryIdentifiers.Location(change.Setting).Name;
    private static string ValueName(SettingChange change) => change.Setting == IdentitySetting.AdapterAddress
        ? "NetworkAddress" : RegistryIdentifiers.Location(change.Setting).Name;

    private static RegistryKey OpenSetting(SettingChange change, bool write)
    {
        if (change.Setting == IdentitySetting.AdapterAddress)
        {
            var adapter = WindowsIdentityInspection.OpenAdapter(change.AdapterId!, write)
                ?? throw new IOException("The adapter is unavailable.");
            using var support = adapter.OpenSubKey(@"Ndi\Params\NetworkAddress", false);
            if (support is not null) return adapter;
            adapter.Dispose();
            throw new IOException("The driver no longer advertises MAC override support.");
        }
        var location = RegistryIdentifiers.Location(change.Setting);
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        return machine.OpenSubKey(location.Path, write) ?? throw new IOException("The stored GUID is unavailable.");
    }

    private static string? ReadSetting(RegistryKey key, SettingChange change)
    {
        var name = ValueName(change);
        if (!key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
        if (key.GetValueKind(name) != RegistryValueKind.String) throw new InvalidDataException("The existing setting has an unsupported registry type.");
        var data = RegistryIdentifiers.Read(key, name);
        if (!data.Terminated || data.EmbeddedNull) throw new InvalidDataException("The stored setting has unsupported REG_SZ encoding.");
        if (change.Setting != IdentitySetting.AdapterAddress && !Guid.TryParse(data.Value, out _))
            throw new InvalidDataException("The stored setting is not a GUID.");
        return data.Value;
    }

    private static void Step(List<string> messages, string label, Func<string> action)
    {
        try { messages.Add(label + ": " + action() + "."); }
        catch (Exception e) when (WindowsIdentityInspection.Expected(e))
        {
            var reason = e is InvalidOperationException or InvalidDataException ? e.Message
                : e is UnauthorizedAccessException or System.Security.SecurityException ? "Access denied; check elevation and permissions."
                : "Windows did not confirm completion; files/settings may be missing, locked, or inaccessible.";
            messages.Add(label + ": incomplete or failed. " + reason + " Retain the recovery backup and inspect Compare before retrying.");
        }
    }

    private static async Task<IdentitySnapshot?> ReadAfter(List<string> messages, CancellationToken token)
    {
        try { return await WindowsIdentityInspection.Read(token); }
        catch (Exception e) when (e is OperationCanceledException || WindowsIdentityInspection.Expected(e))
        {
            messages.Add("After capture did not complete. The operations may already have taken effect; the recovery backup remains available.");
            return null;
        }
    }

    private static void SaveResults(string id, List<string> messages)
    {
        // An immutable journal is useful even if the user closes the panel before copying the result.
        var path = Path.Combine(IdentityBackupFiles.DirectoryPath, id + ".result-" + Guid.NewGuid().ToString("N") + ".json");
        try { IdentityBackupFiles.WriteNew(path, new { ObservedUtc = DateTimeOffset.UtcNow, Messages = messages.ToArray() }); }
        catch (Exception e) when (WindowsIdentityInspection.Expected(e)) { messages.Add("Result journal could not be saved; the original recovery backup was preserved."); }
    }

    internal static string CachePath(string name)
    {
        if (name is not ("logs" or "http")) throw new InvalidDataException("Unsupported cache folder.");
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) throw new IOException("Local application data is unavailable.");
        return Path.Combine(root, "Roblox", name);
    }

    private static string QuarantinePath(string id, string name)
    {
        if (!Guid.TryParseExact(id, "N", out _) || name is not ("logs" or "http")) throw new InvalidDataException("Invalid quarantine target.");
        var root = Path.Combine(IdentityBackupFiles.DirectoryPath, id);
        IdentityBackupFiles.PrivateDirectory(root);
        return Path.Combine(root, name);
    }

    internal static void CheckMove(string source, string target)
    {
        IdentityBackupFiles.RejectLinks(source);
        IdentityBackupFiles.RejectLinks(target);
        if (!Directory.Exists(source)) throw new IOException("The source folder is unavailable.");
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("The destination already exists and was preserved.");
        var parent = Path.GetDirectoryName(target) ?? throw new IOException("The destination parent is unavailable.");
        IdentityBackupFiles.RejectLinks(parent);
        if (!Directory.Exists(parent)) throw new IOException("The destination parent is unavailable.");
    }

    private static void EnsurePlayerClosed()
    {
        foreach (var name in new[] { "RobloxPlayerBeta", "RobloxStudioBeta", "RobloxPlayer", "RobloxStudio" })
        {
            var running = Process.GetProcessesByName(name);
            try { if (running.Length > 0) throw new InvalidOperationException("Close RobloxPlayer and Roblox Studio before cache or registry cleanup."); }
            finally { foreach (var process in running) process.Dispose(); }
        }
    }

    internal static string DescribeDhcp(string? json)
    {
        const string unknown = "DHCP renewal was not confirmed. Check the local IP observations; a request may already have taken effect.";
        if (json is null) return unknown;
        try
        {
            var codes = JsonSerializer.Deserialize<int[]>(json);
            if (codes is null || codes.Length > 256 || codes.Any(c => c < 0)) return unknown;
            if (codes.Length == 0) return "DHCP renewal: no active IPv4 DHCP adapters were found.";
            return $"DHCP renewal: {codes.Count(c => c is 0 or 1)}/{codes.Length} adapter requests reported completion; {codes.Count(c => c == 1)} require reboot. Compare local IPs; no new public IP is promised.";
        }
        catch (JsonException) { return unknown; }
    }

    private const string DhcpScript = """
        $ErrorActionPreference = 'Stop'
        $codes = [Collections.Generic.List[int]]::new()
        foreach ($adapter in @(Get-CimInstance -ClassName Win32_NetworkAdapterConfiguration -Filter 'IPEnabled=True AND DHCPEnabled=True' -OperationTimeoutSec 5)) {
            try { $codes.Add([int](Invoke-CimMethod -InputObject $adapter -MethodName RenewDHCPLease -OperationTimeoutSec 10).ReturnValue) }
            catch { $codes.Add(2147483647) }
        }
        ConvertTo-Json -InputObject $codes.ToArray() -Compress
        """;

}
