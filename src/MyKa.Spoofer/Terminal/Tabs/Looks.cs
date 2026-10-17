using Spectre.Console;
using SpectreColor = Spectre.Console.Color;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>The few styles the tabbed screen uses. What a line means is always in its words; colour only adds emphasis.</summary>
internal static class Looks
{
    public static Style Dim { get; } = new(SpectreColor.Grey);
    public static Style Strong { get; } = new(decoration: Decoration.Bold);
    public static Style Accent { get; } = new(SpectreColor.Cyan1, decoration: Decoration.Bold);
    public static Style Info { get; } = new(SpectreColor.Cyan1);
    public static Style Selected { get; } = new(SpectreColor.Black, SpectreColor.Cyan1, Decoration.Bold);
    public static Style Good { get; } = new(SpectreColor.Green);
    public static Style Warn { get; } = new(SpectreColor.Yellow);
    public static Style Bad { get; } = new(SpectreColor.Red);
}
