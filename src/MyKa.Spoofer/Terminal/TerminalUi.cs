using System.Diagnostics;
using System.Globalization;
using MyKa.Spoofer.Accounts;
using MyKa.Spoofer.Branding;
using MyKa.Spoofer.Diagnostics;
using MyKa.Spoofer.Identity;
using MyKa.Spoofer.Terminal.Tabs;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal;

public sealed class TerminalUi
{
    public const string Title = LogoText.Title;
    public const string Version = "0.3.2";
    private const int FieldWidth = 22;

    private readonly IAnsiConsole console;
    private readonly bool plain;
    private readonly bool still;
    private readonly IKeySource keys;
    private readonly CancellationToken cancellation;
    public bool Interactive { get; }

    public TerminalUi(bool requestedPlain, CancellationToken cancellation = default, bool requestedStill = false,
        IKeySource? keys = null)
    {
        this.cancellation = cancellation;
        this.keys = keys ?? new ConsoleKeys();
        still = requestedStill;
        Interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
        plain = requestedPlain || !Interactive || Environment.GetEnvironmentVariable("NO_COLOR") is not null
            || Environment.GetEnvironmentVariable("TERM") == "dumb";
        console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = plain ? AnsiSupport.No : AnsiSupport.Detect,
            ColorSystem = plain ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
            Interactive = Interactive ? InteractionSupport.Yes : InteractionSupport.No
        });
    }

    public static string SafeText(string value) => new(value.Where(c =>
        !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray());

    public void Help()
    {
        console.WriteLine(Title + " " + Version);
        console.WriteLine("Usage: myka [--plain] [--still] [command]");
        console.WriteLine();
        console.WriteLine("  (no command)      Open the interactive menu");
        console.WriteLine("  accounts list     List saved, verified account identities");
        console.WriteLine("  accounts add      Sign in through an isolated Roblox browser");
        console.WriteLine("  accounts login --account ID   Sign in again without replacing the wrong account");
        console.WriteLine("  accounts play --account ID --place PLACE_ID   Open a saved account's experience page");
        console.WriteLine("  accounts remove --account ID  Remove a local account and browser profile");
        console.WriteLine("  accounts cleanup  Retry unfinished browser-profile cleanup");
        console.WriteLine("  doctor            Inspect environment, runtimes and Roblox installation");
        console.WriteLine("  identity inspect  Read network adapters and their current addresses");
        console.WriteLine("  identity manage   Open identity snapshots, comparison, backup and reviewed Windows operations");
        console.WriteLine("  identity snapshot --output FILE   Export current identifiers locally (read only)");
        console.WriteLine("  identity compare --before FILE --after FILE   Compare saved snapshots");
        console.WriteLine("  logo              Show the logo (it spins once in a colour terminal)");
        console.WriteLine("  --plain           ASCII only: no colour, animation, or live menu");
        console.WriteLine("  --still           Start with the logo still instead of turning");
        console.WriteLine("  --help            Show this help");
        console.WriteLine();
        console.WriteLine("Tabbed screen: F1 shortcuts, Space pause/resume, R refresh.");
        console.WriteLine("Identity tools: choose tab 4, then press Enter or O. The plain menu offers System identity.");
        console.WriteLine("Experience pages can request a desktop handoff. Player launch and joined account remain unverified.");
        console.WriteLine("Stored GUIDs and supported MAC overrides can be changed with a recovery backup; firmware identifiers remain inspect only.");
        console.WriteLine("Concurrent environment isolation and public IP routing are not implemented.");
    }

    /// <summary>
    /// Shows the logo. In a colour terminal it spins once and settles; any key skips the spin. Plain and redirected
    /// output gets the ASCII version, and a terminal too small for any logo gets just the title.
    /// </summary>
    public void Logo()
    {
        if (plain)
        {
            foreach (var line in LogoText.PlainDocument()) console.Write(new Text(line + Environment.NewLine));
            return;
        }

        var layout = LogoLayout.Choose(console.Profile.Width, console.Profile.Height - 4);
        if (layout is null)
        {
            console.Write(new Text(Title + Environment.NewLine));
            return;
        }

        if (still)
        {
            console.Write(Painted(layout, LogoMotion.Still));
            console.WriteLine();
        }
        else PlayIntro(layout);

        // A live display leaves the cursor on a fresh line, so one more line break gives a single blank row.
        console.WriteLine();
        if (layout == LogoLayout.Compact)
        {
            console.Write(Centered(new Markup($"[bold cyan1]{LogoText.Subtitle}[/]")));
            console.WriteLine();
        }
        console.Write(Centered(new Markup($"[bold]{Markup.Escape(Title)}[/] [grey]v{Version}[/]")));
        console.WriteLine();
    }

    /// <summary>
    /// The interactive menu. A colour terminal that can do full-screen drawing gets the tabbed screen; any other
    /// gets the simple numbered (or arrow-key) menu, which prints one report after another.
    /// </summary>
    public int Menu()
    {
        if (!plain && SpectreSurface.Supported(console)) return RunTabs();

        console.WriteLine(Title);
        console.WriteLine($"Version {Version[..3]} | Accounts and diagnostics");
        while (true)
        {
            console.WriteLine();
            cancellation.ThrowIfCancellationRequested();
            var choice = Choose("Choose an action", ["Accounts", "Diagnostics", "Network adapters", "System identity", "Show ASCII logo", "Exit"]);
            if (choice is null or "Exit") return 0;
            switch (choice)
            {
                case "Accounts": new AccountCommands(this, cancellation).Menu(); break;
                case "Diagnostics": Doctor(SystemDiagnostics.Read()); break;
                case "Network adapters": Adapters(SystemDiagnostics.ReadAdapters()); break;
                case "System identity": IdentityWindow.Run(cancellation); break;
                case "Show ASCII logo": Logo(); break;
            }
        }
    }

    private int RunTabs()
    {
        var clock = Stopwatch.StartNew();
        ITabPage[] pages =
        [
            new AccountsPage(new AccountOperations(cancellation)),
            new DiagnosticsPage(SystemDiagnostics.Read, BrowserLogin.TryRuntimeVersion),
            new AdaptersPage(SystemDiagnostics.ReadAdapters, () => IdentityWindow.Run(cancellation)),
            new IdentityPage(() => IdentityWindow.Run(cancellation)),
        ];
        var status = TabsView.StatusLine(BrowserLogin.TryRuntimeVersion());
        return new TabsApp(pages, new SpectreSurface(console), keys, status, animate: !still,
            () => clock.Elapsed.TotalSeconds, Thread.Sleep, cancellation).Run();
    }

    public string? Choose(string title, string[] choices)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!plain)
            return console.PromptAsync(new SelectionPrompt<string>()
                .Title(Markup.Escape(SafeText(title)))
                .UseConverter(value => Markup.Escape(SafeText(value))).AddChoices(choices), cancellation).GetAwaiter().GetResult();
        Message(title);
        for (var i = 0; i < choices.Length; i++) Message($"{i + 1}. {choices[i]}");
        while (true)
        {
            console.Write($"Choice [1-{choices.Length}, Enter to go back]: ");
            var value = Task.Run(Console.ReadLine).WaitAsync(cancellation).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (int.TryParse(value, out var number) && number >= 1 && number <= choices.Length) return choices[number - 1];
            console.WriteLine($"Enter a number from 1 to {choices.Length}.");
        }
    }

    public void Accounts(AccountStore store)
    {
        Heading("SAVED ACCOUNTS");
        if (store.Accounts.Count == 0) console.WriteLine("No saved accounts. Choose Add account to sign in.");
        foreach (var account in store.Accounts)
        {
            console.WriteLine();
            Field("Account", account.Username);
            Field("Roblox user ID", account.UserId);
            Field("Local account ID", account.Id);
            Field("Last verified (UTC)", account.LastVerifiedUtc.UtcDateTime.ToString("u"));
            console.WriteLine("Current login status: not checked");
        }
        if (store.PendingCleanupCount > 0)
            Message($"Browser profiles awaiting cleanup: {store.PendingCleanupCount}. Use accounts cleanup.");
    }

    public void Message(string text) => console.Write(new Text(SafeText(text) + Environment.NewLine));

    public int Doctor(DiagnosticReport report)
    {
        Heading("DIAGNOSTICS");
        foreach (var item in Reports.Doctor(report, BrowserLogin.TryRuntimeVersion()))
        {
            if (item is ReportField field) Field(field.Label, field.Value);
            else if (item is ReportNote note) console.WriteLine(note.Text);
        }
        return report.Installation.Problems.Count == 0 && report.Environment.Complete ? 0 : 1;
    }

    public int Adapters(AdapterReport report)
    {
        Heading("NETWORK ADAPTERS | Read only");
        if (report.Problem is not null) { Error(report.Problem); return 1; }
        if (report.Adapters.Count == 0) console.WriteLine("No adapters were reported by Windows.");
        foreach (var adapter in report.Adapters)
        {
            console.WriteLine();
            Field("Name", adapter.Name);
            Field("Type / status", $"{adapter.Type} / {adapter.State}");
            Field("Current address", adapter.Address);
        }
        console.WriteLine();
        foreach (var note in Reports.AdapterNotes) console.WriteLine(note);
        return 0;
    }

    public void Error(string message) => console.Write(new Text("Error: " + SafeText(message) + Environment.NewLine));

    /// <summary>Section title: a labelled rule in colour terminals, an upper-case line in plain output.</summary>
    private void Heading(string title)
    {
        console.WriteLine();
        if (plain) console.WriteLine(title);
        else console.Write(new Rule($"[bold cyan1]{Markup.Escape(title)}[/]") { Justification = Justify.Left, Style = Style.Parse("grey") });
    }

    /// <summary>One labelled value. Colour terminals align the values in a column; plain output keeps "Label: value".</summary>
    private void Field(string label, string value)
    {
        if (plain) console.Write(new Text($"{label}: {SafeText(value)}{Environment.NewLine}"));
        else console.MarkupLine($"[grey]{Markup.Escape(label.PadRight(FieldWidth))}[/] {Markup.Escape(SafeText(value))}");
    }

    private void PlayIntro(LogoLayout layout)
    {
        while (keys.Available) keys.Read();
        var clock = Stopwatch.StartNew();
        console.Live(Painted(layout, LogoMotion.Intro(0))).AutoClear(false).Start(live =>
        {
            while (clock.Elapsed.TotalSeconds < LogoMotion.IntroSeconds && !keys.Available)
            {
                cancellation.ThrowIfCancellationRequested();
                live.UpdateTarget(Painted(layout, LogoMotion.Intro(clock.Elapsed.TotalSeconds)));
                live.Refresh();
                Thread.Sleep(LogoMotion.FrameMilliseconds);
            }
            live.UpdateTarget(Painted(layout, LogoMotion.Still));
            live.Refresh();
        });
        while (keys.Available) keys.Read();
    }

    private static IRenderable Painted(LogoLayout layout, LogoPose pose)
        => Centered(new LogoRenderable(new LogoRenderer(layout).Render(pose)));

    private static IRenderable Centered(IRenderable content)
        => new Align(content, Spectre.Console.HorizontalAlignment.Center);
}
