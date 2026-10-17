namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>What a page wants after a key: a redraw, and optionally slow work to run once that redraw is on screen.</summary>
internal readonly record struct PageResult(bool Changed, Action? Work = null)
{
    public static PageResult None { get; } = new(false);

    public static PageResult Redraw { get; } = new(true);
}

/// <summary>The content of one tab.</summary>
internal interface ITabPage
{
    string Title { get; }

    /// <summary>True while the page is asking a question, so the tab and quit keys answer it instead of acting.</summary>
    bool IsModal { get; }

    /// <summary>The keys this page understands in its current state, for the footer.</summary>
    string Hints { get; }

    /// <summary>Shorter action labels when the footer cannot fit the full hints.</summary>
    string HintsFor(int width) => Hints;

    /// <summary>Called whenever the tab becomes the one shown.</summary>
    void Activate();

    /// <summary>Handles a key the tab strip did not use.</summary>
    PageResult Handle(ConsoleKeyInfo key);

    /// <summary>The page's rows, at most <paramref name="rows"/> of them. A row wider than <paramref name="width"/> is cut when drawn.</summary>
    IReadOnlyList<Line> Render(int width, int rows);
}
