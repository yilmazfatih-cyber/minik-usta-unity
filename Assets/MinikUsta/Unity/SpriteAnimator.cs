#nullable enable
// Draws the frames of a clip on a SpriteRenderer (world: board FX) or a uGUI Image (overlay: FX over the open window) (paket B2).
// It holds no clock and decides nothing: `FxPlayer` / `AnimPlayer` (Presentation.Core) pick the frame name every frame, this only
// swaps the sprite when the name changed. A frame of king.json / fx.json is trimmed, so its pivot (the clip canvas pivot) is part
// of the sprite: the world sprite sits with its pivot at the transform, the Image gets the same pivot on its RectTransform and the
// frame's design size — the clip is placed by its pivot, never box-fitted.
using MinikUsta.Presentation;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace MinikUsta.Unity
{
    public sealed class SpriteAnimator
    {
        private readonly SpriteRenderer? sr;
        private readonly Image? image;
        private string? shown;

        public SpriteAnimator(SpriteRenderer renderer)
        {
            sr = renderer;
        }

        public SpriteAnimator(Image target)
        {
            image = target;
        }

        /// <summary>The frame shown now (its sprite, design size and pivot).</summary>
        public FrameSprite Current { get; private set; }

        public string? Shown => shown;

        /// <summary>Shows frame `name` (resolved through the v4 atlas, then the 3D libraries); false when nothing can be drawn.</summary>
        public bool Show(string name)
        {
            if (name == shown) return Current.Ok;
            shown = name;
            Current = AtlasSprites.Resolve(name);
            if (sr != null)
            {
                sr.sprite = Current.Sprite;
                sr.drawMode = SpriteDrawMode.Simple;
            }
            if (image != null)
            {
                image.sprite = Current.Sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
            }
            return Current.Ok;
        }

        public void Clear()
        {
            shown = null;
            Current = default;
            if (sr != null) sr.sprite = null;
            if (image != null) image.sprite = null;
        }
    }

    /// <summary>A pooled world FX sprite (`IFxSprite`, depth Under / Over): a SpriteRenderer placed by its clip pivot.</summary>
    public sealed class UnityFxSprite : IFxSprite
    {
        private readonly DesignSpace space;
        private readonly GameObject go;
        private readonly SpriteRenderer sr;
        private readonly SpriteAnimator anim;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private Rgba tint = Rgba.White;

        public UnityFxSprite(DesignSpace space, Transform parent, FxDepth depth)
        {
            this.space = space;
            Depth = depth;
            go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            sr = go.AddComponent<SpriteRenderer>();
            // under: behind the blocks, over the plan and the scaffold (the segment flare); over: above every block and particle
            sr.sortingOrder = depth == FxDepth.Under ? Order.Pieces - 8 : Order.Flyer + 4;
            anim = new SpriteAnimator(sr);
            go.SetActive(false);
        }

        public FxDepth Depth { get; }

        public bool Visible
        {
            get => go.activeSelf;
            set
            {
                go.SetActive(value);
                if (!value) anim.Clear();
            }
        }

        public void Show(string frame, Rgba color)
        {
            anim.Show(frame);
            tint = color;
            Apply();
        }

        public Vec2 Position
        {
            get => position;
            set => position = value;
        }

        public Vec2 Scale
        {
            get => scale;
            set => scale = value;
        }

        public float Rotation
        {
            get => rotation;
            set => rotation = value;
        }

        public float Alpha
        {
            get => alpha;
            set => alpha = value;
        }

        private void Apply()
        {
            var t = go.transform;
            t.localPosition = space.World(position);
            // the sprite's pixels-per-unit (100 × scale) already gives its design size; the pivot is the sprite's own
            var fs = anim.Current;
            t.localScale = new Vector3(scale.X, scale.Y, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, rotation);
            sr.color = SpriteDraw.Mul(UnityConvert.ToColor(tint, alpha), fs.Tint);
        }
    }

    /// <summary>A pooled overlay FX sprite (`IFxSprite`, depth Ui): a uGUI Image on the Fx layer, over the HUD and the open window.</summary>
    public sealed class UnityUiFx : IFxSprite
    {
        private readonly UiRenderer hud;
        private readonly RectTransform rect;
        private readonly Image image;
        private readonly SpriteAnimator anim;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private Rgba tint = Rgba.White;

        public UnityUiFx(RectTransform parent, UiRenderer hud)
        {
            this.hud = hud;
            var go = new GameObject("UiFx", typeof(RectTransform));
            rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            image = go.AddComponent<Image>();
            image.raycastTarget = false;
            anim = new SpriteAnimator(image);
            go.SetActive(false);
        }

        public FxDepth Depth => FxDepth.Ui;

        public bool Visible
        {
            get => rect.gameObject.activeSelf;
            set
            {
                rect.gameObject.SetActive(value);
                if (!value) anim.Clear();
            }
        }

        public void Show(string frame, Rgba color)
        {
            anim.Show(frame);
            tint = color;
            Apply();
        }

        public Vec2 Position
        {
            get => position;
            set => position = value;
        }

        public Vec2 Scale
        {
            get => scale;
            set => scale = value;
        }

        public float Rotation
        {
            get => rotation;
            set => rotation = value;
        }

        public float Alpha
        {
            get => alpha;
            set => alpha = value;
        }

        private void Apply()
        {
            var fs = anim.Current;
            rect.pivot = new Vector2(fs.PivotX, fs.PivotY);
            rect.sizeDelta = new Vector2((float)fs.DesignW, (float)fs.DesignH);
            rect.anchoredPosition = new Vector2(position.X - hud.DesignW / 2f, hud.DesignH / 2f - position.Y);
            rect.localScale = new Vector3(scale.X, scale.Y, 1f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var c = SpriteDraw.Mul(UnityConvert.ToColor(tint, alpha), fs.Tint);
            if (!fs.Ok) c.a = 0f;
            image.color = c;
        }
    }

    /// <summary>
    /// The camera as a tween target (Faz 3 Q2 shakes + B2 push-ins): `Position` = the offset in design px (y down) from its home,
    /// `Scale.X` = the zoom (1 = rest; 1,06 = 6 % closer: orthographicSize ÷ 1,06). The HUD is an overlay canvas: it never zooms.
    /// </summary>
    public sealed class CameraTarget : ITweenTarget
    {
        private readonly Camera camera;
        private readonly Vector3 home;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float baseSize = 9.6f;

        public CameraTarget(Camera camera)
        {
            this.camera = camera;
            home = camera.transform.localPosition;
        }

        /// <summary>The orthographic size of the viewport at zoom 1 (`UnityLevelView.SetViewport`).</summary>
        public float BaseSize
        {
            get => baseSize;
            set
            {
                baseSize = value;
                ApplyZoom();
            }
        }

        public Vec2 Position
        {
            get => position;
            set
            {
                position = value;
                camera.transform.localPosition = new Vector3(home.x + value.X / DesignSpace.PixelsPerUnit, home.y - value.Y / DesignSpace.PixelsPerUnit, home.z);
            }
        }

        public Vec2 Scale
        {
            get => scale;
            set
            {
                scale = value;
                ApplyZoom();
            }
        }

        public float Rotation { get; set; }

        public float Alpha { get; set; } = 1f;

        private void ApplyZoom()
        {
            float z = scale.X > 0.01f ? scale.X : 1f;
            camera.orthographicSize = baseSize / z;
        }
    }
}
