#nullable enable
// Fall timing from the physics tokens (JUICE §0.1; port of web tests/scenes/motion.test.ts) and the stuck pulse
// (GDD K-34 hook 5; port of web stuckPulse.ts behaviour).
using System;
using MinikUsta.Presentation.Feel;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class FallAndHintTests
    {
        private static readonly FeelSettings F = new FeelSettings();

        [Test]
        public void JUICE_0_1_normal_gravity_1_cell_about_150_ms_and_6_cells_about_395_ms()
        {
            Assert.AreEqual(149, FallMotion.DurationMs(1, F.FallNormalAccel, F.FallNormalMax), 0.5);
            Assert.AreEqual(395, FallMotion.DurationMs(6, F.FallNormalAccel, F.FallNormalMax), 0.5);
            Assert.AreEqual((float)FallMotion.DurationMs(6, F.FallNormalAccel, F.FallNormalMax), FallMotion.ReleaseFallMs(6, "normal", F), 1e-3);
        }

        [Test]
        public void JUICE_0_1_heavy_gravity_6_cells_about_216_ms_and_low_gravity_glides_at_4_5_cells_per_s()
        {
            Assert.Less(Math.Abs(FallMotion.DurationMs(6, F.FallHighAccel, F.FallHighMax) - 216), 1);
            Assert.AreEqual(222, FallMotion.GlideMs(1, F.FallLowSpeed), 0.5);
            Assert.AreEqual((float)FallMotion.GlideMs(3, F.FallLowSpeed), FallMotion.ReleaseFallMs(3, "low", F), 1e-3);
            Assert.AreEqual((float)FallMotion.DurationMs(3, F.FallHighAccel, F.FallHighMax), FallMotion.ReleaseFallMs(3, "high", F), 1e-3);
        }

        [Test]
        public void JUICE_0_1_a_zero_fall_takes_no_time_and_the_fall_curve_runs_0_to_1_and_never_goes_back()
        {
            Assert.AreEqual(0, FallMotion.DurationMs(0, F.FallNormalAccel, F.FallNormalMax));
            var leg = FallMotion.Leg(7, F.FallNormalAccel, F.FallNormalMax);
            float prev = -1;
            for (int i = 0; i <= 20; i++)
            {
                float k = leg.Progress(i / 20f);
                Assert.GreaterOrEqual(k, prev);
                prev = k;
            }
            Assert.AreEqual(0f, leg.Progress(0));
            Assert.AreEqual(1f, leg.Progress(1));
            Assert.AreEqual(7, FallMotion.DistanceAt(leg.Ms, F.FallNormalAccel, F.FallNormalMax), 1e-3);
            // accelerating: the first half of the time covers less than half of the distance
            Assert.Less(leg.Progress(0.5f), 0.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => FallMotion.DurationMs(1, 0, 10));
        }

        [Test]
        public void K17_drop_rows_above_the_yard_follow_the_level_grid()
        {
            // K-17 step 2 "sahanın üstünden düşürme": from y0 = rows − h down to the target row
            Assert.AreEqual(10 - 2 - 3, FallMotion.DropRows(3, 2, 10));
            Assert.AreEqual(0, FallMotion.DropRows(9, 2, 10));
            Assert.AreEqual(7 - 1 - 0, FallMotion.DropRows(0, 1, 7));
        }

        [Test]
        public void K34_hook5_stuck_pulse_idle_time_is_6000_ms_easy_12000_ms_normal_and_none_on_hard_levels()
        {
            Assert.AreEqual(6000f, StuckPulse.IdleMsFor("easy"));
            Assert.AreEqual(12000f, StuckPulse.IdleMsFor("normal"));
            Assert.IsNull(StuckPulse.IdleMsFor("hard"));
            Assert.IsNull(StuckPulse.IdleMsFor("superhard"));
            Assert.AreEqual(2, StuckPulse.Pulses, "GDD K-34 hook 5: 2 kez nabız atar");
            var hard = new StuckPulse();
            hard.Level("hard", 0);
            Assert.IsFalse(hard.Due(100000, false));
        }

        [Test]
        public void K34_hook5_stuck_pulse_fires_once_per_idle_period_a_touch_restarts_it_quiet_defers_it()
        {
            var p = new StuckPulse();
            p.Level("easy", 1000);
            Assert.IsFalse(p.Due(6999, false), "not before 6000 ms idle");
            Assert.IsTrue(p.Due(7000, false));
            Assert.IsFalse(p.Due(20000, false), "once per idle period");
            p.Touched(20000);
            Assert.IsFalse(p.Due(25999, false));
            Assert.IsFalse(p.Due(26000, true), "quiet (tutorial step, window, drag, animations): deferred …");
            Assert.IsTrue(p.Due(26500, false), "… and shown when allowed again");
            var n = new StuckPulse();
            n.Level("normal", 0);
            Assert.IsFalse(n.Due(11999, false));
            Assert.IsTrue(n.Due(12000, false));
        }
    }
}
