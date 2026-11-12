#nullable enable
// Port of tests/core/movement.test.ts. "K-12 dragEnteredRail … (Bölüm 4 gapPass)" replays the canonical moves through
// `Moves.ApplyMove` (`Run`, P3); the Bölüm 1–10 start-position test reads the canonical first move straight from the golden
// `log`.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class MovementTests
    {
        // --- K-04 wall boundary ------------------------------------------------------------------------------------------

        [Test(Description = "K-04 closed boundary rows block a FREE crossing; rows at or above the wall height are open")]
        public void K04_closed_boundary_rows_block_a_FREE_crossing_rows_at_or_above_the_wall_height_are_open()
        {
            var s = Init(new LevelSpec { Wall = Wall(6), Pieces = Ps(Pc("B1_0", "W", 5, 2)) });
            var d = Drag(s, 0);
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(5, 2))), "F(6,2)");
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(5, 5))), "F(6,5)");
            CollectionAssert.Contains(Keys(d.Neighbours(N(5, 6))), "F(6,6)");
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(6, 2))), "F(5,2)");
            // (6,2) is still reachable: over the wall at row 6, then down the empty site column
            Assert.AreEqual(4 + 1 + 4, d.DistanceFromStart(N(6, 2)));
        }

        [Test(Description = "K-04 crane rows 8–9 are always open, even above an 8-high wall")]
        public void K04_crane_rows_8_9_are_always_open_even_above_an_8_high_wall()
        {
            var s = Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc("B1_0", "W", 5, 0)) });
            var d = Drag(s, 0);
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(5, 7))), "F(6,7)");
            CollectionAssert.Contains(Keys(d.Neighbours(N(5, 8))), "F(6,8)");
            CollectionAssert.Contains(Keys(d.Neighbours(N(5, 9))), "F(6,9)");
            Assert.IsTrue(d.IsReachable(N(6, 0)));
        }

        [Test(Description = "K-04 an open gap opens its rows only to RAIL mode; a closed gap opens nothing")]
        public void K04_an_open_gap_opens_its_rows_only_to_RAIL_mode_a_closed_gap_opens_nothing()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(6, Gap(2, 2), LockedGap(0, 1, "a")),
                Pieces = Ps(Pc("B1_0", "W", 5, 2), Pc("B1_0", "W", 5, 0)),
            });
            var open = Drag(s, 0);
            CollectionAssert.Contains(Keys(open.Neighbours(N(5, 2))), "R0(6,2)");
            CollectionAssert.DoesNotContain(Keys(open.Neighbours(N(5, 2))), "F(6,2)");
            var closed = Drag(s, 1);
            CollectionAssert.IsEmpty(Keys(closed.Neighbours(N(5, 0))).Where(k => k.Contains("6,0")).ToArray());
            Assert.IsFalse(closed.IsReachable(R(1, 6, 0)));
            // the rule hook decides alone: a K-40-like hook opens the locked gap
            var k40 = new DragRules(canPassGap: (st, g, id) => true);
            Assert.IsTrue(Drag(s, 1, k40).IsReachable(R(1, 6, 0)));
        }

        // --- K-05 crane area ---------------------------------------------------------------------------------------------

        [Test(Description = "K-05 3-tall piece cannot clear an 8-high wall; at height 7 it can (GDD example)")]
        public void K05_3_tall_piece_cannot_clear_an_8_high_wall_at_height_7_it_can()
        {
            var at8 = Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc("I3_0", "W", 5, 0)) }), 0);
            CollectionAssert.IsEmpty(at8.ReachableNodes().Where(n => n.Ix >= 6).ToArray());
            Assert.IsTrue(at8.IsReachable(N(5, 7))); // cells 7–9: the highest anchor
            var at7 = Drag(Init(new LevelSpec { Wall = Wall(7), Pieces = Ps(Pc("I3_0", "W", 5, 0)) }), 0);
            CollectionAssert.Contains(Keys(at7.Neighbours(N(5, 7))), "F(6,7)");
            Assert.IsTrue(at7.IsReachable(N(6, 0)));
        }

        [Test(Description = "K-05 at height 8 only blocks at most 2 tall clear the wall (B1, D2, O4, C3); 3–4 tall ones do not")]
        public void K05_at_height_8_only_blocks_at_most_2_tall_clear_the_wall()
        {
            bool Crosses(string shape) => Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc(shape, "W", 0, 0)) }), 0).ReachableNodes().Any(n => n.Ix >= 6);
            foreach (var shape in new[] { "B1_0", "D2_0", "D2_90", "O4_0", "C3_0", "C3_90", "C3_180", "C3_270" }) Assert.IsTrue(Crosses(shape), shape);
            foreach (var shape in new[] { "I3_0", "I4_0", "L4_0", "J4_0", "T4_0", "S4_0", "Z4_0", "L4_180", "T4_180" }) Assert.IsFalse(Crosses(shape), shape);
        }

        [Test(Description = "K-05 K-07 row 3: release over the yard with any cell in the crane area cancels")]
        public void K05_K07_row_3_release_over_the_yard_with_any_cell_in_the_crane_area_cancels()
        {
            var d = Drag(Init(new LevelSpec { Pieces = Ps(Pc("D2_0", "W", 2, 0)) }), 0);
            AssertDrop(d.Classify(N(2, 6)), DropKinds.Yard, row: 2);
            AssertCancel(d.Classify(N(2, 7)), CancelReasons.CraneOverYard, 3);
            AssertCancel(d.Classify(N(2, 8)), CancelReasons.CraneOverYard, 3);
        }

        [Test(Description = "K-05 tall piece emits blockedByWallHeight once per drag")]
        public void K05_tall_piece_emits_blockedByWallHeight_once_per_drag()
        {
            var s = Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc("I3_0", "W", 5, 5)) });
            var d = Drag(s, 0);
            var first = d.Follow(6.5, 8);
            Assert.AreEqual(N(5, 7), first.Node);
            Assert.IsTrue(first.BlockedByWallHeight);
            Assert.IsFalse(d.Follow(6.6, 8.2).BlockedByWallHeight);
            Assert.IsFalse(d.Follow(6.5, 9).BlockedByWallHeight);
            // a new drag may emit it again
            Assert.IsTrue(Drag(s, 0).Follow(6.5, 8).BlockedByWallHeight);
        }

        [Test(Description = "K-05 S4_0 at height 8 emits blockedByWallHeight (also Z4_0, L4_0); D2_0 and O4_0 cross, no signal")]
        public void K05_S4_0_at_height_8_emits_blockedByWallHeight_D2_0_and_O4_0_cross_no_signal()
        {
            foreach (var shape in new[] { "S4_0", "Z4_0", "L4_0" })
            {
                var d = Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc(shape, "W", 0, 0)) }), 0);
                var r = d.Follow(6.2, 8);
                Assert.Less(r.Node.Ix, 6, shape); // in the yard or straddling: never fully on the site
                Assert.IsTrue(r.BlockedByWallHeight, shape);
            }
            foreach (var shape in new[] { "D2_0", "O4_0" })
            {
                var d = Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc(shape, "W", 0, 0)) }), 0);
                var r = d.Follow(6.2, 8);
                Assert.AreEqual(6, r.Node.Ix, shape);
                Assert.IsFalse(r.BlockedByWallHeight, shape);
            }
        }

        [Test(Description = "K-05 no blockedByWallHeight while the target is over the yard or below the crane area")]
        public void K05_no_blockedByWallHeight_while_the_target_is_over_the_yard_or_below_the_crane_area()
        {
            var d = Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc("I3_0", "W", 5, 0)) }), 0);
            Assert.IsFalse(d.Follow(3, 8).BlockedByWallHeight);
            Assert.IsFalse(d.Follow(6.5, 2).BlockedByWallHeight);
            Assert.IsTrue(d.Follow(6.5, 8).BlockedByWallHeight);
        }

        // --- K-07 release table ------------------------------------------------------------------------------------------

        private static LevelSpec GapLevel() => new LevelSpec { Wall = Wall(4, Gap(1, 2)), Pieces = Ps(Pc("D2_90", "W", 2, 1)) };

        [Test(Description = "K-07 rows 1, 2, 3, 4, 6, 7 in GDD order; E-06 rail straddle and E-28 crane straddle cancel")]
        public void K07_rows_1_2_3_4_6_7_in_GDD_order_E06_rail_straddle_and_E28_crane_straddle_cancel()
        {
            var d = Drag(Init(GapLevel()), 0);
            var rows = new (DragNode node, string kind, string? reason, int row, int? gap)[]
            {
                (N(2, 1), DropKinds.Cancel, CancelReasons.SameSpot, 1, null),
                (N(0, 0), DropKinds.Yard, null, 2, null),
                (N(0, 8), DropKinds.Cancel, CancelReasons.CraneOverYard, 3, null),
                (N(5, 8), DropKinds.Cancel, CancelReasons.Straddle, 4, null), // E-28
                (R(0, 5, 1), DropKinds.Cancel, CancelReasons.Straddle, 4, null), // E-06
                (N(6, 8), DropKinds.SiteFree, null, 6, null),
                (N(6, 0), DropKinds.SiteFree, null, 6, null),
                (R(0, 6, 1), DropKinds.SiteRail, null, 7, 0),
            };
            foreach (var (node, kind, reason, row, gap) in rows)
            {
                Assert.IsTrue(d.IsReachable(node), Key(node));
                AssertDrop(d.Classify(node), kind, reason, row, gap, Key(node));
            }
            Assert.AreEqual(CancelReasons.SameSpot, d.Classify().Reason); // default: the current node
            Assert.IsTrue(Movement.IsCancelled(d.Classify(N(5, 8))));
            Assert.IsFalse(Movement.IsCancelled(d.Classify(N(6, 8))));
        }

        [Test(Description = "K-07 row 5 / E-27: a closed site cancels site drops (FREE and rail); straddle and yard rows come first")]
        public void K07_row_5_E27_a_closed_site_cancels_site_drops_straddle_and_yard_rows_come_first()
        {
            var s = Init(GapLevel());
            SetHdr(s, H.DeliveryCursor, s.Lvl.Segments.Length);
            Assert.IsTrue(Movement.IsSiteClosed(s));
            var d = Drag(s, 0);
            AssertCancel(d.Classify(N(6, 8)), CancelReasons.SiteClosed, 5);
            AssertCancel(d.Classify(R(0, 6, 1)), CancelReasons.SiteClosed, 5);
            AssertDrop(d.Classify(N(5, 8)), reason: CancelReasons.Straddle, row: 4);
            AssertDrop(d.Classify(N(0, 0)), DropKinds.Yard, row: 2);
            // the rule hook can decide instead
            Assert.AreEqual(DropKinds.SiteFree, Drag(s, 0, new DragRules(siteClosed: st => false)).Classify(N(6, 8)).Kind);
        }

        [Test(Description = "K-07 release straddling the boundary cancels (E-06 in a gap, E-28 above the wall)")]
        public void K07_release_straddling_the_boundary_cancels_E06_in_a_gap_E28_above_the_wall()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(6, Gap(2, 1)),
                Pieces = Ps(Pc("D2_90", "W", 3, 2), Pc("C3_0", "W", 0, 0)),
            });
            var rail = Drag(s, 0);
            Assert.AreEqual(R(0, 5, 2), rail.Follow(5, 2).Node);
            AssertCancel(rail.Classify(), CancelReasons.Straddle, 4);
            var air = Drag(s, 1);
            foreach (var node in new[] { N(5, 6), N(5, 8) })
            {
                Assert.IsTrue(air.IsReachable(node), Key(node));
                AssertCancel(air.Classify(node), CancelReasons.Straddle, 4, Key(node));
            }
        }

        [Test(Description = "K-07 a node outside R classifies as invalid")]
        public void K07_a_node_outside_R_classifies_as_invalid()
        {
            var d = Drag(Init(new LevelSpec { Pieces = Ps(Pc("D2_90", "W", 2, 1)).Plus(Blockers("", "", "", "", "", "", "#")) }), 0);
            AssertCancel(d.Classify(N(0, 1)), CancelReasons.Invalid, 0); // (0,1) blocked
            AssertDrop(d.Classify(N(7, 0)), reason: CancelReasons.Invalid); // out of the grid
            AssertDrop(d.Classify(new DragNode(2, 1, 9)), reason: CancelReasons.Invalid);
        }

        [Test(Description = "K-07 row 1 debris released at start cells in other mode cancels")]
        public void K07_row_1_debris_released_at_start_cells_in_other_mode_cancels()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(5, Gap(3, 1)),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("D2_90", "W", 6, 3) },
            });
            var d = Drag(s, 1);
            CollectionAssert.AreEqual(new[] { "F(6,3)", "R0(6,3)" }, Keys(d.StartNodes));
            AssertDrop(d.Classify(N(6, 3)), reason: CancelReasons.SameSpot, row: 1);
            AssertDrop(d.Classify(R(0, 6, 3)), reason: CancelReasons.SameSpot, row: 1);
        }

        // --- K-08 path and sticky follow ---------------------------------------------------------------------------------

        [Test(Description = "K-08 GDD example: the finger over a blocked cell keeps the block; to (3,9) it goes (2,7), (2,8), (3,8), (3,9)")]
        public void K08_GDD_example_the_finger_over_a_blocked_cell_keeps_the_block_to_3_9_it_goes_2_7_2_8_3_8_3_9()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(4),
                Pieces = Ps(Pc("B1_0", "W", 2, 6)).Plus(Blockers("##.###", "##.###", "######", "######", "######", "######")),
            });
            // rows y = 7 … 2 above; rows 1 and 0 are empty but closed off by the full row 2
            var d = Drag(s, 0);
            var stay = d.Follow(2, 3);
            Assert.IsFalse(stay.Changed);
            Assert.AreEqual(N(2, 6), stay.Node);
            var go = d.Follow(3, 9);
            Assert.IsTrue(go.Changed);
            CollectionAssert.AreEqual(new[] { "F(2,7)", "F(2,8)", "F(3,8)", "F(3,9)" }, Keys(go.Path));
            Assert.AreEqual(N(3, 9), d.Current);
        }

        [Test(Description = "K-08 the reachable set is fixed at pick time; later state changes do not alter the session")]
        public void K08_the_reachable_set_is_fixed_at_pick_time_later_state_changes_do_not_alter_the_session()
        {
            var s = Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 2, 6)) });
            var d = Drag(s, 0);
            var before = Keys(d.ReachableNodes());
            SetYardOcc(s, 2, 7, -1);
            SetYardOcc(s, 3, 6, -1);
            CollectionAssert.AreEqual(before, Keys(d.ReachableNodes()));
            Assert.IsTrue(d.IsReachable(N(2, 7)));
        }

        [Test(Description = "K-08 every follow path is a chain of unit steps through R (the block never teleports)")]
        public void K08_every_follow_path_is_a_chain_of_unit_steps_through_R_the_block_never_teleports()
        {
            var lvl = LevelFile(3);
            var s = CreateInitialState(lvl);
            var rng = new Mulberry32(8);
            for (int id = 0; id < lvl.StaticPieceCount; id++)
            {
                var d = Movement.BeginDrag(s, id);
                if (d == null) continue;
                for (int i = 0; i < 120; i++)
                {
                    var prev = d.Current;
                    double px = rng.Next() * 9 - 0.5;
                    double py = rng.Next() * 11 - 0.5;
                    var r = d.Follow(px, py);
                    foreach (var next in r.Path)
                    {
                        Assert.IsTrue(d.IsReachable(next));
                        int step = Math.Abs(next.Ix - prev.Ix) + Math.Abs(next.Iy - prev.Iy);
                        if (step == 0)
                        {
                            var starts = Keys(d.StartNodes);
                            CollectionAssert.Contains(starts, Key(prev));
                            CollectionAssert.Contains(starts, Key(next));
                        }
                        else
                        {
                            CollectionAssert.Contains(Keys(d.Neighbours(prev)), Key(next));
                        }
                        prev = next;
                    }
                    Assert.AreEqual(r.Node, prev);
                    Assert.AreEqual(!r.Changed, r.Path.Length == 0);
                }
            }
        }

        [Test(Description = "K-08 tie-break 1: fewer BFS steps from the current node wins")]
        public void K08_tie_break_1_fewer_BFS_steps_from_the_current_node_wins()
        {
            var s = Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc("B1_0", "W", 2, 7)).Plus(Blockers("##.###")) });
            var d = Drag(s, 0);
            d.MoveTo(N(2, 8));
            // (3,8) and (4,8) are both 0.25 away; (3,8) is 1 step from (2,8), (4,8) is 2
            Assert.AreEqual(N(3, 8), d.Nearest(3.5, 8));
            Assert.AreEqual(N(3, 8), d.Follow(3.5, 8).Node);
        }

        [Test(Description = "K-08 tie-break 2: FREE before RAIL at equal distance and equal BFS steps")]
        public void K08_tie_break_2_FREE_before_RAIL_at_equal_distance_and_equal_BFS_steps()
        {
            // from (5,5): FREE(6,3) = right, down, down; RAIL(6,3) = down, down, right → both 3 steps
            var high = Drag(Init(new LevelSpec { Wall = Wall(5, Gap(3, 1)), Pieces = Ps(Pc("B1_0", "W", 5, 5)) }), 0);
            Assert.AreEqual(3, high.DistanceFromCurrent(N(6, 3)));
            Assert.AreEqual(3, high.DistanceFromCurrent(R(0, 6, 3)));
            Assert.AreEqual(N(6, 3), high.Follow(6, 3).Node);
            // from (5,3) the rail is 1 step, FREE(6,3) 5 steps: tie-break 1 decides first
            var low = Drag(Init(new LevelSpec { Wall = Wall(5, Gap(3, 1)), Pieces = Ps(Pc("B1_0", "W", 5, 3)) }), 0);
            Assert.AreEqual(R(0, 6, 3), low.Follow(6, 3).Node);
        }

        [Test(Description = "K-08 tie-break 3: smaller y at equal distance, equal steps, same mode")]
        public void K08_tie_break_3_smaller_y_at_equal_distance_equal_steps_same_mode()
        {
            // y = 7 … 0; (4,1) and (4,3) are 1 away from p = (4,2) and 3 steps from (2,2)
            var layout = new[] { "######", "######", "######", "######", "##...#", "##.###", "##...#", "######" };
            var s = Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 2, 2)).Plus(Blockers(layout)) });
            var d = Drag(s, 0);
            Assert.AreEqual(3, d.DistanceFromCurrent(N(4, 1)));
            Assert.AreEqual(3, d.DistanceFromCurrent(N(4, 3)));
            Assert.AreEqual(N(4, 1), d.Follow(4, 2).Node);
        }

        [Test(Description = "K-08 tie-break 4: smaller x at equal distance, equal steps, same mode and row")]
        public void K08_tie_break_4_smaller_x_at_equal_distance_equal_steps_same_mode_and_row()
        {
            // (1,4) and (3,4) are 1 away from p = (2,4) and 3 steps from (2,2)
            var layout = new[] { "######", "######", "######", "#.#.##", "#.#.##", "#...##", "######", "######" };
            var s = Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 2, 2)).Plus(Blockers(layout)) });
            var d = Drag(s, 0);
            Assert.AreEqual(3, d.DistanceFromCurrent(N(1, 4)));
            Assert.AreEqual(3, d.DistanceFromCurrent(N(3, 4)));
            Assert.AreEqual(N(1, 4), d.Follow(2, 4).Node);
        }

        private static GameState TwoRailBoard(int[] ys) => Init(new LevelSpec
        {
            Wall = Wall(5, ys.Select(y => Gap(y, 1)).ToArray()),
            Plan = new[] { "WW", "WW", "WW" },
            Pieces = Ps(Pc("B1_0", "W", 4, 2), Pc("B1_0", "W", 5, 2)),
            Debris = new[] { new D("B1_0", "R", 6, 2) },
        });

        [Test(Description = "K-08 tie-break 3 between two rails: the smaller y wins whatever the gap order (data order or runtime y swap)")]
        public void K08_tie_break_3_between_two_rails_the_smaller_y_wins_whatever_the_gap_order()
        {
            // GDD K-08 ties: (2) FREE before RAIL, (3) smaller y, (4) smaller x — the gap index is not a criterion.
            // Piece 0 at (4,2), blocker at (5,2), debris at (6,2): R(6,1) and R(6,3) are 1 away from p = (6,2), 3 steps each.
            var runtimeSwap = TwoRailBoard(new[] { 1, 3 });
            // a W5-style runtime move: gap 0 now covers row 3 and gap 1 row 1 (the data order no longer matches y)
            SetGapField(runtimeSwap, 0, GF.Y, 3);
            SetGapField(runtimeSwap, 1, GF.Y, 1);
            var cases = new (string label, GameState s, int low)[]
            {
                ("data [1, 3]", TwoRailBoard(new[] { 1, 3 }), 0),
                ("data [3, 1]", TwoRailBoard(new[] { 3, 1 }), 1),
                ("runtime swap", runtimeSwap, 1),
            };
            foreach (var (label, s, low) in cases)
            {
                var d = Drag(s, 0);
                int high = 1 - low;
                Assert.AreEqual(3, d.DistanceFromCurrent(R(low, 6, 1)), label);
                Assert.AreEqual(3, d.DistanceFromCurrent(R(high, 6, 3)), label);
                Assert.IsFalse(d.IsReachable(N(6, 2)), label);
                Assert.Greater(d.DistanceFromCurrent(N(6, 3)), 3, label);
                Assert.Greater(d.DistanceFromCurrent(N(7, 2)), 3, label);
                Assert.AreEqual(Key(R(low, 6, 1)), Key(d.Nearest(6, 2)), label);
                Assert.AreEqual(Key(R(low, 6, 1)), Key(d.Follow(6, 2).Node), label);
            }
        }

        [Test(Description = "K-08 tie-break 2 before 3 with two gaps: FREE beats RAIL even when the rail node has the smaller y")]
        public void K08_tie_break_2_before_3_with_two_gaps_FREE_beats_RAIL_even_when_the_rail_node_has_the_smaller_y()
        {
            // piece 0 at (4,3), debris at (6,4); p = (6,4): FREE (5,4) and RAIL (6,3) are 1 away and 2 steps from (4,3)
            foreach (var ys in new[] { new[] { 1, 3 }, new[] { 3, 1 } })
            {
                var s = Init(new LevelSpec
                {
                    Wall = Wall(5, ys.Select(y => Gap(y, 1)).ToArray()),
                    Plan = WW(5),
                    Pieces = Ps(Pc("B1_0", "W", 4, 3)),
                    Debris = new[] { new D("B1_0", "R", 6, 4) },
                });
                var d = Drag(s, 0);
                int g3 = Array.IndexOf(ys, 3);
                Assert.AreEqual(2, d.DistanceFromCurrent(R(g3, 6, 3)));
                Assert.AreEqual(2, d.DistanceFromCurrent(N(5, 4)));
                Assert.AreEqual(N(5, 4), d.Nearest(6, 4));
            }
        }

        [Test(Description = "K-08 hysteresis accepts exactly 0.2 improvement and rejects less")]
        public void K08_hysteresis_accepts_exactly_0_2_improvement_and_rejects_less()
        {
            Assert.AreEqual(0.2, Movement.HysteresisD2);
            var s = Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 2, 2)) });
            // p = (2.6, 2): d²(2,2) = 0.36, d²(3,2) = 0.16 → exactly 0.2 better
            var exact = Drag(s, 0).Follow(2.6, 2);
            Assert.IsTrue(exact.Changed);
            Assert.AreEqual(N(3, 2), exact.Node);
            // p = (2.59, 2): 0.18 better → stays
            var less = Drag(s, 0).Follow(2.59, 2);
            Assert.IsFalse(less.Changed);
            Assert.AreEqual(N(2, 2), less.Node);
        }

        [Test(Description = "K-08 hidden items (screw, key) do not block the path; crates and bags do (K-09 d)")]
        public void K08_hidden_items_do_not_block_the_path_crates_and_bags_do()
        {
            var s = Init(new LevelSpec
            {
                Pieces = Ps(Pc("B1_0", "W", 2, 0)),
                Obstacles = new[] { Screw(2, 1), KeyItem(2, 2, "a"), Crate(1, 0, 2), Bag(3, 0) },
            });
            var d = Drag(s, 0);
            Assert.AreEqual(2, d.DistanceFromStart(N(2, 2)));
            Assert.IsFalse(d.IsReachable(N(1, 0)));
            Assert.IsFalse(d.IsReachable(N(3, 0)));
            Assert.AreEqual(2, d.DistanceFromStart(N(1, 1)));
        }

        [Test(Description = "K-08 moveTo follows the BFS path and refuses nodes outside R")]
        public void K08_moveTo_follows_the_BFS_path_and_refuses_nodes_outside_R()
        {
            var d = Drag(Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            CollectionAssert.AreEqual(new[] { "F(0,1)", "F(0,2)", "F(0,3)" }, Keys(d.MoveTo(N(0, 3)).Path));
            Assert.IsFalse(d.MoveTo(N(0, 3)).Changed);
            CollectionAssert.AreEqual(new[] { "F(1,3)" }, Keys(d.PathTo(N(1, 3))!));
            Assert.Throws<ArgumentOutOfRangeException>(() => d.MoveTo(N(7, 10))); // TS: N(7, 9.5), a non-integer row; ints cannot express it
            Assert.IsNull(d.PathTo(new DragNode(0, 0, 3)));
        }

        [Test(Description = "K-08 a site block whose start breaks open sky can always step back to its start (two-way edges)")]
        public void K08_a_site_block_whose_start_breaks_open_sky_can_always_step_back_to_its_start()
        {
            var s = Init(new LevelSpec
            {
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 2), new D("B1_0", "R", 6, 4) }, // over the dragged debris: its FREE start has no open sky
            });
            var d = Drag(s, 1);
            Assert.AreEqual(N(7, 2), d.Follow(7, 2).Node);
            CollectionAssert.Contains(Keys(d.Neighbours(N(7, 2))), "F(6,2)");
            Assert.AreEqual(1, d.DistanceFromCurrent(N(6, 2)));
            var back = d.Follow(6, 2);
            CollectionAssert.AreEqual(new[] { "F(6,2)" }, Keys(back.Path));
            Assert.AreEqual(CancelReasons.SameSpot, d.Classify().Reason);
            Assert.IsFalse(d.IsReachable(N(6, 3))); // still no open sky between the two
        }

        [Test(Description = "K-08 interleaved sessions (scene drag + solver / tutorial checks) never disturb each other")]
        public void K08_interleaved_sessions_never_disturb_each_other()
        {
            var s = LevelState(1); // 4×4 | 2×5, H 5: crane rows 5–6, site x 4–5
            var targets = new (int x, int y)[] { (4, 5), (0, 6), (4, 0), (3, 5), (5, 6) };
            string[] Replay(int id)
            {
                var d = Drag(s, id);
                return targets.Select(t => string.Join(" ", Keys(d.Follow(t.x, t.y).Path))).ToArray();
            }
            var alone0 = Replay(0);
            var alone3 = Replay(3);
            var a = Drag(s, 0);
            var b = Drag(s, 3);
            var mixed0 = new List<string>();
            var mixed3 = new List<string>();
            foreach (var (x, y) in targets)
            {
                mixed0.Add(string.Join(" ", Keys(a.Follow(x, y).Path)));
                Drag(s, 1); // a third BFS in between
                mixed3.Add(string.Join(" ", Keys(b.Follow(x, y).Path)));
            }
            CollectionAssert.AreEqual(alone0, mixed0);
            CollectionAssert.AreEqual(alone3, mixed3);
        }

        // --- K-09 pick gate ----------------------------------------------------------------------------------------------

        [Test(Description = "K-09 GDD example: the D2_0 under the crane area is pickable; the buried D2_0 is not")]
        public void K09_GDD_example_the_D2_0_under_the_crane_area_is_pickable_the_buried_D2_0_is_not()
        {
            var layout = new[] { ".#####", ".#####", ".#####", ".#####", "######", "######", "######", "######" };
            var s = Init(new LevelSpec { Pieces = Ps(Pc("D2_0", "W", 0, 6), Pc("D2_0", "W", 0, 4)).Plus(Blockers(layout)) });
            Assert.IsTrue(Movement.CanPickPiece(s, 0));
            Assert.IsTrue(Drag(s, 0).IsReachable(N(0, 8)));
            AssertPickFails(Movement.TryBeginDrag(s, 1), PickFailures.Immovable);
            Assert.IsNull(Movement.BeginDrag(s, 1));
        }

        [Test(Description = "K-09 (b) K-14 a locked block cannot be picked")]
        public void K09_b_K14_a_locked_block_cannot_be_picked()
        {
            var s = Init(new LevelSpec { Pieces = Ps(Pc("D2_90", "W", 0, 0)) });
            ToSite(s, 0, 6, 0, locked: true);
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.Locked);
        }

        [Test(Description = "K-09 (c) a canPick rule (Y3 chain, Y4 wet) forbids the pick; core has no obstacle-specific check")]
        public void K09_c_a_canPick_rule_forbids_the_pick_core_has_no_obstacle_specific_check()
        {
            var s = Init(new LevelSpec
            {
                Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "chained" }), Pc("B1_0", "W", 2, 0, new[] { "wet" }, 2), Pc("B1_0", "W", 4, 0)),
            });
            var rules = new DragRules(canPick: (st, id) => !HasFlag(st, id, FlagBit.Chained) && !HasFlag(st, id, FlagBit.Wet));
            AssertPickFails(Movement.TryBeginDrag(s, 0, rules), PickFailures.Rule);
            AssertPickFails(Movement.TryBeginDrag(s, 1, rules), PickFailures.Rule);
            Assert.IsTrue(Movement.CanPickPiece(s, 2, rules));
            Assert.IsTrue(Movement.CanPickPiece(s, 0));
        }

        [Test(Description = "K-09 (d) crates and bags are not pieces: they cannot be picked and they block the path")]
        public void K09_d_crates_and_bags_are_not_pieces_they_cannot_be_picked_and_they_block_the_path()
        {
            var s = Init(new LevelSpec
            {
                Pieces = Ps(Pc("B1_0", "W", 2, 0), Pc("B1_0", "W", 2, 1)),
                Obstacles = new[] { Crate(1, 0), Bag(3, 0) },
            });
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.Immovable);
            AssertPickFails(Movement.TryBeginDrag(s, s.Lvl.Layout.Counts.Pieces), PickFailures.NotOnBoard);
            AssertPickFails(Movement.TryBeginDrag(s, -1), PickFailures.NotOnBoard);
            Assert.IsTrue(Movement.CanPickPiece(s, 1));
        }

        [Test(Description = "K-09 (e) E-30 a block cannot be picked with 0 moves left")]
        public void K09_e_E30_a_block_cannot_be_picked_with_0_moves_left()
        {
            var s = Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            SetHdr(s, H.MovesLeft, 0);
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.NoMoves);
            SetHdr(s, H.MovesLeft, 1);
            Assert.IsTrue(Movement.CanPickPiece(s, 0));
        }

        [Test(Description = "K-09 queued, pending (undelivered batch) and gone pieces cannot be picked")]
        public void K09_queued_pending_and_gone_pieces_cannot_be_picked()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "Y", 1, 8)) } },
            });
            AssertPickFails(Movement.TryBeginDrag(s, 1), PickFailures.NotOnBoard); // pending
            Grid.VacatePiece(s, 0);
            EnqueuePiece(s, 0);
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.NotOnBoard); // queue
            SetPieceField(s, 0, PF.Zone, Zone.Gone);
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.NotOnBoard);
        }

        [Test(Description = "K-09 site piece of a non-visible segment cannot be picked (K-22)")]
        public void K09_site_piece_of_a_non_visible_segment_cannot_be_picked_K22()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 0, 1) },
            });
            AssertPickFails(Movement.TryBeginDrag(s, 1), PickFailures.HiddenSegment);
            SetHdr(s, H.ActiveSeg, 1);
            Assert.IsTrue(Movement.CanPickPiece(s, 1));
        }

        [Test(Description = "K-09 site piece of non-front carousel segment cannot be picked")]
        public void K09_site_piece_of_non_front_carousel_segment_cannot_be_picked()
        {
            var s = Init(new LevelSpec
            {
                Mode = "carousel",
                CarouselEvery = 2,
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 0, 1) },
            });
            AssertPickFails(Movement.TryBeginDrag(s, 1), PickFailures.HiddenSegment);
            SetHdr(s, H.FrontSeg, 1);
            Assert.IsTrue(Movement.CanPickPiece(s, 1));
        }

        [Test(Description = "K-09 debris in a closed gap row is not rail-pickable")]
        public void K09_debris_in_a_closed_gap_row_is_not_rail_pickable()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(5, LockedGap(3, 1, "k")),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 3) },
            });
            CollectionAssert.AreEqual(new[] { "F(6,3)" }, Keys(Drag(s, 1).StartNodes));
            // unlocked (or K-40 open shutter through the rule hook): RAIL start too
            CollectionAssert.AreEqual(new[] { "F(6,3)", "R0(6,3)" }, Keys(Drag(s, 1, new DragRules(canPassGap: (st, g, id) => true)).StartNodes));
        }

        [Test(Description = "K-09 two start nodes do not make immovable debris pickable")]
        public void K09_two_start_nodes_do_not_make_immovable_debris_pickable()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(5, Gap(3, 1)),
                Pieces = Ps(Pc("B1_0", "W", 5, 3)),
                Debris = new[] { new D("B1_0", "R", 6, 3), new D("B1_0", "R", 6, 2), new D("B1_0", "R", 6, 4), new D("B1_0", "R", 7, 3) },
            });
            AssertPickFails(Movement.TryBeginDrag(s, 1), PickFailures.Immovable);
            // freeing the yard cell behind the gap makes the RAIL start move
            Grid.VacatePiece(s, 0);
            SetPieceField(s, 0, PF.Zone, Zone.Gone);
            Assert.IsTrue(Drag(s, 1).IsReachable(N(5, 3)));
        }

        // --- K-10 yard ---------------------------------------------------------------------------------------------------

        [Test(Description = "K-10 any other empty yard position is a yard placement (K-07 row 2); the start cancels (row 1)")]
        public void K10_any_other_empty_yard_position_is_a_yard_placement_the_start_cancels()
        {
            var s = Init(new LevelSpec { Pieces = Ps(Pc("O4_0", "W", 2, 6)).Plus(Blockers("", "", "", "", "....##")) });
            var d = Drag(s, 0);
            Assert.IsTrue(d.IsReachable(N(4, 6)));
            AssertDrop(d.Classify(N(4, 6)), DropKinds.Yard, row: 2);
            AssertDrop(d.Classify(N(4, 4)), DropKinds.Yard, row: 2);
            Assert.IsFalse(d.IsReachable(N(4, 3))); // (4,3), (5,3) are blocked
            AssertCancel(d.Classify(N(2, 6)), CancelReasons.SameSpot);
        }

        // --- K-11 over the wall ------------------------------------------------------------------------------------------

        [Test(Description = "K-11 GDD example: D2_90 over the wall descends to the silhouette, never below it")]
        public void K11_GDD_example_D2_90_over_the_wall_descends_to_the_silhouette_never_below_it()
        {
            var s = Init(new LevelSpec { Wall = Wall(2), Pieces = Ps(Pc("D2_90", "Y", 0, 0), Pc("D2_0", "W", 2, 0)) });
            var empty = Drag(s, 0);
            Assert.IsTrue(empty.IsReachable(N(6, 8)));
            Assert.IsTrue(empty.IsReachable(N(6, 0)));
            AssertDrop(empty.Classify(N(6, 1)), DropKinds.SiteFree, row: 6);
            // a locked D2_0 at (6,0)–(6,1): top(6) = 2 → the lowest anchor is (6,2)
            ToSite(s, 1, 6, 0, locked: true);
            var d = Drag(s, 0);
            CollectionAssert.AreEqual(new[] { "o.", "o.", "o.", "o.", "o.", "o.", "o.", "o.", "..", ".." }, AnchorMap(d).Select(row => row.Substring(6)).ToArray());
        }

        [Test(Description = "K-11 in FREE mode a cell crosses the boundary only in a row y ≥ height")]
        public void K11_in_FREE_mode_a_cell_crosses_the_boundary_only_in_a_row_y_at_least_height()
        {
            var d = Drag(Init(new LevelSpec { Wall = Wall(4), Pieces = Ps(Pc("D2_0", "W", 5, 0)) }), 0);
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(5, 3))), "F(6,3)"); // rows 3–4: row 3 closed
            CollectionAssert.Contains(Keys(d.Neighbours(N(5, 4))), "F(6,4)"); // rows 4–5 open
            Assert.IsTrue(d.IsReachable(N(6, 0)));
        }

        // --- K-12 rail ---------------------------------------------------------------------------------------------------

        private static WallSpec Gap3 => Wall(5, Gap(3, 1));

        [Test(Description = "K-12 W1 GDD example: D2_90 enters the y = 3 gap from (4,3) and lands on the rail; D2_0 cannot enter")]
        public void K12_W1_GDD_example_D2_90_enters_the_y_3_gap_from_4_3_and_lands_on_the_rail_D2_0_cannot_enter()
        {
            var s = Init(new LevelSpec { Wall = Gap3, Pieces = Ps(Pc("D2_90", "W", 4, 3), Pc("D2_0", "W", 0, 3)) });
            var d = Drag(s, 0);
            CollectionAssert.Contains(Keys(d.Neighbours(N(4, 3))), "R0(5,3)");
            Assert.AreEqual(2, d.DistanceFromStart(R(0, 6, 3)));
            AssertDrop(d.Classify(R(0, 5, 3)), reason: CancelReasons.Straddle);
            AssertDrop(d.Classify(R(0, 6, 3)), DropKinds.SiteRail, row: 7, gap: 0);
            CollectionAssert.IsEmpty(Drag(s, 1).ReachableNodes().Where(n => n.Mode != Movement.Free).ToArray());
        }

        [Test(Description = "K-12 on the rail a block moves only horizontally and leaves it to the left, back to FREE")]
        public void K12_on_the_rail_a_block_moves_only_horizontally_and_leaves_it_to_the_left_back_to_FREE()
        {
            var d = Drag(Init(new LevelSpec { Wall = Gap3, Pieces = Ps(Pc("B1_0", "W", 5, 3)) }), 0);
            CollectionAssert.AreEqual(new[] { "F(5,3)", "R0(7,3)" }, Keys(d.Neighbours(R(0, 6, 3))));
            CollectionAssert.AreEqual(new[] { "R0(6,3)" }, Keys(d.Neighbours(R(0, 7, 3))));
            CollectionAssert.AreEqual(new[]
            {
                "........", "........", "........", "........", "........", "........", "......oo", "........", "........", "........",
            }, AnchorMap(d, Movement.RailMode(0)));
        }

        [Test(Description = "K-12 a rail is entered only from the yard side, never from a FREE block on the site")]
        public void K12_a_rail_is_entered_only_from_the_yard_side_never_from_a_FREE_block_on_the_site()
        {
            var d = Drag(Init(new LevelSpec { Wall = Gap3, Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            Assert.IsTrue(d.IsReachable(N(6, 3))); // dropped down the empty site column
            CollectionAssert.AreEqual(new[] { "F(7,3)", "F(6,2)", "F(6,4)" }, Keys(d.Neighbours(N(6, 3))));
            Assert.IsTrue(d.IsReachable(R(0, 6, 3))); // only through (5,3)
            var path = d.PathTo(R(0, 6, 3))!;
            Assert.AreEqual(N(5, 3), path[path.Length - 2]);
        }

        [Test(Description = "K-12 piece must fit the gap rows entirely")]
        public void K12_piece_must_fit_the_gap_rows_entirely()
        {
            var aligned = Drag(Init(new LevelSpec { Wall = Wall(5, Gap(2, 2)), Pieces = Ps(Pc("D2_0", "W", 5, 2)) }), 0);
            Assert.IsTrue(aligned.IsReachable(R(0, 6, 2)));
            Assert.IsFalse(aligned.IsReachable(R(0, 6, 3))); // rows 3–4: row 4 is outside the gap
            var narrow = Drag(Init(new LevelSpec { Wall = Gap3, Pieces = Ps(Pc("D2_0", "W", 5, 3)) }), 0);
            CollectionAssert.IsEmpty(narrow.ReachableNodes().Where(n => n.Mode != Movement.Free).ToArray());
        }

        [Test(Description = "K-12 N14 debris example: in an open gap row it is pulled left through the gap to the yard")]
        public void K12_N14_debris_example_in_an_open_gap_row_it_is_pulled_left_through_the_gap_to_the_yard()
        {
            var s = Init(new LevelSpec { Wall = Gap3, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("D2_90", "R", 6, 3) } });
            var d = Drag(s, 1);
            CollectionAssert.AreEqual(new[] { "F(6,3)", "R0(6,3)" }, Keys(d.StartNodes));
            Assert.AreEqual(2, d.DistanceFromStart(N(4, 3)));
            AssertDrop(d.Classify(N(4, 3)), DropKinds.Yard, row: 2);
            // closed gap: only FREE, up and over the wall (2 up, 2 left, 2 down)
            var locked = Init(new LevelSpec
            {
                Wall = Wall(5, LockedGap(3, 1, "k")),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("D2_90", "R", 6, 3) },
            });
            Assert.AreEqual(6, Drag(locked, 1).DistanceFromStart(N(4, 3)));
        }

        [Test(Description = "K-12 stuck mortar never starts in rail")]
        public void K12_stuck_mortar_never_starts_in_rail()
        {
            var s = Init(new LevelSpec { Wall = Gap3, Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "mortar" })) });
            ToSite(s, 0, 6, 3, locked: false, stuck: true);
            var d = Drag(s, 0);
            CollectionAssert.AreEqual(new[] { "F(6,3)" }, Keys(d.StartNodes));
            Assert.IsTrue(d.IsReachable(N(5, 3))); // through the yard: up, over, down
            Assert.Greater(d.DistanceFromStart(N(5, 3)), 1);
        }

        [Test(Description = "K-12 K-44 Ağır Yük (I5/Q9) never crosses the boundary, not even through a fitting gap; a 3-wide material block is no longer heavy (Faz 2R)")]
        public void K12_K44_Agir_Yuk_never_crosses_the_boundary_a_3_wide_material_block_is_no_longer_heavy()
        {
            // GDD K-44 Faz 2R: only I5/Q9 are cargo; cargo stays in the yard (x ≤ 5, y ≤ 7: no crane area, no rail)
            var cargo = Drag(Init(new LevelSpec { Id = 8, Wall = Gap3, Pieces = Ps(Pc("I5_0", "W", 0, 3)) }), 0);
            Assert.IsTrue(cargo.Cargo);
            CollectionAssert.IsEmpty(cargo.ReachableNodes().Where(n => n.Ix + 5 > 6 || n.Iy > 7 || n.Mode != Movement.Free).ToArray());
            Assert.IsTrue(cargo.IsReachable(N(1, 7)));
            Assert.IsFalse(cargo.IsReachable(N(1, 8)));
            // I3_90 is a material block now (the pre-2R "w ≥ 3 is heavy" branch is gone): it enters the 1-row gap, but a
            // 3-wide block never fits the 2-wide site (`piece_too_wide` keeps it out of valid data)
            var wide = Drag(Init(new LevelSpec { Wall = Gap3, Pieces = Ps(Pc("I3_90", "W", 3, 3)) }), 0);
            Assert.IsFalse(wide.Cargo);
            Assert.IsTrue(wide.CanEnterRail);
            CollectionAssert.IsEmpty(wide.ReachableNodes().Where(n => n.Ix >= 6).ToArray());
            Assert.IsTrue(wide.IsReachable(N(3, 9)));
        }

        // --- K-13 no side entry under an overhang ------------------------------------------------------------------------

        [Test(Description = "K-13 GDD example: a FREE block cannot slide under a rail-placed overhang")]
        public void K13_GDD_example_a_FREE_block_cannot_slide_under_a_rail_placed_overhang()
        {
            var s = Init(new LevelSpec { Wall = Wall(2), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 2, 0)) });
            ToSite(s, 1, 7, 3, locked: true); // placed from the rail, (7,2) below is empty
            var d = Drag(s, 0);
            Assert.IsTrue(d.IsReachable(N(6, 1)));
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(6, 1))), "F(7,1)");
            CollectionAssert.AreEqual(new[] { "oo", "oo", "oo", "oo", "oo", "oo", "o.", "o.", "o.", "o." }, AnchorMap(d).Select(row => row.Substring(6)).ToArray());
        }

        [Test(Description = "K-13 the underside of an overhang is reachable only by rail")]
        public void K13_the_underside_of_an_overhang_is_reachable_only_by_rail()
        {
            var s = Init(new LevelSpec { Wall = Wall(4, Gap(2, 1)), Pieces = Ps(Pc("B1_0", "W", 5, 2), Pc("B1_0", "W", 2, 0)) });
            ToSite(s, 1, 7, 3, locked: true);
            var d = Drag(s, 0);
            Assert.IsFalse(d.IsReachable(N(7, 2)));
            Assert.IsTrue(d.IsReachable(R(0, 7, 2)));
            Assert.AreEqual(DropKinds.SiteRail, d.Classify(R(0, 7, 2)).Kind);
        }

        // --- tutorial drag signals (TECH §8.2) ---------------------------------------------------------------------------

        [Test(Description = "K-11 dragCrossedWall fires once, on the first FREE step across the boundary (Bölüm 1 overWall)")]
        public void K11_dragCrossedWall_fires_once_on_the_first_FREE_step_across_the_boundary()
        {
            var s = LevelState(1);
            var d = Drag(s, 0); // `a`, D2_0 Y at (0,2); wall 4, H 5 (LEVELS §2 Bölüm 1 tutorial step 1)
            Assert.IsTrue(d.CanCrossWall);
            Assert.IsFalse(d.CanEnterRail);
            var up = d.Follow(0, 4);
            Assert.IsFalse(up.CrossedWall);
            var over = d.Follow(4, 4);
            CollectionAssert.AreEqual(new[] { "F(1,4)", "F(2,4)", "F(3,4)", "F(4,4)" }, Keys(over.Path));
            Assert.IsTrue(over.CrossedWall);
            Assert.AreEqual(DropKinds.SiteFree, d.Classify().Kind);
            Assert.IsFalse(d.Follow(3, 4).CrossedWall);
            Assert.IsFalse(d.Follow(4, 4).CrossedWall);
        }

        [Test(Description = "K-12 dragEnteredRail fires on the first FREE → RAIL step (Bölüm 4 gapPass)")]
        public void K12_dragEnteredRail_fires_on_the_first_FREE_to_RAIL_step_Bolum_4_gapPass()
        {
            var s = LevelState(4);
            var hand = HandMoves(4);
            Run(s, new DragMove(hand[0].PieceId, hand[0].To)); // e: shift (LEVELS §2 Bölüm 4 moves 1–2)
            Run(s, new DragMove(hand[1].PieceId, hand[1].To)); // c: shift, the corridor x 2–3 is open
            var d = Drag(s, 0); // `a`, O4 W at (0,0)
            Assert.IsTrue(d.CanEnterRail);
            Assert.IsFalse(d.CanCrossWall);
            var r = d.Follow(4, 0);
            CollectionAssert.AreEqual(new[] { "F(1,0)", "F(2,0)", "R0(3,0)", "R0(4,0)" }, Keys(r.Path));
            Assert.IsTrue(r.EnteredRail);
            Assert.IsFalse(r.CrossedWall);
            AssertDrop(d.Classify(), DropKinds.SiteRail, null, null, 0);
        }

        [Test(Description = "K-12 a debris RAIL start is not a rail entry; leaving to the yard and re-entering from FREE is")]
        public void K12_a_debris_RAIL_start_is_not_a_rail_entry_leaving_to_the_yard_and_re_entering_from_FREE_is()
        {
            var s = Init(new LevelSpec { Wall = Wall(5, Gap(3, 1)), Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = new[] { new D("D2_90", "R", 6, 3) } });
            var d = Drag(s, 1);
            var o = d.Follow(4, 3);
            CollectionAssert.AreEqual(new[] { "R0(6,3)", "R0(5,3)", "F(4,3)" }, Keys(o.Path)); // mode switch at the start cells first
            Assert.IsFalse(o.EnteredRail);
            var back = d.Follow(5.9, 3);
            Assert.IsTrue(back.EnteredRail);
            Assert.AreEqual(N(6, 3), back.Node); // tie with R0(6,3): equal steps, FREE first
            Assert.AreEqual(CancelReasons.SameSpot, d.Classify().Reason);
        }

        // --- levels 1–10 -------------------------------------------------------------------------------------------------

        [Test(Description = "K-09 K-11 K-12 every piece of levels 1–10 is checked; the canonical first moves are reachable with their kind")]
        public void K09_K11_K12_every_piece_of_levels_1_10_is_checked_the_canonical_first_moves_are_reachable_with_their_kind()
        {
            for (int id = 1; id <= 10; id++)
            {
                var lvl = LevelFile(id);
                var s = CreateInitialState(lvl);
                int wy = lvl.Geo.Wy;
                int hy = lvl.Geo.Hy;
                int rows = lvl.Geo.Rows;
                int pickable = 0;
                for (int p = 0; p < lvl.Layout.Counts.Pieces; p++)
                {
                    var attempt = Movement.TryBeginDrag(s, p);
                    if (!attempt.Ok) continue;
                    pickable++;
                    foreach (var n in attempt.Session!.ReachableNodes())
                    {
                        foreach (var c in attempt.Session.Cells(n))
                        {
                            Assert.Less(c.Y, rows);
                            if (c.X < wy && c.Y < hy) CollectionAssert.Contains(new[] { 0, p + 1 }, YardOcc(s, c.X, c.Y));
                        }
                    }
                }
                Assert.Greater(pickable, 0, "level " + id);
                // LEVELS §2 "Kanonik çözüm" move 1: shift (yard), over the wall (siteFree) or rail (siteRail)
                var (pieceId, to, stepKind) = FirstHandMove(id);
                string kind = stepKind == "shift" ? DropKinds.Yard : stepKind == "rail" ? DropKinds.SiteRail : DropKinds.SiteFree;
                Assert.AreEqual(kind, Drag(s, pieceId).Classify(to).Kind, "level " + id + " move 1");
            }
        }

        // --- edge model ≡ cell-by-cell model (TECH §2.2, §12.4) ------------------------------------------------------------

        private static JsonValue[] RandomGaps(IRng rng, int height)
        {
            var gaps = new List<JsonValue>();
            if (height < 2) return gaps.ToArray();
            var used = new HashSet<int>();
            int wanted = rng.NextInt(4);
            for (int tries = 0; tries < 6 && gaps.Count < wanted; tries++)
            {
                int y = rng.NextInt(height - 1);
                int size = 1 + rng.NextInt(Math.Min(3, height - 1 - y));
                var rowsOf = Enumerable.Range(y, size).ToArray();
                if (rowsOf.Any(r => used.Contains(r))) continue;
                foreach (var r in rowsOf) used.Add(r);
                gaps.Add(rng.Next() < 0.75 ? Gap(y, size) : LockedGap(y, size, "k" + gaps.Count));
            }
            return gaps.ToArray();
        }

        [Test(Description = "K-04 K-05 K-07 K-09 K-11 K-12 K-13 R, BFS distances, edges and release classes equal a cell-by-cell model on 600 seeded boards")]
        public void K04_K05_K07_K09_K11_K12_K13_R_BFS_distances_edges_and_release_classes_equal_a_cell_by_cell_model_on_600_seeded_boards()
        {
            var rng = new Mulberry32(20261006);
            var canonical = Canonical;
            int compared = 0;
            int immovable = 0;
            for (int trial = 0; trial < 600; trial++)
            {
                int height = rng.NextInt(9);
                var lvl = Builders.Compiled(new LevelSpec
                {
                    Wall = Wall(height, RandomGaps(rng, height)),
                    Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                    Debris = new[] { new D("B1_0", "W", 6, 0) },
                });
                var s = CreateInitialState(lvl);
                Array.Clear(s.Buf, lvl.Layout.YardOcc, lvl.Layout.Pieces - lvl.Layout.YardOcc);
                int elev = rng.Next() < 0.7 ? 0 : 1 + rng.NextInt(2);
                SetHdr(s, H.Elev, elev);
                bool onSite = rng.Next() < 0.3;
                int id = onSite ? 1 : 0;
                SetPieceField(s, 1 - id, PF.Zone, Zone.Gone);
                var pool = onSite ? canonical.Where(sh => sh.W <= 2 && !sh.Heavy && sh.H <= 8 - elev).ToArray() : canonical;
                var shape = pool[rng.NextInt(pool.Length)];
                SetPieceField(s, id, PF.Shape, shape.Index);
                if (onSite)
                {
                    SetPieceField(s, id, PF.Zone, Zone.Site);
                    SetPieceField(s, id, PF.X, 6 + rng.NextInt(3 - shape.W));
                    SetPieceField(s, id, PF.Y, rng.NextInt(8 - elev - shape.H + 1));
                    SetPieceField(s, id, PF.Seg, 0);
                    if (rng.Next() < 0.3) SetFlag(s, id, FlagBit.Debris, false); // a stuck-mortar-like site block
                }
                else
                {
                    SetPieceField(s, id, PF.Zone, Zone.Yard);
                    SetPieceField(s, id, PF.X, rng.NextInt(6 - shape.W + 1));
                    SetPieceField(s, id, PF.Y, rng.NextInt(8 - shape.H + 1));
                }
                Grid.OccupyPiece(s, id);
                double yardDensity = rng.Next() * 0.9;
                double siteDensity = rng.Next() * 0.5;
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 6; x++)
                        if (YardOcc(s, x, y) == 0 && rng.Next() < yardDensity) SetYardOcc(s, x, y, -1);
                    for (int sx = 0; sx < 2; sx++)
                        if (SiteOcc(s, 0, sx, y) == 0 && rng.Next() < siteDensity) SetSiteOcc(s, 0, sx, y, SiteTrowel);
                }

                var m = new CellModel(lvl.Geo, shape, height,
                    lvl.Gaps.Select(g => new ModelGap(g.Y, g.Y + g.Size, g.Type != "locked")).ToArray(),
                    (x, y) =>
                    {
                        if (x <= 5)
                        {
                            int v = YardOcc(s, x, y);
                            return v != 0 && v != id + 1;
                        }
                        if (y < elev) return true;
                        if (y >= 8) return false;
                        int sv = SiteOcc(s, 0, x - 6, y - elev);
                        return sv != 0 && sv != id + 1;
                    });
                var anchor = N(s.Buf[lvl.Layout.Pieces + id * 9 + PF.X], s.Buf[lvl.Layout.Pieces + id * 9 + PF.Y] + (onSite ? elev : 0));
                var starts = new List<DragNode> { anchor };
                if (onSite && HasFlag(s, id, FlagBit.Debris))
                    for (int g = 0; g < m.Gaps.Length; g++)
                        if (m.Valid(R(g, anchor.Ix, anchor.Iy))) starts.Add(R(g, anchor.Ix, anchor.Iy));
                foreach (var st in starts) m.Starts.Add(CellModel.Key(st));
                var want = m.Bfs(starts);
                bool moves = want.Keys.Any(k => !k.EndsWith(":" + anchor.Ix + "," + anchor.Iy, StringComparison.Ordinal));
                var attempt = Movement.TryBeginDrag(s, id);
                if (!moves)
                {
                    AssertPickFails(attempt, PickFailures.Immovable, "trial " + trial);
                    immovable++;
                    continue;
                }
                if (!attempt.Ok) throw new InvalidOperationException("trial " + trial + ": expected a session, got " + attempt.Reason);
                var d = attempt.Session!;
                CollectionAssert.AreEqual(Keys(starts), Keys(d.StartNodes), "trial " + trial);
                var got = new Dictionary<string, int>();
                foreach (var n in d.ReachableNodes()) got[CellModel.Key(n)] = d.DistanceFromStart(n);
                CollectionAssert.AreEqual(CellModel.Entries(want), CellModel.Entries(got), "trial " + trial + " R");
                foreach (var n in d.ReachableNodes())
                {
                    CollectionAssert.AreEqual(m.Neighbours(n).Select(CellModel.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray(),
                        d.Neighbours(n).Select(CellModel.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray(), "trial " + trial + " edges of " + Key(n));
                    Assert.AreEqual(m.Classify(starts, n), CellModel.DropName(d.Classify(n)), "trial " + trial + " class of " + Key(n));
                }
                compared++;
            }
            Assert.Greater(compared, 300);
            Assert.Greater(immovable, 10);
        }

        [Test(Description = "K-08 node codes follow the TECH §2.1 layout and stay inside the per-level node space")]
        public void K08_node_codes_follow_the_TECH_2_1_layout_and_stay_inside_the_per_level_node_space()
        {
            Assert.AreEqual(90, Movement.NodesPerMode);
            var d = Drag(Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            Assert.IsFalse(d.IsReachable(new DragNode(0, 1, 1))); // no gaps → no RAIL mode
            Assert.AreEqual(-1, d.DistanceFromStart(new DragNode(0, 1, 1)));
            CollectionAssert.AreEqual(new[] { C(5, 8), C(6, 8), C(5, 9) }, Movement.BlockCells(Shapes.ShapeById("C3_0"), 5, 8));
        }
    }
}
