namespace MyKa.Spoofer.Branding;

internal readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);

    public double Dot(V3 other) => X * other.X + Y * other.Y + Z * other.Z;

    public V3 Normalized()
    {
        var length = Math.Sqrt(Dot(this));
        return new V3(X / length, Y / length, Z / length);
    }
}
