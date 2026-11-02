#nullable enable
// Plays one `AnimClip` at a time, deterministically (paket B2): no real time is read — the owner advances it by the frame time
// (`Advance(dtMs)`, the same steps the `TweenClock` gets), so the same steps always show the same drawings and fire the same
// events in the same order, whatever the frame rate (a long frame fires every event it crossed, in order).
//
//   Play(clip, passes, then)  a one-shot plays its timeline once; a loop plays its intro, then its loop range `passes` times
//                             (0 = until `Release`). At the end the player goes on with `then` (the rest of the time carried over,
//                             no frame lost) or holds its last drawing.
//   Release()                 a loop leaves: through its `out` frames (think, point, wave) when it has them; at the end of the
//                             current pass when its timeline returns to its first drawing (talk); else at once (idle, big cheer)
//                             — a hard cut the owner may soften (`Cut`).
//   Reduced                   JUICE §0 rule 8 / ART_3D §7.1 rule 12: the clip's still pose instead of its drawings, time runs
//                             `ReducedRate`× faster (clips are short), no events.
using System;

namespace MinikUsta.Presentation.Anim
{
    public enum AnimPhase
    {
        None,
        Main,
        Out,
        Done,
    }

    public sealed class AnimPlayer
    {
        /// <summary>Events of more than this many timeline steps in one `Advance` are skipped (a resumed tab never fires a storm).</summary>
        public const int MaxEventSteps = 96;

        private AnimClip? clip;
        private AnimClip? next;
        private int nextPasses;
        private int passes;
        private float t;
        private long lastStep = -1;
        private long releaseEnd = long.MaxValue;
        private string? held;

        /// <summary>The clip playing (or holding its last drawing); null before the first `Play`.</summary>
        public AnimClip? Clip => clip;

        public AnimPhase Phase { get; private set; } = AnimPhase.None;

        /// <summary>Called for every event frame reached (not in reduced motion).</summary>
        public Action<AnimEvent>? OnEvent { get; set; }

        /// <summary>The still drawing of a clip in reduced motion (king: `chr_king_still_<pose>`); null = the clip's own drawings.</summary>
        public Func<AnimClip, string?>? StillOf { get; set; }

        public bool Reduced { get; set; }

        /// <summary>Reduced motion: time runs this much faster (clips are short).</summary>
        public float ReducedRate { get; set; } = 2f;

        /// <summary>The last transition was a hard cut (a released loop without `out` that does not end on K0); the owner clears it.</summary>
        public bool Cut { get; set; }

        /// <summary>Clips started since construction (`then` continuations included).</summary>
        public int Started { get; private set; }

        /// <summary>Time (ms) in the current phase.</summary>
        public float Time => t;

        /// <summary>True while a clip is in its timeline or its out frames (a finished one-shot without `then` is not).</summary>
        public bool Playing => Phase == AnimPhase.Main || Phase == AnimPhase.Out;

        /// <summary>The loop is being left (released, or its pass count is set) — the clip will end by itself.</summary>
        public bool Ending => clip != null && (Phase == AnimPhase.Out || (Phase == AnimPhase.Main && MainEnd() != long.MaxValue));

        /// <summary>The clip `Play` will continue with.</summary>
        public AnimClip? Next => next;

        /// <summary>Timeline index now (main phase) or out index (out phase); −1 before any clip.</summary>
        public int Index
        {
            get
            {
                if (clip == null) return -1;
                long n = Step();
                if (Phase == AnimPhase.Out) return (int)Math.Min(n, clip.Out.Length - 1);
                if (Phase == AnimPhase.Main) return TimelineIndex(clip, Math.Min(n, MainEnd() - 1));
                return -1;
            }
        }

        /// <summary>The drawing to show now (null before the first `Play`).</summary>
        public string? Frame
        {
            get
            {
                if (clip == null) return null;
                if (Reduced && StillOf != null)
                {
                    var still = StillOf(clip);
                    if (still != null) return still;
                }
                switch (Phase)
                {
                    case AnimPhase.Main:
                        return clip.Frames[TimelineIndex(clip, Math.Min(Step(), MainEnd() - 1))];
                    case AnimPhase.Out:
                        return clip.Out[(int)Math.Min(Step(), clip.Out.Length - 1)];
                    default:
                        return held;
                }
            }
        }

        /// <summary>Starts `clip` at its first frame; a loop plays `passes` loop passes (0 = until `Release`), then `then` (null = hold).</summary>
        public void Play(AnimClip clip, int passes = 0, AnimClip? then = null, int thenPasses = 0)
        {
            next = then;
            nextPasses = thenPasses;
            Begin(clip ?? throw new ArgumentNullException(nameof(clip)), passes, 0f);
            Run();
        }

