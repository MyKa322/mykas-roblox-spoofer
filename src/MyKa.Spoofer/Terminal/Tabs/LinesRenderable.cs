using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>Draws lines exactly as given, one terminal row each, cutting any that are too wide instead of wrapping them.</summary>
internal sealed class LinesRenderable(IReadOnlyList<Line> lines) : IRenderable
{
    public Measurement Measure(RenderOptions options, int maxWidth)
    {
        var widest = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
        return new Measurement(0, Math.Min(widest, maxWidth));
    }

    public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        foreach (var line in lines)
        {
            foreach (var segment in line.ToSegments(maxWidth)) yield return segment;
            yield return Segment.LineBreak;
        }
    }
}
