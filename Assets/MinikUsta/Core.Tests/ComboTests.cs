#nullable enable
// Port of tests/core/combo.test.ts (web repo): K-33 Usta Serisi and the Faz 2R Golden Trowel through the K-35 pipeline
// (`Run` = TS `run`, strict ArraySink): the comboChanged / trowelEarned order, boosterApplied / pieceLifted, the mini
// pipeline of a segment-completing trowel (E-09), the `legacyTrowel` rejection and the D3a pre-check.
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class ComboTests
    {
        /// <summary>Plan of four W rows; four vertical W dominoes, a Y block and a W single.</summary>
        private static LevelSpec TOWER() => new LevelSpec
        {
            Wall = Wall(2),
            Plan = WW(4),
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_0", "W", 2, 0), Pc("D2_0", "W", 3, 0), Pc("B1_0", "Y", 4, 0), Pc("B1_0", "W", 5, 0)),
        };

        /// <summary>TS `trowel(seg, x, y)`: the Faz 2 cell trowel record (rejected since Faz 2R).</summary>
        private static Move Trowel(int seg, int x, int y) => new TrowelMove(seg, x, y);

        private static int[] Counters(GameState s) => new[] { Hdr(s, H.Turn), Hdr(s, H.MovesLeft), Combo.ComboOf(s), Hdr(s, H.MovesSpent) };

        [Test(Description = "K-33 GDD example: correct, correct, yard move, correct, correct → 1 Golden Trowel and c = 0")]
        public void K33_GDD_example_correct_correct_yard_move_correct_correct_earns_one_trowel()
        {
            var s = Init(TOWER());
            Run(s, DragTo(0, 6, 8));
            Run(s, DragTo(1, 7, 8));
            Assert.AreEqual(2, Combo.ComboOf(s));
            Run(s, DragTo(4, 4, 1)); // yard move: c unchanged
            Assert.AreEqual(2, Combo.ComboOf(s));
            Run(s, DragTo(2, 6, 8));
            Assert.AreEqual(3, Combo.ComboOf(s));
            var fourth = Run(s, DragTo(3, 7, 8));
            CollectionAssert.AreEqual(
                new[] { "comboChanged@3", "trowelEarned@3", "comboChanged@3" },
                fourth.Ev.Where(e => e is ComboChangedEvent || e is TrowelEarnedEvent).Select(e => e.TypeName + "@" + e.Step).ToArray());
            CollectionAssert.AreEqual(new[] { 4, 0 }, fourth.Ev.OfType<ComboChangedEvent>().Select(e => e.Combo).ToArray());
            Assert.AreEqual(1, Find<TrowelEarnedEvent>(fourth.Ev).Trowels);
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { Combo.ComboOf(s), Combo.TrowelsOf(s) });
        }

        [Test(Description = "K-33 a wrong placement sets c = 0 (correct, correct, wrong)")]
        public void K33_a_wrong_placement_sets_c_0()
        {
            var s = Init(TOWER());
            Run(s, DragTo(0, 6, 8));
            Run(s, DragTo(1, 7, 8));
            var wrong = Run(s, DragTo(4, 6, 8)); // Y on W
            CollectionAssert.Contains(Types(wrong.Ev), "pieceBounced");
            Assert.AreEqual(0, Combo.ComboOf(s));
        }

        [Test(Description = "K-33 counter helpers: the 4th step earns a trowel; reset reports a change only when c > 0")]
        public void K33_counter_helpers()
        {
            var s = Init(TOWER());
            Assert.AreEqual(4, Combo.ComboForTrowel);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, new[] { Combo.ComboOnCorrect(s).Combo, Combo.ComboOnCorrect(s).Combo, Combo.ComboOnCorrect(s).Combo });
            Assert.AreEqual(new ComboStep(4, 0, true, 1), Combo.ComboOnCorrect(s));
            Assert.IsFalse(Combo.ComboReset(s));
            Combo.ComboOnCorrect(s);
            Assert.IsTrue(Combo.ComboReset(s));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Combo.GrantTrowels(s, -1));
        }

        // --- K-33 Golden Trowel (Faz 2R: a yard block flies to a spot of its P set) ---------------------------------------------

        /// <summary>GDD K-33 example (Bölüm 8 shape, default board): `O4` R buried under an Ağır Yük; plan bottom → top RR, RR, WW.</summary>
        private static LevelSpec BURIED() => new LevelSpec
        {
            Wall = Wall(2),
            Plan = new[] { "WW", "RR", "RR" },
            Pieces = Ps(Pc("O4_0", "R", 0, 0), Pc("Q9_0", "W", 0, 2), Pc("D2_90", "W", 3, 0)),
        };

        [Test(Description = "K-33 GDD example: the buried O4 R flies to its only spot (6,0) and locks; no move, m or streak (E-50: a D2_90 W with an empty P is refused, the trowel kept)")]
        public void K33_GDD_example_the_buried_O4_R_flies_to_its_only_spot_and_locks()
        {
            var s = Init(BURIED());
            Combo.GrantTrowels(s, 1);
            CollectionAssert.AreEqual(new[] { A(6, 0) }, Boosters.TrowelSpots(s, 0));
            CollectionAssert.IsEmpty(Boosters.TrowelSpots(s, 2));
            var refused = Run(s, new GoldTrowelMove(2, 6, 2));
            AssertResult(refused.Res, MoveStatuses.Rejected, BoosterRejects.NoTarget, false, false);
            Assert.AreEqual(1, Combo.TrowelsOf(s));
            var before = Counters(s);
            var r = Run(s, new GoldTrowelMove(0, 6, 0));
            Assert.AreEqual(MoveStatuses.Applied, r.Res.Status);
            var applied = Find<BoosterAppliedEvent>(r.Ev);
            Assert.AreEqual(1, applied.Step);
            Assert.AreEqual(BoosterNames.Trowel, applied.Booster);
            Assert.AreEqual(
                JsonWriter.Canonical(J.O("pieceId", 0, "to", EventJson.AtJson(At.Site(6, 0, 0)), "trowels", 0)),
                JsonWriter.Canonical(applied.Detail!));
            var lifted = Find<PieceLiftedEvent>(r.Ev);
            Assert.AreEqual("trowel", lifted.By);
            Assert.AreEqual(At.Yard(0, 0), lifted.From);
            CollectionAssert.DoesNotContain(Types(r.Ev), "placementCorrect");
            CollectionAssert.AreEqual(new object[] { Zone.Site, true, 0b11 }, new object[] { PieceZone(s, 0), HasFlag(s, 0, FlagBit.Locked), FilledMask(s, 0, 0) & 0b11 });
            // boosters never spend a move, advance m / movesSpent or touch the streak (GDD §10); YAO ignores it (K-46)
            CollectionAssert.AreEqual(before, Counters(s));
            Assert.IsNull(Moves.MeasureYao(s).Yao);
            CollectionAssert.DoesNotContain(Types(r.Ev), "movesChanged");
            ExpectConsistent(s);
        }

        [Test(Description = "K-33 a refused trowel target is not consumed and changes nothing (no trowel, empty P, a spot outside P, the Faz 2 cell trowel)")]
        public void K33_a_refused_trowel_target_is_not_consumed_and_changes_nothing()
        {
            var s = Init(BURIED());
            var none = Run(s, new GoldTrowelMove(0, 6, 0));
            AssertResult(none.Res, MoveStatuses.Rejected, BoosterRejects.NoTrowel, false, false);
            Assert.AreEqual(1, none.Ev.Count);
            var rejected = Find<BoosterRejectedEvent>(none.Ev);
            CollectionAssert.AreEqual(new object[] { 0, 0, BoosterNames.Trowel, BoosterRejects.NoTrowel }, new object[] { rejected.Seq, rejected.Step, rejected.Booster, rejected.Reason });
            Combo.GrantTrowels(s, 1);
            var before = Snapshot(s);
            Assert.AreEqual(BoosterRejects.NotCorrect, Run(s, new GoldTrowelMove(0, 6, 1)).Res.Reason);
            Assert.AreEqual(BoosterRejects.NoTarget, Run(s, new GoldTrowelMove(1, 6, 0)).Res.Reason); // Ağır Yük
            Assert.AreEqual(BoosterRejects.LegacyTrowel, Run(s, Trowel(0, 0, 0)).Res.Reason);
            CollectionAssert.AreEqual(before, s.Buf);
        }

        [Test(Description = "K-33 a trowel that completes the segment runs the mini pipeline: shift and delivery, no m, no timers (E-09)")]
        public void K33_a_trowel_that_completes_the_segment_runs_the_mini_pipeline_E09()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("D2_90", "W", 0, 8)) } },
            });
            Run(s, DragTo(0, 6, 8));
            Combo.GrantTrowels(s, 1);
            var r = Run(s, new GoldTrowelMove(1, 7, 0));
            CollectionAssert.AreEqual(
                new[] { "boosterApplied@1", "pieceLifted@1", "segmentCompleted@8", "goalProgress@8", "siteShifted@8", "deliveryArrived@9", "pieceFell@9" },
                r.Ev.Select(e => e.TypeName + "@" + e.Step).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1 }, new[] { Hdr(s, H.Turn), Hdr(s, H.ActiveSeg) });
            ExpectConsistent(s);
        }

        [Test(Description = "K-33 a trowel placement on a hidden `?` cell opens it (K-32)")]
        public void K33_a_trowel_placement_on_a_hidden_cell_opens_it()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "??", "WY" },
                Hidden = Repeat1,
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "Y", 3, 0)),
            });
            Run(s, DragTo(0, 6, 8));
            Run(s, DragTo(1, 7, 8));
            Combo.GrantTrowels(s, 1);
            var r = Run(s, new GoldTrowelMove(3, 7, 1));
            var revealed = Find<CellsRevealedEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 1, 0 }, new[] { revealed.Step, revealed.Seg });
            Assert.AreEqual(1, revealed.Cells.Length);
            CollectionAssert.AreEqual(new object[] { 7, 1, "Y" }, new object[] { revealed.Cells[0].X, revealed.Cells[0].Y, revealed.Cells[0].Color });
            Assert.AreEqual(1 << 3, RevealedMask(s, 0));
        }

        [Test(Description = "K-33 pre-check: a P spot whose result is a D3a dead end is left out and refused (CL-2R-05, GDD K-30 tiling example)")]
        public void K33_precheck_a_P_spot_whose_result_is_a_D3a_dead_end_is_left_out_and_refused()
        {
            // plan bottom → top: YW, YW, WW, WW; blocks D2_0 Y, D2_0 W, O4 W (GDD K-30 "döşeme çıkmazı")
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW", "WW", "YW", "YW" },
                Pieces = Ps(Pc("D2_0", "Y", 0, 0), Pc("D2_0", "W", 1, 0), Pc("O4_0", "W", 2, 0)),
            });
            Run(s, DragTo(0, 6, 8)); // Y on column 6, rows 0–1
            Combo.GrantTrowels(s, 1);
            CollectionAssert.AreEqual(new[] { A(6, 2), A(7, 0) }, Boosters.TrowelSpots(s, 1));
            CollectionAssert.AreEqual(new[] { A(7, 0) }, Boosters.TrowelSpots(s, 1, true));
            var before = Snapshot(s);
            var r = Run(s, new GoldTrowelMove(1, 6, 2));
            Assert.AreEqual(BoosterRejects.Precheck, r.Res.Reason);
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreEqual(MoveStatuses.Applied, Run(s, new GoldTrowelMove(1, 7, 0)).Res.Status);
        }
    }
}
