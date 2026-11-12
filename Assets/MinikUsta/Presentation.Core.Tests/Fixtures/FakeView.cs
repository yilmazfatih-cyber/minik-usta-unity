#nullable enable
// A plain-field `ILevelView` for the level player tests (the Unity layer's GameObjects / SpriteRenderers / uGUI stand-in):
// it records what the player shows, nothing more.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tweening;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public sealed class FakePiece : IPieceVisual
    {
        public FakePiece(int id, ShapeDef shape, int color)
        {
            PieceId = id;
            Shape = shape;
            Color = color;
        }

        public int PieceId { get; set; }
        public ShapeDef Shape;
        public int Color;
        public bool Dim;
        public bool Placed { get; set; }
        public bool InPool;
        public Vec2 Position { get; set; }
        public Vec2 Scale { get; set; } = Vec2.One;
        public float Rotation { get; set; }
        public float Alpha { get; set; } = 1f;
        public bool Visible { get; set; } = true;
        public bool Lifted { get; set; }
        public bool Raised { get; set; }
        public float Flash { get; set; }
        public float Tint { get; set; }
        public float Hop { get; set; }
        public float Grow { get; set; } = 1f;
        public float LiftHeight { get; set; }
        public int CellCount => Shape.Cells.Length;
        /// <summary>Per-cell squash (x, y) as `SetCellScale` left it; (1, 1) = rest.</summary>
        public Vec2[] CellScales = new Vec2[0];
        /// <summary>The smallest cell scale ever set (tests: the landing wave happened).</summary>
        public int CellScaleCalls;

        public void SetLook(int colorIndex, bool dim)
        {
            Color = colorIndex;
            Dim = dim;
        }

        /// <summary>The v3 traits as `SetTraits` left them (E3).</summary>
        public BlockTraits Traits = BlockTraits.None;
        private float traitFx;
        public float TraitFx
        {
            get => traitFx;
            set
            {
                traitFx = value;
                if (value > MaxTraitFx) MaxTraitFx = value;
            }
        }
        /// <summary>The largest `TraitFx` ever set (tests: an unwrap / bonus release played).</summary>
        public float MaxTraitFx;

        public void SetTraits(BlockTraits traits)
        {
            Traits = traits;
        }

        public void SetCellScale(int cell, float sx, float sy)
        {
            if (CellScales.Length != CellCount)
            {
                CellScales = new Vec2[CellCount];
                for (int i = 0; i < CellScales.Length; i++) CellScales[i] = Vec2.One;
            }
            CellScales[cell] = new Vec2(sx, sy);
            CellScaleCalls++;
        }

        public void ResetCells()
        {
            for (int i = 0; i < CellScales.Length; i++) CellScales[i] = Vec2.One;
        }
    }

    public sealed class FakeSpark : ISpark
    {
        public SparkKind Kind;
        public Rgba Color;
        public double SizePx;
        public bool InPool;
        public Vec2 Position { get; set; }
        public Vec2 Scale { get; set; } = Vec2.One;
        public float Rotation { get; set; }
        public float Alpha { get; set; } = 1f;
        public bool Visible { get; set; }
        private int cel;

        public int Cel
        {
            get => cel;
            set
            {
                cel = value;
                if (value > MaxCel) MaxCel = value;
            }
        }

        /// <summary>The highest cel set (tests: the 3D confetti / coin turned).</summary>
        public int MaxCel;
    }

    /// <summary>B2: a pooled FX sprite: records what `FxPlayer` showed on it.</summary>
    public sealed class FakeFx : IFxSprite
    {
        public FakeFx(FxDepth depth)
        {
            Depth = depth;
        }

        public FxDepth Depth { get; }
        public bool InPool;
        public string? Frame;
        public Rgba Tint = Rgba.White;
        public readonly List<string> Frames = new List<string>();
        public Vec2 Position { get; set; }
        public Vec2 Scale { get; set; } = Vec2.One;
        public float Rotation { get; set; }
        public float Alpha { get; set; } = 1f;
        public bool Visible { get; set; }

        public void Show(string frame, Rgba tint)
        {
            Frame = frame;
            Tint = tint;
            if (Frames.Count == 0 || Frames[Frames.Count - 1] != frame) Frames.Add(frame);
        }
    }

    public sealed class FakeFlyer : IFlyer
    {
        public Vec2 Position { get; set; }
        public Vec2 Scale { get; set; } = Vec2.One;
        public float Rotation { get; set; }
        public float Alpha { get; set; } = 1f;
        public bool Visible { get; set; }
    }

    public sealed class FakeView : ILevelView, ILevelHudHost
    {
        public readonly Dictionary<int, FakePiece> Live = new Dictionary<int, FakePiece>();
        public readonly List<FakePiece> Pool = new List<FakePiece>();
        public readonly List<FakeFlyer> FlyersOut = new List<FakeFlyer>();
        public int PiecesCreated;
        public int BoardBuilds;
        public BoardScene? Board;
        public ShadowModel? Shadow;
        public int ShadowShows;
        public DesignViewport? Viewport;
        public ScreenLayout? Layout;
        public string LevelLabel = "";
        public string LevelName = "";
        public int Moves = -1;
        public string MovesLabel = "";
        public bool Danger;
        public int Blocks = -1;
        public string BlocksLabel = "";
        public string Segments = "";
        public readonly List<int> MovesShown = new List<int>();
        public PanelModel? Panel;
        public Action<PanelAction>? OnAction;
        public FakeFlyer? LastFlyer;

        // Q2
        public readonly List<FakeSpark> SparksOut = new List<FakeSpark>();
        public readonly List<FakeSpark> SparkLog = new List<FakeSpark>();
        public int SparksAcquired;
        public int StreakBeads = -1;
        public int StreakTrowels = -1;
        public string StreakLabel = "";
        public string TrowelText = "";
        public int Gold = -1;
        public readonly List<int> GoldShown = new List<int>();
        /// <summary>The largest camera offset ever seen (tests: the shake moved the rig).</summary>
        public float MaxCameraOffset;

        public ITweenTarget MovesChip { get; } = new FakeTarget();
        public ITweenTarget BlocksChip { get; } = new FakeTarget();
        public ITweenTarget MovesBurst { get; } = new FakeTarget();
        /// <summary>P4: the "+n" burst text now (null = hidden) and every text it showed.</summary>
        public string? MovesBurstText;
        public readonly List<string> MovesBurstShown = new List<string>();
        public void SetMovesBurst(string? text)
        {
            MovesBurstText = string.IsNullOrEmpty(text) ? null : text;
            if (MovesBurstText != null) MovesBurstShown.Add(MovesBurstText);
            Hud?.SetMovesBurst(text);
        }
        public ITweenTarget PanelTarget { get; } = new FakeTarget();
        public ITweenTarget CameraRig { get; } = new FakeTarget();
        public ITweenTarget SiteLayer { get; } = new FakeTarget();
        public ITweenTarget TrowelIcon { get; } = new FakeTarget();
        public ITweenTarget GoldChip { get; } = new FakeTarget();
        public ITweenTarget StarTarget { get; } = new FakeTarget();
        public readonly FakeTarget[] Beads = { new FakeTarget(), new FakeTarget(), new FakeTarget(), new FakeTarget() };

        public ITweenTarget StreakBead(int index) => Beads[index];

        // V1: the real HUD tree (optional): every HUD call is also forwarded to it (GameApp / HUD tests with a FakeSurface)
        public LevelHud? Hud;

        public LevelHud? HudOrNull => Hud;
        public bool WorldVisible { get; set; } = true;
        public HudState? HudState;
        public int HudStates;

        public void SetHudState(HudState state)
        {
            HudState = state;
            HudStates++;
            Hud?.SetHudState(state);
        }

        public Vec2? StarCenter => Hud?.StarCenter;

        // B2
        public readonly List<FakeFx> FxOut = new List<FakeFx>();
        public readonly List<FakeFx> FxLog = new List<FakeFx>();
        public KingDirector? King;

        public IFxSprite AcquireFx(FxDepth depth)
        {
            var f = new FakeFx(depth);
            FxOut.Add(f);
            FxLog.Add(f);
            return f;
        }

        public void ReleaseFx(IFxSprite fx)
        {
            var f = (FakeFx)fx;
            if (f.InPool || !FxOut.Remove(f)) throw new InvalidOperationException("fx released twice");
            f.InPool = true;
        }

        public void SetKing(KingDirector? king)
        {
            King = king;
            if (Hud != null) Hud.King = king;
        }

        public ISpark AcquireSpark(SparkKind kind, Rgba color, double sizePx)
        {
            var s = new FakeSpark { Kind = kind, Color = color, SizePx = sizePx };
            SparksOut.Add(s);
            SparkLog.Add(s);
            SparksAcquired++;
            return s;
        }

        public void ReleaseSpark(ISpark spark)
        {
            var s = (FakeSpark)spark;
            if (s.InPool || !SparksOut.Remove(s)) throw new InvalidOperationException("spark released twice");
            s.InPool = true;
        }

        public void SetStreak(int beads, int trowels, string label, string trowelText)
        {
            StreakBeads = beads;
            StreakTrowels = trowels;
            StreakLabel = label;
            TrowelText = trowelText;
            Hud?.SetStreak(beads, trowels, label, trowelText);
        }

        public void SetGold(int gold)
        {
            Gold = gold;
            GoldShown.Add(gold);
            Hud?.SetGold(gold);
        }

        public void SetViewport(DesignViewport viewport, ScreenLayout layout)
        {
            Viewport = viewport;
            Layout = layout;
            Hud?.Layout(layout);
        }

        public void BuildBoard(BoardScene board)
        {
            Board = board;
            BoardBuilds++;
        }

        public IPieceVisual AcquirePiece(int pieceId, ShapeDef shape, int colorIndex, double cellPx)
        {
            if (Live.ContainsKey(pieceId)) throw new InvalidOperationException("piece " + pieceId + " acquired twice");
            FakePiece p;
            if (Pool.Count > 0)
            {
                p = Pool[Pool.Count - 1];
                Pool.RemoveAt(Pool.Count - 1);
                p.PieceId = pieceId;
                p.Shape = shape;
                p.Color = colorIndex;
            }
            else
            {
                p = new FakePiece(pieceId, shape, colorIndex);
                PiecesCreated++;
            }
            p.InPool = false;
            Live[pieceId] = p;
            return p;
        }

        public void ReleasePiece(IPieceVisual piece)
        {
            var p = (FakePiece)piece;
            if (p.InPool) throw new InvalidOperationException("piece " + p.PieceId + " released twice");
            Live.Remove(p.PieceId);
            p.InPool = true;
            Pool.Add(p);
        }

        public IFlyer AcquireFlyer(int colorIndex, double sizePx)
        {
            var f = new FakeFlyer();
            FlyersOut.Add(f);
            LastFlyer = f;
            return f;
        }

        public void ReleaseFlyer(IFlyer flyer)
        {
            if (!FlyersOut.Remove((FakeFlyer)flyer)) throw new InvalidOperationException("flyer released twice");
        }

        public void ShowShadow(ShadowModel? shadow)
        {
            Shadow = shadow;
            if (shadow != null) ShadowShows++;
        }

        public void SetLevel(string label, string name)
        {
            LevelLabel = label;
            LevelName = name;
            Hud?.SetLevel(label, name);
        }

        public void SetMoves(int movesLeft, string label, bool danger)
        {
            Moves = movesLeft;
            MovesLabel = label;
            Danger = danger;
            MovesShown.Add(movesLeft);
            Hud?.SetMoves(movesLeft, label, danger);
        }

        public void SetBlocks(int blocksLeft, string label, string segments)
        {
            Blocks = blocksLeft;
            BlocksLabel = label;
            Segments = segments;
            Hud?.SetBlocks(blocksLeft, label, segments);
        }

        public void ShowPanel(PanelModel? panel, Action<PanelAction> onAction)
        {
            Panel = panel;
            OnAction = onAction;
            Hud?.ShowPanel(panel, onAction);
        }

        /// <summary>Presses the option with `action` of the open window (fails when there is none).</summary>
        public void Press(PanelAction action)
        {
            if (Panel == null) throw new InvalidOperationException("no window open");
            foreach (var b in Panel.Buttons)
                if (b.Action == action)
                {
                    OnAction!(action);
                    return;
                }
            throw new InvalidOperationException("the " + Panel.Kind + " window has no " + action + " option");
        }
    }
}
