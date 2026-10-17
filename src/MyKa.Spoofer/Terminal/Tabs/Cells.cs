using System.Text;
using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>Text measured in terminal cells, so wide characters line up the way the terminal draws them.</summary>
internal static class Cells
{
    public const string Ellipsis = "…";

    /// <summary>
    /// The width the text will have on screen. It is measured after control characters are removed, because that is
    /// how it is drawn, and because Spectre counts a control character as minus one cell.
    /// </summary>
    public static int Width(string text)
    {
        if (text.Length == 0) return 0;
        var shown = IsClean(text) ? text : TerminalUi.SafeText(text);
        return shown.Length == 0 ? 0 : Math.Max(0, new Segment(shown).CellCount());
    }

    /// <summary>The longest start of <paramref name="text"/> that fits in <paramref name="width"/> cells.</summary>
    public static string Cut(string text, int width)
    {
        var kept = new StringBuilder();
        var used = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var piece = rune.ToString();
            var cells = Width(piece);
            if (used + cells > width) break;
            kept.Append(piece);
            used += cells;
        }
        return kept.ToString();
    }

    /// <summary>Exactly <paramref name="width"/> cells: padded with spaces, or cut short with an ellipsis.</summary>
    public static string Fit(string text, int width)
    {
        if (width <= 0) return "";
        var safe = TerminalUi.SafeText(text);
        var cells = Width(safe);
        if (cells <= width) return safe + new string(' ', width - cells);
        var kept = Cut(safe, width - 1);
        return kept + Ellipsis + new string(' ', width - 1 - Width(kept));
    }

    /// <summary>Greedy word wrap to <paramref name="width"/> cells. A word wider than a whole line is split.</summary>
    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        if (width <= 0) return [];
        var lines = new List<string>();
        var line = "";
        foreach (var word in TerminalUi.SafeText(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var piece in Pieces(word, width))
            {
                if (line.Length == 0) line = piece;
                else if (Width(line) + 1 + Width(piece) <= width) line += " " + piece;
                else
                {
                    lines.Add(line);
                    line = piece;
                }
            }
        }
        if (line.Length > 0) lines.Add(line);
        return lines.Count == 0 ? [""] : lines;
    }

    private static bool IsClean(string text)
        => text.All(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format);

    private static IEnumerable<string> Pieces(string word, int width)
    {
        while (Width(word) > width)
        {
            var head = Cut(word, width);
            if (head.Length == 0) yield break;
            yield return head;
            word = word[head.Length..];
        }
        if (word.Length > 0) yield return word;
    }
}
