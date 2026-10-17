using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class ScrollerTests
{
    [Fact]
    public void StartsAtTheTop()
        => Assert.Equal(0, new Scroller().Offset);

    [Fact]
    public void ByMovesAndStaysInsideTheList()
    {
        var scroller = new Scroller();

        scroller.By(3, total: 20, rows: 5);
        Assert.Equal(3, scroller.Offset);
        scroller.By(100, total: 20, rows: 5);
        Assert.Equal(15, scroller.Offset);
        scroller.By(-100, total: 20, rows: 5);
        Assert.Equal(0, scroller.Offset);
    }

    [Fact]
    public void AListThatFitsNeverScrolls()
    {
        var scroller = new Scroller();

        scroller.By(5, total: 3, rows: 10);

        Assert.Equal(0, scroller.Offset);
    }

    [Fact]
    public void EndShowsTheLastRowsAndHomeTheFirst()
    {
        var scroller = new Scroller();

        scroller.End(total: 20, rows: 5);
        Assert.Equal(15, scroller.Offset);
        scroller.Home();
        Assert.Equal(0, scroller.Offset);
    }

    [Fact]
    public void ClampPullsBackWhenTheListShrinksOrTheRoomGrows()
    {
        var scroller = new Scroller();
        scroller.End(total: 20, rows: 5);

        scroller.Clamp(total: 8, rows: 5);
        Assert.Equal(3, scroller.Offset);
        scroller.Clamp(total: 8, rows: 20);
        Assert.Equal(0, scroller.Offset);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(19, 15)]
    public void RevealScrollsTheLeastNeededToShowARow(int index, int expectedOffset)
    {
        var scroller = new Scroller();

        scroller.Reveal(index, total: 20, rows: 5);

        Assert.Equal(expectedOffset, scroller.Offset);
    }

    [Fact]
    public void RevealScrollsBackUpToARowAboveTheWindow()
    {
        var scroller = new Scroller();
        scroller.Reveal(19, total: 20, rows: 5);

        scroller.Reveal(10, total: 20, rows: 5);

        Assert.Equal(10, scroller.Offset);
    }

    [Fact]
    public void ZeroRowsIsTreatedAsOne()
    {
        var scroller = new Scroller();

        scroller.By(4, total: 10, rows: 0);

        Assert.Equal(4, scroller.Offset);
    }

    [Theory]
    [InlineData(ConsoleKey.DownArrow, 1)]
    [InlineData(ConsoleKey.PageDown, 4)]
    [InlineData(ConsoleKey.End, 15)]
    public void ScrollingKeysMoveTheWindow(ConsoleKey key, int expected)
    {
        var scroller = new Scroller();

        Assert.True(scroller.Handle(Typed.Of(key), total: 20, rows: 5));

        Assert.Equal(expected, scroller.Offset);
    }

    [Fact]
    public void UpwardKeysMoveBack()
    {
        var scroller = new Scroller();
        scroller.End(total: 20, rows: 5);

        Assert.True(scroller.Handle(Typed.PageUp, total: 20, rows: 5));
        Assert.Equal(11, scroller.Offset);
        Assert.True(scroller.Handle(Typed.Up, total: 20, rows: 5));
        Assert.Equal(10, scroller.Offset);
        Assert.True(scroller.Handle(Typed.Home, total: 20, rows: 5));
        Assert.Equal(0, scroller.Offset);
    }

    [Fact]
    public void OtherKeysAreNotHandled()
    {
        var scroller = new Scroller();

        Assert.False(scroller.Handle(Typed.Letter('x'), total: 20, rows: 5));
        Assert.False(scroller.Handle(Typed.Enter, total: 20, rows: 5));
        Assert.Equal(0, scroller.Offset);
    }
}
