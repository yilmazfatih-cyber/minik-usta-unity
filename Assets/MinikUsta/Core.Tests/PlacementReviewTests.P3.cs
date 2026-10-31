#nullable enable
// Port of tests/review/placement.review.test.ts — the tests that go through the K-35 pipeline (`Moves.ApplyMove`, TS
// `drag(s, id, to)` = strict + `noTruckHelp`) or `GameSession` (P3): K-17 / K-34 / K-22 pipeline examples, the K-18
// "shadow is the move result" properties (free fall and rail, seeded random boards), S4 debris through the pipeline and
// K-14 vs K-39 Undo. Same partial class as PlacementReviewTests.cs (Mortar, RandomBoard, BuildBoard are shared).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    public partial class PlacementReviewTests
    {
        private static At S(int x, int y, int seg = 0) => At.Site(x, y, seg);

        /// <summary>TS `drag(s, pieceId, to)`: `applyMove` strict, step 12 off; the move must apply. Returns the events.</summary>
        private static List<GameEvent> DragEv(GameState s, int pieceId, DragNode to)
        {
            var sink = new ArraySink();
            var res = Moves.ApplyMove(s, new DragMove(pieceId, to), sink, new ApplyOptions(strict: true, noTruckHelp: true));
            Assert.AreEqual(MoveStatuses.Applied, res.Status, "drag " + pieceId + " to " + Key(to));
            return sink.Events;
        }

        private static MoveResult ApplyStrict(GameState s, int pieceId, DragNode to, ArraySink sink) =>
            Moves.ApplyMove(s, new DragMove(pieceId, to), sink, new ApplyOptions(strict: true, noTruckHelp: true));

        private static string[] WWh(int h) => Enumerable.Repeat("WW", h).ToArray();

        private static T Single<T>(IEnumerable<GameEvent> ev) where T : GameEvent
        {
            var xs = ev.OfType<T>().ToArray();
            Assert.AreEqual(1, xs.Length, "one " + typeof(T).Name);
            return xs[0];
        }

        private static void AssertBounce(PieceBouncedEvent e, int pieceId, At? from = null, At? to = null, bool? viaDrop = null, string? reason = null, At[]? missing = null, string? label = null)
        {
            Assert.AreEqual(pieceId, e.PieceId, label + " pieceId");
            if (from.HasValue) Assert.AreEqual(from.Value, e.From, label + " from");
            if (to.HasValue) Assert.AreEqual(to.Value, e.To, label + " to");
            if (viaDrop.HasValue) Assert.AreEqual(viaDrop.Value, e.ViaDrop, label + " viaDrop");
            if (reason != null) Assert.AreEqual(reason, e.Reason, label + " reason");
            if (missing != null) CollectionAssert.AreEqual(missing, e.MissingSupport, label + " missingSupport");
        }

        private static void AssertWrong(PlacementWrongEvent e, int pieceId, string[] reasons, At[]? missing = null, string? label = null)
        {
            Assert.AreEqual(pieceId, e.PieceId, label + " pieceId");
            CollectionAssert.AreEqual(reasons, e.Reasons, label + " reasons");
            if (missing != null) CollectionAssert.AreEqual(missing, e.MissingSupport, label + " missingSupport");
        }

        private static At[] SiteCells(BoardCell[] cells) => cells.Select(c => S(c.X, c.Y)).ToArray();

        // --- review: K-17 / K-34 / K-22 through applyMove -----------------------------------------------------------------

        [Test(Description = "K-17 GDD example: B1 Y from (3,7) onto a W cell bounces back to (3,7); 9 → 8, streak 0, bounce event")]
        public void K17_GDD_example_B1_Y_onto_a_W_cell_bounces_back()
        {
            var s = Init(new LevelSpec { Moves = 9, Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "Y", 3, 7), Pc("B1_0", "W", 0, 0)) });
            SetHdr(s, H.Combo, 2);
            var ev = DragEv(s, 0, N(6, 8));
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
            Assert.AreEqual(0, Hdr(s, H.Combo));
            Assert.AreEqual(1, Hdr(s, H.Turn));
            Assert.AreEqual(YardP(3, 7), Placement.PiecePlace(s, 0));
            Assert.AreEqual(0, SiteOcc(s, 0, 0, 0));
            AssertWrong(Single<PlacementWrongEvent>(ev), 0, Reasons("color"));
            AssertBounce(Single<PieceBouncedEvent>(ev), 0, S(6, 0), YardAt(3, 7), false, "color", NoCells);
            CollectionAssert.IsEmpty(ev.OfType<PlacementCorrectEvent>().ToArray());
            ExpectConsistent(s);
        }

        [Test(Description = "E-43 through the pipeline: a W block into column 7 bounces with support/[(7,1)]; once the mortar leaves, (7,2) is a front cell")]
        public void E43_through_the_pipeline_a_W_block_into_column_7_bounces_with_support()
        {
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "R", 1, 0, new[] { "mortar" }), Pc("B1_0", "W", 2, 0)),
            });
            ToSite(s, 0, 7, 0); // (7,0) correctly filled
            ToSite(s, 1, 7, 1, locked: false, stuck: true); // mortar stuck on the `.` cell (7,1) (E-24)
            CollectionAssert.AreEqual(new[] { S(6, 0) }, Placement.BuildFront(s));
            var ev = DragEv(s, 2, N(7, 8));
            AssertWrong(Single<PlacementWrongEvent>(ev), 2, Reasons("support"), new[] { S(7, 1) });
            AssertBounce(Single<PieceBouncedEvent>(ev), 2, S(7, 2), null, null, "support", new[] { S(7, 1) });
            Assert.AreEqual(YardP(2, 0), Placement.PiecePlace(s, 2));
            // the mortar is dragged out to the yard: (7,1) is an empty `.` again, the front comes back the same move
            DragEv(s, 1, N(5, 7));
            Assert.AreEqual(YardP(5, 7), Placement.PiecePlace(s, 1));
            Assert.IsFalse(HasFlag(s, 1, FlagBit.Stuck));
            Assert.AreEqual(0, WrongOccMask(s, 0, 1));
            CollectionAssert.AreEqual(new[] { S(6, 0), S(7, 2) }, Placement.BuildFront(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-34 example 1 through the pipeline: O4 bounces for support, after a D2_0 W in column 7 it is correct and the level is won")]
        public void K34_example_1_through_the_pipeline()
        {
            var s = Init(new LevelSpec { Moves = 10, Plan = WWh(4), Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("O4_0", "W", 2, 0)) });
            DragEv(s, 0, N(6, 8)); // D2_0 W → (6,0)–(6,1), correct
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            var wrong = DragEv(s, 2, N(6, 8)); // O4 W → (6,2): (7,0), (7,1) empty
            AssertBounce(Single<PieceBouncedEvent>(wrong), 2, S(6, 2), null, null, "support", new[] { S(7, 0), S(7, 1) });
            Assert.AreEqual(YardP(2, 0), Placement.PiecePlace(s, 2));
            DragEv(s, 1, N(7, 8)); // "Doğru hamle: önce bir D2_0 W'yi sütun 7'ye bırakmak."
            var sink = new ArraySink();
            var res = ApplyStrict(s, 2, N(6, 8), sink);
            var pc = Single<PlacementCorrectEvent>(sink.Events);
            Assert.AreEqual(2, pc.PieceId);
            CollectionAssert.AreEqual(new[] { S(6, 2), S(7, 2), S(6, 3), S(7, 3) }, pc.Cells);
            Assert.IsTrue(res.Won);
            Assert.AreEqual(6, Hdr(s, H.MovesLeft));
            ExpectConsistent(s);
        }

        [Test(Description = "K-22 after the slide the next segment comes empty: falls ignore the completed segment, verdicts use the new plan")]
        public void K22_after_the_slide_the_next_segment_comes_empty()
        {
            // segment 0: WW; segment 1 (top → bottom): YY / RR
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "YY", "RR" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "Y", 2, 0), Pc("B1_0", "R", 3, 0)),
            });
            var ev = DragEv(s, 0, N(6, 8));
            Assert.AreEqual(0, Single<SegmentCompletedEvent>(ev).Seg);
            Assert.AreEqual(1, Hdr(s, H.ActiveSeg));
            Assert.AreEqual(1, SiteOcc(s, 0, 0, 0)); // the completed segment keeps its locked blocks (panorama)
            CollectionAssert.AreEqual(new[] { 0, 0 }, Gravity.SiteColumnMasks(s));
            var y = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 0), y.Landing);
            Assert.AreEqual(8, y.Distance);
            AssertVerdict(y.Verdict, false, Reasons("color"), NoCells);
            AssertVerdict(Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 6, 1)), false, Reasons("support"), new[] { S(6, 0, 1) });
            Assert.IsTrue(Gravity.ComputeFall(s, 2, N(7, 8)).Verdict.Ok);
            CollectionAssert.AreEqual(new[] { S(6, 0, 1), S(7, 0, 1) }, Placement.BuildFront(s));
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
            Assert.IsFalse(Placement.IsSegmentComplete(s, 1));
        }

        // --- review: segment, trowel, steer record --------------------------------------------------------------------------

        [Test(Description = "K-22 debris of segment 1 is invisible on segment 0; after the slide it blocks its column and bounces to its own start")]
        public void K22_debris_of_segment_1_is_invisible_on_segment_0()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW", "WW" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 2, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 0, 1) },
            });
            const int debris = 2;
            Assert.AreEqual(0b1, WrongOccMask(s, 1, 1));
            AssertPickFails(Movement.TryBeginDrag(s, debris), PickFailures.HiddenSegment);
            CollectionAssert.AreEqual(new[] { 0, 0 }, Gravity.SiteColumnMasks(s));
            Assert.AreEqual(A(7, 0), Gravity.ComputeFall(s, 1, N(7, 8)).Landing);
            Assert.IsTrue(Gravity.ComputeFall(s, 1, N(7, 8)).Verdict.Ok);
            DragEv(s, 0, N(6, 8)); // segment 0 complete → slide (K-22)
            Assert.AreEqual(1, Hdr(s, H.ActiveSeg));
            CollectionAssert.AreEqual(new[] { 0, 0b1 }, Gravity.SiteColumnMasks(s));
            CollectionAssert.AreEqual(new[] { S(6, 0, 1) }, Placement.BuildFront(s));
            var ev = DragEv(s, debris, N(6, 8));
            AssertBounce(Single<PieceBouncedEvent>(ev), debris, S(6, 0, 1), S(7, 0, 1), false, "color");
            Assert.AreEqual(SiteP(7, 0, 1), Placement.PiecePlace(s, debris));
            Assert.AreEqual(0b1, WrongOccMask(s, 1, 1));
            ExpectConsistent(s);
        }

        [Test(Description = "K-19 normal gravity has no steering: a drag record with steer never shifts the block (refused or ignored)")]
        public void K19_normal_gravity_has_no_steering_a_drag_record_with_steer_never_shifts_the_block()
        {
            var s = Init(new LevelSpec { Plan = WWh(2), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            var before = Snapshot(s);
            var sink = new ArraySink();
            var res = Moves.ApplyMove(s, new DragMove(0, N(6, 8), null, new Steer(1, 4)), sink, new ApplyOptions(noTruckHelp: true));
            CollectionAssert.IsEmpty(sink.Events.OfType<SteeredEvent>().ToArray());
            if (res.Status == MoveStatuses.Applied) Assert.AreEqual(SiteP(6, 0, 0), Placement.PiecePlace(s, 0));
            else CollectionAssert.AreEqual(before, s.Buf);
        }

        [Test(Description = "K-34 example 2 through the pipeline: the rail lintel bounces while (6,1) is empty, then is correct over the empty `.`")]
        public void K34_example_2_through_the_pipeline_the_rail_lintel()
        {
            // plan (bottom → top) y0 WW, y1 W., y2 WW; static gap y = 2 (W1, K-12)
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Wall = Wall(4, Gap(2, 1)),
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 4, 2), Pc("B1_0", "W", 0, 7)),
            });
            ToSite(s, 0, 6, 0); // (6,0)–(7,0) filled
            var wrong = DragEv(s, 1, R(0, 6, 2));
            AssertBounce(Single<PieceBouncedEvent>(wrong), 1, S(6, 2), YardAt(4, 2), null, "support", new[] { S(6, 1) });
            Assert.AreEqual(9, Hdr(s, H.MovesLeft));
            DragEv(s, 2, N(6, 8)); // B1 W falls onto (6,1)
            Assert.AreEqual(SiteP(6, 1, 0), Placement.PiecePlace(s, 2));
            Assert.IsTrue(HasFlag(s, 2, FlagBit.Locked));
            var sink = new ArraySink();
            var res = ApplyStrict(s, 1, R(0, 6, 2), sink);
            var pc = Single<PlacementCorrectEvent>(sink.Events);
            Assert.AreEqual(1, pc.PieceId);
            CollectionAssert.AreEqual(new[] { S(6, 2), S(7, 2) }, pc.Cells);
            Assert.IsFalse(pc.OverWall);
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1)); // the `.` stays empty under the lintel
            Assert.IsTrue(res.Won);
            Assert.AreEqual(7, Hdr(s, H.MovesLeft));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 GDD example through the pipeline: a stuck mortar dropped off the plan bounces to its start (step 1) and stays stuck")]
        public void K17_GDD_example_through_the_pipeline_a_stuck_mortar_dropped_off_the_plan_bounces_to_its_start()
        {
            // plan height 3, column 7 rows 0–2 filled, (6,0)–(6,1) filled, B1 mortar stuck at (6,2); released at (7,8) → (7,3)
            var s = Init(new LevelSpec { Moves = 10, Plan = WWh(3), Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("B1_0", "R", 2, 0, new[] { "mortar" })) });
            ToSite(s, 0, 7, 0);
            ToSite(s, 1, 6, 0);
            ToSite(s, 2, 6, 2, locked: false, stuck: true);
            var ev = DragEv(s, 2, N(7, 8));
            var fell = Single<PieceFellEvent>(ev);
            CollectionAssert.AreEqual(new object[] { 2, S(7, 8), S(7, 3), 5 }, new object[] { fell.PieceId, fell.From, fell.To, fell.Rows });
            CollectionAssert.IsEmpty(ev.OfType<MortarStuckEvent>().ToArray());
            AssertBounce(Single<PieceBouncedEvent>(ev), 2, S(7, 3), S(6, 2), false, "outside", NoCells);
            Assert.AreEqual(SiteP(6, 2, 0), Placement.PiecePlace(s, 2));
            Assert.IsTrue(HasFlag(s, 2, FlagBit.Stuck));
            CollectionAssert.AreEqual(new[] { 0b100, 0 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 3));
            ExpectConsistent(s);
        }

        // --- round 2: K-18 the shadow is the move result -----------------------------------------------------------------------

        [Test(Description = "K-18 on random boards the shadow (computeFall + shadowInfo) predicts applyMove: lock at the shadow or K-17 bounce to the yard start")]
        public void K18_on_random_boards_the_shadow_predicts_applyMove()
        {
            // GDD K-18: "bırakılırsa duracağı konum gölge olarak her zaman doğru gösterilir"; K-17 (1): saha başlangıcı her zaman boş
            var moving = Shapes.All.Where(d => d.Id == d.Canonical && d.W <= 2 && d.H <= 4 && !d.Heavy).Select(d => d.Id).ToArray();
            var colors = new[] { "W", "Y" };
            var rng = new Mulberry32(18_2026);
            int correct = 0, wrong = 0, skipped = 0;
            var seen = new HashSet<string>();
            for (int round = 0; round < 300; round++)
            {
                int h = 1 + rng.NextInt(8);
                var plan = new[] { new string[h], new string[h] };
                for (int sx = 0; sx < 2; sx++)
                    for (int sy = 0; sy < h; sy++)
                        plan[sx][sy] = rng.NextInt(5) == 0 ? "." : colors[rng.NextInt(2)];
                if (!plan.Any(col => col.Any(ch => ch != "."))) plan[0][0] = "W";
                var rows = new List<string>();
                for (int sy = h - 1; sy >= 0; sy--) rows.Add(plan[0][sy] + plan[1][sy]);
                var blocks = new List<(int x, int y, string kind, string color)>();
                var debris = new List<D>();
                var trowels = new List<(int sx, int sy)>();
                // a reachable board: per column a correctly built prefix (locked / trowel, empty `.`), wrong objects above it
                for (int sx = 0; sx < 2; sx++)
                {
                    int built = rng.NextInt(h + 1);
                    for (int sy = 0; sy < 8; sy++)
                    {
                        string? ch = sy < h ? plan[sx][sy] : null;
                        if (sy < built)
                        {
                            if (ch == ".") continue;
                            if (rng.NextInt(4) == 0) trowels.Add((sx, sy));
                            else blocks.Add((6 + sx, sy, Locked, ch!));
                            continue;
                        }
                        int roll = rng.NextInt(14);
                        if (ch != null && roll == 0) blocks.Add((6 + sx, sy, Stuck, "R"));
                        else if (roll == 1 && debris.Count < 2) debris.Add(new D("B1_0", "R", 6 + sx, sy));
                    }
                }
                var common = new[] { "B1_0", "B1_0", "D2_0", "D2_90", "O4_0" };
                string shape = rng.NextInt(2) == 0 ? common[rng.NextInt(common.Length)] : moving[rng.NextInt(moving.Length)];
                // half of the time the colour of a front cell (more correct placements), else random
                var fronts = new List<string>();
                for (int sx = 0; sx < 2; sx++)
                {
                    int fsy = -1;
                    for (int yy = 0; yy < plan[sx].Length; yy++)
                    {
                        int cx = sx, cy = yy;
                        if (plan[sx][yy] != "." && !blocks.Any(b => b.x == 6 + cx && b.y == cy) && !trowels.Any(t => t.sx == cx && t.sy == cy))
                        {
                            fsy = yy;
                            break;
                        }
                    }
                    if (fsy >= 0) fronts.Add(plan[sx][fsy]);
                }
                string? fc = null;
                if (rng.NextInt(2) == 0)
                {
                    int fi = rng.NextInt(Math.Max(fronts.Count, 1));
                    fc = fi < fronts.Count ? fronts[fi] : null;
                }
                string color = fc ?? colors[rng.NextInt(2)];
                var pieces = new List<P> { Pc(shape, color, 2, 4) };
                for (int i = 0; i < blocks.Count; i++) pieces.Add(Pc("B1_0", blocks[i].color, i % 6, i / 6));
                var baseState = Init(new LevelSpec { Moves = 20, Plan = rows.ToArray(), Pieces = pieces.ToArray(), Debris = debris.ToArray() });
                for (int i = 0; i < blocks.Count; i++) ToSite(baseState, 1 + i, blocks[i].x, blocks[i].y, locked: blocks[i].kind == Locked, stuck: blocks[i].kind == Stuck);
                foreach (var t in trowels) SetSiteOcc(baseState, 0, t.sx, t.sy, SiteTrowel);
                Grid.RefreshSiteMasks(baseState, 0);
                ExpectConsistent(baseState);
                var def = Shapes.ShapeById(shape);
                for (int ix = 6; ix <= 8 - def.W; ix++)
                {
                    var node = N(ix, 10 - def.H);
                    var s = CloneState(baseState);
                    FallResult fall;
                    try
                    {
                        fall = Gravity.ComputeFall(s, 0, node);
                    }
                    catch (Exception)
                    {
                        skipped++;
                        continue;
                    }
                    var shadow = Gravity.ShadowInfo(fall, "easy");
                    var sink = new ArraySink();
                    var res = Moves.ApplyMove(s, new DragMove(0, node), sink, new ApplyOptions(noTruckHelp: true));
                    if (res.Status != MoveStatuses.Applied)
                    {
                        skipped++;
                        continue;
                    }
                    string ctx = shape + " ix " + ix + " h " + h + " landing " + fall.Landing.Ix + "," + fall.Landing.Iy;
                    var fell = sink.Events.OfType<PieceFellEvent>().Where(e => e.Cause == FallCauses.Release).ToArray();
                    Assert.AreEqual(1, fell.Length, ctx);
                    CollectionAssert.AreEqual(new object[] { 0, S(node.Ix, node.Iy), S(fall.Landing.Ix, fall.Landing.Iy), fall.Distance },
                        new object[] { fell[0].PieceId, fell[0].From, fell[0].To, fell[0].Rows }, ctx);
                    Assert.AreEqual(19, Hdr(s, H.MovesLeft), ctx);
                    if (fall.Verdict.Ok)
                    {
                        Assert.AreEqual(ShadowInfo.ToneCorrect, shadow.Tone, ctx);
                        var pc = Single<PlacementCorrectEvent>(sink.Events);
                        Assert.AreEqual(0, pc.PieceId, ctx);
                        CollectionAssert.AreEqual(SiteCells(fall.Cells), pc.Cells, ctx);
                        Assert.IsTrue(pc.OverWall, ctx);
                        CollectionAssert.IsEmpty(sink.Events.OfType<PieceBouncedEvent>().ToArray(), ctx);
                        Assert.AreEqual(SiteP(fall.Landing.Ix, fall.Landing.Iy, 0), Placement.PiecePlace(s, 0), ctx);
                        Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked), ctx);
                        correct++;
                    }
                    else
                    {
                        Assert.AreEqual(Shadow(ShadowInfo.ToneWrong, fall.Verdict.Reasons, fall.Verdict.MissingSupport, false), shadow, ctx);
                        AssertWrong(Single<PlacementWrongEvent>(sink.Events), 0, fall.Verdict.Reasons, fall.Verdict.MissingSupport, ctx);
                        AssertBounce(Single<PieceBouncedEvent>(sink.Events), 0, S(fall.Landing.Ix, fall.Landing.Iy), YardAt(2, 4), false, fall.Verdict.Reasons[0], fall.Verdict.MissingSupport, ctx);
                        Assert.AreEqual(YardP(2, 4), Placement.PiecePlace(s, 0), ctx);
                        Assert.AreEqual(0, Hdr(s, H.Combo), ctx);
                        foreach (string r in fall.Verdict.Reasons) seen.Add(r);
                        wrong++;
                    }
                    ExpectConsistent(s);
                }
            }
            Assert.Greater(correct, 20);
            Assert.Greater(wrong, 200);
            Assert.Less(skipped, correct + wrong);
            CollectionAssert.AreEqual(new[] { "color", "outside", "support", "window" }, seen.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        // --- round 2: GDD examples through applyMove ---------------------------------------------------------------------------

        [Test(Description = "K-15 GDD example [\"YY\",\"W.\",\"WW\"]: S2 B1 into column 7 bounces (window); a Y bridge over the `.` completes it with (7,1) empty")]
        public void K15_GDD_example_S2_B1_into_column_7_bounces_window_a_Y_bridge_completes_it()
        {
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Plan = new[] { "YY", "W.", "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "W", 3, 0), Pc("D2_90", "Y", 4, 0)),
            });
            DragEv(s, 0, N(6, 8)); // (6,0)–(7,0)
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            var into = DragEv(s, 2, N(7, 8));
            var fell = Single<PieceFellEvent>(into);
            Assert.AreEqual(2, fell.PieceId);
            Assert.AreEqual(S(7, 1), fell.To);
            AssertBounce(Single<PieceBouncedEvent>(into), 2, S(7, 1), YardAt(3, 0), null, "window", NoCells);
            DragEv(s, 1, N(6, 8)); // (6,1)
            CollectionAssert.AreEqual(new[] { S(6, 2), S(7, 2) }, Placement.BuildFront(s));
            var sink = new ArraySink();
            var res = ApplyStrict(s, 3, N(6, 8), sink);
            var pc = Single<PlacementCorrectEvent>(sink.Events);
            Assert.AreEqual(3, pc.PieceId);
            CollectionAssert.AreEqual(new[] { S(6, 2), S(7, 2) }, pc.Cells);
            Assert.IsTrue(pc.OverWall);
            Assert.IsFalse(res.Won); // K-48 (3): the bounced B1 W (piece 2) is still a material block in the yard
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1));
            CollectionAssert.AreEqual(new[] { 0b111, 0b101 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            Assert.AreEqual(6, Hdr(s, H.MovesLeft));
            ExpectConsistent(s);
        }

        [Test(Description = "K-14 GDD example: touching the D2_90 Y locked at (6,0) does nothing and spends no move")]
        public void K14_GDD_example_touching_the_locked_D2_90_Y_does_nothing_and_spends_no_move()
        {
            var s = Init(new LevelSpec { Moves = 10, Plan = new[] { "WW", "YY" }, Pieces = Ps(Pc("D2_90", "Y", 0, 0), Pc("B1_0", "W", 2, 0)) });
            DragEv(s, 0, N(6, 8));
            Assert.AreEqual(SiteP(6, 0, 0), Placement.PiecePlace(s, 0));
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            Assert.IsFalse(Movement.TryBeginDrag(s, 0).Ok);
            var before = Snapshot(s);
            var res = Moves.ApplyMove(s, new DragMove(0, N(0, 5)), new ArraySink(), new ApplyOptions(noTruckHelp: true));
            Assert.AreEqual(MoveStatuses.Cancelled, res.Status);
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreEqual(9, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "K-34 hook 3 through applyMove: a bounce whose primary reason is colour still carries the missing support cells")]
        public void K34_hook_3_through_applyMove_a_colour_bounce_still_carries_the_missing_support_cells()
        {
            // K-34 kanca 3: "K-17 geri sekmesinde birincil neden ve eksik destek hücreleri olayla birlikte yayınlanır"
            var s = Init(new LevelSpec { Moves = 10, Plan = WWh(2), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_90", "Y", 2, 0)) });
            DragEv(s, 0, N(6, 8)); // (6,0) correct, streak 1
            Assert.AreEqual(1, Hdr(s, H.Combo));
            var ev = DragEv(s, 1, N(6, 8)); // top(6) = 1, top(7) = 0 → lands (6,1)–(7,1)
            AssertWrong(Single<PlacementWrongEvent>(ev), 1, Reasons("color", "support"), new[] { S(7, 0) });
            AssertBounce(Single<PieceBouncedEvent>(ev), 1, S(6, 1), YardAt(2, 0), false, "color", new[] { S(7, 0) });
            Assert.AreEqual(YardP(2, 0), Placement.PiecePlace(s, 1));
            Assert.AreEqual(0, Hdr(s, H.Combo));
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
            ExpectConsistent(s);
        }

        // --- round 3: S4 debris through the pipeline ------------------------------------------------------------------------

        [Test(Description = "K-15 S4 a filled plan with debris above it completes only on the move that takes the debris out; Faz 2R K-48 (3): the debris is then a yard block, so the last move is no win but out of moves (E-01 order)")]
        public void K15_S4_a_filled_plan_with_debris_above_it_completes_only_on_the_move_that_takes_the_debris_out()
        {
            var s = Init(new LevelSpec
            {
                Moves = 2,
                Wall = Wall(1, Gap(0, 1)),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("D2_90", "W", 4, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 2) },
            });
            const int debris = 1;
            Assert.AreEqual(0, WrongOccMask(s, 0, 1)); // above the plan (h = 1): no K-34 bit
            var first = DragEv(s, 0, R(0, 6, 0));
            var pc = Single<PlacementCorrectEvent>(first);
            CollectionAssert.AreEqual(new[] { S(6, 0), S(7, 0) }, pc.Cells);
            Assert.IsFalse(pc.OverWall);
            CollectionAssert.AreEqual(new[] { 0b1, 0b1 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            CollectionAssert.IsEmpty(first.OfType<SegmentCompletedEvent>().ToArray());
            CollectionAssert.IsEmpty(first.OfType<LevelWonEvent>().ToArray());
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            CollectionAssert.IsEmpty(Placement.BuildFront(s));
            Assert.AreEqual(1, Hdr(s, H.MovesLeft));
            // the debris goes left through the open row 2 (≥ height 1) into the yard: a yard move (K-07 row 2)
            var sink = new ArraySink();
            var res = ApplyStrict(s, debris, N(5, 2), sink);
            Assert.AreEqual(MoveStatuses.Applied, res.Status);
            Assert.IsFalse(res.Won);
            Assert.IsTrue(res.OutOfMoves);
            Assert.AreEqual(YardP(5, 2), Placement.PiecePlace(s, debris));
            Assert.AreEqual(0, Single<SegmentCompletedEvent>(sink.Events).Seg);
            CollectionAssert.IsEmpty(sink.Events.OfType<LevelWonEvent>().ToArray());
            Assert.AreEqual(1, sink.Events.OfType<OutOfMovesEvent>().Count());
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-16 S4 Faz 2R (E-44): debris carried to the yard loses the debris flag and is placed like any block (its own colour with full support is correct)")]
        public void K16_S4_debris_carried_to_the_yard_loses_the_debris_flag_and_is_placed_like_any_block()
        {
            var s = Init(new LevelSpec { Moves = 10, Plan = new[] { "WW", "RR" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "R", 7, 3) } });
            const int debris = 1;
            DragEv(s, debris, N(5, 3));
            Assert.AreEqual(YardP(5, 3), Placement.PiecePlace(s, debris));
            Assert.IsFalse(HasFlag(s, debris, FlagBit.Debris));
            var fall = Gravity.ComputeFall(s, debris, N(6, 8));
            Assert.AreEqual(A(6, 0), fall.Landing);
            AssertVerdict(fall.Verdict, true, Reasons(), NoCells);
            var ev = DragEv(s, debris, N(6, 8));
            var pc = Single<PlacementCorrectEvent>(ev);
            Assert.AreEqual(debris, pc.PieceId);
            CollectionAssert.AreEqual(new[] { S(6, 0) }, pc.Cells);
            CollectionAssert.IsEmpty(ev.OfType<PieceBouncedEvent>().ToArray());
            Assert.AreEqual(SiteP(6, 0, 0), Placement.PiecePlace(s, debris));
            CollectionAssert.AreEqual(new[] { 0b1, 0 }, new[] { FilledMask(s, 0, 0), WrongOccMask(s, 0, 0) });
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
            ExpectConsistent(s);
        }

        [Test(Description = "K-12 K-17 debris that starts on its rail and is released at another site cell is wrong (Faz 2R: support only) and returns to its own start (step 1)")]
        public void K12_K17_debris_that_starts_on_its_rail_and_is_released_at_another_site_cell_is_wrong()
        {
            var s = Init(new LevelSpec { Moves = 10, Wall = Wall(4, Gap(3, 1)), Plan = WWh(4), Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "W", 7, 3) } });
            const int debris = 1;
            CollectionAssert.AreEqual(new[] { 0, 0b1000 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            CollectionAssert.AreEqual(new[] { S(6, 0), S(7, 0) }, Placement.BuildFront(s));
            var below = new[] { S(6, 0), S(6, 1), S(6, 2) };
            var rail = Gravity.ComputeFall(s, debris, R(0, 6, 3));
            Assert.AreEqual(FallResult.ModeRail, rail.Mode);
            Assert.AreEqual(A(6, 3), rail.Landing);
            AssertVerdict(rail.Verdict, false, Reasons("support"), below);
            Assert.AreEqual(Shadow(ShadowInfo.ToneNeutral, Reasons("support"), below, false), Gravity.ShadowInfo(rail, "hard"));
            var ev = DragEv(s, debris, R(0, 6, 3));
            var moved = Single<PieceMovedEvent>(ev);
            CollectionAssert.AreEqual(new object[] { debris, S(7, 3), S(6, 3), EntryKinds.Gap, 0 }, new object[] { moved.PieceId, moved.From, moved.To, moved.Entry, moved.Gap! });
            CollectionAssert.IsEmpty(ev.OfType<PieceFellEvent>().ToArray());
            AssertWrong(Single<PlacementWrongEvent>(ev), debris, Reasons("support"), below);
            AssertBounce(Single<PieceBouncedEvent>(ev), debris, S(6, 3), S(7, 3), false, "support", below);
            Assert.AreEqual(SiteP(7, 3, 0), Placement.PiecePlace(s, debris));
            CollectionAssert.AreEqual(new[] { 0, 0b1000 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            Assert.AreEqual(0, SiteOcc(s, 0, 0, 3));
            Assert.AreEqual(9, Hdr(s, H.MovesLeft));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 GDD example through the pipeline: debris D2_0 from (7,0)–(7,1) dropped into column 6 returns to (7,0)–(7,1); a W block on top of it is wrong (support)")]
        public void K17_GDD_example_through_the_pipeline_debris_D2_0_dropped_into_column_6_returns()
        {
            var s = Init(new LevelSpec { Moves = 10, Plan = WWh(3), Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("D2_0", "R", 7, 0) } });
            const int debris = 1;
            var fall = Gravity.ComputeFall(s, debris, N(6, 8));
            Assert.AreEqual(A(6, 0), fall.Landing);
            AssertVerdict(fall.Verdict, false, Reasons("color"), NoCells);
            var ev = DragEv(s, debris, N(6, 8));
            AssertBounce(Single<PieceBouncedEvent>(ev), debris, S(6, 0), S(7, 0), false, "color");
            Assert.AreEqual(SiteP(7, 0, 0), Placement.PiecePlace(s, debris));
            CollectionAssert.AreEqual(new[] { 0, 0b11 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            Assert.AreEqual(9, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(new[] { S(6, 0) }, Placement.BuildFront(s));
            var w = Gravity.ComputeFall(s, 0, N(7, 8));
            Assert.AreEqual(A(7, 2), w.Landing);
            AssertVerdict(w.Verdict, false, Reasons("support"), new[] { S(7, 0), S(7, 1) });
            var wrong = DragEv(s, 0, N(7, 8));
            AssertBounce(Single<PieceBouncedEvent>(wrong), 0, S(7, 2), YardAt(0, 0), null, "support");
            ExpectConsistent(s);
        }

        [Test(Description = "E-43 S2 debris in a `.` cell: a bridge over it is wrong (support); once the debris is in the yard the same bridge is correct and the `.` stays empty; Faz 2R K-48 (3): the yard debris block holds back the win")]
        public void E43_S2_debris_in_a_dot_cell_a_bridge_over_it_is_wrong_until_the_debris_is_in_the_yard()
        {
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0), Pc("B1_0", "W", 2, 0), Pc("D2_90", "W", 3, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 1) },
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            ToSite(s, 2, 6, 1);
            const int debris = 4;
            Assert.AreEqual(0b10, WrongOccMask(s, 0, 1));
            CollectionAssert.AreEqual(new[] { S(6, 2) }, Placement.BuildFront(s));
            var bridge = Gravity.ComputeFall(s, 3, N(6, 8));
            Assert.AreEqual(A(6, 2), bridge.Landing);
            AssertVerdict(bridge.Verdict, false, Reasons("support"), new[] { S(7, 1) });
            var wrong = DragEv(s, 3, N(6, 8));
            AssertBounce(Single<PieceBouncedEvent>(wrong), 3, S(6, 2), YardAt(3, 0), null, "support", new[] { S(7, 1) });
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            DragEv(s, debris, N(5, 2)); // (7,1) → (7,2) → (6,2) → (5,2): out to the yard
            Assert.AreEqual(0, WrongOccMask(s, 0, 1));
            CollectionAssert.AreEqual(new[] { S(6, 2), S(7, 2) }, Placement.BuildFront(s));
            var sink = new ArraySink();
            var res = ApplyStrict(s, 3, N(6, 8), sink);
            var fell = Single<PieceFellEvent>(sink.Events);
            CollectionAssert.AreEqual(new object[] { 3, S(6, 2), 6 }, new object[] { fell.PieceId, fell.To, fell.Rows });
            var pc = Single<PlacementCorrectEvent>(sink.Events);
            CollectionAssert.AreEqual(new[] { S(6, 2), S(7, 2) }, pc.Cells);
            Assert.IsTrue(pc.OverWall);
            // GDD E-44 (Faz 2R): the debris is an ordinary material block now, still in the yard (K-48 (3)): no win yet
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
            Assert.IsFalse(res.Won);
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1));
            CollectionAssert.AreEqual(new[] { 0b111, 0b101 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            Assert.AreEqual(7, Hdr(s, H.MovesLeft));
            ExpectConsistent(s);
        }

        [Test(Description = "K-14 K-39 Undo is the only way back for a locked block: afterwards it is at its yard start, pickable, and the masks and front are as before")]
        public void K14_K39_Undo_is_the_only_way_back_for_a_locked_block()
        {
            var session = GameSession.Start(Builders.Compiled(new LevelSpec { Moves = 10, Plan = WWh(2), Pieces = Ps(Pc("D2_0", "W", 3, 0), Pc("B1_0", "W", 0, 0)) }));
            var s = session.State;
            var front = Placement.BuildFront(s);
            int left = session.MovesLeft;
            CollectionAssert.AreEqual(new[] { S(6, 0), S(7, 0) }, front);
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(new DragMove(0, N(6, 8))).Status);
            Assert.AreEqual(SiteP(6, 0, 0), Placement.PiecePlace(s, 0));
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.Locked);
            Assert.AreEqual(0b11, FilledMask(s, 0, 0));
            CollectionAssert.AreEqual(new[] { S(7, 0) }, Placement.BuildFront(s));
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(YardP(3, 0), Placement.PiecePlace(s, 0));
            Assert.IsFalse(HasFlag(s, 0, FlagBit.Locked));
            Assert.IsTrue(Movement.TryBeginDrag(s, 0).Ok);
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            CollectionAssert.AreEqual(front, Placement.BuildFront(s));
            Assert.AreEqual(left, session.MovesLeft);
            ExpectConsistent(s);
        }

        // --- round 3: K-12 rail placements through applyMove -----------------------------------------------------------------

        [Test(Description = "K-18 K-12 on random boards the rail shadow (the block itself + verdict) predicts applyMove: lock in place over the gap (overWall false) or a K-17 bounce that leaves the site masks untouched")]
        public void K18_K12_on_random_boards_the_rail_shadow_predicts_applyMove()
        {
            var railShapes = new[] { "B1_0", "D2_90", "D2_0", "O4_0" };
            var rng = new Mulberry32(12_1803);
            int correct = 0, wrong = 0, skipped = 0;
            var seen = new HashSet<string>();
            for (int round = 0; round < 400; round++)
            {
                string shape = railShapes[rng.NextInt(railShapes.Length)];
                var def = Shapes.ShapeById(shape);
                int g = rng.NextInt(9 - def.H); // gap rows g … g + h − 1 ≤ 7
                int ix = 6 + rng.NextInt(3 - def.W); // w = 1: x 6 or 7; w = 2: x 6
                // rail path cells: columns 6 … ix + w − 1 in the gap rows stay free; the built prefix there stays below g
                Func<int, int, bool> onPath = (x, y) => x <= ix + def.W - 1 && y >= g && y < g + def.H;
                var b = MakeRandomBoard(rng, onPath, (sx, h) => 6 + sx <= ix + def.W - 1 ? Math.Min(g, h) : h);
                int sx0 = ix - 6;
                string? planColor = sx0 < b.Plan.Length && g < b.Plan[sx0].Length ? b.Plan[sx0][g] : null;
                string color = rng.NextInt(3) > 0 && planColor != null && planColor != "." ? planColor : "W";
                int startX = 6 - def.W;
                var s = BuildBoard(b, Pc(shape, color, startX, g), Wall(8, Gap(g, def.H)));
                ExpectConsistent(s);
                var node = R(0, ix, g);
                var fall = Gravity.ComputeFall(s, 0, node);
                AssertFall(fall, FallResult.ModeRail, A(ix, g), null, 0, 0);
                Assert.IsNull(fall.Steered);
                CollectionAssert.AreEqual(Movement.BlockCells(def, ix, g), fall.Cells);
                Func<int[]> masks = () => new[] { FilledMask(s, 0, 0), WrongOccMask(s, 0, 0), FilledMask(s, 0, 1), WrongOccMask(s, 0, 1) };
                var masksBefore = masks();
                var frontBefore = Placement.BuildFront(s);
                var siteBefore = new List<(int x, int y, int v)>();
                foreach (int x in new[] { 6, 7 })
                    for (int y = 0; y < 8; y++) siteBefore.Add((x, y, SiteOcc(s, 0, x - 6, y)));
                var counters = new[] { Hdr(s, H.OverWallCount), Hdr(s, H.RailCount) };
                var sink = new ArraySink();
                var res = Moves.ApplyMove(s, new DragMove(0, node), sink, new ApplyOptions(noTruckHelp: true));
                if (res.Status != MoveStatuses.Applied)
                {
                    skipped++;
                    continue;
                }
                string ctx = shape + " g " + g + " ix " + ix + " rows " + string.Join("/", b.Rows) + " ok " + fall.Verdict.Ok;
                var moved = Single<PieceMovedEvent>(sink.Events);
                CollectionAssert.AreEqual(new object[] { 0, YardAt(startX, g), S(ix, g), EntryKinds.Gap, 0 }, new object[] { moved.PieceId, moved.From, moved.To, moved.Entry, moved.Gap! }, ctx);
                CollectionAssert.IsEmpty(sink.Events.OfType<PieceFellEvent>().Where(e => e.Cause == FallCauses.Release).ToArray(), ctx);
                Assert.AreEqual(19, Hdr(s, H.MovesLeft), ctx);
                if (fall.Verdict.Ok)
                {
                    var pc = Single<PlacementCorrectEvent>(sink.Events);
                    CollectionAssert.AreEqual(SiteCells(fall.Cells), pc.Cells, ctx);
                    Assert.IsFalse(pc.OverWall, ctx);
                    Assert.AreEqual(SiteP(ix, g, 0), Placement.PiecePlace(s, 0), ctx);
                    Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked), ctx);
                    CollectionAssert.AreEqual(new[] { counters[0], counters[1] + 1 }, new[] { Hdr(s, H.OverWallCount), Hdr(s, H.RailCount) }, ctx);
                    // the block hangs on the rail: every other site cell (also an empty one under it) is as before
                    var others = siteBefore.Where(c => !fall.Cells.Any(o => o.X == c.x && o.Y == c.y)).ToArray();
                    CollectionAssert.AreEqual(others.Select(c => c.v).ToArray(), others.Select(c => SiteOcc(s, 0, c.x - 6, c.y)).ToArray(), ctx);
                    correct++;
                }
                else
                {
                    AssertWrong(Single<PlacementWrongEvent>(sink.Events), 0, fall.Verdict.Reasons, fall.Verdict.MissingSupport, ctx);
                    AssertBounce(Single<PieceBouncedEvent>(sink.Events), 0, S(ix, g), YardAt(startX, g), false, fall.Verdict.Reasons[0], fall.Verdict.MissingSupport, ctx);
                    Assert.AreEqual(YardP(startX, g), Placement.PiecePlace(s, 0), ctx);
                    CollectionAssert.AreEqual(masksBefore, masks(), ctx);
                    CollectionAssert.AreEqual(frontBefore, Placement.BuildFront(s), ctx);
                    CollectionAssert.AreEqual(counters, new[] { Hdr(s, H.OverWallCount), Hdr(s, H.RailCount) }, ctx);
                    foreach (string r in fall.Verdict.Reasons) seen.Add(r);
                    wrong++;
                }
                ExpectConsistent(s);
            }
            Assert.Greater(correct, 30);
            Assert.Greater(wrong, 150);
            Assert.Less(skipped, 40);
            CollectionAssert.AreEqual(new[] { "color", "outside", "support", "window" }, seen.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }
    }
}
