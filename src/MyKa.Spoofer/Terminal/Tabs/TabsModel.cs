namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>What the tab strip did with a key.</summary>
internal enum TabKeyResult
{
    /// <summary>The strip had no use for it; the page should get it.</summary>
    Ignored,

    /// <summary>Another tab is now shown.</summary>
    Switched,

    /// <summary>Help or motion changed without switching the underlying tab.</summary>
    Updated,

    /// <summary>The user asked to leave.</summary>
    Quit,
}

/// <summary>Which tab is shown, and the keys that change it: arrows, Tab, number keys, Esc and Q.</summary>
internal sealed class TabsModel
{
    private readonly IReadOnlyList<ITabPage> pages;

    private HelpPage? help;

    public TabsModel(IReadOnlyList<ITabPage> pages, bool animate = true)
    {
        if (pages.Count == 0) throw new ArgumentException("There must be at least one tab.");
        this.pages = pages;
        Animate = animate;
    }

    public IReadOnlyList<ITabPage> Pages => pages;

    public int Selected { get; private set; }

    public ITabPage Current => help is not null ? help : pages[Selected];

    public bool HelpOpen => help is not null;

    public bool Animate { get; private set; }

    public TabKeyResult Handle(ConsoleKeyInfo key)
    {
        // Account confirmations retain their "any other key keeps it" contract.
        if (!HelpOpen && Current.IsModal) return TabKeyResult.Ignored;
        if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0)
            return TabKeyResult.Ignored;
        if (key.Key == ConsoleKey.F1 || key.KeyChar == '?' || (HelpOpen && key.Key == ConsoleKey.Escape))
        {
            help = HelpOpen ? null : new HelpPage();
            return TabKeyResult.Updated;
        }
        if (key.Key == ConsoleKey.Spacebar)
        {
            Animate = !Animate;
            return TabKeyResult.Updated;
        }
        if (HelpOpen) return TabKeyResult.Ignored;
        if (key.Key == ConsoleKey.Escape || Hotkey.Is(key, 'q')) return TabKeyResult.Quit;
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow: return Select(Selected - 1);
            case ConsoleKey.RightArrow: return Select(Selected + 1);
            case ConsoleKey.Tab: return Select(Selected + ((key.Modifiers & ConsoleModifiers.Shift) != 0 ? -1 : 1));
        }
        return DigitOf(key) is { } digit && digit <= pages.Count ? Select(digit - 1) : TabKeyResult.Ignored;
    }

    private TabKeyResult Select(int index)
    {
        var next = (index % pages.Count + pages.Count) % pages.Count;
        if (next == Selected) return TabKeyResult.Ignored;
        Selected = next;
        Current.Activate();
        return TabKeyResult.Switched;
    }

    private static int? DigitOf(ConsoleKeyInfo key)
    {
        if (key.KeyChar is >= '1' and <= '9') return key.KeyChar - '0';
        if (key.Key is >= ConsoleKey.D1 and <= ConsoleKey.D9) return key.Key - ConsoleKey.D0;
        if (key.Key is >= ConsoleKey.NumPad1 and <= ConsoleKey.NumPad9) return key.Key - ConsoleKey.NumPad0;
        return null;
    }
}
