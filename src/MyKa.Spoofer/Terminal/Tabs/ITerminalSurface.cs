using Spectre.Console.Rendering;

namespace MyKa.Spoofer.Terminal.Tabs;

/// <summary>Why a run of frames ended.</summary>
internal enum SurfaceExit
{
    Quit,

    /// <summary>The window changed size, so the screen must be set up again for the new size.</summary>
    Resized,
}

/// <summary>Where frames are drawn. Each one replaces the last in place.</summary>
internal interface IFrameSink
{
    void Draw(IRenderable frame);
}

/// <summary>A full-screen drawing area. The real one sits on the terminal's alternate screen; tests use a recording one.</summary>
internal interface ITerminalSurface
{
    (int Width, int Height) Size { get; }

    /// <summary>Takes over the screen until the returned scope is disposed, then gives the user's terminal back untouched.</summary>
    IDisposable Enter();

    /// <summary>Clears the screen, shows <paramref name="first"/>, and runs <paramref name="body"/> to draw further frames.</summary>
    SurfaceExit Show(IRenderable first, Func<IFrameSink, SurfaceExit> body);
}
