using System.ComponentModel;
using System.Security;
using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Terminal;
using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

[Collection("Console")]
public sealed class EnvironmentInspectionTests
{
    private static DiagnosticReport Report(EnvironmentReport environment)
        => new("Windows", "X64", ".NET", new([], []), environment);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void CpuFlagsAreObservationsAndNeverAPlayerCompatibilityClaim(bool? hypervisor)
    {
        var environment = DiagnosticFixture.Environment with { Virtualization = new(hypervisor, false, false) };
        var items = Reports.Doctor(Report(environment), "1");

        Assert.Contains(new ReportField("Hypervisor flag", hypervisor switch
        {
            true => "Yes (host or guest)",
            false => "Not reported by CPUID",
            null => "Unavailable",
        }), items);
        Assert.Contains(new ReportField("Firmware virt.", "Not exposed to this Windows environment"), items);
        Assert.Contains(new ReportField("SLAT", "Not exposed to this Windows environment"), items);
        Assert.Contains(new ReportField("Player test", "Not tested"), items);
        Assert.Contains(new ReportField("Public egress", "Not checked; no network probe was sent"), items);
        Assert.Equal(hypervisor.HasValue, environment.Complete);
    }

    [Fact]
    public void AnUncollectedEnvironmentCannotBecomeACompleteCheck()
    {
        var environment = new EnvironmentReport();
        var items = Reports.Doctor(Report(environment), null);

        Assert.False(environment.Complete);
        Assert.Contains(new ReportField("OS-visible RAM", "Unavailable"), items);
        Assert.Contains(new ReportField("Display adapters", "Unavailable in this Windows session"), items);
        Assert.Contains(new ReportField("Observation status", "Incomplete"), items);
    }

    [Fact]
    public void AccessFailuresRemainUnknownAndDoNotLeakExceptionDetails()
    {
        Exception[] failures = [new UnauthorizedAccessException("private path"), new SecurityException("private path"),
            new IOException("private path"), new Win32Exception(5, "private path"),
            new DllNotFoundException("private path"), new EntryPointNotFoundException("private path")];
        foreach (var failure in failures)
        {
            var problems = new List<string>();
            var value = EnvironmentInspection.Observe<string>(() => throw failure, "Memory", problems);
            Assert.Null(value);
            Assert.Equal("Memory: unavailable; Windows did not provide this observation.", Assert.Single(problems));
            Assert.DoesNotContain("private path", problems[0]);
        }
    }

    [Fact]
    public void OneUnavailableProbeDoesNotDiscardOtherObservations()
    {
        var problems = new List<string>();
        Assert.Null(EnvironmentInspection.Observe<string>(() => null, "Edition", problems));
        Assert.Equal("Processor", EnvironmentInspection.Observe(() => "Processor", "CPU", problems));
        Assert.Single(problems);
        Assert.Throws<InvalidOperationException>(() => EnvironmentInspection.Observe<string>(
            () => throw new InvalidOperationException(), "Bug", problems));
    }

    [Fact]
    public void MemoryNumbersUseGiBAndIncludeTheObservationTime()
    {
        var items = Reports.Doctor(Report(DiagnosticFixture.Environment), "1");
        Assert.Contains(new ReportField("Observed UTC", "2026-10-16 12:00:00"), items);
        Assert.Contains(new ReportField("OS-visible RAM", "32.0 GiB total; 12.0 GiB available at observation"), items);
        Assert.Contains(new ReportField("CPUs for this app", "8"), items);
    }

    [Fact]
    public void InconsistentOrMissingObservationsCannotClaimCompletion()
    {
        var complete = DiagnosticFixture.Environment;
        Assert.True(complete.Complete);
        EnvironmentReport[] incomplete = [complete with { Memory = new(0, 0) }, complete with { Memory = new(8, 9) },
            complete with { Memory = null }, complete with { ObservedAt = default },
            complete with { WindowsEdition = " " }, complete with { Processor = null },
            complete with { AvailableProcessors = 0 }, complete with { Virtualization = null },
            complete with { DisplayAdapters = [] }, complete with { Problems = ["Probe failed"] }];
        Assert.All(incomplete, value => Assert.False(value.Complete));
    }

    [Fact]
    public void CommandReturnsIncompleteEvenWhenInstallationDiscoverySucceeded()
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var code = new TerminalUi(true).Doctor(Report(DiagnosticFixture.Environment with { Memory = null }));
            Assert.Equal(1, code);
            Assert.Contains("Observation status: Incomplete", output.ToString());
            Assert.Contains("Player test: Not tested", output.ToString());
        }
        finally { Console.SetOut(previous); }
    }

    [Theory]
    [InlineData(50, 12)]
    [InlineData(80, 24)]
    public void TabCanReachEnvironmentResultsAndRefreshBackToTheTop(int width, int rows)
    {
        var environment = DiagnosticFixture.Environment;
        var page = new DiagnosticsPage(() => Report(environment), () => "1");
        page.Activate();
        var seen = new List<string>();
        for (var i = 0; i < 150; i++)
        {
            var frame = page.Render(width, rows);
            Assert.True(frame.Count <= rows);
            Assert.All(frame, line => Assert.True(line.Width <= width));
            seen.Add(Capture.Text(frame));
            page.Handle(Typed.Down);
        }
        var text = string.Join('\n', seen);
        Assert.Contains("OS-visible RAM", text);
        Assert.Contains("Observation status", text);
        Assert.Contains("Player test", text);
        Assert.Contains("Not tested", text);

        environment = environment with { Memory = null };
        page.Handle(Typed.Letter('r'));
        Assert.Contains("Windows", Capture.Text(page.Render(width, rows)));
        page.Handle(Typed.End);
        Assert.Contains("Next:", Capture.Text(page.Render(width, rows)));
    }

    [Fact]
    public void DeviceDescriptionsCannotInjectTerminalControlCharacters()
    {
        var environment = DiagnosticFixture.Environment with { DisplayAdapters = ["GPU\u001b[2J\u202e"] };
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            new TerminalUi(true).Doctor(Report(environment));
            Assert.DoesNotContain('\u001b', output.ToString());
            Assert.DoesNotContain('\u202e', output.ToString());
            Assert.Contains("GPU[2J", output.ToString());
        }
        finally { Console.SetOut(previous); }
    }
}
