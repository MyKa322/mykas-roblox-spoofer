using MyKa.Spoofer.Branding;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>Builds one frame of the tabbed screen: logo, title, status, tab strip, the current page, and key hints.</summary>
internal static class TabsView
{
    public static Line Title() => Line.Of(TerminalUi.Title, Looks.Strong).Then(" v" + TerminalUi.Version, Looks.Dim);

    /// <summary>The status line. It states the login browser's condition in words, not only in colour.</summary>
    public static Line StatusLine(string? webViewVersion)
    {
        var line = Line.Of("Login browser: ", Looks.Dim);
        return webViewVersion is null
            ? line.Then("WebView2 unavailable", Looks.Warn)
            : line.Then("WebView2 ready", Looks.Good);
    }

    public static IRenderable Build(TabsModel model, TabsLayout layout, LogoPose pose, Line status)
    {
        if (layout.TooSmall) return new LinesRenderable(TooSmall(layout));

        var lines = new List<Line>();
        if (layout.HasSubtitle) lines.Add(Line.Of(Branding.LogoText.Subtitle, Looks.Accent).Centered(layout.Width));
        lines.Add(Title().Centered(layout.Width));
        lines.Add(status.Centered(layout.Width));
        lines.Add(Line.Empty);
        lines.AddRange(TabStrip(model, layout.Width));
        lines.AddRange(Content(model.Current, layout));
        lines.AddRange(Footer(model, layout.Width));

        var body = new LinesRenderable(lines);
        if (layout.Logo is null) return body;
        var logo = new LogoRenderable(new LogoRenderer(layout.Logo).Render(pose));
        return new Rows(new Align(logo, Spectre.Console.HorizontalAlignment.Center), body);
    }

    /// <summary>The tab names and, under them, a rule that is heavier beneath the tab in use.</summary>
    public static IReadOnlyList<Line> TabStrip(TabsModel model, int width)
    {
        var bar = Line.Empty.Indent(TabsLayout.Margin);
        var column = TabsLayout.Margin;
        var chosenStart = 0;
        var chosenLength = 0;
        var labels = model.Pages.Select((page, index) => $" {index + 1} {page.Title} ").ToArray();
        if (TabsLayout.Margin + labels.Sum(Cells.Width) + labels.Length - 1 > width - TabsLayout.Margin)
            labels = model.Pages.Select((page, index) => $"{index + 1} {page.Title}").ToArray();
        for (var index = 0; index < model.Pages.Count; index++)
        {
            var label = labels[index];
            var chosen = index == model.Selected;
            bar = bar.Then(label, chosen ? Looks.Selected : Looks.Dim);
            if (index < model.Pages.Count - 1) bar = bar.Then(" ");
            if (chosen)
            {
                chosenStart = column;
                chosenLength = Cells.Width(label);
            }
            column += Cells.Width(label) + 1;
        }

        var start = Math.Min(chosenStart, Math.Max(0, width));
        var length = Math.Min(chosenLength, Math.Max(0, width) - start);
        var rule = Line.Of(new string('─', start), Looks.Dim)
            .Then(new string('━', length), Looks.Accent)
            .Then(new string('─', Math.Max(0, width) - start - length), Looks.Dim);
        return [bar, rule];
    }

    private static IReadOnlyList<Line> Content(ITabPage page, TabsLayout layout)
    {
        var lines = page.Render(layout.ContentWidth, layout.ContentRows)
            .Take(layout.ContentRows)
            .Select(line => line.Indent(TabsLayout.Margin))
            .ToList();
        while (lines.Count < layout.ContentRows) lines.Add(Line.Empty);
        return lines;
    }

    private static IReadOnlyList<Line> Footer(TabsModel model, int width)
    {
        var room = width - 2 * TabsLayout.Margin;
        var motion = model.Animate ? "pause" : "resume";
        var navigation = model.HelpOpen
            ? $"Esc back   F1 close   Space {motion}"
            : model.Current.IsModal ? ""
            : $"← → / Tab switch tab   1-{model.Pages.Count} jump   F1 help   Space {motion}   Esc quit";
        if (Cells.Width(navigation) > room)
            navigation = $"1-{model.Pages.Count} tabs  F1 help  Space {motion}  Esc quit";
        return
        [
            Line.Of(model.Current.HintsFor(room), Looks.Strong).Indent(TabsLayout.Margin),
            Line.Of(navigation, Looks.Dim).Indent(TabsLayout.Margin),
        ];
    }

    private static IReadOnlyList<Line> TooSmall(TabsLayout layout) =>
    [
        Line.Of(TerminalUi.Title, Looks.Strong),
        Line.Of($"Window too small (need {TabsLayout.MinWidth} x {TabsLayout.MinHeight}).", Looks.Warn),
        Line.Of("Enlarge it, or press Esc to quit.", Looks.Dim),
    ];
}
