namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>A scrollable shortcut guide shown over the current page without changing its state.</summary>
internal sealed class HelpPage : ITabPage
{
    private readonly Scroller scroller = new();
    private int total;
    private int bodyRows = 1;

    public string Title => "Keyboard help";
    public bool IsModal => true;
    public string Hints => "↑ ↓ scroll   PgUp PgDn page   Home End";

    public void Activate() => scroller.Home();

    public PageResult Handle(ConsoleKeyInfo key)
        => scroller.Handle(key, total, bodyRows) ? PageResult.Redraw : PageResult.None;

    public IReadOnlyList<Line> Render(int width, int rows)
    {
        var all = new List<Line>();
        foreach (var (title, text) in Sections)
        {
            if (all.Count > 0) all.Add(Line.Empty);
            all.Add(Line.Of(title, Looks.Strong));
            all.AddRange(Cells.Wrap(text, width).Select(part => Line.Of(part)));
        }
        var frame = PageFrame.Arrange(null, [], rows, minBody: 1);
        bodyRows = frame.BodyRows;
        total = all.Count;
        scroller.Clamp(total, bodyRows);
        var shown = all.Skip(scroller.Offset).Take(bodyRows).ToArray();
        return frame.Compose(PageParts.Heading("KEYBOARD HELP",
            PageParts.Position(scroller.Offset, shown.Length, total)), shown);
    }

    private static readonly (string Title, string Text)[] Sections =
    [
        ("Navigation", "Left / Right or Tab / Shift+Tab switch tabs. 1–4 jump directly. Esc or Q quits. Ctrl+C cancels."),
        ("Accounts", "Up / Down select an account. Home / End go to the first or last; PgUp / PgDn move a page. "
            + "A adds an account. P opens an experience window for a place ID. Enter or S signs in again. D or Delete asks to remove. C retries profile cleanup. R reloads the list."),
        ("Confirmation", "Y confirms removal. Every other key keeps the account. Only the local browser session is removed."),
        ("Diagnostics and adapters", "R refreshes the report. Up / Down, PgUp / PgDn and Home / End scroll through it. "
            + "S in Adapters opens system identity snapshots, comparison, backup and reviewed Windows operations."),
        ("Identity tools", "4 opens the Identity tab. Enter or O opens the system window with Before/After capture, Compare, backup, restore and reviewed Windows operations. Up / Down scroll through capabilities and limits."),
        ("Motion and help", "Space pauses or resumes the logo. --still starts with motion off. F1 or ? opens this guide; "
            + "F1, ? or Esc returns to the same page. The logo stays still while help is open."),
        ("Plain output", "Start with --plain for a numbered menu without colour or animation. "
            + "This also avoids full-screen redrawing when using a screen reader."),
    ];
}
