using MyKa.Spoofer.Terminal.Tabs;
using MyKa.Spoofer.Tests.Support;
using Xunit;

namespace MyKa.Spoofer.Tests;

public sealed class TabsAppTests
{
    private readonly FakeSurface surface = new();
    private readonly FakeKeys keys = new();
    private readonly StubPage first = new("Accounts");
    private readonly StubPage second = new("Diagnostics");
    private readonly List<(int Tick, Action Action)> script = [];
    private readonly CancellationTokenSource cancellation = new();
    private double clock;
    private int tick;

    /// <summary>Scripted input: what happens on each pass of the loop, since keys waiting at the start are discarded.</summary>
    private void At(int onTick, ConsoleKeyInfo key) => script.Add((onTick, () => keys.Press(key)));

    private void At(int onTick, Action action) => script.Add((onTick, action));

    private int Run(bool animate = false)
    {
        var app = new TabsApp([first, second], surface, keys, TabsView.StatusLine("1.0"), animate,
            () => clock, Sleep, cancellation.Token);
        return app.Run();
    }

    private void Sleep(int milliseconds)
    {
        tick++;
        clock += milliseconds / 1000.0;
        if (tick > 500) throw new InvalidOperationException("The loop never ended.");
        foreach (var step in script.Where(step => step.Tick == tick).ToArray()) step.Action();
    }

    private string[] Text(int frame) => Capture.Lines(surface.Frames[frame], surface.ShowSizes[0].Width);

    [Fact]
    public void QuittingReturnsZeroAndGivesTheTerminalBack()
    {
        At(2, Typed.Escape);

        var code = Run();

        Assert.Equal(0, code);
        Assert.Equal(1, surface.Entered);
        Assert.Equal(1, surface.Left);
    }

    [Fact]
    public void TakesOverTheScreenOnceForTheWholeSession()
    {
        At(2, Typed.Right);
        At(3, Typed.Left);
        At(4, Typed.Digit(2));
        At(6, Typed.Letter('q'));

        Run();

        Assert.Equal(1, surface.Entered);
        Assert.Single(surface.ShowSizes);
    }

    [Fact]
    public void TheFirstTabIsActiveFromTheStart()
    {
        At(1, Typed.Escape);

        Run();

        Assert.Equal(1, first.Activations);
        Assert.Contains(Text(0), line => line.Contains("Accounts content"));
    }

    [Fact]
    public void SwitchingTabsChangesWhatIsShownInPlace()
    {
        At(2, Typed.Right);
        At(4, Typed.Escape);

        Run();

        Assert.Equal(1, second.Activations);
        Assert.Contains(Text(surface.Frames.Count - 1), line => line.Contains("Diagnostics content"));
        Assert.DoesNotContain(Text(surface.Frames.Count - 1), line => line.Contains("Accounts content"));
    }

    [Fact]
    public void EveryFrameIsTheSameSizeSoNothingGrowsDownTheScreen()
    {
        At(2, Typed.Right);
        At(3, Typed.Left);
        At(4, Typed.Digit(2));
        At(6, Typed.Escape);

        Run(animate: true);

        Assert.True(surface.Frames.Count > 5);
        Assert.All(surface.Frames, frame => Assert.Equal(29, Capture.Lines(frame, 100).Length));
    }

    [Fact]
    public void KeysThePageIsGivenAreTheOnesTheTabStripDoesNotUse()
    {
        At(2, Typed.Letter('a'));
        At(3, Typed.Right);
        At(4, Typed.Letter('b'));
        At(5, Typed.Escape);

        Run();

        Assert.Equal(new[] { Typed.Letter('a') }, first.Received);
        Assert.Equal(new[] { Typed.Letter('b') }, second.Received);
    }

    [Fact]
    public void SlowWorkRunsOnlyAfterTheFrameThatExplainsItIsOnScreen()
    {
        var events = new List<string>();
        first.NextWork = () => events.Add($"work after {surface.Frames.Count} frames");
        At(2, Typed.Letter('a'));
        At(4, Typed.Escape);

        Run();

        // The frame drawn just before the work is the last one on screen when it starts.
        Assert.Equal(new[] { "work after 2 frames" }, events);
        Assert.True(surface.Frames.Count >= 2);
    }

    [Fact]
    public void KeysTypedWhileSlowWorkBlockedAreDiscarded()
    {
        first.NextWork = () => keys.Press(Typed.Letter('d'), Typed.Letter('y'), Typed.Escape);
        At(2, Typed.Letter('a'));
        At(5, Typed.Escape);

        var code = Run();

        Assert.Equal(0, code);
        Assert.Equal(new[] { Typed.Letter('a') }, first.Received);
        Assert.Equal(5, tick);
    }

    [Fact]
    public void KeysAlreadyWaitingAtTheStartAreDiscarded()
    {
        keys.Press(Typed.Letter('z'), Typed.Escape);
        At(2, Typed.Escape);

        Run();

        Assert.Empty(first.Received);
        Assert.Equal(2, tick);
    }

    [Fact]
    public void WithoutAnimationNothingIsRedrawUntilAKeyArrives()
    {
        At(8, Typed.Letter('x'));
        At(10, Typed.Escape);

        Run(animate: false);

        // The first frame, plus one redraw for the key; the idle passes in between draw nothing.
        Assert.Equal(2, surface.Frames.Count);
    }

    [Fact]
    public void WithAnimationEveryPassDrawsAndTheLogoMoves()
    {
        surface.Size = (120, 50);
        clock = 4.0;   // mid-turn, where the logo moves fastest
        At(6, Typed.Escape);

        Run(animate: true);

        // The opening frame, then one for each of the six passes before the key.
        Assert.Equal(7, surface.Frames.Count);
        var frames = Enumerable.Range(0, surface.Frames.Count).Select(i => string.Join('\n', Text(i))).ToArray();
        Assert.True(frames.Distinct().Count() > 3, "the logo should change from frame to frame");
    }

