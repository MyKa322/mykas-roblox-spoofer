using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security;
using Microsoft.Win32;

namespace MyKa.Spoofer.Diagnostics;

public sealed record MemoryObservation(ulong TotalBytes, ulong AvailableBytes);

public sealed record VirtualizationObservation(bool? HypervisorReported, bool FirmwareEnabled, bool SlatAvailable);

public sealed record EnvironmentReport
{
    public DateTimeOffset ObservedAt { get; init; }
    public string? WindowsEdition { get; init; }
    public string? Processor { get; init; }
    public int AvailableProcessors { get; init; }
    public MemoryObservation? Memory { get; init; }
    public VirtualizationObservation? Virtualization { get; init; }
    public IReadOnlyList<string> DisplayAdapters { get; init; } = [];
    public IReadOnlyList<string> Problems { get; init; } = [];

    // Complete means the observations were collected, not that a VM or Roblox will work.
    public bool Complete => ObservedAt != default && !string.IsNullOrWhiteSpace(WindowsEdition)
        && !string.IsNullOrWhiteSpace(Processor) && AvailableProcessors > 0
        && Memory is { TotalBytes: > 0 } && Memory.AvailableBytes <= Memory.TotalBytes
        && Virtualization?.HypervisorReported is not null
        && DisplayAdapters.Count > 0 && Problems.Count == 0;
}

/// <summary>Read-only observations of the current Windows environment, including when it is a guest.</summary>
public static class EnvironmentInspection
{
    public static EnvironmentReport Read()
    {
        var observedAt = DateTimeOffset.UtcNow;
        if (!OperatingSystem.IsWindows())
            return new() { ObservedAt = observedAt, Problems = ["Environment inspection requires Windows."] };

        var problems = new List<string>();
        var edition = Observe(() => RegistryText(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID"),
            "Windows edition", problems);
        var processor = Observe(() => RegistryText(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString"),
            "Processor description", problems);
        var memory = Observe(ReadMemory, "Physical memory", problems);
        var virtualization = Observe(ReadVirtualization, "Virtualization observations", problems);
        if (virtualization is { HypervisorReported: null })
            problems.Add("Hypervisor observation: CPUID is unavailable in this process.");
        var displays = Observe(ReadDisplays, "Display adapter enumeration", problems);

        return new()
        {
            ObservedAt = observedAt,
            WindowsEdition = edition,
            Processor = processor,
            AvailableProcessors = Environment.ProcessorCount,
            Memory = memory,
            Virtualization = virtualization,
            DisplayAdapters = displays ?? [],
            Problems = problems.ToArray(),
        };
    }

    // Each probe can fail independently. Never turn access denial into a negative capability result.
    internal static T? Observe<T>(Func<T?> read, string label, List<string> problems) where T : class
    {
        try
        {
            var value = read();
            if (value is not null) return value;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException
            or Win32Exception or DllNotFoundException or EntryPointNotFoundException)
        {
            // Exceptions may contain local paths. Only the fixed probe label is reported.
        }
        problems.Add($"{label}: unavailable; Windows did not provide this observation.");
        return null;
    }

    private static string? RegistryText(string subkey, string name)
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(subkey, writable: false);
        var value = (key?.GetValue(name) as string)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static MemoryObservation? ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return status.TotalPhysical == 0 || status.AvailablePhysical > status.TotalPhysical
            ? null : new(status.TotalPhysical, status.AvailablePhysical);
    }

    private static VirtualizationObservation ReadVirtualization()
    {
        bool? hypervisor = X86Base.IsSupported && X86Base.CpuId(0, 0).Eax >= 1
            ? (X86Base.CpuId(1, 0).Ecx & int.MinValue) != 0 : null;
        return new(hypervisor, IsProcessorFeaturePresent(21), IsProcessorFeaturePresent(20));
    }

    private static string[]? ReadDisplays()
    {
        var names = new List<string>();
        for (uint index = 0; ; index++)
        {
            var display = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, index, ref display, 0)) break;
            // Mirroring drivers are not graphics adapters. Device IDs and registry paths are never retained.
            if ((display.StateFlags & 0x8) == 0 && !string.IsNullOrWhiteSpace(display.Description))
                names.Add(display.Description.Trim());
        }
        return names.Count == 0 ? null : names.Distinct(StringComparer.Ordinal).ToArray();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessorFeaturePresent(uint feature);

    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice display, uint flags);
}
