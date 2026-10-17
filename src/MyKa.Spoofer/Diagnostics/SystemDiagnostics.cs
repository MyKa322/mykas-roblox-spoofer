using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace MyKa.Spoofer.Diagnostics;

public sealed record DiagnosticReport(string Os, string Architecture, string Runtime, InstallationScan Installation,
    EnvironmentReport Environment);
public sealed record AdapterInfo(string Name, string Type, string State, string Address);
public sealed record AdapterReport(IReadOnlyList<AdapterInfo> Adapters, string? Problem);

public static class SystemDiagnostics
{
    public static DiagnosticReport Read()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var scan = string.IsNullOrWhiteSpace(localData)
            ? new InstallationScan([], ["The current user's local data directory is unavailable."])
            : InstallationDiscovery.Read(Path.Combine(localData, "Roblox", "Versions"));
        return new(RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription, scan, EnvironmentInspection.Read());
    }

    public static AdapterReport ReadAdapters()
    {
        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a =>
                {
                    var bytes = a.GetPhysicalAddress().GetAddressBytes();
                    var address = bytes.Length == 0 ? "Not reported" : string.Join(":", bytes.Select(b => b.ToString("X2")));
                    return new AdapterInfo(a.Name, a.NetworkInterfaceType.ToString(), a.OperationalStatus.ToString(), address);
                }).ToArray();
            return new(adapters, null);
        }
        catch (Exception e) when (e is NetworkInformationException or System.Security.SecurityException)
        {
            return new([], "Windows could not provide the network adapter inventory.");
        }
    }
}
