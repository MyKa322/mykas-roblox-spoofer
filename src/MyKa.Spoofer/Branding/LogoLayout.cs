namespace MyKa.Spoofer.Branding;

/// <summary>A wordmark arrangement together with the character grid it is drawn on.</summary>
public sealed class LogoLayout
{
    private LogoLayout(string name, PixelMask mask, int depth, int columns, int rows, int samplesPerCell,
        double shiftY, double wallGap)
    {
        Name = name;
        Mask = mask;
        Depth = depth;
        Columns = columns;
        Rows = rows;
        SamplesPerCell = samplesPerCell;
        ShiftY = shiftY;
        WallGap = wallGap;
    }

    public string Name { get; }
    public PixelMask Mask { get; }

    /// <summary>Thickness of the extruded letters, in the same units as the bitmap pixels.</summary>
    public int Depth { get; }

    /// <summary>Terminal columns drawn. One bitmap pixel is one column wide.</summary>
    public int Columns { get; }

    /// <summary>Terminal rows drawn.</summary>
    public int Rows { get; }

    /// <summary>
    /// Two samples stack in each cell, so one bitmap pixel is half a row tall and stays square. One sample per cell
    /// gives the tall, narrow letters of classic ASCII art, which stay legible without colour.
    /// </summary>
    public int SamplesPerCell { get; }

    public int SubRows => Rows * SamplesPerCell;

    /// <summary>How far the wordmark sits above the centre of the frame, leaving room for its shadow.</summary>
    public double ShiftY { get; }

    /// <summary>Distance between the back of the letters and the invisible wall that catches their shadow.</summary>
    public double WallGap { get; }

    /// <summary>All three words stacked. Needs a colour terminal for its half-block detail.</summary>
    public static LogoLayout Large { get; } = new("large",
        PixelMask.Stack([BlockFont.Word("MYKA'S"), BlockFont.Word("ROBLOX"), BlockFont.Word("SPOOFER")], gap: 2),
        depth: 3, columns: 70, rows: 18, samplesPerCell: 2, shiftY: 1.0, wallGap: 1.5);

    /// <summary>Just the first word, for terminals too short for the full stack.</summary>
    public static LogoLayout Compact { get; } = new("compact", BlockFont.Word("MYKA'S"),
        depth: 3, columns: 53, rows: 7, samplesPerCell: 2, shiftY: 1.0, wallGap: 1.5);

    /// <summary>The first word at one sample per character, for plain ASCII output.</summary>
    public static LogoLayout Plain { get; } = new("plain", BlockFont.Word("MYKA'S"),
        depth: 3, columns: 53, rows: 12, samplesPerCell: 1, shiftY: 1.0, wallGap: 1.5);

    /// <summary>Picks the largest colour wordmark that fits, or null when only the plain title fits.</summary>
    public static LogoLayout? Choose(int width, int availableRows)
    {
        foreach (var layout in new[] { Large, Compact })
            if (width >= layout.Columns + 2 && availableRows >= layout.Rows) return layout;
        return null;
    }
}
