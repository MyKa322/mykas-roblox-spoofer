namespace MyKa.Spoofer.Branding;

/// <summary>
/// Ray-casts the extruded wordmark with a small perspective camera. Each sample is one column wide and half a
/// terminal row tall, so a bitmap pixel stays square on screen. Background samples are tested against an invisible
/// wall behind the letters so the wordmark casts a shadow that moves as it turns.
/// </summary>
public sealed class LogoRenderer
{
    private const double EyeDistance = 200;
    private const double Ambient = 0.30;
    private const double Diffuse = 0.95;
    private const double Epsilon = 1e-12;

    private static readonly V3 TowardLight = new V3(-0.38, 0.62, 0.70).Normalized();

    private readonly LogoLayout layout;
    private readonly int width;
    private readonly int height;
    private readonly int depth;

    public LogoRenderer(LogoLayout layout)
    {
        this.layout = layout;
        width = layout.Mask.Width;
        height = layout.Mask.Height;
        depth = layout.Depth;
    }

    public LogoFrame Render(LogoPose pose)
    {
        var view = new View(pose.Yaw, pose.Pitch);
        var frame = new LogoFrame(layout.Columns, layout.SubRows, layout.SamplesPerCell);
        var eye = view.ToModel(new V3(0, -layout.ShiftY, EyeDistance));
        var right = view.ToModel(new V3(1, 0, 0));
        var up = view.ToModel(new V3(0, 1, 0));
        var towardEye = view.ToModel(new V3(0, 0, 1));
        var towardLight = view.ToModel(TowardLight);
        for (var row = 0; row < frame.SubRows; row++)
        {
            var y = frame.SubRows / 2.0 - (row + 0.5);
            for (var column = 0; column < frame.Columns; column++)
            {
                var x = column + 0.5 - frame.Columns / 2.0;
                var direction = right * x + up * y - towardEye * EyeDistance;
                frame[column, row] = Trace(eye, direction, x, y, view, towardLight);
            }
        }
        return frame;
    }

    private Sample Trace(V3 eye, V3 direction, double x, double y, View view, V3 towardLight)
    {
        if (Cast(eye, direction, out var hit)) return Shade(hit, view);

        var wallZ = -(depth / 2.0 + layout.WallGap);
        var along = (EyeDistance - wallZ) / EyeDistance;
        var wallPoint = new V3(x * along, -layout.ShiftY + y * along, wallZ);
        return Cast(view.ToModel(wallPoint), towardLight, out _)
            ? new Sample(SurfaceKind.Shadow, 0, 0)
            : default;
    }

    private Sample Shade(Hit hit, View view)
    {
        var normal = hit.Axis switch
        {
            0 => new V3(hit.Sign, 0, 0),
            1 => new V3(0, hit.Sign, 0),
            _ => new V3(0, 0, hit.Sign),
        };
        var diffuse = Math.Max(0, view.ToWorld(normal).Dot(TowardLight));
        var light = (float)Math.Min(1, Ambient + Diffuse * diffuse);
        var tint = (float)(height - 1 - hit.Y) / Math.Max(1, height - 1);
        var kind = hit.Axis switch
        {
            2 when hit.Sign > 0 => Bevel(hit.X, hit.Y),
            2 => SurfaceKind.Back,
            1 when hit.Sign > 0 => SurfaceKind.Top,
            _ => SurfaceKind.Side,
        };
        if (kind is not (SurfaceKind.Top or SurfaceKind.Side)) return new Sample(kind, light, tint);

        // Walls fade toward the back of the letters so their thickness reads.
        var fade = (float)(0.6 + 0.4 * (hit.Z + 0.5) / depth);
        return new Sample(kind, light * fade, tint);
    }

    /// <summary>Marks front pixels on a lit (upper-left) edge as raised and those on a lower-right edge as recessed.</summary>
    private SurfaceKind Bevel(int x, int yUp)
    {
        var row = height - 1 - yUp;
        var mask = layout.Mask;
        var score = (mask[x, row - 1] ? 0 : 1) + (mask[x - 1, row] ? 0 : 1)
            - (mask[x, row + 1] ? 0 : 1) - (mask[x + 1, row] ? 0 : 1);
        return score > 0 ? SurfaceKind.FrontHigh : score < 0 ? SurfaceKind.FrontLow : SurfaceKind.Front;
    }

    private readonly record struct Hit(int X, int Y, int Z, int Axis, int Sign);

