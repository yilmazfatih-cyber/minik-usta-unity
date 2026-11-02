#nullable enable
// Shared helpers of the movement / gravity / placement / delivery tests (the TS test files each declare their own copies of
// N, R, key, toSite, drag …; one C# copy serves them all). Not a test file.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests.Fixtures
{
    public static class Boards
    {
        // --- nodes, cells, positions ---------------------------------------------------------------------------------------

        public static DragNode N(int ix, int iy) => new DragNode(ix, iy, Movement.Free);
        /// <summary>RAIL node of gap `g`.</summary>
        public static DragNode R(int g, int ix, int iy) => new DragNode(ix, iy, Movement.RailMode(g));
        public static Anchor A(int ix, int iy) => new Anchor(ix, iy);
        public static BoardCell C(int x, int y) => new BoardCell(x, y);
        public static At Site(int x, int y, int seg = 0) => At.Site(x, y, seg);
        public static At YardAt(int x, int y) => At.Yard(x, y);
        public static PiecePlace YardP(int x, int y) => PiecePlace.Yard(x, y);
        public static PiecePlace SiteP(int x, int y, int seg = 0) => PiecePlace.Site(x, y, seg);
        public static PiecePlace QueueP(int x, int y) => PiecePlace.Queue(x, y);

        /// <summary>TS `key(n)`: `F(x,y)` or `Rg(x,y)`.</summary>
        public static string Key(DragNode n) => (n.Mode == Movement.Free ? "F" : "R" + (n.Mode - 1)) + "(" + n.Ix + "," + n.Iy + ")";
        public static string[] Keys(IEnumerable<DragNode> list) => list.Select(Key).ToArray();

        public static BoardCell[] CellsOf(string shape, int ix, int iy) => Movement.BlockCells(Shapes.ShapeById(shape), ix, iy);

        // --- level spec helpers --------------------------------------------------------------------------------------------

        public static P Pc(string shape, string color, int x, int y, string[]? flags = null, int? wetMoves = null) => new P(shape, color, x, y, flags, wetMoves);
        public static P[] Ps(params P[] pieces) => pieces;
        public static P[] Plus(this P[] a, params P[] b) => a.Concat(b).ToArray();
        public static JsonValue Gap(int y, int size = 1) => J.O("type", "static", "y", y, "size", size);
        public static JsonValue LockedGap(int y, int size, string keyId) => J.O("type", "locked", "y", y, "size", size, "keyId", keyId);
        public static WallSpec Wall(int height, params JsonValue[] gaps) => new WallSpec { Height = height, Gaps = gaps };
        public static JsonValue Crate(int x, int y, int? hp = null) => J.O("type", "crate", "x", x, "y", y, "hp", hp);
        public static JsonValue Bag(int x, int y) => J.O("type", "cement_bag", "x", x, "y", y);
        public static JsonValue Screw(int x, int y) => J.O("type", "screw", "x", x, "y", y);
        public static JsonValue KeyItem(int x, int y, string id) => J.O("type", "key", "x", x, "y", y, "id", id);
        public static JsonValue?[] Repeat1 => new JsonValue?[] { J.O("kind", "repeat", "period", 1) };
        public static JsonValue Elevator(int lo, int hi, int start, int dir) => J.O("range", new[] { lo, hi }, "start", start, "dir", dir);
        public static string[] WW(int h) => Enumerable.Repeat("WW", h).ToArray();

        /// <summary>Yard rows y = 7 (first) … 0 (last), 6 characters: `#` = B1 blocker, anything else = empty.</summary>
        public static P[] Blockers(params string[] rows)
        {
            var o = new List<P>();
            for (int i = 0; i < rows.Length; i++)
            {
                for (int x = 0; x < rows[i].Length; x++) if (rows[i][x] == '#') o.Add(new P("B1_0", "W", x, 7 - i));
            }
            return o.ToArray();
        }

        /// <summary>Reachable anchors of one mode, rows y = 9 (first) … 0, `o` reachable.</summary>
        public static string[] AnchorMap(DragSession d, int mode = Movement.Free)
        {
            var rows = new List<string>();
            for (int y = 9; y >= 0; y--)
            {
                var row = new char[8];
                for (int x = 0; x < 8; x++) row[x] = d.IsReachable(new DragNode(x, y, mode)) ? 'o' : '.';
                rows.Add(new string(row));
            }
            return rows.ToArray();
        }

        public static GameState Init(LevelSpec spec) => Builders.InitialState(spec);

        /// <summary>
        /// Faz 2R archive level `Archive2R/level_NNN.json` (`schemaVersion` 1) loaded like the game does (TS `levelFile`: schema +
        /// runtime checks, then compile). The tests of the K-25…K-27 truck rules and of the Faz 2R examples play on it (D-099).
        /// </summary>
        public static CompiledLevel LevelFile(int id) => Compiler.LoadLevel(TestPaths.LoadLevelJson(id)).LevelOrThrow();
        public static GameState LevelState(int id) => CreateInitialState(LevelFile(id));

        /// <summary>The shipped level (D-099, `schemaVersion` 2, K-55) loaded like the game does.</summary>
        public static CompiledLevel CurrentLevelFile(int id) => Compiler.LoadLevel(TestPaths.LoadCurrentLevelJson(id)).LevelOrThrow();
        public static GameState CurrentLevelState(int id) => CreateInitialState(CurrentLevelFile(id));

        // --- the move pipeline (TS tests/core/moves.fixtures.ts) ---------------------------------------------------------------

        /// <summary>TS `dragTo(pieceId, ix, iy, gap?)`: a drag move to a FREE node (or a rail node when `gap` is given).</summary>
        public static DragMove DragTo(int pieceId, int ix, int iy, int? gap = null) =>
            new DragMove(pieceId, gap.HasValue ? R(gap.Value, ix, iy) : N(ix, iy));

        /// <summary>TS `Ran`: the result and the events of one `applyMove`.</summary>
        public sealed class Ran
        {
            public readonly MoveResult Res;
            public readonly List<GameEvent> Ev;

            public Ran(MoveResult res, List<GameEvent> ev)
            {
                Res = res;
                Ev = ev;
            }
        }

        /// <summary>TS `run(s, move, opts)`: `applyMove` with an ArraySink; strict by default (an invalid record throws).</summary>
        public static Ran Run(GameState s, Move move, MoveHooks? hooks = null, bool strict = true, bool noTruckHelp = false, IDeadTable? deadTable = null)
        {
            var sink = new ArraySink();
            var res = Moves.ApplyMove(s, move, sink, new ApplyOptions(hooks, noTruckHelp, strict, null, deadTable));
            return new Ran(res, sink.Events);
        }

        /// <summary>TS `types(ev)`: event type names in order.</summary>
        public static string[] Types(IEnumerable<GameEvent> ev) => ev.Select(e => e.TypeName).ToArray();

        /// <summary>TS `find(ev, t)`: the first event of type `T`; throws when there is none.</summary>
        public static T Find<T>(IEnumerable<GameEvent> ev) where T : GameEvent
        {
            foreach (var e in ev) if (e is T t) return t;
            throw new InvalidOperationException("no " + typeof(T).Name + " event in [" + string.Join(", ", Types(ev)) + "]");
        }

        public static IEnumerable<T> OfType<T>(IEnumerable<GameEvent> ev) where T : GameEvent => ev.OfType<T>();

        public static bool Has<T>(IEnumerable<GameEvent> ev) where T : GameEvent => ev.Any(e => e is T);

        /// <summary>TS `toEqual` on a result: status, reason, won, outOfMoves (teardownCause undefined ≡ null).</summary>
        public static void AssertResult(MoveResult res, string status, string? reason, bool won, bool outOfMoves, string? label = null)
        {
            Assert.AreEqual(status, res.Status, label + " status");
            Assert.AreEqual(reason, res.Reason, label + " reason");
            Assert.AreEqual(won, res.Won, label + " won");
            Assert.AreEqual(outOfMoves, res.OutOfMoves, label + " outOfMoves");
            Assert.IsNull(res.TeardownCause, label + " teardownCause");
        }

        public static void AssertApplied(MoveResult res, bool won = false, bool outOfMoves = false, string? label = null) =>
            AssertResult(res, MoveStatuses.Applied, null, won, outOfMoves, label);

        /// <summary>What `Drop` did: the K-07 class of the release, the result, the events and, for a site release, the K-35 step 3 outcome (rebuilt from the events).</summary>
        public sealed class DropResult
        {
            public readonly DropClass Drop;
            public readonly MoveResult Res;
            public readonly List<GameEvent> Ev;
            /// <summary>Step 3 outcome rebuilt from `placementCorrect` / `mortarStuck` / `pieceBounced` (`At` / `Target.To` = the piece's place after the move; `Target.DropFrom` unknown).</summary>
            public readonly PlacementOutcome? Outcome;
            /// <summary>Step 8 from `segmentCompleted` / `siteShifted` (`Enqueued` is not rebuilt: empty).</summary>
            public readonly SegmentCompletion? Completed;
            /// <summary>Step 9 from `deliveryArrived` + `pieceFell{delivery}` / `deliveryQueued` (or the queue length after the move).</summary>
            public readonly DeliveryResult Delivery;

            public DropResult(DropClass drop, MoveResult res, List<GameEvent> ev, PlacementOutcome? outcome, SegmentCompletion? completed, DeliveryResult delivery)
            {
                Drop = drop;
                Res = res;
                Ev = ev;
                Outcome = outcome;
                Completed = completed;
                Delivery = delivery;
            }

            public bool Correct => Outcome != null && Outcome.Kind == PlacementOutcome.KindCorrect;
        }

        /// <summary>
        /// C# stand-in for the TS test helper `run(s, dragTo(id, ix, iy[, gap]))`: the full K-35 pipeline (`Moves.ApplyMove`,
        /// strict, step 12 on, the level's registry hooks unless `hooks` is given — like TS `run`). A cancelled release throws
        /// (the P1/P2 tests were written against that; the TS tests that assert a cancel use `Run` and read `Res.Reason`).
        /// `DropResult` keeps the step 3 / 8 / 9 views of the P2 tests, rebuilt from the events and the state after the move.
        /// </summary>
        public static DropResult Drop(GameState s, int id, int ix, int iy, int? gap = null, MoveHooks? hooks = null)
        {
            var attempt = Movement.TryBeginDrag(s, id, (hooks ?? MinikUsta.Core.Obstacles.Registry.LevelHooks(s.Lvl)).Drag);
            if (!attempt.Ok) throw new InvalidOperationException("Drop: piece " + id + " cannot be picked (" + attempt.Reason + ")");
            var node = new DragNode(ix, iy, gap.HasValue ? Movement.RailMode(gap.Value) : Movement.Free);
            var drop = attempt.Session!.Classify(node);
            if (drop.IsCancelled) throw new InvalidOperationException("Drop: release " + Key(node) + " of piece " + id + " is cancelled (" + drop.Reason + ")");
            var ran = Run(s, new DragMove(id, node), hooks);
            if (ran.Res.Status != MoveStatuses.Applied) throw new InvalidOperationException("Drop: piece " + id + " to " + Key(node) + " " + ran.Res);
            var ev = ran.Ev;
            int elev = Hdr(s, H.Elev);
            PlacementOutcome? outcome = null;
            foreach (var e in ev)
            {
                if (e is PlacementCorrectEvent pc && pc.PieceId == id)
                {
                    var revealed = ev.OfType<CellsRevealedEvent>().FirstOrDefault(r => r.Step == 3);
                    outcome = PlacementOutcome.Correct(Placement.PiecePlace(s, id), pc.Cells, revealed != null ? revealed.Cells : new RevealedCell[0]);
                }
                else if (e is MortarStuckEvent ms && ms.PieceId == id)
                {
                    outcome = PlacementOutcome.Stuck(Placement.PiecePlace(s, id), ms.Reason, ms.MissingSupport);
                }
                else if (e is PieceBouncedEvent pb && pb.PieceId == id && pb.Step == 3)
                {
                    var place = Placement.PiecePlace(s, id);
                    int step = pb.ViaDrop ? 2 : place.Zone == Zone.Names[Zone.Queue] ? 3 : 1;
                    outcome = PlacementOutcome.Bounced(new Anchor(pb.From.X, pb.From.Y - elev), new ReturnTarget(step, place, null), pb.Reason, pb.MissingSupport);
                }
            }
            SegmentCompletion? completed = null;
            var seg = ev.OfType<SegmentCompletedEvent>().FirstOrDefault();
            if (seg != null)
            {
                var shifted = ev.OfType<SiteShiftedEvent>().FirstOrDefault();
                completed = new SegmentCompletion(seg.Seg, Hdr(s, H.DeliveryCursor), shifted != null ? shifted.ToSeg : (int?)null, new int[0]);
            }
            var delivered = ev.OfType<PieceFellEvent>().Where(f => f.Cause == FallCauses.Delivery)
                .Select(f => new Delivered(f.PieceId, new Anchor(f.From.X, f.From.Y), new Anchor(f.To.X, f.To.Y), f.Rows)).ToArray();
            var queued = ev.OfType<DeliveryQueuedEvent>().LastOrDefault();
            var delivery = new DeliveryResult(delivered, queued != null ? queued.Queued : Hdr(s, H.QueueLen));
            return new DropResult(drop, ran.Res, ev, outcome, completed, delivery);
        }

        // --- state setup -----------------------------------------------------------------------------------------------------

        public static DragSession Drag(GameState s, int id, DragRules? rules = null)
        {
            var attempt = Movement.TryBeginDrag(s, id, rules);
            if (!attempt.Ok) throw new InvalidOperationException("piece " + id + " is not pickable: " + attempt.Reason);
            return attempt.Session!;
        }

        /// <summary>Moves a piece onto the site (global column x / plan row y) of segment `seg` (default: the visible one); test setup, independent of Placement.</summary>
        public static void ToSite(GameState s, int id, int x, int y, bool locked = true, bool stuck = false, int? seg = null)
        {
            int sg = seg ?? Grid.VisibleSegment(s);
            Grid.VacatePiece(s, id);
            SetPieceField(s, id, PF.Zone, Zone.Site);
            SetPieceField(s, id, PF.X, x);
            SetPieceField(s, id, PF.Y, y);
            SetPieceField(s, id, PF.Seg, sg);
            SetFlag(s, id, FlagBit.Locked, locked);
            SetFlag(s, id, FlagBit.Stuck, stuck);
            Grid.OccupyPiece(s, id);
            Grid.RefreshSiteMasks(s, sg);
        }

        /// <summary>Takes a piece off the board (as if it had been moved away).</summary>
        public static void Remove(GameState s, int id)
        {
            Grid.VacatePiece(s, id);
            SetPieceField(s, id, PF.Zone, Zone.Gone);
        }

        public static int[] Snapshot(GameState s) => (int[])s.Buf.Clone();

        // --- assertions ----------------------------------------------------------------------------------------------------

        public static void ExpectConsistent(GameState s)
        {
            var errors = Grid.StateInvariantErrors(s);
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        }

        public static Verdict V(bool ok, string[] reasons, At[] missing) => new Verdict(ok, reasons, missing);
        public static readonly Verdict OkV = new Verdict(true, new string[0], new At[0]);
        public static Verdict Wrong(string[] reasons, params At[] missing) => new Verdict(false, reasons, missing);
        public static string[] Reasons(params string[] r) => r;
        public static At[] Missing(params At[] a) => a;
        public static readonly At[] NoCells = new At[0];

        public static void AssertVerdict(Verdict v, bool ok, string[] reasons, At[] missing, string? label = null)
        {
            Assert.AreEqual(ok, v.Ok, label + " ok");
            CollectionAssert.AreEqual(reasons, v.Reasons, label + " reasons");
            CollectionAssert.AreEqual(missing, v.MissingSupport, label + " missingSupport");
        }

        /// <summary>TS `toMatchObject` on a DropClass: only the given fields are compared.</summary>
        public static void AssertDrop(DropClass d, string? kind = null, string? reason = null, int? row = null, int? gap = null, string? label = null)
        {
            if (kind != null) Assert.AreEqual(kind, d.Kind, label + " kind");
            if (reason != null) Assert.AreEqual(reason, d.Reason, label + " reason");
            if (row.HasValue) Assert.AreEqual(row.Value, d.Row, label + " row");
            if (gap.HasValue) Assert.AreEqual(gap.Value, d.Gap, label + " gap");
        }

        public static void AssertCancel(DropClass d, string reason, int? row = null, string? label = null) => AssertDrop(d, DropKinds.Cancel, reason, row, null, label);

        public static void AssertPickFails(DragAttempt a, string reason, string? label = null)
        {
            Assert.IsFalse(a.Ok, label + " ok");
            Assert.AreEqual(reason, a.Reason, label + " reason");
        }

        public static void AssertFall(FallResult f, string? mode = null, Anchor? landing = null, int? dir = null, int? distance = null, int? drift = null, string? label = null)
        {
            if (mode != null) Assert.AreEqual(mode, f.Mode, label + " mode");
            if (landing.HasValue) Assert.AreEqual(landing.Value, f.Landing, label + " landing");
            if (dir.HasValue) Assert.AreEqual(dir.Value, f.Dir, label + " dir");
            if (distance.HasValue) Assert.AreEqual(distance.Value, f.Distance, label + " distance");
            if (drift.HasValue) Assert.AreEqual(drift.Value, f.Drift, label + " drift");
        }

        public static void AssertSteer(Steer? got, int dir, int atRow)
        {
            Assert.IsTrue(got.HasValue, "steered is null");
            Assert.AreEqual(dir, got!.Value.Dir);
            Assert.AreEqual(atRow, got.Value.AtRow);
        }

        public static ShadowInfo Shadow(string tone, string[] reasons, At[] missing, bool breaks) => new ShadowInfo(tone, reasons, missing, breaks);
        public static readonly ShadowInfo NeutralShadow = new ShadowInfo(ShadowInfo.ToneNeutral, new string[0], new At[0], false);
        public static readonly ShadowInfo CorrectShadow = new ShadowInfo(ShadowInfo.ToneCorrect, new string[0], new At[0], false);

        public static YardMotion Motion(string kind, int id, Anchor from, Anchor to, int dy) => new YardMotion(kind, id, from, to, dy);
        public static YardMotion PieceMotion(int id, int fx, int fy, int tx, int ty) => new YardMotion(YardMotion.KindPiece, id, A(fx, fy), A(tx, ty), ty - fy);

        /// <summary>One drag of a golden log (TS `handMoves`: `{ kind: 'drag', pieceId, to }`).</summary>
        public readonly struct HandMove
        {
            public readonly int PieceId;
            public readonly DragNode To;

            public HandMove(int pieceId, DragNode to)
            {
                PieceId = pieceId;
                To = to;
            }

            public int? Gap => To.Mode == Movement.Free ? (int?)null : To.Mode - 1;
        }

        /// <summary>
        /// The canonical solution of a Faz 2R level (LEVELS §2 "Kanonik çözüm", GDD K-50 item 4) as drag moves, read from the
        /// golden file (TS `HAND[level]` / `handMoves(level)`); `start` and `undo` actions are skipped.
        /// </summary>
        public static HandMove[] HandMoves(int level)
        {
            var golden = TestPaths.LoadGolden("level_" + level.ToString("000") + ".hand.json");
            var o = new List<HandMove>();
            foreach (var a in golden["log"]!.Items)
            {
                if (a["kind"]!.AsString != "drag") continue;
                var to = a["to"]!;
                o.Add(new HandMove(a["pieceId"]!.AsInt, new DragNode(to["ix"]!.AsInt, to["iy"]!.AsInt, to["mode"]!.AsInt)));
            }
            return o.ToArray();
        }

        /// <summary>Plays canonical moves `[from, to)` of `level` on `s` with `Drop` (TS `for (const m of HAND[n].slice(from, to)) run(s, m)`).</summary>
        public static DropResult? Play(GameState s, int level, int from, int to)
        {
            var moves = HandMoves(level);
            DropResult? last = null;
            for (int i = from; i < Math.Min(to, moves.Length); i++) last = Drop(s, moves[i].PieceId, moves[i].To.Ix, moves[i].To.Iy, moves[i].Gap);
            return last;
        }

        public static DropResult Play(GameState s, HandMove m) => Drop(s, m.PieceId, m.To.Ix, m.To.Iy, m.Gap);

        /// <summary>The canonical first move of a golden (`log[1]`) and the described first step (`steps[0]`).</summary>
        public static (int pieceId, DragNode to, string stepKind) FirstHandMove(int level)
        {
            var golden = TestPaths.LoadGolden("level_" + level.ToString("000") + ".hand.json");
            JsonValue? first = null;
            foreach (var a in golden["log"]!.Items)
            {
                if (a["kind"]!.AsString == "drag")
                {
                    first = a;
                    break;
                }
            }
            if (first == null) throw new InvalidOperationException("level " + level + ": no canonical first move");
            var to = first["to"]!;
            var step = golden["steps"]![0];
            return (first["pieceId"]!.AsInt, new DragNode(to["ix"]!.AsInt, to["iy"]!.AsInt, to["mode"]!.AsInt), step["kind"]!.AsString);
        }

        /// <summary>`SHAPES.filter(s => s.index === s.canonicalIndex)`.</summary>
        public static ShapeDef[] Canonical => Shapes.All.Where(sh => sh.Index == sh.CanonicalIndex).ToArray();
    }
}
