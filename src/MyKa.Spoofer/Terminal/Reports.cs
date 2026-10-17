using MyKa.Spoofer.Diagnostics;
using System.Globalization;

namespace MyKa.Spoofer.Terminal;

internal abstract record ReportItem;

internal sealed record ReportField(string Label, string Value) : ReportItem;

internal sealed record ReportNote(string Text) : ReportItem;

/// <summary>What the read-only reports say, apart from how a command or a tab lays it out.</summary>
internal static class Reports
{
    public static IReadOnlyList<string> AdapterNotes { get; } =
    [
        "This is an OS observation, not proof of a permanent hardware address.",
        "Use identity manage for stored GUID/MAC changes and hardware inspection.",
    ];

    public static IReadOnlyList<ReportItem> Doctor(DiagnosticReport report, string? webViewVersion)
    {
        var scan = report.Installation;
        var items = new List<ReportItem>
        {
            new ReportField("Windows", report.Os),
            new ReportField("Architecture", report.Architecture),
            new ReportField("Runtime", report.Runtime),
            new ReportNote("Search: default per-user Roblox desktop installation"),
        };
        if (scan.Clients.Count == 0)
            items.Add(new ReportNote(scan.Problems.Count == 0
                ? "Roblox: no client candidate found in that location."
                : "Roblox discovery: incomplete; installation presence could not be determined."));
        else
            items.AddRange(scan.Clients.Select(client => new ReportField("Client candidate",
                $"{client.DirectoryName} | File version: {client.FileVersion ?? "Not reported"}")));
        items.AddRange(scan.Problems.Select(problem => new ReportField("Check incomplete", problem)));
        items.Add(new ReportNote("Store/custom installations are not detected by this check."));
        items.Add(new ReportNote("File presence does not verify the publisher or multi-client compatibility."));
        items.Add(new ReportField("WebView2 Runtime", webViewVersion ?? "Not installed; needed for account login"));
        items.Add(new ReportNote("Environment isolation: not implemented. Identity tools: identity manage."));
        AddEnvironment(items, report.Environment);
        return items;
    }

    private static void AddEnvironment(List<ReportItem> items, EnvironmentReport environment)
    {
        items.Add(new ReportNote("Environment observations: current Windows session, read only"));
        items.Add(new ReportField("Observed UTC", environment.ObservedAt == default ? "Unavailable"
            : environment.ObservedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        items.Add(new ReportField("Windows edition", environment.WindowsEdition ?? "Unavailable"));
        items.Add(new ReportField("Processor", environment.Processor ?? "Unavailable"));
        items.Add(new ReportField("CPUs for this app", environment.AvailableProcessors > 0
            ? environment.AvailableProcessors.ToString(CultureInfo.InvariantCulture) : "Unavailable"));
        items.Add(new ReportField("OS-visible RAM", environment.Memory is { } memory
            ? $"{GiB(memory.TotalBytes)} GiB total; {GiB(memory.AvailableBytes)} GiB available at observation"
            : "Unavailable"));
        items.Add(new ReportField("Hypervisor flag", environment.Virtualization?.HypervisorReported switch
        {
            true => "Yes (host or guest)",
            false => "Not reported by CPUID",
            null => "Unavailable",
        }));
        items.Add(new ReportField("Firmware virt.", Feature(environment.Virtualization?.FirmwareEnabled)));
        items.Add(new ReportField("SLAT", Feature(environment.Virtualization?.SlatAvailable)));
        if (environment.DisplayAdapters.Count == 0)
            items.Add(new ReportField("Display adapters", "Unavailable in this Windows session"));
        else
            items.AddRange(environment.DisplayAdapters.Select(name => new ReportField("Display adapter", name)));
        items.AddRange(environment.Problems.Select(problem => new ReportField("Check incomplete", problem)));
        items.Add(new ReportField("Observation status", environment.Complete ? "Collected" : "Incomplete"));
        items.Add(new ReportNote("CPU flags reflect what this environment exposes. An active hypervisor can mask host capabilities."));
        items.Add(new ReportNote("A hypervisor flag does not distinguish a Windows host from a guest or prove Hyper-V is installed."));
        items.Add(new ReportNote("Display names do not establish GPU passthrough support, available VRAM, or capacity for three clients."));
        items.Add(new ReportField("Player test", "Not tested"));
        items.Add(new ReportField("Public egress", "Not checked; no network probe was sent"));
        items.Add(new ReportNote("Next: validate one complete environment before attempting three concurrent accounts. See docs/environment-preflight.md."));
    }

    private static string Feature(bool? value) => value switch
    {
        true => "Reported available",
        false => "Not exposed to this Windows environment",
        null => "Unavailable",
    };

    private static string GiB(ulong bytes) => (bytes / 1073741824d).ToString("0.0", CultureInfo.InvariantCulture);
}
