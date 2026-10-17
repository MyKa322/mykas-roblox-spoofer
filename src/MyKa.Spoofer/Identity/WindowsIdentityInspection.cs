using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace MyKa.Spoofer.Identity;

internal static class WindowsIdentityInspection
{
    internal const string CryptographyPath = @"SOFTWARE\Microsoft\Cryptography";
    internal const string AdapterClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    internal const string PlayerSettingsPath = @"SOFTWARE\Roblox\RobloxPlayer";

    public static async Task<IdentitySnapshot> Read(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var values = new List<IdentityValue>();
        Probe(values, "machine-guid", "MachineGuid", "Windows registry; writable with administrator rights", () => ReadMachineGuid());
        RegistryIdentifiers.Inspect(values);
        ReadAdapters(values);
        ReadVolumes(values);
        ReadMonitors(values);
        var wmi = await WindowsPowerShell.Run(WmiScript, token);
        if (wmi is null)
        {
            foreach (var (key, label) in new[] { ("wmi-uuid", "System UUID (WMI)"), ("wmi-board", "Baseboard serial (WMI)"), ("wmi-drive", "Physical drive serials (WMI)") })
                values.Add(new(key, label, null, "Firmware/device observation; inspect only", "WMI unavailable or access denied."));
        }
        else
        {
            try
            {
                var observed = JsonSerializer.Deserialize<IdentityValue[]>(wmi, IdentitySnapshots.JsonOptions);
                if (observed is null || observed.Length is < 3 or > 128) throw new JsonException();
                IdentitySnapshots.Validate(new(1, DateTimeOffset.UtcNow, Environment.MachineName, observed));
                if (observed.Any(v => !v.Key.StartsWith("wmi-", StringComparison.Ordinal))) throw new JsonException();
                values.AddRange(observed);
            }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                values.Add(new("wmi-unavailable", "Hardware WMI classes", null, "Inspect only", "WMI returned an unreadable report."));
            }
        }
        var result = new IdentitySnapshot(1, DateTimeOffset.UtcNow, Environment.MachineName, values.ToArray());
        IdentitySnapshots.Validate(result);
        return result;
    }

    internal static string? ReadMachineGuid()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(CryptographyPath, false);
        return key?.GetValue("MachineGuid", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    private static void ReadAdapters(List<IdentityValue> values)
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().OrderBy(a => a.Id, StringComparer.Ordinal))
            {
                var id = adapter.Id;
                var label = IdentitySnapshots.Display(adapter.Name);
                if (label.Length == 0) label = "Adapter " + id;
                Probe(values, "mac/" + id, label + " | effective MAC", "Driver-reported address", () =>
                {
                    var bytes = adapter.GetPhysicalAddress().GetAddressBytes();
                    return bytes.Length == 0 ? null : Convert.ToHexString(bytes);
                });
                Probe(values, "ip/" + id, label + " | local IPs", "Local addresses; public IP is not measured", () =>
                    string.Join(", ", adapter.GetIPProperties().UnicastAddresses.Select(a => a.Address.ToString()).Order(StringComparer.Ordinal)) is { Length: > 0 } ips ? ips : "No local addresses");
                Probe(values, "mac-override/" + id, label + " | configured MAC override", "Driver setting; effective MAC may need adapter restart/reboot", () =>
                {
                    using var key = OpenAdapter(id, false);
                    if (key is null) return "No matching driver settings";
                    using var support = key.OpenSubKey(@"Ndi\Params\NetworkAddress", false);
                    if (support is null) return "Driver does not advertise NetworkAddress support";
                    if (!key.GetValueNames().Contains("NetworkAddress", StringComparer.OrdinalIgnoreCase)) return "Not configured";
                    var configured = RegistryIdentifiers.Read(key, "NetworkAddress");
                    if (!configured.Terminated || configured.EmbeddedNull) throw new InvalidDataException("Unsupported REG_SZ encoding.");
                    return configured.Value.Length == 0 ? "<empty override>" : configured.Value;
                });
            }
            if (!values.Any(v => v.Key.StartsWith("mac/", StringComparison.Ordinal)))
                values.Add(new("adapters", "Network adapters", null, "Windows", "No adapters were reported."));
        }
        catch (Exception e) when (Expected(e) || e is NetworkInformationException)
        {
            values.Add(new("adapters", "Network adapter inventory", null, "Windows", "Adapter enumeration is unavailable."));
        }
    }

    internal static RegistryKey? OpenAdapter(string adapterId, bool writable)
    {
        if (!Guid.TryParse(adapterId, out var wanted)) throw new InvalidDataException("Invalid adapter ID.");
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var adapters = machine.OpenSubKey(AdapterClassPath, false);
        if (adapters is null) return null;
        foreach (var name in adapters.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsAsciiDigit)))
        {
            using var candidate = adapters.OpenSubKey(name, false);
            if (Guid.TryParse(candidate?.GetValue("NetCfgInstanceId") as string, out var found) && wanted == found)
                return machine.OpenSubKey(AdapterClassPath + "\\" + name, writable);
        }
        return null;
    }

    private static void ReadVolumes(List<IdentityValue> values)
    {
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
                Probe(values, "volume/" + drive.Name, drive.Name + " | volume serial", "Filesystem serial; inspect only", () =>
                {
                    if (!GetVolumeInformation(drive.Name, null, 0, out var serial, out _, out _, null, 0))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    return $"{serial >> 16:X4}-{serial & 0xffff:X4}";
                });
        }
        catch (Exception e) when (Expected(e))
        {
            values.Add(new("volumes", "Volume serial inventory", null, "Inspect only", "Volume enumeration is unavailable."));
        }
    }

    private static void ReadMonitors(List<IdentityValue> values)
    {
        const string root = @"SYSTEM\CurrentControlSet\Enum\DISPLAY";
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var displays = machine.OpenSubKey(root, false);
            if (displays is null) throw new IOException();
            foreach (var model in displays.GetSubKeyNames().Take(128))
            {
                using var models = displays.OpenSubKey(model, false);
                foreach (var instance in models?.GetSubKeyNames().Take(128) ?? [])
                {
                    if (values.Count(v => v.Key.StartsWith("edid/", StringComparison.Ordinal)) >= 256)
                        throw new IOException("EDID enumeration exceeded its limit.");
                    Probe(values, "edid/" + model + "/" + instance, "Monitor " + model + " | EDID SHA-256", "Registry EDID observation; inspect only", () =>
                    {
                        using var parameters = models!.OpenSubKey(instance + @"\Device Parameters", false);
                        return parameters?.GetValue("EDID") is byte[] { Length: >= 128 and <= 4096 } bytes
                            ? Convert.ToHexString(SHA256.HashData(bytes)) : null;
                    });
                }
            }
            if (!values.Any(v => v.Key.StartsWith("edid/", StringComparison.Ordinal))) throw new IOException();
        }
        catch (Exception e) when (Expected(e))
        {
            values.Add(new("edid", "Monitor EDID inventory", null, "Inspect only", "EDID enumeration is unavailable."));
        }
    }

    private static void Probe(List<IdentityValue> values, string key, string label, string scope, Func<string?> read)
    {
        try
        {
            var value = read();
            if (!string.IsNullOrWhiteSpace(value)) { values.Add(new(key, label, IdentitySnapshots.Display(value), scope)); return; }
        }
        catch (Exception e) when (Expected(e) || e is NetworkInformationException) { }
        values.Add(new(key, label, null, scope, "Windows did not provide this value."));
    }

    internal static bool Expected(Exception e) => e is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException
        or Win32Exception or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException
        or DllNotFoundException or EntryPointNotFoundException;

    [DllImport("kernel32.dll", EntryPoint = "GetVolumeInformationW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(string root, StringBuilder? volumeName, uint volumeNameSize,
        out uint serial, out uint maxComponent, out uint flags, StringBuilder? filesystem, uint filesystemSize);

    private const string WmiScript = """
        $ErrorActionPreference = 'Stop'
        $result = [Collections.Generic.List[object]]::new()
        foreach ($probe in @(
            @{ Class = 'Win32_ComputerSystemProduct'; Property = 'UUID'; Key = 'wmi-uuid'; Label = 'System UUID (WMI)' },
            @{ Class = 'Win32_BaseBoard'; Property = 'SerialNumber'; Key = 'wmi-board'; Label = 'Baseboard serial (WMI)' },
            @{ Class = 'Win32_DiskDrive'; Property = 'SerialNumber'; Key = 'wmi-drive'; Label = 'Physical drive serial (WMI)' }
        )) {
            try {
                $items = @(Get-CimInstance -ClassName $probe.Class -OperationTimeoutSec 5)
                if ($items.Count -eq 0) { throw 'No instances' }
                for ($i = 0; $i -lt $items.Count; $i++) {
                    $text = [string]$items[$i].($probe.Property)
                    if ([string]::IsNullOrWhiteSpace($text)) { throw 'No value' }
                    $suffix = if ($probe.Key -eq 'wmi-drive') { '/' + [string]$items[$i].Index } else { '/' + [string]$i }
                    $result.Add(@{ key = $probe.Key + $suffix; label = $probe.Label + $suffix; value = $text.Trim(); scope = $probe.Class + '.' + $probe.Property + '; inspect only'; problem = $null })
                }
            } catch {
                $result.Add(@{ key = $probe.Key; label = $probe.Label; value = $null; scope = 'Firmware/device observation; inspect only'; problem = 'WMI unavailable or access denied.' })
            }
        }
        ConvertTo-Json -InputObject $result.ToArray() -Depth 4 -Compress
        """;
}

