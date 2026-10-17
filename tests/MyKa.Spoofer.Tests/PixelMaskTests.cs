using MyKa.Spoofer.Branding;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class PixelMaskTests
{
    private static PixelMask Mask(params string[] rows) => PixelMask.FromRows(rows);

    [Fact]
    public void ReadsPixelsAndTreatsAnythingOutsideAsOff()
    {
        var mask = Mask("#.", ".#");

        Assert.True(mask[0, 0]);
        Assert.False(mask[1, 0]);
        Assert.True(mask[1, 1]);
        Assert.False(mask[-1, 0]);
        Assert.False(mask[0, -1]);
        Assert.False(mask[2, 0]);
        Assert.False(mask[0, 2]);
    }

    [Fact]
    public void RowsOfDifferentWidthsAreRejected()
        => Assert.Throws<ArgumentException>(() => Mask("##", "#"));

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(2, 2, 3)]
    public void DimensionsMustMatchTheCells(int width, int height, int cells)
        => Assert.Throws<ArgumentException>(() => new PixelMask(width, height, new bool[cells]));

    [Fact]
    public void ConcatPlacesMasksSideBySideWithAGap()
    {
        var joined = PixelMask.Concat([Mask("#", "#"), Mask("##", ".#")], gap: 2);

        Assert.Equal(1 + 2 + 2, joined.Width);
        Assert.Equal(2, joined.Height);
        Assert.True(joined[0, 0]);
        Assert.False(joined[1, 0]);
        Assert.False(joined[2, 0]);
        Assert.True(joined[3, 0]);
        Assert.True(joined[4, 0]);
        Assert.False(joined[3, 1]);
        Assert.True(joined[4, 1]);
    }

    [Fact]
    public void StackCentresNarrowerMasksAndSeparatesThemWithEmptyRows()
    {
        var stacked = PixelMask.Stack([Mask("####"), Mask("##")], gap: 1);

        Assert.Equal(4, stacked.Width);
        Assert.Equal(3, stacked.Height);
        Assert.True(Enumerable.Range(0, 4).All(x => stacked[x, 0]));
        Assert.All(Enumerable.Range(0, 4), x => Assert.False(stacked[x, 1]));
        Assert.Equal([false, true, true, false], Enumerable.Range(0, 4).Select(x => stacked[x, 2]));
    }
}
