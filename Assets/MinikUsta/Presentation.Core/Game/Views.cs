#nullable enable
// The seam between the engine-independent level player and the engine (Faz 3 Q1; D-093, D-096). The Unity layer
// (Assets/MinikUsta/Unity) implements these on GameObjects / SpriteRenderers / uGUI; the NUnit tests implement them with
// plain fields. Nothing here decides a rule: the views only show what `LevelPlayer` hands them.
using System;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
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

        /// <summary>Q2: a factor on top of `Scale` (1 = rest); the #12 grow-and-lock pulse runs on it so it does not fight the #11 squash.</summary>
        float Grow { get; set; }

        /// <summary>Q2: number of cells (= the shape's cell count; cell `i` is `ShapeDef.Cells[i]`).</summary>
        int CellCount { get; }

        /// <summary>
        /// Q2 (JUICE #11 stagger): squashes cell `cell` alone, about the bottom centre of the block box so the block keeps its
        /// footing. (1, 1) = rest. `Scale` and `Grow` still apply to the whole block.
        /// </summary>
        void SetCellScale(int cell, float sx, float sy);

        /// <summary>Every cell back to rest (1, 1).</summary>
        void ResetCells();

        /// <summary>V1 (K-14, ART_UNITY §4.1): placed on the site — locked look (tint #EFEFEF + mortar dots, no gloss).</summary>
        bool Placed { get; set; }
    }

    public enum SparkKind
    {
        /// <summary>Four-point sparkle (JUICE fx_spark): placement sparks, gold sparks.</summary>
        Spark,
        /// <summary>Soft dust puff (fx_dust).</summary>
        Dust,
        /// <summary>Five-point star (JUICE #96).</summary>
        Star,
        /// <summary>Bonus İnşaat coin (JUICE #56).</summary>
        Coin,
        /// <summary>Confetti piece (JUICE #18, #55): a small rectangle.</summary>
        Confetti,
        /// <summary>Ring outline (JUICE #96 white ring).</summary>
        Ring,
    }

    /// <summary>A pooled sprite particle (D-096: no ParticleSystem; the field in `ParticleField` moves it).</summary>
    public interface ISpark : ITweenTarget
    {
        bool Visible { get; set; }
    }

    /// <summary>A scenery prop above the board (the delivery truck): shown / hidden, placed by `Position` (its centre), turned by `Rotation`.</summary>
    public interface IPropView : ITweenTarget
    {
        bool Visible { get; set; }

        /// <summary>The cell size (design px) the prop is drawn for: `TruckModel` sizes are in cells.</summary>
        double CellPx { get; set; }
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
        /// <summary>K-29 window 1 "Hamleler bitti!" (with the +5 offer when a host runs the level; Q1: "Tekrar dene" only).</summary>
        Lose,
        /// <summary>V1: UX §4.1 level start window.</summary>
        LevelStart,
        /// <summary>V1: K-43/2 exit confirmation.</summary>
        ExitConfirm,
        /// <summary>V1: UX §7 window 2 (a life is gone).</summary>
        LoseFinal,
    }

    public enum PanelAction
    {
        Resume,
        Restart,
        Next,
        Retry,
        // V1 (flow host)
        Home,
        Settings,
        /// <summary>Pause "Ana sayfa": opens the exit confirmation.</summary>
        Exit,
        Stay,
        Leave,
        BuyMoves,
        AdMoves,
        Decline,
        Play,
        Close,
        Continue,
    }

    /// <summary>V1: the look of a window option (ART_REFERENCE §3.1 kit).</summary>
    public enum ButtonKind
    {
        Primary,
        Secondary,
        Danger,
        Offer,
        Disabled,
    }

    public sealed class PanelButton
    {
        public readonly string Label;
        public readonly PanelAction Action;
        /// <summary>The primary option (green, UX §0.2); the others are neutral.</summary>
        public readonly bool Primary;
        /// <summary>V1: kit look; a `Disabled` option shows but does not fire.</summary>
        public readonly ButtonKind Kind;
        /// <summary>V1: the PriceLabel / ad second line ("≈ ₺81", "Şu an reklam yok").</summary>
        public readonly string? Line2;
        /// <summary>V1: coin price drawn with the coin icon on the right of the option (−1 none).</summary>
        public readonly int Price;

        public PanelButton(string label, PanelAction action, bool primary)
        {
            Label = label;
            Action = action;
            Primary = primary;
            Kind = primary ? ButtonKind.Primary : ButtonKind.Secondary;
            Price = -1;
        }

        public PanelButton(string label, PanelAction action, ButtonKind kind, string? line2 = null, int price = -1)
        {
            Label = label;
            Action = action;
            Kind = kind;
            Primary = kind == ButtonKind.Primary;
            Line2 = line2;
            Price = price;
        }

        public bool Enabled => Kind != ButtonKind.Disabled;
    }

    /// <summary>V1: what a rich window shows beyond title, body and options (set by the flow host).</summary>
    public sealed class PanelDetail
    {
        /// <summary>Extra body lines (exit / loss lines, STORY §7.5).</summary>
        public string[] Lines = new string[0];
        /// <summary>Character frame beside the body (chr_tuna_think, chr_tuna_cheer …); null = none.</summary>
        public string? Character;
        /// <summary>Win: coins, Bonus İnşaat, trowels, stars (META §3.1).</summary>
        public int Coins = -1;
        public int TrowelCoins;
        public int Trowels;
        public string? BonusLine;
        public string? TrowelLine;
        /// <summary>The structure of the level (win card, level start goal, lose preview).</summary>
        public PlanThumb? Thumb;
        /// <summary>Small counter line above the options ("Teklif 1/3").</summary>
        public string? Counter;
        /// <summary>Level start: name, difficulty pill text (null = easy / normal), goal chips.</summary>
        public string? LevelName;
        public string? DifficultyText;
        public int Blocks = -1;
        public string? MovesText;
        /// <summary>Level start: the pre-level booster slots (locked: "Bölüm N").</summary>
        public string[] LockedSlots = new string[0];
        public string? SlotsTitle;
        /// <summary>A close (×) button in the corner firing this action (null = none).</summary>
        public PanelAction? CloseAction;
    }

    /// <summary>A level window (win, out of moves, pause …) as text + options.</summary>
    public sealed class PanelModel
    {
        public readonly PanelKind Kind;
        public readonly string Title;
        public readonly string Body;
        public readonly PanelButton[] Buttons;
        /// <summary>Q2 (META §1): a first win shows the level star on the window (JUICE #96 star burst).</summary>
        public readonly bool Star;
        /// <summary>V1: rich content (null = the Q1 text window).</summary>
        public readonly PanelDetail? Detail;

        public PanelModel(PanelKind kind, string title, string body, PanelButton[] buttons, bool star = false, PanelDetail? detail = null)
        {
            Kind = kind;
            Title = title;
            Body = body;
            Buttons = buttons;
            Star = star;
            Detail = detail;
        }
    }

    /// <summary>
    /// V1: what the HUD shows beyond the counters (refreshed with every `SyncAll`): the K-54 booster slots, the panorama well
    /// (plan of the visible segment, filled cells), the truck chip and the level star.
    /// </summary>
    public sealed class HudState
    {
        /// <summary>UX §5.1 slot order: hammer, crane, brush, undo.</summary>
        public static readonly string[] SlotOrder = { BoosterSlots.Hammer, BoosterSlots.Crane, BoosterSlots.PaintBrush, BoosterSlots.Undo };

        public readonly int Level;
        /// <summary>`SlotStates` value per slot (SlotOrder).</summary>
        public readonly string[] Slots;
        public readonly int[] Counts;
        public readonly int[] UnlockLevels;
        public readonly PlanThumb Thumb;
        /// <summary>Truck chip: queued + pending blocks; `Truck` false hides the chip (no batches in the level).</summary>
        public readonly int Queue;
        public readonly bool Truck;
        /// <summary>The level was won before (the well shows a full star).</summary>
        public readonly bool Starred;

        public HudState(int level, string[] slots, int[] counts, int[] unlockLevels, PlanThumb thumb, int queue, bool truck, bool starred)
        {
            Level = level;
            Slots = slots;
            Counts = counts;
            UnlockLevels = unlockLevels;
            Thumb = thumb;
            Queue = queue;
            Truck = truck;
            Starred = starred;
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

        // --- Q2 additions -----------------------------------------------------------------------------------------------

        /// <summary>A sprite particle of `kind`, `color` and `sizePx` (a pooled sprite; hidden until `Visible` is set).</summary>
        ISpark AcquireSpark(SparkKind kind, Rgba color, double sizePx);

        void ReleaseSpark(ISpark spark);

        /// <summary>The camera offset (design px, y down) the shakes move; the HUD canvas does not shake.</summary>
        ITweenTarget CameraRig { get; }

        /// <summary>The site layer of the board (blueprint, plan, platform): `Position` is its offset (the #18 slide).</summary>
        ITweenTarget SiteLayer { get; }

        /// <summary>The delivery truck (JUICE #19).</summary>
        IPropView Truck { get; }

        /// <summary>The streak capsule (UX §5.1 status strip): `beads` of 4 filled, `trowels` Golden Trowels held (`trowelText` = "×n"), `label` = "Usta Serisi".</summary>
        void SetStreak(int beads, int trowels, string label, string trowelText);

        /// <summary>Bead `i` (0 … 3) of the streak capsule: `Scale` pops (JUICE #15).</summary>
        ITweenTarget StreakBead(int index);

        /// <summary>The Golden Trowel icon of the streak capsule (JUICE #16: pop and spin).</summary>
        ITweenTarget TrowelIcon { get; }

        /// <summary>The gold chip (JUICE #56 coins fly there); `gold` &lt; 0 hides it.</summary>
        void SetGold(int gold);

        ITweenTarget GoldChip { get; }

        /// <summary>The level star on the open window (JUICE #96 central star: 0 → 1,2 → 1).</summary>
        ITweenTarget StarTarget { get; }

        // --- V1 additions -----------------------------------------------------------------------------------------------

        /// <summary>The board, blocks and HUD are shown (false on the home screen).</summary>
        bool WorldVisible { get; set; }

        /// <summary>Booster slots, panorama well, truck chip, level star (after every `SyncAll`).</summary>
        void SetHudState(HudState state);

        /// <summary>Centre (design px) of the open window's star (JUICE #96 burst); null = no star shown.</summary>
        Vec2? StarCenter { get; }
    }
}
