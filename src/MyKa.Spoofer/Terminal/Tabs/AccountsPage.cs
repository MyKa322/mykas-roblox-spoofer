using System.Globalization;
using System.Security;
using MyKa.Spoofer.Accounts;
using Spectre.Console;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>
/// The saved accounts and the actions on them. Everything happens inside this one tab: results appear as messages
/// under the list, and removal asks its question here rather than in a separate prompt.
/// </summary>
internal sealed class AccountsPage(IAccountOperations operations) : ITabPage
{
    private const int Gap = 2;
    private const int DateWidth = 20;
    private const int MinBodyRows = 2;
    private const string IdHeading = "Roblox user ID";

    private readonly List<PageMessage> messages = [];
    private readonly Scroller scroller = new();
    private AccountListing listing = new([], 0);
    private SavedAccount? pendingRemoval;
    private string? loadError;
    private int errorRows;
    private int selected;
    private int bodyRows = 6;

    public string Title => "Accounts";

    public bool IsModal => pendingRemoval is not null;

    public string Hints => pendingRemoval is not null
        ? "Y remove   any other key keeps the account"
        : loadError is not null ? "R retry   ↑ ↓ scroll"
        : listing.Accounts.Count == 0
            ? "A add account   C clean up   R reload"
            : "A add   P play   S or Enter sign in again   D remove   C clean up   R reload   ↑ ↓ select";

    public string HintsFor(int width)
        => Cells.Width(Hints) <= width ? Hints : "A add P play S login D remove C clean R reload";

    private SavedAccount? Current
        => selected >= 0 && selected < listing.Accounts.Count ? listing.Accounts[selected] : null;

    public void Activate()
    {
        messages.Clear();
        pendingRemoval = null;
        Reload(null, null);
    }

    public PageResult Handle(ConsoleKeyInfo key)
    {
        if (pendingRemoval is { } account) return Confirm(account, key);
        if (Hotkey.Is(key, 'r'))
        {
            messages.Clear();
            Reload(Current?.Id, null);
            return PageResult.Redraw;
        }
        if (loadError is not null)
            return scroller.Handle(key, errorRows, bodyRows) ? PageResult.Redraw : PageResult.None;
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: return MoveTo(selected - 1);
            case ConsoleKey.DownArrow: return MoveTo(selected + 1);
            case ConsoleKey.PageUp: return MoveTo(selected - Math.Max(1, bodyRows - 1));
            case ConsoleKey.PageDown: return MoveTo(selected + Math.Max(1, bodyRows - 1));
            case ConsoleKey.Home: return MoveTo(0);
            case ConsoleKey.End: return MoveTo(listing.Accounts.Count - 1);
            case ConsoleKey.Enter: return WithAccount(StartSignIn);
            case ConsoleKey.Delete: return WithAccount(AskToRemove);
        }

