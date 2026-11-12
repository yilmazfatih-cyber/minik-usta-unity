#nullable enable
// Port of the event side of src/core/moves.ts (web repo, source of truth): `EventSink`, `NULL_SINK`, `ArraySink`, the
// `Emitter` that numbers the events of one move (`seq`, `step`), and the JSON form of a `GameEvent` that `canonicalJson`
// sees in TypeScript (field names and presence exactly as the TS object literals, so `eventLogHash` matches bit for bit;
// TECH_DESIGN §6.3, §9.5).
using System.Collections.Generic;
using MinikUsta.Core.Json;

namespace MinikUsta.Core
{
    /// <summary>Receiver of the move's events (TECH §6.3): the game uses `ArraySink`, solver and bots `NullSink`. TS `EventSink`.</summary>
    public interface IEventSink
    {
        /// <summary>`false` = events are not built at all (no allocation). TS `enabled` (default true).</summary>
        bool Enabled { get; }
        void Push(GameEvent e);
    }

    /// <summary>TS `NULL_SINK`.</summary>
    public sealed class NullSink : IEventSink
    {
        public static readonly NullSink Instance = new NullSink();

        private NullSink() { }

        public bool Enabled => false;
        public void Push(GameEvent e) { }
    }

    /// <summary>Collects events in order. TS `ArraySink`.</summary>
    public sealed class ArraySink : IEventSink
    {
        public readonly List<GameEvent> Events = new List<GameEvent>();

        public bool Enabled => true;
        public void Push(GameEvent e) => Events.Add(e);
        public void Clear() => Events.Clear();
    }

    /// <summary>Numbers the events of one move: `Seq` 0, 1, 2 …, `Step` = the current K-35 step. TS `Emitter` (module private).</summary>
    internal sealed class Emitter
    {
        public int Step;
        private int seq;
        private readonly bool on;
        private readonly IEventSink sink;

        public Emitter(IEventSink sink)
        {
            this.sink = sink;
            on = sink.Enabled;
        }

        public void Emit(GameEvent e)
        {
            if (!on) return;
            e.Seq = seq++;
            e.Step = Step;
            sink.Push(e);
        }
    }

    /// <summary>
    /// The TS object shape of every event (`{ seq, step, ...body }`), as JSON. Optional TS fields (`pieceMoved.gap`,
    /// `movesChanged.cost`, `truckHelp.moves` / `delivered`, `At.seg`) are written only when present — `canonicalJson`
    /// drops `undefined` in TS, so the two texts are identical. `At | 'queue'` unions write the string.
    /// </summary>
    public static class EventJson
    {
        public static JsonValue AtJson(At a)
        {
            var o = JsonValue.Object().Set("zone", JsonValue.Of(a.Zone)).Set("x", JsonValue.Of(a.X)).Set("y", JsonValue.Of(a.Y));
            if (a.Seg.HasValue) o.Set("seg", JsonValue.Of(a.Seg.Value));
            return o;
        }

        /// <summary>`At | 'queue'` (TS `targetAt`): null = "queue".</summary>
        public static JsonValue AtOrQueue(At? a) => a.HasValue ? AtJson(a.Value) : JsonValue.Of(TeardownPlace.Queue);

        public static JsonValue TeardownPlaceJson(TeardownPlace p) => p.At.HasValue ? AtJson(p.At.Value) : JsonValue.Of(p.Off!);

        public static JsonValue AtArray(At[] cells)
        {
            var a = JsonValue.Array();
            foreach (var c in cells) a.Add(AtJson(c));
            return a;
        }

        public static JsonValue Ints(int[] xs)
        {
            var a = JsonValue.Array();
            foreach (int x in xs) a.Add(JsonValue.Of(x));
            return a;
        }

        public static JsonValue Strings(string[] xs)
        {
            var a = JsonValue.Array();
            foreach (string x in xs) a.Add(JsonValue.Of(x));
            return a;
        }

        public static JsonValue RevealedJson(RevealedCell[] cells)
        {
            var a = JsonValue.Array();
            foreach (var c in cells) a.Add(JsonValue.Object().Set("x", JsonValue.Of(c.X)).Set("y", JsonValue.Of(c.Y)).Set("color", JsonValue.Of(c.Color)));
            return a;
        }

