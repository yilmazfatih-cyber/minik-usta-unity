#nullable enable
// Ease functions of the built-in tween (D-096): end points, the Phaser formulas the web feel was tuned with
// (node_modules/phaser 4.2.1 src/math/easing), overshoot of Back / Elastic, and the Phaser names of tokens.json.
using System;
using System.Linq;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Tweening;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class EasingTests
    {
        [Test]
        public void Easing_every_ease_is_exactly_0_at_the_start_and_1_at_the_end([ValueSource(typeof(Easing), nameof(Easing.All))] EaseType ease)
        {
            Assert.AreEqual(0.0, Easing.Evaluate(ease, 0.0), ease + "(0)");
            Assert.AreEqual(1.0, Easing.Evaluate(ease, 1.0), ease + "(1)");
            Assert.AreEqual(0.0, Easing.Evaluate(ease, -0.5), ease + "(−0.5) clamps to 0");
            Assert.AreEqual(1.0, Easing.Evaluate(ease, 1.5), ease + "(1.5) clamps to 1");
            Assert.AreEqual(0.0, Easing.Evaluate(ease, double.NaN), ease + "(NaN) → 0");
            Assert.AreEqual(0f, Easing.Evaluate(ease, 0f));
            Assert.AreEqual(1f, Easing.Evaluate(ease, 1f));
        }

        [Test]
        public void Easing_values_match_the_Phaser_formulas_at_sample_points()
        {
            const double tol = 1e-12;
            Assert.AreEqual(0.5, Easing.Evaluate(EaseType.Linear, 0.5), tol);
            Assert.AreEqual(0.25, Easing.Evaluate(EaseType.InQuad, 0.5), tol);
            Assert.AreEqual(0.75, Easing.Evaluate(EaseType.OutQuad, 0.5), tol);
            Assert.AreEqual(0.125, Easing.Evaluate(EaseType.InOutQuad, 0.25), tol);
            Assert.AreEqual(0.875, Easing.Evaluate(EaseType.InOutQuad, 0.75), tol);
            Assert.AreEqual(0.125, Easing.Evaluate(EaseType.InCubic, 0.5), tol);
            Assert.AreEqual(0.875, Easing.Evaluate(EaseType.OutCubic, 0.5), tol);
            Assert.AreEqual(0.0625, Easing.Evaluate(EaseType.InOutCubic, 0.25), tol);
            // Back.easeOut(0.5) = (−0.5)²·(2.70158·(−0.5) + 1.70158) + 1
            Assert.AreEqual(0.25 * (2.70158 * -0.5 + 1.70158) + 1, Easing.Evaluate(EaseType.OutBack, 0.5), tol);
            Assert.AreEqual(0.25 * (2.70158 * 0.5 - 1.70158), Easing.Evaluate(EaseType.InBack, 0.5), tol);
            // Bounce.easeOut: the four parabolas
            Assert.AreEqual(7.5625 * 0.25 * 0.25, Easing.Evaluate(EaseType.OutBounce, 0.25), tol);
            Assert.AreEqual(7.5625 * Math.Pow(0.5 - 1.5 / 2.75, 2) + 0.75, Easing.Evaluate(EaseType.OutBounce, 0.5), tol);
            Assert.AreEqual(7.5625 * Math.Pow(0.8 - 2.25 / 2.75, 2) + 0.9375, Easing.Evaluate(EaseType.OutBounce, 0.8), tol);
            Assert.AreEqual(7.5625 * Math.Pow(0.95 - 2.625 / 2.75, 2) + 0.984375, Easing.Evaluate(EaseType.OutBounce, 0.95), tol);
            // Elastic.easeOut with Phaser's defaults (amplitude 0.1 → 1, period 0.1, s = 0.025)
            Assert.AreEqual(Math.Pow(2, -5) * Math.Sin((0.5 - 0.025) * 2 * Math.PI / 0.1) + 1, Easing.Evaluate(EaseType.OutElastic, 0.5), tol);
            Assert.AreEqual(1 - Math.Cos(0.5 * Math.PI / 2), Easing.Evaluate(EaseType.InSine, 0.5), tol);
            Assert.AreEqual(Math.Sin(0.5 * Math.PI / 2), Easing.Evaluate(EaseType.OutSine, 0.5), tol);
            Assert.AreEqual(0.5 * (1 - Math.Cos(Math.PI * 0.25)), Easing.Evaluate(EaseType.InOutSine, 0.25), tol);
            Assert.AreEqual(0.5, Easing.Evaluate(EaseType.InOutSine, 0.5), tol);
            Assert.AreEqual(1 - Math.Pow(2, -5), Easing.Evaluate(EaseType.OutExpo, 0.5), tol);
            Assert.AreEqual(Math.Pow(2, -5) - 0.001, Easing.Evaluate(EaseType.InExpo, 0.5), tol);
        }

        [Test]
        public void Easing_OutBack_overshoots_by_about_10_percent_with_overshoot_1_70158_and_settles_on_1()
        {
            Assert.AreEqual(1.70158, Easing.BackOvershoot);
            double peak = 0;
            for (int i = 0; i <= 10000; i++) peak = Math.Max(peak, Easing.Evaluate(EaseType.OutBack, i / 10000.0));
            Assert.AreEqual(1.0998, peak, 1e-3, "Back.easeOut peak (Penner: 10 % overshoot)");
            Assert.Greater(Easing.Evaluate(EaseType.OutBack, 0.9), 1.0, "still above 1 near the end (the landing bounce)");
        }

        [Test]
        public void Easing_OutElastic_oscillates_around_1_and_OutBounce_never_leaves_0_to_1()
        {
            bool above = false, below = false;
            for (int i = 1; i < 1000; i++)
            {
                double u = i / 1000.0;
                double e = Easing.Evaluate(EaseType.OutElastic, u);
                if (u > 0.1 && e > 1) above = true;
                if (u > 0.1 && e < 1) below = true;
                double b = Easing.Evaluate(EaseType.OutBounce, u);
                Assert.That(b, Is.InRange(0.0, 1.0), "Bounce.easeOut(" + u + ")");
            }
            Assert.IsTrue(above && below, "Elastic.easeOut overshoots and undershoots 1");
        }

        [Test]
        public void Easing_monotonic_eases_never_go_back()
        {
            var monotonic = new[]
            {
                EaseType.Linear, EaseType.InQuad, EaseType.OutQuad, EaseType.InOutQuad, EaseType.InCubic, EaseType.OutCubic, EaseType.InOutCubic,
                EaseType.InSine, EaseType.OutSine, EaseType.InOutSine, EaseType.OutExpo,
            };
            foreach (var ease in monotonic)
            {
                double prev = 0;
                for (int i = 0; i <= 200; i++)
                {
                    double v = Easing.Evaluate(ease, i / 200.0);
                    Assert.GreaterOrEqual(v, prev - 1e-15, ease + " at " + i / 200.0);
                    prev = v;
                }
            }
        }

        [Test]
        public void Easing_Phaser_names_round_trip_and_every_tokens_json_easing_is_known()
        {
            foreach (var ease in Easing.All) Assert.AreEqual(ease, Easing.FromPhaserName(Easing.PhaserName(ease)), Easing.PhaserName(ease));
            var easings = PresentationPaths.Token("easing");
            Assert.Greater(easings.Members.Count(), 5);
            foreach (var kv in easings.Members)
                Assert.IsTrue(Easing.TryFromPhaserName(kv.Value.AsString, out _), "tokens.json easing." + kv.Key + " = " + kv.Value.AsString);
            Assert.AreEqual(EaseType.Linear, Easing.FromPhaserName("Power0"));
            Assert.IsFalse(Easing.TryFromPhaserName("Wobble.easeOut", out var unknown));
            Assert.AreEqual(EaseType.Linear, unknown);
            Assert.AreEqual(EaseType.Linear, Easing.FromPhaserName(null), "unknown → Linear like Phaser's GetEaseFunction");
        }
    }
}
