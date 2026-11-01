#nullable enable
// The UI trees of Presentation.Core in uGUI (paket V1; D-096: uGUI Text with the Baloo 2 font, no TMP). One ScreenSpaceOverlay
// canvas (CanvasScaler ScaleWithScreenSize 1080 × 1920, `Shrink` = the camera's FIT), a "Design" rectangle of the design canvas
// centred in it and one child per `UiLayer` (Home, Hud, Window, Fx, Overlay; back to front). Every `UiNode` becomes one
// GameObject: RectTransform from its absolute design rect (made parent-relative), an Image (atlas frame, Simple or Sliced,
// tint × alpha), a Text (ART_REFERENCE §4 style → colour + Outline + Shadow; best fit down to half size), a Button (tap → the
// tree's handler) with the JUICE #69 press feel. It decides nothing: the screens build the trees, this only draws them.
using System;
using System.Collections.Generic;
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tweening;
using MinikUsta.Presentation.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MinikUsta.Unity
{
    /// <summary>A RectTransform as a tween target (scale pops, window open); `Position` / `Alpha` are not used.</summary>
    public sealed class UiTarget : ITweenTarget
    {
        private readonly RectTransform rt;
        private Vec2 scale = Vec2.One;
        private float rotation;

        public UiTarget(RectTransform rt)
        {
            this.rt = rt;
        }

        public Vec2 Position { get; set; }

        public Vec2 Scale
        {
            get => scale;
            set
            {
                scale = value;
                rt.localScale = new Vector3(value.X, value.Y, 1f);
            }
        }

        public float Rotation
        {
            get => rotation;
            set
            {
                rotation = value;
                rt.localRotation = Quaternion.Euler(0f, 0f, value);
            }
        }

        public float Alpha { get; set; } = 1f;
    }

    public sealed class UiRenderer : IUiSurface
    {
        /// <summary>Resources path of the Baloo 2 ExtraBold TTF (OFL; Assets/MinikUsta/Resources/fonts).</summary>
        public const string FontResource = "fonts/Baloo2-ExtraBold";

        private sealed class Bound
        {
            public UiNode Node = null!;
            public RectTransform Rt = null!;
            public Image? Image;
            public Text? Text;
            public Button? Button;
            public UiTarget? Target;
        }

        private readonly FeelSettings feel;
        private readonly RectTransform design;
        private readonly RectTransform[] layers = new RectTransform[(int)UiLayer.Overlay + 1];
        private readonly Dictionary<UiNode, Bound> bound = new Dictionary<UiNode, Bound>();
        private readonly Action<UiNode>?[] handlers = new Action<UiNode>?[(int)UiLayer.Overlay + 1];

        public Font Font { get; }

        /// <summary>The design canvas size (design px): the origin of `UnityUiSpark` positions.</summary>
        public float DesignW { get; private set; } = 1080f;
        public float DesignH { get; private set; } = 1920f;

        public UiRenderer(FeelSettings feel)
        {
            this.feel = feel;
            var baloo = Resources.Load<Font>(FontResource);
            if (baloo == null) Debug.LogWarning("Minik Usta: " + FontResource + " not found; LegacyRuntime font (Turkish glyphs unchecked)");
            Font = baloo != null ? baloo : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("Ui", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
            scaler.referencePixelsPerUnit = 100f;
            root.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            design = NewRect("Design", root.transform);
            design.anchorMin = new Vector2(0.5f, 0.5f);
            design.anchorMax = new Vector2(0.5f, 0.5f);
            design.pivot = new Vector2(0.5f, 0.5f);
            design.anchoredPosition = Vector2.zero;
            design.sizeDelta = new Vector2(1080f, 1920f);
            for (int i = 0; i < layers.Length; i++)
            {
                var l = NewRect(((UiLayer)i).ToString(), design);
                Stretch(l);
                layers[i] = l;
            }
        }

        /// <summary>The design canvas of the current viewport (FIT 1080 × 1920; EXPAND taller).</summary>
        public void SetCanvas(double w, double h)
        {
            DesignW = (float)w;
            DesignH = (float)h;
            design.sizeDelta = new Vector2(DesignW, DesignH);
        }

        /// <summary>The RectTransform of a layer (the Fx layer holds the uGUI particles of `UnityUiSpark`).</summary>
        public RectTransform Layer(UiLayer layer) => layers[(int)layer];

        // --- IUiSurface -------------------------------------------------------------------------------------------------------

        public void Show(UiLayer layer, UiNode? root, Action<UiNode>? onTap)
        {
            var parent = layers[(int)layer];
            handlers[(int)layer] = onTap;
            // drop the previous tree of the layer
            var old = new List<Transform>();
            foreach (Transform child in parent) old.Add(child);
            foreach (var t in old)
            {
                t.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(t.gameObject);
            }
            var stale = new List<UiNode>();
            foreach (var kv in bound) if (kv.Value.Rt == null || IsUnder(kv.Value.Rt, parent)) stale.Add(kv.Key);
            foreach (var n in stale) bound.Remove(n);
            if (root == null) return;
            Build(root, parent, new PxRect(0, 0, DesignW, DesignH), layer);
        }

        public void Refresh(UiNode node)
        {
            if (!bound.TryGetValue(node, out var b)) return;
            var parentRect = node.Parent != null ? node.Parent.Rect : new PxRect(0, 0, DesignW, DesignH);
            Apply(b, parentRect);
        }

        public ITweenTarget Target(UiNode node)
        {
            if (!bound.TryGetValue(node, out var b)) return new PlainTarget();
            return b.Target ??= new UiTarget(b.Rt);
        }

        public void SetLayerVisible(UiLayer layer, bool visible) => layers[(int)layer].gameObject.SetActive(visible);

        // --- building ---------------------------------------------------------------------------------------------------------

        private static bool IsUnder(Transform t, Transform root)
        {
            for (var p = t; p != null; p = p.parent) if (p == root) return true;
            return false;
        }

        private void Build(UiNode n, Transform parent, PxRect parentRect, UiLayer layer)
        {
            var rt = NewRect(n.Id, parent);
            var b = new Bound { Node = n, Rt = rt };
            bool image = n.Frame != null || n.BlocksInput || n.Action != null;
            if (image)
            {
                b.Image = rt.gameObject.AddComponent<Image>();
            }
            if (n.Text != null)
            {
                RectTransform trt = rt;
                if (b.Image != null)
                {
                    trt = NewRect("Text", rt);
                    Stretch(trt);
                }
                var t = trt.gameObject.AddComponent<Text>();
                t.font = Font;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.resizeTextForBestFit = true;
                t.raycastTarget = false;
                t.supportRichText = false;
                var look = TextStyles.Of(n.Style, n.FontPx, n.TextFill, n.TextStroke);
                // effects first: Shadow (drop), then Outline (stroke); a thick stroke gets a second, half-distance ring (no gaps)
                if (look.ShadowPx > 0.5)
                {
                    var sh = trt.gameObject.AddComponent<Shadow>();
                    sh.effectColor = UnityConvert.ToColor(look.Shadow);
                    sh.effectDistance = new Vector2(0f, -(float)look.ShadowPx);
                }
                if (look.OutlinePx > 0.5)
                {
                    var o = trt.gameObject.AddComponent<Outline>();
                    o.effectColor = UnityConvert.ToColor(look.Outline);
                    o.effectDistance = new Vector2((float)look.OutlinePx, -(float)look.OutlinePx);
                    if (look.OutlinePx > 4)
                    {
                        var o2 = trt.gameObject.AddComponent<Outline>();
                        o2.effectColor = UnityConvert.ToColor(look.Outline);
                        o2.effectDistance = new Vector2((float)look.OutlinePx * 0.5f, -(float)look.OutlinePx * 0.5f);
                    }
                }
                b.Text = t;
            }
            if (n.Action != null)
            {
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = b.Image;
                var node = n;
                var l = layer;
                button.onClick.AddListener(() => Tapped(l, node));
                b.Button = button;
                if (n.Pressable) rt.gameObject.AddComponent<PressableButton>().Init(rt, feel);
            }
            bound[n] = b;
            Apply(b, parentRect);
            foreach (var c in n.Children) Build(c, rt, n.Rect, layer);
        }

        private void Tapped(UiLayer layer, UiNode node)
        {
            if (!node.Enabled) return;
            handlers[(int)layer]?.Invoke(node);
        }

        /// <summary>Writes every property of a node (rect, image, text, button, visibility).</summary>
        private void Apply(Bound b, PxRect parentRect)
        {
            var n = b.Node;
            var rt = b.Rt;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((float)(n.Rect.CenterX - parentRect.X), -(float)(n.Rect.CenterY - parentRect.Y));
            rt.sizeDelta = new Vector2((float)n.Rect.W, (float)n.Rect.H);
            rt.localRotation = Quaternion.Euler(0f, 0f, n.Rotation);
            if (b.Image != null)
            {
                var img = b.Image;
                img.raycastTarget = n.Action != null || n.BlocksInput;
                if (n.Frame != null)
                {
                    var fs = AtlasSprites.Resolve(n.Frame);
                    img.sprite = fs.Sprite;
                    img.type = n.Sliced && fs.Sliced ? Image.Type.Sliced : Image.Type.Simple;
                    img.preserveAspect = n.PreserveAspect;
                    var c = SpriteDraw.Mul(UnityConvert.ToColor(n.Tint), fs.Tint);
                    c.a *= n.Alpha;
                    if (!fs.Ok) c.a = 0f; // nothing to draw, but keep the raycast target of a button / panel
                    img.color = c;
                }
                else img.color = new Color(0f, 0f, 0f, 0f); // an invisible input catcher
            }
            if (b.Text != null)
            {
                var t = b.Text;
                var look = TextStyles.Of(n.Style, n.FontPx, n.TextFill, n.TextStroke);
                t.text = n.Text ?? "";
                int size = (int)Math.Round(n.FontPx);
                t.fontSize = size;
                t.resizeTextMaxSize = size;
                t.resizeTextMinSize = Math.Max(8, size / 2);
                t.alignment = n.Align == TextAlign.Left ? TextAnchor.MiddleLeft : n.Align == TextAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                var fill = UnityConvert.ToColor(look.Fill);
                fill.a *= n.Alpha;
                t.color = fill;
            }
            if (b.Button != null) b.Button.interactable = n.Enabled;
            rt.gameObject.SetActive(n.Visible);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
