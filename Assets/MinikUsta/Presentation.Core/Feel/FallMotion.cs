#nullable enable
// Fall timing from tokens.physics (JUICE §0.1; port of web src/scenes/level/motion.ts `fallDurationMs`, `fallDistanceAt`,
// `fallLeg`, `glideMs` and juice/plan.ts `releaseFallMs`, `yardFallMs`). Falls accelerate up to a top speed — they are
// never "ms per row" — and the K-19 rules contain no speed: the core decides where a block lands, this only says how long
// the view takes to get there.
using System;

namespace MinikUsta.Presentation.Feel
{
    /// <summary>An accelerated fall: its duration and the progress curve (u → fallen distance fraction, 0 → 1).</summary>
    public readonly struct FallLeg
    {
        public readonly float Ms;
        private readonly double rows;
        private readonly double accel;
        private readonly double maxSpeed;

        public FallLeg(float ms, double rows, double accel, double maxSpeed)
        {
            Ms = ms;
            this.rows = rows;
            this.accel = accel;
            this.maxSpeed = maxSpeed;
        }

        /// <summary>Distance fraction at linear time `u` (monotonic, 0 at 0, 1 at 1); usable as `Tween.SetCurve`.</summary>
        public float Progress(float u)
        {
            if (Ms <= 0 || u >= 1) return 1f;
            if (u <= 0) return 0f;
            return (float)Math.Min(1.0, FallMotion.DistanceAt(u * Ms, accel, maxSpeed) / rows);
        }
    }

    public static class FallMotion
    {
        /// <summary>Duration (ms) of a fall of `rows` cells: constant acceleration `accel` (cells/s²) up to `maxSpeed` (cells/s).</summary>
        public static double DurationMs(double rows, double accel, double maxSpeed)
        {
            double d = Math.Abs(rows);
            if (d == 0) return 0;
            if (!(accel > 0) || !(maxSpeed > 0)) throw new ArgumentOutOfRangeException(nameof(accel), "fallDurationMs: accel and maxSpeed must be > 0");
            double t1 = maxSpeed / accel;
            double d1 = maxSpeed * maxSpeed / (2 * accel);
            double t = d <= d1 ? Math.Sqrt(2 * d / accel) : t1 + (d - d1) / maxSpeed;
            return t * 1000;
        }

        /// <summary>Cells fallen after `ms` with the same profile (not clamped to a distance).</summary>
        public static double DistanceAt(double ms, double accel, double maxSpeed)
        {
            double t = Math.Max(0, ms) / 1000;
            double t1 = maxSpeed / accel;
            if (t <= t1) return 0.5 * accel * t * t;
            return maxSpeed * maxSpeed / (2 * accel) + maxSpeed * (t - t1);
        }

        public static FallLeg Leg(double rows, double accel, double maxSpeed)
        {
            double d = Math.Abs(rows);
            return new FallLeg((float)DurationMs(d, accel, maxSpeed), d, accel, maxSpeed);
        }

        /// <summary>Duration of a constant-speed glide (G-L release, balloon rise).</summary>
        public static double GlideMs(double rows, double speed)
        {
            if (!(speed > 0)) throw new ArgumentOutOfRangeException(nameof(speed), "glideMs: speed must be > 0");
            return Math.Abs(rows) / speed * 1000;
        }

        /// <summary>Release fall on the site per gravity profile (`CompiledLevel.Gravity.Build`: low | normal | high).</summary>
        public static float ReleaseFallMs(int rows, string gravity, FeelSettings f)
        {
            if (gravity == "low") return (float)GlideMs(rows, f.FallLowSpeed);
            if (gravity == "high") return (float)DurationMs(rows, f.FallHighAccel, f.FallHighMax);
            return (float)DurationMs(rows, f.FallNormalAccel, f.FallNormalMax);
        }

        /// <summary>The progress curve of a release fall (linear for the G-L glide).</summary>
        public static Func<float, float> ReleaseFallCurve(int rows, string gravity, FeelSettings f)
        {
            if (gravity == "low") return u => u <= 0 ? 0f : u >= 1 ? 1f : u;
            var leg = gravity == "high" ? Leg(rows, f.FallHighAccel, f.FallHighMax) : Leg(rows, f.FallNormalAccel, f.FallNormalMax);
            return leg.Progress;
        }

        /// <summary>Yard gravity cascade, truck drops and the K-17 drop over the yard (`physics.yardFall*`).</summary>
        public static float YardFallMs(int rows, FeelSettings f) => (float)DurationMs(rows, f.YardFallAccel, f.YardFallMax);

        public static FallLeg YardFallLeg(int rows, FeelSettings f) => Leg(rows, f.YardFallAccel, f.YardFallMax);

        /// <summary>
        /// Rows a block above the yard falls to land at row `toY` (K-17 step 2 "sahanın üstünden düşürme", K-25
        /// `y0 = rows − h`): `max(0, gridRows − h − toY)`; `gridRows` = the level's `geo.Rows` (TECH §2R.1).
        /// </summary>
        public static int DropRows(int toY, int h, int gridRows) => Math.Max(0, gridRows - h - toY);
    }
}
