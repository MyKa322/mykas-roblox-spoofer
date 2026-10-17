namespace MyKa.Spoofer.Branding;

/// <summary>How the wordmark is turned. Yaw spins it about the vertical axis; pitch tips its top toward the viewer.</summary>
public readonly record struct LogoPose(double Yaw, double Pitch);

public static class LogoMotion
{
    /// <summary>Seconds for one full turn of the idle animation.</summary>
    public const double PeriodSeconds = 9.0;

    /// <summary>Seconds for the single spin played by the logo command.</summary>
    public const double IntroSeconds = 2.8;

    /// <summary>Delay between frames: about twenty a second, which keeps the turn smooth without taxing the terminal.</summary>
    public const int FrameMilliseconds = 50;

    /// <summary>The tilt, in radians, when the wordmark faces the viewer.</summary>
    public const double FacingPitch = 0.18;

    // 0 would spin at a constant speed. Closer to 1 lingers longer facing the viewer and whips round the back.
    private const double Dwell = 0.72;

    /// <summary>The resting pose: turned slightly so the letters show their depth.</summary>
    public static LogoPose Still { get; } = Facing(-0.36);

    /// <summary>The idle animation. It repeats every <see cref="PeriodSeconds"/> and never stops turning.</summary>
    public static LogoPose At(double seconds)
    {
        if (!double.IsFinite(seconds)) return Still;
        var turns = seconds / PeriodSeconds;
        var phase = 2 * Math.PI * (turns - Math.Floor(turns));
        return Facing(phase - Dwell * Math.Sin(phase));
    }

    /// <summary>One eased full turn that ends exactly on <see cref="Still"/>.</summary>
    public static LogoPose Intro(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds >= IntroSeconds) return Still;
        var progress = Math.Clamp(seconds / IntroSeconds, 0, 1);
        var eased = progress * progress * (3 - 2 * progress);
        return Facing(Still.Yaw + 2 * Math.PI * eased);
    }

    /// <summary>
    /// A pose at the given yaw. The tilt eases off as the wordmark turns edge-on: tipped, its nearest corner would
    /// otherwise swing out of the frame, and the tilt is least visible then anyway.
    /// </summary>
    private static LogoPose Facing(double yaw)
    {
        var across = Math.Cos(yaw);
        return new LogoPose(yaw, FacingPitch * across * across);
    }
}