    [Fact]
    public void ResizingRedrawsForTheNewSizeWithoutLeavingTheScreen()
    {
        At(2, () => surface.Size = (80, 24));
        At(5, Typed.Escape);

        Run();

        Assert.Equal(new[] { (100, 30), (80, 24) }, surface.ShowSizes);
        Assert.Equal(1, surface.Entered);
        Assert.Equal(1, surface.Left);
        Assert.Equal(23, Capture.Lines(surface.Frames[^1], 80).Length);
    }

    [Fact]
    public void ResizingKeepsTheCurrentTab()
    {
        At(2, Typed.Right);
        At(3, () => surface.Size = (90, 28));
        At(5, Typed.Escape);

        Run();

        Assert.Contains(Capture.Lines(surface.Frames[^1], 90), line => line.Contains("Diagnostics content"));
        Assert.Equal(1, second.Activations);
    }

    [Fact]
    public void CancellingLeavesTheScreenBeforeStopping()
    {
        At(2, () => cancellation.Cancel());

        Assert.Throws<OperationCanceledException>(() => Run());

        Assert.Equal(1, surface.Left);
    }

    [Fact]
    public void CancellingBeforeStartingNeverTakesTheScreen()
    {
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => Run());

        Assert.Equal(0, surface.Entered);
        Assert.Empty(surface.ShowSizes);
    }

    [Fact]
    public void AFailureInAPageStillGivesTheTerminalBack()
    {
        first.NextWork = () => throw new InvalidOperationException("boom");
        At(2, Typed.Letter('a'));

        Assert.Throws<InvalidOperationException>(() => Run());

        Assert.Equal(1, surface.Left);
    }

    [Fact]
    public void ATooSmallWindowDoesNotDispatchHiddenActionsOrSwitchTabs()
    {
        surface.Size = (30, 8);
        At(1, Typed.Letter('a'));
        At(2, Typed.Right);
        At(3, Typed.Escape);

        Run();

        Assert.Empty(first.Received);
        Assert.Equal(0, second.Activations);
    }

    [Fact]
    public void ShrinkingTheWindowBlocksAnActionQueuedWithTheResize()
    {
        At(1, () => { surface.Size = (30, 8); keys.Press(Typed.Letter('a')); });
        At(2, Typed.Escape);

        Run();

        Assert.Empty(first.Received);
        Assert.Equal(new[] { (100, 30), (30, 8) }, surface.ShowSizes);
    }

    [Fact]
    public void AHiddenModalCannotConsumeTheAdvertisedQuitKey()
    {
        first.Modal = true;
        surface.Size = (30, 8);
        At(1, Typed.Escape);
        At(2, () => cancellation.Cancel()); // bounds the old loop that swallowed Esc

        Assert.Equal(0, Run());
        Assert.Empty(first.Received);
    }

    [Fact]
    public void QueuedRemovalKeysShowTheQuestionBeforeActingOnTheAnswer()
    {
        var ops = new FakeAccountOperations();
        ops.Accounts.Add(FakeAccountOperations.Account("alice", "1001", 'a'));
        var page = new AccountsPage(ops);
        At(1, () => keys.Press(Typed.Letter('d'), Typed.Letter('y')));
        At(4, Typed.Escape);
        var app = new TabsApp([page], surface, keys, TabsView.StatusLine("1.0"), false,
            () => clock, Sleep, cancellation.Token);

        app.Run();

        Assert.Empty(ops.Accounts);
        var frames = surface.Frames.Select(frame => string.Join('\n', Capture.Lines(frame, 100))).ToArray();
        var question = Array.FindIndex(frames, frame => frame.Contains("Press Y to remove it."));
        var removed = Array.FindIndex(frames, frame => frame.Contains("Local account removed."));
        Assert.True(question >= 0 && question < removed, "the confirmation must be visible before removal");
    }


    [Fact]
    public void HelpContainsActionsAndReturnsWithoutReloadingTheUnderlyingPage()
    {
        At(1, Typed.Of(ConsoleKey.F1));
        At(2, Typed.Letter('a'));
        At(3, Typed.Right);
        At(4, Typed.Digit(2));
        At(5, Typed.Escape); // closes help
        At(6, Typed.Letter('b'));
        At(7, Typed.Escape); // quits

        Run();

        Assert.Equal(new[] { Typed.Letter('b') }, first.Received);
        Assert.Equal(1, first.Activations);
        Assert.Equal(0, second.Activations);
        Assert.Contains(surface.Frames, frame => Capture.Lines(frame, 100).Any(line => line.Contains("KEYBOARD HELP")));
    }

    [Fact]
    public void PausingMotionStopsIdleRedrawsAndUpdatesTheHint()
    {
        surface.Size = (120, 50);
        var atPause = 0;
        var whilePaused = 0;
        At(1, Typed.Of(ConsoleKey.Spacebar, ' '));
        At(2, () => atPause = surface.Frames.Count);
        At(6, () => whilePaused = surface.Frames.Count);
        At(7, Typed.Escape);

        Run(animate: true);

        Assert.Equal(atPause, whilePaused);
        Assert.Contains(Capture.Lines(surface.Frames[^1], 120), line => line.Contains("Space resume"));
        Assert.Empty(first.Received);
    }

    [Fact]
    public void ATooSmallWindowCanStillBeQuit()
    {
        surface.Size = (30, 8);
        At(2, Typed.Escape);

        var code = Run();

        Assert.Equal(0, code);
        Assert.Contains(Capture.Lines(surface.Frames[0], 30), line => line.Contains("too small"));
    }
}
