#nullable enable
// Small 2D value types of the engine-independent presentation layer (D-096). Unity's Vector2 / Rect are not available
// here (asmdef noEngineReferences); the Unity layer converts at its edge.
using System;

namespace MinikUsta.Presentation
{
    /// <summary>
    /// A float 2D vector for tween targets (position in design px, scale factors). Design px: x to the right, y DOWN from the
    /// top-left corner of the 1080-wide design canvas (UX_FLOWS §5 coordinates); the Unity adapter flips y.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 One = new Vec2(1f, 1f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Y * k);

        /// <summary>`a + (b − a)·k`; `k` may leave [0, 1] (overshooting eases).</summary>
        public static Vec2 Lerp(Vec2 a, Vec2 b, float k) => new Vec2(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k);

        public bool Equals(Vec2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => "(" + Num.Text(X) + ", " + Num.Text(Y) + ")";
    }

    /// <summary>
    /// An axis-aligned rectangle in design px (double: the layout and drag math match the TypeScript numbers exactly). `X, Y`
    /// = top-left corner, y down. Containment is half-open: `[X, X + W) × [Y, Y + H)`.
    /// </summary>
    public readonly struct PxRect : IEquatable<PxRect>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double W;
        public readonly double H;

        public PxRect(double x, double y, double w, double h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public double Right => X + W;
        public double Bottom => Y + H;
        public double CenterX => X + W / 2;
        public double CenterY => Y + H / 2;

        public bool Contains(double px, double py) => px >= X && px < X + W && py >= Y && py < Y + H;

        /// <summary>Grown by `d` on every side (UX §0.1 block hit slop).</summary>
        public PxRect Grow(double d) => new PxRect(X - d, Y - d, W + 2 * d, H + 2 * d);

        public bool Overlaps(PxRect o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;

        public bool Equals(PxRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object? obj) => obj is PxRect r && Equals(r);
        public override int GetHashCode() => ((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ W.GetHashCode()) * 397 ^ H.GetHashCode();
        public override string ToString() => "{x " + Num.Text(X) + ", y " + Num.Text(Y) + ", w " + Num.Text(W) + ", h " + Num.Text(H) + "}";
    }

    /// <summary>Culture-independent number text for logs and test messages (REVIEW_PHASE2 R-06).</summary>
    internal static class Num
    {
        public static string Text(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        public static string Text(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
