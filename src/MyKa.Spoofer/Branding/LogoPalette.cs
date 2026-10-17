namespace MyKa.Spoofer.Branding;

public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>Colours for each surface: an aqua-to-violet gradient down the wordmark, lit by the sample's light.</summary>
public static class LogoPalette
{
    private static readonly Rgb[] Stops = [new(96, 235, 255), new(84, 156, 255), new(176, 124, 255)];
    private static readonly Rgb White = new(255, 255, 255);
    private static readonly Rgb Gray = new(130, 130, 150);

    public static Rgb Shadow { get; } = new(40, 44, 64);

    public static Rgb Of(Sample sample)
    {
        var tone = Gradient(sample.Tint);
        var lit = 0.55 + 0.45 * sample.Light;
        return sample.Kind switch
        {
            SurfaceKind.Front => Scale(tone, lit),
            SurfaceKind.FrontHigh => Scale(Mix(tone, White, 0.24), lit),
            SurfaceKind.FrontLow => Scale(tone, 0.78 * lit),
            SurfaceKind.Top => Scale(tone, 0.45 + 0.5 * sample.Light),
            SurfaceKind.Side => Scale(tone, 0.25 + 0.55 * sample.Light),
            SurfaceKind.Back => Scale(Mix(tone, Gray, 0.6), 0.35 + 0.4 * sample.Light),
            _ => Shadow,
        };
    }

    public static Rgb Mix(Rgb from, Rgb to, double amount) => new(
        Channel(from.R + (to.R - from.R) * amount),
        Channel(from.G + (to.G - from.G) * amount),
        Channel(from.B + (to.B - from.B) * amount));

    public static Rgb Scale(Rgb color, double factor)
        => new(Channel(color.R * factor), Channel(color.G * factor), Channel(color.B * factor));

    private static Rgb Gradient(double position)
    {
        var scaled = Math.Clamp(position, 0, 1) * (Stops.Length - 1);
        var index = Math.Min((int)scaled, Stops.Length - 2);
        return Mix(Stops[index], Stops[index + 1], scaled - index);
    }

    private static byte Channel(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
