#nullable enable
// Step playback (port of web src/scenes/level/juice/playback.ts; TECH §6.3 "Animasyon sırasında girdi"; JUICE §0 rules 3,
// 10; D-021 / R-12). Pure: the caller passes its animation time (ms) — in Unity the `TweenClock.Now`, advanced by
// `dt × Rate(now)`.
//
// - `Schedule(plan, now)`: steps carry offsets (`At`) from the start of their action; they run in time order (ties: plan
//   order) through the runner callback `run(step, scheduledTime, instant)`.
// - Lock windows: a `Lock` step (segment slide, truck, truck help, Söküm, level end) closes board input for [t, t + Ms).
// - `FastForward(now)` (a grab, R-12): every step BEFORE the next lock window runs at once with `instant = true` (the runner
//   completes board motions: `TweenClock.Complete(tag)`); the rest of the schedule moves earlier so the lock window starts
//   now. Inside a lock window nothing is skipped: the board stays locked.
// - `SpeedUp(now)`: a touch during a lock window plays it `speedUp`× (3) faster (`Rate`); the rate falls back to 1 when the
//   window ends.
// The game state never waits for this: the core applied the action before the plan was made.
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Animation
{
    public sealed class AnimationQueue
    {
        private sealed class Entry
        {
            public float T;
            public readonly int Seq;
            public readonly AnimationStep Step;

            public Entry(float t, int seq, AnimationStep step)
            {
                T = t;
                Seq = seq;
                Step = step;
            }
        }

        private sealed class Window
        {
            public float From;
            public float To;
        }

        private readonly Action<AnimationStep, float, bool> run;
        private readonly float speedUp;
        private readonly List<Entry> queue = new List<Entry>();
        private readonly List<Window> windows = new List<Window>();
        private int seq;
        private bool spedUp;

        /// <summary>`run(step, time, instant)`: `time` = the step's scheduled (absolute) start.</summary>
        public AnimationQueue(Action<AnimationStep, float, bool> run, float speedUp = 3f)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            if (!(speedUp >= 1)) throw new ArgumentOutOfRangeException(nameof(speedUp), "speedUp must be ≥ 1");
            this.speedUp = speedUp;
        }

        public void Schedule(AnimationPlan plan, float now) => Schedule(plan.Steps, now);

        public void Schedule(IEnumerable<AnimationStep> steps, float now)
        {
            foreach (var step in steps)
            {
                float t = now + step.At;
                queue.Add(new Entry(t, seq++, step));
                if (step.Lock && step.Ms > 0) windows.Add(new Window { From = t, To = t + step.Ms });
            }
            queue.Sort((a, b) => a.T != b.T ? a.T.CompareTo(b.T) : a.Seq.CompareTo(b.Seq));
            windows.Sort((a, b) => a.From.CompareTo(b.From));
        }

        /// <summary>Runs every step due at `now` (steps scheduled by a running step run when due, in the same pass if already due).</summary>
        public void Advance(float now)
        {
            while (queue.Count > 0 && queue[0].T <= now)
            {
                var e = queue[0];
                queue.RemoveAt(0);
                run(e.Step, e.T, false);
            }
            PruneWindows(now);
        }

        /// <summary>True while a lock window holds `now` (board input closed).</summary>
        public bool Locked(float now)
        {
            foreach (var w in windows) if (w.From <= now && now < w.To) return true;
            return false;
        }

        /// <summary>End of the lock window holding `now` (or `now`).</summary>
        public float LockEnd(float now)
        {
            float end = now;
            foreach (var w in windows) if (w.From <= now && now < w.To) end = Math.Max(end, w.To);
            return end;
        }

        /// <summary>R-12: unlocked steps before the next lock window run now (instant); the rest of the schedule moves to `now`.</summary>
        public void FastForward(float now)
        {
            if (Locked(now)) return;
            Window? next = null;
            foreach (var w in windows)
                if (w.From > now)
                {
                    next = w;
                    break;
                }
            float limit = next != null ? next.From : float.PositiveInfinity;
            while (queue.Count > 0 && queue[0].T < limit)
            {
                var e = queue[0];
                queue.RemoveAt(0);
                run(e.Step, Math.Min(e.T, now), true);
            }
            if (next != null)
            {
                float from = next.From;
                float shift = from - now;
                foreach (var e in queue) e.T -= shift;
                foreach (var w in windows)
                {
                    if (w.From >= from)
                    {
                        w.From -= shift;
                        w.To -= shift;
                    }
                }
            }
            PruneWindows(now);
        }

        /// <summary>A touch during a lock window: the rest of it plays faster (JUICE §0 rule 3).</summary>
        public void SpeedUp(float now)
        {
            if (Locked(now)) spedUp = true;
        }

        /// <summary>Clock rate for the frame starting at `now`.</summary>
        public float Rate(float now)
        {
            if (!spedUp) return 1f;
            if (Locked(now)) return speedUp;
            spedUp = false;
            return 1f;
        }

        /// <summary>Runs every remaining step at once (level change, debug "skip").</summary>
        public void Flush(float now)
        {
            while (queue.Count > 0)
            {
                var e = queue[0];
                queue.RemoveAt(0);
                run(e.Step, Math.Min(e.T, now), true);
            }
            windows.Clear();
            spedUp = false;
        }

        /// <summary>Drops everything without running it.</summary>
        public void Clear()
        {
            queue.Clear();
            windows.Clear();
            spedUp = false;
        }

        /// <summary>Steps not run yet.</summary>
        public int Pending => queue.Count;

        /// <summary>Animations are pending (the stuck pulse stays quiet, K-34 hook 5).</summary>
        public bool Busy => queue.Count > 0;

        /// <summary>Time the last scheduled step starts (or `now`).</summary>
        public float LastStart(float now) => queue.Count > 0 ? Math.Max(now, queue[queue.Count - 1].T) : now;

        private void PruneWindows(float now)
        {
            // manual loop: no closure allocation per frame (TECH §10.6)
            for (int i = windows.Count - 1; i >= 0; i--)
                if (windows[i].To <= now) windows.RemoveAt(i);
        }
    }
}
