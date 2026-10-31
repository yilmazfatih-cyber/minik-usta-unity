#nullable enable
// Built-in tween engine (D-096): the base of Tween and Sequence. Time is in milliseconds (JUICE.md and tokens.json
// `duration.*` are ms) and only moves forward; nothing reads a real clock — `TweenClock.Advance(dt)` drives everything,
// so the same dt sequence gives the same frames (deterministic, testable without Unity).
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Tweening
{
    /// <summary>
    /// Something that plays over time: a `Tween` (one value) or a `Sequence` (children and callbacks on a timeline).
    /// Lifecycle: waiting (local time &lt; `Delay`) → started (`OnStart` once, start values captured) → complete
    /// (`OnComplete` once, the final value applied exactly). `Kill` stops it where it is, without completing.
    /// </summary>
    public abstract class Tweenable
    {
        private float local = -1f;
        private Action? onStart;
        private Action? onUpdate;
        private Action? onComplete;

        /// <summary>Wait before the motion starts (ms); part of `Span`.</summary>
        public float Delay { get; private set; }

        /// <summary>Length of the motion itself (ms), delay excluded.</summary>
        public abstract float Duration { get; }

        /// <summary>`Delay + Duration`: the time a sequence reserves for this item.</summary>
        public float Span => Delay + Duration;

        /// <summary>Local time reached (ms since the tweenable began, delay included); 0 before the first step.</summary>
        public float Elapsed => local < 0 ? 0 : local;

        public bool IsStarted { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsKilled { get; private set; }

        /// <summary>Running = neither complete nor killed.</summary>
        public bool IsActive => !IsComplete && !IsKilled;

        /// <summary>A free tag for `TweenClock.Complete(tag)` / `Kill(tag)` (e.g. "board motions" for the R-12 fast-forward).</summary>
        public int Tag { get; set; }

        /// <summary>Delay before the start; only before the tweenable started.</summary>
        public Tweenable SetDelay(float ms)
        {
            AssertNotStarted("SetDelay");
            if (!(ms >= 0)) throw new ArgumentOutOfRangeException(nameof(ms), "SetDelay: the delay must be ≥ 0");
            Delay = ms;
            return this;
        }

        public Tweenable SetTag(int tag)
        {
            Tag = tag;
            return this;
        }

        /// <summary>Adds a start callback (after the start values are captured, before the first frame is applied).</summary>
        public Tweenable OnStart(Action action)
        {
            onStart += action;
            return this;
        }

        /// <summary>Adds a frame callback: after every applied frame, the final one included (before `OnComplete`).</summary>
        public Tweenable OnUpdate(Action action)
        {
            onUpdate += action;
            return this;
        }

        /// <summary>Adds a completion callback (after the final value).</summary>
        public Tweenable OnComplete(Action action)
        {
            onComplete += action;
            return this;
        }

        /// <summary>Jumps to the end: every remaining frame is skipped, start / completion callbacks run in order.</summary>
        public void Complete()
        {
            if (!IsActive) return;
            Goto(Math.Max(Elapsed, Span));
        }

        /// <summary>Stops without completing (no further frames, no `OnComplete`).</summary>
        public void Kill() => IsKilled = true;

        /// <summary>
        /// Moves the local time to `time` (ms since this tweenable began, delay included). Time only moves forward: an
        /// earlier time is ignored. Called by `TweenClock` and by the parent `Sequence`.
        /// </summary>
        public void Goto(float time)
        {
            if (!IsActive) return;
            if (float.IsNaN(time)) throw new ArgumentException("Goto: time is NaN", nameof(time));
            if (time < local) return;
            local = time;
            float active = time - Delay;
            if (active < 0) return;
            if (!IsStarted)
            {
                IsStarted = true;
                Begin();
                onStart?.Invoke();
                if (!IsActive) return;
            }
            float d = Duration;
            Apply(active < d ? active : d);
            if (!IsActive) return;
            onUpdate?.Invoke();
            if (!IsActive) return;
            if (active >= d)
            {
                IsComplete = true;
                onComplete?.Invoke();
            }
        }

        /// <summary>Captures the start values (a `To` tween reads its target here, not at creation).</summary>
        protected abstract void Begin();

        /// <summary>Applies the frame at `active` ms into the motion (0 … Duration; Duration = the final frame).</summary>
        protected abstract void Apply(float active);

        /// <summary>
        /// Adds this item's time boundaries (start, end of delay, end), shifted by `offset`, to `set`. A sequence adds its
        /// children's boundaries too, so a parent splits a long step at every descendant boundary (time-ordered callbacks).
        /// </summary>
        internal virtual void CollectBoundaries(SortedSet<float> set, float offset)
        {
            set.Add(offset);
            set.Add(offset + Delay);
            set.Add(offset + Span);
        }

        protected void AssertNotStarted(string what)
        {
            if (IsStarted || local >= 0) throw new InvalidOperationException(what + ": the tween already started");
        }
    }
}
