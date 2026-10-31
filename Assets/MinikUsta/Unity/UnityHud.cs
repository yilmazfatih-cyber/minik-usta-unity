#nullable enable
// The placeholder HUD and level windows in uGUI (Faz 3 Q1; D-096: uGUI Text + the built-in LegacyRuntime font; no TMP).
// One ScreenSpaceOverlay canvas, CanvasScaler ScaleWithScreenSize 1080 × 1920 with `Shrink` (= the FIT of the camera:
// the 1080-wide design canvas fits inside the screen, centred). Every rectangle comes from `HudLayout` / `ScreenLayout`
// in design px, so the HUD lines up with the board drawn by the sprites. Texts come from tr.json through `LevelPlayer`.
using System;
using System.Collections.Generic;
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tweening;
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

    public sealed class UnityHud
    {
        private const int MaxButtons = 3;

        private readonly Font font;
        private readonly RectTransform design;
        private readonly RectTransform capsule;
        private readonly Text capsuleText;
        private readonly Text nameText;
        private readonly RectTransform blocks;
        private readonly Text blocksText;
        private readonly RectTransform segments;
        private readonly Text segmentsText;
        private readonly RectTransform moves;
        private readonly RectTransform movesBand;
        private readonly RectTransform movesNumberRect;
        private readonly Text movesLabel;
        private readonly Text movesNumber;
        private readonly RectTransform[] boosters = new RectTransform[4];
        private readonly LayoutSettings settings;
        private ScreenLayout? layout;
        private readonly RectTransform pause;
        private readonly GameObject overlay;
        private readonly RectTransform panel;
        private readonly RectTransform panelTitleRect;
        private readonly RectTransform panelBodyRect;
        private readonly Text panelTitle;
        private readonly Text panelBody;
        private readonly Button[] buttons = new Button[MaxButtons];
        private readonly Image[] buttonImages = new Image[MaxButtons];
        private readonly Text[] buttonTexts = new Text[MaxButtons];
        private Action<PanelAction>? onAction;
        private PanelModel? shown;

        // Q2: streak capsule (beads + Golden Trowel), gold chip, the level star on the win window
        private readonly RectTransform streak;
        private readonly Text streakLabel;
        private readonly RectTransform[] beadRects = new RectTransform[Combo4];
        private readonly Image[] beadImages = new Image[Combo4];
        private readonly UiTarget[] beadTargets = new UiTarget[Combo4];
        private readonly RectTransform trowelRect;
        private readonly Image trowelImage;
        private readonly Text trowelCount;
        private readonly RectTransform goldChip;
        private readonly RectTransform goldCoinRect;
        private readonly RectTransform goldTextRect;
        private readonly Text goldText;
        private readonly RectTransform star;
        private readonly RectTransform windowRoot;
        private readonly Stack<UnityUiSpark> uiSparks = new Stack<UnityUiSpark>();

        /// <summary>Beads of the Usta Serisi (K-33: four correct placements earn a Golden Trowel).</summary>
        private const int Combo4 = 4;

        public UiTarget MovesChip { get; }
        public UiTarget BlocksChip { get; }
        public UiTarget PanelTarget { get; }
        public UiTarget TrowelIcon { get; }
        public UiTarget GoldChip { get; }
        public UiTarget StarTarget { get; }

        /// <summary>The design canvas size (design px) of the current layout: the origin of `UnityUiSpark` positions.</summary>
        public float DesignW { get; private set; } = 1080f;
        public float DesignH { get; private set; } = 1920f;

        public UnityHud(Action onPause, LayoutSettings settings, FeelSettings feel)
        {
            this.settings = settings;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("Hud", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
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

            var neutral = UnityConvert.ToColor(Rgba.Hex(Palette.Neutral));
            var ink = UnityConvert.ToColor(Rgba.Hex(Palette.Ink));
            var white = Color.white;

            capsule = Panel("LevelCapsule", design, neutral);
            capsuleText = Label(capsule, "", 40, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            var nameRect = NewRect("LevelName", design);
            nameText = Label(nameRect, "", 40, white, FontStyle.Bold, TextAnchor.MiddleLeft);
            blocks = Panel("BlocksChip", design, neutral);
            blocksText = Label(blocks, "", 44, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            segments = Panel("Segments", design, UnityConvert.ToColor(Rgba.Hex(Palette.BlueprintDeep)));
            segmentsText = Label(segments, "", 56, white, FontStyle.Bold, TextAnchor.MiddleCenter);
            // HUD v3 moves plate (kit.v3.screen.hud): cream plate, dark label band on top, the number below
            moves = Panel("Moves", design, UnityConvert.ToColor(Rgba.Hex(Palette.MovesCream)));
            movesBand = Panel("Band", moves, UnityConvert.ToColor(Rgba.Hex(Palette.MovesBand)));
            movesLabel = Label(movesBand, "", 34, white, FontStyle.Bold, TextAnchor.MiddleCenter);
            movesNumberRect = NewRect("Number", moves);
            movesNumber = Label(movesNumberRect, "", (int)settings.MovesFontPx, ink, FontStyle.Bold, TextAnchor.MiddleCenter);

            // booster bar (UX §5.1, §5.10): hammer, crane, brush, undo — not playable in Faz 3 Q1, drawn disabled
            var slot = new Color(neutral.r, neutral.g, neutral.b, 0.55f);
            var boosterIcons = new[] { Tile.Hammer, Tile.CraneHook, Tile.Brush, Tile.Undo };
            for (int i = 0; i < boosters.Length; i++)
            {
                boosters[i] = Panel("Booster" + i, design, slot);
                var bi = NewRect("Icon", boosters[i]);
                Stretch(bi, 28f, 28f);
                var img = bi.gameObject.AddComponent<Image>();
                img.sprite = ProceduralSprites.Get(boosterIcons[i]);
                img.color = new Color(ink.r, ink.g, ink.b, 0.45f);
                img.raycastTarget = false;
            }

            // streak capsule in the status strip (UX §5.1 "Usta Serisi ●●●○ [mala]", ART §14.5: dark capsule α 0,78)
            var gold = UnityConvert.ToColor(Rgba.Hex(Palette.Gold));
            streak = Panel("Streak", design, new Color(0.08f, 0.06f, 0.2f, 0.78f));
            streakLabel = Label(NewRect("StreakLabel", design), "", 26, white, FontStyle.Bold, TextAnchor.MiddleLeft);
            for (int i = 0; i < Combo4; i++)
            {
                beadRects[i] = NewRect("Bead" + i, design);
                beadImages[i] = beadRects[i].gameObject.AddComponent<Image>();
                beadImages[i].sprite = ProceduralSprites.Get(Tile.Circle);
                beadImages[i].raycastTarget = false;
                beadTargets[i] = new UiTarget(beadRects[i]);
            }
            trowelRect = NewRect("Trowel", design);
            trowelImage = trowelRect.gameObject.AddComponent<Image>();
            trowelImage.sprite = ProceduralSprites.Get(Tile.Trowel);
            trowelImage.raycastTarget = false;
            trowelCount = Label(NewRect("TrowelCount", design), "", 36, white, FontStyle.Bold, TextAnchor.MiddleLeft);
            TrowelIcon = new UiTarget(trowelRect);
            SetStreak(0, 0, "", "");

            // gold chip (JUICE #56: the Bonus İnşaat coins fly here); hidden until a win counts gold
            goldChip = Panel("GoldChip", design, neutral);
            goldCoinRect = NewRect("Coin", goldChip);
            var coinImage = goldCoinRect.gameObject.AddComponent<Image>();
            coinImage.sprite = ProceduralSprites.Get(Tile.Coin);
            coinImage.color = gold;
            coinImage.raycastTarget = false;
            goldTextRect = NewRect("GoldText", goldChip);
            goldText = Label(goldTextRect, "", 40, ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            goldChip.gameObject.SetActive(false);
            GoldChip = new UiTarget(goldChip);

            pause = Panel("Pause", design, UnityConvert.ToColor(Rgba.Hex(Palette.Secondary)));
            var pauseButton = pause.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pause.GetComponent<Image>();
            pauseButton.onClick.AddListener(() => onPause());
            pause.gameObject.AddComponent<PressableButton>().Init(pause, feel); // JUICE #69
            var icon = NewRect("Icon", pause);
            Stretch(icon, 24f, 24f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = ProceduralSprites.Get(Tile.Pause);
            iconImage.color = white;
            iconImage.raycastTarget = false;

            // level windows: full-screen dim (alpha.overlay) + a cream panel (UX §0.2: options at the bottom of the panel)
            var dim = NewRect("Overlay", root.transform);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.sprite = ProceduralSprites.Get(Tile.Plain);
            dimImage.color = UnityConvert.ToColor(Rgba.Hex(Palette.Overlay, Palette.OverlayAlpha));
            overlay = dim.gameObject;
            windowRoot = dim;
            panel = Panel("Window", dim, UnityConvert.ToColor(Rgba.Hex(Palette.Panel)));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panelTitleRect = NewRect("Title", panel);
            panelTitle = Label(panelTitleRect, "", 80, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            panelBodyRect = NewRect("Body", panel);
            panelBody = Label(panelBodyRect, "", 46, ink, FontStyle.Normal, TextAnchor.MiddleCenter);
            for (int i = 0; i < MaxButtons; i++)
            {
                var b = Panel("Option" + i, panel, UnityConvert.ToColor(Rgba.Hex(Palette.Primary)));
                b.anchorMin = new Vector2(0.5f, 0.5f);
                b.anchorMax = new Vector2(0.5f, 0.5f);
                buttonImages[i] = b.GetComponent<Image>();
                buttons[i] = b.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = buttonImages[i];
                int index = i;
                buttons[i].onClick.AddListener(() => Pressed(index));
                buttonTexts[i] = Label(b, "", 56, white, FontStyle.Bold, TextAnchor.MiddleCenter);
                b.gameObject.AddComponent<PressableButton>().Init(b, feel); // JUICE #69: 0,94 pressed, Back.easeOut released
            }
            // the level star over the top edge of the win window (JUICE #96)
            star = NewRect("Star", dim);
            var starImage = star.gameObject.AddComponent<Image>();
            starImage.sprite = ProceduralSprites.Get(Tile.Star);
            starImage.color = gold;
            starImage.raycastTarget = false;
            star.gameObject.SetActive(false);
            StarTarget = new UiTarget(star);
            overlay.SetActive(false);
            MovesChip = new UiTarget(moves);
            BlocksChip = new UiTarget(blocks);
            PanelTarget = new UiTarget(panel);
        }

        /// <summary>Places every HUD element for the design canvas of `vp` (FIT: 1080 × 1920).</summary>
        public void Layout(DesignViewport vp, ScreenLayout L)
        {
            layout = L;
            DesignW = (float)vp.W;
            DesignH = (float)vp.H;
            design.sizeDelta = new Vector2((float)vp.W, (float)vp.H);
            Place(capsule, HudLayout.Capsule(L));
            Place((RectTransform)nameText.transform, HudLayout.LevelName(L));
            Place(blocks, HudLayout.BlocksChip(L));
            Place(segments, HudLayout.Segments(L));
            Place(moves, HudLayout.Moves(L));
            PlaceIn(movesBand, HudLayout.MovesBand(L, settings), HudLayout.Moves(L));
            PlaceIn(movesNumberRect, HudLayout.MovesNumber(L, settings), HudLayout.Moves(L));
            Place(pause, HudLayout.Pause(L));
            for (int i = 0; i < boosters.Length && i < L.Boosters.Length; i++) Place(boosters[i], L.Boosters[i]);
            Place(streak, HudLayout.Streak(L));
            Place((RectTransform)streakLabel.transform, HudLayout.StreakLabel(L));
            for (int i = 0; i < Combo4; i++) Place(beadRects[i], HudLayout.Bead(L, i));
            Place(trowelRect, HudLayout.TrowelIcon(L));
            Place((RectTransform)trowelCount.transform, HudLayout.TrowelCount(L));
            var chip = HudLayout.GoldChip(L);
            Place(goldChip, chip);
            PlaceIn(goldCoinRect, new PxRect(chip.X + 10, chip.Y + 8, 44, 44), chip);
            PlaceIn(goldTextRect, new PxRect(chip.X + 60, chip.Y, chip.W - 68, chip.H), chip);
        }

        /// <summary>The streak capsule: `beads` of 4 filled (gold; the rest white α 0,25), the trowel lit while one is held (UX §5.9: ART §14.5).</summary>
        public void SetStreak(int beads, int trowels, string label, string trowelText)
        {
            streakLabel.text = label;
            var gold = UnityConvert.ToColor(Rgba.Hex(Palette.Gold));
            var empty = new Color(1f, 1f, 1f, 0.25f);
            for (int i = 0; i < Combo4; i++) beadImages[i].color = i < beads ? gold : empty;
            trowelImage.color = trowels > 0 ? gold : new Color(1f, 1f, 1f, 0.35f);
            trowelCount.text = trowelText;
            trowelCount.color = trowels > 0 ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        }

        public ITweenTarget StreakBead(int index) => beadTargets[index];

        /// <summary>A uGUI particle above the open window (the win star burst); pooled.</summary>
        public ISpark AcquireWindowSpark(SparkKind kind, Rgba color, double sizePx)
        {
            var s = uiSparks.Count > 0 ? uiSparks.Pop() : new UnityUiSpark(windowRoot, this);
            s.Configure(kind, color, sizePx);
            return s;
        }

        public void ReleaseWindowSpark(UnityUiSpark spark)
        {
            spark.Visible = false;
            uiSparks.Push(spark);
        }

        /// <summary>The gold chip (JUICE #56); `gold` &lt; 0 hides it.</summary>
        public void SetGold(int gold)
        {
            goldChip.gameObject.SetActive(gold >= 0);
            if (gold >= 0) goldText.text = gold.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public void SetLevel(string label, string name)
        {
            capsuleText.text = label;
            nameText.text = name;
        }

        public void SetMoves(int n, string label, bool danger)
        {
            movesLabel.text = label;
            movesNumber.text = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            movesNumber.color = danger ? UnityConvert.ToColor(Rgba.Hex(Palette.Danger)) : UnityConvert.ToColor(Rgba.Hex(Palette.Ink));
        }

        public void SetBlocks(int n, string label, string segs)
        {
            blocksText.text = n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + label;
            segmentsText.text = segs;
        }

        public void ShowPanel(PanelModel? model, Action<PanelAction> action)
        {
            shown = model;
            onAction = action;
            if (model == null)
            {
                overlay.SetActive(false);
                star.gameObject.SetActive(false);
                return;
            }
            overlay.SetActive(true);
            star.gameObject.SetActive(model.Star);
            var L = layout;
            if (L != null)
            {
                // UX §0.2: anchored to the bottom (layout.popup), relative to the design canvas centre
                var w = HudLayout.Window(L, settings, model.Buttons.Length);
                Centered(panel, w.Panel, L.W / 2, L.H / 2);
                if (model.Star) Centered(star, HudLayout.WindowStar(L, settings, model.Buttons.Length), L.W / 2, L.H / 2);
                PlaceIn(panelTitleRect, w.Title, w.Panel);
                PlaceIn(panelBodyRect, w.Body, w.Panel);
                for (int i = 0; i < model.Buttons.Length && i < MaxButtons; i++) PlaceIn((RectTransform)buttons[i].transform, w.Options[i], w.Panel);
            }
            panelTitle.text = model.Title;
            panelBody.text = model.Body;
            var primary = UnityConvert.ToColor(Rgba.Hex(Palette.Primary));
            var other = UnityConvert.ToColor(Rgba.Hex(Palette.Neutral));
            var ink = UnityConvert.ToColor(Rgba.Hex(Palette.Ink));
            for (int i = 0; i < MaxButtons; i++)
            {
                bool on = i < model.Buttons.Length;
                buttons[i].gameObject.SetActive(on);
                if (!on) continue;
                var b = model.Buttons[i];
                buttonTexts[i].text = b.Label;
                buttonImages[i].color = b.Primary ? primary : other;
                buttonTexts[i].color = b.Primary ? Color.white : ink;
            }
        }

        private void Pressed(int index)
        {
            var model = shown;
            var act = onAction;
            if (model == null || act == null || index >= model.Buttons.Length) return;
            act(model.Buttons[index].Action);
        }

        // --- construction helpers ------------------------------------------------------------------------------------

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private RectTransform Panel(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ProceduralSprites.Get(Tile.Panel);
            img.type = Image.Type.Sliced;
            img.color = color;
            return rt;
        }

        private Text Label(RectTransform parent, string text, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            var rt = parent.GetComponent<Image>() != null ? NewRect("Text", parent) : parent;
            if (rt != parent) Stretch(rt, 12f, 0f);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Fills the parent minus `padX` / `padY` on each side.</summary>
        private static void Stretch(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        /// <summary>`r` (design px) placed in a parent whose own design px rectangle is `parent` (anchored at its centre).</summary>
        private static void PlaceIn(RectTransform rt, PxRect r, PxRect parent) => Centered(rt, r, parent.CenterX, parent.CenterY);

        /// <summary>`r` (design px) in a centre-anchored parent whose centre is the design point (cx, cy).</summary>
        private static void Centered(RectTransform rt, PxRect r, double cx, double cy)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((float)(r.CenterX - cx), (float)(cy - r.CenterY));
            rt.sizeDelta = new Vector2((float)r.W, (float)r.H);
        }

        /// <summary>A design px rectangle (origin top-left of the design canvas, y down); pivot in the middle (pops scale about the centre).</summary>
        private static void Place(RectTransform rt, PxRect r)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((float)r.CenterX, -(float)r.CenterY);
            rt.sizeDelta = new Vector2((float)r.W, (float)r.H);
        }
    }
}
