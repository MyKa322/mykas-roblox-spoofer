using MyKa.Spoofer.Diagnostics;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>Environment observations, the login browser and Roblox discovery, refreshed on request.</summary>
internal sealed class DiagnosticsPage(Func<DiagnosticReport> read, Func<string?> webView) : ITabPage
{
    private readonly Scroller scroller = new();
    private DiagnosticReport? report;
    private string? webViewVersion;
    private int total;
    private int bodyRows = 6;

    public string Title => "Diagnostics";

    public bool IsModal => false;

    public string Hints => "R check again   ↑ ↓ PgUp PgDn scroll";

    public void Activate()
    {
        if (report is null) Refresh();
    }

    public PageResult Handle(ConsoleKeyInfo key)
    {
        if (Hotkey.Is(key, 'r'))
        {
            Refresh();
            return PageResult.Redraw;
        }
        return scroller.Handle(key, total, bodyRows) ? PageResult.Redraw : PageResult.None;
    }

    public IReadOnlyList<Line> Render(int width, int rows)
    {
        var all = report is null
            ? []
            : PageParts.Report(Reports.Doctor(report, webViewVersion), width).ToList();
        var frame = PageFrame.Arrange(null, [], rows, minBody: 1);
        bodyRows = frame.BodyRows;
        total = all.Count;
        scroller.Clamp(total, bodyRows);
        var shown = all.Skip(scroller.Offset).Take(bodyRows).ToList();

        var detail = PageParts.Join("read only", PageParts.Position(scroller.Offset, shown.Count, total));
        return frame.Compose(PageParts.Heading("DIAGNOSTICS", detail), shown);
    }

    private void Refresh()
    {
        report = read();
        webViewVersion = webView();
        scroller.Home();
    }
}
