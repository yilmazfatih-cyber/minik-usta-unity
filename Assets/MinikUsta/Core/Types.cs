#nullable enable
// Port of src/core/types.ts (web repo, source of truth). Behaviour and names follow the TS file line by line.
using System;
using System.Collections.Generic;

namespace MinikUsta.Core
{
    /// <summary>
    /// Cross-cutting core types (docs/TECH_DESIGN.md §2.1, §5.1, §6.1, §6.3). Rules: docs/GDD.md.
    /// Pure data declarations: no runtime dependencies.
    /// </summary>
    public static class ColorCodes
    {
        /// <summary>Block colours (BRIEF §6). The array order is the internal index 0..7 and the D2 help colour order (TECH §9.7).</summary>
        public static readonly string[] All = { "W", "Y", "G", "R", "O", "C", "B", "P" };

        /// <summary>`COLOR_CODES.indexOf(c)`: 0..7, or −1 for an unknown code.</summary>
        public static int IndexOf(string? code)
        {
            if (code == null) return -1;
            for (int i = 0; i < All.Length; i++) if (All[i] == code) return i;
            return -1;
        }

        public static bool IsColor(string? code) => IndexOf(code) >= 0;
    }

    public static class ShapeKinds
    {
        /// <summary>Shape kinds (BRIEF §5). The array order fixes the shape index: `kindIndex * 4 + rotationIndex`.</summary>
        public static readonly string[] All =
        {
            "B1", "D2", "I3", "I4", "O4", "C3", "L4", "J4", "T4", "S4", "Z4", "I5", "Q9",
        };

        public static int IndexOf(string kind) => Array.IndexOf(All, kind);
    }

    public static class Rotations
    {
        /// <summary>Clockwise rotation in degrees (GDD K-44, D-051).</summary>
        public static readonly int[] All = { 0, 90, 180, 270 };

        public static int IndexOf(int rotation) => Array.IndexOf(All, rotation);
    }

    /// <summary>
    /// Where a piece is. Stored as an int in the state buffer (TECH §2.1). `pending` = truck block whose batch is not queued
    /// yet; it keeps undelivered supply apart from destroyed (`gone`) blocks (K-30 D2, L-19 K-27).
    /// </summary>
    public static class Zone
    {
        public const int Yard = 0;
        public const int Site = 1;
        public const int Queue = 2;
        public const int Gone = 3;
        public const int Pending = 4;

        /// <summary>`ZoneName` by zone value (TS `Object.keys(Zone)` order).</summary>
        public static readonly string[] Names = { "yard", "site", "queue", "gone", "pending" };
    }

    /// <summary>Bottom-left corner of a piece's bounding box, global coordinates (internal = global, R-03).</summary>
    public readonly struct Anchor : IEquatable<Anchor>
    {
        public readonly int Ix;
        public readonly int Iy;

        public Anchor(int ix, int iy)
        {
            Ix = ix;
            Iy = iy;
        }

        public bool Equals(Anchor other) => Ix == other.Ix && Iy == other.Iy;
        public override bool Equals(object? obj) => obj is Anchor a && Equals(a);
        public override int GetHashCode() => Ix * 31 + Iy;
        public override string ToString() => "(" + Ix + "," + Iy + ")";
    }

    /// <summary>Drag mode: 0 = FREE, 1 + gapIndex = RAIL(gap) (TECH §2.1, §4.2).</summary>
    public static class DragMode
    {
        public const int Free = 0;
    }

    /// <summary>A drag graph node. Integer code: `(mode * 10 + iy) * 8 + ix` (core/coords `DragNodeCode`).</summary>
    public readonly struct DragNode : IEquatable<DragNode>
    {
        public readonly int Ix;
        public readonly int Iy;
        public readonly int Mode;

        public DragNode(int ix, int iy, int mode)
        {
            Ix = ix;
            Iy = iy;
            Mode = mode;
        }

        public bool Equals(DragNode other) => Ix == other.Ix && Iy == other.Iy && Mode == other.Mode;
        public override bool Equals(object? obj) => obj is DragNode n && Equals(n);
        public override int GetHashCode() => (Mode * 10 + Iy) * 8 + Ix;
        public override string ToString() => Mode + ":" + Ix + "," + Iy;
    }

    /// <summary>Piece flag names (state `flags` field bits in this order) — TS `PIECE_FLAGS` / `DATA_PIECE_FLAGS`.</summary>
    public static class PieceFlags
    {
        public const string Glass = "glass";
        public const string Mortar = "mortar";
        public const string Balloon = "balloon";
        public const string Chained = "chained";
        public const string Wet = "wet";
        public const string Locked = "locked";
        public const string Debris = "debris";
        public const string Stuck = "stuck";

