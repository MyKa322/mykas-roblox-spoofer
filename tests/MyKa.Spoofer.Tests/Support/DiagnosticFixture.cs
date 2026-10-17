using MyKa.Spoofer.Diagnostics;

namespace MyKa.Spoofer.Tests.Support;

internal static class DiagnosticFixture
{
    public static EnvironmentReport Environment => new()
    {
        ObservedAt = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero),
        WindowsEdition = "Professional",
        Processor = "Test processor",
        AvailableProcessors = 8,
        Memory = new(32UL << 30, 12UL << 30),
        Virtualization = new(false, true, true),
        DisplayAdapters = ["Test graphics adapter"],
    };
}
