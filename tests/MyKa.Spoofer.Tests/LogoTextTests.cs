using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Terminal;
using Xunit;

namespace MyKa.Spoofer.Tests;

[Collection("Console")]
public sealed class LogoTextTests
{
    private static string SnapshotPath => Path.Combine(AppContext.BaseDirectory, "TestData", "logo.txt");

    [Fact]
    public void DocumentOpensWithTheExactMixedCaseTitle()
    {
        var document = LogoText.PlainDocument();

        Assert.Equal("MyKa's Roblox Spoofer", document[0]);
        Assert.Equal("", document[1]);
    }

    [Fact]
    public void DocumentEndsWithTheSubtitleCentredUnderTheArt()
    {
        var document = LogoText.PlainDocument();
        var artWidth = LogoText.PlainArt().Max(line => line.Length);

        var last = document[^1];

        Assert.Equal(LogoText.Subtitle, last.Trim());
        var centre = last.Length - last.TrimStart().Length + LogoText.Subtitle.Length / 2.0;
        Assert.InRange(centre, artWidth / 2.0 - 1.5, artWidth / 2.0 + 1.5);
    }

    [Fact]
    public void DocumentIsPrintableAsciiThatFitsAnOrdinaryTerminal()
    {
        foreach (var line in LogoText.PlainDocument())
        {
            Assert.All(line, c => Assert.InRange(c, ' ', '~'));
            Assert.True(line.Length <= 80, $"'{line}' is wider than 80 columns");
            Assert.Equal(line.TrimEnd(), line);
        }
    }

    [Fact]
    public void ArtHasNoBlankRowsAtEitherEndAndNoSharedIndent()
    {
        var art = LogoText.PlainArt();

        Assert.NotEqual("", art[0]);
        Assert.NotEqual("", art[^1]);
        Assert.Contains(art, line => line.Length > 0 && !char.IsWhiteSpace(line[0]));
        Assert.InRange(art.Count, 8, 14);
    }

    [Fact]
    public void ArtUsesSeveralDifferentSymbolsToShowDepth()
    {
        var symbols = LogoText.PlainArt().SelectMany(line => line).Where(c => c != ' ').ToHashSet();

        Assert.Contains('#', symbols);
        Assert.Contains('/', symbols);
        Assert.Contains('.', symbols);
    }

    [Fact]
    public void ShippedSnapshotMatchesWhatTheProgramDraws()
    {
        var expected = string.Join('\n', LogoText.PlainDocument()) + "\n";

        var actual = File.ReadAllText(SnapshotPath).Replace("\r\n", "\n");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LogoCommandPrintsThePlainDocumentWhenOutputIsPlain()
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            new TerminalUi(true).Logo();
        }
        finally { Console.SetOut(previous); }

        var printed = output.ToString().Replace("\r\n", "\n");
        Assert.Equal(string.Join('\n', LogoText.PlainDocument()) + "\n", printed);
    }

    [Fact]
    public void PlainLogoIsDeterministic()
        => Assert.Equal(LogoText.PlainDocument(), LogoText.PlainDocument());
}
