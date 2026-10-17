using Spectre.Console;
using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>
/// The real screen: the terminal's alternate buffer with a Spectre live display on it. Leaving the buffer restores
/// what the user had on screen, so nothing from the tabs is ever left behind in the scrollback.
/// </summary>
internal sealed class SpectreSurface(IAnsiConsole console) : ITerminalSurface
{
    private const string EnterAlternate = "\u001b[?1049h";
    private const string LeaveAlternate = "\u001b[?1049l";

    /// <summary>Whether this terminal can do full-screen drawing at all.</summary>
    public static bool Supported(IAnsiConsole console)
        => console.Profile.Capabilities is { Ansi: true, AlternateBuffer: true, Interactive: true };

    public (int Width, int Height) Size => (console.Profile.Width, console.Profile.Height);

    public IDisposable Enter()
    {
        console.Write(new ControlCode(EnterAlternate));
        return new Scope(console);
    }

    public SurfaceExit Show(IRenderable first, Func<IFrameSink, SurfaceExit> body)
    {
        console.Clear(true);
        var exit = SurfaceExit.Quit;
        console.Live(first)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Bottom)
            .Start(context => exit = body(new Sink(context)));
        return exit;
    }

    private sealed class Sink(LiveDisplayContext context) : IFrameSink
    {
        public void Draw(IRenderable frame)
        {
            context.UpdateTarget(frame);
            context.Refresh();
        }
    }

    private sealed class Scope(IAnsiConsole console) : IDisposable
    {
        public void Dispose() => console.Write(new ControlCode(LeaveAlternate));
    }
}
