using MyKa.Spoofer.Branding;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class LogoRendererTests
{
    private static readonly LogoPose Front = new(0, 0.20);

    private static LogoLayout Layout(string name) => name switch
    {
        "large" => LogoLayout.Large,
        "compact" => LogoLayout.Compact,
        _ => LogoLayout.Plain,
    };

    private static IEnumerable<Sample> Samples(LogoFrame frame)
    {
        for (var row = 0; row < frame.SubRows; row++)
            for (var column = 0; column < frame.Columns; column++)
                yield return frame[column, row];
    }

    private static bool IsFront(Sample sample)
        => sample.Kind is SurfaceKind.Front or SurfaceKind.FrontHigh or SurfaceKind.FrontLow;

    private static int Filled(PixelMask mask)
    {
        var count = 0;
        for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
                if (mask[x, y]) count++;
        return count;
    }

    [Theory]
    [InlineData("large")]
    [InlineData("compact")]
    [InlineData("plain")]
    public void FrameMatchesTheLayoutGrid(string name)
    {
        var layout = Layout(name);

        var frame = new LogoRenderer(layout).Render(Front);

        Assert.Equal(layout.Columns, frame.Columns);
        Assert.Equal(layout.SubRows, frame.SubRows);
        Assert.Equal(layout.Rows, frame.Rows);
    }

    [Theory]
    [InlineData("large")]
    [InlineData("compact")]
    public void FacingTheViewerShowsRoughlyTheWholeFrontFace(string name)
    {
        var layout = Layout(name);

        var frame = new LogoRenderer(layout).Render(Front);

        var fronts = Samples(frame).Count(IsFront);
        var area = Filled(layout.Mask);
        Assert.InRange(fronts, area * 0.85, area * 1.25);
    }

    [Fact]
    public void TurningEdgeOnHidesTheFrontAndShowsOnlyWalls()
    {
        var renderer = new LogoRenderer(LogoLayout.Large);
        var facing = Samples(renderer.Render(Front)).Count(IsFront);

        var edgeOn = renderer.Render(new LogoPose(Math.PI / 2, 0.20));

        Assert.True(Samples(edgeOn).Count(IsFront) < facing * 0.05);
        Assert.True(Samples(edgeOn).Count(sample => sample.Kind == SurfaceKind.Side) > 0);
    }

    [Fact]
    public void TurnedToTheBackShowsTheBackFaceAndNoFront()
    {
        var frame = new LogoRenderer(LogoLayout.Large).Render(new LogoPose(Math.PI, 0.20));

        Assert.True(Samples(frame).Count(sample => sample.Kind == SurfaceKind.Back) > 500);
        Assert.Equal(0, Samples(frame).Count(IsFront));
    }

    [Fact]
    public void TheBackIsTheMirrorImageOfTheFront()
    {
        var layout = LogoLayout.Large;
        var renderer = new LogoRenderer(layout);
        var front = renderer.Render(Front);
        var back = renderer.Render(new LogoPose(Math.PI, 0.20));

        var mismatches = 0;
        for (var row = 0; row < layout.SubRows; row++)
            for (var column = 0; column < layout.Columns; column++)
                if (front[column, row].IsSolid != back[layout.Columns - 1 - column, row].IsSolid) mismatches++;

        Assert.True(mismatches < layout.Columns * layout.SubRows * 0.01, $"{mismatches} samples differ");
    }

    [Fact]
    public void ARevolutionLooksLikeNoTurnAtAll()
    {
        var renderer = new LogoRenderer(LogoLayout.Large);
        var once = renderer.Render(new LogoPose(0.3, 0.2));
        var twice = renderer.Render(new LogoPose(0.3 + 2 * Math.PI, 0.2));

        var different = Samples(once).Zip(Samples(twice)).Count(pair => pair.First.Kind != pair.Second.Kind);

        Assert.True(different < 20, $"{different} samples differ");
    }

    [Fact]
    public void TurningChangesTheImage()
    {
        var renderer = new LogoRenderer(LogoLayout.Large);
        var before = renderer.Render(new LogoPose(0.0, 0.2));
        var after = renderer.Render(new LogoPose(0.5, 0.2));

        var different = Samples(before).Zip(Samples(after)).Count(pair => pair.First.Kind != pair.Second.Kind);

        Assert.True(different > 300, $"only {different} samples differ");
    }

    [Fact]
    public void LettersCastAShadowThatFallsOnlyOutsideThem()
    {
        var frame = new LogoRenderer(LogoLayout.Large).Render(LogoMotion.Still);

        var shadows = Samples(frame).Count(sample => sample.Kind == SurfaceKind.Shadow);

        Assert.True(shadows > 200, $"only {shadows} shadow samples");
        Assert.All(Samples(frame).Where(sample => sample.Kind == SurfaceKind.Shadow),
            sample => Assert.False(sample.IsSolid));
    }

    [Fact]
    public void ColourRunsFromTheTopOfTheWordmarkToTheBottom()
    {
        var frame = new LogoRenderer(LogoLayout.Large).Render(Front);
        var half = frame.SubRows / 2;
        double Average(int from, int to) => Enumerable.Range(from, to - from)
            .SelectMany(row => Enumerable.Range(0, frame.Columns).Select(column => frame[column, row]))
            .Where(IsFront).Average(sample => (double)sample.Tint);

        Assert.True(Average(0, half) < Average(half, frame.SubRows));
    }

    [Theory]
    [InlineData("large")]
    [InlineData("compact")]
    [InlineData("plain")]
    public void EverySampleStaysInRangeAtEveryAngle(string name)
    {
        var renderer = new LogoRenderer(Layout(name));

        for (var yaw = -7.0; yaw <= 7.0; yaw += 0.37)
            foreach (var pitch in new[] { -0.5, 0.0, 0.2, 1.0 })
                Assert.All(Samples(renderer.Render(new LogoPose(yaw, pitch))), sample =>
                {
                    Assert.InRange(sample.Light, 0f, 1f);
                    Assert.InRange(sample.Tint, 0f, 1f);
                });
    }

    /// <summary>Every pose the program can actually show: the idle turn, the logo command's spin, and the resting pose.</summary>
    private static IEnumerable<(string Where, LogoPose Pose)> ReachablePoses()
    {
        for (var seconds = 0.0; seconds < LogoMotion.PeriodSeconds; seconds += 0.04)
            yield return ($"idle turn at {seconds:F2}s", LogoMotion.At(seconds));
        for (var seconds = 0.0; seconds < LogoMotion.IntroSeconds; seconds += 0.02)
            yield return ($"spin at {seconds:F2}s", LogoMotion.Intro(seconds));
        yield return ("resting pose", LogoMotion.Still);
    }

    [Theory]
    [InlineData("large")]
    [InlineData("compact")]
    public void NoLetterIsClippedAtAnyPoseTheProgramShows(string name)
    {
        var renderer = new LogoRenderer(Layout(name));

        foreach (var (where, pose) in ReachablePoses())
        {
            var frame = renderer.Render(pose);
            for (var column = 0; column < frame.Columns; column++)
            {
                Assert.False(frame[column, 0].IsSolid, $"top edge touched during {where}");
                Assert.False(frame[column, frame.SubRows - 1].IsSolid, $"bottom edge touched during {where}");
            }
            for (var row = 0; row < frame.SubRows; row++)
            {
                Assert.False(frame[0, row].IsSolid, $"left edge touched during {where}");
                Assert.False(frame[frame.Columns - 1, row].IsSolid, $"right edge touched during {where}");
            }
        }
    }

    [Fact]
    public void AFullFrameRendersQuickEnoughToAnimate()
    {
        var renderer = new LogoRenderer(LogoLayout.Large);
        renderer.Render(Front);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 20; i++) renderer.Render(new LogoPose(i * 0.3, 0.2));

        // The animation draws about twenty frames a second, so each must cost a small fraction of 50 ms.
        Assert.True(clock.Elapsed.TotalMilliseconds / 20 < 25, $"{clock.Elapsed.TotalMilliseconds / 20:F1} ms per frame");
    }
}
