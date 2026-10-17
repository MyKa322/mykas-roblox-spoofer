namespace MyKa.Spoofer.Branding;

/// <summary>What a ray found. The front face is split by its bevel so letter edges read as raised.</summary>
public enum SurfaceKind : byte
{
    Empty,
    Shadow,
    Front,
    FrontHigh,
    FrontLow,
    Top,
    Side,
    Back,
}

/// <summary>One ray's result. Light runs 0 to 1; Tint runs 0 at the top of the wordmark to 1 at the bottom.</summary>
public readonly record struct Sample(SurfaceKind Kind, float Light, float Tint)
{
    /// <summary>True when the ray hit a letter rather than the background or its shadow.</summary>
    public bool IsSolid => Kind > SurfaceKind.Shadow;
}

/// <summary>A grid of samples, one column wide each, with one or two samples stacked in every terminal row.</summary>
public sealed class LogoFrame
{
    private readonly Sample[] samples;

    public LogoFrame(int columns, int subRows, int samplesPerCell)
    {
        Columns = columns;
        SubRows = subRows;
        SamplesPerCell = samplesPerCell;
        samples = new Sample[columns * subRows];
    }

    public int Columns { get; }
    public int SubRows { get; }
    public int SamplesPerCell { get; }
    public int Rows => SubRows / SamplesPerCell;

    public Sample this[int column, int subRow]
    {
        get => samples[subRow * Columns + column];
        set => samples[subRow * Columns + column] = value;
    }
}
