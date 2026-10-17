namespace MyKa.Spoofer.Branding;

/// <summary>The plain ASCII logo, drawn from the same model as the colour one. assets/logo.txt is a snapshot of it.</summary>
public static class LogoText
{
    public const string Title = "MyKa's Roblox Spoofer";

    /// <summary>The rest of the wordmark, which the plain art leaves out to stay readable.</summary>
    public const string Subtitle = "R O B L O X   S P O O F E R";

    /// <summary>Turned slightly and level, so side walls show as '/' without top faces cluttering the letters.</summary>
    public static LogoPose PlainPose { get; } = new(-0.30, 0);

    /// <summary>The plain art with blank rows at both ends and any indentation shared by every row removed.</summary>
    public static IReadOnlyList<string> PlainArt()
    {
        var frame = new LogoRenderer(LogoLayout.Plain).Render(PlainPose);
        var lines = LogoPainter.ToLines(new LogoPainter(ascii: true).Paint(frame)).ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var indent = lines.Where(line => line.Length > 0).Min(line => line.Length - line.TrimStart().Length);
        return lines.Select(line => line.Length == 0 ? line : line[indent..]).ToArray();
    }

    /// <summary>The full plain logo: exact title, the art, and the subtitle centred beneath it.</summary>
    public static IReadOnlyList<string> PlainDocument()
    {
        var art = PlainArt();
        var width = art.Max(line => line.Length);
        var subtitle = new string(' ', Math.Max(0, (width - Subtitle.Length) / 2)) + Subtitle;
        return [Title, "", .. art, "", subtitle];
    }
}
