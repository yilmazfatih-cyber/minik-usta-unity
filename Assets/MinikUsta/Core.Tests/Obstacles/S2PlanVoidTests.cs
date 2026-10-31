#nullable enable
// Port of tests/obstacles/S2_planVoid.test.ts (web repo): S2 Plan Boşluğu is out of the MVP (R2-01), the `.` cell rules stay
// in the core (K-15, K-16, K-17, K-34, E-43). Drags go through the K-35 pipeline (`Run`), the Pencere board through
// `GameSession` (events, `won`, YAO, `wrongCount`, the `legacyTrowel` rejection; P3).
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
            CollectionAssert.Contains(Types(Run(s, DragTo(0, 7, 8)).Ev), "placementCorrect"); // (7,0) W
            int before = Hdr(s, H.MovesLeft);
            var ev = Run(s, DragTo(1, 7, 8)).Ev;
            var fell = Find<PieceFellEvent>(ev).To;
            CollectionAssert.AreEqual(new object[] { At.ZoneSite, 7, 1 }, new object[] { fell.Zone, fell.X, fell.Y });
            var wrong = Find<PlacementWrongEvent>(ev);
            Assert.AreEqual(1, wrong.PieceId);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Window), wrong.Reasons);
            var bounced = Find<PieceBouncedEvent>(ev);
            Assert.AreEqual(VerdictReasons.Window, bounced.Reason);
            Assert.AreEqual(YardAt(2, 0), bounced.To);
            Assert.AreEqual(before - 1, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(new[] { Zone.Yard, 2, 0 }, new[] { PieceZone(s, 1), PieceX(s, 1), PieceY(s, 1) });
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1)); // the void is still empty
            ExpectConsistent(s);
        }

        [Test(Description = "S2 K-18 the shadow verdict already shows the void before the release (registry fall hooks)")]
        public void S2_K18_the_shadow_verdict_already_shows_the_void_before_the_release()
        {
            var s = Init(EXAMPLE());
            Run(s, DragTo(0, 7, 8));
            var shadow = Gravity.ComputeFall(s, 1, N(7, 8), new FallOptions(null, Registry.LevelHooks(s.Lvl).Fall));
            Assert.AreEqual(A(7, 1), shadow.Landing);
            Assert.IsFalse(shadow.Verdict.Ok);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Window), shadow.Verdict.Reasons);
        }

        [Test(Description = "S2 K-34 a 2-wide block bridging both columns over the wall fills the cell above an empty void")]
        public void S2_K34_a_2_wide_block_bridging_both_columns_fills_the_cell_above_an_empty_void()
        {
            var s = Init(BRIDGE());
            var l = Run(s, DragTo(0, 6, 8));
            CollectionAssert.AreEqual(new[] { Site(6, 0), Site(7, 0), Site(6, 1) }, Find<PlacementCorrectEvent>(l.Ev).Cells);
            var bridge = Find<PlacementCorrectEvent>(Run(s, DragTo(1, 6, 8)).Ev);
            Assert.AreEqual(1, bridge.PieceId);
            Assert.IsTrue(bridge.OverWall);
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, bridge.Cells);
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1)); // (7,1) stays empty under the bridge
        }

        [Test(Description = "S2 K-15 a segment is complete with its void cells empty")]
        public void S2_K15_a_segment_is_complete_with_its_void_cells_empty()
        {
            var s = Init(BRIDGE());
            Run(s, DragTo(0, 6, 8));
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            var r = Run(s, DragTo(1, 6, 8));
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
            Assert.AreEqual(0, Find<SegmentCompletedEvent>(r.Ev).Seg);
            Assert.IsTrue(r.Res.Won);
        }

        [Test(Description = "S2 K-34 the build front skips an empty void and offers the cell above it")]
        public void S2_K34_the_build_front_skips_an_empty_void_and_offers_the_cell_above_it()
        {
            var s = Init(BRIDGE());
            CollectionAssert.AreEqual(new[] { Site(6, 0), Site(7, 0) }, Placement.BuildFront(s));
            Run(s, DragTo(0, 6, 8));
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, Placement.BuildFront(s));
        }

        [Test(Description = "S2 K-33 the Golden Trowel places a block over an empty void, never into the void (Faz 2R: blocks, not cells)")]
        public void S2_K33_the_Golden_Trowel_places_a_block_over_an_empty_void_never_into_the_void()
        {
            var s = Init(BRIDGE());
            Run(s, DragTo(0, 6, 8));
            Combo.GrantTrowels(s, 1);
            CollectionAssert.AreEqual(new[] { A(6, 2) }, Boosters.TrowelSpots(s, 1));
            AssertResult(Run(s, new TrowelMove(0, 1, 2)).Res, MoveStatuses.Rejected, BoosterRejects.LegacyTrowel, false, false);
            var r = Run(s, new GoldTrowelMove(1, 6, 2));
            Assert.AreEqual(MoveStatuses.Applied, r.Res.Status);
            Assert.AreEqual(BoosterNames.Trowel, Find<BoosterAppliedEvent>(r.Ev).Booster);
            Assert.AreEqual(0, SiteOcc(s, 0, 1, 1));
            Assert.AreEqual(2, SiteOcc(s, 0, 1, 2));
            ExpectConsistent(s);
        }

        [Test(Description = "S2 E-43 a void holding debris is not filled: no build front in that column, the block above it is wrong (support)")]
        public void S2_E43_a_void_holding_debris_is_not_filled()
        {
            var s = Init(DEBRIS_IN_VOID());
            CollectionAssert.AreEqual(new[] { Site(6, 0) }, Placement.BuildFront(s));
            Run(s, DragTo(0, 6, 8)); // (6,0) W
            CollectionAssert.AreEqual(new[] { Site(6, 1) }, Placement.BuildFront(s));
            var ev = Run(s, DragTo(1, 6, 8)).Ev; // lands on (6,1)–(7,1): right colours, but the void holds debris
            var wrong = Find<PlacementWrongEvent>(ev);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Support), wrong.Reasons);
            CollectionAssert.AreEqual(new[] { Site(7, 0) }, wrong.MissingSupport);
            Assert.AreEqual(VerdictReasons.Support, Find<PieceBouncedEvent>(ev).Reason);

            // the same move without the debris is correct (an empty void counts as filled)
            var clean = Init(DEBRIS_IN_VOID(false));
            Run(clean, DragTo(0, 6, 8));
            CollectionAssert.Contains(Types(Run(clean, DragTo(1, 6, 8)).Ev), "placementCorrect");
        }

        [Test(Description = "S2 K-34 the Pencere board: the lintel railed in before the wall below the window is wrong for support")]
        public void S2_K34_the_Pencere_board_the_lintel_railed_in_before_the_wall_below_the_window_is_wrong_for_support()
        {
            var session = GameSession.Start(Builders.Compiled(PENCERE()));
            session.Commit(DragTo(0, 6, 8)); // a: Y row 0
            var state = session.State;
            // (6,1), (6,2), (7,1) are still empty W cells; the empty window (7,2) counts as filled
            var missing = new[] { Site(6, 1), Site(6, 2), Site(7, 1) };
            var shadow = Gravity.ComputeFall(state, 2, R(0, 6, 3), new FallOptions(null, Registry.LevelHooks(state.Lvl).Fall));
            Assert.IsFalse(shadow.Verdict.Ok);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Support), shadow.Verdict.Reasons);
            CollectionAssert.AreEqual(missing, shadow.Verdict.MissingSupport);
            var sink = new ArraySink();
            session.Commit(DragTo(2, 6, 3, 0), sink);
            var bounced = Find<PieceBouncedEvent>(sink.Events);
            Assert.AreEqual(VerdictReasons.Support, bounced.Reason);
            CollectionAssert.AreEqual(missing, bounced.MissingSupport);
            Assert.AreEqual(YardAt(4, 3), bounced.To);
        }

        [Test(Description = "S2 the Pencere board solution: the lintel goes through the gap above the window (4 moves, 8 left, YAO 3/4)")]
        public void S2_the_Pencere_board_solution()
        {
            var session = GameSession.Start(Builders.Compiled(PENCERE()));
            var moves = new[] { DragTo(0, 6, 8), DragTo(1, 6, 8), DragTo(2, 6, 3, 0), DragTo(3, 6, 8) };
            var results = moves.Select(m => session.Commit(m)).ToArray();
            CollectionAssert.AreEqual(Enumerable.Repeat(MoveStatuses.Applied, 4).ToArray(), results.Select(r => r.Status).ToArray());
            Assert.AreEqual(BoosterOutcomes.Won, session.Outcome);
            Assert.AreEqual(8, session.MovesLeft);
            Assert.AreEqual(new YaoMeasure(3, 1, 0.75), session.Yao());
            Assert.AreEqual(0, SiteOcc(session.State, 0, 1, 2)); // the window (7,2) stays open
            Assert.AreEqual(0, Hdr(session.State, H.WrongCount));
        }
    }
}
