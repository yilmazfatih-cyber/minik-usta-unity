#nullable enable
// `ILevelView` on Unity objects (Faz 3 Q1): the orthographic camera (FIT of the 1080 × 1920 design canvas), the static
// board, the pooled block visuals and flyers, the K-18 shadow and the uGUI HUD. It holds no game logic: `LevelPlayer`
// (Presentation.Core) decides what is shown, this only draws it.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tweening;
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
        private readonly UnityHud hud;
        private readonly Transform pieceRoot;
        private readonly Stack<UnityPieceVisual> piecePool = new Stack<UnityPieceVisual>();
        private readonly Stack<UnityFlyer> flyerPool = new Stack<UnityFlyer>();
        private readonly Stack<UnitySpark> sparkPool = new Stack<UnitySpark>();
        private readonly Transform sparkRoot;
        private readonly UnityTruckView truck;

        public UnityLevelView(Camera camera, Action onPause, LayoutSettings settings, FeelSettings feel)
        {
            this.camera = camera;
            ProceduralSprites.Build();
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
            hud = new UnityHud(onPause, settings, feel);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UnityConvert.ToColor(Rgba.Hex(Palette.GameBottom));
            camera.transform.position = new Vector3(0f, 0f, -10f);
            CameraRig = new OffsetTarget(camera.transform);
        }

        public void SetViewport(DesignViewport viewport, ScreenLayout layout)
        {
            space.Set(viewport);
            camera.orthographicSize = (float)viewport.OrthographicSize(DesignSpace.PixelsPerUnit);
            hud.Layout(viewport, layout);
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

        public void SetLevel(string label, string name) => hud.SetLevel(label, name);

        public void SetMoves(int movesLeft, string label, bool danger) => hud.SetMoves(movesLeft, label, danger);

        public void SetBlocks(int blocksLeft, string label, string segments) => hud.SetBlocks(blocksLeft, label, segments);

        public ITweenTarget MovesChip => hud.MovesChip;

        public ITweenTarget BlocksChip => hud.BlocksChip;

        public void ShowPanel(PanelModel? panel, Action<PanelAction> onAction) => hud.ShowPanel(panel, onAction);

        public ITweenTarget PanelTarget => hud.PanelTarget;

        // --- Q2 --------------------------------------------------------------------------------------------------------------------

        public ISpark AcquireSpark(SparkKind kind, Rgba color, double sizePx)
        {
            // the win star burst must show above the window: uGUI particles (the overlay canvas draws over every sprite)
            if (kind == SparkKind.Star || kind == SparkKind.Ring) return hud.AcquireWindowSpark(kind, color, sizePx);
            var s = sparkPool.Count > 0 ? sparkPool.Pop() : new UnitySpark(space, sparkRoot);
            s.Configure(kind, color, sizePx);
            return s;
        }

        public void ReleaseSpark(ISpark spark)
        {
            if (spark is UnityUiSpark ui)
            {
                hud.ReleaseWindowSpark(ui);
                return;
            }
            var s = (UnitySpark)spark;
            s.Visible = false;
            sparkPool.Push(s);
        }

        public ITweenTarget CameraRig { get; }

        public ITweenTarget SiteLayer => board.SiteLayer;

        public IPropView Truck => truck;

        public void SetStreak(int beads, int trowels, string label, string trowelText) => hud.SetStreak(beads, trowels, label, trowelText);

        public ITweenTarget StreakBead(int index) => hud.StreakBead(index);

        public ITweenTarget TrowelIcon => hud.TrowelIcon;

        public void SetGold(int gold) => hud.SetGold(gold);

        public ITweenTarget GoldChip => hud.GoldChip;

        public ITweenTarget StarTarget => hud.StarTarget;
    }
}
