#nullable enable
// The seam between the engine-independent level player and the engine (Faz 3 Q1; D-093, D-096). The Unity layer
// (Assets/MinikUsta/Unity) implements these on GameObjects / SpriteRenderers / uGUI; the NUnit tests implement them with
// plain fields. Nothing here decides a rule: the views only show what `LevelPlayer` hands them.
using System;
using MinikUsta.Core;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Game
{
    /// <summary>
    /// The visual of one block (Unity: a pooled group of cell sprites). `Position` = centre of the piece box (design px, y
    /// down); `Scale` about that centre; `Rotation` degrees counter-clockwise; `Alpha` multiplies every sprite.
    /// </summary>
    public interface IPieceVisual : ITweenTarget
    {
        int PieceId { get; }
        bool Visible { get; set; }
        /// <summary>JUICE #1 lifted look: the lifted drop shadow (`shadow.lifted`) under the block.</summary>
        bool Lifted { get; set; }
        /// <summary>Drawn above the resting blocks (drag, flights).</summary>
        bool Raised { get; set; }
        /// <summary>0 … 1 white flash (#12 "kilit parlaması", #2 blockers, #55 site glow).</summary>
        float Flash { get; set; }
        /// <summary>0 … 1 red tint (#13 wrong placement, color.ghost.invalid).</summary>
        float Tint { get; set; }
        /// <summary>Extra upward offset (design px): JUICE #1 hop, UX §5.3 tap hop.</summary>
        float Hop { get; set; }
        /// <summary>Colour (K-38 brush, W6 paint gate) and state look: `dim` = not holdable (K-09, blockV2.notHoldableTint).</summary>
        void SetLook(int colorIndex, bool dim);
    }

    /// <summary>A small icon flying to a HUD chip (D-091 blocks-left chip flight).</summary>
    public interface IFlyer : ITweenTarget
    {
        bool Visible { get; set; }
    }

    public enum PanelKind
    {
        None,
        Pause,
        Win,
        Lose,
    }

    public enum PanelAction
    {
        Resume,
        Restart,
        Next,
        Retry,
    }

    public sealed class PanelButton
    {
        public readonly string Label;
        public readonly PanelAction Action;
        /// <summary>The primary option (green, UX §0.2); the others are neutral.</summary>
        public readonly bool Primary;

        public PanelButton(string label, PanelAction action, bool primary)
        {
            Label = label;
            Action = action;
            Primary = primary;
        }
    }

    /// <summary>A level window (win, out of moves, pause) as text + options.</summary>
    public sealed class PanelModel
    {
        public readonly PanelKind Kind;
        public readonly string Title;
        public readonly string Body;
        public readonly PanelButton[] Buttons;

        public PanelModel(PanelKind kind, string title, string body, PanelButton[] buttons)
        {
            Kind = kind;
            Title = title;
            Body = body;
            Buttons = buttons;
        }
    }

    /// <summary>The level screen the engine draws. Calls come only from `LevelPlayer` (one thread, the frame loop).</summary>
    public interface ILevelView
    {
        /// <summary>The screen ↔ design mapping (camera size, canvas) and the screen layout for HUD rectangles.</summary>
        void SetViewport(DesignViewport viewport, ScreenLayout layout);

        /// <summary>(Re)draws the static board: every item of `board`, the previous ones are returned to the pool.</summary>
        void BuildBoard(BoardScene board);

        IPieceVisual AcquirePiece(int pieceId, ShapeDef shape, int colorIndex, double cellPx);
        void ReleasePiece(IPieceVisual piece);

        IFlyer AcquireFlyer(int colorIndex, double sizePx);
        void ReleaseFlyer(IFlyer flyer);

        /// <summary>The K-18 shadow of the dragged block; null hides it.</summary>
        void ShowShadow(ShadowModel? shadow);

        void SetLevel(string label, string name);
        void SetMoves(int movesLeft, string label, bool danger);
        void SetBlocks(int blocksLeft, string label, string segments);

        /// <summary>Scale targets of the HUD chips (pops, pulses); `Position` is not used.</summary>
        ITweenTarget MovesChip { get; }
        ITweenTarget BlocksChip { get; }

        /// <summary>Shows a window (null hides); a button press calls `onAction`.</summary>
        void ShowPanel(PanelModel? panel, Action<PanelAction> onAction);

        /// <summary>The open window (JUICE #70 pop: scale).</summary>
        ITweenTarget PanelTarget { get; }
    }
}
