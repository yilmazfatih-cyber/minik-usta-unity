#nullable enable
// One pooled sprite particle (Faz 3 Q2; D-096: no ParticleSystem): a SpriteRenderer on the shared atlas, moved by
// `ParticleField` (Presentation.Core). The kind picks the tile (spark, dust puff, star, coin, confetti rectangle, ring); the
// colour tints it; `Alpha` multiplies the colour alpha. Pooled by `UnityLevelView` (it only grows to the peak the 120-particle
// budget allows).
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class UnitySpark : ISpark
    {
        private readonly DesignSpace space;
        private readonly GameObject go;
        private readonly SpriteRenderer sr;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private float size = 32f;
        private float aspect = 1f;
        private Rgba color = Rgba.White;

        public UnitySpark(DesignSpace space, Transform parent)
        {
            this.space = space;
            go = new GameObject("Spark");
            go.transform.SetParent(parent, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Order.Flyer + 2;
            go.SetActive(false);
        }

        /// <summary>The v4 frame a particle kind draws (ART_UNITY §4.4 fx; tinted by the particle colour).</summary>
        public static string FrameOf(SparkKind kind)
        {
            switch (kind)
            {
                case SparkKind.Dust: return Frames.FxDust;
                case SparkKind.Star: return Frames.FxStar;
                case SparkKind.Coin: return "icon_coin";
                case SparkKind.Confetti: return Frames.FxConfetti;
                case SparkKind.Ring: return Frames.FxRing;
                default: return Frames.FxSpark;
            }
        }

        private FrameSprite fs;

        public void Configure(SparkKind kind, Rgba tint, double sizePx)
        {
            fs = AtlasSprites.Resolve(FrameOf(kind));
            sr.sprite = fs.Sprite;
            // a coin is drawn in its own colours (the gold tint would darken the atlas coin)
            if (kind == SparkKind.Coin && fs.Frame != null) tint = Rgba.White;
            color = tint;
            size = (float)sizePx;
            aspect = kind == SparkKind.Confetti ? 0.5f : 1f; // confetti: a small rectangle
            position = Vec2.Zero;
            scale = Vec2.One;
            rotation = 0f;
            alpha = 1f;
            // a ring sits under the stars, the rest over the blocks
            sr.sortingOrder = Order.Flyer + (kind == SparkKind.Ring ? 1 : 2);
            Apply();
        }

        public bool Visible
        {
            get => go.activeSelf;
            set => go.SetActive(value);
        }

        public Vec2 Position
        {
            get => position;
            set
            {
                position = value;
                Apply();
            }
        }

        public Vec2 Scale
        {
            get => scale;
            set
            {
                scale = value;
                Apply();
            }
        }

        public float Rotation
        {
            get => rotation;
            set
            {
                rotation = value;
                Apply();
            }
        }

        public float Alpha
        {
            get => alpha;
            set
            {
                alpha = value;
                Apply();
            }
        }

        private void Apply()
        {
            var t = go.transform;
            t.localPosition = space.World(position);
            // `size` design px wide (the frame's own aspect for atlas art, `aspect` for the placeholder confetti)
            double w = size * scale.X, h = fs.Frame != null ? size * scale.Y * fs.DesignH / fs.DesignW : size * aspect * scale.Y;
            t.localScale = new Vector3((float)(w / fs.DesignW), (float)(h / fs.DesignH), 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, rotation);
            sr.color = SpriteDraw.Mul(UnityConvert.ToColor(color, alpha), fs.Tint);
        }
    }
}
