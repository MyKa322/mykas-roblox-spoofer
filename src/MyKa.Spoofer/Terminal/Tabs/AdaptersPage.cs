using MyKa.Spoofer.Diagnostics;
using Spectre.Console;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>The network adapters Windows reports. Reading only: nothing here changes an address.</summary>
internal sealed class AdaptersPage(Func<AdapterReport> read, Action? manageIdentity = null) : ITabPage
{
    private const int NarrowWidth = 60;
    private const int Gap = 2;

    private readonly Scroller scroller = new();
    private AdapterReport? report;
    private int total;
    private int bodyRows = 6;

    public string Title => "Adapters";

    public bool IsModal => false;

    public string Hints => manageIdentity is null ? "R read again   ↑ ↓ PgUp PgDn scroll" : "S system identity   R read   ↑ ↓ scroll";

    public void Activate()
    {
        if (report is null) Refresh();
    }

    public PageResult Handle(ConsoleKeyInfo key)
    {
        if (Hotkey.Is(key, 's') && manageIdentity is not null)
            return new PageResult(true, () => { manageIdentity(); Refresh(); });
        if (Hotkey.Is(key, 'r'))
        {
            Refresh();
            return PageResult.Redraw;
        }
        return scroller.Handle(key, total, bodyRows) ? PageResult.Redraw : PageResult.None;
    }

    public IReadOnlyList<Line> Render(int width, int rows)
    {
        var notes = Reports.AdapterNotes.SelectMany(note => Cells.Wrap(note, width))
            .Select(part => Line.Of(part, Looks.Dim)).ToList();
        var (header, body) = Table(width);
        var frame = PageFrame.Arrange(header, notes, rows, minBody: 1);
        bodyRows = frame.BodyRows;
        total = body.Count;
        scroller.Clamp(total, bodyRows);
        var shown = body.Skip(scroller.Offset).Take(bodyRows).ToList();

        var detail = PageParts.Join("read only", PageParts.Position(scroller.Offset, shown.Count, total));
        return frame.Compose(PageParts.Heading("NETWORK ADAPTERS", detail), shown);
    }

    private (Line? Header, IReadOnlyList<Line> Rows) Table(int width)
    {
        if (report is null) return (null, []);
        if (report.Problem is not null) return (null, [Line.Of("Error: " + report.Problem, Looks.Bad)]);
        if (report.Adapters.Count == 0) return (null, [Line.Of("No adapters were reported by Windows.", Looks.Dim)]);

        // Wide enough for their own column headings, so a list of "Not reported" still reads as a table.
        var addressWidth = Math.Clamp(report.Adapters.Max(a => Cells.Width(a.Address)), 15, 17);
        var narrow = width < NarrowWidth;
        var statusWidth = narrow ? 0 : Math.Clamp(report.Adapters.Max(a => Cells.Width(StatusOf(a))), 13, 24);
        // The name column fits its longest name, so the table stays together instead of spreading across the window.
        var room = Math.Max(8, width - addressWidth - Gap - (narrow ? 0 : statusWidth + Gap));
        var nameWidth = Math.Clamp(report.Adapters.Max(a => Cells.Width(a.Name)), 8, room);

        Line Row(string name, string status, string address, Style? style = null)
        {
            var text = Cells.Fit(name, nameWidth) + new string(' ', Gap)
                + (narrow ? "" : Cells.Fit(status, statusWidth) + new string(' ', Gap))
                + Cells.Fit(address, addressWidth);
            return Line.Of(text, style);
        }

        var header = Row("Name", "Type / status", "Current address", Looks.Dim);
        return (header, report.Adapters.Select(a => Row(a.Name, StatusOf(a), a.Address)).ToArray());
    }

    private static string StatusOf(AdapterInfo adapter) => $"{adapter.Type} / {adapter.State}";

    private void Refresh()
    {
        report = read();
        scroller.Home();
    }
}
