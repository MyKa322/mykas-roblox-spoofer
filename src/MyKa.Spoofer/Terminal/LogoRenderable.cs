using System.Text;
using MyKa.Spoofer.Branding;
using Spectre.Console;
using Spectre.Console.Rendering;
using SpectreColor = Spectre.Console.Color;

namespace MyKa.Spoofer.Terminal;

/// <summary>Draws a logo frame as styled text, one run of identically coloured cells per segment.</summary>
public sealed class LogoRenderable : IRenderable
{
    private readonly LogoCell[,] cells;

    public LogoRenderable(LogoFrame frame) => cells = new LogoPainter(ascii: false).Paint(frame);

    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(cells.GetLength(1), maxWidth);
        return new Measurement(width, width);
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var columns = Math.Min(cells.GetLength(1), maxWidth);
        for (var row = 0; row < cells.GetLength(0); row++)
        {
            var run = new StringBuilder();
            var style = Style.Plain;
            for (var column = 0; column < columns; column++)
            {
                var cell = cells[row, column];
                var next = StyleOf(cell);
                if (run.Length > 0 && !next.Equals(style))
                {
                    yield return new Segment(run.ToString(), style);
                    run.Clear();
                }
                style = next;
                run.Append(cell.Glyph);
            }
            if (run.Length > 0) yield return new Segment(run.ToString(), style);
            yield return Segment.LineBreak;
        }
    }

    private static Style StyleOf(LogoCell cell)
    {
        SpectreColor? foreground = cell.Foreground is { } f ? new SpectreColor(f.R, f.G, f.B) : null;
        SpectreColor? background = cell.Background is { } b ? new SpectreColor(b.R, b.G, b.B) : null;
        return new Style(foreground, background);
    }
}
