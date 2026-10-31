#nullable enable
// Port of tests/review/pipeline.review.test.ts (web repo), the scenario describes of the truck queue, the +5 offer
// window, Undo and resume/exit (P4 scenario tests): K-25 / K-26 truck delivery and the FIFO queue (rounds 1–3: E-03,
// E-04, E-34, the GDD worked examples, the queue counter, K-48 (3) win held back by the truck), K-28 / K-29 / K-35 step
// order and the out-of-moves window (offer exhaustion, decline, ad only on offer 1), K-39 Undo (streak, trowel, rail,
// +5, step-12 RNG help E-37, depth 1, level over), K-43 exit and resume (free / loss exit, tampered logs, E-42 RNG help
// replay, level 5 prefix-by-prefix resume, levelHash, replay sink = live stream). Expectations are the TS ones, value
// for value. The other describes of the file (K-22 / S1, K-33, K-46, K-35 step 5, W1 / S2) are covered by the earlier
// packages' ports of the same rules and stay listed in docs/PORT_MAP.md.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class PipelineReviewTests
    {
        // --- helpers ------------------------------------------------------------------------------------------------------

        private static DragMove Drag(int id, DragNode to) => new DragMove(id, to);
        private static GoldTrowelMove GoldTrowel(int id, int x, int y) => new GoldTrowelMove(id, x, y);
        /// <summary>TS `trowel(x, y, seg)`: the Faz 2 cell trowel (rejected since Faz 2R: `legacyTrowel`).</summary>
        private static TrowelMove Trowel(int x, int y, int seg = 0) => new TrowelMove(seg, x, y);

        private static Ran Play(GameState s, Move m, MoveHooks? hooks = null) => Run(s, m, hooks);

        private static int[] AtOf(GameState s, int id) => new[] { PieceX(s, id), PieceY(s, id), PieceZone(s, id) };

        private static string[] Steps(IEnumerable<GameEvent> ev) => ev.Select(e => e.TypeName + "@" + e.Step).ToArray();

        private static PieceFellEvent[] DeliveryFalls(IEnumerable<GameEvent> ev) => ev.OfType<PieceFellEvent>().Where(e => e.Cause == "delivery").ToArray();

        private static string FallText(PieceFellEvent e) => e.PieceId + ":" + e.From.X + "," + e.From.Y + "→" + e.To.Zone + ":" + e.To.X + "," + e.To.Y + ":" + e.Rows + "@" + e.Step;

        private static string ExitText(ExitResult x) => x.Kind + "|" + x.MovesMade + "|" + x.MovesSpent + "|" + string.Join(",", x.RefundPreBoosters) + "|" + x.StreakBonusConsumed;

        private static string OfferText(OfferInfo? o) => o.HasValue ? o.Value.N + (o.Value.AdAllowed ? "a" : "") : "null";

        private static string YaoText(YaoMeasure y) => y.OverWall + "," + y.Rail + "," + (y.Yao.HasValue ? JsNumber.Format(y.Yao.Value) : "null");

        private static CompiledLevel Lvl(LevelSpec spec) => Builders.Compiled(spec);

        /// <summary>A full yard column x (rows 0–7) of Y filler blocks.</summary>
        private static P[] FullColumn(int x) => Ps(Pc("I4_0", "Y", x, 0), Pc("I4_0", "Y", x, 4));
        /// <summary>Yard column x filled on rows 0–6 only (row 7 free).</summary>
        private static P[] Column7(int x) => Ps(Pc("I4_0", "Y", x, 0), Pc("I3_0", "Y", x, 4));

        private static P[] Cat(params P[][] parts) => parts.SelectMany(p => p).ToArray();

        private static string[][] Segs(params string[][] rows) => rows;
        private static string[] Row(params string[] r) => r;

        private static BatchSpec Batch(int forSegment, P[] pieces, int[]? dropColumns = null) => new BatchSpec { ForSegment = forSegment, Pieces = pieces, DropColumns = dropColumns };

        // --- fixtures (pipeline.review.test.ts) -----------------------------------------------------------------------------

        /// <summary>K-25 stage 3: ids 0 mover, 1–12 fillers, 13 = B1 R, 14 = B1 G (both x = 3).</summary>
        private static LevelSpec TIE() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 2, 7)), FullColumn(0), FullColumn(1), Column7(2), FullColumn(3), Column7(4), FullColumn(5)),
            Batches = new[] { Batch(1, Ps(Pc("B1_0", "R", 3, 8), Pc("B1_0", "G", 3, 8))) },
        };

        /// <summary>K-25 stage 2: room at (0,7) (after the mover leaves) and at (4,7); the truck block has x = 3 (column full).</summary>
        private static LevelSpec DropColumnsLevel(int[]? dropColumns = null) => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 0, 7)), Column7(0), FullColumn(1), FullColumn(2), FullColumn(3), Column7(4), FullColumn(5)),
            Batches = new[] { Batch(1, Ps(Pc("B1_0", "R", 3, 8)), dropColumns) },
        };

        /// <summary>K-25 "ilk desteğe oturur": ids 0 mover, 1–2 column 5, 3 the hanging B1, 4–11 columns 0, 1, 3, 4, 12 = B1 R, 13 = D2_0 G.</summary>
        private static LevelSpec OVERHANG() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 5, 7)), Column7(5), Ps(Pc("B1_0", "Y", 2, 5)), FullColumn(0), FullColumn(1), FullColumn(3), FullColumn(4)),
            Batches = new[] { Batch(1, Ps(Pc("B1_0", "R", 2, 8), Pc("D2_0", "G", 2, 8))) },
        };

        /// <summary>K-26 / E-04: three `W.` segments; movers A (0,7), B (5,7); batch 1 P1, P2 (B1 R x 0) = 14, 15; batch 2 Q (B1 G x 5) = 16.</summary>
        private static LevelSpec FIFO(bool secondBatch = true) => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("W."), Row("W.")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 0, 7), Pc("B1_0", "W", 5, 7)), Column7(0), Column7(5), FullColumn(1), FullColumn(2), FullColumn(3), FullColumn(4)),
            Batches = secondBatch
                ? new[] { Batch(1, Ps(Pc("B1_0", "R", 0, 8), Pc("B1_0", "R", 0, 8))), Batch(2, Ps(Pc("B1_0", "G", 5, 8))) }
                : new[] { Batch(1, Ps(Pc("B1_0", "R", 0, 8), Pc("B1_0", "R", 0, 8))) },
        };

        /// <summary>K-22: segment 0 is a 2 × 2 W wall, segment 1 one R row. O4 W (0,6) = 0, D2_90 R (2,7) = 1.</summary>
        private static LevelSpec TWO_ROOMS(int? moves = null, P[]? pieces = null) => new LevelSpec
        {
            Moves = moves,
            Wall = new WallSpec { Height = 2 },
            Plans = Segs(Row("WW", "WW"), Row("RR")),
            Pieces = pieces ?? Ps(Pc("O4_0", "W", 0, 6), Pc("D2_90", "R", 2, 7)),
        };

        /// <summary>K-33 / K-39: five W rows; four W dominoes (0–3), a Y single (4), a W single (5) shuttling between (5,0) and (5,1).</summary>
        private static LevelSpec STREAK(int moves = 20) => new LevelSpec
        {
            Moves = moves,
            Wall = new WallSpec { Height = 2 },
            Plan = new[] { "WW", "WW", "WW", "WW", "WW" },
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_0", "W", 2, 0), Pc("D2_0", "W", 3, 0), Pc("B1_0", "Y", 4, 0), Pc("B1_0", "W", 5, 0)),
        };

        /// <summary>K-46 example: wall 5, static gap y 3 (size 1); 0–2 D2_90 W, 3 D2_90 R (4,3), 4 B1 W, 5 B1 Y.</summary>
        private static LevelSpec YAO_LEVEL() => new LevelSpec
        {
            Wall = Wall(5, Gap(3, 1)),
            Plan = new[] { "WW", "RR", "WW", "WW", "WW" },
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 0, 2), Pc("D2_90", "W", 0, 4), Pc("D2_90", "R", 4, 3), Pc("B1_0", "W", 2, 0), Pc("B1_0", "Y", 3, 0)),
        };

        /// <summary>K-25 start row: mover B1 W (0,0); batch 1: B1 R x 1 (1), O4 G x 2 (2), D2_0 Y x 4 (3).</summary>
        private static LevelSpec DROP_START() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Ps(Pc("B1_0", "W", 0, 0)),
            Batches = new[] { Batch(1, Ps(Pc("B1_0", "R", 1, 8), Pc("O4_0", "G", 2, 8), Pc("D2_0", "Y", 4, 8))) },
        };

        /// <summary>K-25 stage 3 for a 2-wide block: batch 1 = two O4 with x = 2 (ids 13, 14).</summary>
        private static LevelSpec WIDE_TIE() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(
                Ps(Pc("B1_0", "W", 2, 7), Pc("I4_0", "Y", 0, 0), Pc("D2_0", "Y", 0, 4), Pc("I4_0", "Y", 1, 0), Pc("D2_0", "Y", 1, 4), Pc("I4_0", "Y", 2, 0), Pc("I3_0", "Y", 2, 4)),
                FullColumn(3),
                Ps(Pc("I4_0", "Y", 4, 0), Pc("D2_0", "Y", 4, 4), Pc("I4_0", "Y", 5, 0), Pc("D2_0", "Y", 5, 4))),
            Batches = new[] { Batch(1, Ps(Pc("O4_0", "R", 2, 8), Pc("O4_0", "G", 2, 8))) },
        };

        /// <summary>K-25 per column: column 1 rows 0–3, column 2 rows 0–5; mover B1 W (5,0) = 0; batch 1: C3_90 R x 1 = 4.</summary>
        private static LevelSpec UNEVEN() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Ps(Pc("B1_0", "W", 5, 0), Pc("I4_0", "Y", 1, 0), Pc("I4_0", "Y", 2, 0), Pc("D2_0", "Y", 2, 4)),
            Batches = new[] { Batch(1, Ps(Pc("C3_90", "R", 1, 8))) },
        };

        /// <summary>K-28 + K-26: mover A B1 W (0,7); batch 1: B1 W x 0 (13, lands on (0,7)) and O4 R x 0 (14, never fits).</summary>
        private static LevelSpec WIN_WITH_TRUCK() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("W.")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 0, 7)), Column7(0), FullColumn(1), FullColumn(2), FullColumn(3), FullColumn(4), FullColumn(5)),
            Batches = new[] { Batch(1, Ps(Pc("B1_0", "W", 0, 8), Pc("O4_0", "R", 0, 8))) },
        };

        /// <summary>K-25 GDD example: mover B1 W (2,0) = 0; batch 1 = [O4 G x 4 (7), B1 R x 0 (8)].</summary>
        private static LevelSpec K25_EXAMPLE() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 2, 0)), FullColumn(0), Ps(Pc("I4_0", "Y", 1, 0), Pc("D2_0", "Y", 1, 4), Pc("O4_0", "Y", 4, 0), Pc("O4_0", "Y", 4, 2))),
            Batches = new[] { Batch(1, Ps(Pc("O4_0", "G", 4, 8), Pc("B1_0", "R", 0, 8))) },
        };

        /// <summary>K-26 GDD example: only (5,7) frees up; queue [O4 W (13), B1 Y x 1 (14)].</summary>
        private static LevelSpec K26_EXAMPLE() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 5, 7)), FullColumn(0), FullColumn(1), FullColumn(2), FullColumn(3), FullColumn(4), Column7(5)),
            Batches = new[] { Batch(1, Ps(Pc("O4_0", "W", 0, 8), Pc("B1_0", "Y", 1, 8))) },
        };

        /// <summary>E-34: x = 2 and dropColumns [3, 0] are full; free after the move: (1,7), (4,7), (5,7); batch 1 = four B1 R x 2 (13–16).</summary>
        private static LevelSpec E34_LEVEL() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 5, 7)), Column7(5), FullColumn(0), Column7(1), FullColumn(2), FullColumn(3), Column7(4)),
            Batches = new[] { Batch(1, Ps(Pc("B1_0", "R", 2, 8), Pc("B1_0", "R", 2, 8), Pc("B1_0", "R", 2, 8), Pc("B1_0", "R", 2, 8)), new[] { 3, 0 }) },
        };

        /// <summary>E-03: the truck O4 R (x 0, id 14) waits; mover B1 W (5,7) = 0; B1 Y (1,6) = 13.</summary>
        private static LevelSpec E03_LEVEL() => new LevelSpec
        {
            Plans = Segs(Row("W."), Row("WW")),
            Pieces = Cat(Ps(Pc("B1_0", "W", 5, 7)), Column7(5), FullColumn(2), FullColumn(3), FullColumn(4),
                Ps(Pc("I4_0", "Y", 0, 0), Pc("D2_0", "Y", 0, 4), Pc("I4_0", "Y", 1, 0), Pc("D2_0", "Y", 1, 4), Pc("B1_0", "Y", 1, 6))),
            Batches = new[] { Batch(1, Ps(Pc("O4_0", "R", 0, 8))) },
        };

        /// <summary>
        /// The level 5 canonical solution (LEVELS §2 Bölüm 5): d (yard), a, b, c, d — Sol Oda complete, shift, truck — k1_1,
        /// k1_2 (yard), k1_0, k1_3, k1_1, k1_2. Ids: 0 a, 1 b, 2 c, 3 d; truck 4 = k1_0, 5 = k1_1, 6 = k1_2, 7 = k1_3.
        /// </summary>
        private static readonly DragMove[] LEVEL5_HAND =
        {
            Drag(3, N(2, 2)), Drag(0, N(4, 5)), Drag(1, N(4, 5)), Drag(2, N(5, 5)), Drag(3, N(4, 5)), Drag(5, N(2, 1)),
            Drag(6, N(3, 1)), Drag(4, N(4, 5)), Drag(7, N(4, 5)), Drag(5, N(4, 5)), Drag(6, N(5, 5)),
        };

        private static readonly int[] LEVEL5_TRUCK = { 4, 5, 6, 7 };

        /// <summary>
        /// A stand-in for the Phase 3 truck help (K-30, E-37 / E-42): at step 12 it draws from the state's RNG and moves the
        /// STREAK yard single B1 W (id 5, alone in column 5) to row 1 + rng(6). Deterministic only if the RNG lives in the state.
        /// </summary>
        private static MoveHooks RngHelp(List<int>? calls = null) => new MoveHooks(deadlock: ctx =>
        {
            const int id = 5;
            if (PieceZone(ctx.S, id) != Zone.Yard || PieceX(ctx.S, id) != 5) return;
            int fromY = PieceY(ctx.S, id);
            int toY = 1 + ctx.Rng.NextInt(6);
            calls?.Add(toY);
            Placement.MovePiece(ctx.S, id, Placement.YardPlace(new Anchor(5, toY)));
            ctx.Emit(new TruckHelpEvent("reshuffle", new[] { new PieceShift(id, At.Yard(5, fromY), At.Yard(5, toY)) }));
        });

        // =====================================================================================================================
        // K-25 / K-26 truck delivery and FIFO queue
        // =====================================================================================================================

        [Test(Description = "K-25 stage 3: x full → nearest column, tie to the wall side (4 before 2); the 2nd block of the same step sees the 1st")]
        public void K25_stage_3_x_full_nearest_column_tie_to_the_wall_side_the_2nd_block_of_the_step_sees_the_1st()
        {
            var s = Init(TIE());
            var m = Play(s, Drag(0, N(6, 8)));
            Assert.AreEqual(1, Find<SiteShiftedEvent>(m.Ev).ToSeg);
            CollectionAssert.AreEqual(new[] { 13, 14 }, Find<DeliveryArrivedEvent>(m.Ev).Pieces);
            CollectionAssert.AreEqual(new[] { 4, 7, Zone.Yard }, AtOf(s, 13));
            CollectionAssert.AreEqual(new[] { 2, 7, Zone.Yard }, AtOf(s, 14));
            CollectionAssert.IsEmpty(QueueIds(s));
            CollectionAssert.AreEqual(new[] { "13@9→yard:4,7", "14@9→yard:2,7" }, DeliveryFalls(m.Ev).Select(e => e.PieceId + "@" + e.Step + "→" + e.To.Zone + ":" + e.To.X + "," + e.To.Y).ToArray());
            ExpectConsistent(s);
        }

        [Test(Description = "K-25 stage 2: dropColumns are tried before the nearest columns of stage 3 (column 0 beats the nearer column 4)")]
        public void K25_stage_2_dropColumns_are_tried_before_the_nearest_columns_of_stage_3()
        {
            var plain = Init(DropColumnsLevel());
            Play(plain, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 4, 7, Zone.Yard }, AtOf(plain, 13));
            var listed = Init(DropColumnsLevel(new[] { 0 }));
            Play(listed, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 0, 7, Zone.Yard }, AtOf(listed, 13));
        }

        [Test(Description = "K-25 a truck block falls from y = 10 − h onto its FIRST support: it stops on a hanging block, never in the hollow under it")]
        public void K25_a_truck_block_falls_onto_its_first_support_never_into_the_hollow_under_a_hanging_block()
        {
            var s = Init(OVERHANG());
            var m = Play(s, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 2, 6, Zone.Yard }, AtOf(s, 12));
            Assert.AreEqual(Zone.Queue, PieceZone(s, 13));
            CollectionAssert.AreEqual(new[] { 13 }, QueueIds(s));
            var q = Find<DeliveryQueuedEvent>(m.Ev);
            CollectionAssert.AreEqual(new[] { 9, 1 }, new[] { q.Step, q.Queued });
            CollectionAssert.AreEqual(new[] { 12 }, Find<DeliveryArrivedEvent>(m.Ev).Pieces);
            ExpectConsistent(s);
        }

        [Test(Description = "K-25 the truck drop moves no other block and triggers no neighbour effect (onNeighborMoved never sees a delivered block)")]
        public void K25_the_truck_drop_moves_no_other_block_and_triggers_no_neighbour_effect()
        {
            var s = Init(TIE());
            var seen = new HashSet<int>();
            var hooks = new MoveHooks(onNeighborMoved: (ctx, entity, moved) =>
            {
                seen.Add(moved);
                return NeighborEffects.None;
            });
            var ids = Enumerable.Range(1, 12).ToArray();
            var before = ids.Select(id => string.Join(",", AtOf(s, id))).ToArray();
            Play(s, Drag(0, N(6, 8)), hooks);
            CollectionAssert.AreEquivalent(new[] { 0 }, seen);
            CollectionAssert.AreEqual(before, ids.Select(id => string.Join(",", AtOf(s, id))).ToArray());
        }

        [Test(Description = "K-26 E-04 a block left in the truck keeps its place: it is tried before the next batch and takes the only free cell")]
        public void K26_E04_a_block_left_in_the_truck_keeps_its_place_and_is_tried_before_the_next_batch()
        {
            var s = Init(FIFO());
            var m1 = Play(s, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 14 }, Find<DeliveryArrivedEvent>(m1.Ev).Pieces);
            CollectionAssert.AreEqual(new[] { 0, 7, Zone.Yard }, AtOf(s, 14));
            CollectionAssert.AreEqual(new[] { 15 }, QueueIds(s));
            Assert.AreEqual(1, Find<DeliveryQueuedEvent>(m1.Ev).Queued);
            // K-26: queued (and not yet delivered) truck blocks cannot be picked
            foreach (int id in new[] { 15, 16 })
            {
                var a = Movement.TryBeginDrag(s, id);
                Assert.IsFalse(a.Ok);
                Assert.AreEqual(PickFailures.NotOnBoard, a.Reason);
            }
            var m2 = Play(s, Drag(1, N(6, 8)));
            Assert.AreEqual(2, Find<SiteShiftedEvent>(m2.Ev).ToSeg);
            // P2 (old) is tried first and drops into (5,7), freed by this move; Q (new batch) finds no room and waits
            CollectionAssert.AreEqual(new[] { 15 }, Find<DeliveryArrivedEvent>(m2.Ev).Pieces);
            CollectionAssert.AreEqual(new[] { 5, 7, Zone.Yard }, AtOf(s, 15));
            CollectionAssert.AreEqual(new[] { 16 }, QueueIds(s));
            Assert.AreEqual(Zone.Queue, PieceZone(s, 16));
            ExpectConsistent(s);
        }

        // =====================================================================================================================
        // K-28 / K-29 / K-35 step order
        // =====================================================================================================================

        [Test(Description = "K-35 a segment-completing move emits its events at the GDD step numbers (1 · 2 · 3 · 4 · 8 · 9)")]
        public void K35_a_segment_completing_move_emits_its_events_at_the_GDD_step_numbers()
        {
            var s = LevelState(5);
            foreach (var m in LEVEL5_HAND.Take(4)) Play(s, m);
            var m5 = Play(s, LEVEL5_HAND[4]);
            // the 4th correct drag in a row also earns a Golden Trowel in step 3 (K-33)
            CollectionAssert.AreEqual(new[]
            {
                "pieceMoved@1", "pieceFell@2", "placementCorrect@3", "comboChanged@3", "trowelEarned@3", "comboChanged@3", "movesChanged@4",
                "segmentCompleted@8", "goalProgress@8", "siteShifted@8", "deliveryArrived@9", "pieceFell@9", "pieceFell@9", "pieceFell@9", "pieceFell@9",
            }, Steps(m5.Ev));
            CollectionAssert.AreEqual(Enumerable.Range(0, m5.Ev.Count).ToArray(), m5.Ev.Select(e => e.Seq).ToArray());
        }

        /// <summary>GDD K-12 example: gap y = 3, size 1; plan row 3 = RR above three `.` rows. 0 = D2_90 R (4,3), 1 = D2_0 R (3,3).</summary>
        private static LevelSpec GapOverVoids(string top = "RR") => new LevelSpec
        {
            Wall = Wall(5, Gap(3, 1)),
            Plan = new[] { top, "..", "..", ".." },
            Pieces = Ps(Pc("D2_90", "R", 4, 3), Pc("D2_0", "R", 3, 3), Pc("D2_90", "R", 0, 0)),
        };

        [Test(Description = "K-35 step 0: a cancelled release (straddling the gap, E-06) changes no byte and emits only moveCancelled")]
        public void K35_E06_step_0_a_cancelled_release_straddling_the_gap_changes_no_byte_and_emits_only_moveCancelled()
        {
            var s = Init(GapOverVoids());
            var before = Snapshot(s);
            var m = Play(s, Drag(0, R(0, 5, 3)));
            AssertResult(m.Res, MoveStatuses.Cancelled, "straddle", false, false);
            CollectionAssert.AreEqual(new[] { "{\"pieceId\":0,\"reason\":\"straddle\",\"seq\":0,\"step\":0,\"t\":\"moveCancelled\"}" }, EvJsons(m.Ev));
            CollectionAssert.AreEqual(before, s.Buf);
        }

        [Test(Description = "K-28 E-01 in a session: a win on the very last move opens no +5 window (win is checked before out of moves)")]
        public void K28_E01_a_win_on_the_very_last_move_opens_no_plus_5_window()
        {
            var session = GameSession.Start(Lvl(new LevelSpec { Moves = 1, Plan = new[] { "WW" }, Pieces = Ps(Pc("D2_90", "W", 0, 0)) }));
            var res = session.Commit(Drag(0, N(6, 8)));
            AssertResult(res, MoveStatuses.Applied, null, true, false);
            CollectionAssert.AreEqual(new[] { "won", "0", "null" }, new[] { session.Outcome, session.MovesLeft.ToString(), OfferText(session.NextOffer()) });
            Assert.IsTrue(Moves.IsLevelWon(session.State));
        }

        [Test(Description = "K-29 the out-of-moves window offers n = 1 with the ad option; accepting gives exactly 5 moves and play goes on")]
        public void K29_the_out_of_moves_window_offers_n_1_with_the_ad_option_accepting_gives_exactly_5_moves()
        {
            var session = GameSession.Start(Lvl(STREAK(2)));
            session.Commit(Drag(5, N(5, 1)));
            var last = session.Commit(Drag(5, N(5, 0)));
            Assert.IsTrue(last.OutOfMoves);
            CollectionAssert.AreEqual(new[] { "outOfMoves", "1a" }, new[] { session.Outcome, OfferText(session.NextOffer()) });
            Assert.AreEqual("notPlaying", session.Commit(Drag(0, N(6, 8))).Reason);
            Assert.AreEqual(MoveStatuses.Applied, session.AcceptOffer(AddMovesSource.OfferCoins).Status);
            CollectionAssert.AreEqual(new object[] { "playing", 5, 2 }, new object[] { session.Outcome, session.MovesLeft, session.MovesMade });
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(Drag(0, N(6, 8))).Status);
            Assert.AreEqual(4, session.MovesLeft);
        }

        // =====================================================================================================================
        // K-39 Undo
        // =====================================================================================================================

        [Test(Description = "K-39 Undo of the 4th correct placement takes the earned Golden Trowel back and restores c = 3 and the YAO counters")]
        public void K39_Undo_of_the_4th_correct_placement_takes_the_earned_trowel_back_and_restores_c_3_and_YAO()
        {
            var session = GameSession.Start(Lvl(STREAK()));
            var s = session.State;
            session.Commit(Drag(0, N(6, 8)));
            session.Commit(Drag(1, N(7, 8)));
            session.Commit(Drag(2, N(6, 8)));
            var before = Snapshot(s);
            session.Commit(Drag(3, N(7, 8)));
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, new[] { Hdr(s, H.Combo), Hdr(s, H.Trowels), session.Yao().OverWall });
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(before, s.Buf);
            CollectionAssert.AreEqual(new[] { 3, 0, 17, 3 }, new[] { Hdr(s, H.Combo), Hdr(s, H.Trowels), session.MovesLeft, session.MovesMade });
            CollectionAssert.AreEqual(new[] { 3, 0, Zone.Yard }, AtOf(s, 3));
            Assert.AreEqual("3,0,1", YaoText(session.Yao()));
            Assert.AreEqual(UndoBlocks.NoDragMove, session.UndoBlock());
        }

        [Test(Description = "K-39 a cancelled drag or a refused trowel is not an action: Undo still takes back the last committed drag")]
        public void K39_a_cancelled_drag_or_a_refused_trowel_is_not_an_action_Undo_still_takes_back_the_last_drag()
        {
            var session = GameSession.Start(Lvl(STREAK()), new[] { PreBoosters.TrowelStart });
            var s = session.State;
            var before = Snapshot(s);
            session.Commit(Drag(0, N(6, 8)));
            int logLength = session.Log.Count;
            Assert.AreEqual(MoveStatuses.Cancelled, session.Commit(Drag(1, N(1, 0))).Status); // K-07 row 1: back on its start cells
            Assert.AreEqual(MoveStatuses.Rejected, session.Commit(Trowel(1, 1)).Status);
            Assert.AreEqual(logLength, session.Log.Count);
            Assert.IsTrue(session.CanUndo());
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(before, s.Buf);
        }

        [Test(Description = "K-39 Undo of a rail placement restores the rail counter (K-46) and the block in the yard")]
        public void K39_K46_Undo_of_a_rail_placement_restores_the_rail_counter_and_the_block_in_the_yard()
        {
            var session = GameSession.Start(Lvl(YAO_LEVEL()));
            session.Commit(Drag(0, N(6, 8)));
            session.Commit(Drag(1, N(6, 8)));
            session.Commit(Drag(2, N(6, 8)));
            session.Commit(Drag(3, R(0, 6, 3)));
            Assert.AreEqual("3,1,0.75", YaoText(session.Yao()));
            session.Undo();
            Assert.AreEqual("3,0,1", YaoText(session.Yao()));
            CollectionAssert.AreEqual(new[] { 4, 3, Zone.Yard }, AtOf(session.State, 3));
        }

        // =====================================================================================================================
        // K-43 exit and resume
        // =====================================================================================================================

        [Test(Description = "K-43 a Golden Trowel use does not raise m: an exit after it is still free (m = 0)")]
        public void K43_a_Golden_Trowel_use_does_not_raise_m_an_exit_after_it_is_still_free()
        {
            var session = GameSession.Start(Lvl(STREAK()), new[] { PreBoosters.TrowelStart });
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(GoldTrowel(0, 6, 0)).Status);
            Assert.AreEqual(0, session.MovesMade);
            var x = session.Exit();
            CollectionAssert.AreEqual(new object[] { "free", 0, false }, new object[] { x.Kind, x.MovesMade, x.StreakBonusConsumed });
        }

        [Test(Description = "K-43 K-39 Undo of the only move takes m back to 0, so the confirmed exit is free again")]
        public void K43_K39_Undo_of_the_only_move_takes_m_back_to_0_so_the_exit_is_free_again()
        {
            var session = GameSession.Start(Lvl(STREAK()), new[] { PreBoosters.Thermos });
            session.Commit(Drag(0, N(6, 8)));
            Assert.AreEqual(1, session.MovesMade);
            session.Undo();
            Assert.AreEqual(0, session.MovesMade);
            Assert.AreEqual("free|0|0|thermos|False", ExitText(session.Exit()));
        }

        [Test(Description = "K-43 E-38 a mixed log (pre-level boosters, wrong placement, Undo, trowel, +5 offer) replays bit for bit and plays on identically")]
        public void K43_E38_a_mixed_log_replays_bit_for_bit_and_plays_on_identically()
        {
            var lvl = Lvl(STREAK(6));
            var live = GameSession.Start(lvl, new[] { PreBoosters.TrowelStart });
            live.Commit(Drag(0, N(6, 8))); // correct, 5 left
            live.Commit(Drag(4, N(7, 8))); // Y on W: wrong, bounces, 4 left
            live.Undo(); // 5 left
            live.Commit(Drag(1, N(7, 8))); // correct, 4 left
            live.Commit(Trowel(0, 2)); // (6,2): the legacy cell trowel is refused, not logged
            live.Commit(Drag(2, N(7, 8))); // correct on column 7, 3 left
            foreach (int y in new[] { 1, 0, 1 }) live.Commit(Drag(5, N(5, y))); // yard shuttle → 0 left
            Assert.AreEqual("outOfMoves", live.Outcome);
            live.AcceptOffer(AddMovesSource.OfferAd);
            live.Commit(Drag(3, N(6, 8)));
            Assert.AreEqual("playing", live.Outcome);

            var resumed = GameSession.Replay(lvl, live.Log);
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
            Assert.IsTrue(SessionActionJson.SameLog(live.Log, resumed.Log));
            CollectionAssert.AreEqual(
                new object[] { live.OffersUsed, live.AdOfferUsed, live.CanUndo(), live.MovesMade, YaoText(live.Yao()) },
                new object[] { resumed.OffersUsed, resumed.AdOfferUsed, resumed.CanUndo(), resumed.MovesMade, YaoText(resumed.Yao()) });
            Assert.AreEqual(1, resumed.OffersUsed);
            // the next move produces the same event log on both
            var a = new ArraySink();
            var b = new ArraySink();
            live.Commit(Drag(5, N(5, 0)), a);
            resumed.Commit(Drag(5, N(5, 0)), b);
            Assert.AreEqual(Moves.EventLogHash(a.Events), Moves.EventLogHash(b.Events));
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
        }

        // =====================================================================================================================
        // Round 2 · K-25 / K-26 truck delivery geometry and the queue counter
        // =====================================================================================================================

        [Test(Description = "K-25 a truck block is dropped from y = 10 − h (B1 from row 9, O4 / D2_0 from row 8) onto its first support; deliveryArrived names the new segment")]
        public void K25_a_truck_block_is_dropped_from_y_10_minus_h_onto_its_first_support_deliveryArrived_names_the_new_segment()
        {
            var s = Init(DROP_START());
            var m = Play(s, Drag(0, N(6, 8)));
            var arrived = Find<DeliveryArrivedEvent>(m.Ev);
            CollectionAssert.AreEqual(new[] { 9, 1 }, new[] { arrived.Step, arrived.Seg });
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, arrived.Pieces);
            CollectionAssert.AreEqual(new[] { "1:1,9→yard:1,0:9@9", "2:2,8→yard:2,0:8@9", "3:4,8→yard:4,0:8@9" }, DeliveryFalls(m.Ev).Select(FallText).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 0, Zone.Yard, 2, 0, Zone.Yard, 4, 0, Zone.Yard }, AtOf(s, 1).Concat(AtOf(s, 2)).Concat(AtOf(s, 3)).ToArray());
            ExpectConsistent(s);
        }

        [Test(Description = "K-25 stage 3 for a 2-wide block: anchors 0 and 4 are both 2 away from x = 2 → the wall side (4) wins; the 2nd O4 then takes anchor 0")]
        public void K25_stage_3_for_a_2_wide_block_the_wall_side_anchor_wins_the_tie_the_2nd_O4_takes_anchor_0()
        {
            var s = Init(WIDE_TIE());
            var m = Play(s, Drag(0, N(6, 8)));
            Assert.AreEqual(1, Find<SiteShiftedEvent>(m.Ev).ToSeg);
            CollectionAssert.AreEqual(new[] { 4, 6, Zone.Yard }, AtOf(s, 13));
            CollectionAssert.AreEqual(new[] { 0, 6, Zone.Yard }, AtOf(s, 14));
            CollectionAssert.IsEmpty(QueueIds(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-25 \"ilk desteğe oturur\" is per column: a C3_90 (cells (0,0)(0,1)(1,1)) rests on column 2 (top 5) over a hollow (1,4) in column 1")]
        public void K25_first_support_is_per_column_a_C3_90_rests_on_column_2_over_a_hollow_in_column_1()
        {
            var s = Init(UNEVEN());
            var m = Play(s, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 1, 5, Zone.Yard }, AtOf(s, 4));
            Assert.IsTrue(Has<PieceFellEvent>(m.Ev));
            CollectionAssert.AreEqual(new[] { "4:1,8→yard:1,5:3@9" }, DeliveryFalls(m.Ev).Select(FallText).ToArray());
            // the hollow under the overhang stays empty: the truck drop never slides a block sideways into a gap
            CollectionAssert.AreEqual(new[] { 0, 5, 5 }, new[] { YardOcc(s, 1, 4), YardOcc(s, 1, 5), YardOcc(s, 2, 6) });
            ExpectConsistent(s);
        }

        [Test(Description = "K-26 \"Kamyonda: N\": deliveryQueued reports N when it changes, and N = 0 when the last queued block lands (chip hidden)")]
        public void K26_Kamyonda_N_deliveryQueued_reports_N_when_it_changes_and_0_when_the_last_queued_block_lands()
        {
            var s = Init(FIFO(secondBatch: false));
            var m1 = Play(s, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { "9:1" }, m1.Ev.OfType<DeliveryQueuedEvent>().Select(e => e.Step + ":" + e.Queued).ToArray());
            var m2 = Play(s, Drag(1, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 15 }, Find<DeliveryArrivedEvent>(m2.Ev).Pieces);
            CollectionAssert.AreEqual(new[] { "9:0" }, m2.Ev.OfType<DeliveryQueuedEvent>().Select(e => e.Step + ":" + e.Queued).ToArray());
            CollectionAssert.IsEmpty(QueueIds(s));
        }

        [Test(Description = "K-28 K-48 (3) the win waits for the truck: the last segment completes while an O4 still has no room → no win, the O4 stays queued")]
        public void K28_K48_the_win_waits_for_the_truck_the_last_segment_completes_while_an_O4_still_has_no_room()
        {
            var s = Init(WIN_WITH_TRUCK());
            Play(s, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 0, 7, Zone.Yard }, AtOf(s, 13));
            CollectionAssert.AreEqual(new[] { 14 }, QueueIds(s));
            var last = Play(s, Drag(13, N(6, 8)));
            Assert.AreEqual(1, Find<SegmentCompletedEvent>(last.Ev).Seg);
            Assert.IsFalse(Has<SiteShiftedEvent>(last.Ev));
            Assert.IsFalse(Has<DeliveryArrivedEvent>(last.Ev)); // (0,7) is free again, but an O4 needs 2 × 2
            // Faz 2R K-48 (3): a material block still in the truck queue holds back the win
            Assert.IsFalse(last.Res.Won);
            Assert.IsFalse(Has<LevelWonEvent>(last.Ev));
            CollectionAssert.AreEqual(new[] { 14 }, QueueIds(s));
        }

        // =====================================================================================================================
        // Round 2 · K-39 Undo corner cases
        // =====================================================================================================================

        [Test(Description = "E-21 K-26 Undo of a shift + delivery puts the old truck block back at the head of the queue and the new batch off the truck; redoing the move replays the same events")]
        public void E21_K26_K39_Undo_of_a_shift_and_delivery_puts_the_old_truck_block_back_at_the_head_of_the_queue()
        {
            var session = GameSession.Start(Lvl(FIFO()));
            var s = session.State;
            session.Commit(Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 15 }, QueueIds(s));
            int pendingZone = PieceZone(s, 16);
            var before = Snapshot(s);
            var first2 = new ArraySink();
            session.Commit(Drag(1, N(6, 8)), first2);
            CollectionAssert.AreEqual(new[] { 16 }, QueueIds(s));
            CollectionAssert.AreEqual(new[] { 5, 7, Zone.Yard }, AtOf(s, 15));
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(before, s.Buf);
            CollectionAssert.AreEqual(new[] { 15 }, QueueIds(s));
            CollectionAssert.AreEqual(new[] { Zone.Queue, pendingZone }, new[] { PieceZone(s, 15), PieceZone(s, 16) });
            CollectionAssert.AreEqual(new[] { 1, 1 }, new[] { Hdr(s, H.ActiveSeg), Goals.GoalViews(s)[0].Value });
            CollectionAssert.AreEqual(new[] { 5, 7, Zone.Yard }, AtOf(s, 1));
            var again = new ArraySink();
            session.Commit(Drag(1, N(6, 8)), again);
            Assert.AreEqual(Moves.EventLogHash(first2.Events), Moves.EventLogHash(again.Events));
        }

        [Test(Description = "K-39 after an accepted +5 offer the next drag can be undone: back to the post-offer state (5 moves, offer kept, no window)")]
        public void K39_K29_after_an_accepted_plus_5_offer_the_next_drag_can_be_undone_back_to_the_post_offer_state()
        {
            var session = GameSession.Start(Lvl(STREAK(2)));
            session.Commit(Drag(5, N(5, 1)));
            session.Commit(Drag(5, N(5, 0)));
            Assert.AreEqual("outOfMoves", session.Outcome);
            session.AcceptOffer(AddMovesSource.OfferCoins);
            Assert.AreEqual(UndoBlocks.NoDragMove, session.UndoBlock());
            var afterOffer = Snapshot(session.State);
            session.Commit(Drag(0, N(6, 8)));
            Assert.IsTrue(session.CanUndo());
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(afterOffer, session.State.Buf);
            CollectionAssert.AreEqual(new object[] { 5, 2, 1, "playing" }, new object[] { session.MovesLeft, session.MovesMade, session.OffersUsed, session.Outcome });
            CollectionAssert.AreEqual(new[] { "null", "False" }, new[] { OfferText(session.NextOffer()), session.CanUndo().ToString() });
        }

        [Test(Description = "K-39 E-37 a step-12 truck help is undone with its move (RNG state included), so the same move afterwards brings the same help")]
        public void K39_E37_a_step_12_truck_help_is_undone_with_its_move_RNG_state_included()
        {
            var calls = new List<int>();
            var session = GameSession.Start(Lvl(STREAK()), opts: new SessionOptions(hooks: RngHelp(calls)));
            var s = session.State;
            var before = Snapshot(s);
            int rngBefore = Hdr(s, H.Rng);
            var a = new ArraySink();
            session.Commit(Drag(0, N(6, 8)), a);
            Assert.AreEqual(12, Find<TruckHelpEvent>(a.Events).Step);
            int helped = PieceY(s, 5);
            Assert.AreEqual(calls[0], helped);
            Assert.AreNotEqual(rngBefore, Hdr(s, H.Rng));
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(before, s.Buf);
            CollectionAssert.AreEqual(new[] { 5, 0, Zone.Yard }, AtOf(s, 5));
            var b = new ArraySink();
            session.Commit(Drag(0, N(6, 8)), b);
            Assert.AreEqual(helped, PieceY(s, 5));
            Assert.AreEqual(Moves.EventLogHash(a.Events), Moves.EventLogHash(b.Events));
        }

        // =====================================================================================================================
        // Round 2 · K-43 resume and exit corner cases
        // =====================================================================================================================

        [Test(Description = "K-43 K-39 a trailing Undo survives the restart: the resumed attempt has the same state and no Undo left (depth 1)")]
        public void K43_K39_a_trailing_Undo_survives_the_restart_with_no_Undo_left()
        {
            var lvl = Lvl(STREAK());
            var live = GameSession.Start(lvl);
            live.Commit(Drag(0, N(6, 8)));
            live.Commit(Drag(1, N(7, 8)));
            live.Undo();
            var resumed = GameSession.Replay(lvl, live.Log);
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
            CollectionAssert.AreEqual(new object[] { 1, UndoBlocks.NoDragMove }, new object?[] { resumed.MovesMade, resumed.UndoBlock() });
            Assert.IsFalse(resumed.Undo());
        }

        [Test(Description = "K-43 K-29 an out-of-moves window after an ad offer comes back as offer 2 without the ad option")]
        public void K43_K29_an_out_of_moves_window_after_an_ad_offer_comes_back_as_offer_2_without_the_ad_option()
        {
            var lvl = Lvl(STREAK(1));
            var live = GameSession.Start(lvl);
            live.Commit(Drag(5, N(5, 1)));
            Assert.AreEqual("1a", OfferText(live.NextOffer()));
            live.AcceptOffer(AddMovesSource.OfferAd);
            foreach (int y in new[] { 0, 1, 0, 1, 0 }) live.Commit(Drag(5, N(5, y)));
            CollectionAssert.AreEqual(new[] { "outOfMoves", "2" }, new[] { live.Outcome, OfferText(live.NextOffer()) });
            var resumed = GameSession.Replay(lvl, live.Log);
            CollectionAssert.AreEqual(new object[] { "outOfMoves", "2", 1, true }, new object[] { resumed.Outcome, OfferText(resumed.NextOffer()), resumed.OffersUsed, resumed.AdOfferUsed });
            var before = Snapshot(resumed.State);
            Assert.AreEqual("adNotAllowed", resumed.AcceptOffer(AddMovesSource.OfferAd).Reason);
            CollectionAssert.AreEqual(before, resumed.State.Buf);
            Assert.AreEqual(MoveStatuses.Applied, resumed.AcceptOffer(AddMovesSource.OfferCoins).Status);
            CollectionAssert.AreEqual(new[] { 5, 2 }, new[] { resumed.MovesLeft, resumed.OffersUsed });
        }

        [Test(Description = "K-43 a tampered log does not replay: an offer with no window, an Undo after a trowel, a cancelled drag (never logged), a Termos inside the log")]
        public void K43_a_tampered_log_does_not_replay_offer_without_window_undo_after_trowel_cancelled_drag_termos_inside()
        {
            var lvl = Lvl(STREAK());
            StartAction Start(params string[] pre) => new StartAction(pre, 0);
            var cases = new (SessionAction[] Log, int Index)[]
            {
                (new SessionAction[] { Start(), new AddMovesMove(5, AddMovesSource.OfferCoins) }, 1),
                (new SessionAction[] { Start(PreBoosters.TrowelStart), Drag(0, N(6, 8)), GoldTrowel(1, 7, 0), new UndoAction() }, 3),
                (new SessionAction[] { Start(), Drag(0, N(0, 0)) }, 1),
                (new SessionAction[] { Start(), new AddMovesMove(3, AddMovesSource.Thermos) }, 1),
            };
            foreach (var (log, index) in cases)
            {
                var e = Assert.Throws<ReplayError>(() => GameSession.Replay(lvl, log));
                Assert.AreEqual(index, e!.Index);
            }
        }

        [Test(Description = "K-43 E-42 a step-12 help that draws random numbers (after moves and after an accepted offer) replays bit for bit")]
        public void K43_E42_a_step_12_help_that_draws_random_numbers_replays_bit_for_bit()
        {
            var lvl = Lvl(STREAK(2));
            var liveCalls = new List<int>();
            var live = GameSession.Start(lvl, opts: new SessionOptions(hooks: RngHelp(liveCalls)));
            live.Commit(Drag(0, N(6, 8))); // step 12 help #1
            live.Commit(Drag(1, N(7, 8))); // 0 moves left: step 12 runs before the window (Faz 2R) → help #2
            Assert.AreEqual("outOfMoves", live.Outcome);
            var sink = new ArraySink();
            live.AcceptOffer(AddMovesSource.OfferCoins, sink); // step 12 once (E-42) → help #3
            CollectionAssert.AreEqual(new[] { "movesChanged@1", "truckHelp@12" }, Steps(sink.Events));
            Assert.AreEqual(3, liveCalls.Count);
            var replayCalls = new List<int>();
            var resumed = GameSession.Replay(lvl, live.Log, new SessionOptions(hooks: RngHelp(replayCalls)));
            CollectionAssert.AreEqual(liveCalls, replayCalls);
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
            CollectionAssert.AreEqual(new object[] { "playing", 5, 2 }, new object[] { resumed.Outcome, resumed.MovesLeft, resumed.MovesMade });
        }

        [Test(Description = "K-43 resume right after a shift + delivery (level 5, move 5): same truck blocks and queue, and move 6 yields the same events")]
        public void K43_resume_right_after_a_shift_and_delivery_level_5_move_5_same_truck_blocks_and_events()
        {
            var lvl = LevelFile(5);
            var live = GameSession.Start(lvl);
            foreach (var m in LEVEL5_HAND.Take(5)) Assert.AreEqual(MoveStatuses.Applied, live.Commit(m).Status);
            var resumed = GameSession.Replay(lvl, live.Log);
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
            CollectionAssert.IsEmpty(QueueIds(resumed.State));
            CollectionAssert.AreEqual(LEVEL5_TRUCK.Select(_ => Zone.Yard).ToArray(), LEVEL5_TRUCK.Select(id => PieceZone(resumed.State, id)).ToArray());
            Assert.AreEqual(live.CanUndo(), resumed.CanUndo());
            var a = new ArraySink();
            var b = new ArraySink();
            live.Commit(LEVEL5_HAND[5], a);
            resumed.Commit(LEVEL5_HAND[5], b);
            Assert.AreEqual(Moves.EventLogHash(a.Events), Moves.EventLogHash(b.Events));
        }

        [Test(Description = "K-43 K-40 the Mala Başlangıcı trowel used at m = 0: the exit is still free and the pre-level booster is refunded")]
        public void K43_K40_the_trowel_start_trowel_used_at_m_0_the_exit_is_still_free_and_refunded()
        {
            var session = GameSession.Start(Lvl(STREAK()), new[] { PreBoosters.TrowelStart });
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(GoldTrowel(0, 6, 0)).Status);
            Assert.AreEqual(MoveStatuses.Cancelled, session.Commit(Drag(1, N(1, 0))).Status); // K-07 row 1: not a move
            Assert.AreEqual("free|0|0|trowelStart|False", ExitText(session.Exit()));
        }

        [Test(Description = "K-43 E-09 a log that ends with a winning Golden Trowel replays to the won outcome (mini pipeline step 11)")]
        public void K43_E09_a_log_that_ends_with_a_winning_Golden_Trowel_replays_to_the_won_outcome()
        {
            var lvl = Lvl(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0)), Moves = 7 });
            var live = GameSession.Start(lvl, new[] { PreBoosters.TrowelStart });
            live.Commit(Drag(0, N(6, 8)));
            Assert.IsTrue(live.Commit(GoldTrowel(1, 7, 0)).Won);
            Assert.AreEqual("won", live.Outcome);
            var resumed = GameSession.Replay(lvl, live.Log);
            CollectionAssert.AreEqual(new object[] { "won", true, 6, 1 }, new object[] { resumed.Outcome, Moves.IsLevelWon(resumed.State), resumed.MovesLeft, resumed.MovesMade });
            CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
            Assert.AreEqual(UndoBlocks.LevelOver, resumed.UndoBlock());
        }

        [Test(Description = "K-43 two moves and an Undo of the second leave m = 1: the confirmed exit is a loss")]
        public void K43_two_moves_and_an_Undo_of_the_second_leave_m_1_the_confirmed_exit_is_a_loss()
        {
            var session = GameSession.Start(Lvl(STREAK()));
            session.Commit(Drag(0, N(6, 8)));
            session.Commit(Drag(1, N(7, 8)));
            session.Undo();
            Assert.AreEqual(1, session.MovesMade);
            Assert.AreEqual("loss|1|1||True", ExitText(session.Exit()));
            Assert.AreEqual("lost", session.Outcome);
        }

        // =====================================================================================================================
        // Round 3 · K-25 / K-26 / E-03 / E-34 truck delivery worked examples
        // =====================================================================================================================

        [Test(Description = "K-25 GDD example: O4 G x = 4 lands on (4,4) over columns 4–5 (top y = 3); B1 R x = 0 (column full) tries x = 1 first and lands on (1,6)")]
        public void K25_GDD_example_O4_G_lands_on_4_4_B1_R_with_a_full_column_tries_x_1_first_and_lands_on_1_6()
        {
            var s = Init(K25_EXAMPLE());
            var m = Play(s, Drag(0, N(6, 8)));
            var arrived = Find<DeliveryArrivedEvent>(m.Ev);
            CollectionAssert.AreEqual(new[] { 9, 1 }, new[] { arrived.Step, arrived.Seg });
            CollectionAssert.AreEqual(new[] { 7, 8 }, arrived.Pieces);
            CollectionAssert.AreEqual(new[] { 4, 4, Zone.Yard, 1, 6, Zone.Yard }, AtOf(s, 7).Concat(AtOf(s, 8)).ToArray());
            CollectionAssert.AreEqual(new[] { "7:4,8→yard:4,4:4@9", "8:1,9→yard:1,6:3@9" }, DeliveryFalls(m.Ev).Select(FallText).ToArray());
            CollectionAssert.IsEmpty(QueueIds(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-26 GDD example: queue [O4 W, B1 Y], only (5,7) free → the O4 stays, the B1 behind it is not held back and lands on (5,7); \"Kamyonda: 1\"")]
        public void K26_GDD_example_the_O4_stays_the_B1_behind_it_is_not_held_back_and_lands_on_5_7()
        {
            var s = Init(K26_EXAMPLE());
            var m = Play(s, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 14 }, Find<DeliveryArrivedEvent>(m.Ev).Pieces);
            CollectionAssert.AreEqual(new[] { 5, 7, Zone.Yard }, AtOf(s, 14));
            Assert.AreEqual(Zone.Queue, PieceZone(s, 13));
            CollectionAssert.AreEqual(new[] { 13 }, QueueIds(s));
            CollectionAssert.AreEqual(new[] { "9:1" }, m.Ev.OfType<DeliveryQueuedEvent>().Select(e => e.Step + ":" + e.Queued).ToArray());
            ExpectConsistent(s);
        }

        [Test(Description = "E-34 x and both dropColumns [3, 0] are full: stage 3 still runs by distance to x (1, 4, 5); the 4th block finds no column and stays queued")]
        public void E34_K25_x_and_both_dropColumns_full_stage_3_runs_by_distance_the_4th_block_stays_queued()
        {
            var s = Init(E34_LEVEL());
            var m = Play(s, Drag(0, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 13, 14, 15 }, Find<DeliveryArrivedEvent>(m.Ev).Pieces);
            CollectionAssert.AreEqual(new[] { 1, 7, Zone.Yard, 4, 7, Zone.Yard, 5, 7, Zone.Yard }, AtOf(s, 13).Concat(AtOf(s, 14)).Concat(AtOf(s, 15)).ToArray());
            CollectionAssert.AreEqual(new[] { 16 }, QueueIds(s));
            Assert.AreEqual(1, Find<DeliveryQueuedEvent>(m.Ev).Queued);
            ExpectConsistent(s);
        }

        [Test(Description = "E-03 a plain yard move that opens room delivers the waiting truck block in its own step 9 (no segment change needed)")]
        public void E03_K26_a_plain_yard_move_that_opens_room_delivers_the_waiting_truck_block_in_its_own_step_9()
        {
            var s = Init(E03_LEVEL());
            var m1 = Play(s, Drag(0, N(6, 8)));
            Assert.AreEqual(1, Find<SiteShiftedEvent>(m1.Ev).ToSeg);
            Assert.IsFalse(Has<DeliveryArrivedEvent>(m1.Ev));
            CollectionAssert.AreEqual(new[] { 14 }, QueueIds(s));
            // the B1 Y leaves (1,6) for (5,7): (0,6)–(1,7) is now a free 2 × 2 on top of columns 0–1
            var m2 = Play(s, Drag(13, N(5, 7)));
            CollectionAssert.AreEqual(new[] { "pieceMoved@1", "movesChanged@4", "deliveryArrived@9", "pieceFell@9", "deliveryQueued@9" }, Steps(m2.Ev));
            var arrived = Find<DeliveryArrivedEvent>(m2.Ev);
            Assert.AreEqual(1, arrived.Seg);
            CollectionAssert.AreEqual(new[] { 14 }, arrived.Pieces);
            Assert.AreEqual(0, Find<DeliveryQueuedEvent>(m2.Ev).Queued);
            CollectionAssert.AreEqual(new[] { 0, 6, Zone.Yard }, AtOf(s, 14));
            CollectionAssert.IsEmpty(QueueIds(s));
            ExpectConsistent(s);
        }

        // =====================================================================================================================
        // Round 3 · K-29 offers and K-39 Undo around the out-of-moves window
        // =====================================================================================================================

        [Test(Description = "K-29 after 3 accepted offers (ad, coins, coins) the next empty counter loses the level at once: no window, no offer, Undo closed")]
        public void K29_after_3_accepted_offers_the_next_empty_counter_loses_the_level_at_once()
        {
            var session = GameSession.Start(Lvl(STREAK(1)));
            int y = 0;
            MoveResult Shuttle(int n)
            {
                MoveResult? last = null;
                for (int i = 0; i < n; i++)
                {
                    y = 1 - y;
                    last = session.Commit(Drag(5, N(5, y)));
                    Assert.AreEqual(MoveStatuses.Applied, last.Status);
                }
                return last!;
            }
            Shuttle(1);
            Assert.AreEqual("1a", OfferText(session.NextOffer()));
            session.AcceptOffer(AddMovesSource.OfferAd);
            Shuttle(5);
            Assert.AreEqual("2", OfferText(session.NextOffer()));
            session.AcceptOffer(AddMovesSource.OfferCoins);
            Shuttle(5);
            Assert.AreEqual("3", OfferText(session.NextOffer()));
            session.AcceptOffer(AddMovesSource.OfferCoins);
            var final = Shuttle(5);
            Assert.IsTrue(final.OutOfMoves);
            CollectionAssert.AreEqual(new object[] { "lost", "null", 3, 16 }, new object[] { session.Outcome, OfferText(session.NextOffer()), session.OffersUsed, session.MovesMade });
            Assert.AreEqual(StateFlag.Lost, Hdr(session.State, H.Flags) & StateFlag.Lost);
            Assert.AreEqual("noOffer", session.AcceptOffer(AddMovesSource.OfferCoins).Reason);
            Assert.AreEqual(UndoBlocks.LevelOver, session.UndoBlock());
            Assert.AreEqual("notPlaying", session.Commit(Drag(0, N(6, 8))).Reason);
        }

        [Test(Description = "K-39 K-29 Undo is closed while the out-of-moves window is open; declining loses the level (Undo, moves and exit closed)")]
        public void K39_K29_Undo_is_closed_while_the_window_is_open_declining_loses_the_level()
        {
            var session = GameSession.Start(Lvl(STREAK(2)));
            session.Commit(Drag(0, N(6, 8)));
            session.Commit(Drag(5, N(5, 1)));
            Assert.AreEqual("outOfMoves", session.Outcome);
            var before = Snapshot(session.State);
            int logLength = session.Log.Count;
            CollectionAssert.AreEqual(new object?[] { UndoBlocks.LossWindow, false }, new object?[] { session.UndoBlock(), session.Undo() });
            CollectionAssert.AreEqual(before, session.State.Buf);
            session.DeclineOffer();
            CollectionAssert.AreEqual(new object?[] { "lost", UndoBlocks.LevelOver, "null" }, new object?[] { session.Outcome, session.UndoBlock(), OfferText(session.NextOffer()) });
            Assert.AreEqual(StateFlag.Lost, Hdr(session.State, H.Flags) & StateFlag.Lost);
            Assert.AreEqual("notPlaying", session.Commit(Drag(1, N(7, 8))).Reason);
            Assert.Catch(() => session.Exit());
            Assert.AreEqual(logLength, session.Log.Count);
        }

        [Test(Description = "K-29 an accepted offer changes neither m nor the streak nor the trowels; a coins offer still counts as n = 1, so offer 2 has no ad option")]
        public void K29_an_accepted_offer_changes_neither_m_nor_the_streak_nor_the_trowels_offer_2_has_no_ad()
        {
            var session = GameSession.Start(Lvl(STREAK(4)));
            var s = session.State;
            session.Commit(Drag(0, N(6, 8)));
            session.Commit(Drag(1, N(7, 8)));
            session.Commit(Drag(2, N(6, 8)));
            session.Commit(Drag(5, N(5, 1))); // yard move: c stays 3, counter 0
            CollectionAssert.AreEqual(new object[] { "outOfMoves", 3 }, new object[] { session.Outcome, Hdr(s, H.Combo) });
            var sink = new ArraySink();
            Assert.AreEqual(MoveStatuses.Applied, session.AcceptOffer(AddMovesSource.OfferCoins, sink).Status);
            CollectionAssert.AreEqual(new[] { "movesChanged@1" }, Steps(sink.Events));
            var mc = Find<MovesChangedEvent>(sink.Events);
            CollectionAssert.AreEqual(new object[] { 5, 5, "offer" }, new object[] { mc.MovesLeft, mc.Delta, mc.Reason });
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 0 }, new[] { session.MovesLeft, session.MovesMade, Hdr(s, H.Combo), Hdr(s, H.Trowels) });
            // the streak goes on from c = 3: the next correct placement earns the trowel
            var fourth = new ArraySink();
            session.Commit(Drag(3, N(7, 8)), fourth);
            Assert.AreEqual(1, Find<TrowelEarnedEvent>(fourth.Events).Trowels);
            foreach (int y in new[] { 0, 1, 0, 1 }) session.Commit(Drag(5, N(5, y)));
            CollectionAssert.AreEqual(new[] { "outOfMoves", "2" }, new[] { session.Outcome, OfferText(session.NextOffer()) });
        }

        [Test(Description = "K-39 GDD example: a wrong placement that bounced (c 3 → 0, counter −1) is undone → c = 3, the counter, m and the wrong count come back")]
        public void K39_GDD_example_a_wrong_placement_that_bounced_is_undone_c_the_counter_m_and_the_wrong_count_come_back()
        {
            var session = GameSession.Start(Lvl(STREAK()));
            var s = session.State;
            session.Commit(Drag(0, N(6, 8)));
            session.Commit(Drag(1, N(7, 8)));
            session.Commit(Drag(2, N(6, 8)));
            CollectionAssert.AreEqual(new[] { 3, 17 }, new[] { Hdr(s, H.Combo), session.MovesLeft });
            var sink = new ArraySink();
            session.Commit(Drag(4, N(7, 8)), sink); // B1 Y onto W (7,2)
            Assert.AreEqual(At.Yard(4, 0), Find<PieceBouncedEvent>(sink.Events).To);
            CollectionAssert.AreEqual(new[] { 0, 16, 1 }, new[] { Hdr(s, H.Combo), session.MovesLeft, Hdr(s, H.WrongCount) });
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(new[] { 3, 17, 3, 0 }, new[] { Hdr(s, H.Combo), session.MovesLeft, session.MovesMade, Hdr(s, H.WrongCount) });
            CollectionAssert.AreEqual(new[] { 4, 0, Zone.Yard }, AtOf(s, 4));
        }

        [Test(Description = "K-39 depth 1 is per move: drag, Undo, a new drag → Undo is available again; the log replays to the start state")]
        public void K39_depth_1_is_per_move_drag_Undo_a_new_drag_Undo_again_the_log_replays_to_the_start_state()
        {
            var lvl = Lvl(STREAK());
            var session = GameSession.Start(lvl);
            var start = Snapshot(session.State);
            session.Commit(Drag(0, N(6, 8)));
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.CanUndo());
            session.Commit(Drag(1, N(7, 8)));
            Assert.IsTrue(session.CanUndo());
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(start, session.State.Buf);
            Assert.IsFalse(session.Undo());
            CollectionAssert.AreEqual(new[] { "start", "drag", "undo", "drag", "undo" }, session.Log.Select(a => a.Kind).ToArray());
            var resumed = GameSession.Replay(lvl, session.Log);
            CollectionAssert.AreEqual(start, resumed.State.Buf);
            CollectionAssert.AreEqual(new object[] { 0, false }, new object[] { resumed.MovesMade, resumed.CanUndo() });
        }

        [Test(Description = "K-39 a Golden Trowel use is never undone: Undo of the drag after it goes back to the post-trowel state (block stays placed, trowel spent)")]
        public void K39_K33_a_Golden_Trowel_use_is_never_undone_Undo_of_the_next_drag_goes_back_to_the_post_trowel_state()
        {
            var session = GameSession.Start(Lvl(STREAK()), new[] { PreBoosters.TrowelStart });
            var s = session.State;
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(GoldTrowel(0, 6, 0)).Status);
            Assert.AreEqual(UndoBlocks.NoDragMove, session.UndoBlock());
            var afterTrowel = Snapshot(s);
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(Drag(1, N(7, 8))).Status);
            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(afterTrowel, s.Buf);
            CollectionAssert.AreEqual(new[] { Zone.Site, 0, 0 }, new[] { PieceZone(s, 0), Hdr(s, H.Trowels), session.MovesMade });
            Assert.AreEqual(UndoBlocks.NoDragMove, session.UndoBlock());
        }

        [Test(Description = "K-39 K-28 a winning drag cannot be undone (level over), and the attempt cannot be exited any more")]
        public void K39_K28_a_winning_drag_cannot_be_undone_and_the_attempt_cannot_be_exited()
        {
            var session = GameSession.Start(Lvl(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("D2_90", "W", 0, 0)) }));
            Assert.IsTrue(session.Commit(Drag(0, N(6, 8))).Won);
            var before = Snapshot(session.State);
            CollectionAssert.AreEqual(new object?[] { UndoBlocks.LevelOver, false }, new object?[] { session.UndoBlock(), session.Undo() });
            CollectionAssert.AreEqual(before, session.State.Buf);
            Assert.Catch(() => session.Exit());
        }

        // =====================================================================================================================
        // Round 3 · K-43 resume and exit
        // =====================================================================================================================

        [Test(Description = "K-43 K-39 level 5 canonical solution: every prefix resumes bit for bit; Undo + redo of each non-winning move (shift and truck moves included) gives the same state and events")]
        public void K43_K39_level_5_canonical_solution_every_prefix_resumes_and_Undo_plus_redo_gives_the_same_state_and_events()
        {
            var lvl = LevelFile(5);
            var live = GameSession.Start(lvl);
            for (int i = 0; i < LEVEL5_HAND.Length; i++)
            {
                var move = LEVEL5_HAND[i];
                var before = Snapshot(live.State);
                var a = new ArraySink();
                Assert.AreEqual(MoveStatuses.Applied, live.Commit(move, a).Status, "move " + (i + 1));
                var after = Snapshot(live.State);
                if (i < LEVEL5_HAND.Length - 1)
                {
                    Assert.IsTrue(live.Undo());
                    CollectionAssert.AreEqual(before, live.State.Buf);
                    var b = new ArraySink();
                    live.Commit(move, b);
                    CollectionAssert.AreEqual(after, live.State.Buf);
                    Assert.AreEqual(Moves.EventLogHash(a.Events), Moves.EventLogHash(b.Events), "move " + (i + 1));
                }
                var resumed = GameSession.Replay(lvl, live.Log);
                CollectionAssert.AreEqual(live.State.Buf, resumed.State.Buf);
                CollectionAssert.AreEqual(new object[] { true, live.CanUndo(), live.Outcome }, new object[] { resumed.Resumed, resumed.CanUndo(), resumed.Outcome });
            }
            CollectionAssert.AreEqual(new object[] { false, "won", 6, 11 }, new object[] { live.Resumed, live.Outcome, live.MovesLeft, live.MovesMade });
            Assert.AreEqual("8,0,1", YaoText(live.Yao()));
        }

        [Test(Description = "K-43 E-41 K-40 Termos + streak tier 2 (+2 moves, +1 trowel): 20 → 25 moves and 1 trowel; using that trowel keeps m = 0, the exit is free and the bonus is not consumed")]
        public void K43_E41_K40_Termos_and_streak_tier_2_using_the_bonus_trowel_keeps_m_0_and_the_exit_free()
        {
            var lvl = Lvl(STREAK(20));
            var bonus = new Dictionary<int, StreakBonus> { { 1, new StreakBonus(1, 0) }, { 2, new StreakBonus(2, 1) }, { 3, new StreakBonus(3, 1) } };
            var opts = new SessionOptions(streakBonus: tier => bonus[tier]);
            var session = GameSession.Start(lvl, new[] { PreBoosters.Thermos }, 2, opts);
            CollectionAssert.AreEqual(new[] { 25, 1, 0 }, new[] { session.MovesLeft, Hdr(session.State, H.Trowels), session.MovesMade });
            Assert.AreEqual(MoveStatuses.Applied, session.Commit(GoldTrowel(0, 6, 0)).Status);
            var resumed = GameSession.Replay(lvl, session.Log, opts);
            CollectionAssert.AreEqual(session.State.Buf, resumed.State.Buf);
            Assert.AreEqual("free|0|0|thermos|False", ExitText(resumed.Exit()));
        }

        [Test(Description = "K-43 item 4 levelHash: 16 hex digits, independent of JSON key order, changed by any change of the level data")]
        public void K43_item_4_levelHash_16_hex_digits_independent_of_key_order_changed_by_any_data_change()
        {
            var json = Builders.LevelJson(TWO_ROOMS());
            string h = GameSession.LevelHash(json);
            StringAssert.IsMatch("^[0-9a-f]{16}$", h);
            var reordered = JsonValue.Object();
            foreach (var kv in json.Members.Reverse()) reordered.Set(kv.Key, kv.Value);
            Assert.AreEqual(h, GameSession.LevelHash(reordered));
            Assert.AreEqual(GameSession.LevelHash(Builders.Level(TWO_ROOMS())), GameSession.LevelHash(Builders.Level(TWO_ROOMS())));
            Assert.AreNotEqual(h, GameSession.LevelHash(Builders.LevelJson(TWO_ROOMS(moves: 21))));
            Assert.AreNotEqual(h, GameSession.LevelHash(Builders.LevelJson(TWO_ROOMS(pieces: Ps(Pc("O4_0", "W", 0, 6), Pc("D2_90", "R", 3, 7))))));
        }

        // =====================================================================================================================
        // review round 4: K-43 replay(sink) gives the caller every event of the attempt
        // =====================================================================================================================

        /// <summary>Live: every commit / offer into one sink. Then the replay of the log into another; both streams must be equal.</summary>
        private static (List<GameEvent> Live, List<GameEvent> Replayed, GameSession Game) LiveThenReplay(
            CompiledLevel lvl, string[] pre, int tier, SessionOptions opts, Action<GameSession, ArraySink> script)
        {
            var live = new ArraySink();
            var game = GameSession.Start(lvl, pre, tier, opts, live);
            script(game, live);
            var replayed = new ArraySink();
            var again = GameSession.Replay(lvl, game.Log, opts, replayed);
            CollectionAssert.AreEqual(game.State.Buf, again.State.Buf);
            return (live.Events.ToList(), replayed.Events.ToList(), game);
        }

        private static string[] EvJsons(IEnumerable<GameEvent> ev) => ev.Select(e => JsonWriter.Canonical(EventJson.ToJson(e))).ToArray();

        [Test(Description = "K-43 / TECH 6 level 5 with Termos (K-40 start bonus), two K-17 wrong placements, one Undo (K-39) and the canonical solution (truck, segment slide): the replay sink equals the live stream")]
        public void K43_K40_K17_K39_level_5_with_Termos_wrong_placements_Undo_and_the_canonical_solution_replay_sink_equals_live()
        {
            var lvl = LevelFile(5);
            var (live, replayed, game) = LiveThenReplay(lvl, new[] { PreBoosters.Thermos }, 0, new SessionOptions(), (g, sink) =>
            {
                Assert.AreEqual(MoveStatuses.Applied, g.Commit(LEVEL5_HAND[0], sink).Status); // d parked
                Assert.AreEqual(MoveStatuses.Applied, g.Commit(LEVEL5_HAND[1], sink).Status); // a → (4,0)
                Assert.AreEqual(MoveStatuses.Applied, g.Commit(Drag(1, N(5, 5)), sink).Status); // b (W) onto the Y column: K-17 bounce
                Assert.IsTrue(g.Undo());
                Assert.AreEqual(MoveStatuses.Applied, g.Commit(Drag(1, N(5, 5)), sink).Status); // the same wrong drop again (not undone)
                foreach (var m in LEVEL5_HAND.Skip(2)) Assert.AreEqual(MoveStatuses.Applied, g.Commit(m, sink).Status);
            });
            Assert.AreEqual("won", game.Outcome);
            Assert.AreEqual(2, live.Count(e => e is PlacementWrongEvent));
            Assert.IsTrue(live.Any(e => e is DeliveryArrivedEvent));
            Assert.IsTrue(live.OfType<MovesChangedEvent>().Any(e => e.Reason != "move")); // Termos +3 at the start
            CollectionAssert.AreEqual(EvJsons(live), EvJsons(replayed));
            Assert.AreEqual(Moves.EventLogHash(live), Moves.EventLogHash(replayed));
        }

        [Test(Description = "K-43 / K-29 an accepted +5 offer (coins) and a K-40 streak bonus replay into the same events, in log order")]
        public void K43_K29_K40_an_accepted_offer_and_a_streak_bonus_replay_into_the_same_events_in_log_order()
        {
            var lvl = Lvl(new LevelSpec { Moves = 1, Plan = new[] { "WW", "WW" }, Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "W", 2, 0), Pc("B1_0", "Y", 5, 0)) });
            var opts = new SessionOptions(streakBonus: _ => new StreakBonus(2, 0));
            var (live, replayed, game) = LiveThenReplay(lvl, new string[0], 1, opts, (g, sink) =>
            {
                foreach (int y in new[] { 1, 0, 1 }) Assert.AreEqual(MoveStatuses.Applied, g.Commit(Drag(2, N(5, y)), sink).Status);
                Assert.AreEqual("outOfMoves", g.Outcome);
                Assert.AreEqual(MoveStatuses.Applied, g.AcceptOffer(AddMovesSource.OfferCoins, sink).Status);
                Assert.AreEqual(MoveStatuses.Applied, g.Commit(Drag(0, N(6, 8)), sink).Status);
            });
            Assert.AreEqual(1, game.OffersUsed);
            Assert.AreEqual(1, live.OfType<MovesChangedEvent>().Count(e => e.Reason == "offer"));
            CollectionAssert.AreEqual(EvJsons(live), EvJsons(replayed));
        }
    }
}
