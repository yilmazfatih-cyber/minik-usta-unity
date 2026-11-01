#nullable enable
// A sprite particle drawn by uGUI (Faz 3 Q2): the star burst of the win window. The level windows are a ScreenSpaceOverlay
// canvas, which draws above every sprite — a SpriteRenderer burst would sit under the dimming and the window. So the star and
// ring particles are Images on the UI's Fx layer (above the window, V1 `UiRenderer`), at design px like everything else; the
// rest of the particles stay sprites (`UnitySpark`). `Position` is design px (origin top-left, y down); the parent is centre-anchored.
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using UnityEngine;
using UnityEngine.UI;

namespace MinikUsta.Unity
{
    public sealed class UnityUiSpark : ISpark
    {
        private readonly RectTransform rect;
        private readonly Image image;
        private readonly UiRenderer hud;
        private Vec2 position;
        private Vec2 scale = Vec2.One;
        private float rotation;
        private float alpha = 1f;
        private float size = 32f;
        private Rgba color = Rgba.White;

        public UnityUiSpark(RectTransform parent, UiRenderer hud)
        {
            this.hud = hud;
            var go = new GameObject("UiSpark", typeof(RectTransform));
            rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            image = go.AddComponent<Image>();
            image.raycastTarget = false;
            go.SetActive(false);
        }

        public void Configure(SparkKind kind, Rgba tint, double sizePx)
        {
            var fs = AtlasSprites.Resolve(UnitySpark.FrameOf(kind));
            image.sprite = fs.Sprite;
            image.preserveAspect = true;
            color = tint;
            size = (float)sizePx;
            position = Vec2.Zero;
            scale = Vec2.One;
            rotation = 0f;
            alpha = 1f;
            Apply();
        }

        public bool Visible
        {
            get => rect.gameObject.activeSelf;
            set => rect.gameObject.SetActive(value);
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
            rect.anchoredPosition = new Vector2(position.X - hud.DesignW / 2f, hud.DesignH / 2f - position.Y);
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = new Vector3(scale.X, scale.Y, 1f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            image.color = UnityConvert.ToColor(color, alpha);
        }
    }
}