/// <summary>Only fixed built-in scripts reach this boundary. No user input becomes PowerShell code.</summary>
internal static class WindowsPowerShell
{
    public static async Task<string?> Run(string script, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var info = new ProcessStartInfo(Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = system,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)\n" + script)) }) info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        try
        {
            if (!process.Start()) return null;
            process.StandardInput.Close();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(25));
            var output = ReadBounded(process.StandardOutput, deadline.Token);
            var errors = ReadBounded(process.StandardError, deadline.Token);
            try
            {
                await process.WaitForExitAsync(deadline.Token);
                var stdout = await output;
                await errors;
                return process.ExitCode == 0 ? stdout : null;
            }
            catch (Exception e) when (e is OperationCanceledException or InvalidDataException or IOException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (Exception error) when (WindowsIdentityInspection.Expected(error)) { }
                try { await Task.WhenAll(output, errors); } catch (Exception error) when (error is OperationCanceledException or IOException or InvalidDataException) { }
                token.ThrowIfCancellationRequested();
                return null;
            }
        }
        catch (Exception e) when (WindowsIdentityInspection.Expected(e)) { return null; }
    }

    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token);
            if (count == 0) return result.ToString();
            if (result.Length + count > IdentitySnapshots.MaxFileBytes) throw new InvalidDataException("Windows command output exceeded its limit.");
            result.Append(buffer, 0, count);
        }
    }
}
