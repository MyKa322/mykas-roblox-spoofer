using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Terminal;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

[Collection("Console")]
public sealed class ReportsTests
{
    private static DiagnosticReport Report(IReadOnlyList<ClientCandidate>? clients = null, params string[] problems)
        => new("Windows 11 Pro", "X64", ".NET 10.0", new InstallationScan(clients ?? [], problems), DiagnosticFixture.Environment);

    private static string Printed(Action<TerminalUi> action)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            action(new TerminalUi(true));
        }
        finally { Console.SetOut(previous); }
        return output.ToString().Replace("\r\n", "\n");
    }

    [Fact]
    public void DoctorItemsAreInTheOrderTheCommandHasAlwaysPrintedThem()
    {
        var items = Reports.Doctor(Report(), "125.0");

        Assert.Equal(
        [
            new ReportField("Windows", "Windows 11 Pro"),
            new ReportField("Architecture", "X64"),
            new ReportField("Runtime", ".NET 10.0"),
            new ReportNote("Search: default per-user Roblox desktop installation"),
            new ReportNote("Roblox: no client candidate found in that location."),
            new ReportNote("Store/custom installations are not detected by this check."),
            new ReportNote("File presence does not verify the publisher or multi-client compatibility."),
            new ReportField("WebView2 Runtime", "125.0"),
            new ReportNote("Environment isolation: not implemented. Identity tools: identity manage."),
        ], items.Take(9));
    }

    [Fact]
    public void ClientCandidatesAndProblemsAreListedAndAbsenceIsNotClaimed()
    {
        var items = Reports.Doctor(Report([new ClientCandidate("version-a", null)], "A linked installation entry was skipped."), null);

        Assert.Contains(new ReportField("Client candidate", "version-a | File version: Not reported"), items);
        Assert.Contains(new ReportField("Check incomplete", "A linked installation entry was skipped."), items);
        Assert.Contains(new ReportField("WebView2 Runtime", "Not installed; needed for account login"), items);
        Assert.DoesNotContain(items.OfType<ReportNote>(), note => note.Text.Contains("no client candidate"));
    }

    [Fact]
    public void AnIncompleteSearchSaysThePresenceCouldNotBeDetermined()
    {
        var items = Reports.Doctor(Report(null, "The default Roblox installation directory could not be fully inspected."), "1");

        Assert.Contains(new ReportNote("Roblox discovery: incomplete; installation presence could not be determined."), items);
    }

    [Fact]
    public void TheDoctorCommandStillPrintsPlainLabelAndValueLines()
    {
        var code = 0;

        var text = Printed(ui => code = ui.Doctor(Report()));

        var lines = text.Split('\n');
        Assert.Equal(0, code);
        Assert.Equal("", lines[0]);
        Assert.Equal("DIAGNOSTICS", lines[1]);
        Assert.Equal("Windows: Windows 11 Pro", lines[2]);
        Assert.Equal("Architecture: X64", lines[3]);
        Assert.Equal(".NET 10.0", lines[4].Replace("Runtime: ", ""));
        Assert.Equal("Search: default per-user Roblox desktop installation", lines[5]);
        Assert.Equal("Roblox: no client candidate found in that location.", lines[6]);
        Assert.Equal("Store/custom installations are not detected by this check.", lines[7]);
        Assert.Equal("File presence does not verify the publisher or multi-client compatibility.", lines[8]);
        Assert.StartsWith("WebView2 Runtime: ", lines[9]);
        Assert.Equal("Environment isolation: not implemented. Identity tools: identity manage.", lines[10]);
    }

    [Fact]
    public void TheDoctorCommandReturnsOneWhenTheCheckWasIncomplete()
    {
        var code = -1;

        Printed(ui => code = ui.Doctor(Report(null, "An installation entry could not be inspected.")));

        Assert.Equal(1, code);
    }

    [Fact]
    public void TheAdaptersCommandStillPrintsOneBlockPerAdapterThenTheNotes()
    {
        var code = -1;
        var report = new AdapterReport([new AdapterInfo("Ethernet", "Ethernet", "Up", "AA:BB:CC:DD:EE:FF")], null);

        var text = Printed(ui => code = ui.Adapters(report));

        Assert.Equal(0, code);
        Assert.Equal(
            "\nNETWORK ADAPTERS | Read only\n\nName: Ethernet\nType / status: Ethernet / Up\nCurrent address: AA:BB:CC:DD:EE:FF\n\n"
            + "This is an OS observation, not proof of a permanent hardware address.\n"
            + "Use identity manage for stored GUID/MAC changes and hardware inspection.\n", text);
    }

    [Fact]
    public void TheAdaptersCommandReportsAProblemAndFails()
    {
        var code = 0;

        var text = Printed(ui => code = ui.Adapters(new AdapterReport([], "Windows could not provide the network adapter inventory.")));

        Assert.Equal(1, code);
        Assert.Contains("Error: Windows could not provide the network adapter inventory.", text);
        Assert.DoesNotContain("not proof of a permanent hardware address", text);
    }
}
