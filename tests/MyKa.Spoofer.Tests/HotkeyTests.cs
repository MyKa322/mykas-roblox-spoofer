using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class HotkeyTests
{
    /// <summary>A key event with no key code, only the character typed, as some terminals deliver letters.</summary>
    private static ConsoleKeyInfo TypedOnly(char character) => Typed.Of((ConsoleKey)0, character);

    [Theory]
    [InlineData('a')]
    [InlineData('d')]
    [InlineData('q')]
    [InlineData('y')]
    public void MatchesTheLetterInEitherCase(char letter)
    {
        Assert.True(Hotkey.Is(Typed.Letter(letter), letter));
        Assert.True(Hotkey.Is(Typed.Of((ConsoleKey)char.ToUpperInvariant(letter), char.ToUpperInvariant(letter), ConsoleModifiers.Shift), letter));
    }

    [Fact]
    public void MatchesWhenTheTerminalReportsOnlyTheTypedCharacter()
    {
        Assert.True(Hotkey.Is(TypedOnly('d'), 'd'));
        Assert.True(Hotkey.Is(TypedOnly('D'), 'd'));
    }

    [Fact]
    public void MatchesWhenTheTerminalReportsOnlyThePhysicalKey()
    {
        // A Russian layout reports the key that is D on a US keyboard, with a Cyrillic character.
        Assert.True(Hotkey.Is(Typed.Of(ConsoleKey.D, 'в'), 'd'));
        Assert.True(Hotkey.Is(Typed.Of(ConsoleKey.D), 'd'));
    }

    [Theory]
    [InlineData('a', 'ф')]
    [InlineData('c', 'с')]
    [InlineData('d', 'в')]
    [InlineData('q', 'й')]
    [InlineData('r', 'к')]
    [InlineData('s', 'ы')]
    [InlineData('y', 'н')]
    public void MatchesTheRussianLayoutLetterOnTheSamePhysicalKey(char letter, char cyrillic)
    {
        Assert.True(Hotkey.Is(TypedOnly(cyrillic), letter));
        Assert.True(Hotkey.Is(TypedOnly(char.ToUpperInvariant(cyrillic)), letter));
    }

    [Fact]
    public void ACyrillicLetterNeverMatchesAnotherShortcut()
    {
        Assert.False(Hotkey.Is(TypedOnly('в'), 'a'));
        Assert.False(Hotkey.Is(TypedOnly('ф'), 'd'));
    }

    [Theory]
    [InlineData('x')]
    [InlineData('1')]
    [InlineData(' ')]
    [InlineData('\0')]
    public void OtherKeysDoNotMatch(char character)
        => Assert.False(Hotkey.Is(TypedOnly(character), 'd'));

    [Fact]
    public void ControlAndAltCombinationsAreLeftToTheTerminal()
    {
        Assert.False(Hotkey.Is(Typed.Of(ConsoleKey.D, '\u0004', ConsoleModifiers.Control), 'd'));
        Assert.False(Hotkey.Is(Typed.Of(ConsoleKey.D, 'd', ConsoleModifiers.Alt), 'd'));
        Assert.False(Hotkey.Is(Typed.Of(ConsoleKey.A, 'a', ConsoleModifiers.Control | ConsoleModifiers.Alt), 'a'));
    }

    [Fact]
    public void TheTabStripQuitsOnAnyEncodingOfQ()
    {
        var model = new TabsModel([new StubPage("One")]);

        Assert.Equal(TabKeyResult.Quit, model.Handle(TypedOnly('q')));
        Assert.Equal(TabKeyResult.Quit, model.Handle(TypedOnly('й')));
        Assert.Equal(TabKeyResult.Quit, model.Handle(Typed.Of(ConsoleKey.Q, 'й')));
    }

    [Fact]
    public void TheAccountsPageActsOnAnyEncodingOfItsLetters()
    {
        var ops = new FakeAccountOperations();
        ops.Accounts.Add(FakeAccountOperations.Account("alice", "1001", 'a'));
        ops.PendingCleanup = 1;
        var page = new AccountsPage(ops);
        page.Activate();

        Assert.NotNull(page.Handle(TypedOnly('a')).Work);
        Assert.NotNull(page.Handle(TypedOnly('ф')).Work);
        Assert.NotNull(page.Handle(TypedOnly('s')).Work);
        Assert.NotNull(page.Handle(TypedOnly('ы')).Work);

        page.Handle(TypedOnly('в'));
        Assert.True(page.IsModal);
        page.Handle(TypedOnly('н'));
        Assert.False(page.IsModal);
        Assert.Contains("remove:" + new string('a', 32), ops.Calls);

        page.Handle(TypedOnly('с'));
        Assert.Contains("cleanup", ops.Calls);
    }

    [Fact]
    public void TheReadOnlyPagesRefreshOnAnyEncodingOfR()
    {
        var reads = 0;
        var page = new DiagnosticsPage(() =>
        {
            reads++;
            return new DiagnosticReport("Windows", "X64", ".NET", new InstallationScan([], []), DiagnosticFixture.Environment);
        }, () => "1.0");
        page.Activate();

        page.Handle(TypedOnly('r'));
        page.Handle(TypedOnly('к'));
        page.Handle(Typed.Of(ConsoleKey.R, 'к'));

        Assert.Equal(4, reads);
    }

    [Fact]
    public void AnyKeyOtherThanYKeepsTheAccountEvenIfItIsCyrillic()
    {
        var ops = new FakeAccountOperations();
        ops.Accounts.Add(FakeAccountOperations.Account("alice", "1001", 'a'));
        var page = new AccountsPage(ops);
        page.Activate();
        page.Handle(Typed.Letter('d'));

        page.Handle(TypedOnly('в'));

        Assert.Single(ops.Accounts);
        Assert.DoesNotContain(ops.Calls, call => call.StartsWith("remove"));
    }
}
