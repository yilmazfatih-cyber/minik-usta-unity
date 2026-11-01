#nullable enable
// `ILevelView` on Unity objects (Faz 3 Q1; paket V1 v4 art): the orthographic camera (FIT of the 1080 × 1920 design canvas),
// the static board, the pooled block visuals and flyers, the K-18 shadow, the particles and the truck as SpriteRenderers on
// the atlas; the HUD and the level windows are the engine-independent `LevelHud` drawn by the shared `UiRenderer`. It holds no
// game logic: `LevelPlayer` (Presentation.Core) decides what is shown, this only draws it.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Text;
using MinikUsta.Presentation.Tweening;
using MinikUsta.Presentation.Ui;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class UnityLevelView : ILevelView
    {
        private readonly DesignSpace space = new DesignSpace();
        private readonly Camera camera;
        private readonly Transform world;
        private readonly UnityBoardView board;
        private readonly UnityShadowView shadow;
        private readonly UiRenderer ui;
        private readonly Transform pieceRoot;
        private readonly Stack<UnityPieceVisual> piecePool = new Stack<UnityPieceVisual>();
        private readonly Stack<UnityFlyer> flyerPool = new Stack<UnityFlyer>();
        private readonly Stack<UnitySpark> sparkPool = new Stack<UnitySpark>();
        private readonly Stack<UnityUiSpark> uiSparks = new Stack<UnityUiSpark>();
        private readonly Transform sparkRoot;
        private readonly UnityTruckView truck;

        public UnityLevelView(Camera camera, UiRenderer ui, Strings text, Action onPause, LayoutSettings settings, FeelSettings feel)
        {
            this.camera = camera;
            this.ui = ui;
            AtlasSprites.Load();
            world = new GameObject("World").transform;
            var boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(world, false);
            pieceRoot = new GameObject("Pieces").transform;
            pieceRoot.SetParent(world, false);
            var shadowRoot = new GameObject("Shadow").transform;
            shadowRoot.SetParent(world, false);
            sparkRoot = new GameObject("Particles").transform;
            sparkRoot.SetParent(world, false);
            board = new UnityBoardView(space, boardRoot);
            shadow = new UnityShadowView(space, shadowRoot);
            truck = new UnityTruckView(space, world);
            Hud = new LevelHud(ui, text, settings, onPause);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UnityConvert.ToColor(Rgba.Hex(Palette.GameBottom));
            camera.transform.position = new Vector3(0f, 0f, -10f);
            CameraRig = new OffsetTarget(camera.transform);
        }

        /// <summary>The HUD and level windows (engine-independent, drawn by the UiRenderer).</summary>
        public LevelHud Hud { get; }

        public void SetViewport(DesignViewport viewport, ScreenLayout layout)
        {
            space.Set(viewport);
            camera.orthographicSize = (float)viewport.OrthographicSize(DesignSpace.PixelsPerUnit);
            ui.SetCanvas(viewport.W, viewport.H);
            Hud.Layout(layout);
        }

        public void BuildBoard(BoardScene scene) => board.Build(scene);

        public IPieceVisual AcquirePiece(int pieceId, ShapeDef shape, int colorIndex, double cellPx)
        {
            var v = piecePool.Count > 0 ? piecePool.Pop() : new UnityPieceVisual(space, pieceRoot);
            v.Configure(pieceId, shape, colorIndex, cellPx);
            return v;
        }

        public void ReleasePiece(IPieceVisual piece)
        {
            var v = (UnityPieceVisual)piece;
            v.Park();
            piecePool.Push(v);
        }

        public IFlyer AcquireFlyer(int colorIndex, double sizePx)
        {
            var f = flyerPool.Count > 0 ? flyerPool.Pop() : new UnityFlyer(space, world);
            f.Configure(colorIndex, sizePx);
            return f;
        }

        public void ReleaseFlyer(IFlyer flyer)
        {
            var f = (UnityFlyer)flyer;
            f.Visible = false;
            flyerPool.Push(f);
        }

        public void ShowShadow(ShadowModel? model) => shadow.Show(model);

        public void SetLevel(string label, string name) => Hud.SetLevel(label, name);

        public void SetMoves(int movesLeft, string label, bool danger) => Hud.SetMoves(movesLeft, label, danger);

        public void SetBlocks(int blocksLeft, string label, string segments) => Hud.SetBlocks(blocksLeft, label, segments);

        public ITweenTarget MovesChip => Hud.MovesChip;

        public ITweenTarget BlocksChip => Hud.BlocksChip;

        public void ShowPanel(PanelModel? panel, Action<PanelAction> onAction) => Hud.ShowPanel(panel, onAction);

        public ITweenTarget PanelTarget => Hud.PanelTarget;

        // --- Q2 --------------------------------------------------------------------------------------------------------------

        public ISpark AcquireSpark(SparkKind kind, Rgba color, double sizePx)
        {
            // the win star burst must show above the window: uGUI particles on the Fx layer (the overlay canvas draws over every sprite)
            if (kind == SparkKind.Star || kind == SparkKind.Ring)
            {
                var u = uiSparks.Count > 0 ? uiSparks.Pop() : new UnityUiSpark(ui.Layer(UiLayer.Fx), ui);
                u.Configure(kind, color, sizePx);
                return u;
            }
            var s = sparkPool.Count > 0 ? sparkPool.Pop() : new UnitySpark(space, sparkRoot);
            s.Configure(kind, color, sizePx);
            return s;
        }

        public void ReleaseSpark(ISpark spark)
        {
            if (spark is UnityUiSpark u)
            {
                u.Visible = false;
                uiSparks.Push(u);
                return;
            }
            var s = (UnitySpark)spark;
            s.Visible = false;
            sparkPool.Push(s);
        }

        public ITweenTarget CameraRig { get; }

        public ITweenTarget SiteLayer => board.SiteLayer;

        public IPropView Truck => truck;

        public void SetStreak(int beads, int trowels, string label, string trowelText) => Hud.SetStreak(beads, trowels, label, trowelText);

        public ITweenTarget StreakBead(int index) => Hud.StreakBead(index);

        public ITweenTarget TrowelIcon => Hud.TrowelIcon;

        public void SetGold(int gold) => Hud.SetGold(gold);

        public ITweenTarget GoldChip => Hud.GoldChip;

        public ITweenTarget StarTarget => Hud.StarTarget;

        // --- V1 ----------------------------------------------------------------------------------------------------------------

        public bool WorldVisible
        {
            get => world.gameObject.activeSelf;
            set => world.gameObject.SetActive(value);
        }

        public void SetHudState(HudState state) => Hud.SetHudState(state);

        public Vec2? StarCenter => Hud.StarCenter;
    }
}
