#nullable enable
// Sprite particles without a ParticleSystem (Faz 3 Q2; D-096, JUICE §0 rule 4): every spark, dust puff, confetti piece and
// star of the juice rows is one pooled sprite (`ISpark`, a SpriteRenderer in Unity) that this field moves in closed form —
// position = from + (to − from)·ease(u) + (0, gravity·u²), scale / alpha / rotation linear in u — so a particle costs a few
// float operations per frame and no tween object. Deterministic: the scatter comes from a seeded generator (`Reset(seed)`
// at every level start), so the same level and moves always scatter the same way.
//
// Budget (JUICE rule 4): at most `ParticleMaxOnScreen` (120) alive, at most `ParticleMaxPerBurst` (40) per burst; when a
// burst would exceed the budget the OLDEST burst ends early. Reduced motion (rule 8): ≤ 20 % of the count (at least one),
// no confetti, no star ring. The counts and the scatter are `FeelSettings` values.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Game
{
    public sealed class ParticleField
    {
        private sealed class Particle
        {
            public ISpark? Spark;
            public int Burst;
            public SparkKind Kind;
            public Rgba Color;
            public float Size;
            public float Age;
            public float Life;
            public Vec2 From;
            public Vec2 To;
            public float Gravity;
            public float ScaleFrom;
            public float ScaleTo;
            public float Spin;
            public float StartAlpha;
            public float FadeFrom;
            public EaseType Ease;
        }

        /// <summary>The dust colour: a light warm grey (JUICE `fx_dust`), faded by `FeelSettings.DustAlpha`.</summary>
        public static readonly Rgba DustColor = Rgba.Hex("#EFE6D2");

        private readonly ILevelView view;
        private readonly FeelSettings feel;
        private readonly List<Particle> live = new List<Particle>(128);
        private readonly Stack<Particle> free = new Stack<Particle>();
        private Mulberry32 rng = new Mulberry32(1);
        private int nextBurst;

        public ParticleField(ILevelView view, FeelSettings feel)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.feel = feel ?? throw new ArgumentNullException(nameof(feel));
        }

        /// <summary>"Animasyonları azalt" (JUICE §0 rule 8).</summary>
        public bool Reduced { get; set; }

        /// <summary>Particles alive (waiting for their delay included).</summary>
        public int Alive => live.Count;

        /// <summary>Particles whose sprite is on screen now.</summary>
        public int Shown
        {
            get
            {
                int n = 0;
                foreach (var p in live) if (p.Spark != null) n++;
                return n;
            }
        }

        /// <summary>Bursts emitted since `Reset`.</summary>
        public int Bursts { get; private set; }

        /// <summary>Particles emitted since `Reset` (culled and finished ones included).</summary>
        public int Emitted { get; private set; }

        /// <summary>Particles ended early by the budget since `Reset`.</summary>
        public int Culled { get; private set; }

        /// <summary>The highest `Alive` seen since `Reset` (tests: the 120 budget).</summary>
        public int Peak { get; private set; }

        /// <summary>Every particle gone, the generator re-seeded (a new level or attempt).</summary>
        public void Reset(int seed)
        {
            foreach (var p in live) Free(p);
            live.Clear();
            rng = new Mulberry32(seed);
            Bursts = 0;
            Emitted = 0;
            Culled = 0;
            Peak = 0;
            nextBurst = 0;
        }

        /// <summary>Moves every particle by `dtMs` (the animation clock's step, so the lock speed-up applies).</summary>
        public void Advance(float dtMs)
        {
            if (live.Count == 0) return;
            int keep = 0;
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i];
                p.Age += dtMs;
                if (p.Age >= p.Life)
                {
                    Free(p);
                    continue;
                }
                if (p.Age >= 0f) Pose(p);
                live[keep++] = p;
            }
            if (keep < live.Count) live.RemoveRange(keep, live.Count - keep);
        }

        // --- bursts ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Four-point sparkles flying up from `origin` in a cone (JUICE #12 "yukarı 120–220 px"; #15, #56 gold sparks), alternating
        /// `color` and white. `reach` scales the flight (a bead's four sparks fly a third as far as a block's fourteen).
        /// </summary>
        public int Sparks(Vec2 origin, Rgba color, int count, float reach = 1f)
        {
            int n = Reserve(count);
            if (n == 0) return 0;
            int burst = nextBurst++;
            for (int i = 0; i < n; i++)
            {
                double a = (-90.0 + (Rand() * 2 - 1) * feel.SparkConeDeg) * Math.PI / 180.0;
                float dist = (feel.SparkRiseMinPx + (float)Rand() * (feel.SparkRiseMaxPx - feel.SparkRiseMinPx)) * reach;
                var p = New(burst, SparkKind.Spark, i % 2 == 0 ? color : Rgba.White, feel.SparkSizePx * (0.7f + 0.5f * (float)Rand()));
                p.From = origin;
                p.To = origin + new Vec2((float)Math.Cos(a) * dist, (float)Math.Sin(a) * dist);
                p.Gravity = 36f;
                p.Life = feel.SparkLifeMs * (0.8f + 0.4f * (float)Rand());
                p.ScaleFrom = 1f;
                p.ScaleTo = 0.2f;
                p.Spin = 0f;
                p.StartAlpha = 1f;
                p.FadeFrom = 0.5f;
                p.Ease = EaseType.OutExpo;
                Add(p);
            }
            return n;
        }

        /// <summary>Soft dust puffs at `origin` (the ground contact), spreading ±`spreadPx` to both sides and rising a little (JUICE #9, #11, #13, #19, #44, #107).</summary>
        public int Dust(Vec2 origin, int count, float? spreadPx = null)
        {
            int n = Reserve(count);
            if (n == 0) return 0;
            int burst = nextBurst++;
            float spread = spreadPx ?? feel.DustSpreadPx;
            for (int i = 0; i < n; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float dx = side * (0.35f + 0.65f * (float)Rand()) * spread;
                float dy = -feel.DustRisePx * (0.4f + 0.6f * (float)Rand());
                var p = New(burst, SparkKind.Dust, DustColor, feel.DustSizePx * (0.7f + 0.5f * (float)Rand()));
                p.From = origin + new Vec2(side * 8f, 0f);
                p.To = origin + new Vec2(dx, dy);
                p.Gravity = 0f;
                p.Life = feel.DustLifeMs * (0.8f + 0.4f * (float)Rand());
                p.ScaleFrom = 0.5f;
                p.ScaleTo = 1.4f;
                p.Spin = 0f;
                p.StartAlpha = feel.DustAlpha;
                p.FadeFrom = 0f;
                p.Ease = EaseType.OutQuad;
                Add(p);
            }
            return n;
        }

        /// <summary>A rain of gold sparkles falling onto `origin` from above (JUICE #16, "yukarıdan, fx_gold").</summary>
        public int GoldRain(Vec2 origin, int count)
        {
            int n = Reserve(count);
            if (n == 0) return 0;
            int burst = nextBurst++;
            var gold = Rgba.Hex(Palette.Gold);
            for (int i = 0; i < n; i++)
            {
                float x = ((float)Rand() * 2f - 1f) * 70f;
                float h = feel.GoldRainHeightPx * (0.6f + 0.4f * (float)Rand());
                var p = New(burst, SparkKind.Spark, i % 3 == 0 ? Rgba.White : gold, feel.SparkSizePx * (0.6f + 0.5f * (float)Rand()));
                p.From = origin + new Vec2(x, -h);
                p.To = origin + new Vec2(x + ((float)Rand() * 2f - 1f) * 30f, 30f);
                p.Gravity = 0f;
                p.Age = -(float)Rand() * 0.35f * feel.GoldRainLifeMs; // staggered start
                p.Life = feel.GoldRainLifeMs;
                p.ScaleFrom = 1f;
                p.ScaleTo = 0.5f;
                p.Spin = 0f;
                p.StartAlpha = 1f;
                p.FadeFrom = 0.6f;
                p.Ease = EaseType.InQuad;
                Add(p);
            }
            return n;
        }

        /// <summary>
        /// Confetti falling over a `widthPx` wide band at `origin` (JUICE #18 segment: the segment's colours; #55 win: palette
        /// colours). `lifeMs` = one piece's fall; pieces start staggered over 25 % of it. Reduced motion: none ("konfeti yok").
        /// </summary>
        public int Confetti(Vec2 origin, float widthPx, int count, IList<Rgba> palette, float lifeMs, float fallPx)
        {
            if (Reduced || palette.Count == 0) return 0;
            int n = Reserve(count);
            if (n == 0) return 0;
            int burst = nextBurst++;
            for (int i = 0; i < n; i++)
            {
                float x = ((float)Rand() - 0.5f) * widthPx;
                var p = New(burst, SparkKind.Confetti, palette[i % palette.Count], feel.ConfettiSizePx * (0.7f + 0.4f * (float)Rand()));
                p.From = origin + new Vec2(x, -(float)Rand() * 60f);
                p.To = p.From + new Vec2(((float)Rand() - 0.5f) * 180f, fallPx * (0.7f + 0.3f * (float)Rand()));
                p.Gravity = 0f;
                p.Age = -(float)Rand() * 0.25f * lifeMs;
                p.Life = lifeMs * (0.85f + 0.3f * (float)Rand());
                p.ScaleFrom = 1f;
                p.ScaleTo = 1f;
                p.Spin = (float)(Rand() < 0.5 ? -1 : 1) * (360f + (float)Rand() * 360f);
                p.StartAlpha = 1f;
                p.FadeFrom = 0.75f;
                p.Ease = EaseType.OutQuad;
                Add(p);
            }
            return n;
        }

        /// <summary>
        /// JUICE #96 star burst: `count` small stars radially 160–320 px and a white ring 0,6 → 1,6 (α 0,6 → 0), `StarBurstMs`,
        /// Expo.easeOut. Reduced motion: ≤ 20 % of the stars (3 of 16) and no ring.
        /// </summary>
        public int StarBurst(Vec2 origin, int count)
        {
            int n = Reserve(count + (Reduced ? 0 : 1));
            if (n == 0) return 0;
            int burst = nextBurst++;
            var gold = Rgba.Hex(Palette.Gold);
            int stars = Reduced ? n : n - 1;
            for (int i = 0; i < stars; i++)
            {
                double a = 2 * Math.PI * (i + (Rand() - 0.5) * 0.5) / Math.Max(1, stars);
                float dist = feel.StarBurstMinPx + (float)Rand() * (feel.StarBurstMaxPx - feel.StarBurstMinPx);
                var p = New(burst, SparkKind.Star, i % 2 == 0 ? gold : Rgba.White, feel.StarSizePx * 0.85f);
                p.From = origin;
                p.To = origin + new Vec2((float)Math.Cos(a) * dist, (float)Math.Sin(a) * dist);
                p.Gravity = 0f;
                p.Life = feel.StarBurstMs;
                p.ScaleFrom = 1f;
                p.ScaleTo = 0.3f;
                p.Spin = (i % 2 == 0 ? 1f : -1f) * 180f;
                p.StartAlpha = 1f;
                p.FadeFrom = 0.6f;
                p.Ease = EaseType.OutExpo;
                Add(p);
            }
            if (!Reduced)
            {
                var ring = New(burst, SparkKind.Ring, Rgba.White, feel.StarSizePx * 4f);
                ring.From = origin;
                ring.To = origin;
                ring.Gravity = 0f;
                ring.Life = feel.StarBurstMs;
                ring.ScaleFrom = feel.StarRingFrom;
                ring.ScaleTo = feel.StarRingTo;
                ring.Spin = 0f;
                ring.StartAlpha = feel.StarRingAlpha;
                ring.FadeFrom = 0f;
                ring.Ease = EaseType.OutExpo;
                Add(ring);
            }
            return n;
        }

        // --- internals -------------------------------------------------------------------------------------------------------

        private double Rand() => rng.Next();

        /// <summary>The number of particles a burst of `want` may emit (burst cap, reduced factor, budget); older bursts end early to make room.</summary>
        private int Reserve(int want)
        {
            if (want <= 0) return 0;
            int n = Math.Min(want, feel.ParticleMaxPerBurst);
            if (Reduced) n = Math.Max(1, (int)Math.Round(n * (double)feel.ReducedParticleFactor, MidpointRounding.AwayFromZero));
            n = Math.Min(n, feel.ParticleMaxOnScreen);
            while (live.Count + n > feel.ParticleMaxOnScreen && live.Count > 0) CullOldest();
            Bursts++;
            return n;
        }

        private void CullOldest()
        {
            int id = live[0].Burst;
            int n = 0;
            while (n < live.Count && live[n].Burst == id)
            {
                Free(live[n]);
                n++;
            }
            live.RemoveRange(0, n);
            Culled += n;
        }

        private Particle New(int burst, SparkKind kind, Rgba color, float size)
        {
            var p = free.Count > 0 ? free.Pop() : new Particle();
            p.Burst = burst;
            p.Kind = kind;
            p.Color = color;
            p.Size = size;
            p.Age = 0f;
            p.Spark = null;
            return p;
        }

        private void Add(Particle p)
        {
            live.Add(p);
            Emitted++;
            if (live.Count > Peak) Peak = live.Count;
        }

        private void Free(Particle p)
        {
            if (p.Spark != null)
            {
                p.Spark.Visible = false;
                view.ReleaseSpark(p.Spark);
                p.Spark = null;
            }
            free.Push(p);
        }

        private void Pose(Particle p)
        {
            if (p.Spark == null)
            {
                p.Spark = view.AcquireSpark(p.Kind, p.Color, p.Size);
                p.Spark.Visible = true;
            }
            float u = p.Age / p.Life;
            float k = Easing.Evaluate(p.Ease, u);
            var pos = Vec2.Lerp(p.From, p.To, k);
            var s = p.Spark;
            s.Position = new Vec2(pos.X, pos.Y + p.Gravity * u * u);
            float sc = p.ScaleFrom + (p.ScaleTo - p.ScaleFrom) * u;
            s.Scale = new Vec2(sc, sc);
            s.Rotation = p.Spin * u;
            float fade = p.FadeFrom >= 1f || u < p.FadeFrom ? 1f : 1f - (u - p.FadeFrom) / (1f - p.FadeFrom);
            s.Alpha = p.StartAlpha * fade;
        }
    }
}
