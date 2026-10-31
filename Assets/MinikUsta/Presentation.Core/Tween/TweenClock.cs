#nullable enable
// The driver of the built-in tween (D-096). The Unity layer calls `Advance(Time.deltaTime * 1000)` once per frame; tests
// call it with fixed steps. No real time is read here, so a run is reproducible frame by frame.
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Tweening
{
    public sealed class TweenClock
    {
        private struct Entry
        {
            public Tweenable T;
            public float Start;
        }

        private readonly List<Entry> active = new List<Entry>();
        private readonly List<Entry> added = new List<Entry>();
        private bool advancing;
        private float timeScale = 1f;

        /// <summary>Clock time (ms): the sum of `Advance` steps × `TimeScale`.</summary>
        public float Now { get; private set; }

        /// <summary>
        /// Multiplies every step (JUICE §0 rule 3: a touch during a locked sequence plays it `FeelSettings.LockSpeedUp` = 3×
        /// faster; the `AnimationQueue` gives the rate). Must be ≥ 0.
        /// </summary>
        public float TimeScale
        {
            get => timeScale;
            set
            {
                if (!(value >= 0)) throw new ArgumentOutOfRangeException(nameof(value), "TimeScale must be ≥ 0");
                timeScale = value;
            }
        }

        /// <summary>Running tweenables (incomplete, not killed).</summary>
        public int Count
        {
            get
            {
                int n = 0;
                foreach (var e in active) if (e.T.IsActive) n++;
                foreach (var e in added) if (e.T.IsActive) n++;
                return n;
            }
        }

        /// <summary>
        /// Starts `t` now. `elapsedMs` > 0 starts it that far in (a step scheduled at an earlier time of this frame). The
        /// first frame is applied at once: start values are captured and a zero-length tween completes inside `Play`.
        /// </summary>
        public T Play<T>(T t, float elapsedMs = 0f) where T : Tweenable
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (!(elapsedMs >= 0)) throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Play: elapsedMs must be ≥ 0");
            if (t.IsStarted) throw new InvalidOperationException("Play: the tween already started");
            var e = new Entry { T = t, Start = Now - elapsedMs };
            t.Goto(elapsedMs);
            if (!t.IsActive) return t;
            if (advancing) added.Add(e);
            else active.Add(e);
            return t;
        }

        /// <summary>Moves the clock by `dtMs` × `TimeScale` and steps every running tweenable, in the order they were played.</summary>
        public void Advance(float dtMs)
        {
            if (!(dtMs >= 0)) throw new ArgumentOutOfRangeException(nameof(dtMs), "Advance: dt must be ≥ 0");
            Now += dtMs * timeScale;
            advancing = true;
            try
            {
                for (int i = 0; i < active.Count; i++)
                {
                    var e = active[i];
                    if (e.T.IsActive) e.T.Goto(Now - e.Start);
                }
            }
            finally
            {
                advancing = false;
                Prune();
            }
        }

        /// <summary>
        /// Jumps every running tweenable with `tag` (every one when null) to its end, in play order; callbacks run, and tweens
        /// they start are completed too (R-12 fast-forward of board motions, debug "skip").
        /// </summary>
        public void Complete(int? tag = null)
        {
            for (int guard = 0; guard < 1000; guard++)
            {
                bool any = false;
                advancing = true;
                try
                {
                    for (int i = 0; i < active.Count; i++)
                    {
                        var t = active[i].T;
                        if (!t.IsActive || (tag.HasValue && t.Tag != tag.Value)) continue;
                        t.Complete();
                        any = true;
                    }
                }
                finally
                {
                    advancing = false;
                    Prune();
                }
                if (!any) return;
            }
            throw new InvalidOperationException("TweenClock.Complete: callbacks keep starting tweens (1000 rounds)");
        }

        /// <summary>Stops every running tweenable with `tag` (every one when null) without completing it.</summary>
        public void Kill(int? tag = null)
        {
            foreach (var e in active) if (!tag.HasValue || e.T.Tag == tag.Value) e.T.Kill();
            foreach (var e in added) if (!tag.HasValue || e.T.Tag == tag.Value) e.T.Kill();
            Prune();
        }

        /// <summary>True while a tweenable with `tag` runs.</summary>
        public bool IsRunning(int tag)
        {
            foreach (var e in active) if (e.T.IsActive && e.T.Tag == tag) return true;
            foreach (var e in added) if (e.T.IsActive && e.T.Tag == tag) return true;
            return false;
        }

        private void Prune()
        {
            active.RemoveAll(e => !e.T.IsActive);
            if (added.Count > 0)
            {
                foreach (var e in added) if (e.T.IsActive) active.Add(e);
                added.Clear();
            }
        }
    }
}
