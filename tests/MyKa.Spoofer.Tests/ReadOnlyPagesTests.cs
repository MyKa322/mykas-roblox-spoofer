using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class ReadOnlyPagesTests
{
    private int reads;

    private static string Show(ITabPage page, int width = 100, int rows = 16) => Capture.Text(page.Render(width, rows));

    private DiagnosticsPage Diagnostics(DiagnosticReport report, string? webView = "125.0.1")
        => new(() =>
        {
            reads++;
            return report;
        }, () => webView);

    private static DiagnosticReport Report(IReadOnlyList<ClientCandidate>? clients = null, params string[] problems)
        => new("Windows 11 Pro", "X64", ".NET 10.0", new InstallationScan(clients ?? [], problems), DiagnosticFixture.Environment);

    private static AdapterInfo Adapter(string name, string type = "Ethernet", string state = "Up", string address = "AA:BB:CC:DD:EE:FF")
        => new(name, type, state, address);

    private AdaptersPage Adapters(AdapterReport report)
        => new(() =>
        {
            reads++;
            return report;
        });

    private static AdapterReport Twelve()
        => new(Enumerable.Range(0, 12).Select(i => Adapter($"adapter{i:00}")).ToArray(), null);

    [Fact]
    public void DiagnosticsShowsTheFactsAndTheLimitsOfTheCheck()
    {
        var page = Diagnostics(Report());
        page.Activate();

        var text = Show(page);

        Assert.Contains("DIAGNOSTICS", text);
        Assert.Contains("read only", text);
        Assert.Contains("Windows", text);
        Assert.Contains("Windows 11 Pro", text);
        Assert.Contains("X64", text);
        Assert.Contains(".NET 10.0", text);
        Assert.Contains("WebView2 Runtime", text);
        Assert.Contains("125.0.1", text);
        Assert.Contains("Roblox: no client candidate found in that location.", text);
        Assert.Contains("File presence does not verify the publisher", text);
        Assert.Contains("not implemented", text);
    }

    [Fact]
    public void DiagnosticsDoesNotClaimRobloxIsAbsentWhenTheCheckWasIncomplete()
    {
        var page = Diagnostics(Report(null, "The default Roblox installation directory could not be fully inspected."));
        page.Activate();

        var text = Show(page);

        Assert.Contains("installation presence could not be determined", text);
        Assert.DoesNotContain("no client candidate found", text);
        Assert.Contains("Check incomplete", text);
    }

    [Fact]
    public void DiagnosticsListsClientCandidatesWithoutInventingAVersion()
    {
        var page = Diagnostics(Report([new ClientCandidate("version-abc", null), new ClientCandidate("version-def", "0.600.1")]));
        page.Activate();

        var text = Show(page);

        Assert.Contains("version-abc | File version: Not reported", text);
        Assert.Contains("version-def | File version: 0.600.1", text);
    }

    [Fact]
    public void DiagnosticsSaysWhenTheLoginBrowserIsMissing()
    {
        var page = Diagnostics(Report(), webView: null);
        page.Activate();

        Assert.Contains("Not installed; needed for account login", Show(page));
    }

    [Fact]
    public void DiagnosticsChecksOnceThenOnlyWhenAsked()
    {
        var page = Diagnostics(Report());

        page.Activate();
        page.Activate();
        Assert.Equal(1, reads);

        Assert.True(page.Handle(Typed.Letter('r')).Changed);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void DiagnosticsShowsNothingUntilItIsActivated()
    {
        var page = Diagnostics(Report());

        var text = Show(page);

        Assert.DoesNotContain("Windows 11 Pro", text);
        Assert.Equal(0, reads);
    }

    [Fact]
    public void DiagnosticsScrollsWhenTheRoomIsShortAndSaysWhere()
    {
        var page = Diagnostics(Report());
        page.Activate();

        var top = Show(page, 100, 6);
        Assert.Contains("1–4 of", top);
        Assert.Contains("Windows 11 Pro", top);
        Assert.DoesNotContain("not implemented", top);

        page.Handle(Typed.End);
        var bottom = Show(page, 100, 6);
        Assert.Contains("Player test", bottom);
        Assert.Contains("Not tested", bottom);
        Assert.DoesNotContain("Windows 11 Pro", bottom);

        page.Handle(Typed.Home);
        Assert.Contains("Windows 11 Pro", Show(page, 100, 6));
    }

    [Fact]
    public void DiagnosticsDoesNotScrollWhenEverythingFits()
    {
        var page = Diagnostics(Report());
        page.Activate();

        var text = Show(page, 100, 30);

        Assert.DoesNotContain(" of ", text.Split('\n')[0]);
        Assert.Contains("not implemented", text);
    }

    [Fact]
    public void DiagnosticsWrapsLongNotesInsteadOfCuttingThem()
    {
        var page = Diagnostics(Report());
        page.Activate();

        var lines = page.Render(46, 40);

        Assert.Contains("multi-client compatibility.", Capture.Text(lines));
        Assert.All(lines.Skip(2), line => Assert.True(line.Width <= 46, $"'{line.Text}' is wider than 46"));
    }

    [Fact]
    public void DiagnosticsNeverReturnsMoreRowsThanItWasGiven()
    {
        var page = Diagnostics(Report([new ClientCandidate("version-abc", "1")], "one", "two"));
        page.Activate();

        for (var rows = 4; rows < 30; rows++) Assert.True(page.Render(60, rows).Count <= rows);
    }

    [Fact]
    public void DiagnosticsIgnoresKeysItDoesNotUse()
    {
        var page = Diagnostics(Report());
        page.Activate();

        Assert.False(page.Handle(Typed.Letter('x')).Changed);
        Assert.False(page.Handle(Typed.Enter).Changed);
        Assert.Equal(1, reads);
    }

    [Fact]
    public void AdaptersShowsNameStatusAndCurrentAddress()
    {
        var page = Adapters(new AdapterReport([Adapter("Ethernet 2", "Ethernet", "Up", "00:11:22:33:44:55")], null));
        page.Activate();

        var text = Show(page);

        Assert.Contains("NETWORK ADAPTERS", text);
        Assert.Contains("Name", text);
        Assert.Contains("Type / status", text);
        Assert.Contains("Current address", text);
        Assert.Contains("Ethernet 2", text);
        Assert.Contains("Ethernet / Up", text);
        Assert.Contains("00:11:22:33:44:55", text);
    }

    [Fact]
    public void AdaptersAlwaysKeepTheHonestyNotesInView()
    {
        var page = Adapters(Twelve());
        page.Activate();

        foreach (var key in new[] { Typed.Home, Typed.PageDown, Typed.End })
        {
            page.Handle(key);
            var text = Show(page, 100, 10);
            Assert.Contains("not proof of a permanent hardware address", text);
            Assert.Contains("identity manage", text);
        }
    }

    [Fact]
    public void AdaptersScrollThroughALongList()
    {
        var page = Adapters(Twelve());
        page.Activate();

        var top = Show(page, 100, 10);
        Assert.Contains("adapter00", top);
        Assert.DoesNotContain("adapter11", top);
        Assert.Contains("1–4 of 12", top);

        page.Handle(Typed.End);
        var bottom = Show(page, 100, 10);
        Assert.Contains("adapter11", bottom);
        Assert.DoesNotContain("adapter00", bottom);
        Assert.Contains("9–12 of 12", bottom);

        page.Handle(Typed.PageUp);
        Assert.Contains("adapter0", Show(page, 100, 10));
        page.Handle(Typed.Down);
        page.Handle(Typed.Up);
        page.Handle(Typed.Home);
        Assert.Contains("adapter00", Show(page, 100, 10));
    }

    [Fact]
    public void AdaptersDropTheStatusColumnInANarrowWindow()
    {
        var page = Adapters(new AdapterReport([Adapter("Ethernet 2")], null));
        page.Activate();

        var wide = Show(page, 100);
        var narrow = Show(page, 50);

        Assert.Contains("Ethernet / Up", wide);
        Assert.DoesNotContain("Type / status", narrow);
        Assert.Contains("AA:BB:CC:DD:EE:FF", narrow);
    }

    [Fact]
    public void AdaptersShowAProblemInsteadOfAList()
    {
        var page = Adapters(new AdapterReport([], "Windows could not provide the network adapter inventory."));
        page.Activate();

        var text = Show(page);

        Assert.Contains("Error: Windows could not provide the network adapter inventory.", text);
        Assert.DoesNotContain("Current address", text);
    }

    [Fact]
    public void AdaptersSayWhenWindowsReportsNone()
    {
        var page = Adapters(new AdapterReport([], null));
        page.Activate();

        Assert.Contains("No adapters were reported by Windows.", Show(page));
    }

    [Fact]
    public void AdaptersReadOnceThenOnlyWhenAsked()
    {
        var page = Adapters(Twelve());

        page.Activate();
        page.Activate();
        Assert.Equal(1, reads);

        page.Handle(Typed.Letter('r'));
        Assert.Equal(2, reads);
    }

    [Fact]
    public void AdaptersNeverReturnMoreRowsThanTheyWereGiven()
    {
        var page = Adapters(Twelve());
        page.Activate();

        for (var rows = 4; rows < 30; rows++)
            foreach (var width in new[] { 40, 50, 80, 120 })
                Assert.True(page.Render(width, rows).Count <= rows, $"{rows} rows x {width}");
    }

    [Fact]
    public void ControlCharactersInAnAdapterNameNeverReachTheScreen()
    {
        var page = Adapters(new AdapterReport([Adapter("Wi-Fi\u001b[2J‮")], null));
        page.Activate();

        var text = Show(page);

        Assert.DoesNotContain('\u001b', text);
        Assert.DoesNotContain('‮', text);
        Assert.Contains("Wi-Fi[2J", text);
    }
}
