#nullable enable
// Step playback (port of web tests/scenes/juice.test.ts "input during animations"; JUICE §0 rule 3, R-12, D-021) and an
// end-to-end run of a real plan through the queue and the tween clock, as the Unity layer will drive them.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Tweening;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class AnimationQueueTests
    {
        private static AnimationPlan PlanOf(params (StepKind Kind, float At, float Ms, bool Lock)[] steps) =>
            AnimationPlan.FromSteps(steps.Select(s => AnimationStep.Create(s.Kind, s.At, s.Ms, lockInput: s.Lock)).ToArray());

        [Test]
        public void R12_a_grab_during_the_landing_completes_board_steps_first_and_the_next_locked_sequence_starts_at_once()
        {
            var ran = new List<(StepKind Kind, float Time, bool Instant)>();
            var q = new AnimationQueue((s, time, instant) => ran.Add((s.Kind, time, instant)));
            q.Schedule(PlanOf((StepKind.SiteFall, 0, 400, false), (StepKind.Landing, 400, 120, false), (StepKind.PlaceCorrect, 400, 200, false),
                (StepKind.SegmentDone, 700, 600, true), (StepKind.End, 1300, 0, false)), 1000);
            q.Advance(1000);
            CollectionAssert.AreEqual(new[] { StepKind.SiteFall }, ran.Select(r => r.Kind));
            Assert.IsFalse(q.Locked(1100));
            q.FastForward(1100); // the player grabs during the fall
            CollectionAssert.AreEqual(new[] { (StepKind.SiteFall, false), (StepKind.Landing, true), (StepKind.PlaceCorrect, true) }, ran.Select(r => (r.Kind, r.Instant)));
            Assert.IsTrue(q.Locked(1100), "the segment slide starts now and locks the board");
            q.Advance(1100);
            Assert.AreEqual((StepKind.SegmentDone, 1100f, false), ran.Last());
            q.FastForward(1200); // locked: nothing is skipped
            Assert.AreEqual(4, ran.Count);
            q.Advance(1700);
            Assert.AreEqual(StepKind.End, ran.Last().Kind);
            Assert.IsFalse(q.Locked(1700));
            Assert.AreEqual(0, q.Pending);
        }

        [Test]
        public void JUICE_rule3_a_touch_during_a_locked_sequence_plays_it_3x_faster_then_the_clock_is_back_to_1x()
        {
            var q = new AnimationQueue((s, t, i) => { });
            q.Schedule(PlanOf((StepKind.TruckDelivery, 0, 700, true)), 0);
            q.Advance(0);
            Assert.AreEqual(1f, q.Rate(10));
            q.SpeedUp(100);
            Assert.AreEqual(3f, q.Rate(100));
            Assert.AreEqual(1f, q.Rate(800));
            q.SpeedUp(900); // not locked: no effect
            Assert.AreEqual(1f, q.Rate(900));
            Assert.AreEqual(700f, q.LockEnd(300), "the truck window ends at 700");
        }

        [Test]
        public void Flush_runs_everything_instantly_and_Clear_drops_everything()
        {
            var ran = new List<(StepKind, bool)>();
            var q = new AnimationQueue((s, t, i) => ran.Add((s.Kind, i)));
            q.Schedule(PlanOf((StepKind.SiteFall, 0, 400, false), (StepKind.SegmentDone, 400, 600, true), (StepKind.End, 1000, 0, false)), 0);
            Assert.AreEqual(1000f, q.LastStart(0));
            Assert.IsTrue(q.Busy);
            q.Flush(10);
            CollectionAssert.AreEqual(new[] { (StepKind.SiteFall, true), (StepKind.SegmentDone, true), (StepKind.End, true) }, ran);
            Assert.IsFalse(q.Locked(20));
            q.Schedule(PlanOf((StepKind.SiteFall, 0, 400, false)), 0);
            q.Clear();
            Assert.AreEqual(0, q.Pending);
        }

        [Test]
        public void K35_a_real_plan_runs_through_the_queue_and_the_tween_clock_in_time_order_and_ends_on_time()
        {
            // the last move of level 1: fall → landing / fill → counter → segment (locked) → yard clear → win → bonus → end
            var planned = Play.PlayCrane(1).Last();
            var plan = planned.Plan;
            var feel = new FeelSettings();
            var clock = new TweenClock();
            var view = new FakeTarget { Position = new Vec2(810, 288) };
            var ran = new List<(AnimationStep Step, float Time)>();
            const int BoardTag = 1;
            var queue = new AnimationQueue((step, time, instant) =>
            {
                ran.Add((step, time));
                if (step.Ms <= 0) return;
                // a stand-in runner: one tween per step on a fake view, started at its scheduled time
                Tweenable tw = step.Kind == StepKind.SiteFall
                    ? Tweens.Move(view, new Vec2(810, 1368), step.Ms).SetCurve(FallMotion.ReleaseFallCurve(((PieceFellEvent)step.Event!).Rows, "normal", feel))
                    : Tweens.Value(0, 1, step.Ms, _ => { }, step.Ease);
                if (step.FastForward) tw.SetTag(BoardTag);
                clock.Play(tw, Math.Max(0, clock.Now - time));
                if (instant) tw.Complete();
            }, feel.LockSpeedUp);
            queue.Schedule(plan, clock.Now);
            int frames = 0;
            bool sawLock = false;
            while (queue.Busy || clock.Count > 0)
            {
                float rate = queue.Rate(clock.Now);
                clock.TimeScale = rate;
                clock.Advance(16);
                queue.Advance(clock.Now);
                sawLock |= queue.Locked(clock.Now);
                if (++frames > 2000) Assert.Fail("the plan never ended");
            }
            Assert.IsTrue(sawLock, "the segment slide / win locked the board");
            CollectionAssert.AreEqual(plan.Steps.OrderBy(s => s.At).ThenBy(s => Array.IndexOf(plan.Steps, s)).Select(s => s.Kind), ran.Select(r => r.Step.Kind), "steps ran in time order");
            foreach (var (step, time) in ran) Assert.AreEqual(step.At, time, 1e-3, step + " ran at its time");
            Assert.AreEqual(new Vec2(810, 1368), view.Position, "the fall landed exactly");
            Assert.AreEqual(plan.Ms, ran.Last().Time, 1e-3);
            Assert.LessOrEqual(frames * 16f - plan.Ms, 16f * 2, "no frame wasted after the end (beyond the last tween)");
        }
    }
}
