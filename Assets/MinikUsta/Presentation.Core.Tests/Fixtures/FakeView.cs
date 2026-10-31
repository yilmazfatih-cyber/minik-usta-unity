#nullable enable
// A plain-field `ILevelView` for the level player tests (the Unity layer's GameObjects / SpriteRenderers / uGUI stand-in):
// it records what the player shows, nothing more.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tweening;

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

        public void SetLook(int colorIndex, bool dim)
        {
            Color = colorIndex;
            Dim = dim;
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

    public sealed class FakeView : ILevelView
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

        public ITweenTarget MovesChip { get; } = new FakeTarget();
        public ITweenTarget BlocksChip { get; } = new FakeTarget();
        public ITweenTarget PanelTarget { get; } = new FakeTarget();

        public void SetViewport(DesignViewport viewport, ScreenLayout layout)
        {
            Viewport = viewport;
            Layout = layout;
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
        }

        public void SetMoves(int movesLeft, string label, bool danger)
        {
            Moves = movesLeft;
            MovesLabel = label;
            Danger = danger;
            MovesShown.Add(movesLeft);
        }

        public void SetBlocks(int blocksLeft, string label, string segments)
        {
            Blocks = blocksLeft;
            BlocksLabel = label;
            Segments = segments;
        }

        public void ShowPanel(PanelModel? panel, Action<PanelAction> onAction)
        {
            Panel = panel;
            OnAction = onAction;
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
