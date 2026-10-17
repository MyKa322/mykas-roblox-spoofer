using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class TabsModelTests
{
    private readonly StubPage first = new("First");
    private readonly StubPage second = new("Second");
    private readonly StubPage third = new("Third");

    private TabsModel Model() => new([first, second, third]);

    [Fact]
    public void StartsOnTheFirstTab()
    {
        var model = Model();

        Assert.Equal(0, model.Selected);
        Assert.Same(first, model.Current);
    }

    [Fact]
    public void ATabStripNeedsTabs()
        => Assert.Throws<ArgumentException>(() => new TabsModel([]));

    [Fact]
    public void RightAndLeftSwitchTabsAndActivateTheNewOne()
    {
        var model = Model();

        Assert.Equal(TabKeyResult.Switched, model.Handle(Typed.Right));
        Assert.Same(second, model.Current);
        Assert.Equal(1, second.Activations);
        Assert.Equal(TabKeyResult.Switched, model.Handle(Typed.Left));
        Assert.Same(first, model.Current);
        Assert.Equal(1, first.Activations);
    }

    [Fact]
    public void SwitchingWrapsAroundAtBothEnds()
    {
        var model = Model();

        model.Handle(Typed.Left);
        Assert.Same(third, model.Current);
        model.Handle(Typed.Right);
        Assert.Same(first, model.Current);
    }

    [Fact]
    public void TabAndShiftTabSwitchToo()
    {
        var model = Model();

        model.Handle(Typed.Tab);
        Assert.Same(second, model.Current);
        model.Handle(Typed.ShiftTab);
        Assert.Same(first, model.Current);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void NumberKeysJumpToATab(int digit, int expected)
    {
        var model = Model();
        model.Handle(Typed.Right);

        model.Handle(Typed.Digit(digit));

        Assert.Equal(expected, model.Selected);
    }

    [Fact]
    public void NumberPadKeysJumpToo()
    {
        var model = Model();

        model.Handle(Typed.Of(ConsoleKey.NumPad3, '3'));

        Assert.Equal(2, model.Selected);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void ANumberWithNoTabIsIgnoredAndLeftForThePage(int digit)
    {
        var model = Model();

        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Digit(digit)));
        Assert.Equal(0, model.Selected);
    }

    [Fact]
    public void ZeroIsNotATab()
        => Assert.Equal(TabKeyResult.Ignored, Model().Handle(Typed.Digit(0)));

    [Fact]
    public void ChoosingTheTabAlreadyShownChangesNothing()
    {
        var model = Model();

        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Digit(1)));
        Assert.Equal(0, first.Activations);
    }

    [Fact]
    public void ASingleTabCannotBeSwitched()
    {
        var model = new TabsModel([first]);

        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Right));
        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Left));
        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Tab));
    }

    [Theory]
    [InlineData(ConsoleKey.Escape, '\u001b')]
    [InlineData(ConsoleKey.Q, 'q')]
    [InlineData(ConsoleKey.Q, 'Q')]
    public void EscapeAndQLeave(ConsoleKey key, char character)
        => Assert.Equal(TabKeyResult.Quit, Model().Handle(Typed.Of(key, character)));

    [Fact]
    public void QuitWorksFromAnyLayoutBecauseItUsesThePhysicalKey()
    {
        // A Cyrillic layout reports the key that is Q on a US keyboard with a different character.
        Assert.Equal(TabKeyResult.Quit, Model().Handle(Typed.Of(ConsoleKey.Q, 'й')));
    }

    [Theory]
    [InlineData(ConsoleKey.A)]
    [InlineData(ConsoleKey.Enter)]
    [InlineData(ConsoleKey.UpArrow)]
    [InlineData(ConsoleKey.PageDown)]
    public void KeysItHasNoUseForGoToThePage(ConsoleKey key)
        => Assert.Equal(TabKeyResult.Ignored, Model().Handle(Typed.Of(key)));

    [Fact]
    public void WhileAPageIsAskingAQuestionEveryKeyIsLeftForIt()
    {
        var model = Model();
        first.Modal = true;

        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Escape));
        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Right));
        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Digit(2)));
        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Letter('q')));
        Assert.Same(first, model.Current);
    }
}
