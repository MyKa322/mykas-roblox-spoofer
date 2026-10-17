using Spectre.Console;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>How serious a message on a page is. It sets the colour; the words carry the meaning.</summary>
internal enum Tone
{
    Info,
    Good,
    Warn,
    Bad,
}

internal sealed record PageMessage(Tone Tone, string Text);

/// <summary>Pieces every page draws the same way.</summary>
internal static class PageParts
{
    private const int LabelWidth = 18;

    public static Line Heading(string title, string detail)
        => Line.Of(title, Looks.Accent).Then(detail.Length == 0 ? "" : "   " + detail, Looks.Dim);

    /// <summary>"3-10 of 24" when only part of a list is showing, otherwise nothing.</summary>
    public static string Position(int offset, int shown, int total)
        => total > shown && shown > 0 ? $"{offset + 1}–{offset + shown} of {total}" : "";

    public static string Join(params string[] parts) => string.Join("   ", parts.Where(part => part.Length > 0));

    public static Style StyleOf(Tone tone) => tone switch
    {
        Tone.Good => Looks.Good,
        Tone.Warn => Looks.Warn,
        Tone.Bad => Looks.Bad,
        _ => Looks.Info,
    };

    public static IEnumerable<Line> MessageLines(IEnumerable<PageMessage> messages, int width)
        => messages.SelectMany(message => Cells.Wrap(message.Text, width).Select(part => Line.Of(part, StyleOf(message.Tone))));

    /// <summary>Report items as rows: a dim label column beside wrapped values, and wrapped dim notes.</summary>
    public static IEnumerable<Line> Report(IEnumerable<ReportItem> items, int width)
    {
        var valueWidth = Math.Max(10, width - LabelWidth - 1);
        foreach (var item in items)
        {
            switch (item)
            {
                case ReportField field:
                    var parts = Cells.Wrap(field.Value, valueWidth);
                    for (var index = 0; index < parts.Count; index++)
                        yield return Line.Of(Cells.Fit(index == 0 ? field.Label : "", LabelWidth) + " ", Looks.Dim).Then(parts[index]);
                    break;
                case ReportNote note:
                    foreach (var part in Cells.Wrap(note.Text, width)) yield return Line.Of(part, Looks.Dim);
                    break;
            }
        }
    }
}
