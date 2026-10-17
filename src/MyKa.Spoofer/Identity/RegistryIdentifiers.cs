using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace MyKa.Spoofer.Identity;

internal sealed record RegistryStringData(string Value, bool Terminated, bool EmbeddedNull);

internal static class RegistryIdentifiers
{
    internal const string ProfilePath = @"SYSTEM\CurrentControlSet\Control\IDConfigDB\Hardware Profiles\0001";
    internal const string InformationPath = @"SYSTEM\CurrentControlSet\Control\SystemInformation";
    internal const string BiosPath = @"HARDWARE\DESCRIPTION\System\BIOS";

    internal static (string Path, string Name) Location(IdentitySetting setting) => setting switch
    {
        IdentitySetting.MachineGuid => (WindowsIdentityInspection.CryptographyPath, "MachineGuid"),
        IdentitySetting.HardwareProfileGuid => (ProfilePath, "HwProfileGuid"),
        IdentitySetting.ComputerHardwareId => (InformationPath, "ComputerHardwareId"),
        _ => throw new InvalidDataException("Unsupported system registry setting."),
    };

    internal static RegistryStringData Read(RegistryKey key, string name)
    {
        uint bytes = 0;
        var result = RegQueryValueEx(key.Handle, name, IntPtr.Zero, out var kind, null, ref bytes);
        if (result != 0 && result != 234) throw new Win32Exception(result);
        if (kind != (uint)RegistryValueKind.String || bytes > 16384 || (bytes & 1) != 0)
            throw new InvalidDataException("The existing registry value is not a bounded REG_SZ string.");
        var buffer = new byte[bytes];
        result = RegQueryValueEx(key.Handle, name, IntPtr.Zero, out kind, buffer, ref bytes);
        if (result != 0) throw new Win32Exception(result);
        if (kind != (uint)RegistryValueKind.String || bytes > buffer.Length || (bytes & 1) != 0)
            throw new InvalidDataException("Registry data changed while being read.");
        return Decode(buffer.AsSpan(0, (int)bytes));
    }

    internal static RegistryStringData Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 16384 || (bytes.Length & 1) != 0) throw new InvalidDataException("Invalid UTF-16 registry string length.");
        var text = System.Text.Encoding.Unicode.GetString(bytes);
        var terminated = text.EndsWith('\0');
        if (terminated) text = text[..^1];
        return new(text, terminated, text.Contains('\0'));
    }

    public static string ReadGuid(IdentitySetting setting)
    {
        var location = Location(setting);
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(location.Path, false) ?? throw new InvalidOperationException(location.Name + " does not exist; no synthetic value will be created.");
        var data = Read(key, location.Name);
        if (!data.Terminated || data.EmbeddedNull || !Guid.TryParse(data.Value, out _))
            throw new InvalidOperationException(location.Name + " is not a normally terminated GUID REG_SZ value. It will not be changed.");
        return data.Value;
    }

    public static void Inspect(List<IdentityValue> values)
    {
        foreach (var (id, path, name, scope) in new[]
        {
            ("profile-guid", ProfilePath, "HwProfileGuid", "Stored hardware profile GUID; changing it does not alter firmware"),
            ("computer-hardware-id", InformationPath, "ComputerHardwareId", "Stored Windows value; not a replacement for firmware-derived IDs"),
            ("bios-board-registry", BiosPath, "BaseBoardSerialNumber", "Registry mirror, if exposed; inspect only"),
            ("bios-uuid-registry", BiosPath, "SystemUUID", "Registry mirror, if exposed; inspect only"),
            ("machine-guid-encoding", WindowsIdentityInspection.CryptographyPath, "MachineGuid", "REG_SZ encoding observation"),
        })
        {
            try
            {
                using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = machine.OpenSubKey(path, false) ?? throw new IOException();
                var data = Read(key, name);
                if (id != "machine-guid-encoding")
                    values.Add(new(id, name + " (registry)", data.Value.Length == 0 ? "<empty>" : IdentitySnapshots.Display(data.Value.Replace("\0", "\\0", StringComparison.Ordinal)), scope));
                values.Add(new(id + "/encoding", name + " | REG_SZ encoding", $"Trailing NUL: {data.Terminated}; embedded NUL: {data.EmbeddedNull}", "Encoding is observed; malformed strings are never created"));
            }
            catch (Exception e) when (WindowsIdentityInspection.Expected(e))
            {
                values.Add(new(id, name + " (registry)", null, scope, "Value absent, inaccessible, or not REG_SZ."));
            }
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int RegQueryValueEx(SafeRegistryHandle key, string name, IntPtr reserved,
        out uint type, [Out] byte[]? data, ref uint size);
}
