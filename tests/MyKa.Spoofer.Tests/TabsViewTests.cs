using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Terminal;
using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Spectre.Console;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class TabsViewTests
{
    private readonly StubPage first = new("Accounts");
    private readonly StubPage second = new("Diagnostics");
    private readonly StubPage third = new("Network adapters");

    private TabsModel Model() => new([first, second, third]);

    private static string[] Frame(TabsModel model, int width, int height, string? webView = "1.0")
        => Capture.Lines(TabsView.Build(model, TabsLayout.For(width, height), LogoMotion.Still, TabsView.StatusLine(webView)), width);

    [Theory]
    [InlineData(120, 30)]
    [InlineData(120, 24)]
    [InlineData(120, 40)]
    [InlineData(80, 24)]
    [InlineData(60, 20)]
    [InlineData(50, 12)]
    [InlineData(200, 60)]
    public void TheFrameFillsEveryRowButTheLastAndNeverExceedsTheWidth(int width, int height)
    {
        var lines = Frame(Model(), width, height);

        Assert.Equal(height - TabsLayout.SpareRows, lines.Length);
        Assert.All(lines, line => Assert.True(Cells.Width(line) <= width, $"'{line}' is wider than {width}"));
    }

    [Fact]
    public void EveryWindowSizeGivesAFrameThatFitsIt()
    {
        var model = Model();

        for (var width = 50; width <= 130; width += 10)
            for (var height = TabsLayout.MinHeight; height <= 60; height += 2)
                Assert.Equal(height - TabsLayout.SpareRows, Frame(model, width, height).Length);
    }

    [Fact]
    public void ShowsTheTitleWithTheVersionCentred()
    {
        var lines = Frame(Model(), 120, 30);

        var title = lines.Single(line => line.Contains("MyKa's Roblox Spoofer v" + TerminalUi.Version));
        Assert.InRange(title.Length - title.TrimStart().Length, 40, 50);
    }

    [Fact]
    public void StatusNamesTheLoginBrowserInWords()
    {
        Assert.Contains(Frame(Model(), 120, 30, "125.0"), line => line.Contains("Login browser: WebView2 ready"));
        Assert.Contains(Frame(Model(), 120, 30, null), line => line.Contains("Login browser: WebView2 unavailable"));
    }

    [Fact]
    public void TheShortLogoIsCompletedBySubtitleSoTheWholeNameShows()
    {
        var lines = Frame(Model(), 120, 30);

        Assert.Contains(lines, line => line.Contains(LogoText.Subtitle));
    }

    [Fact]
    public void TheFullLogoNeedsNoSubtitleAndShortWindowsHaveNoLogo()
    {
        Assert.DoesNotContain(Frame(Model(), 120, 50), line => line.Contains(LogoText.Subtitle));
        Assert.DoesNotContain(Frame(Model(), 120, 16), line => line.Contains(LogoText.Subtitle));
        Assert.Contains(Frame(Model(), 120, 50), line => line.Contains('█'));
        Assert.DoesNotContain(Frame(Model(), 120, 16), line => line.Contains('█'));
    }

    [Fact]
    public void NumbersEveryTabAndShowsAllOfThem()
    {
        var bar = Frame(Model(), 120, 30).Single(line => line.Contains("1 Accounts"));

        Assert.Contains("2 Diagnostics", bar);
        Assert.Contains("3 Network adapters", bar);
    }

    [Fact]
    public void TheRuleIsHeavyOnlyBeneathTheTabInUse()
    {
        var model = Model();
        model.Handle(Typed.Right);

        var strip = TabsView.TabStrip(model, 100);

        var label = " 2 Diagnostics ";
        var start = strip[0].Text.IndexOf(label, StringComparison.Ordinal);
        Assert.Equal(new string('━', label.Length), strip[1].Text.Substring(start, label.Length));
        Assert.Equal(label.Length, strip[1].Text.Count(c => c == '━'));
        Assert.Equal(100, strip[1].Text.Length);
    }

    [Fact]
    public void TheSelectedTabIsHighlightedAndTheOthersAreDim()
    {
        var model = Model();

        var bar = TabsView.TabStrip(model, 100)[0];

        Assert.Equal(Looks.Selected, bar.Spans.Single(span => span.Text == " 1 Accounts ").Style);
        Assert.Equal(Looks.Dim, bar.Spans.Single(span => span.Text == " 2 Diagnostics ").Style);
    }

    [Fact]
    public void ShowsTheSelectedPageAndNoOther()
    {
        var model = Model();
        Assert.Contains(Frame(model, 120, 30), line => line.Contains("Accounts content"));

        model.Handle(Typed.Right);
        var lines = Frame(model, 120, 30);

        Assert.Contains(lines, line => line.Contains("Diagnostics content"));
        Assert.DoesNotContain(lines, line => line.Contains("Accounts content"));
    }

    [Fact]
    public void TheFooterGivesThePageKeysThenTheTabKeys()
    {
        var lines = Frame(Model(), 120, 30);

        Assert.Equal("  Accounts hints", lines[^2].TrimEnd());
        Assert.Contains("switch tab", lines[^1]);
        Assert.Contains("1-3 jump", lines[^1]);
        Assert.Contains("Esc quit", lines[^1]);
    }

    [Fact]
    public void WhileAQuestionIsOpenTheTabKeysAreNotOffered()
    {
        first.Modal = true;

        var lines = Frame(Model(), 120, 30);

        Assert.DoesNotContain(lines, line => line.Contains("Esc quit"));
        Assert.Contains(lines, line => line.Contains("Accounts hints"));
    }

    [Fact]
    public void APageThatReturnsLessIsPaddedToItsFullHeight()
    {
        var lines = Frame(Model(), 120, 30);

        Assert.Equal(29, lines.Length);
        Assert.Equal("  Accounts hints", lines[^2].TrimEnd());
    }

    [Fact]
    public void APageThatReturnsMoreIsCroppedSoTheFooterStaysPut()
    {
        var model = new TabsModel([new TallPage()]);

        var lines = Frame(model, 120, 30);

        Assert.Equal(29, lines.Length);
        Assert.Contains("Tall hints", lines[^2]);
        Assert.DoesNotContain(lines, line => line.Contains("row 99"));
    }

    [Fact]
    public void ATooSmallWindowSaysSoAndHowToLeave()
    {
        var lines = Frame(Model(), 40, 10);

        Assert.Contains(lines, line => line.Contains("too small"));
        Assert.Contains(lines, line => line.Contains("50 x 12"));
        Assert.Contains(lines, line => line.Contains("Esc"));
        Assert.True(lines.Length <= 10);
    }

    [Fact]
    public void LongPageLinesAreCutAtTheWindowEdge()
    {
        var model = new TabsModel([new WidePage()]);

        var lines = Frame(model, 60, 20);

        Assert.All(lines, line => Assert.True(Cells.Width(line) <= 60));
        Assert.Contains(lines, line => line.Contains(Cells.Ellipsis));
    }

    [Fact]
    public void ThePageGetsTheContentWidthAndRows()
    {
        var page = new RecordingPage();

        Frame(new TabsModel([page]), 100, 30);

        Assert.Equal(TabsLayout.For(100, 30).ContentWidth, page.Width);
        Assert.Equal(TabsLayout.For(100, 30).ContentRows, page.Rows);
    }

    [Fact]
    public void TheLogoTurnsBetweenFrames()
    {
        var model = Model();
        var layout = TabsLayout.For(120, 50);

        string[] At(double yaw) => Capture.Lines(
            TabsView.Build(model, layout, new LogoPose(yaw, LogoMotion.FacingPitch), TabsView.StatusLine("1")), 120);

        Assert.NotEqual(At(0), At(1.2));
    }

    private sealed class TallPage : ITabPage
    {
        public string Title => "Tall";
        public bool IsModal => false;
        public string Hints => "Tall hints";
        public void Activate() { }
        public PageResult Handle(ConsoleKeyInfo key) => PageResult.None;
        public IReadOnlyList<Line> Render(int width, int rows) => Enumerable.Range(0, 100).Select(i => Line.Of("row " + i)).ToArray();
    }

    private sealed class WidePage : ITabPage
    {
        public string Title => "Wide";
        public bool IsModal => false;
        public string Hints => "Wide hints";
        public void Activate() { }
        public PageResult Handle(ConsoleKeyInfo key) => PageResult.None;
        public IReadOnlyList<Line> Render(int width, int rows) => [Line.Of(new string('w', 200))];
    }

    private sealed class RecordingPage : ITabPage
    {
        public int Width { get; private set; }
        public int Rows { get; private set; }
        public string Title => "Recording";
        public bool IsModal => false;
        public string Hints => "";
        public void Activate() { }
        public PageResult Handle(ConsoleKeyInfo key) => PageResult.None;

        public IReadOnlyList<Line> Render(int width, int rows)
        {
            Width = width;
            Rows = rows;
            return [];
        }
    }
}
