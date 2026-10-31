#nullable enable
// One animated value of the built-in tween (D-096). The factories in `Tweens` build the common ones (move, arc, scale,
// squash, pulse, rotate, fade, shake) on an `ITweenTarget`; `Tweens.Value` animates any float.
using System;

namespace MinikUsta.Presentation.Tweening
{
    /// <summary>
    /// The visual object a tween moves (a block view, a HUD chip …). The Unity layer implements it on a Transform +
    /// SpriteRenderer / Graphic. Units: `Position` design px (y down, UX_FLOWS §5 coordinates), `Scale` factors (1 = rest),
    /// `Rotation` degrees counter-clockwise on screen (Unity's z rotation), `Alpha` 0 … 1.
    /// </summary>
    public interface ITweenTarget
    {
        Vec2 Position { get; set; }
        Vec2 Scale { get; set; }
        float Rotation { get; set; }
        float Alpha { get; set; }
    }

    /// <summary>
    /// A tween of one value over `Duration` ms. `begin` captures the start values when the tween starts (after its delay);
    /// `apply(u, k)` writes the frame: `u` = linear progress 0 … 1, `k` = eased progress (may overshoot for Back / Elastic).
    /// The final frame is always `apply(1, 1)` (exact target).
    /// </summary>
    public sealed class Tween : Tweenable
    {
        private readonly float duration;
        private readonly Action? begin;
        private readonly Action<float, float> apply;
        private EaseType ease;
        private Func<float, float>? curve;

        public Tween(float durationMs, Action<float, float> apply, Action? begin = null, EaseType ease = EaseType.Linear)
        {
            if (!(durationMs >= 0)) throw new ArgumentOutOfRangeException(nameof(durationMs), "Tween: the duration must be ≥ 0");
            duration = durationMs;
            this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
            this.begin = begin;
            this.ease = ease;
        }

        public override float Duration => duration;

        public EaseType Ease => ease;

        /// <summary>A named ease (before the start).</summary>
        public Tween SetEase(EaseType value)
        {
            AssertNotStarted("SetEase");
            ease = value;
            curve = null;
            return this;
        }

        /// <summary>
        /// A custom progress curve u → k (before the start), e.g. the accelerated fall of `FallMotion` (progress = fallen
        /// distance fraction). The final frame is still `apply(1, 1)`.
        /// </summary>
        public Tween SetCurve(Func<float, float> value)
        {
            AssertNotStarted("SetCurve");
            curve = value ?? throw new ArgumentNullException(nameof(value));
            return this;
        }

        protected override void Begin() => begin?.Invoke();

        protected override void Apply(float active)
        {
            if (active >= duration)
            {
                apply(1f, 1f);
                return;
            }
            float u = active / duration;
            float k = curve != null ? curve(u) : Easing.Evaluate(ease, u);
            apply(u, k);
        }
    }

    /// <summary>Factories of the common tweens. Every "to" tween reads its start value when it starts (sequence-safe).</summary>
    public static class Tweens
    {
        /// <summary>Any float from `from` to `to`.</summary>
        public static Tween Value(float from, float to, float ms, Action<float> set, EaseType ease = EaseType.Linear)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            return new Tween(ms, (u, k) => set(u >= 1f ? to : from + (to - from) * k), null, ease);
        }