        /// <summary>Block flags written in level data (GDD K-21).</summary>
        public static readonly string[] DataFlags = { Glass, Mortar, Balloon, Chained, Wet };

        /// <summary>All runtime piece flags: data flags + `debris` (S4, added by compile) + `locked` (K-14) + `stuck` (Y8).</summary>
        public static readonly string[] All = { Glass, Mortar, Balloon, Chained, Wet, Locked, Debris, Stuck };

        public static int IndexOf(string flag) => Array.IndexOf(All, flag);
    }

    /// <summary>A position in events and views; GLOBAL coordinates (TECH §6.3). `Zone` is "yard" or "site".</summary>
    public readonly struct At : IEquatable<At>
    {
        public const string ZoneYard = "yard";
        public const string ZoneSite = "site";

        public readonly string Zone;
        public readonly int X;
        public readonly int Y;
        /// <summary>Segment of a site position; null for yard positions (TS optional `seg`).</summary>
        public readonly int? Seg;

        public At(string zone, int x, int y, int? seg = null)
        {
            Zone = zone;
            X = x;
            Y = y;
            Seg = seg;
        }

        public static At Yard(int x, int y) => new At(ZoneYard, x, y);
        public static At Site(int x, int y, int seg) => new At(ZoneSite, x, y, seg);

        public bool Equals(At other) => Zone == other.Zone && X == other.X && Y == other.Y && Seg == other.Seg;
        public override bool Equals(object? obj) => obj is At a && Equals(a);
        public override int GetHashCode() => (Zone == ZoneSite ? 1 : 0) + 2 * (X + 8 * (Y + 16 * (Seg ?? 7)));
        public override string ToString() => Zone + "(" + X + "," + Y + (Seg.HasValue ? "," + Seg.Value : "") + ")";
    }

    /// <summary>GDD K-34 visibility hook 2: fixed order, `reasons[0]` is the primary reason.</summary>
    public static class VerdictReasons
    {
        public const string Debris = "debris";
        public const string Outside = "outside";
        public const string Window = "window";
        public const string Color = "color";
        public const string Support = "support";

        public static readonly string[] All = { Debris, Outside, Window, Color, Support };
    }

    /// <summary>`Ok ⇔ Reasons.Length == 0` (TECH §5.1).</summary>
    public sealed class Verdict
    {
        public readonly bool Ok;
        public readonly string[] Reasons;
        public readonly At[] MissingSupport;

        public Verdict(bool ok, string[] reasons, At[] missingSupport)
        {
            Ok = ok;
            Reasons = reasons;
            MissingSupport = missingSupport;
        }
    }

    /// <summary>G-L steering input (GDD K-19, S-26 record format). `Dir` is −1 or 1.</summary>
    public readonly struct Steer
    {
        public readonly int Dir;
        public readonly int AtRow;

        public Steer(int dir, int atRow)
        {
            Dir = dir;
            AtRow = atRow;
        }
    }

    /// <summary>K-36 hammer target: a piece (Ağır Yük, chained block, site debris, stuck mortar) or an obstacle (crate, bag).</summary>
    public readonly struct HammerTarget
    {
        public readonly int? PieceId;
        public readonly int? Obstacle;

        private HammerTarget(int? pieceId, int? obstacle)
        {
            PieceId = pieceId;
            Obstacle = obstacle;
        }

        public static HammerTarget Piece(int pieceId) => new HammerTarget(pieceId, null);
        public static HammerTarget ObstacleAt(int obstacle) => new HammerTarget(null, obstacle);
    }

    /// <summary>Move log entry kinds (TECH §6.1, §11.1): the core moves, plus the session actions `undo` and `start`.</summary>
    public abstract class SessionAction
    {
        /// <summary>TS `kind` literal: drag, hammer, crane, paint, goldTrowel, trowel, addMoves, undo, start.</summary>
        public abstract string Kind { get; }
    }

    public enum MoveKind
    {
        Drag,
        Hammer,
        Crane,
        Paint,
        GoldTrowel,
        /// <summary>Deprecated Faz 2 cell trowel; applyMove rejects it (`legacyTrowel`).</summary>
        Trowel,
        AddMoves,
    }

    /// <summary>
    /// A core move (TECH §6.1, §2R.2). The undo booster is a session action, not a core move (K-39). Board coordinates are
    /// global (`x` 0 … wy+ws−1, board row `y`; a site target lies on the visible segment, board row = plan row + `e`).
    /// </summary>
    public abstract class Move : SessionAction
    {
        public abstract MoveKind MoveType { get; }
    }

