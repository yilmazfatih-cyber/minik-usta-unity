#nullable enable
// Port of tests/core/combo.test.ts (web repo). Drags go through `Boards.Drop` (steps 2–3 + streak), booster uses through the
// Boosters helpers (`GoldTrowelRejection`, `ApplyTrowelEffect`); the event assertions (comboChanged / trowelEarned order,
// boosterApplied, pieceLifted, the mini pipeline of a segment-completing trowel E-09, the `legacyTrowel` rejection) come
// with the K-35 package.
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

        [Test(Description = "K-33 GDD example: correct, correct, yard move, correct, correct → 1 Golden Trowel and c = 0 (state half; the comboChanged / trowelEarned events come with K-35)")]
        public void K33_GDD_example_correct_correct_yard_move_correct_correct_earns_one_trowel()
        {
            var s = Init(TOWER());
            Drop(s, 0, 6, 8);
            Drop(s, 1, 7, 8);
            Assert.AreEqual(2, Combo.ComboOf(s));
            Drop(s, 4, 4, 1); // yard move: c unchanged
            Assert.AreEqual(2, Combo.ComboOf(s));
            Drop(s, 2, 6, 8);
            Assert.AreEqual(3, Combo.ComboOf(s));
            var fourth = Drop(s, 3, 7, 8);
            Assert.IsTrue(fourth.Correct);
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { Combo.ComboOf(s), Combo.TrowelsOf(s) });
        }

        [Test(Description = "K-33 a wrong placement sets c = 0 (correct, correct, wrong)")]
        public void K33_a_wrong_placement_sets_c_0()
        {
            var s = Init(TOWER());
            Drop(s, 0, 6, 8);
            Drop(s, 1, 7, 8);
            var wrong = Drop(s, 4, 6, 8); // Y on W
            Assert.AreEqual(PlacementOutcome.KindBounced, wrong.Outcome!.Kind);
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
            Assert.AreEqual(BoosterRejects.NoTarget, Boosters.GoldTrowelRejection(s, new GoldTrowelMove(2, 6, 2), MoveHooks.None));
            Assert.AreEqual(1, Combo.TrowelsOf(s));
            var before = new[] { Hdr(s, H.Turn), Hdr(s, H.MovesLeft), Combo.ComboOf(s), Hdr(s, H.MovesSpent) };
            Assert.IsNull(Boosters.GoldTrowelRejection(s, new GoldTrowelMove(0, 6, 0), MoveHooks.None));
            var fx = Boosters.ApplyTrowelEffect(s, 0, A(6, 0));
            Assert.AreEqual(YardAt(0, 0), fx.From);
            Assert.AreEqual(Site(6, 0, 0), fx.To);
            Assert.AreEqual(0, fx.Trowels);
            CollectionAssert.AreEqual(new object[] { Zone.Site, true, 0b11 }, new object[] { PieceZone(s, 0), HasFlag(s, 0, FlagBit.Locked), FilledMask(s, 0, 0) & 0b11 });
            // boosters never spend a move, advance m / movesSpent or touch the streak (GDD §10)
            CollectionAssert.AreEqual(before, new[] { Hdr(s, H.Turn), Hdr(s, H.MovesLeft), Combo.ComboOf(s), Hdr(s, H.MovesSpent) });
            ExpectConsistent(s);
        }

        [Test(Description = "K-33 a refused trowel target is not consumed and changes nothing (no trowel, empty P, a spot outside P; the Faz 2 cell trowel rejection comes with K-35)")]
        public void K33_a_refused_trowel_target_is_not_consumed_and_changes_nothing()
        {
            var s = Init(BURIED());
            Assert.AreEqual(BoosterRejects.NoTrowel, Boosters.GoldTrowelRejection(s, new GoldTrowelMove(0, 6, 0), MoveHooks.None));
            Combo.GrantTrowels(s, 1);
            var before = Snapshot(s);
            Assert.AreEqual(BoosterRejects.NotCorrect, Boosters.GoldTrowelRejection(s, new GoldTrowelMove(0, 6, 1), MoveHooks.None));
            Assert.AreEqual(BoosterRejects.NoTarget, Boosters.GoldTrowelRejection(s, new GoldTrowelMove(1, 6, 0), MoveHooks.None)); // Ağır Yük
            CollectionAssert.AreEqual(before, s.Buf);
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
            Drop(s, 0, 6, 8);
            Drop(s, 1, 7, 8);
            Combo.GrantTrowels(s, 1);
            var fx = Boosters.ApplyTrowelEffect(s, 3, A(7, 1));
            Assert.AreEqual(1, fx.Revealed.Length);
            Assert.AreEqual(7, fx.Revealed[0].X);
            Assert.AreEqual(1, fx.Revealed[0].Y);
            Assert.AreEqual("Y", fx.Revealed[0].Color);
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
            Drop(s, 0, 6, 8); // Y on column 6, rows 0–1
            Combo.GrantTrowels(s, 1);
            CollectionAssert.AreEqual(new[] { A(6, 2), A(7, 0) }, Boosters.TrowelSpots(s, 1));
            CollectionAssert.AreEqual(new[] { A(7, 0) }, Boosters.TrowelSpots(s, 1, true));
            var before = Snapshot(s);
            Assert.AreEqual(BoosterRejects.Precheck, Boosters.GoldTrowelRejection(s, new GoldTrowelMove(1, 6, 2), MoveHooks.None));
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.IsNull(Boosters.GoldTrowelRejection(s, new GoldTrowelMove(1, 7, 0), MoveHooks.None));
        }
    }
}
