#nullable enable
// Port of tests/obstacles/S2_planVoid.test.ts (web repo): S2 Plan Boşluğu is out of the MVP (R2-01), the `.` cell rules stay
// in the core (K-15, K-16, K-17, K-34, E-43). Drags go through `Boards.Drop`; the session / event halves (`won`, YAO,
// `wrongCount`, the `legacyTrowel` rejection) come with the K-35 / session packages.
using System.Linq;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests.Obstacles
{
    [TestFixture]
    public class S2PlanVoidTests
    {
        /// <summary>OBSTACLES S2 example: column 7 is y0 W, y1 `.`, y2 W (plan top → bottom).</summary>
        private static LevelSpec EXAMPLE() => new LevelSpec { Plan = new[] { "WW", "W.", "WW" }, Pieces = Ps(Pc("B1_0", "W", 3, 0), Pc("B1_0", "W", 2, 0)) };

        /// <summary>Plan (bottom → top) `WW`, `W.`, `YY`: a C3 W fills the L, a D2_90 Y bridges both columns above the void.</summary>
        private static LevelSpec BRIDGE() => new LevelSpec { Plan = new[] { "YY", "W.", "WW" }, Pieces = Ps(Pc("C3_0", "W", 0, 0), Pc("D2_90", "Y", 2, 0)) };

        /// <summary>Plan (bottom → top) `W.`, `YY` with a debris block in the void (7,0) (E-43).</summary>
        private static LevelSpec DEBRIS_IN_VOID(bool debris = true) => new LevelSpec
        {
            Plan = new[] { "YY", "W." },
            Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_90", "Y", 2, 0)),
            Debris = debris ? new[] { new D("B1_0", "R", 7, 0) } : new D[0],
        };

        /// <summary>
        /// The Faz 2 "Pencere" board (old level 4) without its decoys, as a full-cover builder level: plan (bottom → top) `YY`,
        /// `WW`, `W.`, `WW`, `RR`; wall 6 with a static gap at rows 3–4. The lintel (c) reaches row 3 above the window only by rail.
        /// </summary>
        private static LevelSpec PENCERE() => new LevelSpec
        {
            Moves = 12,
            Wall = Wall(6, Gap(3, 2)),
            Plan = new[] { "RR", "WW", "W.", "WW", "YY" },
            Pieces = Ps(Pc("D2_90", "Y", 0, 6), Pc("C3_0", "W", 2, 0), Pc("D2_90", "W", 4, 3), Pc("D2_90", "R", 0, 2)),
        };

        [Test(Description = "S2 is out of the MVP (R2-01): no rule plugin, not even for a `.` plan; the void rules stay core (K-34)")]
        public void S2_is_out_of_the_MVP_no_rule_plugin()
        {
            CollectionAssert.DoesNotContain(Registry.AllRules.Select(r => r.Id).ToArray(), "S2");
            var pencere = Builders.Compiled(PENCERE());
            CollectionAssert.AreEqual(new[] { "W1" }, Registry.ActiveRuleIds(pencere));
            Assert.IsTrue(Registry.LevelHooks(pencere).IsEmpty);
        }

        [Test(Description = "S2 K-17 OBSTACLES example: a block dropped onto the void is wrong (window) and bounces back to its start")]
        public void S2_K17_a_block_dropped_onto_the_void_is_wrong_and_bounces_back_to_its_start()
        {
            var s = Init(EXAMPLE());
            Assert.IsTrue(Drop(s, 0, 7, 8).Correct); // (7,0) W
            int before = Hdr(s, H.MovesLeft);
            var r = Drop(s, 1, 7, 8);
            Assert.AreEqual(PlacementOutcome.KindBounced, r.Outcome!.Kind);
            Assert.AreEqual(A(7, 1), r.Outcome.Landing);
            Assert.AreEqual(VerdictReasons.Window, r.Outcome.Reason);
            Assert.AreEqual(YardP(2, 0), r.Outcome.Target!.To);
            Assert.AreEqual(before - 1, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(new[] { Zone.Yard, 2, 0 }, new[] { PieceZone(s, 1), PieceX(s, 1), PieceY(s, 1) });
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1)); // the void is still empty
            ExpectConsistent(s);
        }

        [Test(Description = "S2 K-18 the shadow verdict already shows the void before the release (registry fall hooks)")]
        public void S2_K18_the_shadow_verdict_already_shows_the_void_before_the_release()
        {
            var s = Init(EXAMPLE());
            Drop(s, 0, 7, 8);
            var shadow = Gravity.ComputeFall(s, 1, N(7, 8), new FallOptions(null, Registry.LevelHooks(s.Lvl).Fall));
            Assert.AreEqual(A(7, 1), shadow.Landing);
            AssertVerdict(shadow.Verdict, false, Reasons(VerdictReasons.Window), NoCells);
        }

        [Test(Description = "S2 K-34 a 2-wide block bridging both columns over the wall fills the cell above an empty void")]
        public void S2_K34_a_2_wide_block_bridging_both_columns_fills_the_cell_above_an_empty_void()
        {
            var s = Init(BRIDGE());
            var l = Drop(s, 0, 6, 8);
            CollectionAssert.AreEqual(new[] { Site(6, 0), Site(7, 0), Site(6, 1) }, l.Outcome!.Cells);
            var bridge = Drop(s, 1, 6, 8);
            Assert.IsTrue(bridge.Correct);
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, bridge.Outcome!.Cells);
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1)); // (7,1) stays empty under the bridge
        }

        [Test(Description = "S2 K-15 a segment is complete with its void cells empty")]
        public void S2_K15_a_segment_is_complete_with_its_void_cells_empty()
        {
            var s = Init(BRIDGE());
            Drop(s, 0, 6, 8);
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            var r = Drop(s, 1, 6, 8);
            Assert.AreEqual(0, r.Completed!.Seg);
            Assert.IsTrue(Goals.LevelGoalsMet(s));
        }

        [Test(Description = "S2 K-34 the build front skips an empty void and offers the cell above it")]
        public void S2_K34_the_build_front_skips_an_empty_void_and_offers_the_cell_above_it()
        {
            var s = Init(BRIDGE());
            CollectionAssert.AreEqual(new[] { Site(6, 0), Site(7, 0) }, Placement.BuildFront(s));
            Drop(s, 0, 6, 8);
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, Placement.BuildFront(s));
        }

        [Test(Description = "S2 K-33 the Golden Trowel places a block over an empty void, never into the void (Faz 2R: blocks, not cells)")]
        public void S2_K33_the_Golden_Trowel_places_a_block_over_an_empty_void_never_into_the_void()
        {
            var s = Init(BRIDGE());
            Drop(s, 0, 6, 8);
            Combo.GrantTrowels(s, 1);
            CollectionAssert.AreEqual(new[] { A(6, 2) }, Boosters.TrowelSpots(s, 1));
            Assert.IsNull(Boosters.GoldTrowelRejection(s, new GoldTrowelMove(1, 6, 2), MoveHooks.None));
            Boosters.ApplyTrowelEffect(s, 1, A(6, 2));
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1));
            Assert.AreEqual(2, SiteOcc(s, 0, 1, 2));
            ExpectConsistent(s);
        }

        [Test(Description = "S2 E-43 a void holding debris is not filled: no build front in that column, the block above it is wrong (support)")]
        public void S2_E43_a_void_holding_debris_is_not_filled()
        {
            var s = Init(DEBRIS_IN_VOID());
            CollectionAssert.AreEqual(new[] { Site(6, 0) }, Placement.BuildFront(s));
            Drop(s, 0, 6, 8); // (6,0) W
            CollectionAssert.AreEqual(new[] { Site(6, 1) }, Placement.BuildFront(s));
            var r = Drop(s, 1, 6, 8); // lands on (6,1)–(7,1): right colours, but the void holds debris
            Assert.AreEqual(PlacementOutcome.KindBounced, r.Outcome!.Kind);
            Assert.AreEqual(VerdictReasons.Support, r.Outcome.Reason);
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, r.Outcome.MissingSupport);

            // the same move without the debris is correct (an empty void counts as filled)
            var clean = Init(DEBRIS_IN_VOID(false));
            Drop(clean, 0, 6, 8);
            Assert.IsTrue(Drop(clean, 1, 6, 8).Correct);
        }

        [Test(Description = "S2 K-34 the Pencere board: the lintel railed in before the wall below the window is wrong for support")]
        public void S2_K34_the_Pencere_board_the_lintel_railed_in_before_the_wall_below_the_window_is_wrong_for_support()
        {
            var s = Init(PENCERE());
            Drop(s, 0, 6, 8); // a: Y row 0
            // (6,1), (6,2), (7,1) are still empty W cells; the empty window (7,2) counts as filled
            var missing = new[] { Site(6, 1), Site(6, 2), Site(7, 1) };
            var shadow = Gravity.ComputeFall(s, 2, R(0, 6, 3), new FallOptions(null, Registry.LevelHooks(s.Lvl).Fall));
            AssertVerdict(shadow.Verdict, false, Reasons(VerdictReasons.Support), missing);
            var r = Drop(s, 2, 6, 3, 0);
            Assert.AreEqual(PlacementOutcome.KindBounced, r.Outcome!.Kind);
            Assert.AreEqual(VerdictReasons.Support, r.Outcome.Reason);
            CollectionAssert.AreEqual(missing, r.Outcome.MissingSupport);
            Assert.AreEqual(YardP(4, 3), r.Outcome.Target!.To);
        }

        [Test(Description = "S2 the Pencere board solution: the lintel goes through the gap above the window (4 moves, 8 left; the YAO 3/4 comes with K-35)")]
        public void S2_the_Pencere_board_solution()
        {
            var s = Init(PENCERE());
            var results = new[] { Drop(s, 0, 6, 8), Drop(s, 1, 6, 8), Drop(s, 2, 6, 3, 0), Drop(s, 3, 6, 8) };
            Assert.IsTrue(results.All(r => r.Correct));
            Assert.IsTrue(Goals.LevelGoalsMet(s));
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 2)); // the window (7,2) stays open
        }
    }
}