        /// <summary>
        /// Position to `to` (design px). `arcPx` > 0 lifts the path into a parabola peaking `arcPx` above the straight line
        /// at mid-time (JUICE #8 cancel arc, #13 bounce 1,5 cells, #107 Söküm; web motion.ts `Track` arc on linear time).
        /// </summary>
        public static Tween Move(ITweenTarget target, Vec2 to, float ms, EaseType ease = EaseType.OutQuad, float arcPx = 0f)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var from = Vec2.Zero;
            return new Tween(ms, (u, k) =>
            {
                if (u >= 1f)
                {
                    target.Position = to;
                    return;
                }
                var p = Vec2.Lerp(from, to, k);
                if (arcPx != 0f) p = new Vec2(p.X, p.Y - arcPx * 4f * u * (1f - u));
                target.Position = p;
            }, () => from = target.Position, ease);
        }

        /// <summary>Scale to `to`.</summary>
        public static Tween Scale(ITweenTarget target, Vec2 to, float ms, EaseType ease = EaseType.OutQuad)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var from = Vec2.One;
            return new Tween(ms, (u, k) => target.Scale = u >= 1f ? to : Vec2.Lerp(from, to, k), () => from = target.Scale, ease);
        }

        /// <summary>Uniform scale to `to`.</summary>
        public static Tween Scale(ITweenTarget target, float to, float ms, EaseType ease = EaseType.OutQuad) =>
            Scale(target, new Vec2(to, to), ms, ease);

        /// <summary>
        /// Squash and recover (JUICE #11 landing x 1,12 / y 0,86 → 1, #9 yard drop y 0,94): the scale jumps to
        /// `(sx, sy) × rest` at the start and eases back to `rest` (the scale at the start).
        /// </summary>
        public static Tween Squash(ITweenTarget target, float sx, float sy, float ms, EaseType ease = EaseType.OutBack)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var rest = Vec2.One;
            Vec2 squashed = Vec2.One;
            return new Tween(ms, (u, k) =>
            {
                target.Scale = u >= 1f ? rest : Vec2.Lerp(squashed, rest, k);
            }, () =>
            {
                rest = target.Scale;
                squashed = new Vec2(rest.X * sx, rest.Y * sy);
                target.Scale = squashed;
            }, ease);
        }

        /// <summary>
        /// Scale pulses `rest → rest·peak → rest`, `count` times over `ms` each (K-34 hook 5 stuck pulse 1,06 × 2; JUICE #20
        /// chip 1,0 → 1,15 → 1,0; the curve is sin(π·k) per pulse, like the web `pulse01`).
        /// </summary>
        public static Tween Pulse(ITweenTarget target, float peak, float ms, int count = 1, EaseType ease = EaseType.Linear)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "Pulse: count must be ≥ 1");
            var rest = Vec2.One;
            return new Tween(ms * count, (u, k) =>
            {
                if (u >= 1f)
                {
                    target.Scale = rest;
                    return;
                }
                float f = k * count;
                f -= (float)Math.Floor(f);
                float s = 1f + (peak - 1f) * (float)Math.Sin(Math.PI * f);
                target.Scale = new Vec2(rest.X * s, rest.Y * s);
            }, () => rest = target.Scale, ease);
        }

        /// <summary>
        /// Scale `from → peak → to` with the peak at `split` of the time (web `popCurve`: JUICE #15 bead 0,6 → 1,2 → 1,0, #16
        /// 1,0 → 1,4 → 1,1, #55 title 0 → 1,1 → 1,0). Uniform; independent of the start scale.
        /// </summary>
        public static Tween Pop(ITweenTarget target, float from, float peak, float to, float ms, float split = 0.5f, EaseType ease = EaseType.Linear)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return new Tween(ms, (u, k) =>
            {
                float s = u >= 1f ? to : PopCurve(k, from, peak, to, split);
                target.Scale = new Vec2(s, s);
            }, null, ease);
        }

        /// <summary>Web `popCurve(u, from, peak, rest, split)`: linear from → peak until `split`, then peak → rest.</summary>
        public static float PopCurve(float u, float from, float peak, float rest, float split = 0.5f)
        {
            if (u <= 0f) return from;
            if (u >= 1f) return rest;
            if (u < split) return from + (peak - from) * (u / split);
            return peak + (rest - peak) * ((u - split) / (1f - split));
        }

        /// <summary>Rotation to `toDeg` (degrees, counter-clockwise).</summary>
        public static Tween Rotate(ITweenTarget target, float toDeg, float ms, EaseType ease = EaseType.OutQuad)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            float from = 0f;
            return new Tween(ms, (u, k) => target.Rotation = u >= 1f ? toDeg : from + (toDeg - from) * k, () => from = target.Rotation, ease);
        }

        /// <summary>Alpha to `to`.</summary>
        public static Tween Fade(ITweenTarget target, float to, float ms, EaseType ease = EaseType.Linear)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            float from = 1f;
            return new Tween(ms, (u, k) => target.Alpha = u >= 1f ? to : from + (to - from) * k, () => from = target.Alpha, ease);
        }

        /// <summary>
        /// Side-to-side shake around the start position: offset = amplitude · sin(2π · cycles · k) (web PieceView
        /// `startShake`; JUICE #2 / #13: 2 px × 3 cycles). Ends exactly on the start position.
        /// </summary>
        public static Tween Shake(ITweenTarget target, Vec2 amplitude, int cycles, float ms, EaseType ease = EaseType.Linear)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var home = Vec2.Zero;
            return new Tween(ms, (u, k) =>
            {
                if (u >= 1f)
                {
                    target.Position = home;
                    return;
                }
                float s = (float)Math.Sin(2 * Math.PI * cycles * k);
                target.Position = home + amplitude * s;
            }, () => home = target.Position, ease);
        }

        /// <summary>
        /// A short push toward `offset` and back (JUICE #4 sticky bump 6 px, 120 ms Back.easeOut): position = start +
        /// offset · (1 − k), starting at the full offset.
        /// </summary>
        public static Tween Nudge(ITweenTarget target, Vec2 offset, float ms, EaseType ease = EaseType.OutBack)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var home = Vec2.Zero;
            return new Tween(ms, (u, k) => target.Position = u >= 1f ? home : home + offset * (1f - k), () =>
            {
                home = target.Position;
                target.Position = home + offset;
            }, ease);
        }

        /// <summary>Calls `action` with no duration (a zero-length tween; in a sequence prefer `AppendCallback`).</summary>
        public static Tween Call(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return new Tween(0f, (u, k) => { }, action);
        }
    }
}
