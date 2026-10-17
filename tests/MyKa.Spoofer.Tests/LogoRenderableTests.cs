using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Terminal;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class LogoRenderableTests
{
    private static readonly LogoFrame Frame = new LogoRenderer(LogoLayout.Compact).Render(LogoMotion.Still);

    private static RenderOptions Options()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(new StringWriter()),
        });
        return RenderOptions.Create(console, console.Profile.Capabilities);
    }

    private static List<List<Segment>> Lines(int maxWidth)
        => Segment.SplitLines(new LogoRenderable(Frame).Render(Options(), maxWidth)).Select(line => line.ToList()).ToList();

    [Fact]
    public void MeasuresAsWideAsTheLogo()
    {
        var measurement = new LogoRenderable(Frame).Measure(Options(), 200);

        Assert.Equal(new Measurement(LogoLayout.Compact.Columns, LogoLayout.Compact.Columns), measurement);
    }

    [Fact]
    public void NeverClaimsMoreWidthThanItIsOffered()
        => Assert.Equal(new Measurement(20, 20), new LogoRenderable(Frame).Measure(Options(), 20));

    [Fact]
    public void RendersOneLinePerTerminalRowEachExactlyAsWide()
    {
        var lines = Lines(200);

        Assert.Equal(LogoLayout.Compact.Rows, lines.Count);
        Assert.All(lines, line => Assert.Equal(LogoLayout.Compact.Columns, Segment.CellCount(line)));
    }

    [Fact]
    public void CropsToTheWidthItIsGiven()
    {
        var lines = Lines(20);

        Assert.All(lines, line => Assert.Equal(20, Segment.CellCount(line)));
    }

    [Fact]
    public void JoinsNeighbouringCellsOfTheSameColourIntoOneSegment()
    {
        var uniform = new LogoFrame(10, 2, 2);
        for (var column = 0; column < 10; column++) uniform[column, 0] = uniform[column, 1] = new Sample(SurfaceKind.Front, 0.9f, 0.2f);

        var line = Segment.SplitLines(new LogoRenderable(uniform).Render(Options(), 80)).Single().ToList();

        var run = Assert.Single(line);
        Assert.Equal(10, run.Text.Length);
    }

    [Fact]
    public void StartsANewSegmentWhenTheColourChanges()
    {
        var banded = new LogoFrame(6, 2, 2);
        for (var column = 0; column < 6; column++)
            banded[column, 0] = banded[column, 1] = new Sample(SurfaceKind.Front, 0.9f, column < 3 ? 0f : 1f);

        var line = Segment.SplitLines(new LogoRenderable(banded).Render(Options(), 80)).Single().ToList();

        Assert.Equal(new[] { 3, 3 }, line.Select(segment => segment.Text.Length));
    }

    [Fact]
    public void ColoursTheLettersAndLeavesTheBackgroundUnstyled()
    {
        var all = Lines(200).SelectMany(line => line).ToList();

        Assert.Contains(all, segment => segment.Text.Contains('▀') && !segment.Style.Equals(Style.Plain));
        Assert.Contains(all, segment => segment.Text.Trim().Length == 0 && segment.Style.Equals(Style.Plain));
    }
}