        if (Hotkey.Is(key, 'a')) return StartSignIn(null);
        if (Hotkey.Is(key, 's')) return WithAccount(StartSignIn);
        if (Hotkey.Is(key, 'p')) return WithAccount(StartExperience);
        if (Hotkey.Is(key, 'd')) return WithAccount(AskToRemove);
        if (Hotkey.Is(key, 'c')) return Clean();
        return PageResult.None;
    }

    public IReadOnlyList<Line> Render(int width, int rows)
    {
        if (pendingRemoval is { } doomed) return ConfirmationLines(doomed, width, rows);
        if (loadError is not null) return LoadFailureLines(width, rows);

        var accounts = listing.Accounts;
        (Line? Header, IReadOnlyList<Line> Rows) table = accounts.Count == 0 ? (null, EmptyState(width)) : Table(width);
        var frame = PageFrame.Arrange(table.Header, BottomLines(width), rows, MinBodyRows);
        bodyRows = frame.BodyRows;
        if (accounts.Count > 0) scroller.Reveal(selected, table.Rows.Count, bodyRows);
        else scroller.Home();
        scroller.Clamp(table.Rows.Count, bodyRows);
        var shown = table.Rows.Skip(scroller.Offset).Take(bodyRows).ToList();

        var count = accounts.Count == 0 ? "none saved" : $"{accounts.Count} saved";
        var position = accounts.Count == 0 ? "" : PageParts.Position(scroller.Offset, shown.Count, table.Rows.Count);
        return frame.Compose(PageParts.Heading("ACCOUNTS", PageParts.Join(count, position)), shown);
    }

    private PageResult MoveTo(int index)
    {
        if (listing.Accounts.Count == 0) return PageResult.None;
        selected = Math.Clamp(index, 0, listing.Accounts.Count - 1);
        return PageResult.Redraw;
    }

    private PageResult WithAccount(Func<SavedAccount, PageResult> action)
    {
        if (Current is { } account) return action(account);
        messages.Clear();
        Say(Tone.Info, "There are no saved accounts. Press A to add one.");
        return PageResult.Redraw;
    }

    private PageResult AskToRemove(SavedAccount account)
    {
        pendingRemoval = account;
        return PageResult.Redraw;
    }

    private PageResult Confirm(SavedAccount account, ConsoleKeyInfo key)
    {
        pendingRemoval = null;
        messages.Clear();
        if (Hotkey.Is(key, 'y')) Run(report => operations.Remove(account.Id, _ => true, report), null);
        else Say(Tone.Info, $"{account.Username} was kept.");
        return PageResult.Redraw;
    }

    private PageResult Clean()
    {
        messages.Clear();
        Run(operations.CleanUp, null);
        return PageResult.Redraw;
    }

    /// <summary>The sign-in window blocks, so the page shows what to do first and runs it once that is on screen.</summary>
    private PageResult StartSignIn(SavedAccount? account)
    {
        messages.Clear();
        Say(Tone.Info, account is null
            ? "Opening Roblox sign-in. Sign in there, choose Verify account, then Save account."
            : $"Opening Roblox sign-in for {account.Username}. Sign in to that same Roblox account there, "
                + "choose Verify account, then Save account.");
        return new PageResult(true, () => Run(
            report => account is null ? operations.Add(report) : operations.SignInAgain(account.Id, report),
            account?.Id));
    }

    private PageResult StartExperience(SavedAccount account)
    {
        messages.Clear();
        Say(Tone.Info, $"Opening an experience window for {account.Username}. Enter a place ID there, then use Play on the Roblox page.");
        return new PageResult(true, () => Run(report => operations.OpenExperience(account.Id, null, report), account.Id));
    }

    private void Run(Func<Action<string>, int> action, string? keepId)
    {
        var known = listing.Accounts.Select(a => a.Id).ToHashSet();
        var reported = new List<string>();
        string? failure = null;
        var code = 1;
        try { code = action(reported.Add); }
        catch (Exception e) when (Describe(e) is { } text) { failure = text; }

        messages.Clear();
        var tone = code switch { 0 => Tone.Good, 130 => Tone.Info, _ => Tone.Warn };
        for (var index = 0; index < reported.Count; index++) Say(index == 0 ? tone : Tone.Info, reported[index]);
        if (failure is not null) Say(Tone.Bad, "Error: " + failure);
        Reload(keepId, known);
    }

    /// <summary>
    /// Re-reads the list and picks what to select: the requested account, else one that was not in the list before
    /// (a new sign-in), else whatever was selected, else the nearest row.
    /// </summary>
    private void Reload(string? keepId, ISet<string>? known)
    {
        var previous = Current?.Id;
        try
        {
            listing = operations.List();
            loadError = null;
        }
        catch (Exception e) when (Describe(e) is { } text)
        {
            listing = new AccountListing([], 0);
            loadError = text;
            scroller.Home();
        }

        var accounts = listing.Accounts.ToList();
        var index = IndexOf(accounts, keepId);
        if (index < 0 && known is not null) index = accounts.FindIndex(a => !known.Contains(a.Id));
        if (index < 0) index = IndexOf(accounts, previous);
        selected = accounts.Count == 0 ? 0 : index >= 0 ? index : Math.Min(selected, accounts.Count - 1);
    }

    private IReadOnlyList<Line> LoadFailureLines(int width, int rows)
    {
        var all = Cells.Wrap("Error: " + loadError, width).Select(part => Line.Of(part, Looks.Bad)).ToArray();
        var frame = PageFrame.Arrange(null, [Line.Of("Press R to retry loading accounts.", Looks.Strong)], rows, 1);
        bodyRows = frame.BodyRows;
        errorRows = all.Length;
        scroller.Clamp(errorRows, bodyRows);
        return frame.Compose(PageParts.Heading("ACCOUNTS", "unavailable"),
            all.Skip(scroller.Offset).Take(bodyRows));
    }

    private static int IndexOf(List<SavedAccount> accounts, string? id)
        => id is null ? -1 : accounts.FindIndex(a => a.Id == id);

    /// <summary>The words for an expected failure, or null for anything that should not be swallowed.</summary>
    private static string? Describe(Exception error) => error switch
    {
        OperationCanceledException => null,
        InvalidDataException or InvalidOperationException or ArgumentException => error.Message,
        IOException or UnauthorizedAccessException or SecurityException
            => "Account data could not be accessed. Close other account operations and check local permissions.",
        _ => null,
    };

    private void Say(Tone tone, string text) => messages.Add(new PageMessage(tone, text));

    private (Line? Header, IReadOnlyList<Line> Rows) Table(int width)
    {
        var accounts = listing.Accounts;
        var idWidth = Math.Max(Cells.Width(IdHeading), accounts.Max(a => Cells.Width(a.UserId)));
        var showDate = width >= 2 + 12 + Gap + idWidth + Gap + DateWidth;
        var room = width - 2 - Gap - idWidth - (showDate ? Gap + DateWidth : 0);
        var nameWidth = Math.Clamp(accounts.Max(a => Cells.Width(a.Username)), 8, Math.Clamp(room, 8, 28));

        string Text(string marker, string name, string id, string date)
            => marker + Cells.Fit(name, nameWidth) + new string(' ', Gap) + Cells.Fit(id, idWidth)
                + (showDate ? new string(' ', Gap) + date : "");

        var header = Line.Of(Text("  ", "Account", IdHeading, "Last verified (UTC)"), Looks.Dim);
        var rows = accounts.Select((account, index) =>
        {
            var date = account.LastVerifiedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            var chosen = index == selected;
            var text = Text(chosen ? "► " : "  ", account.Username, account.UserId, date);
            // Padding the row to the full width makes the highlight a bar rather than a patch of text.
            return Line.Of(Cells.Fit(text, width), chosen ? Looks.Selected : Style.Plain);
        }).ToArray();
        return (header, rows);
    }

    private static IReadOnlyList<Line> EmptyState(int width)
    {
        var lines = new List<Line> { Line.Of("No saved accounts yet.", Looks.Strong) };
        lines.AddRange(Cells.Wrap("Press A to sign in to Roblox in a separate browser window and add one. "
            + "Your password is typed only into the Roblox page, never into this terminal.", width)
            .Select(part => Line.Of(part, Looks.Dim)));
        return lines;
    }

    /// <summary>
    /// What is pinned under the list, least important first: the selected account's details, the cleanup notice, then
    /// results. When rows run short the arranger drops from the front, so results are the last thing to go.
    /// </summary>
    private List<Line> BottomLines(int width)
    {
        var lines = new List<Line>();
        if (Current is { } account)
        {
            lines.Add(Line.Of("Local account ID  ", Looks.Dim).Then(account.Id));
            lines.Add(Line.Of("Login status      ", Looks.Dim).Then("not checked"));
        }
        if (listing.PendingCleanup > 0)
            lines.Add(Line.Of($"{listing.PendingCleanup} browser profile(s) awaiting cleanup. Press C to retry.", Looks.Warn));
        lines.AddRange(PageParts.MessageLines(messages, width));
        return lines;
    }

    /// <summary>The removal question. Its instruction line is pinned, so it shows however short the window is.</summary>
    private static IReadOnlyList<Line> ConfirmationLines(SavedAccount account, int width, int rows)
    {
        // Both identity rows must survive at the minimum height; long names must not push the ID offscreen.
        Line[] question =
        [
            Line.Of("Account: ", Looks.Dim).Then(Cells.Fit(account.Username, Math.Max(1, width - 9)).TrimEnd(), Looks.Warn),
            Line.Of("Roblox user ID: ", Looks.Dim).Then(account.UserId, Looks.Warn),
        ];
        var consequence = Cells.Wrap("This removes its local browser session. Other Roblox sessions are not revoked.", width)
            .Select(part => Line.Of(part, Looks.Dim));
        var frame = PageFrame.Arrange(null, [Line.Of("Press Y to remove it. Any other key keeps it.", Looks.Strong)], rows, 2);
        return frame.Compose(PageParts.Heading("ACCOUNTS", "remove account"), question.Concat(consequence));
    }
}
