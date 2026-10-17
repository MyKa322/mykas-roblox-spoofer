using MyKa.Spoofer.Terminal.Tabs;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class CellsTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("日本", 4)]
    public void WidthCountsTerminalCellsNotCharacters(string text, int expected)
        => Assert.Equal(expected, Cells.Width(text));

    [Fact]
    public void WidthIsMeasuredAsTheTextIsDisplayedWithControlCharactersRemoved()
    {
        Assert.Equal(2, Cells.Width("a\u001Bb"));
        Assert.Equal(11, Cells.Width("evil\u001B[2J‮name"));
        Assert.Equal(0, Cells.Width("\u001B\u0007"));
    }

    [Fact]
    public void CutKeepsTheLongestStartThatFits()
    {
        Assert.Equal("abc", Cells.Cut("abcdef", 3));
        Assert.Equal("", Cells.Cut("abc", 0));
        Assert.Equal("abcdef", Cells.Cut("abcdef", 99));
    }

    [Fact]
    public void CutNeverSplitsAWideCharacterInHalf()
        => Assert.Equal("日", Cells.Cut("日本", 3));

    [Theory]
    [InlineData("ab", 5, "ab   ")]
    [InlineData("abcde", 5, "abcde")]
    [InlineData("abcdef", 5, "abcd…")]
    [InlineData("abc", 1, "…")]
    [InlineData("abc", 0, "")]
    public void FitGivesExactlyTheRequestedWidth(string text, int width, string expected)
    {
        Assert.Equal(expected, Cells.Fit(text, width));
        Assert.Equal(Math.Max(0, width), Cells.Width(Cells.Fit(text, width)));
    }

    [Fact]
    public void FitKeepsWideTextWithinTheWidth()
        => Assert.Equal(6, Cells.Width(Cells.Fit("日本語文字", 6)));

    [Fact]
    public void FitRemovesControlCharacters()
        => Assert.Equal("ab[31m   ", Cells.Fit("a\u001Bb[31m", 9));

    [Fact]
    public void WrapBreaksBetweenWords()
        => Assert.Equal(new[] { "one two", "three" }, Cells.Wrap("one two three", 8));

    [Fact]
    public void WrapKeepsALineThatFitsWhole()
        => Assert.Equal(new[] { "short line" }, Cells.Wrap("short line", 40));

    [Fact]
    public void WrapSplitsAWordWiderThanTheLine()
    {
        var lines = Cells.Wrap("abcdefghij", 4);

        Assert.Equal(new[] { "abcd", "efgh", "ij" }, lines);
    }

    [Fact]
    public void WrapNeverExceedsTheWidth()
    {
        var text = string.Join(' ', Enumerable.Range(0, 40).Select(i => new string('x', i % 9 + 1)));

        for (var width = 3; width < 40; width++)
            Assert.All(Cells.Wrap(text, width), line => Assert.True(Cells.Width(line) <= width, $"'{line}' is wider than {width}"));
    }

    [Fact]
    public void WrapLosesNoWords()
    {
        const string text = "File presence does not verify the publisher or multi-client compatibility.";

        Assert.Equal(text, string.Join(' ', Cells.Wrap(text, 17)));
    }

    [Fact]
    public void WrapOfNothingIsOneEmptyLine()
    {
        Assert.Equal(new[] { "" }, Cells.Wrap("", 10));
        Assert.Equal(new[] { "" }, Cells.Wrap("   ", 10));
    }

    [Fact]
    public void WrapWithNoRoomGivesNothing()
        => Assert.Empty(Cells.Wrap("abc", 0));
}
