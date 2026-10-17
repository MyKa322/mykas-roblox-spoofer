using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class AccountsPageTests
{
    private static readonly string[] Names = ["alice", "bob", "carol", "dave"];

    private readonly FakeAccountOperations ops = new();

    [Fact]
    public void PlayDefersTheWindowAndKeepsTheSelectedAccount()
    {
        var page = Open();
        page.Handle(Typed.Down);
        var result = page.Handle(Typed.Letter('p'));
        Assert.NotNull(result.Work);
        Assert.DoesNotContain(ops.Calls, call => call.StartsWith("play:"));
        Assert.Contains("bob", Show(page));
        result.Work!();
        Assert.Contains("play:" + ops.Accounts[1].Id, ops.Calls);
        Assert.StartsWith("► bob", SelectedRow(page));
        Assert.Contains("unverified", Show(page));
    }

    [Fact]
    public void PlayWithNoAccountExplainsHowToAddOne()
    {
        var page = Open(0);
        Assert.Null(page.Handle(Typed.Letter('p')).Work);
        Assert.Contains("Press A", Show(page));
    }

    [Fact]
    public void PlayIsDisabledAfterAnAccountReadError()
    {
        var page = Open();
        ops.ListFailure = new IOException();
        page.Handle(Typed.Letter('r'));
        Assert.Null(page.Handle(Typed.Letter('p')).Work);
        Assert.DoesNotContain(ops.Calls, call => call.StartsWith("play:"));
    }

    private AccountsPage Open(int accounts = 2)
    {
        for (var index = 0; index < accounts; index++)
            ops.Accounts.Add(FakeAccountOperations.Account(Names[index % Names.Length] + (index >= Names.Length ? index.ToString() : ""),
                ((index + 1) * 1001).ToString(), (char)('a' + index % 20)));
        var page = new AccountsPage(ops);
        page.Activate();
        return page;
    }

    private static string Show(ITabPage page, int width = 100, int rows = 14) => Capture.Text(page.Render(width, rows));

    private static PageResult Press(ITabPage page, ConsoleKeyInfo key)
    {
        var result = page.Handle(key);
        result.Work?.Invoke();
        return result;
    }

    private static string? SelectedRow(ITabPage page, int width = 100, int rows = 14)
        => page.Render(width, rows).Select(line => line.Text).SingleOrDefault(text => text.StartsWith('\u25BA'));

    [Fact]
    public void WithNoAccountsItSaysSoAndExplainsHowToAddOne()
    {
        var page = Open(0);

        var text = Show(page);

        Assert.Contains("ACCOUNTS", text);
        Assert.Contains("none saved", text);
        Assert.Contains("No saved accounts yet.", text);
        Assert.Contains("Press A to sign in to Roblox", text);
        Assert.Contains("never into this terminal", text);
    }

    [Fact]
    public void ListsEachAccountWithItsRobloxIdentityAndWhenItWasVerified()
    {
        var page = Open();

        var text = Show(page);

        Assert.Contains("2 saved", text);
        Assert.Contains("Account", text);
        Assert.Contains("Roblox user ID", text);
        Assert.Contains("Last verified (UTC)", text);
        Assert.Contains("alice", text);
        Assert.Contains("1001", text);
        Assert.Contains("bob", text);
        Assert.Contains("2002", text);
        Assert.Contains("2026-10-15 19:20:11Z", text);
    }

    [Fact]
    public void MarksTheSelectedAccountAndShowsItsLocalIdAndLoginStatus()
    {
        var page = Open();

        var text = Show(page);

        Assert.StartsWith("\u25BA alice", SelectedRow(page));
        Assert.Contains("Local account ID  " + new string('a', 32), text);
        Assert.Contains("Login status      not checked", text);
    }

    [Fact]
    public void UpAndDownMoveTheSelectionAndStopAtTheEnds()
    {
        var page = Open();

        page.Handle(Typed.Up);
        Assert.StartsWith("\u25BA alice", SelectedRow(page));
        page.Handle(Typed.Down);
        Assert.StartsWith("\u25BA bob", SelectedRow(page));
        Assert.Contains("Local account ID  " + new string('b', 32), Show(page));
        page.Handle(Typed.Down);
        Assert.StartsWith("\u25BA bob", SelectedRow(page));
        page.Handle(Typed.Home);
        Assert.StartsWith("\u25BA alice", SelectedRow(page));
        page.Handle(Typed.End);
        Assert.StartsWith("\u25BA bob", SelectedRow(page));
    }

    [Fact]
    public void MovingWithNoAccountsDoesNothing()
    {
        var page = Open(0);

        Assert.False(page.Handle(Typed.Down).Changed);
        Assert.False(page.Handle(Typed.End).Changed);
    }

    [Fact]
    public void ShowsOnlyTheKeysThatApplyToTheState()
    {
        Assert.Equal("A add account   C clean up   R reload", Open(0).Hints);
        var page = Open();
        Assert.Contains("S or Enter sign in again", page.Hints);
        Assert.Contains("D remove", page.Hints);
    }

    [Fact]
    public void AddingShowsWhatToDoThenRunsTheSignInOnceThatIsOnScreen()
    {
        var page = Open();

        var result = page.Handle(Typed.Letter('a'));

        Assert.True(result.Changed);
        Assert.NotNull(result.Work);
        Assert.Contains("Opening Roblox sign-in", Show(page));
        Assert.DoesNotContain("add", ops.Calls);

        result.Work!();

        Assert.Contains("add", ops.Calls);
    }

    [Fact]
    public void AfterASuccessfulAddTheNewAccountIsListedSelectedAndExplained()
    {
        var page = Open();

        Press(page, Typed.Letter('a'));

        var text = Show(page);
        Assert.Contains("3 saved", text);
        Assert.StartsWith("\u25BA newuser", SelectedRow(page));
        Assert.Contains("Saved verified Roblox account: newuser (user 9001).", text);
        Assert.DoesNotContain("Opening Roblox sign-in", text);
    }

    [Fact]
    public void AnAddThatIsCancelledChangesNothingAndSaysSo()
    {
        var page = Open();
        ops.AddExitCode = 130;

        Press(page, Typed.Letter('a'));

        var text = Show(page);
        Assert.Contains("Sign-in cancelled. Saved accounts were not changed.", text);
        Assert.Contains("2 saved", text);
        Assert.StartsWith("\u25BA alice", SelectedRow(page));
    }

    [Fact]
    public void ResultsAreColouredByHowTheyWent()
    {
        var page = Open();
        Press(page, Typed.Letter('a'));
        var saved = page.Render(100, 14).Single(line => line.Text.StartsWith("Saved verified"));

        ops.AddExitCode = 130;
        Press(page, Typed.Letter('a'));
        var cancelled = page.Render(100, 14).Single(line => line.Text.StartsWith("Sign-in cancelled"));

        Assert.Equal(Looks.Good, saved.Spans[0].Style);
        Assert.Equal(Looks.Info, cancelled.Spans[0].Style);
    }

    [Theory]
    [InlineData(ConsoleKey.S, 's')]
    [InlineData(ConsoleKey.Enter, '\r')]
    public void SigningInAgainUsesTheSelectedAccountAndKeepsItSelected(ConsoleKey key, char character)
    {
        var page = Open();
        page.Handle(Typed.Down);

        Press(page, Typed.Of(key, character));

        Assert.Contains("again:" + new string('b', 32), ops.Calls);
        Assert.StartsWith("\u25BA bob", SelectedRow(page));
        Assert.Contains("Saved verified Roblox account: bob (user 2002).", Show(page));
    }

    [Fact]
    public void SigningInAgainTellsWhichAccountThatWindowIsFor()
    {
        var page = Open();

        var result = page.Handle(Typed.Enter);

        Assert.Contains("Opening Roblox sign-in for alice", Show(page));
        Assert.NotNull(result.Work);
    }

    [Theory]
    [InlineData(ConsoleKey.S)]
    [InlineData(ConsoleKey.Enter)]
    [InlineData(ConsoleKey.D)]
    [InlineData(ConsoleKey.Delete)]
    public void ActionsOnAnAccountNeedOneToExist(ConsoleKey key)
    {
        var page = Open(0);

        var result = page.Handle(Typed.Of(key));

        Assert.Null(result.Work);
        Assert.Contains("There are no saved accounts. Press A to add one.", Show(page));
        Assert.False(page.IsModal);
    }

    [Theory]
    [InlineData(ConsoleKey.D, 'd')]
    [InlineData(ConsoleKey.Delete, '\0')]
    public void RemovingFirstAsksInsideThePage(ConsoleKey key, char character)
    {
        var page = Open();

        page.Handle(Typed.Of(key, character));

        var text = Show(page);
        Assert.True(page.IsModal);
        Assert.Contains("Account: alice", text);
        Assert.Contains("Roblox user ID: 1001", text);
        Assert.Contains("Other Roblox sessions are not revoked.", text);
        Assert.Contains("Press Y to remove it. Any other key keeps it.", text);
        Assert.DoesNotContain(ops.Calls, call => call.StartsWith("remove"));
        Assert.StartsWith("Y remove", page.Hints);
    }

    [Fact]
    public void YConfirmsTheRemoval()
    {
        var page = Open();
        page.Handle(Typed.Letter('d'));

        page.Handle(Typed.Letter('y'));

        var text = Show(page);
        Assert.False(page.IsModal);
        Assert.Contains("remove:" + new string('a', 32), ops.Calls);
        Assert.Contains("1 saved", text);
        Assert.Contains("Local account removed.", text);
        Assert.DoesNotContain("alice", text.Replace("Remove alice", ""));
        Assert.StartsWith("\u25BA bob", SelectedRow(page));
    }

    [Theory]
    [InlineData(ConsoleKey.N, 'n')]
    [InlineData(ConsoleKey.Escape, '\u001b')]
    [InlineData(ConsoleKey.Enter, '\r')]
    [InlineData(ConsoleKey.Spacebar, ' ')]
    public void AnyOtherKeyKeepsTheAccount(ConsoleKey key, char character)
    {
        var page = Open();
        page.Handle(Typed.Letter('d'));

        page.Handle(Typed.Of(key, character));

        Assert.False(page.IsModal);
        Assert.DoesNotContain(ops.Calls, call => call.StartsWith("remove"));
        Assert.Contains("alice was kept.", Show(page));
        Assert.Contains("2 saved", Show(page));
    }

    [Fact]
    public void RemovingTheLastAccountReturnsToTheEmptyState()
    {
        var page = Open(1);
        page.Handle(Typed.Letter('d'));

        page.Handle(Typed.Letter('y'));

        var text = Show(page);
        Assert.Contains("none saved", text);
        Assert.Contains("No saved accounts yet.", text);
    }

    [Fact]
    public void CleanUpRunsAndClearsTheNotice()
    {
        ops.PendingCleanup = 2;
        var page = Open();
        Assert.Contains("2 browser profile(s) awaiting cleanup. Press C to retry.", Show(page));

        page.Handle(Typed.Letter('c'));

        var text = Show(page);
        Assert.Contains("cleanup", ops.Calls);
        Assert.Contains("Unfinished profile cleanup is complete.", text);
        Assert.DoesNotContain("awaiting cleanup", text);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException), "Another account operation may be open. Close it and retry.")]
    [InlineData(typeof(InvalidDataException), "Account metadata is invalid. The file was preserved; recover accounts.json from its backup.")]
    [InlineData(typeof(ArgumentException), "That account was not found. Use accounts list.")]
    public void ExpectedFailuresBecomeMessagesInsteadOfCrashes(Type type, string message)
    {
        var page = Open();
        ops.ActionFailure = (Exception)Activator.CreateInstance(type, message)!;

        Press(page, Typed.Letter('a'));

        var text = Show(page);
        Assert.Contains("Error: " + message, text);
        Assert.Contains("2 saved", text);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(System.Security.SecurityException))]
    public void AccessProblemsGetTheGenericPermissionsMessage(Type type)
    {
        var page = Open();
        ops.ActionFailure = (Exception)Activator.CreateInstance(type, "C:\\secret\\path was locked")!;

        Press(page, Typed.Letter('c'));

        var text = Show(page, 140);
        Assert.Contains("Account data could not be accessed. Close other account operations and check local permissions.", text);
        Assert.DoesNotContain("secret", text);
    }

    [Fact]
    public void ACancelledOperationStopsTheWholeProgram()
    {
        var page = Open();
        ops.ActionFailure = new OperationCanceledException();

        Assert.Throws<OperationCanceledException>(() => Press(page, Typed.Letter('a')));
    }

    [Fact]
    public void AnUnexpectedFailureIsNotSwallowed()
    {
        var page = Open();
        ops.ActionFailure = new NullReferenceException();

        Assert.Throws<NullReferenceException>(() => Press(page, Typed.Letter('a')));
    }

    [Fact]
    public void AFailureReadingTheListIsShownAndTheListIsEmpty()
    {
        ops.ListFailure = new IOException("locked");
        var page = new AccountsPage(ops);

        page.Activate();

        var text = Show(page);
        Assert.Contains("Error: Account data could not be accessed.", text);
        Assert.Contains("unavailable", text);
        Assert.DoesNotContain("none saved", text);
    }

    [Fact]
    public void ComingBackToTheTabStartsFreshWithTheLatestList()
    {
        var page = Open();
        Press(page, Typed.Letter('a'));
        page.Handle(Typed.Letter('d'));
        Assert.True(page.IsModal);

        page.Activate();

        var text = Show(page);
        Assert.False(page.IsModal);
        Assert.DoesNotContain("Saved verified", text);
        Assert.Contains("3 saved", text);
    }

    [Fact]
    public void OtherKeysDoNothing()
    {
        var page = Open();

        Assert.False(page.Handle(Typed.Letter('x')).Changed);
        Assert.False(page.Handle(Typed.Digit(2)).Changed);
        Assert.Equal(new[] { "list" }, ops.Calls);
    }

    [Fact]
    public void HotkeysUseThePhysicalKeySoTheyWorkOnAnyKeyboardLayout()
    {
        var page = Open();

        // A Russian layout types a different character on the key that is A on a US keyboard.
        var result = page.Handle(Typed.Of(ConsoleKey.A, '\u0444'));

        Assert.NotNull(result.Work);
    }

    [Fact]
    public void ALongNameIsCutAndTheSelectedRowStillSpansTheWholeWidth()
    {
        ops.Accounts.Add(FakeAccountOperations.Account(new string('n', 60), "1001", 'a'));
        var page = new AccountsPage(ops);
        page.Activate();

        var row = page.Render(80, 14).Single(line => line.Text.StartsWith('\u25BA'));

        Assert.Contains(Cells.Ellipsis, row.Text);
        Assert.Equal(80, row.Width);
        Assert.Equal(Looks.Selected, row.Spans[0].Style);
    }

    [Fact]
    public void TheDateColumnGoesFirstWhenTheWindowIsNarrow()
    {
        var page = Open();

        var wide = Show(page, 100);
        var narrow = Show(page, 40);

        Assert.Contains("Last verified (UTC)", wide);
        Assert.DoesNotContain("Last verified (UTC)", narrow);
        Assert.Contains("1001", narrow);
    }

    [Fact]
    public void ControlCharactersInANameNeverReachTheScreen()
    {
        ops.Accounts.Add(FakeAccountOperations.Account("evil\u001b[2J\u202Ename", "1001", 'a'));
        var page = new AccountsPage(ops);
        page.Activate();

        var text = Show(page);

        Assert.DoesNotContain('\u001b', text);
        Assert.DoesNotContain('\u202E', text);
        Assert.Contains("evil[2Jname", text);
    }

    [Fact]
    public void ALongListScrollsToKeepTheSelectionInViewAndSaysWhere()
    {
        var page = Open(0);
        for (var index = 0; index < 30; index++)
            ops.Accounts.Add(FakeAccountOperations.Account($"user{index:00}", (1000 + index).ToString(), (char)('a' + index % 20)));
        page.Activate();

        var top = Show(page, 100, 12);
        Assert.Contains("30 saved", top);
        Assert.Contains("1\u20136 of 30", top);
        Assert.Contains("user00", top);
        Assert.DoesNotContain("user29", top);

        page.Handle(Typed.End);
        var bottom = Show(page, 100, 12);
        Assert.Contains("user29", bottom);
        Assert.DoesNotContain("user00", bottom);
        Assert.StartsWith("\u25BA user29", SelectedRow(page, 100, 12));

        page.Handle(Typed.PageUp);
        Assert.Contains("\u25BA", Show(page, 100, 12));
        page.Handle(Typed.Home);
        Assert.StartsWith("\u25BA user00", SelectedRow(page, 100, 12));
    }

    [Fact]
    public void WalkingDownAListNeverLosesTheSelection()
    {
        var page = Open(0);
        for (var index = 0; index < 25; index++)
            ops.Accounts.Add(FakeAccountOperations.Account($"user{index:00}", (1000 + index).ToString(), (char)('a' + index % 20)));
        page.Activate();

        for (var step = 0; step < 24; step++)
        {
            page.Handle(Typed.Down);
            Assert.StartsWith($"\u25BA user{step + 1:00}", SelectedRow(page, 100, 10));
        }
    }

    [Theory]
    [InlineData(30, 4)]
    [InlineData(30, 5)]
    [InlineData(50, 6)]
    [InlineData(80, 8)]
    [InlineData(120, 14)]
    [InlineData(120, 40)]
    public void NeverReturnsMoreRowsThanItWasGiven(int width, int rows)
    {
        var page = Open(5);
        ops.PendingCleanup = 3;
        Press(page, Typed.Letter('a'));

        Assert.True(page.Render(width, rows).Count <= rows);
        page.Handle(Typed.Letter('d'));
        Assert.True(page.IsModal);
        Assert.True(page.Render(width, rows).Count <= rows);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void TheRemovalInstructionIsVisibleHoweverShortTheWindow(int rows)
    {
        var page = Open();
        page.Handle(Typed.Letter('d'));

        Assert.Contains("Press Y to remove it.", Show(page, 60, rows));
    }

    [Fact]
    public void WhenRoomIsShortMessagesSurviveAndDetailsGo()
    {
        var page = Open();
        Press(page, Typed.Letter('a'));

        var text = Show(page, 100, 5);

        Assert.Contains("Saved verified Roblox account: newuser (user 9001).", text);
        Assert.Contains("Local account ID: ", text);
        Assert.DoesNotContain("Login status", text);
        Assert.DoesNotContain("Local account ID  ", text);
    }

    [Fact]
    public void WhenThereIsRoomEverythingIsShown()
    {
        ops.PendingCleanup = 1;
        var page = Open();
        Press(page, Typed.Letter('a'));

        var text = Show(page, 100, 20);

        Assert.Contains("Saved verified Roblox account", text);
        Assert.Contains("1 browser profile(s) awaiting cleanup", text);
        Assert.Contains("Login status", text);
    }
}