    /// <summary>Walks the ray through the voxel grid and reports the first letter voxel it enters.</summary>
    private bool Cast(V3 origin, V3 direction, out Hit hit)
    {
        hit = default;
        double near = 0, far = double.PositiveInfinity;
        int axis = 2, sign = 1;
        if (!Clip(origin.X, direction.X, width / 2.0, 0, ref near, ref far, ref axis, ref sign)
            || !Clip(origin.Y, direction.Y, height / 2.0, 1, ref near, ref far, ref axis, ref sign)
            || !Clip(origin.Z, direction.Z, depth / 2.0, 2, ref near, ref far, ref axis, ref sign))
            return false;

        var start = origin + direction * (near + 1e-7);
        var sx = start.X + width / 2.0;
        var sy = start.Y + height / 2.0;
        var sz = start.Z + depth / 2.0;
        var x = Math.Clamp((int)Math.Floor(sx), 0, width - 1);
        var y = Math.Clamp((int)Math.Floor(sy), 0, height - 1);
        var z = Math.Clamp((int)Math.Floor(sz), 0, depth - 1);
        var stepX = direction.X >= 0 ? 1 : -1;
        var stepY = direction.Y >= 0 ? 1 : -1;
        var stepZ = direction.Z >= 0 ? 1 : -1;
        var nextX = Boundary(sx, x, stepX, direction.X);
        var nextY = Boundary(sy, y, stepY, direction.Y);
        var nextZ = Boundary(sz, z, stepZ, direction.Z);
        var spanX = Span(direction.X);
        var spanY = Span(direction.Y);
        var spanZ = Span(direction.Z);

        while (true)
        {
            if (layout.Mask[x, height - 1 - y])
            {
                hit = new Hit(x, y, z, axis, sign);
                return true;
            }
            if (nextX <= nextY && nextX <= nextZ)
            {
                x += stepX;
                if (x < 0 || x >= width) return false;
                nextX += spanX;
                axis = 0;
                sign = -stepX;
            }
            else if (nextY <= nextZ)
            {
                y += stepY;
                if (y < 0 || y >= height) return false;
                nextY += spanY;
                axis = 1;
                sign = -stepY;
            }
            else
            {
                z += stepZ;
                if (z < 0 || z >= depth) return false;
                nextZ += spanZ;
                axis = 2;
                sign = -stepZ;
            }
        }
    }

    /// <summary>Narrows the ray's overlap with the box on one axis and remembers which face it entered through.</summary>
    private static bool Clip(double origin, double direction, double half, int axisIndex,
        ref double near, ref double far, ref int axis, ref int sign)
    {
        if (Math.Abs(direction) < Epsilon) return origin >= -half && origin <= half;
        var first = (-half - origin) / direction;
        var second = (half - origin) / direction;
        if (first > second) (first, second) = (second, first);
        if (first > near)
        {
            near = first;
            axis = axisIndex;
            sign = direction > 0 ? -1 : 1;
        }
        if (second < far) far = second;
        return near <= far;
    }

    private static double Boundary(double position, int index, int step, double component)
        => Math.Abs(component) < Epsilon
            ? double.PositiveInfinity
            : ((step > 0 ? index + 1 : index) - position) / component;

    private static double Span(double component)
        => Math.Abs(component) < Epsilon ? double.PositiveInfinity : Math.Abs(1 / component);

    /// <summary>The wordmark's rotation: world = pitch(yaw(model)).</summary>
    private readonly struct View
    {
        private readonly double cosYaw;
        private readonly double sinYaw;
        private readonly double cosPitch;
        private readonly double sinPitch;

        public View(double yaw, double pitch)
        {
            cosYaw = Math.Cos(yaw);
            sinYaw = Math.Sin(yaw);
            cosPitch = Math.Cos(pitch);
            sinPitch = Math.Sin(pitch);
        }

        public V3 ToWorld(V3 model)
        {
            var turned = new V3(model.X * cosYaw + model.Z * sinYaw, model.Y, -model.X * sinYaw + model.Z * cosYaw);
            return new V3(turned.X, turned.Y * cosPitch - turned.Z * sinPitch, turned.Y * sinPitch + turned.Z * cosPitch);
        }

        public V3 ToModel(V3 world)
        {
            var tipped = new V3(world.X, world.Y * cosPitch + world.Z * sinPitch, -world.Y * sinPitch + world.Z * cosPitch);
            return new V3(tipped.X * cosYaw - tipped.Z * sinYaw, tipped.Y, tipped.X * sinYaw + tipped.Z * cosYaw);
        }
    }
}
