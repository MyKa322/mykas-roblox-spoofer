using MyKa.Spoofer.Terminal.Tabs;
using Spectre.Console;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class LineTests
{
    [Fact]
    public void EmptyLineHasNoTextAndNoWidth()
    {
        Assert.Equal("", Line.Empty.Text);
        Assert.Equal(0, Line.Empty.Width);
        Assert.Empty(Line.Empty.ToSegments(10));
    }

    [Fact]
    public void ThenAppendsWithoutChangingTheOriginal()
    {
        var first = Line.Of("ab");

        var second = first.Then("cd", Looks.Dim);

        Assert.Equal("ab", first.Text);
        Assert.Equal("abcd", second.Text);
        Assert.Equal(new[] { Style.Plain, Looks.Dim }, second.Spans.Select(span => span.Style));
    }

    [Fact]
    public void ControlAndDirectionalCharactersNeverReachTheTerminal()
    {
        var line = Line.Of("alice\u001B[31m‮\r\n").Then("\u0007bob");

        Assert.Equal("alice[31mbob", line.Text);
        Assert.DoesNotContain(line.ToSegments(80), segment => segment.Text.Any(char.IsControl));
    }

    [Fact]
    public void MarkupInTextStaysLiteral()
    {
        var line = Line.Of("[red]Boom[/] [bold]");

        Assert.Equal("[red]Boom[/] [bold]", string.Concat(line.ToSegments(80).Select(segment => segment.Text)));
    }

    [Fact]
    public void IndentAndCentreAddLeadingSpaces()
    {
        Assert.Equal("   ab", Line.Of("ab").Indent(3).Text);
        Assert.Equal("ab", Line.Of("ab").Indent(0).Text);
        Assert.Equal("ab", Line.Of("ab").Indent(-4).Text);
        Assert.Equal("    ab", Line.Of("ab").Centered(10).Text);
        Assert.Equal("ab", Line.Of("ab").Centered(1).Text);
    }

    [Fact]
    public void ALineThatFitsIsDrawnWhole()
    {
        var segments = Line.Of("ab", Looks.Dim).Then("cd", Looks.Good).ToSegments(10);

        Assert.Equal(new[] { "ab", "cd" }, segments.Select(segment => segment.Text));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(1)]
    public void ALineThatIsTooWideIsCutToFitWithAnEllipsis(int width)
    {
        var drawn = string.Concat(Line.Of("abcd", Looks.Dim).Then("efghij", Looks.Good).ToSegments(width).Select(s => s.Text));

        Assert.Equal(Math.Min(width, 10), Cells.Width(drawn));
        Assert.EndsWith(Cells.Ellipsis, drawn);
    }

    [Fact]
    public void CutKeepsTheStyleOfTheTextThatSurvives()
    {
        var segments = Line.Of("abcd", Looks.Dim).Then("efghij", Looks.Good).ToSegments(7);

        Assert.Equal(new[] { "abcd", "ef", Cells.Ellipsis }, segments.Select(segment => segment.Text));
        Assert.Equal(new[] { Looks.Dim, Looks.Good, Looks.Good }, segments.Select(segment => segment.Style));
    }

    [Fact]
    public void NoRoomMeansNoSegments()
    {
        Assert.Empty(Line.Of("abc").ToSegments(0));
        Assert.Empty(Line.Of("abc").ToSegments(-3));
    }
}