    public sealed class DragMove : Move
    {
        public readonly int PieceId;
        public readonly DragNode To;
        public readonly int? Via;
        public readonly Steer? Steer;

        public DragMove(int pieceId, DragNode to, int? via = null, Steer? steer = null)
        {
            PieceId = pieceId;
            To = to;
            Via = via;
            Steer = steer;
        }

        public override string Kind => "drag";
        public override MoveKind MoveType => MoveKind.Drag;
    }

    public sealed class HammerMove : Move
    {
        public readonly HammerTarget Target;

        public HammerMove(HammerTarget target)
        {
            Target = target;
        }

        public override string Kind => "hammer";
        public override MoveKind MoveType => MoveKind.Hammer;
    }

    /// <summary>
    /// `crane` (K-37): `to` = the block's anchor at the target; `rotation` = the block's orientation there (a yard target
    /// keeps the current orientation; a site target may turn clockwise, width ≤ Ws; Ağır Yük never turns).
    /// </summary>
    public sealed class CraneMove : Move
    {
        public readonly int PieceId;
        /// <summary>"yard" or "site".</summary>
        public readonly string ToZone;
        public readonly int ToX;
        public readonly int ToY;
        public readonly int Rotation;

        public CraneMove(int pieceId, string toZone, int toX, int toY, int rotation)
        {
            PieceId = pieceId;
            ToZone = toZone;
            ToX = toX;
            ToY = toY;
            Rotation = rotation;
        }

        public override string Kind => "crane";
        public override MoveKind MoveType => MoveKind.Crane;
    }

    /// <summary>`paint` (K-38, Faz 2R): swaps the colours of the yard blocks `a` and `b`.</summary>
    public sealed class PaintMove : Move
    {
        public readonly int A;
        public readonly int B;

        public PaintMove(int a, int b)
        {
            A = a;
            B = b;
        }

        public override string Kind => "paint";
        public override MoveKind MoveType => MoveKind.Paint;
    }

    /// <summary>`goldTrowel` (K-33, Faz 2R): flies yard block `pieceId` to anchor `(x, y)` of its `P` set on the visible segment.</summary>
    public sealed class GoldTrowelMove : Move
    {
        public readonly int PieceId;
        public readonly int X;
        public readonly int Y;

        public GoldTrowelMove(int pieceId, int x, int y)
        {
            PieceId = pieceId;
            X = x;
            Y = y;
        }

        public override string Kind => "goldTrowel";
        public override MoveKind MoveType => MoveKind.GoldTrowel;
    }

    /// <summary>Deprecated Faz 2 cell trowel; applyMove rejects it (`legacyTrowel`). Use `GoldTrowelMove` (K-33).</summary>
    public sealed class TrowelMove : Move
    {
        public readonly int Seg;
        /// <summary>0 or 1.</summary>
        public readonly int X;
        public readonly int Y;

        public TrowelMove(int seg, int x, int y)
        {
            Seg = seg;
            X = x;
            Y = y;
        }

        public override string Kind => "trowel";
        public override MoveKind MoveType => MoveKind.Trowel;
    }

    public static class AddMovesSource
    {
        public const string OfferCoins = "offerCoins";
        public const string OfferAd = "offerAd";
        public const string Thermos = "thermos";
        public const string Streak = "streak";
    }

    public sealed class AddMovesMove : Move
    {
        public readonly int Amount;
        /// <summary>offerCoins | offerAd | thermos | streak (`AddMovesSource`).</summary>
        public readonly string Source;

        public AddMovesMove(int amount, string source)
        {
            Amount = amount;
            Source = source;
        }

        public override string Kind => "addMoves";
        public override MoveKind MoveType => MoveKind.AddMoves;
    }

    public sealed class UndoAction : SessionAction
    {
        public override string Kind => "undo";
    }

    /// <summary>Pre-level boosters (META §4; economy.json keys).</summary>
    public static class PreBoosters
    {
        public const string Thermos = "thermos";
        public const string TrowelStart = "trowelStart";
        public const string OpenShutter = "openShutter";
    }

    public sealed class StartAction : SessionAction
    {
        public readonly string[] PreBoosters;
        /// <summary>0 … 3.</summary>
        public readonly int StreakTier;

        public StartAction(string[] preBoosters, int streakTier)
        {
            PreBoosters = preBoosters;
            StreakTier = streakTier;
        }

        public override string Kind => "start";
    }

    /// <summary>GDD K-30 `teardown.cause`: D2 colour balance, D3a tiling, D3b access (solver table).</summary>
    public static class TeardownCauses
    {
        public const string Color = "color";
        public const string Tiling = "tiling";
        public const string Access = "access";
    }

