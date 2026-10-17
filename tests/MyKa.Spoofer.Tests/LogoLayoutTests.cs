using MyKa.Spoofer.Branding;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class LogoLayoutTests
{
    [Fact]
    public void TheFullStackIsThreeWordsOfEightPixelLetters()
    {
        // SPOOFER is the widest word: seven 6-pixel letters and six gaps. Three rows of 8 with two gaps of 2.
        Assert.Equal(7 * 6 + 6 * BlockFont.LetterGap, LogoLayout.Large.Mask.Width);
        Assert.Equal(3 * 8 + 2 * 2, LogoLayout.Large.Mask.Height);
    }

    [Theory]
    [InlineData(200, 40, "large")]
    [InlineData(72, 18, "large")]
    [InlineData(71, 40, "compact")]
    [InlineData(200, 17, "compact")]
    [InlineData(55, 7, "compact")]
    public void PicksTheLargestLogoThatFits(int width, int rows, string expected)
        => Assert.Equal(expected, LogoLayout.Choose(width, rows)?.Name);

    [Theory]
    [InlineData(54, 40)]
    [InlineData(200, 6)]
    [InlineData(0, 0)]
    [InlineData(-5, -5)]
    public void FallsBackToTheTitleWhenNothingFits(int width, int rows)
        => Assert.Null(LogoLayout.Choose(width, rows));

    [Theory]
    [InlineData(72, 18)]
    [InlineData(55, 7)]
    public void ARoomyEnoughTerminalNeverNeedsMoreThanTheChosenSize(int width, int rows)
    {
        var layout = LogoLayout.Choose(width, rows)!;

        Assert.True(layout.Columns + 2 <= width);
        Assert.True(layout.Rows <= rows);
    }

    [Fact]
    public void ColourLayoutsStackTwoSamplesPerCellAndPlainUsesOne()
    {
        Assert.Equal(2, LogoLayout.Large.SamplesPerCell);
        Assert.Equal(2, LogoLayout.Compact.SamplesPerCell);
        Assert.Equal(1, LogoLayout.Plain.SamplesPerCell);
        Assert.Equal(LogoLayout.Large.Rows * 2, LogoLayout.Large.SubRows);
        Assert.Equal(LogoLayout.Plain.Rows, LogoLayout.Plain.SubRows);
    }

    [Fact]
    public void ChoiceNeverOffersThePlainLayout()
    {
        for (var width = 0; width < 200; width += 3)
            for (var rows = 0; rows < 60; rows += 3)
                Assert.NotEqual("plain", LogoLayout.Choose(width, rows)?.Name);
    }
}
