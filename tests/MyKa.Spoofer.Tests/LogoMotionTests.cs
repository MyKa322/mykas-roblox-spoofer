using MyKa.Spoofer.Branding;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class LogoMotionTests
{
    private static double Wrapped(double yaw)
    {
        var turned = yaw % (2 * Math.PI);
        return turned > Math.PI ? turned - 2 * Math.PI : turned;
    }

    [Fact]
    public void StartsFacingTheViewer()
        => Assert.Equal(0, LogoMotion.At(0).Yaw, precision: 9);

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.7)]
    [InlineData(4.4)]
    [InlineData(8.9)]
    public void RepeatsEveryPeriod(double seconds)
    {
        var first = LogoMotion.At(seconds);
        var later = LogoMotion.At(seconds + 3 * LogoMotion.PeriodSeconds);

        Assert.Equal(Wrapped(first.Yaw), Wrapped(later.Yaw), precision: 6);
        Assert.Equal(first.Pitch, later.Pitch, precision: 6);
    }

    [Fact]
    public void NeverStopsOrTurnsBackwards()
    {
        var previous = LogoMotion.At(0).Yaw;

        // Stop just before the period ends, where yaw wraps from a full turn back to zero.
        for (var seconds = 0.05; seconds < LogoMotion.PeriodSeconds - 1e-6; seconds += 0.05)
        {
            var yaw = LogoMotion.At(seconds).Yaw;
            Assert.True(yaw > previous, $"yaw stalled or reversed at {seconds:F2}s");
            previous = yaw;
        }
    }

    [Fact]
    public void LingersFacingTheViewerAndWhipsRoundTheBack()
    {
        var samples = Enumerable.Range(0, 900).Select(i => LogoMotion.At(i * LogoMotion.PeriodSeconds / 900)).ToArray();

        var facing = samples.Count(pose => Math.Abs(Wrapped(pose.Yaw)) < Math.PI / 4) / (double)samples.Length;

        // A constant-speed turn spends a quarter of its time within 45 degrees of the front.
        Assert.True(facing > 0.33, $"facing {facing:P0} of the time");
    }

    [Fact]
    public void TiltNeverLeavesTheRangeTheLayoutsAreSizedFor()
    {
        var pitches = Enumerable.Range(0, 900).Select(i => LogoMotion.At(i * LogoMotion.PeriodSeconds / 900).Pitch)
            .Concat(Enumerable.Range(0, 280).Select(i => LogoMotion.Intro(i * 0.01).Pitch)).ToArray();

        Assert.All(pitches, pitch => Assert.InRange(pitch, 0, LogoMotion.FacingPitch));
    }

    [Fact]
    public void TiltedTowardTheViewerWhenFacingAndLevelWhenEdgeOn()
    {
        var samples = Enumerable.Range(0, 900).Select(i => LogoMotion.At(i * LogoMotion.PeriodSeconds / 900)).ToArray();

        Assert.Equal(LogoMotion.FacingPitch, LogoMotion.At(0).Pitch, precision: 9);
        var edgeOn = samples.Where(pose => Math.Abs(Math.Abs(Wrapped(pose.Yaw)) - Math.PI / 2) < 0.05).ToArray();
        Assert.NotEmpty(edgeOn);
        Assert.All(edgeOn, pose => Assert.True(pose.Pitch < 0.003, $"pitch {pose.Pitch} while edge-on"));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteTimeFallsBackToTheRestingPose(double seconds)
    {
        Assert.Equal(LogoMotion.Still, LogoMotion.At(seconds));
        Assert.Equal(LogoMotion.Still, LogoMotion.Intro(seconds));
    }

    [Fact]
    public void NegativeTimeStillGivesAValidPose()
    {
        var pose = LogoMotion.At(-3.2);

        Assert.True(double.IsFinite(pose.Yaw) && double.IsFinite(pose.Pitch));
    }

    [Fact]
    public void IntroStartsAndEndsAtTheRestingPose()
    {
        Assert.Equal(LogoMotion.Still.Yaw, LogoMotion.Intro(0).Yaw, precision: 9);
        Assert.Equal(LogoMotion.Still, LogoMotion.Intro(LogoMotion.IntroSeconds));
        Assert.Equal(LogoMotion.Still, LogoMotion.Intro(LogoMotion.IntroSeconds + 5));
    }

    [Fact]
    public void IntroMakesOneFullTurn()
    {
        var almostDone = LogoMotion.Intro(LogoMotion.IntroSeconds - 1e-6);

        Assert.Equal(LogoMotion.Still.Yaw + 2 * Math.PI, almostDone.Yaw, precision: 3);
    }

    [Fact]
    public void IntroEasesInAndOut()
    {
        var start = LogoMotion.Intro(0.1).Yaw - LogoMotion.Intro(0).Yaw;
        var middle = LogoMotion.Intro(LogoMotion.IntroSeconds / 2 + 0.05).Yaw - LogoMotion.Intro(LogoMotion.IntroSeconds / 2 - 0.05).Yaw;

        Assert.True(middle > start * 4, "the turn should be fastest in the middle");
    }
}
