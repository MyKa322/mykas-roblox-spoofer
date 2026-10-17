using MyKa.Spoofer.Branding;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class BlockFontTests
{
    private const string Letters = "ABEFKLMOPRSXY'";

    private static string[] Rows(PixelMask mask) => Enumerable.Range(0, mask.Height)
        .Select(y => new string(Enumerable.Range(0, mask.Width).Select(x => mask[x, y] ? '#' : '.').ToArray()))
        .ToArray();

    [Theory]
    [InlineData('A')]
    [InlineData('B')]
    [InlineData('E')]
    [InlineData('F')]
    [InlineData('K')]
    [InlineData('L')]
    [InlineData('M')]
    [InlineData('O')]
    [InlineData('P')]
    [InlineData('R')]
    [InlineData('S')]
    [InlineData('X')]
    [InlineData('Y')]
    [InlineData('\'')]
    public void EveryLetterIsEightRowsTall(char letter)
    {
        Assert.True(BlockFont.Supports(letter));

        var mask = BlockFont.Word(letter.ToString());

        Assert.Equal(BlockFont.Height, mask.Height);
        Assert.True(Rows(mask).All(row => row.Contains('#') || mask.Width <= 2));
    }

    [Fact]
    public void NoTwoLettersShareAShape()
    {
        var shapes = Letters.Select(letter => string.Join('/', Rows(BlockFont.Word(letter.ToString())))).ToArray();

        Assert.Equal(shapes.Length, shapes.Distinct().Count());
    }

    [Fact]
    public void LetterWidthsAreTheOnesTheLayoutsAreSizedFor()
    {
        Assert.Equal(7, BlockFont.Word("M").Width);
        Assert.Equal(2, BlockFont.Word("'").Width);
        Assert.All("ABEFKLOPRSXY", letter => Assert.Equal(6, BlockFont.Word(letter.ToString()).Width));
    }

    [Fact]
    public void WordsAreLettersSeparatedByTheLetterGap()
    {
        var word = BlockFont.Word("MYKA'S");

        // M 7 + Y 6 + K 6 + A 6 + ' 2 + S 6 = 33, plus five gaps.
        Assert.Equal(33 + 5 * BlockFont.LetterGap, word.Width);
    }

    [Fact]
    public void LettersStayApartSoNeighbouringWallsDoNotMerge()
    {
        var word = BlockFont.Word("OO");

        for (var gap = 0; gap < BlockFont.LetterGap; gap++)
            for (var y = 0; y < word.Height; y++)
                Assert.False(word[6 + gap, y]);
    }

    [Theory]
    [InlineData("MYKA'S")]
    [InlineData("ROBLOX")]
    [InlineData("SPOOFER")]
    public void EveryWordInTheWordmarkIsSupported(string word)
        => Assert.All(word, letter => Assert.True(BlockFont.Supports(letter)));

    [Theory]
    [InlineData("a")]
    [InlineData("Z")]
    [InlineData("MYKA S")]
    public void UnsupportedLettersAreRejected(string text)
        => Assert.Throws<ArgumentException>(() => BlockFont.Word(text));

    [Fact]
    public void KIsReadableAsAK()
        => Assert.Equal(["##..##", "##.##.", "####..", "###...", "###...", "####..", "##.##.", "##..##"],
            Rows(BlockFont.Word("K")));
}