        /// <summary>The event as the TS `GameEvent` object (`seq`, `step`, `t`, body fields).</summary>
        public static JsonValue ToJson(GameEvent e)
        {
            var o = JsonValue.Object().Set("seq", JsonValue.Of(e.Seq)).Set("step", JsonValue.Of(e.Step)).Set("t", JsonValue.Of(e.TypeName));
            switch (e)
            {
                case MoveCancelledEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("reason", JsonValue.Of(x.Reason));
                    break;
                case PieceMovedEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("from", AtJson(x.From)).Set("to", AtJson(x.To)).Set("entry", JsonValue.Of(x.Entry));
                    if (x.Gap.HasValue) o.Set("gap", JsonValue.Of(x.Gap.Value));
                    break;
                case PiecePaintedEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("from", JsonValue.Of(x.From)).Set("to", JsonValue.Of(x.To)).Set("gap", JsonValue.Of(x.Gap));
                    break;
                case WindDriftEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("dx", JsonValue.Of(x.Dx));
                    break;
                case PieceFellEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("from", AtJson(x.From)).Set("to", AtJson(x.To)).Set("rows", JsonValue.Of(x.Rows)).Set("cause", JsonValue.Of(x.Cause));
                    break;
                case BalloonRoseEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("from", AtJson(x.From)).Set("to", AtJson(x.To)).Set("rows", JsonValue.Of(x.Rows));
                    break;
                case SteeredEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("atRow", JsonValue.Of(x.AtRow)).Set("dx", JsonValue.Of(x.Dx));
                    break;
                case GlassBrokeEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("at", AtJson(x.At)).Set("penalty", JsonValue.Of(x.Penalty));
                    break;
                case PieceReturnedEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("from", AtJson(x.From)).Set("to", AtOrQueue(x.To));
                    break;
                case PlacementCorrectEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("cells", AtArray(x.Cells)).Set("overWall", JsonValue.Of(x.OverWall));
                    break;
                case CellsRevealedEvent x:
                    o.Set("seg", JsonValue.Of(x.Seg)).Set("cells", RevealedJson(x.Cells));
                    break;
                case ComboChangedEvent x:
                    o.Set("combo", JsonValue.Of(x.Combo));
                    break;
                case TrowelEarnedEvent x:
                    o.Set("trowels", JsonValue.Of(x.Trowels));
                    break;
                case PlacementWrongEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("reasons", Strings(x.Reasons)).Set("missingSupport", AtArray(x.MissingSupport));
                    break;
                case MortarStuckEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("reason", JsonValue.Of(x.Reason)).Set("missingSupport", AtArray(x.MissingSupport));
                    break;
                case PieceBouncedEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("from", AtJson(x.From)).Set("to", AtOrQueue(x.To)).Set("viaDrop", JsonValue.Of(x.ViaDrop))
                        .Set("reason", JsonValue.Of(x.Reason)).Set("missingSupport", AtArray(x.MissingSupport));
                    break;
                case MovesChangedEvent x:
                    o.Set("movesLeft", JsonValue.Of(x.MovesLeft)).Set("delta", JsonValue.Of(x.Delta)).Set("reason", JsonValue.Of(x.Reason));
                    if (x.PieceId >= 0) o.Set("pieceId", JsonValue.Of(x.PieceId));
                    if (x.Cost.HasValue) o.Set("cost", JsonValue.Object().Set("base", JsonValue.Of(x.Cost.Value.Base)).Set("glass", JsonValue.Of(x.Cost.Value.Glass)));
                    break;
                case CrateDamagedEvent x:
                    o.Set("obstacle", JsonValue.Of(x.Obstacle)).Set("hp", JsonValue.Of(x.Hp));
                    break;
                case CrateBrokenEvent x:
                    o.Set("obstacle", JsonValue.Of(x.Obstacle));
                    break;
                case BagTornEvent x:
                    o.Set("obstacle", JsonValue.Of(x.Obstacle));
                    break;
                case ChainReleasedEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId));
                    break;
                case UnwrappedEvent x:
                    o.Set("by", JsonValue.Of(x.By)).Set("pieceId", JsonValue.Of(x.PieceId)).Set("pieces", Ints(x.Pieces));
                    break;
                case ScrewCollectedEvent x:
                    o.Set("at", AtJson(x.At)).Set("total", JsonValue.Of(x.Total));
                    break;
                case KeyCollectedEvent x:
                    o.Set("keyId", JsonValue.Of(x.KeyId));
                    break;
                case GapUnlockedEvent x:
                    o.Set("gap", JsonValue.Of(x.Gap));
                    break;
                case GapChangedEvent x:
                    o.Set("gap", JsonValue.Of(x.Gap)).Set("open", JsonValue.Of(x.Open)).Set("y", JsonValue.Of(x.Y));
                    break;
                case WetTickEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("left", JsonValue.Of(x.Left));
                    break;
                case GoalProgressEvent x:
                    o.Set("goal", JsonValue.Of(x.Goal)).Set("value", JsonValue.Of(x.Value)).Set("target", JsonValue.Of(x.Target));
                    break;
                case SegmentCompletedEvent x:
                    o.Set("seg", JsonValue.Of(x.Seg));
                    break;
                case SiteShiftedEvent x:
                    o.Set("toSeg", JsonValue.Of(x.ToSeg));
                    break;
                case CarouselRotatedEvent x:
                    o.Set("front", JsonValue.Of(x.Front));
                    break;
                case ElevatorMovedEvent x:
                    o.Set("offset", JsonValue.Of(x.Offset));
                    break;
                case DeliveryArrivedEvent x:
                    o.Set("seg", JsonValue.Of(x.Seg)).Set("pieces", Ints(x.Pieces));
                    break;
                case DeliveryQueuedEvent x:
                    o.Set("queued", JsonValue.Of(x.Queued));
                    break;
                case LevelWonEvent x:
                    o.Set("movesLeft", JsonValue.Of(x.MovesLeft));
                    break;
                case OutOfMovesEvent _:
                    break;
                case DeadlockDetectedEvent x:
                    o.Set("reason", JsonValue.Of(x.Reason));
                    break;
                case TruckHelpEvent x:
                    o.Set("kind", JsonValue.Of(x.HelpKind));
                    if (x.Moves != null)
                    {
                        var moves = JsonValue.Array();
                        foreach (var m in x.Moves) moves.Add(JsonValue.Object().Set("pieceId", JsonValue.Of(m.PieceId)).Set("from", AtJson(m.From)).Set("to", AtJson(m.To)));
                        o.Set("moves", moves);
                    }
                    if (x.Delivered != null) o.Set("delivered", Ints(x.Delivered));
                    break;
                case BoosterAppliedEvent x:
                    o.Set("booster", JsonValue.Of(x.Booster));
                    if (x.Detail != null) o.Set("detail", x.Detail);
                    break;
                case BoosterRejectedEvent x:
                    o.Set("booster", JsonValue.Of(x.Booster)).Set("reason", JsonValue.Of(x.Reason));
                    break;
                case PieceLiftedEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("by", JsonValue.Of(x.By)).Set("from", AtJson(x.From)).Set("to", AtJson(x.To)).Set("shape", JsonValue.Of(x.Shape));
                    break;
                case ColorsSwappedEvent x:
                    o.Set("a", JsonValue.Of(x.A)).Set("b", JsonValue.Of(x.B)).Set("aColor", JsonValue.Of(x.AColor)).Set("bColor", JsonValue.Of(x.BColor));
                    break;
                case CargoSmashedEvent x:
                    o.Set("pieceId", JsonValue.Of(x.PieceId)).Set("at", AtJson(x.At));
                    break;
                case TeardownEvent x:
                    {
                        var pieces = JsonValue.Array();
                        foreach (var p in x.Pieces) pieces.Add(JsonValue.Object().Set("pieceId", JsonValue.Of(p.PieceId)).Set("from", TeardownPlaceJson(p.From)).Set("to", TeardownPlaceJson(p.To)));
                        o.Set("toTurn", JsonValue.Of(x.ToTurn)).Set("pieces", pieces).Set("cause", JsonValue.Of(x.Cause));
                        break;
                    }
                default:
                    throw new System.InvalidOperationException("EventJson: unknown event type " + e.TypeName);
            }
            return o;
        }

        /// <summary>The TS `GameEvent[]` array as JSON.</summary>
        public static JsonValue ToJson(IEnumerable<GameEvent> events)
        {
            var a = JsonValue.Array();
            foreach (var e in events) a.Add(ToJson(e));
            return a;
        }
    }
}
