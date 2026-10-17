namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>
/// Matches a letter shortcut however the terminal reports the key. Some report the physical key, some only the
/// character typed, and on a Russian keyboard layout the character is Cyrillic. All three are accepted.
/// </summary>
internal static class Hotkey
{
    // The Cyrillic letter on the same physical key of the standard Russian layout, for each shortcut the screen uses.
    private static readonly Dictionary<char, char> Russian = new()
    {
        ['a'] = 'ф',
        ['c'] = 'с',
        ['d'] = 'в',
        ['q'] = 'й',
        ['r'] = 'к',
        ['s'] = 'ы',
        ['y'] = 'н',
    };

    public static bool Is(ConsoleKeyInfo key, char letter)
    {
        // Ctrl and Alt combinations belong to the terminal and the system, not to this screen.
        if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) return false;

        var lower = char.ToLowerInvariant(letter);
        if (key.Key == (ConsoleKey)char.ToUpperInvariant(letter)) return true;

        var typed = char.ToLowerInvariant(key.KeyChar);
        return typed == lower || (Russian.TryGetValue(lower, out var cyrillic) && typed == cyrillic);
    }
}