    /// <summary>Where a piece is in a teardown move: a board position (`At`), or off the board (`Off` = queue | pending | gone).</summary>
    public readonly struct TeardownPlace
    {
        public const string Queue = "queue";
        public const string Pending = "pending";
        public const string Gone = "gone";

        public readonly At? At;
        public readonly string? Off;

        private TeardownPlace(At? at, string? off)
        {
            At = at;
            Off = off;
        }

        public static TeardownPlace OnBoard(At at) => new TeardownPlace(at, null);
        public static TeardownPlace OffBoard(string off) => new TeardownPlace(null, off);
    }

    /// <summary>One block a Söküm moves back (K-30): from its place after the action to its place before it.</summary>
    public readonly struct TeardownMove
    {
        public readonly int PieceId;
        public readonly TeardownPlace From;
        public readonly TeardownPlace To;

        public TeardownMove(int pieceId, TeardownPlace from, TeardownPlace to)
        {
            PieceId = pieceId;
            From = from;
            To = to;
        }
    }

    /// <summary>K-36 hammer target kinds (analytics `booster_used.target`, TECH §2R.16).</summary>
    public static class HammerTargetKinds
    {
        public const string Cargo = "cargo";
        public const string Crate = "crate";
        public const string CementBag = "cementBag";
        public const string Chain = "chain";
        public const string SiteDebris = "siteDebris";
        public const string StuckMortar = "stuckMortar";
    }

    public static class BoosterNames
    {
        public const string Hammer = "hammer";
        public const string Crane = "crane";
        public const string Paint = "paint";
        public const string Trowel = "trowel";
    }

    // --- events (TECH §6.3) ------------------------------------------------------------------------------------------

    /// <summary>The 43 event types of `GameEventBody['t']`, in the TS union order.</summary>
    public enum EventKind
    {
        MoveCancelled,
        PieceMoved,
        PiecePainted,
        WindDrift,
        PieceFell,
        BalloonRose,
        Steered,
        GlassBroke,
        PieceReturned,
        PlacementCorrect,
        CellsRevealed,
        ComboChanged,
        TrowelEarned,
        PlacementWrong,
        MortarStuck,
        PieceBounced,
        MovesChanged,
        CrateDamaged,
        CrateBroken,
        BagTorn,
        ChainReleased,
        ScrewCollected,
        KeyCollected,
        GapUnlocked,
        GapChanged,
        WetTick,
        GoalProgress,
        SegmentCompleted,
        SiteShifted,
        CarouselRotated,
        ElevatorMoved,
        DeliveryArrived,
        DeliveryQueued,
        LevelWon,
        OutOfMoves,
        DeadlockDetected,
        TruckHelp,
        BoosterApplied,
        BoosterRejected,
        PieceLifted,
        ColorsSwapped,
        CargoSmashed,
        Teardown,
    }

    /// <summary>Event payloads (TECH §6.3). `Seq` and `Step` are added by the sink (TS `EvBase`).</summary>
    public abstract class GameEvent
    {
        public abstract EventKind T { get; }
        /// <summary>The TS `t` literal (camelCase), e.g. "pieceMoved".</summary>
        public abstract string TypeName { get; }
        /// <summary>0, 1, 2 … inside one move.</summary>
        public int Seq { get; set; }
        /// <summary>K-35 pipeline step (GDD §12, TECH §6.2).</summary>
        public int Step { get; set; }
    }

    public static class CancelReasons
    {
        public const string SameSpot = "sameSpot";
        public const string CraneOverYard = "craneOverYard";
        public const string Straddle = "straddle";
        public const string SiteClosed = "siteClosed";
        public const string Invalid = "invalid";
    }

    public sealed class MoveCancelledEvent : GameEvent
    {
        public readonly int PieceId;
        /// <summary>sameSpot | craneOverYard | straddle | siteClosed | invalid.</summary>
        public readonly string Reason;

        public MoveCancelledEvent(int pieceId, string reason)
        {
            PieceId = pieceId;
            Reason = reason;
        }

        public override EventKind T => EventKind.MoveCancelled;
        public override string TypeName => "moveCancelled";
    }

    public static class EntryKinds
    {
        public const string Yard = "yard";
        public const string OverWall = "overWall";
        public const string Gap = "gap";
    }

    public sealed class PieceMovedEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At From;
        public readonly At To;
        /// <summary>yard | overWall | gap.</summary>
        public readonly string Entry;
        public readonly int? Gap;

        public PieceMovedEvent(int pieceId, At from, At to, string entry, int? gap = null)
        {
            PieceId = pieceId;
            From = from;
            To = to;
            Entry = entry;
            Gap = gap;
        }