        /// <summary>Sets what follows the clip playing now (null = hold its last drawing).</summary>
        public void Then(AnimClip? then, int thenPasses = 0)
        {
            next = then;
            nextPasses = thenPasses;
        }

        /// <summary>A playing loop leaves (see the file header); a one-shot simply finishes.</summary>
        public void Release()
        {
            var c = clip;
            if (c == null || Phase != AnimPhase.Main || !c.Loops) return;
            if (c.Out.Length > 0)
            {
                ToOut(0f);
                Run();
                return;
            }
            long n = Step();
            if (c.EndsOnFirst)
            {
                long k = n <= c.LoopEnd ? 0 : (n - (c.LoopEnd + 1)) / c.LoopSpan + 1;
                releaseEnd = Math.Min(MainEnd(), c.LoopEnd + 1 + k * c.LoopSpan);
                Run();
                return;
            }
            held = c.Frames[TimelineIndex(c, n)];
            Cut = true;
            Finish(0f);
            Run();
        }

        /// <summary>Stops at once and shows nothing new (the owner hides the node).</summary>
        public void Stop()
        {
            if (clip != null) held = Frame;
            next = null;
            Phase = clip == null ? AnimPhase.None : AnimPhase.Done;
        }

        public void Advance(float dtMs)
        {
            if (clip == null || !(dtMs > 0) || Phase == AnimPhase.Done || Phase == AnimPhase.None) return;
            t += Reduced ? dtMs * ReducedRate : dtMs;
            Run();
        }

        // --- internals -------------------------------------------------------------------------------------------------------

        private void Begin(AnimClip c, int p, float startMs)
        {
            clip = c;
            passes = Math.Max(0, p);
            releaseEnd = long.MaxValue;
            t = startMs;
            lastStep = -1;
            held = c.Frames[c.Frames.Length - 1];
            Phase = AnimPhase.Main;
            Started++;
        }

        private long Step() => clip == null ? 0 : (long)Math.Floor(t / clip.FrameMs + 1e-6);

        /// <summary>The absolute frame counter at which the main phase ends (exclusive); MaxValue = open loop.</summary>
        private long MainEnd()
        {
            var c = clip!;
            if (!c.Loops) return c.Length;
            long end = passes > 0 ? c.LoopEnd + 1 + (long)(passes - 1) * c.LoopSpan : long.MaxValue;
            return Math.Min(end, releaseEnd);
        }

        private static int TimelineIndex(AnimClip c, long n)
        {
            if (n < 0) return 0;
            if (!c.Loops || n <= c.LoopEnd) return (int)Math.Min(n, c.Length - 1);
            return c.LoopStart + (int)((n - c.LoopStart) % c.LoopSpan);
        }

        private void Run()
        {
            for (int guard = 0; guard < 16 && clip != null; guard++)
            {
                var c = clip;
                if (Phase == AnimPhase.Main)
                {
                    long n = Step();
                    long end = MainEnd();
                    long last = Math.Min(n, end - 1);
                    Fire(c, last);
                    if (n < end) return;
                    float left = t - end * c.FrameMs;
                    if (c.Loops && c.Out.Length > 0)
                    {
                        ToOut(left);
                        continue;
                    }
                    held = c.Frames[TimelineIndex(c, end - 1)];
                    Finish(left);
                    continue;
                }
                if (Phase == AnimPhase.Out)
                {
                    long n = Step();
                    if (n < c.Out.Length) return;
                    held = c.Out[c.Out.Length - 1];
                    Finish(t - c.Out.Length * c.FrameMs);
                    continue;
                }
                return;
            }
        }

        private void Fire(AnimClip c, long upTo)
        {
            if (upTo <= lastStep) return;
            long from = lastStep + 1;
            if (upTo - from >= MaxEventSteps) from = upTo - MaxEventSteps + 1;
            lastStep = upTo;
            if (Reduced || OnEvent == null || c.Events.Count == 0) return;
            for (long s = from; s <= upTo; s++)
            {
                int i = TimelineIndex(c, s);
                if (c.Events.TryGetValue(i, out var name)) OnEvent(new AnimEvent(c, name, i));
            }
        }

        private void ToOut(float startMs)
        {
            Phase = AnimPhase.Out;
            t = Math.Max(0f, startMs);
            lastStep = -1;
        }

        private void Finish(float leftMs)
        {
            var n = next;
            if (n != null)
            {
                int p = nextPasses;
                next = null;
                nextPasses = 0;
                Begin(n, p, Math.Max(0f, leftMs));
                return;
            }
            Phase = AnimPhase.Done;
        }
    }
}
