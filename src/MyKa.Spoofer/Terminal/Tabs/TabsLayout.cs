using MyKa.Spoofer.Branding;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>
/// How a terminal of a given size is divided: an optional logo, the fixed chrome, and whatever is left for the page.
/// The frame is always one row shorter than the window, so drawing it never scrolls the screen.
/// </summary>
internal readonly record struct TabsLayout(int Width, int Height, LogoLayout? Logo, int ContentRows, bool TooSmall)
{
    public const int Margin = 2;
    public const int MinWidth = 50;

    /// <summary>Title, status, a spacer, the tab bar, its rule, and two footer lines.</summary>
    public const int FixedRows = 7;

    public const int SpareRows = 1;

    /// <summary>A logo is only shown while the page still keeps at least this many rows. Information comes first.</summary>
    public const int PreferredContentRows = 12;

    public const int MinContentRows = 4;
    public const int MinHeight = FixedRows + SpareRows + MinContentRows;

    public int ContentWidth => Math.Max(0, Width - 2 * Margin);

    /// <summary>The short logo spells only the first word, so the rest of the name goes on a line beneath it.</summary>
    public bool HasSubtitle => Logo == LogoLayout.Compact;

    public static TabsLayout For(int width, int height)
    {
        var withoutLogo = height - SpareRows - FixedRows;
        if (width < MinWidth || withoutLogo < MinContentRows) return new TabsLayout(width, height, null, 0, true);

        foreach (var logo in new[] { LogoLayout.Large, LogoLayout.Compact })
        {
            var rows = logo.Rows + (logo == LogoLayout.Compact ? 1 : 0);
            if (width >= logo.Columns + 2 && withoutLogo - rows >= PreferredContentRows)
                return new TabsLayout(width, height, logo, withoutLogo - rows, false);
        }
        return new TabsLayout(width, height, null, withoutLogo, false);
    }
}
