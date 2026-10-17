namespace MyKa.Spoofer.Branding;

/// <summary>One terminal cell: a symbol plus optional colours. Cells without colours use the terminal's own.</summary>
public readonly record struct LogoCell(char Glyph, Rgb? Foreground = null, Rgb? Background = null)
{
    public static LogoCell Blank { get; } = new(' ');
}

/// <summary>
/// Turns a frame into cells. The rich set draws faces with different block symbols and combines two samples per
/// cell with half blocks; the ASCII set keeps to printable ASCII for plain output.
/// </summary>
public sealed class LogoPainter
{
    private const char FullBlock = '█';
    private const char DarkShade = '▓';
    private const char MediumShade = '▒';
    private const char LightShade = '░';
    private const char UpperHalf = '▀';
    private const char LowerHalf = '▄';

    private readonly bool ascii;

    public LogoPainter(bool ascii) => this.ascii = ascii;

    /// <summary>Returns cells indexed [row, column].</summary>
    public LogoCell[,] Paint(LogoFrame frame)
    {
        var cells = new LogoCell[frame.Rows, frame.Columns];
        var stacked = frame.SamplesPerCell == 2;
        for (var row = 0; row < frame.Rows; row++)
            for (var column = 0; column < frame.Columns; column++)
            {
                var upper = frame[column, row * frame.SamplesPerCell];
                // With one sample per cell the same sample stands for both halves.
                var lower = stacked ? frame[column, row * 2 + 1] : upper;
                cells[row, column] = ascii ? Ascii(upper, lower) : Rich(upper, lower);
            }
        return cells;
    }

    /// <summary>Joins painted cells into text lines with trailing blanks removed. Only meaningful for the ASCII set.</summary>
    public static string[] ToLines(LogoCell[,] cells)
    {
        var lines = new string[cells.GetLength(0)];
        for (var row = 0; row < lines.Length; row++)
        {
            var chars = new char[cells.GetLength(1)];
            for (var column = 0; column < chars.Length; column++) chars[column] = cells[row, column].Glyph;
            lines[row] = new string(chars).TrimEnd();
        }
        return lines;
    }

    private static LogoCell Ascii(Sample upper, Sample lower)
    {
        if (upper.IsSolid || lower.IsSolid) return new LogoCell(AsciiGlyph(Dominant(upper, lower).Kind));
        return upper.Kind == SurfaceKind.Shadow || lower.Kind == SurfaceKind.Shadow ? new LogoCell('.') : LogoCell.Blank;
    }

    private static LogoCell Rich(Sample upper, Sample lower)
    {
        if (!upper.IsSolid && !lower.IsSolid)
            return upper.Kind == SurfaceKind.Shadow || lower.Kind == SurfaceKind.Shadow
                ? new LogoCell(LightShade, LogoPalette.Shadow)
                : LogoCell.Blank;

        if (upper.IsSolid && lower.IsSolid)
            return upper.Kind == lower.Kind
                ? Whole(upper, lower)
                : new LogoCell(UpperHalf, LogoPalette.Of(upper), LogoPalette.Of(lower));

        var solid = upper.IsSolid ? upper : lower;
        var other = upper.IsSolid ? lower : upper;
        Rgb? behind = other.Kind == SurfaceKind.Shadow ? LogoPalette.Shadow : null;
        return new LogoCell(upper.IsSolid ? UpperHalf : LowerHalf, LogoPalette.Of(solid), behind);
    }

    /// <summary>A cell whose two samples are the same kind of surface.</summary>
    private static LogoCell Whole(Sample upper, Sample lower)
    {
        var top = LogoPalette.Of(upper);
        var bottom = LogoPalette.Of(lower);
        var glyph = RichGlyph(upper.Kind);
        if (glyph == FullBlock) return top == bottom ? new LogoCell(FullBlock, top) : new LogoCell(UpperHalf, top, bottom);

        var average = LogoPalette.Mix(top, bottom, 0.5);
        return new LogoCell(glyph, average, LogoPalette.Scale(average, 0.45));
    }

    private static char RichGlyph(SurfaceKind kind) => kind switch
    {
        SurfaceKind.Front or SurfaceKind.FrontHigh or SurfaceKind.FrontLow => FullBlock,
        SurfaceKind.Top => DarkShade,
        _ => MediumShade,
    };

    private static char AsciiGlyph(SurfaceKind kind) => kind switch
    {
        SurfaceKind.Front or SurfaceKind.FrontHigh or SurfaceKind.FrontLow => '#',
        SurfaceKind.Top => '=',
        SurfaceKind.Side => '/',
        SurfaceKind.Back => '+',
        _ => '.',
    };

    private static Sample Dominant(Sample first, Sample second)
        => Priority(second.Kind) > Priority(first.Kind) ? second : first;

    private static int Priority(SurfaceKind kind) => kind switch
    {
        SurfaceKind.FrontHigh => 6,
        SurfaceKind.Front => 5,
        SurfaceKind.FrontLow => 4,
        SurfaceKind.Top => 3,
        SurfaceKind.Side => 2,
        SurfaceKind.Back => 1,
        _ => 0,
    };
}
