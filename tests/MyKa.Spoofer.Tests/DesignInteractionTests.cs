using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace MyKa.Spoofer.Tests;

public sealed class DesignInteractionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(50, 12)]
    [InlineData(60, 20)]
    [InlineData(80, 24)]
    [InlineData(120, 40)]
    public void EssentialChromeIsReadableAtEverySupportedSize(int width, int height)
    {
        var ops = new FakeAccountOperations();
        ops.Accounts.Add(FakeAccountOperations.Account("alice", "1001", 'a'));
        var accounts = new AccountsPage(ops);
        accounts.Activate();
        var model = new TabsModel([accounts,
            new DiagnosticsPage(() => new DiagnosticReport("Windows", "X64", ".NET", new([], []), DiagnosticFixture.Environment), () => null),
            new AdaptersPage(() => new([], null))]);

        var lines = Capture.Lines(TabsView.Build(model, TabsLayout.For(width, height),
            LogoMotion.Still, TabsView.StatusLine(null)), width);

        output.WriteLine(string.Join('\n', lines));
        Assert.Equal(height - 1, lines.Length);
        Assert.Contains(lines, line => line.Contains("Login browser: WebView2 unavailable"));
        Assert.Contains(lines, line => line.Contains("1 Accounts") && line.Contains("2 Diagnostics") && line.Contains("3 Adapters"));
        Assert.Contains("D remove", lines[^2]);
        Assert.Contains("R reload", lines[^2]);
        Assert.Contains("F1 help", lines[^1]);
        Assert.Contains("Space pause", lines[^1]);
        Assert.Contains("Esc quit", lines[^1]);
        Assert.DoesNotContain(Cells.Ellipsis, lines[^2] + lines[^1]);
    }

    [Fact]
    public void HelpReturnsToTheSameSelectionWithoutReloadingOrClearingFeedback()
    {
        var ops = new FakeAccountOperations();
        ops.Accounts.Add(FakeAccountOperations.Account("alice", "1001", 'a'));
        ops.Accounts.Add(FakeAccountOperations.Account("bob", "2002", 'b'));
        var accounts = new AccountsPage(ops);
        accounts.Activate();
        accounts.Handle(Typed.Down);
        var before = Capture.Text(accounts.Render(96, 20));
        var model = new TabsModel([accounts]);

        model.Handle(Typed.Of(ConsoleKey.F1));
        Assert.True(model.HelpOpen);
        Assert.IsType<HelpPage>(model.Current);
        model.Handle(Typed.Escape);

        Assert.False(model.HelpOpen);
        Assert.Same(accounts, model.Current);
        Assert.Equal(before, Capture.Text(accounts.Render(96, 20)));
        Assert.Equal(new[] { "list" }, ops.Calls);
    }

    [Fact]
    public void EveryHelpSectionIsReachableInTheSmallestPage()
    {
        var help = new HelpPage();
        var seen = new List<string>();
        for (var i = 0; i < 150; i++)
        {
            var lines = help.Render(46, 4);
            Assert.True(lines.Count <= 4);
            Assert.All(lines, line => Assert.True(line.Width <= 46));
            seen.Add(Capture.Text(lines));
            help.Handle(Typed.Down);
        }

        var text = string.Join('\n', seen);
        Assert.Contains("Shift+Tab", text);
        Assert.Contains("Confirmation", text);
        Assert.Contains("R reloads", text);
        Assert.Contains("Space pauses", text);
        Assert.Contains("screen reader", text.Replace('\n', ' '));
        help.Handle(Typed.Home);
        Assert.Contains("Navigation", Capture.Text(help.Render(46, 4)));
    }

    [Fact]
    public void SpaceTogglesTheInitialMotionPreferenceAndDoesNotReachThePage()
    {
        var page = new StubPage("Accounts");
        var model = new TabsModel([page], animate: false);
        Assert.False(model.Animate);

        Assert.Equal(TabKeyResult.Updated, model.Handle(Typed.Of(ConsoleKey.Spacebar, ' ')));
        Assert.True(model.Animate);
        model.Handle(Typed.Of(ConsoleKey.Spacebar, ' '));
        Assert.False(model.Animate);
        Assert.Empty(page.Received);
    }

    [Fact]
    public void HelpAndMotionKeysDoNotOverrideAccountConfirmation()
    {
        var page = new StubPage("Accounts") { Modal = true };
        var model = new TabsModel([page]);
        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Of(ConsoleKey.F1)));
        Assert.Equal(TabKeyResult.Ignored, model.Handle(Typed.Of(ConsoleKey.Spacebar, ' ')));
        Assert.False(model.HelpOpen);
        Assert.True(model.Animate);
    }

    [Fact]
    public void FailedAccountReadIsUnknownAndCannotTriggerAccountActionsUntilRetrySucceeds()
    {
        var ops = new FakeAccountOperations { ListFailure = new IOException("locked") };
        var page = new AccountsPage(ops);
        page.Activate();

        var failed = Capture.Text(page.Render(46, 4));
        Assert.Contains("unavailable", failed);
        Assert.Contains("Error:", failed);
        Assert.DoesNotContain("none saved", failed);
        Assert.DoesNotContain("No saved accounts", failed);
        foreach (var key in new[] { Typed.Letter('a'), Typed.Enter, Typed.Letter('d'), Typed.Letter('c') })
            Assert.Null(page.Handle(key).Work);
        Assert.Equal(new[] { "list" }, ops.Calls);

        ops.ListFailure = null;
        ops.Accounts.Add(FakeAccountOperations.Account("alice", "1001", 'a'));
        page.Handle(Typed.Letter('r'));

        var recovered = Capture.Text(page.Render(46, 12));
        Assert.Contains("alice", recovered);
        Assert.DoesNotContain("unavailable", recovered);
        Assert.DoesNotContain("Error:", recovered);
        Assert.NotNull(page.Handle(Typed.Enter).Work);
    }

    [Fact]
    public void LoadFailureDetailsCanBeScrolledWithoutLosingTheRetryInstruction()
    {
        var ops = new FakeAccountOperations
        {
            ListFailure = new InvalidDataException(new string('x', 200) + " Recover from the backup."),
        };
        var page = new AccountsPage(ops);
        page.Activate();
        page.Render(46, 4);
        page.Handle(Typed.End);

        var text = Capture.Text(page.Render(46, 4));
        Assert.Contains("Recover from the backup.", text);
        Assert.Contains("Press R", text);
    }

    [Fact]
    public void RemovalAlwaysShowsTheFullNumericIdentityAtMinimumSize()
    {
        const string id = "18446744073709551615";
        var ops = new FakeAccountOperations();
        ops.Accounts.Add(FakeAccountOperations.Account(new string('n', 128), id, 'a'));
        var page = new AccountsPage(ops);
        page.Activate();
        page.Handle(Typed.Delete);

        var lines = page.Render(46, 4);
        var text = Capture.Text(lines);
        Assert.Equal(4, lines.Count);
        Assert.Contains("Roblox user ID: " + id, text);
        Assert.Contains("Press Y to remove", text);
        Assert.All(lines, line => Assert.True(line.Width <= 46));
    }
}
