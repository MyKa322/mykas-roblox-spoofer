using MyKa.Spoofer.Branding;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class LogoPainterTests
{
    private const char FullBlock = '█';
    private const char DarkShade = '▓';
    private const char MediumShade = '▒';
    private const char LightShade = '░';
    private const char UpperHalf = '▀';
    private const char LowerHalf = '▄';

    private static readonly Sample None = default;
    private static readonly Sample Shadow = new(SurfaceKind.Shadow, 0, 0);

    private static Sample Of(SurfaceKind kind, float light = 0.9f, float tint = 0f) => new(kind, light, tint);

    private static LogoCell Paint(bool ascii, Sample upper, Sample lower)
    {
        var frame = new LogoFrame(1, 2, 2);
        frame[0, 0] = upper;
        frame[0, 1] = lower;
        return new LogoPainter(ascii).Paint(frame)[0, 0];
    }

    private static int Brightness(Rgb color) => color.R + color.G + color.B;

    [Fact]
    public void EmptyCellsAreBlank()
    {
        Assert.Equal(LogoCell.Blank, Paint(false, None, None));
        Assert.Equal(LogoCell.Blank, Paint(true, None, None));
    }

    [Fact]
    public void ASolidUpperHalfIsAnUpperHalfBlock()
    {
        var front = Of(SurfaceKind.Front);

        var cell = Paint(false, front, None);

        Assert.Equal(UpperHalf, cell.Glyph);
        Assert.Equal(LogoPalette.Of(front), cell.Foreground);
        Assert.Null(cell.Background);
    }

    [Fact]
    public void ASolidLowerHalfIsALowerHalfBlock()
    {
        var front = Of(SurfaceKind.Front);

        var cell = Paint(false, None, front);

        Assert.Equal(LowerHalf, cell.Glyph);
        Assert.Equal(LogoPalette.Of(front), cell.Foreground);
        Assert.Null(cell.Background);
    }

    [Fact]
    public void TwoMatchingFrontSamplesFillTheCell()
    {
        var front = Of(SurfaceKind.Front);

        var cell = Paint(false, front, front);

        Assert.Equal(FullBlock, cell.Glyph);
        Assert.Equal(LogoPalette.Of(front), cell.Foreground);
    }

    [Fact]
    public void TwoFrontSamplesOfDifferentColourKeepBothColours()
    {
        var upper = Of(SurfaceKind.Front, tint: 0f);
        var lower = Of(SurfaceKind.Front, tint: 1f);

        var cell = Paint(false, upper, lower);

        Assert.Equal(UpperHalf, cell.Glyph);
        Assert.Equal(LogoPalette.Of(upper), cell.Foreground);
        Assert.Equal(LogoPalette.Of(lower), cell.Background);
    }

    [Fact]
    public void AFaceMeetingAWallIsSplitIntoTwoColours()
    {
        var front = Of(SurfaceKind.Front);
        var side = Of(SurfaceKind.Side, 0.5f);

        var cell = Paint(false, front, side);

        Assert.Equal(UpperHalf, cell.Glyph);
        Assert.Equal(LogoPalette.Of(front), cell.Foreground);
        Assert.Equal(LogoPalette.Of(side), cell.Background);
    }

    [Fact]
    public void RaisedAndRecessedEdgesOfTheFrontShowAsTwoShades()
    {
        var cell = Paint(false, Of(SurfaceKind.FrontHigh), Of(SurfaceKind.FrontLow));

        Assert.Equal(UpperHalf, cell.Glyph);
        Assert.True(Brightness(cell.Foreground!.Value) > Brightness(cell.Background!.Value));
    }

    [Theory]
    [InlineData(SurfaceKind.Top, DarkShade)]
    [InlineData(SurfaceKind.Side, MediumShade)]
    [InlineData(SurfaceKind.Back, MediumShade)]
    public void WallsAndTopsUseDifferentShadeSymbolsOnADimmerBackground(SurfaceKind kind, char expected)
    {
        var sample = Of(kind, 0.6f);

        var cell = Paint(false, sample, sample);

        Assert.Equal(expected, cell.Glyph);
        Assert.True(Brightness(cell.Foreground!.Value) > Brightness(cell.Background!.Value));
    }

    [Fact]
    public void ShadowIsALightShadeInItsOwnColour()
    {
        Assert.Equal(new LogoCell(LightShade, LogoPalette.Shadow), Paint(false, Shadow, Shadow));
        Assert.Equal(new LogoCell(LightShade, LogoPalette.Shadow), Paint(false, Shadow, None));
    }

    [Fact]
    public void ALetterHalfOverShadowIsDrawnOverTheShadowColour()
    {
        var front = Of(SurfaceKind.Front);

        Assert.Equal(new LogoCell(UpperHalf, LogoPalette.Of(front), LogoPalette.Shadow), Paint(false, front, Shadow));
        Assert.Equal(new LogoCell(LowerHalf, LogoPalette.Of(front), LogoPalette.Shadow), Paint(false, Shadow, front));
    }

    [Theory]
    [InlineData(SurfaceKind.Front, '#')]
    [InlineData(SurfaceKind.FrontHigh, '#')]
    [InlineData(SurfaceKind.FrontLow, '#')]
    [InlineData(SurfaceKind.Top, '=')]
    [InlineData(SurfaceKind.Side, '/')]
    [InlineData(SurfaceKind.Back, '+')]
    public void AsciiUsesOneSymbolPerKindOfSurfaceAndNoColour(SurfaceKind kind, char expected)
    {
        var cell = Paint(true, Of(kind), None);

        Assert.Equal(new LogoCell(expected), cell);
        Assert.Equal(new LogoCell(expected), Paint(true, None, Of(kind)));
    }

    [Fact]
    public void AsciiShowsTheMostProminentSurfaceInACell()
    {
        Assert.Equal('=', Paint(true, Of(SurfaceKind.Top), Of(SurfaceKind.Side)).Glyph);
        Assert.Equal('/', Paint(true, Of(SurfaceKind.Back), Of(SurfaceKind.Side)).Glyph);
        Assert.Equal('#', Paint(true, Of(SurfaceKind.Front), Shadow).Glyph);
        Assert.Equal('#', Paint(true, Shadow, Of(SurfaceKind.FrontLow)).Glyph);
    }

    [Fact]
    public void AsciiMarksShadowWithAPoint()
    {
        Assert.Equal('.', Paint(true, Shadow, None).Glyph);
        Assert.Equal('.', Paint(true, None, Shadow).Glyph);
    }

    [Fact]
    public void OneSampleStandsForTheWholeCell()
    {
        var frame = new LogoFrame(3, 2, 1);
        frame[0, 0] = Of(SurfaceKind.Front);
        frame[1, 0] = Shadow;

        var cells = new LogoPainter(true).Paint(frame);

        Assert.Equal(2, cells.GetLength(0));
        Assert.Equal(3, cells.GetLength(1));
        Assert.Equal(new[] { '#', '.', ' ' }, new[] { cells[0, 0].Glyph, cells[0, 1].Glyph, cells[0, 2].Glyph });
        Assert.All(Enumerable.Range(0, 3), column => Assert.Equal(' ', cells[1, column].Glyph));
    }

    [Theory]
    [InlineData("large")]
    [InlineData("compact")]
    [InlineData("plain")]
    public void AsciiPaintingIsPrintableAsciiWithoutColour(string name)
    {
        var layout = name switch { "large" => LogoLayout.Large, "compact" => LogoLayout.Compact, _ => LogoLayout.Plain };

        var cells = new LogoPainter(true).Paint(new LogoRenderer(layout).Render(LogoMotion.Still));

        foreach (var cell in cells)
        {
            Assert.InRange(cell.Glyph, ' ', '~');
            Assert.Null(cell.Foreground);
            Assert.Null(cell.Background);
        }
    }

    [Fact]
    public void RichPaintingUsesOnlyTheBlockSymbolsAndColoursEverythingItDraws()
    {
        var allowed = new[] { ' ', FullBlock, DarkShade, MediumShade, LightShade, UpperHalf, LowerHalf };
        var seen = new HashSet<char>();

        for (var yaw = 0.0; yaw < 2 * Math.PI; yaw += 0.4)
            foreach (var cell in new LogoPainter(false).Paint(new LogoRenderer(LogoLayout.Large).Render(new LogoPose(yaw, 0.2))))
            {
                Assert.Contains(cell.Glyph, allowed);
                if (cell.Glyph != ' ') Assert.NotNull(cell.Foreground);
                seen.Add(cell.Glyph);
            }

        // The whole palette of symbols appears somewhere in a turn, which is what makes the faces distinguishable.
        Assert.Superset(new HashSet<char> { FullBlock, MediumShade, LightShade, UpperHalf, LowerHalf }, seen);
    }

    [Fact]
    public void ToLinesTrimsTrailingBlanksOnly()
    {
        var frame = new LogoFrame(4, 2, 1);
        frame[1, 0] = Of(SurfaceKind.Front);
        frame[0, 1] = Of(SurfaceKind.Side);

        var lines = LogoPainter.ToLines(new LogoPainter(true).Paint(frame));

        Assert.Equal(new[] { " #", "/" }, lines);
    }
}
