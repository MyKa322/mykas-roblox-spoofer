namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>The first visible row of a list that may be taller than the room it is drawn in.</summary>
internal sealed class Scroller
{
    public int Offset { get; private set; }

    public void Clamp(int total, int rows) => Offset = Math.Clamp(Offset, 0, Math.Max(0, total - Math.Max(1, rows)));

    public void By(int delta, int total, int rows)
    {
        Offset += delta;
        Clamp(total, rows);
    }

    public void Home() => Offset = 0;

    public void End(int total, int rows)
    {
        Offset = int.MaxValue;
        Clamp(total, rows);
    }

    /// <summary>Scrolls the least amount that brings <paramref name="index"/> into view.</summary>
    public void Reveal(int index, int total, int rows)
    {
        var room = Math.Max(1, rows);
        if (index < Offset) Offset = index;
        else if (index >= Offset + room) Offset = index - room + 1;
        Clamp(total, rows);
    }

    /// <summary>Handles the scrolling keys. Returns false for any other key.</summary>
    public bool Handle(ConsoleKeyInfo key, int total, int rows)
    {
        var page = Math.Max(1, rows - 1);
        switch (key.Key)
        {
            case ConsoleKey.UpArrow: By(-1, total, rows); return true;
            case ConsoleKey.DownArrow: By(1, total, rows); return true;
            case ConsoleKey.PageUp: By(-page, total, rows); return true;
            case ConsoleKey.PageDown: By(page, total, rows); return true;
            case ConsoleKey.Home: Home(); return true;
            case ConsoleKey.End: End(total, rows); return true;
            default: return false;
        }
    }
}
