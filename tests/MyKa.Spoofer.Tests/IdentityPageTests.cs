using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class IdentityPageTests
{
    private static string Show(IdentityPage page, int width = 80, int rows = 20) => Capture.Text(page.Render(width, rows));

    [Fact]
    public void VisitingThePageDoesNotReadOrChangeSystemSettings()
    {
        var opened = 0;
        var page = new IdentityPage(() => opened++);
        page.Activate();
        Assert.Contains("Open system identity tools", Show(page));
        Assert.Equal(0, opened);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnterAndOExplainTheActionBeforeOpeningTheExistingWindow(bool enter)
    {
        var opened = 0;
        var page = new IdentityPage(() => opened++);
        var action = page.Handle(enter ? Typed.Enter : Typed.Letter('o'));
        Assert.True(action.Changed);
        Assert.NotNull(action.Work);
        Assert.Equal(0, opened);
        Assert.Contains("Opening system identity tools", Show(page));
        action.Work!();
        Assert.Equal(1, opened);
        Assert.Contains("System tools closed", Show(page));
    }

    [Fact]
    public void AWindowFailureKeepsTheOpenActionVisibleAtMinimumSize()
    {
        var page = new IdentityPage(() => throw new IOException("private-path-or-device-value"));
        page.Handle(Typed.Enter).Work!();
        var lines = page.Render(46, 4);
        Assert.True(lines.Count <= 4);
        Assert.Contains("[Enter / O] Open system identity tools", Capture.Text(lines));
        Assert.DoesNotContain("private-path-or-device-value", Capture.Text(lines));
        Assert.Contains("could not open", Show(page));
    }

    [Fact]
    public void CancellationPropagatesToTheApplication()
    {
        var page = new IdentityPage(() => throw new OperationCanceledException());
        Assert.Throws<OperationCanceledException>(() => page.Handle(Typed.Enter).Work!());
    }

    [Fact]
    public void ScrollingMakesTheLimitsReadableAndNeverHidesTheAction()
    {
        var page = new IdentityPage(() => { });
        Assert.Contains("SNAPSHOTS", Show(page, 46, 8));
        page.Handle(Typed.End);
        Assert.Contains("Privileged changes", Show(page, 46, 8));
        Assert.Contains("Open system identity tools", Show(page, 46, 8));
        page.Activate();
        Assert.Contains("Privileged changes", Show(page, 46, 8));
        page.Handle(Typed.Home);
        Assert.Contains("SNAPSHOTS", Show(page, 46, 8));
    }

    [Fact]
    public void CapabilityDescriptionsSeparateAvailableToolsAndUnimplementedChanges()
    {
        var text = Show(new IdentityPage(() => { }), 100, 80);
        foreach (var term in new[] { "Capture Before and After", "backup", "MachineGuid", "HwProfileGuid", "ComputerHardwareId", "MAC", "DNS", "DHCP", "ROBLOX CLEANUP", "INSPECT ONLY", "NOT IMPLEMENTED", "Firmware spoofing", "public-IP routing" })
            Assert.Contains(term, text);
    }

    [Theory]
    [InlineData(50, 12)]
    [InlineData(60, 20)]
    [InlineData(80, 24)]
    [InlineData(120, 40)]
    public void AllFourTabsAndTheIdentityActionFitEverySupportedSize(int width, int height)
    {
        var opened = 0;
        var page = new IdentityPage(() => opened++);
        var model = new TabsModel([new StubPage("Accounts"), new StubPage("Diagnostics"), new StubPage("Adapters"), page]);
        Assert.Equal(TabKeyResult.Switched, model.Handle(Typed.Digit(4)));
        Assert.Same(page, model.Current);
        var lines = Capture.Lines(TabsView.Build(model, TabsLayout.For(width, height), LogoMotion.Still, TabsView.StatusLine(null)), width);
        var tabBar = Assert.Single(lines, line => line.Contains("1 Accounts"));
        foreach (var label in new[] { "2 Diagnostics", "3 Adapters", "4 Identity" }) Assert.Contains(label, tabBar);
        Assert.DoesNotContain(Cells.Ellipsis, tabBar);
        Assert.Equal(height - 1, lines.Length);
        Assert.All(lines, line => Assert.True(Cells.Width(line) <= width));
        Assert.Contains(lines, line => line.Contains("[Enter / O] Open system identity tools"));
        Assert.Contains("1-4", lines[^1]);
        Assert.Equal(0, opened);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void TheFourthTabsUnderlineTracksBothCompactAndPaddedLabels(int width)
    {
        var model = new TabsModel([new StubPage("Accounts"), new StubPage("Diagnostics"), new StubPage("Adapters"), new IdentityPage(() => { })]);
        model.Handle(Typed.Digit(4));
        var strip = TabsView.TabStrip(model, width);
        var selected = Assert.Single(strip[0].Spans, span => span.Style == Looks.Selected);
        var start = strip[0].Text.IndexOf(selected.Text, StringComparison.Ordinal);
        Assert.Equal(new string('━', Cells.Width(selected.Text)), strip[1].Text.Substring(start, Cells.Width(selected.Text)));
        Assert.True(strip[0].Width <= width);
    }

    [Fact]
    public void TheRealTabLoopDrawsTheActionAndDiscardsKeysQueuedByTheWindow()
    {
        var surface = new FakeSurface { Size = (50, 12) };
        var keys = new FakeKeys();
        var tick = 0;
        var opened = 0;
        var identity = new IdentityPage(() =>
        {
            opened++;
            Assert.Contains("Opening system identity tools", string.Join('\n', Capture.Lines(surface.Frames[^1], 50)));
            keys.Press(Typed.Enter, Typed.Letter('o'), Typed.Escape);
        });
        void Sleep(int _)
        {
            tick++;
            if (tick == 1) keys.Press(Typed.Digit(4));
            if (tick == 2) keys.Press(Typed.Enter);
            if (tick == 5) keys.Press(Typed.Escape);
            if (tick > 10) throw new InvalidOperationException("The test loop did not exit.");
        }
        var app = new TabsApp([new StubPage("Accounts"), new StubPage("Diagnostics"), new StubPage("Adapters"), identity],
            surface, keys, TabsView.StatusLine(null), false, () => 0, Sleep, CancellationToken.None);
        Assert.Equal(0, app.Run());
        Assert.Equal(1, opened);
        Assert.Equal(5, tick);
        Assert.Equal(1, surface.Left);
    }
}
