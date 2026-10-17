namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>
/// How a page divides its rows between a heading, an optional column header, a scrolling body and lines pinned to
/// the bottom. When rows run short, the least important parts go first, so the heading, a body row and the last
/// pinned lines (where results and honesty notes sit) always stay.
/// </summary>
internal readonly record struct PageFrame(bool SpaceAbove, Line? Header, IReadOnlyList<Line> Bottom, int BodyRows)
{
    public static PageFrame Arrange(Line? header, IReadOnlyList<Line> bottom, int rows, int minBody)
    {
        var body = Math.Min(Math.Max(1, minBody), Math.Max(1, rows - 1));
        var room = Math.Max(0, rows - 1 - body);

        var kept = bottom.TakeLast(Math.Min(room, bottom.Count)).ToList();
        room -= kept.Count;
        var useHeader = header is not null && room > 0;
        if (useHeader) room--;
        var spaceAbove = room > 0;
        if (spaceAbove) room--;
        var spaceBelow = kept.Count > 0 && room > 0;
        if (spaceBelow)
        {
            kept.Insert(0, Line.Empty);
            room--;
        }
        return new PageFrame(spaceAbove, useHeader ? header : null, kept, body + room);
    }

    public IReadOnlyList<Line> Compose(Line heading, IEnumerable<Line> shown)
    {
        var lines = new List<Line> { heading };
        if (SpaceAbove) lines.Add(Line.Empty);
        if (Header is not null) lines.Add(Header);
        lines.AddRange(shown.Take(BodyRows));
        lines.AddRange(Bottom);
        return lines;
    }
}
