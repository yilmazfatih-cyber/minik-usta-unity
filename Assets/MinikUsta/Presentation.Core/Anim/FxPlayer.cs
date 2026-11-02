#nullable enable
// The pre-rendered FX clips of fx.json on pooled sprites (paket B2; ART_3D §7.4; JUICE rows #11, #12/#92, #16/#17, #18/#93,
// #55, #56, #61–#63, #96): dust puff, lock shine, segment flare, hammer, impact star, crane claw, paint splash, trowel sparkle,
// coin spin, star burst. One `AnimPlayer` per live effect, advanced by the level's animation clock (the lock speed-up applies,
// like the particles); the sprite is placed by its clip pivot (`Position`), sized by `Scale` (1 = the clip's design size),
// tinted for the white renders (paint splash, confetti). Budget: at most `MaxAlive` live effects (the oldest ends early).
// Reduced motion (JUICE §0 rule 8): heavy effects are not played, the light ones play twice as fast (short).
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Anim
{
    /// <summary>Where an effect is drawn.</summary>
    public enum FxDepth
    {
        /// <summary>Over the board floor and plan, under the blocks (the segment flare behind the site).</summary>
        Under,
        /// <summary>Over the blocks (dust, shine, hammer, claw, splash).</summary>
        Over,
        /// <summary>Over the HUD and the open window (uGUI Fx layer: the win star burst, coins).</summary>
        Ui,
    }

    /// <summary>A pooled FX sprite (Unity: a SpriteRenderer, or a uGUI Image on the Fx layer). `Position` = the clip pivot, design px.</summary>
    public interface IFxSprite : ITweenTarget
    {
        bool Visible { get; set; }
        FxDepth Depth { get; }

        /// <summary>Shows drawing `frame` of a 3D library tinted `tint` (× `Alpha`).</summary>
        void Show(string frame, Rgba tint);
    }

    public sealed class FxPlayer
    {
        public const int MaxAlive = 24;

        private sealed class Live
        {
            public readonly AnimPlayer P = new AnimPlayer();
            public IFxSprite? S;
            public FxDepth Depth;
            public Vec2 At;
            public Func<Vec2>? Follow;
            public float Scale;
            public float Rotation;
            public Rgba Tint;
            public float Delay;
            public float Age;
            public float LifeMs;
            public float FadeMs;
            public int Id;
            public Action<AnimEvent>? OnEvent;
        }

        private readonly Func<FxDepth, IFxSprite> acquire;
        private readonly Action<IFxSprite> release;
        private readonly List<Live> live = new List<Live>();
        private readonly List<Live> incoming = new List<Live>();
        private readonly Stack<Live> free = new Stack<Live>();
        private int nextId = 1;
        private bool advancing;

        public FxPlayer(Func<FxDepth, IFxSprite> acquire, Action<IFxSprite> release)
        {
            this.acquire = acquire ?? throw new ArgumentNullException(nameof(acquire));
            this.release = release ?? throw new ArgumentNullException(nameof(release));
        }

        /// <summary>The clips (null = no fx.json: every `Play` returns −1 and the game keeps its v4 particles).</summary>
        public ArtSet Art { get; set; } = ArtSet.Empty;

        public bool Reduced { get; set; }

        /// <summary>Clips that are not played in reduced motion.</summary>
        public static readonly HashSet<string> Heavy = new HashSet<string>(StringComparer.Ordinal)
        {
            "fx_segment_done", "fx_star_burst", "fx_hammer_smash", "fx_impact_star", "fx_crane_grab", "fx_paint_splash", "fx_trowel_sparkle",
            "fx_confetti3d_rect", "fx_confetti3d_round",
        };

        public int Alive => live.Count + incoming.Count;
        public int Peak { get; private set; }
        /// <summary>Effects started since `Reset` (tests).</summary>
        public int Played { get; private set; }
        /// <summary>Clip names started since `Reset`, in order (tests, debug).</summary>
        public List<string> Log { get; } = new List<string>();

        public bool Has(string clip) => Art.Fx?.Clip(clip) != null;

        /// <summary>
        /// Plays `clip` with its pivot at `at` (or following `follow()` + `at` each frame), `scale` × its design size, after `delayMs`.
        /// A loop plays `passes` passes (0 = until `lifeMs`). Returns an id (−1 = not played: no such clip, reduced + heavy).
        /// </summary>
        public int Play(string clip, Vec2 at, FxDepth depth, float scale = 1f, Rgba? tint = null, float delayMs = 0f, Func<Vec2>? follow = null,
            Action<AnimEvent>? onEvent = null, int passes = 1, float lifeMs = 0f, float rotation = 0f, float fadeMs = 0f)
        {
            var c = Art.Fx?.Clip(clip);
            if (c == null) return -1;
            if (Reduced && Heavy.Contains(clip)) return -1;
            if (!advancing) Budget(MaxAlive - 1);
            var l = free.Count > 0 ? free.Pop() : new Live();
            l.S = null;
            l.Depth = depth;
            l.At = at;
            l.Follow = follow;
            l.Scale = scale;
            l.Rotation = rotation;
            l.Tint = tint ?? Rgba.White;
            l.Delay = Math.Max(0f, delayMs);
            l.Age = 0f;
            l.LifeMs = lifeMs;
            l.FadeMs = fadeMs;
            l.Id = nextId++;
            l.OnEvent = onEvent;
            l.P.Reduced = Reduced;
            l.P.OnEvent = onEvent;
            l.P.Play(c, c.Loops ? (lifeMs > 0 ? 0 : Math.Max(1, passes)) : 0);
            // an effect started by an event of another one (the hammer's impact star) joins after the running frame
            (advancing ? incoming : live).Add(l);
            Played++;
            Log.Add(clip);
            if (Alive > Peak) Peak = Alive;
            if (l.Delay <= 0f) Pose(l);
            return l.Id;
        }

        /// <summary>Ends the effect `id` now (a loop that follows a block which has landed).</summary>
        public void Stop(int id)
        {
            if (advancing) return;
            for (int i = 0; i < live.Count; i++)
                if (live[i].Id == id)
                {
                    End(i);
                    return;
                }
        }

        public void Advance(float dtMs)
        {
            if (live.Count == 0 || !(dtMs > 0)) return;
            advancing = true;
            try
            {
                Step(dtMs);
            }
            finally
            {
                advancing = false;
            }
            if (incoming.Count > 0)
            {
                live.AddRange(incoming);
                incoming.Clear();
                Budget(MaxAlive);
            }
        }

        /// <summary>The oldest effects end until at most `max` are alive.</summary>
        private void Budget(int max)
        {
            while (live.Count > Math.Max(0, max)) End(0);
        }

        private void Step(float dtMs)
        {
            for (int i = 0; i < live.Count;)
            {
                var l = live[i];
                if (l.Delay > 0f)
                {
                    l.Delay -= dtMs;
                    if (l.Delay > 0f)
                    {
                        i++;
                        continue;
                    }
                    float over = -l.Delay;
                    l.Delay = 0f;
                    l.P.Advance(over);
                    l.Age += over;
                }
                else
                {
                    l.P.Advance(dtMs);
                    l.Age += dtMs;
                }
                bool done = !l.P.Playing || (l.LifeMs > 0f && l.Age >= l.LifeMs);
                if (done)
                {
                    End(i);
                    continue;
                }
                Pose(l);
                i++;
            }
        }

        /// <summary>Every effect gone (a new level, a resize).</summary>
        public void Reset()
        {
            live.AddRange(incoming);
            incoming.Clear();
            while (live.Count > 0) End(live.Count - 1);
            Played = 0;
            Peak = 0;
            Log.Clear();
        }

        private void Pose(Live l)
        {
            string? frame = l.P.Frame;
            if (frame == null) return;
            if (l.S == null)
            {
                l.S = acquire(l.Depth);
                l.S.Visible = true;
            }
            var s = l.S;
            s.Position = l.Follow != null ? l.Follow() + l.At : l.At;
            s.Scale = new Vec2(l.Scale, l.Scale);
            s.Rotation = l.Rotation;
            float a = 1f;
            if (l.FadeMs > 0f && l.LifeMs > 0f && l.Age > l.LifeMs - l.FadeMs) a = Math.Max(0f, (l.LifeMs - l.Age) / l.FadeMs);
            s.Alpha = a;
            s.Show(frame, l.Tint);
        }

        private void End(int index)
        {
            var l = live[index];
            live.RemoveAt(index);
            if (l.S != null)
            {
                l.S.Visible = false;
                release(l.S);
                l.S = null;
            }
            l.Follow = null;
            l.OnEvent = null;
            l.P.OnEvent = null;
            free.Push(l);
        }
    }
}