        public override EventKind T => EventKind.PieceMoved;
        public override string TypeName => "pieceMoved";
    }

    public sealed class PiecePaintedEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly string From;
        public readonly string To;
        public readonly int Gap;

        public PiecePaintedEvent(int pieceId, string from, string to, int gap)
        {
            PieceId = pieceId;
            From = from;
            To = to;
            Gap = gap;
        }

        public override EventKind T => EventKind.PiecePainted;
        public override string TypeName => "piecePainted";
    }

    public sealed class WindDriftEvent : GameEvent
    {
        public readonly int PieceId;
        /// <summary>−1 or 1.</summary>
        public readonly int Dx;

        public WindDriftEvent(int pieceId, int dx)
        {
            PieceId = pieceId;
            Dx = dx;
        }

        public override EventKind T => EventKind.WindDrift;
        public override string TypeName => "windDrift";
    }

    public static class FallCauses
    {
        public const string Release = "release";
        public const string YardGravity = "yardGravity";
        public const string Delivery = "delivery";
        public const string Bounce = "bounce";
    }

    public sealed class PieceFellEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At From;
        public readonly At To;
        public readonly int Rows;
        /// <summary>release | yardGravity | delivery | bounce.</summary>
        public readonly string Cause;

        public PieceFellEvent(int pieceId, At from, At to, int rows, string cause)
        {
            PieceId = pieceId;
            From = from;
            To = to;
            Rows = rows;
            Cause = cause;
        }

        public override EventKind T => EventKind.PieceFell;
        public override string TypeName => "pieceFell";
    }

    public sealed class BalloonRoseEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At From;
        public readonly At To;
        public readonly int Rows;

        public BalloonRoseEvent(int pieceId, At from, At to, int rows)
        {
            PieceId = pieceId;
            From = from;
            To = to;
            Rows = rows;
        }

        public override EventKind T => EventKind.BalloonRose;
        public override string TypeName => "balloonRose";
    }

    public sealed class SteeredEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly int AtRow;
        /// <summary>−1 or 1.</summary>
        public readonly int Dx;

        public SteeredEvent(int pieceId, int atRow, int dx)
        {
            PieceId = pieceId;
            AtRow = atRow;
            Dx = dx;
        }

        public override EventKind T => EventKind.Steered;
        public override string TypeName => "steered";
    }

    public sealed class GlassBrokeEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At At;
        public readonly int Penalty;

        public GlassBrokeEvent(int pieceId, At at, int penalty)
        {
            PieceId = pieceId;
            At = at;
            Penalty = penalty;
        }

        public override EventKind T => EventKind.GlassBroke;
        public override string TypeName => "glassBroke";
    }

    /// <summary>`To` null = TS `'queue'`.</summary>
    public sealed class PieceReturnedEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At From;
        public readonly At? To;

        public PieceReturnedEvent(int pieceId, At from, At? to)
        {
            PieceId = pieceId;
            From = from;
            To = to;
        }

        public override EventKind T => EventKind.PieceReturned;
        public override string TypeName => "pieceReturned";
    }

    public sealed class PlacementCorrectEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At[] Cells;
        public readonly bool OverWall;

        public PlacementCorrectEvent(int pieceId, At[] cells, bool overWall)
        {
            PieceId = pieceId;
            Cells = cells;
            OverWall = overWall;
        }

        public override EventKind T => EventKind.PlacementCorrect;
        public override string TypeName => "placementCorrect";
    }

    public readonly struct RevealedCell
    {
        public readonly int X;
        public readonly int Y;
        public readonly string Color;

        public RevealedCell(int x, int y, string color)
        {
            X = x;
            Y = y;
            Color = color;
        }
    }

    public sealed class CellsRevealedEvent : GameEvent
    {
        public readonly int Seg;
        public readonly RevealedCell[] Cells;

        public CellsRevealedEvent(int seg, RevealedCell[] cells)
        {
            Seg = seg;
            Cells = cells;
        }

        public override EventKind T => EventKind.CellsRevealed;
        public override string TypeName => "cellsRevealed";
    }

    public sealed class ComboChangedEvent : GameEvent
    {
        public readonly int Combo;

        public ComboChangedEvent(int combo)
        {
            Combo = combo;
        }

        public override EventKind T => EventKind.ComboChanged;
        public override string TypeName => "comboChanged";
    }

    public sealed class TrowelEarnedEvent : GameEvent
    {
        public readonly int Trowels;

        public TrowelEarnedEvent(int trowels)
        {
            Trowels = trowels;
        }

        public override EventKind T => EventKind.TrowelEarned;
        public override string TypeName => "trowelEarned";
    }

    public sealed class PlacementWrongEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly string[] Reasons;
        public readonly At[] MissingSupport;

        public PlacementWrongEvent(int pieceId, string[] reasons, At[] missingSupport)
        {
            PieceId = pieceId;
            Reasons = reasons;
            MissingSupport = missingSupport;
        }

        public override EventKind T => EventKind.PlacementWrong;
        public override string TypeName => "placementWrong";
    }

    public sealed class MortarStuckEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly string Reason;
        public readonly At[] MissingSupport;

        public MortarStuckEvent(int pieceId, string reason, At[] missingSupport)
        {
            PieceId = pieceId;
            Reason = reason;
            MissingSupport = missingSupport;
        }

        public override EventKind T => EventKind.MortarStuck;
        public override string TypeName => "mortarStuck";
    }

    /// <summary>`To` null = TS `'queue'`.</summary>
    public sealed class PieceBouncedEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At From;
        public readonly At? To;
        public readonly bool ViaDrop;
        public readonly string Reason;
        public readonly At[] MissingSupport;

        public PieceBouncedEvent(int pieceId, At from, At? to, bool viaDrop, string reason, At[] missingSupport)
        {
            PieceId = pieceId;
            From = from;
            To = to;
            ViaDrop = viaDrop;
            Reason = reason;
            MissingSupport = missingSupport;
        }

        public override EventKind T => EventKind.PieceBounced;
        public override string TypeName => "pieceBounced";
    }

    /// <summary>`movesChanged.cost`: `base` 1 or 2, `glass` 0 or 1.</summary>
    public readonly struct MoveCost
    {
        public readonly int Base;
        public readonly int Glass;

        public MoveCost(int @base, int glass)
        {
            Base = @base;
            Glass = glass;
        }
    }

    public static class MovesChangedReasons
    {
        public const string Move = "move";
        public const string Offer = "offer";
        public const string Booster = "booster";
    }

    public sealed class MovesChangedEvent : GameEvent
    {
        public readonly int MovesLeft;
        public readonly int Delta;
        /// <summary>move | offer | booster.</summary>
        public readonly string Reason;
        public readonly MoveCost? Cost;

        public MovesChangedEvent(int movesLeft, int delta, string reason, MoveCost? cost = null)
        {
            MovesLeft = movesLeft;
            Delta = delta;
            Reason = reason;
            Cost = cost;
        }

        public override EventKind T => EventKind.MovesChanged;
        public override string TypeName => "movesChanged";
    }

    public sealed class CrateDamagedEvent : GameEvent
    {
        public readonly int Obstacle;
        public readonly int Hp;

        public CrateDamagedEvent(int obstacle, int hp)
        {
            Obstacle = obstacle;
            Hp = hp;
        }

        public override EventKind T => EventKind.CrateDamaged;
        public override string TypeName => "crateDamaged";
    }

    public sealed class CrateBrokenEvent : GameEvent
    {
        public readonly int Obstacle;

        public CrateBrokenEvent(int obstacle)
        {
            Obstacle = obstacle;
        }

        public override EventKind T => EventKind.CrateBroken;
        public override string TypeName => "crateBroken";
    }

    public sealed class BagTornEvent : GameEvent
    {
        public readonly int Obstacle;

        public BagTornEvent(int obstacle)
        {
            Obstacle = obstacle;
        }

        public override EventKind T => EventKind.BagTorn;
        public override string TypeName => "bagTorn";
    }

    public sealed class ChainReleasedEvent : GameEvent
    {
        public readonly int PieceId;

        public ChainReleasedEvent(int pieceId)
        {
            PieceId = pieceId;
        }

        public override EventKind T => EventKind.ChainReleased;
        public override string TypeName => "chainReleased";
    }

    public sealed class ScrewCollectedEvent : GameEvent
    {
        public readonly At At;
        public readonly int Total;

        public ScrewCollectedEvent(At at, int total)
        {
            At = at;
            Total = total;
        }

        public override EventKind T => EventKind.ScrewCollected;
        public override string TypeName => "screwCollected";
    }

    public sealed class KeyCollectedEvent : GameEvent
    {
        public readonly string KeyId;

        public KeyCollectedEvent(string keyId)
        {
            KeyId = keyId;
        }

        public override EventKind T => EventKind.KeyCollected;
        public override string TypeName => "keyCollected";
    }

    public sealed class GapUnlockedEvent : GameEvent
    {
        public readonly int Gap;

        public GapUnlockedEvent(int gap)
        {
            Gap = gap;
        }

        public override EventKind T => EventKind.GapUnlocked;
        public override string TypeName => "gapUnlocked";
    }

    public sealed class GapChangedEvent : GameEvent
    {
        public readonly int Gap;
        public readonly bool Open;
        public readonly int Y;

        public GapChangedEvent(int gap, bool open, int y)
        {
            Gap = gap;
            Open = open;
            Y = y;
        }

        public override EventKind T => EventKind.GapChanged;
        public override string TypeName => "gapChanged";
    }

    public sealed class WetTickEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly int Left;

        public WetTickEvent(int pieceId, int left)
        {
            PieceId = pieceId;
            Left = left;
        }

        public override EventKind T => EventKind.WetTick;
        public override string TypeName => "wetTick";
    }

    public sealed class GoalProgressEvent : GameEvent
    {
        public readonly int Goal;
        public readonly int Value;
        public readonly int Target;

        public GoalProgressEvent(int goal, int value, int target)
        {
            Goal = goal;
            Value = value;
            Target = target;
        }

        public override EventKind T => EventKind.GoalProgress;
        public override string TypeName => "goalProgress";
    }

    public sealed class SegmentCompletedEvent : GameEvent
    {
        public readonly int Seg;

        public SegmentCompletedEvent(int seg)
        {
            Seg = seg;
        }

        public override EventKind T => EventKind.SegmentCompleted;
        public override string TypeName => "segmentCompleted";
    }

    public sealed class SiteShiftedEvent : GameEvent
    {
        public readonly int ToSeg;

        public SiteShiftedEvent(int toSeg)
        {
            ToSeg = toSeg;
        }

        public override EventKind T => EventKind.SiteShifted;
        public override string TypeName => "siteShifted";
    }

    public sealed class CarouselRotatedEvent : GameEvent
    {
        public readonly int Front;

        public CarouselRotatedEvent(int front)
        {
            Front = front;
        }

        public override EventKind T => EventKind.CarouselRotated;
        public override string TypeName => "carouselRotated";
    }

    public sealed class ElevatorMovedEvent : GameEvent
    {
        public readonly int Offset;

        public ElevatorMovedEvent(int offset)
        {
            Offset = offset;
        }

        public override EventKind T => EventKind.ElevatorMoved;
        public override string TypeName => "elevatorMoved";
    }

    public sealed class DeliveryArrivedEvent : GameEvent
    {
        public readonly int Seg;
        public readonly int[] Pieces;

        public DeliveryArrivedEvent(int seg, int[] pieces)
        {
            Seg = seg;
            Pieces = pieces;
        }

        public override EventKind T => EventKind.DeliveryArrived;
        public override string TypeName => "deliveryArrived";
    }

    public sealed class DeliveryQueuedEvent : GameEvent
    {
        public readonly int Queued;

        public DeliveryQueuedEvent(int queued)
        {
            Queued = queued;
        }

        public override EventKind T => EventKind.DeliveryQueued;
        public override string TypeName => "deliveryQueued";
    }

    public sealed class LevelWonEvent : GameEvent
    {
        public readonly int MovesLeft;

        public LevelWonEvent(int movesLeft)
        {
            MovesLeft = movesLeft;
        }

        public override EventKind T => EventKind.LevelWon;
        public override string TypeName => "levelWon";
    }

    public sealed class OutOfMovesEvent : GameEvent
    {
        public override EventKind T => EventKind.OutOfMoves;
        public override string TypeName => "outOfMoves";
    }

    public static class DeadlockReasons
    {
        public const string NoMoves = "noMoves";
        public const string Material = "material";
        public const string Tiling = "tiling";
    }

    public sealed class DeadlockDetectedEvent : GameEvent
    {
        /// <summary>noMoves | material | tiling.</summary>
        public readonly string Reason;

        public DeadlockDetectedEvent(string reason)
        {
            Reason = reason;
        }

        public override EventKind T => EventKind.DeadlockDetected;
        public override string TypeName => "deadlockDetected";
    }

    public readonly struct PieceShift
    {
        public readonly int PieceId;
        public readonly At From;
        public readonly At To;

        public PieceShift(int pieceId, At from, At to)
        {
            PieceId = pieceId;
            From = from;
            To = to;
        }
    }

    public static class TruckHelpKinds
    {
        public const string Unchain = "unchain";
        public const string DeliverMissing = "deliverMissing";
        public const string Reshuffle = "reshuffle";
        public const string Reshape = "reshape";
    }

    public sealed class TruckHelpEvent : GameEvent
    {
        /// <summary>unchain | deliverMissing | reshuffle | reshape.</summary>
        public readonly string HelpKind;
        public readonly PieceShift[]? Moves;
        public readonly int[]? Delivered;

        public TruckHelpEvent(string helpKind, PieceShift[]? moves = null, int[]? delivered = null)
        {
            HelpKind = helpKind;
            Moves = moves;
            Delivered = delivered;
        }

        public override EventKind T => EventKind.TruckHelp;
        public override string TypeName => "truckHelp";
    }

    public sealed class BoosterAppliedEvent : GameEvent
    {
        public readonly string Booster;
        /// <summary>TS `detail: unknown` (booster specific payload; written by the booster package).</summary>
        public readonly object? Detail;

        public BoosterAppliedEvent(string booster, object? detail)
        {
            Booster = booster;
            Detail = detail;
        }

        public override EventKind T => EventKind.BoosterApplied;
        public override string TypeName => "boosterApplied";
    }

    public sealed class BoosterRejectedEvent : GameEvent
    {
        public readonly string Booster;
        public readonly string Reason;

        public BoosterRejectedEvent(string booster, string reason)
        {
            Booster = booster;
            Reason = reason;
        }

        public override EventKind T => EventKind.BoosterRejected;
        public override string TypeName => "boosterRejected";
    }

    /// <summary>K-37 crane / K-33 Golden Trowel flight (no fall, no drag events; a site target locks like a correct placement).</summary>
    public sealed class PieceLiftedEvent : GameEvent
    {
        public readonly int PieceId;
        /// <summary>crane | trowel.</summary>
        public readonly string By;
        public readonly At From;
        public readonly At To;
        public readonly string Shape;

        public PieceLiftedEvent(int pieceId, string by, At from, At to, string shape)
        {
            PieceId = pieceId;
            By = by;
            From = from;
            To = to;
            Shape = shape;
        }

        public override EventKind T => EventKind.PieceLifted;
        public override string TypeName => "pieceLifted";
    }

    /// <summary>K-38 paint brush: the colours of `a` and `b` are swapped (`aColor` / `bColor` = the new colours).</summary>
    public sealed class ColorsSwappedEvent : GameEvent
    {
        public readonly int A;
        public readonly int B;
        public readonly string AColor;
        public readonly string BColor;

        public ColorsSwappedEvent(int a, int b, string aColor, string bColor)
        {
            A = a;
            B = b;
            AColor = aColor;
            BColor = bColor;
        }

        public override EventKind T => EventKind.ColorsSwapped;
        public override string TypeName => "colorsSwapped";
    }

    /// <summary>K-36 hammer: an Ağır Yük is smashed (gone).</summary>
    public sealed class CargoSmashedEvent : GameEvent
    {
        public readonly int PieceId;
        public readonly At At;

        public CargoSmashedEvent(int pieceId, At at)
        {
            PieceId = pieceId;
            At = at;
        }

        public override EventKind T => EventKind.CargoSmashed;
        public override string TypeName => "cargoSmashed";
    }

    /// <summary>K-30 Söküm: always the LAST event of an action's package (TECH §2R.15 item 3).</summary>
    public sealed class TeardownEvent : GameEvent
    {
        /// <summary>`m` after the Söküm (the action's start value).</summary>
        public readonly int ToTurn;
        public readonly TeardownMove[] Pieces;
        /// <summary>color | tiling | access (`TeardownCauses`).</summary>
        public readonly string Cause;

        public TeardownEvent(int toTurn, TeardownMove[] pieces, string cause)
        {
            ToTurn = toTurn;
            Pieces = pieces;
            Cause = cause;
        }

        public override EventKind T => EventKind.Teardown;
        public override string TypeName => "teardown";
    }

    /// <summary>Event type names in `EventKind` order (TS `GameEventType` union, 43 entries).</summary>
    public static class EventTypes
    {
        public static readonly string[] All =
        {
            "moveCancelled", "pieceMoved", "piecePainted", "windDrift", "pieceFell", "balloonRose", "steered",
            "glassBroke", "pieceReturned", "placementCorrect", "cellsRevealed", "comboChanged", "trowelEarned",
            "placementWrong", "mortarStuck", "pieceBounced", "movesChanged", "crateDamaged", "crateBroken", "bagTorn",
            "chainReleased", "screwCollected", "keyCollected", "gapUnlocked", "gapChanged", "wetTick", "goalProgress",
            "segmentCompleted", "siteShifted", "carouselRotated", "elevatorMoved", "deliveryArrived", "deliveryQueued",
            "levelWon", "outOfMoves", "deadlockDetected", "truckHelp", "boosterApplied", "boosterRejected",
            "pieceLifted", "colorsSwapped", "cargoSmashed", "teardown",
        };

        public static string NameOf(EventKind kind) => All[(int)kind];
    }
}
