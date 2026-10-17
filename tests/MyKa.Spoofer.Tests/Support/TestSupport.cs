using MyKa.Spoofer.Accounts;
using MyKa.Spoofer.Terminal;
using MyKa.Spoofer.Terminal.Tabs;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Tests.Support;

internal static class Typed
{
    public static ConsoleKeyInfo Of(ConsoleKey key, char character = '\0', ConsoleModifiers modifiers = 0)
        => new(character, key, modifiers.HasFlag(ConsoleModifiers.Shift), modifiers.HasFlag(ConsoleModifiers.Alt),
            modifiers.HasFlag(ConsoleModifiers.Control));

    public static ConsoleKeyInfo Letter(char letter)
        => Of((ConsoleKey)(int)char.ToUpperInvariant(letter), char.ToLowerInvariant(letter));

    public static ConsoleKeyInfo Digit(int digit) => Of(ConsoleKey.D0 + digit, (char)('0' + digit));

    public static ConsoleKeyInfo Up => Of(ConsoleKey.UpArrow);
    public static ConsoleKeyInfo Down => Of(ConsoleKey.DownArrow);
    public static ConsoleKeyInfo Left => Of(ConsoleKey.LeftArrow);
    public static ConsoleKeyInfo Right => Of(ConsoleKey.RightArrow);
    public static ConsoleKeyInfo PageUp => Of(ConsoleKey.PageUp);
    public static ConsoleKeyInfo PageDown => Of(ConsoleKey.PageDown);
    public static ConsoleKeyInfo Home => Of(ConsoleKey.Home);
    public static ConsoleKeyInfo End => Of(ConsoleKey.End);
    public static ConsoleKeyInfo Enter => Of(ConsoleKey.Enter, '\r');
    public static ConsoleKeyInfo Escape => Of(ConsoleKey.Escape, '\u001b');
    public static ConsoleKeyInfo Delete => Of(ConsoleKey.Delete);
    public static ConsoleKeyInfo Tab => Of(ConsoleKey.Tab, '\t');
    public static ConsoleKeyInfo ShiftTab => Of(ConsoleKey.Tab, '\t', ConsoleModifiers.Shift);
}

/// <summary>Renders widgets to text the way a terminal without colour would show them.</summary>
internal static class Capture
{
    public static string[] Lines(IRenderable renderable, int width)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = width;
        console.Write(renderable);
        return writer.ToString().TrimEnd('\r', '\n').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
    }

    public static string Text(IEnumerable<Line> lines) => string.Join('\n', lines.Select(line => line.Text));
}

internal sealed class FakeKeys : IKeySource
{
    private readonly Queue<ConsoleKeyInfo> queue = new();

    public bool Available => queue.Count > 0;

    public ConsoleKeyInfo Read() => queue.Dequeue();

    public void Press(params ConsoleKeyInfo[] keys)
    {
        foreach (var key in keys) queue.Enqueue(key);
    }
}

internal sealed class FakeSurface : ITerminalSurface
{
    public (int Width, int Height) Size { get; set; } = (100, 30);

    public int Entered { get; private set; }

    public int Left { get; private set; }

    /// <summary>The first frame of every show, then every frame drawn after it, in order.</summary>
    public List<IRenderable> Frames { get; } = [];

    public List<(int Width, int Height)> ShowSizes { get; } = [];

    public IDisposable Enter()
    {
        Entered++;
        return new Scope(this);
    }

    public SurfaceExit Show(IRenderable first, Func<IFrameSink, SurfaceExit> body)
    {
        ShowSizes.Add(Size);
        Frames.Add(first);
        return body(new Sink(this));
    }

    private sealed class Sink(FakeSurface surface) : IFrameSink
    {
        public void Draw(IRenderable frame) => surface.Frames.Add(frame);
    }

    private sealed class Scope(FakeSurface surface) : IDisposable
    {
        public void Dispose() => surface.Left++;
    }
}

internal sealed class StubPage(string title) : ITabPage
{
    public int Activations { get; private set; }

    public List<ConsoleKeyInfo> Received { get; } = [];

    public bool Modal { get; set; }

    /// <summary>Slow work returned for the next key only.</summary>
    public Action? NextWork { get; set; }

    public string Title => title;

    public bool IsModal => Modal;

    public string Hints => title + " hints";

    public void Activate() => Activations++;

    public PageResult Handle(ConsoleKeyInfo key)
    {
        Received.Add(key);
        var work = NextWork;
        NextWork = null;
        return new PageResult(true, work);
    }

    public IReadOnlyList<Line> Render(int width, int rows) => [Line.Of(title + " content")];
}

internal sealed class FakeAccountOperations : IAccountOperations
{
    public List<SavedAccount> Accounts { get; } = [];

    public int PendingCleanup { get; set; }

    public Exception? ListFailure { get; set; }

    public Exception? ActionFailure { get; set; }

    /// <summary>What Add returns: 0 saves a new account, anything else changes nothing.</summary>
    public int AddExitCode { get; set; }

    public List<string> Calls { get; } = [];

    /// <summary>Runs inside Add, so tests can do things while the sign-in window would be open.</summary>
    public Action? DuringAdd { get; set; }

    public static SavedAccount Account(string name, string userId, char idCharacter)
        => new(new string(idCharacter, 32), new string('f', 32),
            userId, name, name + " display", new DateTimeOffset(2026, 10, 15, 19, 20, 11, TimeSpan.Zero));

    public AccountListing List()
    {
        Calls.Add("list");
        if (ListFailure is not null) throw ListFailure;
        return new AccountListing(Accounts.ToArray(), PendingCleanup);
    }

    public int Add(Action<string> report)
    {
        Calls.Add("add");
        DuringAdd?.Invoke();
        if (ActionFailure is not null) throw ActionFailure;
        if (AddExitCode != 0)
        {
            report("Sign-in cancelled. Saved accounts were not changed.");
            return AddExitCode;
        }
        var created = Account("newuser", "9001", (char)('a' + Accounts.Count));
        Accounts.Add(created);
        report($"Saved verified Roblox account: {created.Username} (user {created.UserId}).");
        report("Local account ID: " + created.Id);
        return 0;
    }

    public int SignInAgain(string accountId, Action<string> report)
    {
        Calls.Add("again:" + accountId);
        if (ActionFailure is not null) throw ActionFailure;
        var account = Accounts.First(a => a.Id == accountId);
        report($"Saved verified Roblox account: {account.Username} (user {account.UserId}).");
        return 0;
    }

    public int Remove(string accountId, Func<SavedAccount, bool> confirm, Action<string> report)
    {
        Calls.Add("remove:" + accountId);
        if (ActionFailure is not null) throw ActionFailure;
        var account = Accounts.First(a => a.Id == accountId);
        if (!confirm(account)) return 130;
        Accounts.Remove(account);
        report("Local account removed.");
        return 0;
    }

    public int OpenExperience(string accountId, string? placeId, Action<string> report)
    {
        Calls.Add("play:" + accountId);
        if (ActionFailure is not null) throw ActionFailure;
        report("Desktop handoff requested. RobloxPlayer launch and joined account are unverified.");
        return 0;
    }

    public int CleanUp(Action<string> report)
    {
        Calls.Add("cleanup");
        if (ActionFailure is not null) throw ActionFailure;
        report("Unfinished profile cleanup is complete.");
        PendingCleanup = 0;
        return 0;
    }
}
