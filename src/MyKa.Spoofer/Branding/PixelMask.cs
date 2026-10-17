namespace MyKa.Spoofer.Branding;

/// <summary>A rectangular on/off bitmap. Rows run top to bottom and anything outside the bitmap is off.</summary>
public sealed class PixelMask
{
    private readonly bool[] cells;

    public PixelMask(int width, int height, bool[] cells)
    {
        if (width <= 0 || height <= 0 || cells.Length != width * height)
            throw new ArgumentException("Mask dimensions do not match its cells.");
        Width = width;
        Height = height;
        this.cells = cells;
    }

    public int Width { get; }
    public int Height { get; }

    public bool this[int x, int y] => x >= 0 && x < Width && y >= 0 && y < Height && cells[y * Width + x];

    /// <summary>Builds a mask from equal-width rows where '#' is on.</summary>
    public static PixelMask FromRows(IReadOnlyList<string> rows)
    {
        var width = rows[0].Length;
        if (rows.Any(row => row.Length != width)) throw new ArgumentException("All rows must have the same width.");
        var cells = new bool[width * rows.Count];
        for (var y = 0; y < rows.Count; y++)
            for (var x = 0; x < width; x++)
                cells[y * width + x] = rows[y][x] == '#';
        return new PixelMask(width, rows.Count, cells);
    }

    /// <summary>Places masks side by side, top aligned, separated by <paramref name="gap"/> empty columns.</summary>
    public static PixelMask Concat(IReadOnlyList<PixelMask> parts, int gap)
    {
        var width = parts.Sum(part => part.Width) + gap * (parts.Count - 1);
        var height = parts.Max(part => part.Height);
        var cells = new bool[width * height];
        var left = 0;
        foreach (var part in parts)
        {
            Copy(part, cells, width, left, 0);
            left += part.Width + gap;
        }
        return new PixelMask(width, height, cells);
    }

    /// <summary>Places masks one above the other, centred, separated by <paramref name="gap"/> empty rows.</summary>
    public static PixelMask Stack(IReadOnlyList<PixelMask> parts, int gap)
    {
        var width = parts.Max(part => part.Width);
        var height = parts.Sum(part => part.Height) + gap * (parts.Count - 1);
        var cells = new bool[width * height];
        var top = 0;
        foreach (var part in parts)
        {
            Copy(part, cells, width, (width - part.Width) / 2, top);
            top += part.Height + gap;
        }
        return new PixelMask(width, height, cells);
    }

    private static void Copy(PixelMask part, bool[] target, int targetWidth, int left, int top)
    {
        for (var y = 0; y < part.Height; y++)
            for (var x = 0; x < part.Width; x++)
                target[(top + y) * targetWidth + left + x] = part[x, y];
    }
}
