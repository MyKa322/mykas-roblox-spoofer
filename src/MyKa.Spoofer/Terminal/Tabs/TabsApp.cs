using MyKa.Spoofer.Branding;
using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>
/// The tabbed screen's loop. It owns one drawing area for the whole session: keys switch tabs or act inside a page,
/// the logo turns between keys, and a resize just redraws for the new size. Nothing is ever printed below the frame.
/// </summary>
internal sealed class TabsApp(IReadOnlyList<ITabPage> pages, ITerminalSurface surface, IKeySource keys, Line status,
    bool animate, Func<double> seconds, Action<int> sleep, CancellationToken cancellation)
{
    private const int IdleMilliseconds = 30;

    public int Run()
    {
        cancellation.ThrowIfCancellationRequested();
        var model = new TabsModel(pages, animate);
        model.Current.Activate();
        DiscardKeys();
        using (surface.Enter())
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var size = surface.Size;
                if (surface.Show(Frame(model, size), sink => Loop(model, size, sink)) == SurfaceExit.Quit) return 0;
            }
        }
    }

    private SurfaceExit Loop(TabsModel model, (int Width, int Height) size, IFrameSink sink)
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            // Resize before consuming input: a hidden page must never receive account actions.
            if (surface.Size != size) return SurfaceExit.Resized;
            var moving = model.Animate && !model.HelpOpen && TabsLayout.For(size.Width, size.Height).Logo is not null;
            var changed = moving;
            if (keys.Available)
            {
                var key = keys.Read();
                if (TabsLayout.For(size.Width, size.Height).TooSmall)
                {
                    if (key.Key == ConsoleKey.Escape || Hotkey.Is(key, 'q')) return SurfaceExit.Quit;
                }
                else
                {
                    changed = true;
                    if (HandleKey(model, key, size, sink)) return SurfaceExit.Quit;
                }
            }
            // Draw after each key so a queued answer cannot skip over its confirmation screen.
            if (surface.Size != size) return SurfaceExit.Resized;
            if (changed) sink.Draw(Frame(model, size));
            sleep(moving ? LogoMotion.FrameMilliseconds : IdleMilliseconds);
        }
    }

    /// <summary>Returns true when the key asked to leave.</summary>
    private bool HandleKey(TabsModel model, ConsoleKeyInfo key, (int Width, int Height) size, IFrameSink sink)
    {
        switch (model.Handle(key))
        {
            case TabKeyResult.Quit:
                return true;
            case TabKeyResult.Ignored:
                if (model.Current.Handle(key).Work is { } work)
                {
                    // Slow work blocks the loop, so what it is about to do must already be on screen.
                    sink.Draw(Frame(model, size));
                    work();
                    DiscardKeys();
                }
                break;
        }
        return false;
    }

    private IRenderable Frame(TabsModel model, (int Width, int Height) size)
    {
        var pose = model.Animate && !model.HelpOpen ? LogoMotion.At(seconds()) : LogoMotion.Still;
        return TabsView.Build(model, TabsLayout.For(size.Width, size.Height), pose, status);
    }

    private void DiscardKeys()
    {
        while (keys.Available) keys.Read();
    }
}
