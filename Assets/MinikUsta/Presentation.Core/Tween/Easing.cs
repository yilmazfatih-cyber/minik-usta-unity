#nullable enable
// Ease functions of the built-in tween (D-096: no DOTween in Phase 3). The formulas are Phaser 4.2.1's
// (node_modules/phaser/src/math/easing/**), because the web game's feel (docs/JUICE.md, tokens.json `easing.*`) was tuned
// with them; JUICE §0 rule 9 names eases in Phaser notation ("Back.easeOut"), `FromPhaserName` reads those names.
using System;

namespace MinikUsta.Presentation.Tweening
{
    /// <summary>The eases of the game feel (tokens.json `easing.*` + the JUICE.md rows). Linear is the default.</summary>
    public enum EaseType
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InBack,
        OutBack,
        OutBounce,
        OutElastic,
        InSine,
        OutSine,
        InOutSine,
        InExpo,
        OutExpo,
    }

    /// <summary>
    /// `Evaluate(type, u)` maps normalised time u ∈ [0, 1] to progress. Every ease returns exactly 0 at u ≤ 0 and exactly 1
    /// at u ≥ 1 (a tween ends on its target value); between, the Phaser formula — Back and Elastic overshoot.
    /// Deviation from Phaser at the end points only: `Expo.easeOut(1)` is 1 − 2^−10 and `Expo.easeIn(0)` is 2^−10 − 0,001
    /// in Phaser; here they are 1 and 0 (Penner's guard), so a tween never stops 0,1 % short of its target.
    /// </summary>
    public static class Easing
    {
        /// <summary>Phaser `Back` default overshoot (= Penner's 1.70158; 10 % overshoot).</summary>
        public const double BackOvershoot = 1.70158;
        /// <summary>Phaser `Elastic.easeOut` defaults: amplitude 0.1 (raised to 1 by the formula) and period 0.1.</summary>
        public const double ElasticAmplitude = 0.1;
        public const double ElasticPeriod = 0.1;

        public static float Evaluate(EaseType type, float u) => (float)Evaluate(type, (double)u);

        public static double Evaluate(EaseType type, double u)
        {
            if (!(u > 0)) return 0; // also NaN
            if (u >= 1) return 1;
            switch (type)
            {
                case EaseType.Linear: return u;
                case EaseType.InQuad: return u * u;
                case EaseType.OutQuad: return u * (2 - u);
                case EaseType.InOutQuad: return InOutQuad(u);
                case EaseType.InCubic: return u * u * u;
                case EaseType.OutCubic:
                    {
                        double v = u - 1;
                        return v * v * v + 1;
                    }
                case EaseType.InOutCubic: return InOutCubic(u);
                case EaseType.InBack: return InBack(u, BackOvershoot);
                case EaseType.OutBack: return OutBack(u, BackOvershoot);
                case EaseType.OutBounce: return OutBounce(u);
                case EaseType.OutElastic: return OutElastic(u, ElasticAmplitude, ElasticPeriod);
                case EaseType.InSine: return 1 - Math.Cos(u * Math.PI / 2);
                case EaseType.OutSine: return Math.Sin(u * Math.PI / 2);
                case EaseType.InOutSine: return 0.5 * (1 - Math.Cos(Math.PI * u));
                case EaseType.InExpo: return Math.Pow(2, 10 * (u - 1)) - 0.001;
                case EaseType.OutExpo: return 1 - Math.Pow(2, -10 * u);
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "unknown ease");
            }
        }

        /// <summary>Phaser `Back.easeIn(v, overshoot)`: `v·v·((s + 1)·v − s)`.</summary>
        public static double InBack(double v, double overshoot) => v * v * ((overshoot + 1) * v - overshoot);

        /// <summary>Phaser `Back.easeOut(v, overshoot)`: `(v − 1)²·((s + 1)·(v − 1) + s) + 1`.</summary>
        public static double OutBack(double v, double overshoot)
        {
            double w = v - 1;
            return w * w * ((overshoot + 1) * w + overshoot) + 1;
        }

        /// <summary>Phaser `Bounce.easeOut`: four parabolic bounces (7.5625, 2.75).</summary>
        public static double OutBounce(double v)
        {
            if (v < 1 / 2.75) return 7.5625 * v * v;
            if (v < 2 / 2.75)
            {
                v -= 1.5 / 2.75;
                return 7.5625 * v * v + 0.75;
            }
            if (v < 2.5 / 2.75)
            {
                v -= 2.25 / 2.75;
                return 7.5625 * v * v + 0.9375;
            }
            v -= 2.625 / 2.75;
            return 7.5625 * v * v + 0.984375;
        }

        /// <summary>Phaser `Elastic.easeOut(v, amplitude, period)` (amplitude &lt; 1 is raised to 1, s = period / 4).</summary>
        public static double OutElastic(double v, double amplitude, double period)
        {
            if (v == 0) return 0;
            if (v == 1) return 1;
            double s = period / 4;
            if (amplitude < 1) amplitude = 1;
            else s = period * Math.Asin(1 / amplitude) / (2 * Math.PI);
            return amplitude * Math.Pow(2, -10 * v) * Math.Sin((v - s) * (2 * Math.PI) / period) + 1;
        }

        private static double InOutQuad(double v)
        {
            v *= 2;
            if (v < 1) return 0.5 * v * v;
            v -= 1;
            return -0.5 * (v * (v - 2) - 1);
        }

        private static double InOutCubic(double v)
        {
            v *= 2;
            if (v < 1) return 0.5 * v * v * v;
            v -= 2;
            return 0.5 * (v * v * v + 2);
        }

        /// <summary>
        /// The ease of a Phaser ease name (`tokens.easing.*`, JUICE §0 rule 9): "Back.easeOut" → OutBack, "Quad.easeInOut" →
        /// InOutQuad, "Linear" / "Power0" → Linear. Unknown names fall back to Linear like Phaser's `GetEaseFunction`
        /// (web scenes/level/easing.ts); use `TryFromPhaserName` to detect them.
        /// </summary>
        public static EaseType FromPhaserName(string? name) => TryFromPhaserName(name, out var e) ? e : EaseType.Linear;

        public static bool TryFromPhaserName(string? name, out EaseType ease)
        {
            ease = EaseType.Linear;
            if (name == null) return false;
            switch (name)
            {
                case "Linear":
                case "Power0":
                    ease = EaseType.Linear;
                    return true;
                case "Quad.easeIn": ease = EaseType.InQuad; return true;
                case "Quad.easeOut": ease = EaseType.OutQuad; return true;
                case "Quad.easeInOut": ease = EaseType.InOutQuad; return true;
                case "Cubic.easeIn": ease = EaseType.InCubic; return true;
                case "Cubic.easeOut": ease = EaseType.OutCubic; return true;
                case "Cubic.easeInOut": ease = EaseType.InOutCubic; return true;
                case "Back.easeIn": ease = EaseType.InBack; return true;
                case "Back.easeOut": ease = EaseType.OutBack; return true;
                case "Bounce.easeOut": ease = EaseType.OutBounce; return true;
                case "Elastic.easeOut": ease = EaseType.OutElastic; return true;
                case "Sine.easeIn": ease = EaseType.InSine; return true;
                case "Sine.easeOut": ease = EaseType.OutSine; return true;
                case "Sine.easeInOut": ease = EaseType.InOutSine; return true;
                case "Expo.easeIn": ease = EaseType.InExpo; return true;
                case "Expo.easeOut": ease = EaseType.OutExpo; return true;
                default: return false;
            }
        }

        /// <summary>The Phaser name of an ease (inverse of `FromPhaserName`).</summary>
        public static string PhaserName(EaseType ease)
        {
            switch (ease)
            {
                case EaseType.Linear: return "Linear";
                case EaseType.InQuad: return "Quad.easeIn";
                case EaseType.OutQuad: return "Quad.easeOut";
                case EaseType.InOutQuad: return "Quad.easeInOut";
                case EaseType.InCubic: return "Cubic.easeIn";
                case EaseType.OutCubic: return "Cubic.easeOut";
                case EaseType.InOutCubic: return "Cubic.easeInOut";
                case EaseType.InBack: return "Back.easeIn";
                case EaseType.OutBack: return "Back.easeOut";
                case EaseType.OutBounce: return "Bounce.easeOut";
                case EaseType.OutElastic: return "Elastic.easeOut";
                case EaseType.InSine: return "Sine.easeIn";
                case EaseType.OutSine: return "Sine.easeOut";
                case EaseType.InOutSine: return "Sine.easeInOut";
                case EaseType.InExpo: return "Expo.easeIn";
                case EaseType.OutExpo: return "Expo.easeOut";
                default: throw new ArgumentOutOfRangeException(nameof(ease), ease, "unknown ease");
            }
        }

        /// <summary>Every ease, in enum order (tests, debug panel).</summary>
        public static readonly EaseType[] All = (EaseType[])Enum.GetValues(typeof(EaseType));
    }
}
