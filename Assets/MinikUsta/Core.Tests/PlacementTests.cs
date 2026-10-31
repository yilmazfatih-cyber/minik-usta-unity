#nullable enable
// Port of tests/core/placement.test.ts (K-14…K-17, K-33, K-34, Y8 lifecycle).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class PlacementTests
    {
        /// <summary>S3-like landing hook for tests (the real one is the obstacle plugin): glass breaks when d &gt; threshold.</summary>
        private static readonly FallRules GlassRules = new FallRules(onLanded: (s, id, fall) =>
            HasFlag(s, id, FlagBit.Glass) && fall.Distance > s.Lvl.Gravity.GlassThreshold ? LandingEffect.Break(1) : LandingEffect.None);

        /// <summary>Y8-like placement hook for tests: mortar blocks stick on a wrong placement.</summary>
        private static readonly PlacementRules MortarRules = new PlacementRules((s, id, cells, v) =>
            HasFlag(s, id, FlagBit.Mortar) ? PlacementOverrides.Stick : PlacementOverrides.Default);

        private static FallOptions Opts(FallRules rules) => new FallOptions(null, rules);

        // --- K-16 ------------------------------------------------------------------------------------------------------------

        private static LevelSpec K16Spec() => new LevelSpec
        {
            Plan = WW(2),
            Pieces = Ps(Pc("O4_0", "W", 0, 0), Pc("D2_0", "W", 2, 0), Pc("D2_0", "W", 3, 0), Pc("D2_0", "Y", 4, 0)),
        };

        [Test(Description = "K-16 GDD example: O4 W on WW/WW is correct; two D2_0 W are both correct; D2_0 Y on W is wrong")]
        public void K16_GDD_example_O4_W_on_WW_WW_is_correct_two_D2_0_W_are_both_correct_D2_0_Y_on_W_is_wrong()
        {
            var s = Init(K16Spec());
            Assert.AreEqual(OkV, Placement.IsCorrectPlacement(s, 0, CellsOf("O4_0", 6, 0)));
            var first = Placement.IsCorrectPlacement(s, 1, CellsOf("D2_0", 6, 0));
            Assert.IsTrue(first.Ok);
            Assert.AreEqual(PlacementOutcome.KindCorrect, Placement.SettlePlacement(s, 1, A(6, 0), first).Kind);
            Assert.IsTrue(Placement.IsCorrectPlacement(s, 2, CellsOf("D2_0", 7, 0)).Ok);
            var t = Init(K16Spec());
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Color)), Placement.IsCorrectPlacement(t, 3, CellsOf("D2_0", 6, 0)));
        }

        [Test(Description = "K-16 condition 1: a cell outside the plan area or in the yard is wrong (outside)")]
        public void K16_condition_1_a_cell_outside_the_plan_area_or_in_the_yard_is_wrong()
        {
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("O4_0", "W", 0, 0), Pc("B1_0", "W", 2, 0)) });
            ToSite(s, 0, 6, 0);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Outside), Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 6, 2)).Reasons);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Outside), Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 5, 2)).Reasons);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Outside), Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 7, 9)).Reasons);
        }

        [Test(Description = "K-34 hook 2 debris and window never produced: Faz 2R debris is a material block, correct on its own colour (S4, K-16 (2) removed)")]
        public void K34_hook_2_debris_and_window_never_produced_Faz_2R_debris_is_a_material_block()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "W", 6, 0) } });
            Assert.AreEqual(OkV, Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 7, 0)));
            var r = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "R", 6, 0) } });
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color), Placement.IsCorrectPlacement(r, 1, CellsOf("B1_0", 7, 0)).Reasons);
            // a Faz 2R plan has no `.` (K-15): over every site cell, neither reason ever shows up
            var full = Init(new LevelSpec { Plan = new[] { "WY", "YW", "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "R", 6, 2) } });
            foreach (int id in new[] { 0, 1 })
                for (int x = 6; x <= 7; x++)
                    for (int y = 0; y < 10; y++)
                    {
                        var reasons = Placement.IsCorrectPlacement(full, id, CellsOf("B1_0", x, y)).Reasons;
                        Assert.IsFalse(reasons.Contains(VerdictReasons.Debris) || reasons.Contains(VerdictReasons.Window), id + " (" + x + "," + y + ")");
                    }
        }

        [Test(Description = "K-16 a hidden `?` cell is checked against its resolved colour; the correct block opens it (K-32)")]
        public void K16_a_hidden_cell_is_checked_against_its_resolved_colour_the_correct_block_opens_it()
        {
            var s = Init(new LevelSpec
            {
                Plan = new[] { "??", "WY" },
                Hidden = Repeat1,
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "Y", 3, 0)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color), Placement.IsCorrectPlacement(s, 3, CellsOf("B1_0", 6, 1)).Reasons);
            var v = Placement.IsCorrectPlacement(s, 2, CellsOf("B1_0", 6, 1));
            Assert.IsTrue(v.Ok);
            var o = Placement.SettlePlacement(s, 2, A(6, 1), v);
            Assert.AreEqual(PlacementOutcome.KindCorrect, o.Kind);
            CollectionAssert.AreEqual(new[] { new RevealedCell(6, 1, "W") }, o.Revealed);
            ExpectConsistent(s);
        }

        // --- K-34 ------------------------------------------------------------------------------------------------------------

        [Test(Description = "K-34 example 1 and example 3: O4 over an empty column is wrong (support); verdict and buildFront")]
        public void K34_example_1_and_example_3_O4_over_an_empty_column_is_wrong_verdict_and_buildFront()
        {
            var s = Init(new LevelSpec { Plan = WW(4), Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("O4_0", "W", 2, 0)) });
            ToSite(s, 0, 6, 0);
            Assert.IsTrue(Drag(s, 1).IsReachable(N(6, 8)));
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 2), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 0), Site(7, 1)), fall.Verdict);
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 0) }, Placement.BuildFront(s));
        }

        [Test(Description = "K-34 example 2: rail over an empty `.` cell is correct (empty dot cells count as filled)")]
        public void K34_example_2_rail_over_an_empty_dot_cell_is_correct()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(4, Gap(2, 1)),
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("D2_90", "W", 4, 2)),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 1);
            var d = Drag(s, 2);
            Assert.AreEqual(DropKinds.SiteRail, d.Classify(R(0, 6, 2)).Kind);
            var fall = Gravity.ComputeFall(s, 2, R(0, 6, 2));
            Assert.AreEqual(FallResult.ModeRail, fall.Mode);
            Assert.AreEqual(A(6, 2), fall.Landing);
            Assert.IsTrue(fall.Verdict.Ok);
        }

        [Test(Description = "K-34 rail over an empty coloured cell is wrong (a Y lintel through a gap at rows 2–3 before the W base)")]
        public void K34_rail_over_an_empty_coloured_cell_is_wrong()
        {
            // the Faz 2R levels 1–10 have their gaps at row 0 (rail = floor); a builder board keeps the Faz 2 case
            var s = Init(new LevelSpec
            {
                Wall = Wall(8, Gap(2, 2)),
                Plan = new[] { "YY", "WW", "WW" },
                Pieces = Ps(Pc("O4_0", "W", 0, 0), Pc("D2_90", "Y", 4, 2)),
            });
            var d = Drag(s, 1);
            Assert.AreEqual(DropKinds.SiteRail, d.Classify(R(0, 6, 2)).Kind);
            var fall = Gravity.ComputeFall(s, 1, R(0, 6, 2));
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(6, 0), Site(6, 1), Site(7, 0), Site(7, 1)), fall.Verdict);
            var o = Placement.SettlePlacement(s, 1, fall.Landing, fall.Verdict);
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(VerdictReasons.Support, o.Reason);
            Assert.AreEqual(new ReturnTarget(1, YardP(4, 2), null), o.Target);
            ExpectConsistent(s);
        }

        [Test(Description = "K-34 debris below blocks correct placement")]
        public void K34_debris_below_blocks_correct_placement()
        {
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "W", 7, 0) } });
            var fall = Gravity.ComputeFall(s, 0, N(7, 8));
            Assert.AreEqual(A(7, 1), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 0)), fall.Verdict);
            CollectionAssert.AreEqual(new[] { Site(6, 0) }, Placement.BuildFront(s));
        }

        private static LevelSpec DotSpec() => new LevelSpec
        {
            Plan = new[] { "WW", "W.", "WW" },
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "W", 4, 0), Pc("B1_0", "R", 5, 0, new[] { "mortar" })),
        };

        [Test(Description = "K-34 dot cell with debris is missing support")]
        public void K34_dot_cell_with_debris_is_missing_support()
        {
            var spec = DotSpec();
            spec.Debris = new[] { new D("B1_0", "R", 7, 1) };
            var s = Init(spec);
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 1);
            var fall = Gravity.ComputeFall(s, 2, N(7, 8));
            Assert.AreEqual(A(7, 2), fall.Landing);
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 1)), fall.Verdict);
            CollectionAssert.AreEqual(new[] { Site(6, 2) }, Placement.BuildFront(s));
        }

        [Test(Description = "E-43 dot cell with stuck mortar breaks support; buildFront returns after the mortar is removed")]
        public void E43_dot_cell_with_stuck_mortar_breaks_support_buildFront_returns_after_the_mortar_is_removed()
        {
            var s = Init(DotSpec());
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 1);
            ToSite(s, 3, 7, 1, locked: false, stuck: true);
            Assert.AreEqual(0b10, WrongOccMask(s, 0, 1));
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Support), Site(7, 1)), Placement.IsCorrectPlacement(s, 2, CellsOf("B1_0", 7, 2)));
            CollectionAssert.AreEqual(new[] { Site(6, 2) }, Placement.BuildFront(s));
            // the mortar is dragged to the yard: the `.` cell is empty again, (7,2) becomes a front cell the same move
            Placement.MovePiece(s, 3, Placement.YardPlace(A(3, 0)));
            Assert.IsFalse(HasFlag(s, 3, FlagBit.Stuck));
            Assert.AreEqual(0, WrongOccMask(s, 0, 1));
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, Placement.BuildFront(s));
            Assert.IsTrue(Placement.IsCorrectPlacement(s, 2, CellsOf("B1_0", 7, 2)).Ok);
            ExpectConsistent(s);
        }

        [Test(Description = "K-34 the moving block does not count as a wrong object below itself")]
        public void K34_the_moving_block_does_not_count_as_a_wrong_object_below_itself()
        {
            var spec = DotSpec();
            spec.Pieces = spec.Pieces!.Take(3).ToArray().Plus(Pc("B1_0", "W", 5, 0, new[] { "mortar" }));
            var s = Init(spec);
            ToSite(s, 0, 6, 0);
            ToSite(s, 3, 7, 1, locked: false, stuck: true);
            Assert.IsTrue(Placement.IsCorrectPlacement(s, 3, CellsOf("B1_0", 7, 2)).Ok);
            CollectionAssert.AreEqual(Missing(Site(7, 1)), Placement.IsCorrectPlacement(s, 2, CellsOf("B1_0", 7, 2)).MissingSupport);
        }

        [Test(Description = "K-34 verdict reasons keep fixed order: outside, window, colour, support (Faz 2R: debris never produced)")]
        public void K34_verdict_reasons_keep_fixed_order_outside_window_colour_support()
        {
            var s = Init(new LevelSpec { Plan = new[] { "R.", "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("O4_0", "W", 6, 0) } });
            Assert.AreEqual(Wrong(Reasons(VerdictReasons.Outside, VerdictReasons.Window, VerdictReasons.Color, VerdictReasons.Support), Site(6, 0), Site(7, 0)),
                Placement.IsCorrectPlacement(s, 1, CellsOf("O4_0", 6, 1)));
        }

        [Test(Description = "K-34 crane and trowel obey support rule")]
        public void K34_crane_and_trowel_obey_support_rule()
        {
            var s = Init(new LevelSpec { Plan = WW(3), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0)) });
            ToSite(s, 0, 6, 0);
            // crane target (K-37): any position, no fall; the same single test decides
            CollectionAssert.AreEqual(Missing(Site(6, 1)), Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 6, 2)).MissingSupport);
            CollectionAssert.AreEqual(Missing(Site(7, 0)), Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 7, 1)).MissingSupport);
            Assert.IsTrue(Placement.IsCorrectPlacement(s, 1, CellsOf("B1_0", 6, 1)).Ok);
            // trowel (K-33): only build-front cells
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 0) }, Placement.EligibleTrowelCells(s));
            CollectionAssert.AreEqual(Placement.BuildFront(s), Placement.EligibleTrowelCells(s));
        }

        [Test(Description = "K-33 trowel cells equal buildFront (GDD K-33 example: (7,2) above an empty `.`, not (7,3))")]
        public void K33_trowel_cells_equal_buildFront()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW", "WW", "W.", "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 1, 0)) });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 2) }, Placement.BuildFront(s));
            CollectionAssert.AreEqual(Placement.BuildFront(s), Placement.EligibleTrowelCells(s));
            // a trowel cell is correctly filled: the front moves up
            SetSiteOcc(s, 0, 1, 2, SiteTrowel);
            Grid.RefreshSiteMasks(s, 0);
            CollectionAssert.AreEqual(new[] { Site(6, 1), Site(7, 3) }, Placement.BuildFront(s));
        }

        [Test(Description = "K-34 the front follows the elevator frame (board row = plan row + offset)")]
        public void K34_the_front_follows_the_elevator_frame()
        {
            var s = Init(new LevelSpec { Id = 37, Plan = WW(2), Elevator = Elevator(0, 2, 2, 1), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            Assert.AreEqual(2, Hdr(s, H.Elev));
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, Placement.BuildFront(s));
            Assert.IsTrue(Placement.IsCorrectPlacement(s, 0, CellsOf("B1_0", 6, 2)).Ok);
            CollectionAssert.AreEqual(Missing(Site(6, 2)), Placement.IsCorrectPlacement(s, 0, CellsOf("B1_0", 6, 3)).MissingSupport);
        }

        // --- K-34 hook 2 reason cells ----------------------------------------------------------------------------------------

        [Test(Description = "K-34 hook 2 reasonCells names the cells behind each verdict reason: color, window, outside, debris; support has none")]
        public void K34_hook_2_reasonCells_names_the_cells_behind_each_verdict_reason()
        {
            // plan rows top → bottom: row 2 = Y Y / row 1 = Y . / row 0 = W W
            var s = Init(new LevelSpec { Plan = new[] { "YY", "Y.", "WW" }, Pieces = Ps(Pc("D2_90", "W", 0, 0)) });
            var row1 = CellsOf("D2_90", 6, 1);
            var v = Placement.IsCorrectPlacement(s, 0, row1);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Window, VerdictReasons.Color), v.Reasons.Take(2).ToArray());
            CollectionAssert.AreEqual(new[] { C(7, 1) }, Placement.ReasonCells(s, 0, row1, VerdictReasons.Window));
            CollectionAssert.AreEqual(new[] { C(6, 1) }, Placement.ReasonCells(s, 0, row1, VerdictReasons.Color));
            CollectionAssert.IsEmpty(Placement.ReasonCells(s, 0, row1, VerdictReasons.Support));
            var above = CellsOf("D2_90", 6, 3);
            Assert.AreEqual(VerdictReasons.Outside, Placement.IsCorrectPlacement(s, 0, above).Reasons[0]);
            CollectionAssert.AreEqual(new[] { C(6, 3), C(7, 3) }, Placement.ReasonCells(s, 0, above, VerdictReasons.Outside));
            CollectionAssert.IsEmpty(Placement.ReasonCells(s, 0, CellsOf("D2_90", 6, 0), VerdictReasons.Color)); // W on W W
            CollectionAssert.AreEqual(row1, Placement.ReasonCells(s, 0, row1, VerdictReasons.Debris));
        }

        // --- K-15 ------------------------------------------------------------------------------------------------------------

        private static LevelSpec K15Spec() => new LevelSpec
        {
            Plan = new[] { "W.", "WW" },
            Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "R", 3, 0, new[] { "mortar" })),
        };

        [Test(Description = "K-15 a segment completes when every non-`.` cell is filled and nothing else is in it")]
        public void K15_a_segment_completes_when_every_non_dot_cell_is_filled_and_nothing_else_is_in_it()
        {
            var s = Init(K15Spec());
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            ToSite(s, 0, 6, 0);
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            ToSite(s, 1, 6, 1);
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
        }

        [Test(Description = "E-24 a block stuck on a `.` cell keeps the segment incomplete")]
        public void E24_a_block_stuck_on_a_dot_cell_keeps_the_segment_incomplete()
        {
            var s = Init(K15Spec());
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 6, 1);
            ToSite(s, 2, 7, 1, locked: false, stuck: true);
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
        }

        [Test(Description = "K-15 debris anywhere in the segment area blocks completion; trowel cells count as filled")]
        public void K15_debris_anywhere_in_the_segment_area_blocks_completion_trowel_cells_count_as_filled()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("B1_0", "R", 6, 3) } });
            ToSite(s, 0, 6, 0);
            SetSiteOcc(s, 0, 1, 0, SiteTrowel);
            Grid.RefreshSiteMasks(s, 0);
            Assert.IsFalse(Placement.IsSegmentComplete(s, 0));
            Placement.MovePiece(s, 1, Placement.YardPlace(A(0, 0)));
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
        }

        // --- K-14 ------------------------------------------------------------------------------------------------------------

        [Test(Description = "K-14 a correctly placed block locks: it cannot be picked and fills its plan cells")]
        public void K14_a_correctly_placed_block_locks_it_cannot_be_picked_and_fills_its_plan_cells()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("D2_90", "W", 0, 0)) });
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            var o = Placement.SettlePlacement(s, 0, fall.Landing, fall.Verdict);
            Assert.AreEqual(PlacementOutcome.KindCorrect, o.Kind);
            Assert.AreEqual(SiteP(6, 0), o.At);
            CollectionAssert.AreEqual(new[] { Site(6, 0), Site(7, 0) }, o.Cells);
            CollectionAssert.IsEmpty(o.Revealed!);
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.Locked);
            CollectionAssert.AreEqual(new[] { 1, 1 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            Assert.IsTrue(Placement.IsSegmentComplete(s, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-14 locking a stuck block clears stuck and wrongOcc (paint lock, K-38)")]
        public void K14_locking_a_stuck_block_clears_stuck_and_wrongOcc()
        {
            var s = Init(new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "mortar" })) });
            ToSite(s, 0, 6, 0, locked: false, stuck: true);
            Assert.AreEqual(1, WrongOccMask(s, 0, 0));
            CollectionAssert.IsEmpty(Placement.LockPiece(s, 0));
            Assert.IsFalse(HasFlag(s, 0, FlagBit.Stuck));
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            CollectionAssert.AreEqual(new[] { 0, 1 }, new[] { WrongOccMask(s, 0, 0), FilledMask(s, 0, 0) });
            ExpectConsistent(s);
        }

        // --- K-17 ------------------------------------------------------------------------------------------------------------

        [Test(Description = "K-17 GDD example: B1 Y from (3,7) lands on a W cell and returns to (3,7)")]
        public void K17_GDD_example_B1_Y_from_3_7_lands_on_a_W_cell_and_returns_to_3_7()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "Y", 3, 7)) });
            var start = Placement.PiecePlace(s, 0);
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color), fall.Verdict.Reasons);
            var o = Placement.SettlePlacement(s, 0, fall.Landing, fall.Verdict, new SettleOptions(start: start));
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(A(6, 0), o.Landing);
            Assert.AreEqual(new ReturnTarget(1, YardP(3, 7), null), o.Target);
            Assert.AreEqual(VerdictReasons.Color, o.Reason);
            CollectionAssert.IsEmpty(o.MissingSupport!);
            Assert.AreEqual(1, YardOcc(s, 3, 7));
            Assert.AreEqual(0, SiteOcc(s, 0, 0, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 GDD example: debris from (7,0)–(7,1) dropped elsewhere returns to (7,0)–(7,1)")]
        public void K17_GDD_example_debris_from_7_0_7_1_dropped_elsewhere_returns_to_7_0_7_1()
        {
            var s = Init(new LevelSpec { Plan = WW(3), Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("D2_0", "R", 7, 0) } });
            var d = Drag(s, 1);
            Assert.IsTrue(d.IsReachable(N(6, 8)));
            var fall = Gravity.ComputeFall(s, 1, N(6, 8));
            Assert.AreEqual(A(6, 0), fall.Landing);
            Assert.AreEqual(VerdictReasons.Color, fall.Verdict.Reasons[0]);
            var o = Placement.SettlePlacement(s, 1, fall.Landing, fall.Verdict);
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(SiteP(7, 0), o.Target!.To);
            Assert.AreEqual(0b11, WrongOccMask(s, 0, 1));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 step 2 order: nearest column to the start x first, ties nearer the wall")]
        public void K17_step_2_order_nearest_column_to_the_start_x_first_ties_nearer_the_wall()
        {
            var geo = Geometry.DefaultGeo;
            CollectionAssert.AreEqual(new[] { 2, 3, 1, 4, 0, 5 }, Placement.NearestColumnsFirst(geo, 2, 1));
            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1, 0 }, Placement.NearestColumnsFirst(geo, 6, 1));
            CollectionAssert.AreEqual(new[] { 4, 3, 2, 1, 0 }, Placement.NearestColumnsFirst(geo, 7, 2));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, Placement.NearestColumnsFirst(geo, 0, 2));
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, Placement.NearestColumnsFirst(geo, 3, 3));
        }

        [Test(Description = "K-17 step 2: blocked start cells → drop over the yard into the nearest column that fits")]
        public void K17_step_2_blocked_start_cells_drop_over_the_yard_into_the_nearest_column_that_fits()
        {
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("I4_0", "W", 2, 0), Pc("I4_0", "W", 2, 4), Pc("I4_0", "W", 3, 0), Pc("I4_0", "W", 3, 4), Pc("I4_0", "W", 1, 0), Pc("B1_0", "Y", 5, 0)),
            });
            // start (2,7) is occupied by another block: columns 2 and 3 are full, column 1 lands on (1,4)
            var target = Placement.ReturnTarget(s, 5, new ReturnOptions(start: Placement.YardPlace(A(2, 7))));
            Assert.AreEqual(new ReturnTarget(2, YardP(1, 4), A(1, 9)), target);
        }

        [Test(Description = "K-17 step 3 and K-26: no yard column fits → end of the truck queue, after older queued blocks")]
        public void K17_step_3_and_K26_no_yard_column_fits_end_of_the_truck_queue_after_older_queued_blocks()
        {
            var full = new List<P>();
            for (int x = 0; x < 6; x++)
            {
                full.Add(Pc("I4_0", "W", x, 0));
                full.Add(Pc("I4_0", "W", x, 4));
            }
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = full.ToArray(),
                Batches = new[] { new BatchSpec { ForSegment = 0, Pieces = Ps(Pc("B1_0", "G", 0, 8)) } },
                Debris = new[] { new D("B1_0", "R", 6, 0) },
            });
            EnqueuePiece(s, 12);
            var target = Placement.ReturnTarget(s, 13, new ReturnOptions(skipStart: true));
            Assert.AreEqual(new ReturnTarget(3, QueueP(5, 8), null), target);
            Placement.MovePiece(s, 13, target.To);
            CollectionAssert.AreEqual(new[] { 12, 13 }, QueueIds(s));
            Assert.AreEqual(0, SiteOcc(s, 0, 0, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 GDD example: a stuck mortar block dropped off the plan returns to its start and stays stuck")]
        public void K17_GDD_example_a_stuck_mortar_block_dropped_off_the_plan_returns_to_its_start_and_stays_stuck()
        {
            // plan height 3; column 7 rows 0–2 filled, (6,0)–(6,1) filled, B1 R mortar stuck at (6,2)
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Plan = WW(3),
                Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("B1_0", "R", 2, 0, new[] { "mortar" })),
            });
            ToSite(s, 0, 7, 0);
            ToSite(s, 1, 6, 0);
            ToSite(s, 2, 6, 2, locked: false, stuck: true);
            var start = Placement.PiecePlace(s, 2);
            Assert.IsTrue(Drag(s, 2).IsReachable(N(7, 8)));
            var fall = Gravity.ComputeFall(s, 2, N(7, 8));
            Assert.AreEqual(A(7, 3), fall.Landing);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Outside), fall.Verdict.Reasons);
            // the mortar hook asks to stick, but a cell is outside the plan area: normal bounce, step 1 (E-08)
            Assert.IsFalse(Placement.AllCellsInPlanArea(s, fall.Cells));
            var o = Placement.SettlePlacement(s, 2, fall.Landing, fall.Verdict, new SettleOptions(start, MortarRules));
            Assert.AreEqual(PlacementOutcome.KindBounced, o.Kind);
            Assert.AreEqual(new ReturnTarget(1, start, null), o.Target);
            Assert.AreEqual(SiteP(6, 2), Placement.PiecePlace(s, 2));
            Assert.IsTrue(HasFlag(s, 2, FlagBit.Stuck));
            Assert.AreEqual(0b100, WrongOccMask(s, 0, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 broken stuck glass mortar returns to yard column 5 and unsticks")]
        public void K17_broken_stuck_glass_mortar_returns_to_yard_column_5_and_unsticks()
        {
            // GDD K-17 example: normal threshold 3; stuck glass mortar B1 at (6,2) released at (7,8) → lands (7,3), d = 5
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Plan = WW(3),
                Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("B1_0", "R", 2, 0, new[] { "mortar", "glass" }), Pc("B1_0", "G", 5, 0)),
            });
            ToSite(s, 0, 7, 0);
            ToSite(s, 1, 6, 0);
            ToSite(s, 2, 6, 2, locked: false, stuck: true);
            var start = Placement.PiecePlace(s, 2);
            var fall = Gravity.ComputeFall(s, 2, N(7, 8), Opts(GlassRules));
            Assert.AreEqual(5, fall.Distance);
            Assert.AreEqual(LandingEffect.Break(1), fall.Effect);
            var target = Placement.ReturnBrokenPiece(s, 2, start);
            Assert.AreEqual(new ReturnTarget(2, YardP(5, 1), A(5, 9)), target);
            Assert.AreEqual(0, SiteOcc(s, 0, 0, 2));
            Assert.IsFalse(HasFlag(s, 2, FlagBit.Stuck));
            Assert.AreEqual(0, WrongOccMask(s, 0, 0));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 a broken glass block that started in the yard returns to its start cells (step 1)")]
        public void K17_a_broken_glass_block_that_started_in_the_yard_returns_to_its_start_cells()
        {
            var s = Init(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 2, 7, new[] { "glass" })) });
            var fall = Gravity.ComputeFall(s, 0, N(6, 8), Opts(GlassRules));
            Assert.AreEqual(LandingEffect.KindBreak, fall.Effect.Kind);
            Assert.AreEqual(YardP(2, 7), Placement.ReturnBrokenPiece(s, 0).To);
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 a stuck mortar block dropped wrong inside the plan sticks again at the new cell (Y8 hook)")]
        public void K17_a_stuck_mortar_block_dropped_wrong_inside_the_plan_sticks_again_at_the_new_cell()
        {
            var s = Init(new LevelSpec
            {
                Plan = WW(3),
                Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("B1_0", "W", 1, 0), Pc("B1_0", "R", 2, 0, new[] { "mortar" })),
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 0);
            ToSite(s, 2, 6, 2, locked: false, stuck: true);
            var fall = Gravity.ComputeFall(s, 2, N(7, 8));
            Assert.AreEqual(A(7, 1), fall.Landing);
            CollectionAssert.AreEqual(Reasons(VerdictReasons.Color), fall.Verdict.Reasons);
            var o = Placement.SettlePlacement(s, 2, fall.Landing, fall.Verdict, new SettleOptions(rules: MortarRules));
            Assert.AreEqual(PlacementOutcome.KindStuck, o.Kind);
            Assert.AreEqual(SiteP(7, 1), o.At);
            Assert.AreEqual(VerdictReasons.Color, o.Reason);
            CollectionAssert.IsEmpty(o.MissingSupport!);
            CollectionAssert.AreEqual(new[] { 0, 0b10 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            Assert.IsTrue(HasFlag(s, 2, FlagBit.Stuck));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 a fresh mortar block sticks on a wrong in-plan landing; without the hook it bounces")]
        public void K17_a_fresh_mortar_block_sticks_on_a_wrong_in_plan_landing_without_the_hook_it_bounces()
        {
            LevelSpec Spec() => new LevelSpec { Plan = WW(2), Pieces = Ps(Pc("B1_0", "R", 0, 0, new[] { "mortar" })) };
            var s = Init(Spec());
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.AreEqual(PlacementOutcome.KindStuck, Placement.SettlePlacement(s, 0, fall.Landing, fall.Verdict, new SettleOptions(rules: MortarRules)).Kind);
            Assert.AreEqual(1, WrongOccMask(s, 0, 0));
            ExpectConsistent(s);
            var t = Init(Spec());
            Assert.AreEqual(PlacementOutcome.KindBounced, Placement.SettlePlacement(t, 0, fall.Landing, fall.Verdict).Kind);
        }

        // --- Y8 stuck lifecycle / state upkeep ---------------------------------------------------------------------------------

        [Test(Description = "K-17 leaving the site (yard, queue, gone) clears stuck and its wrongOcc bits")]
        public void K17_leaving_the_site_clears_stuck_and_its_wrongOcc_bits()
        {
            var s = Init(new LevelSpec
            {
                Plan = WW(2),
                Pieces = Ps(Pc("B1_0", "R", 0, 0, new[] { "mortar" }), Pc("B1_0", "R", 1, 0, new[] { "mortar" }), Pc("B1_0", "R", 2, 0, new[] { "mortar" })),
            });
            ToSite(s, 0, 6, 0, locked: false, stuck: true);
            ToSite(s, 1, 7, 0, locked: false, stuck: true);
            ToSite(s, 2, 6, 1, locked: false, stuck: true);
            Placement.MovePiece(s, 0, Placement.YardPlace(A(4, 0))); // OBSTACLES Y8 example: dragged back to the yard
            Placement.MovePiece(s, 1, QueueP(5, 8));
            Placement.MovePiece(s, 2, PiecePlace.Gone(6, 1)); // hammer
            foreach (int id in new[] { 0, 1, 2 }) Assert.IsFalse(HasFlag(s, id, FlagBit.Stuck));
            CollectionAssert.AreEqual(new[] { 0, 0 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            CollectionAssert.AreEqual(new[] { 1 }, QueueIds(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-17 stickPiece marks the plan-area cells only; movePiece rejects cells outside the yard or site")]
        public void K17_stickPiece_marks_the_plan_area_cells_only_movePiece_rejects_cells_outside_the_yard_or_site()
        {
            var s = Init(new LevelSpec { Plan = new[] { "W." }, Pieces = Ps(Pc("D2_0", "R", 0, 0, new[] { "mortar" })) });
            Placement.MovePiece(s, 0, Placement.SitePlace(s, A(7, 0)));
            Placement.StickPiece(s, 0);
            Assert.AreEqual(0b1, WrongOccMask(s, 0, 1));
            ExpectConsistent(s);
            Assert.Throws<ArgumentOutOfRangeException>(() => Placement.MovePiece(s, 0, SiteP(7, 7)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Placement.MovePiece(s, 0, YardP(5, 7)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Placement.MovePiece(s, 0, SiteP(5, 0)));
        }

        [Test(Description = "K-17 places map to event positions in board coordinates")]
        public void K17_places_map_to_event_positions_in_board_coordinates()
        {
            var s = Init(new LevelSpec { Id = 37, Plan = WW(2), Elevator = Elevator(0, 2, 1, 1), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            Assert.AreEqual(SiteP(7, 2), Placement.SitePlace(s, A(7, 3)));
            Assert.AreEqual(Site(7, 3), Placement.PlaceAt(s, SiteP(7, 2)));
            Assert.AreEqual(YardAt(1, 2), Placement.PlaceAt(s, Placement.YardPlace(A(1, 2))));
            Assert.IsNull(Placement.PlaceAt(s, QueueP(5, 8)));
        }

        [Test(Description = "K-17 drop over the yard ignores the yard gravity setting and lands on the first support (K-25 example)")]
        public void K17_drop_over_the_yard_ignores_the_yard_gravity_setting_and_lands_on_the_first_support()
        {
            // GDD K-25 example: columns 4–5 top filled row 3 → O4 at (4,4); column 0 full → B1 goes to x = 1, (1,6)
            var s = Init(new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("I4_0", "W", 4, 0), Pc("I4_0", "W", 5, 0), Pc("I4_0", "W", 0, 0), Pc("I4_0", "W", 0, 4), Pc("I4_0", "W", 1, 0), Pc("D2_0", "W", 1, 4), Pc("O4_0", "G", 2, 0)),
            });
            var geo = Geometry.DefaultGeo;
            Assert.AreEqual(4, Placement.DropIntoYard(s, Shapes.ShapeById("O4_0"), 4));
            Assert.AreEqual(-1, Placement.DropIntoYard(s, Shapes.ShapeById("B1_0"), 0));
            Assert.AreEqual(1, Placement.NearestColumnsFirst(geo, 0, 1).First(x => Placement.DropIntoYard(s, Shapes.ShapeById("B1_0"), x) >= 0));
            Assert.AreEqual(6, Placement.DropIntoYard(s, Shapes.ShapeById("B1_0"), 1));
            // the moving piece itself is ignored; invalid columns give −1
            Assert.AreEqual(2, Placement.DropIntoYard(s, Shapes.ShapeById("O4_0"), 2));
            Assert.AreEqual(0, Placement.DropIntoYard(s, Shapes.ShapeById("O4_0"), 2, 6));
            Assert.AreEqual(-1, Placement.DropIntoYard(s, Shapes.ShapeById("O4_0"), 5));
        }
    }
}
