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

        /// <summary>`levels/level_NNN.json` loaded like the game does (TS `levelFile`: schema + runtime checks, then compile).</summary>
        public static CompiledLevel LevelFile(int id) => Compiler.LoadLevel(TestPaths.LoadLevelJson(id)).LevelOrThrow();
        public static GameState LevelState(int id) => CreateInitialState(LevelFile(id));

        /// <summary>What `Drop` did: the K-07 class of the release and, for a site release, the K-35 step 3 outcome.</summary>
        public sealed class DropResult
        {
            public readonly DropClass Drop;
            public readonly PlacementOutcome? Outcome;
            public readonly SegmentCompletion? Completed;
            public readonly DeliveryResult Delivery;

            public DropResult(DropClass drop, PlacementOutcome? outcome, SegmentCompletion? completed, DeliveryResult delivery)
            {
                Drop = drop;
                Outcome = outcome;
                Completed = completed;
                Delivery = delivery;
            }

            public bool Correct => Outcome != null && Outcome.Kind == PlacementOutcome.KindCorrect;
        }

        /// <summary>
        /// C# stand-in for the TS test helper `run(s, dragTo(id, ix, iy[, gap]))` until the K-35 pipeline (`applyMove`) is ported:
        /// the deterministic core steps of one drag without events — 0 classify (a cancel throws: the TS tests run strict), 1
        /// move (yard release), 2 fall (`ComputeFall` with `hooks.Fall`), 3 settle (`SettlePlacement` with `hooks.Placement`),
        /// 4 base cost 1 (`movesLeft`, `turn`, `movesSpent`), 6 `SettleYard`, 7 `CountDebrisLeftSite`, 8 segment completion +
        /// build goal, 9 delivery. Steps 5 (neighbour effects), 10 (timers), 11 (win flag) and 12 (deadlock) are NOT run.
        /// </summary>
        public static DropResult Drop(GameState s, int id, int ix, int iy, int? gap = null, MoveHooks? hooks = null)
        {
            hooks ??= MoveHooks.None;
            var attempt = Movement.TryBeginDrag(s, id, hooks.Drag);
            if (!attempt.Ok) throw new InvalidOperationException("Drop: piece " + id + " cannot be picked (" + attempt.Reason + ")");
            var node = new DragNode(ix, iy, gap.HasValue ? Movement.RailMode(gap.Value) : Movement.Free);
            var drop = attempt.Session!.Classify(node);
            if (drop.IsCancelled) throw new InvalidOperationException("Drop: release " + Key(node) + " of piece " + id + " is cancelled (" + drop.Reason + ")");
            var start = Placement.PiecePlace(s, id);
            int startZone = PieceZone(s, id);
            PlacementOutcome? outcome = null;
            if (drop.Kind == DropKinds.Yard) Placement.MovePiece(s, id, Placement.YardPlace(new Anchor(ix, iy)));
            else
            {
                var fall = Gravity.ComputeFall(s, id, node, new FallOptions(null, hooks.Fall));
                if (fall.Effect.Kind == LandingEffect.KindBreak) Placement.ReturnBrokenPiece(s, id, start);
                else outcome = Placement.SettlePlacement(s, id, fall.Landing, fall.Verdict, new SettleOptions(start, hooks.Placement));
            }
            if (outcome != null)
            {
                if (outcome.Kind == PlacementOutcome.KindCorrect) Combo.ComboOnCorrect(s);
                else Combo.ComboReset(s);
            }
            int left = Hdr(s, H.MovesLeft);
            SetHdr(s, H.MovesLeft, left - Math.Min(left, 1));
            SetHdr(s, H.Turn, Hdr(s, H.Turn) + 1);
            SetHdr(s, H.MovesSpent, Hdr(s, H.MovesSpent) + 1);
            Gravity.SettleYard(s);
            Goals.CountDebrisLeftSite(s, id, startZone);
            var done = Core.Site.SiteStrategy(s.Lvl).CompleteIfDone(s);
            if (done != null) Goals.SetBuildProgress(s, done.Completed);
            var delivery = Delivery.DeliverQueue(s);
            return new DropResult(drop, outcome, done, delivery);
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
