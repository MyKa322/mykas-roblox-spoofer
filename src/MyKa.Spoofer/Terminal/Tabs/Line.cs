using Spectre.Console;
using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>A run of text in one style.</summary>
internal readonly record struct Span(string Text, Style Style);

/// <summary>
/// One row of styled text. Text is cleaned of control characters on the way in and is never read as markup, so
/// account names and server messages cannot inject terminal sequences or formatting.
/// </summary>
internal sealed class Line
{
    private readonly Span[] spans;

    private Line(Span[] spans) => this.spans = spans;

    public static Line Empty { get; } = new([]);

    public IReadOnlyList<Span> Spans => spans;

    public string Text => string.Concat(spans.Select(span => span.Text));

    public int Width => spans.Sum(span => Cells.Width(span.Text));

    public static Line Of(string text, Style? style = null) => Empty.Then(text, style);

    public Line Then(string text, Style? style = null)
    {
        var safe = TerminalUi.SafeText(text);
        return safe.Length == 0 ? this : new Line([.. spans, new Span(safe, style ?? Style.Plain)]);
    }

    public Line Indent(int columns)
        => columns <= 0 ? this : new Line([new Span(new string(' ', columns), Style.Plain), .. spans]);

    public Line Centered(int width) => Indent((width - Width) / 2);

    /// <summary>The line cut to <paramref name="width"/> cells, ending in an ellipsis when something was lost.</summary>
    public IReadOnlyList<Segment> ToSegments(int width)
    {
        if (width <= 0) return [];
        if (Width <= width) return spans.Select(span => new Segment(span.Text, span.Style)).ToArray();

        var kept = new List<Segment>();
        var room = width - 1;
        foreach (var span in spans)
        {
            var head = Cells.Cut(span.Text, room);
            if (head.Length > 0) kept.Add(new Segment(head, span.Style));
            room -= Cells.Width(head);
            if (head.Length < span.Text.Length) break;
        }
        kept.Add(new Segment(Cells.Ellipsis, kept.Count > 0 ? kept[^1].Style : Style.Plain));
        return kept;
    }
}
