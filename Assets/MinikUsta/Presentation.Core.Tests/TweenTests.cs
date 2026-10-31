#nullable enable
// The built-in tween engine (D-096): Tween, the target factories, Sequence (Append / Join / Insert / AppendInterval /
// AppendCallback) and TweenClock (Advance, TimeScale, Complete / Kill by tag). Everything is driven by explicit dt steps:
// the same dt sequence gives the same frames.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Tweening;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class TweenTests
    {
        private static void Step(TweenClock clock, float total, float dt)
        {
            for (float t = 0; t < total; t += dt) clock.Advance(dt);
        }

        // --- Tween ---------------------------------------------------------------------------------------------------------

        [Test]
        public void Tween_value_follows_the_ease_and_ends_exactly_on_the_target_with_callbacks_once()
        {
            var clock = new TweenClock();
            var seen = new List<float>();
            int starts = 0, completes = 0;
            int updates = 0;
            var tw = Tweens.Value(0f, 0.3f, 100f, v => seen.Add(v), EaseType.OutQuad);
            tw.OnStart(() => starts++).OnUpdate(() => updates++).OnComplete(() => completes++);
            clock.Play(tw);
            Assert.AreEqual(0f, seen.Last(), "the first frame is applied in Play");
            clock.Advance(50);
            Assert.AreEqual(0.3f * 0.75f, seen.Last(), 1e-6, "OutQuad(0.5) = 0.75");
            clock.Advance(50);
            Assert.AreEqual(0.3f, seen.Last(), "exact target (no float drift)");
            clock.Advance(50);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, completes);
            Assert.IsTrue(tw.IsComplete);
            Assert.AreEqual(0, clock.Count, "a finished tween leaves the clock");
            Assert.AreEqual(3, seen.Count, "no frame after the end");
            Assert.AreEqual(3, updates, "onUpdate after every applied frame, the final one included");
        }

        [Test]
        public void Tween_a_to_tween_reads_its_start_value_when_it_starts_not_when_it_is_created()
        {
            var clock = new TweenClock();
            var target = new FakeTarget { Position = new Vec2(0, 0) };
            var move = Tweens.Move(target, new Vec2(100, 0), 100f, EaseType.Linear);
            move.SetDelay(50);
            target.Position = new Vec2(40, 0); // moved by someone else before the tween starts
            clock.Play(move);
            clock.Advance(50);
            Assert.AreEqual(40f, target.Position.X, 1e-4, "started at the position it found");
            clock.Advance(50);
            Assert.AreEqual(70f, target.Position.X, 1e-4, "halfway from 40 to 100");
            clock.Advance(60);
            Assert.AreEqual(new Vec2(100, 0), target.Position);
        }

        [Test]
        public void Tween_zero_duration_completes_inside_Play_and_a_killed_tween_never_completes()
        {
            var clock = new TweenClock();
            float v = -1;
            int done = 0;
            var zero = Tweens.Value(0, 5, 0, x => v = x);
            zero.OnComplete(() => done++);
            clock.Play(zero);
            Assert.AreEqual(5f, v);
            Assert.AreEqual(1, done);
            Assert.AreEqual(0, clock.Count);

            float w = 0;
            int killedDone = 0;
            var tw = Tweens.Value(0, 10, 100, x => w = x);
            tw.OnComplete(() => killedDone++);
            clock.Play(tw);
            clock.Advance(30);
            tw.Kill();
            clock.Advance(100);
            Assert.AreEqual(3f, w, 1e-4, "frozen where it was killed");
            Assert.AreEqual(0, killedDone);
            Assert.IsFalse(tw.IsComplete);
            Assert.IsTrue(tw.IsKilled);
        }

        [Test]
        public void Tween_arc_move_peaks_at_mid_time_above_the_straight_line_and_lands_on_the_target()
        {
            // JUICE #13 bounce / #8 cancel: the arc is on linear time (web motion.ts Track.at: lift = arc · 4u(1 − u))
            var clock = new TweenClock();
            var target = new FakeTarget { Position = new Vec2(0, 500) };
            clock.Play(Tweens.Move(target, new Vec2(200, 500), 100, EaseType.Linear, arcPx: 180));
            clock.Advance(50);
            Assert.AreEqual(100f, target.Position.X, 1e-4);
            Assert.AreEqual(500f - 180f, target.Position.Y, 1e-3, "peak 180 px up (y down) at mid-time");
            clock.Advance(50);
            Assert.AreEqual(new Vec2(200, 500), target.Position);
        }

        [Test]
        public void Tween_squash_jumps_to_the_squashed_scale_and_eases_back_to_rest_with_the_landing_bounce()
        {
            // JUICE #11 landing: x 1,12 / y 0,86 → 1 / 1 over 120 ms Back.easeOut
            var clock = new TweenClock();
            var target = new FakeTarget();
            var seen = new List<Vec2>();
            var squash = Tweens.Squash(target, 1.12f, 0.86f, 120, EaseType.OutBack);
            clock.Play(squash);
            Assert.AreEqual(1.12f, target.Scale.X, 1e-5);
            Assert.AreEqual(0.86f, target.Scale.Y, 1e-5);
            for (int i = 0; i < 12; i++)
            {
                clock.Advance(10);
                seen.Add(target.Scale);
            }
            Assert.IsTrue(seen.Any(s => s.Y > 1f), "Back.easeOut overshoots past rest (the bounce)");
            Assert.AreEqual(Vec2.One, target.Scale, "ends exactly at rest");
        }

        [Test]
        public void Tween_pulse_pop_shake_and_nudge_return_to_their_rest_values()
        {
            var clock = new TweenClock();
            var t = new FakeTarget { Position = new Vec2(10, 20) };
            // K-34 hook 5 stuck pulse: 2 × (1 → 1,06 → 1) over 600 ms each
            clock.Play(Tweens.Pulse(t, 1.06f, 600, 2));
            clock.Advance(300);
            Assert.AreEqual(1.06f, t.Scale.X, 1e-4, "peak of the first pulse");
            clock.Advance(300);
            Assert.AreEqual(1f, t.Scale.X, 1e-4, "between the pulses");
            clock.Advance(300);
            Assert.AreEqual(1.06f, t.Scale.X, 1e-4, "peak of the second pulse");
            clock.Advance(300);
            Assert.AreEqual(Vec2.One, t.Scale);

            // JUICE #15 bead 0,6 → 1,2 → 1,0 (web popCurve)
            clock.Play(Tweens.Pop(t, 0.6f, 1.2f, 1.0f, 160));
            Assert.AreEqual(0.6f, t.Scale.X, 1e-5);
            clock.Advance(80);
            Assert.AreEqual(1.2f, t.Scale.X, 1e-5);
            clock.Advance(80);
            Assert.AreEqual(1f, t.Scale.X);
            Assert.AreEqual(0.6f + 0.6f * 0.5f, Tweens.PopCurve(0.25f, 0.6f, 1.2f, 1.0f), 1e-6);

            // JUICE #2 / #13 block shake: 2 px × 3 cycles, ends home
            clock.Play(Tweens.Shake(t, new Vec2(2, 0), 3, 120));
            clock.Advance(10); // u = 1/12: sin(2π·3/12) = 1
            Assert.AreEqual(12f, t.Position.X, 1e-4);
            clock.Advance(110);
            Assert.AreEqual(new Vec2(10, 20), t.Position);

            // JUICE #4 sticky bump: 6 px push, back in 120 ms
            clock.Play(Tweens.Nudge(t, new Vec2(6, 0), 120, EaseType.OutBack));
            Assert.AreEqual(16f, t.Position.X, 1e-5);
            clock.Advance(120);
            Assert.AreEqual(new Vec2(10, 20), t.Position);
        }

        [Test]
        public void Tween_rotate_and_fade_reach_their_targets()
        {
            var clock = new TweenClock();
            var t = new FakeTarget { Rotation = 4, Alpha = 1 };
            clock.Play(Tweens.Rotate(t, 0, 120)); // JUICE #3 tilt decays in 120 ms
            clock.Play(Tweens.Fade(t, 0.6f, 80)); // UX §5.3 cancel preview 60 %
            clock.Advance(40);
            Assert.AreEqual(0.8f, t.Alpha, 1e-5);
            clock.Advance(80);
            Assert.AreEqual(0f, t.Rotation);
            Assert.AreEqual(0.6f, t.Alpha);
        }

        // --- Sequence ------------------------------------------------------------------------------------------------------

        [Test]
        public void Sequence_append_plays_children_one_after_another_and_join_plays_with_the_last_appended()
        {
            var clock = new TweenClock();
            var a = new FakeTarget();
            var b = new FakeTarget();
            var seq = new Sequence()
                .Append(Tweens.Move(a, new Vec2(100, 0), 100, EaseType.Linear))
                .Append(Tweens.Move(a, new Vec2(100, 100), 200, EaseType.Linear))
                .Join(Tweens.Fade(b, 0f, 100))
                .AppendInterval(50)
                .Append(Tweens.Scale(b, 2f, 100, EaseType.Linear));
            Assert.AreEqual(100 + 200 + 50 + 100, seq.Duration, "durations add up; the join does not extend");
            clock.Play(seq);
            clock.Advance(100);
            Assert.AreEqual(new Vec2(100, 0), a.Position, "first leg done");
            Assert.AreEqual(1f, b.Alpha, "the join starts with the second leg");
            clock.Advance(50);
            Assert.AreEqual(25f, a.Position.Y, 1e-4, "a quarter of the 200 ms leg");
            Assert.AreEqual(0.5f, b.Alpha, 1e-5, "half of the joined 100 ms fade");
            clock.Advance(150);
            Assert.AreEqual(new Vec2(100, 100), a.Position);
            Assert.AreEqual(0f, b.Alpha);
            clock.Advance(50);
            Assert.AreEqual(1f, b.Scale.X, "interval: nothing yet");
            clock.Advance(50);
            Assert.AreEqual(1.5f, b.Scale.X, 1e-5);
            clock.Advance(50);
            Assert.AreEqual(2f, b.Scale.X);
            Assert.IsTrue(seq.IsComplete);
        }

        [Test]
        public void Sequence_callbacks_starts_and_completions_run_in_time_order_even_in_one_long_step()
        {
            string Run(float dt)
            {
                var log = new List<string>();
                var clock = new TweenClock();
                var seq = new Sequence();
                seq.Append(Tweens.Value(0, 1, 100, _ => { }).OnComplete(() => log.Add("A.end@100")));
                seq.AppendCallback(() => log.Add("cb@100"));
                seq.Append(Tweens.Value(0, 1, 100, _ => { }).OnStart(() => log.Add("B.start@100")).OnComplete(() => log.Add("B.end@200")));
                seq.Insert(30, Tweens.Value(0, 1, 120, _ => { }).OnComplete(() => log.Add("C.end@150")));
                seq.InsertCallback(170, () => log.Add("cb@170"));
                seq.OnComplete(() => log.Add("seq.end@200"));
                clock.Play(seq);
                Step(clock, 400, dt);
                return string.Join(" ", log);
            }

            string fine = Run(1);
            Assert.AreEqual("A.end@100 cb@100 B.start@100 C.end@150 cb@170 B.end@200 seq.end@200", fine);
            Assert.AreEqual(fine, Run(16), "60 fps frames");
            Assert.AreEqual(fine, Run(1000), "one step over everything (fast-forward) keeps the order");
        }

        [Test]
        public void Sequence_nested_sequences_keep_time_order_across_siblings()
        {
            var log = new List<string>();
            var inner1 = new Sequence().InsertCallback(80, () => log.Add("inner1@80"));
            var inner2 = new Sequence().InsertCallback(60, () => log.Add("inner2@60"));
            var outer = new Sequence().Insert(0, inner1).Insert(0, inner2).InsertCallback(70, () => log.Add("outer@70"));
            var clock = new TweenClock();
            clock.Play(outer);
            clock.Advance(500);
            CollectionAssert.AreEqual(new[] { "inner2@60", "outer@70", "inner1@80" }, log);
        }

        [Test]
        public void Sequence_delay_of_a_child_shifts_it_and_a_started_sequence_cannot_change()
        {
            var clock = new TweenClock();
            var t = new FakeTarget();
            var child = Tweens.Fade(t, 0f, 100);
            child.SetDelay(40);
            var seq = new Sequence().Append(child).AppendCallback(() => { });
            Assert.AreEqual(140f, seq.Duration);
            clock.Play(seq);
            clock.Advance(40);
            Assert.AreEqual(1f, t.Alpha);
            clock.Advance(50);
            Assert.AreEqual(0.5f, t.Alpha, 1e-5);
            Assert.Throws<InvalidOperationException>(() => seq.Append(Tweens.Fade(t, 1f, 10)));
            Assert.Throws<InvalidOperationException>(() => child.SetDelay(0));
        }

        // --- TweenClock ----------------------------------------------------------------------------------------------------

        [Test]
        public void TweenClock_is_deterministic_the_same_dt_sequence_gives_the_same_frames()
        {
            float[] Frames()
            {
                var clock = new TweenClock();
                var t = new FakeTarget { Position = new Vec2(30, 1200) };
                var seq = new Sequence()
                    .Append(Tweens.Move(t, new Vec2(30, 1100), 80, EaseType.OutBack))
                    .Append(Tweens.Move(t, new Vec2(800, 600), 300, EaseType.InOutCubic, 90))
                    .Join(Tweens.Scale(t, 1.08f, 80, EaseType.OutBack))
                    .Append(Tweens.Squash(t, 1.12f, 0.86f, 120, EaseType.OutBack));
                clock.Play(seq);
                var o = new List<float>();
                var rng = new Random(7);
                for (int i = 0; i < 60; i++)
                {
                    clock.Advance(8 + rng.Next(0, 17)); // uneven frames, same sequence every run
                    o.Add(t.Position.X);
                    o.Add(t.Position.Y);
                    o.Add(t.Scale.X);
                    o.Add(t.Scale.Y);
                }
                return o.ToArray();
            }

            CollectionAssert.AreEqual(Frames(), Frames());
        }

        [Test]
        public void TweenClock_time_scale_plays_faster_and_play_with_elapsed_starts_in_the_middle()
        {
            var clock = new TweenClock { TimeScale = 3f }; // JUICE §0 rule 3: a touch during a locked sequence → 3×
            float v = 0;
            clock.Play(Tweens.Value(0, 600, 600, x => v = x));
            clock.Advance(100);
            Assert.AreEqual(300f, v, 1e-3);
            Assert.AreEqual(300f, clock.Now, 1e-4);
            clock.TimeScale = 1f;
            float w = 0;
            clock.Play(Tweens.Value(0, 100, 100, x => w = x), 40f); // scheduled 40 ms earlier in this frame
            Assert.AreEqual(40f, w, 1e-4);
            clock.Advance(60);
            Assert.AreEqual(100f, w);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.TimeScale = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
        }

        [Test]
        public void TweenClock_complete_by_tag_fast_forwards_board_motions_only_and_completes_tweens_their_callbacks_start()
        {
            const int Board = 1;
            const int Hud = 2;
            var clock = new TweenClock();
            var block = new FakeTarget();
            var chip = new FakeTarget();
            var landing = new FakeTarget();
            var fall = Tweens.Move(block, new Vec2(0, 900), 400, EaseType.InQuad);
            fall.SetTag(Board);
            fall.OnComplete(() => clock.Play(Tweens.Squash(landing, 1.12f, 0.86f, 120).SetTag(Board)));
            clock.Play(fall);
            clock.Play(Tweens.Scale(chip, 1.15f, 200).SetTag(Hud));
            clock.Advance(50);
            clock.Complete(Board); // R-12: the player grabs during the fall
            Assert.AreEqual(new Vec2(0, 900), block.Position, "the fall jumped to its last frame");
            Assert.AreEqual(Vec2.One, landing.Scale, "the landing squash it started was completed too");
            Assert.IsFalse(clock.IsRunning(Board));
            Assert.IsTrue(clock.IsRunning(Hud), "HUD animations keep their pace (JUICE §0 rule 3)");
            Assert.Less(chip.Scale.X, 1.15f);
            clock.Kill(Hud);
            Assert.AreEqual(0, clock.Count);
        }
    }
}
