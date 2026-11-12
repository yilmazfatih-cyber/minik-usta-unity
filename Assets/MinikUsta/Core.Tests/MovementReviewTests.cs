#nullable enable
// Port of tests/review/movement.review.test.ts (independent rule review, rounds 1–3) — the tests that run on the
// movement / coords / grid / gravity APIs alone. The tests that go through `applyMove` (K-07 costs, K-10 / K-11 / K-12 /
// K-13 pipeline examples, E-30, round-2 crane transit, later-segment debris after a slide, N14 pull-out, Bölüm 1/4/9 hand
// paths after canonical moves, S2 `.` through a placement) are in MovementReviewTests.P3.cs (same partial class).
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
    public partial class MovementReviewTests
    {
        private static readonly BoardGeo Geo = Geometry.DefaultGeo;

        /// <summary>Every yard cell not covered by `pieces` and not listed in `empty` gets a B1 blocker (a ≥ 80 % full yard, K-02).</summary>
        private static P[] FullYard(P[] pieces, params (int x, int y)[] empty)
        {
            var taken = new HashSet<string>(empty.Select(e => e.x + "," + e.y));
            foreach (var p in pieces)
                foreach (var c in Shapes.ShapeById(p.Shape).Cells) taken.Add((p.X + c.X) + "," + (p.Y + c.Y));
            var o = new List<P>(pieces);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 6; x++)
                    if (!taken.Contains(x + "," + y)) o.Add(Pc("B1_0", "W", x, y));
            return o.ToArray();
        }

        private static BoardCell[] CellsAt(string shape, DragNode node) => Movement.BlockCells(Shapes.ShapeById(shape), node.Ix, node.Iy);

        /// <summary>GDD K-07 release table, written from the text over the block's cells (rows tried top-down, first match wins).</summary>
        private static int GddReleaseRow(BoardCell[] cells, int mode, bool isStart, bool closed)
        {
            if (isStart) return 1;
            bool yard = cells.All(c => c.X <= 5);
            bool site = cells.All(c => c.X >= 6);
            if (mode == Movement.Free && yard && cells.All(c => c.Y <= 7)) return 2;
            if (mode == Movement.Free && yard) return 3;
            if (!yard && !site) return 4;
            if (site && closed) return 5;
            if (site && mode == Movement.Free) return 6;
            if (site && mode != Movement.Free) return 7;
            return -1;
        }

        /// <summary>Bölüm 3 start yard (levels/level_003.json, copied so a later data edit does not change this review).</summary>
        private static readonly P[] Level3Yard =
        {
            Pc("O4_0", "W", 0, 6), Pc("D2_90", "Y", 4, 2), Pc("D2_90", "G", 2, 7), Pc("D2_90", "G", 2, 6), Pc("D2_0", "Y", 4, 6), Pc("D2_0", "G", 5, 6),
            Pc("O4_0", "G", 0, 4), Pc("O4_0", "W", 2, 4), Pc("O4_0", "G", 4, 4), Pc("D2_90", "G", 4, 3), Pc("O4_0", "Y", 0, 2), Pc("O4_0", "W", 2, 2),
            Pc("O4_0", "W", 0, 0), Pc("O4_0", "G", 2, 0), Pc("O4_0", "Y", 4, 0),
        };

        private static LevelSpec Level3() => new LevelSpec { Id = 3, Wall = Wall(6, Gap(2, 2)), Plan = new[] { "GG", "YY", "WW", "WW" }, Moves = 11, Pieces = Level3Yard };

        /// <summary>Bölüm 4 start yard (levels/level_004.json).</summary>
        private static LevelSpec Level4() => new LevelSpec
        {
            Id = 4,
            Wall = Wall(6, Gap(3, 2)),
            Plan = new[] { "RR", "WW", "W.", "WW", "YY" },
            Moves = 12,
            Pieces = Ps(Pc("D2_90", "Y", 0, 7), Pc("C3_0", "W", 2, 6), Pc("D2_90", "W", 4, 3), Pc("D2_90", "R", 4, 7), Pc("D2_90", "R", 0, 6), Pc("D2_90", "Y", 4, 6),
                Pc("O4_0", "W", 0, 4), Pc("O4_0", "R", 2, 4), Pc("O4_0", "Y", 4, 4), Pc("O4_0", "Y", 0, 2), Pc("O4_0", "W", 2, 2), Pc("D2_90", "R", 4, 2),
                Pc("O4_0", "R", 0, 0), Pc("O4_0", "Y", 2, 0), Pc("O4_0", "W", 4, 0)),
        };

        private static ShapeDef[] CanonicalShapes => Shapes.All.Where(sh => sh.Index == sh.CanonicalIndex && !Shapes.ForbiddenShapes.Contains(sh.Id)).ToArray();

        // --- R-03 / K-04 wall boundary ------------------------------------------------------------------------------------

        [Test(Description = "K-04 GDD example: height 6, gap y=2 size 2 → rows 0–1 closed, 2–3 gap (rail only), 4–5 closed, 6–9 open")]
        public void K04_GDD_example_height_6_gap_y2_size_2()
        {
            int gapRows = Coords.OpenRailMask(Geo, 2, 2, true);
            Assert.AreEqual(0b1100, gapRows);
            Assert.AreEqual(0b110011, Coords.ClosedBoundaryMask(Geo, 6, gapRows));
            Assert.AreEqual(0b1111000000, Coords.OpenFreeMask(Geo, 6));
            // a closed gap opens nothing: all six wall rows are closed, rail mask empty
            Assert.AreEqual(0, Coords.OpenRailMask(Geo, 2, 2, false));
            Assert.AreEqual(0b111111, Coords.ClosedBoundaryMask(Geo, 6, 0));
            Assert.AreEqual(Coords.OpenFreeMask(Geo, 6), Coords.RowRangeMask(Geo, 6, 4));

            // the same rows seen by a dragged B1: a FREE step across exactly at y ≥ 6, a RAIL step exactly at y 2–3
            var s = Init(new LevelSpec { Wall = Wall(6, Gap(2, 2)), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            var d = Drag(s, 0);
            for (int y = 0; y <= 9; y++)
            {
                var o = Keys(d.Neighbours(N(5, y)));
                Assert.AreEqual(y >= 6, o.Contains("F(6," + y + ")"), "FREE right at row " + y);
                Assert.AreEqual(y == 2 || y == 3, o.Contains("R0(6," + y + ")"), "RAIL right at row " + y);
                Assert.IsTrue(d.IsReachable(N(6, y)), "site column reachable at row " + y);
                Assert.AreEqual(y >= 6, Keys(d.Neighbours(N(6, y))).Contains("F(5," + y + ")"), "FREE left at row " + y);
            }
        }

        [Test(Description = "K-04 height 0: no boundary row is closed and a B1 at (5,0) steps straight onto the site")]
        public void K04_height_0_no_boundary_row_is_closed_and_a_B1_at_5_0_steps_straight_onto_the_site()
        {
            Assert.AreEqual(0, Coords.ClosedBoundaryMask(Geo, 0, 0));
            var d = Drag(Init(new LevelSpec { Wall = Wall(0), Pieces = Ps(Pc("B1_0", "W", 5, 0)) }), 0);
            CollectionAssert.Contains(Keys(d.Neighbours(N(5, 0))), "F(6,0)");
            Assert.AreEqual(1, d.DistanceFromStart(N(6, 0)));
            AssertDrop(d.Classify(N(6, 0)), DropKinds.SiteFree, row: 6);
        }

        [Test(Description = "E-46 x = 5 and x = 6 are never 4-neighbours, in any row (§0: neighbourhood never crosses the boundary)")]
        public void E46_x_5_and_x_6_are_never_4_neighbours_in_any_row()
        {
            for (int y = 0; y <= 7; y++)
            {
                var of5 = Coords.Neighbors4(Geo, 5, y).Select(a => a.Ix + "," + a.Iy).ToArray();
                var of6 = Coords.Neighbors4(Geo, 6, y).Select(a => a.Ix + "," + a.Iy).ToArray();
                CollectionAssert.DoesNotContain(of5, "6," + y, "(5," + y + ")");
                CollectionAssert.Contains(of5, "4," + y, "(5," + y + ")");
                CollectionAssert.DoesNotContain(of6, "5," + y, "(6," + y + ")");
                CollectionAssert.Contains(of6, "7," + y, "(6," + y + ")");
            }
        }

        // --- K-05 crane area and the open height ----------------------------------------------------------------------------

        [Test(Description = "K-05 open height: a block crosses the wall in FREE mode iff its height h ≤ 10 − wall.height; heavy blocks never (Y5, N6)")]
        public void K05_Y5_N6_open_height_a_block_crosses_the_wall_in_FREE_mode_iff_its_height_h_at_most_10_minus_wall_height_heavy_never()
        {
            foreach (var shape in CanonicalShapes)
            {
                for (int height = 0; height <= 8; height++)
                {
                    var d = Drag(Init(new LevelSpec { Wall = Wall(height), Pieces = Ps(Pc(shape.Id, "W", 0, 0)) }), 0);
                    bool onSite = d.ReachableNodes().Any(n => n.Mode == Movement.Free && n.Ix >= 6);
                    // default board: a 2-column site, so a 3-wide material block has no site node (D-103: width is not weight)
                    Assert.AreEqual(!shape.Heavy && shape.W <= 2 && shape.H <= 10 - height, onSite, shape.Id + " at wall height " + height);
                }
            }
        }

        [Test(Description = "K-05 GDD example: at height 7 an I3_0 crosses only with anchor row 7 (cells 7, 8, 9); at height 8 it never crosses")]
        public void K05_GDD_example_at_height_7_an_I3_0_crosses_only_with_anchor_row_7_at_height_8_it_never_crosses()
        {
            var at7 = Drag(Init(new LevelSpec { Wall = Wall(7), Pieces = Ps(Pc("I3_0", "W", 5, 0)) }), 0);
            for (int y = 0; y <= 7; y++) Assert.AreEqual(y == 7, Keys(at7.Neighbours(N(5, y))).Contains("F(6," + y + ")"), "anchor row " + y);
            Assert.IsTrue(at7.IsReachable(N(6, 0)));
            var at8 = Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc("I3_0", "W", 5, 0)) }), 0);
            Assert.IsTrue(at8.IsReachable(N(5, 7))); // highest anchor: cells 7–9
            Assert.IsFalse(at8.IsReachable(N(5, 8))); // K-01: a cell at y = 10 does not exist
            CollectionAssert.IsEmpty(at8.ReachableNodes().Where(n => n.Ix + at8.Shape.W - 1 >= 6).ToArray());
        }

        [Test(Description = "K-05 K-07 row 3: every FREE node over the yard with a cell in rows 8–9 cancels, every one with all cells ≤ 7 is row 2 (K-01 D2_0 example)")]
        public void K05_K07_row_3_every_FREE_node_over_the_yard_with_a_cell_in_rows_8_9_cancels()
        {
            foreach (var shape in new[] { "B1_0", "D2_0", "I4_0", "L4_180", "C3_180", "T4_0", "D2_90" })
            {
                var d = Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc(shape, "W", 0, 0)) }), 0);
                var sh = Shapes.ShapeById(shape);
                int crane = 0;
                foreach (var n in d.ReachableNodes())
                {
                    if (n.Mode != Movement.Free || n.Ix + sh.W - 1 > 5 || (n.Ix == 0 && n.Iy == 0)) continue;
                    int top = n.Iy + sh.H - 1;
                    if (top >= 8) crane++;
                    if (top >= 8) AssertCancel(d.Classify(n), CancelReasons.CraneOverYard, 3, shape + " " + Key(n));
                    else AssertDrop(d.Classify(n), DropKinds.Yard, row: 2, label: shape + " " + Key(n));
                }
                Assert.Greater(crane, 0, shape);
            }
            // K-01 example: D2_0 anchored at (3,8) is a position, anchored at (3,9) is not
            var d2 = Drag(Init(new LevelSpec { Pieces = Ps(Pc("D2_0", "W", 3, 0)) }), 0);
            Assert.IsTrue(d2.IsReachable(N(3, 8)));
            Assert.IsFalse(d2.IsReachable(N(3, 9)));
        }

        [Test(Description = "K-05 K-08 K-44 Y5 example (Faz 2R): Ağır Yük I5_0 at (0,7) cannot be lifted to (1,8) (not in R); slid to (1,7) with (5,7) empty is a yard move; it never reaches x = 6")]
        public void K05_K08_K44_Y5_example_Agir_Yuk_I5_0_at_0_7_cannot_be_lifted_to_1_8()
        {
            var s = Init(new LevelSpec { Id = 8, Pieces = FullYard(Ps(Pc("I5_0", "W", 0, 7)), (5, 7)) });
            var d = Drag(s, 0);
            Assert.IsFalse(d.IsReachable(N(1, 8))); // pre-2R: reachable and cancelled (row 3); K-08 Faz 2R: no crane area
            AssertCancel(d.Classify(N(1, 8)), CancelReasons.Invalid, 0);
            AssertDrop(d.Classify(N(1, 7)), DropKinds.Yard, row: 2);
            Assert.IsFalse(d.IsReachable(N(2, 7)));
            Assert.IsTrue(d.ReachableNodes().All(n => n.Mode == Movement.Free && n.Ix + 5 <= 6 && n.Iy <= 7));
        }

        // --- K-07 release table ----------------------------------------------------------------------------------------------

        [Test(Description = "K-07 the table, applied top-down over the cells, gives the class of every reachable node (Bölüm 3 and 4 layouts, an open two-gap board)")]
        public void K07_the_table_applied_top_down_over_the_cells_gives_the_class_of_every_reachable_node()
        {
            var boards = new[]
            {
                Level3(),
                Level4(),
                new LevelSpec
                {
                    Wall = Wall(7, Gap(1, 2), Gap(4, 2)),
                    Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("C3_180", "Y", 3, 0), Pc("L4_0", "G", 0, 4), Pc("B1_0", "R", 4, 5), Pc("C3_90", "W", 2, 3)),
                },
            };
            var seen = new HashSet<int>();
            foreach (var spec in boards)
            {
                var s = Init(spec);
                var pieces = spec.Pieces!;
                for (int id = 0; id < pieces.Length; id++)
                {
                    var attempt = Movement.TryBeginDrag(s, id);
                    if (!attempt.Ok) continue;
                    var d = attempt.Session!;
                    var p = pieces[id];
                    foreach (var n in d.ReachableNodes())
                    {
                        int want = GddReleaseRow(CellsAt(p.Shape, n), n.Mode, n.Mode == Movement.Free && n.Ix == p.X && n.Iy == p.Y, false);
                        Assert.AreEqual(want, d.Classify(n).Row, "piece " + id + " " + p.Shape + " " + Key(n));
                        seen.Add(want);
                    }
                }
            }
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 6, 7 }, seen.OrderBy(x => x).ToArray());
        }

        // --- K-08 path rule and sticky follow -----------------------------------------------------------------------------

        [Test(Description = "K-08 GDD example on a full yard: finger over the filled (2,3) keeps B1 at (2,6); finger at (3,9) → (2,7), (2,8), (3,8), (3,9)")]
        public void K08_GDD_example_on_a_full_yard()
        {
            var s = Init(new LevelSpec { Wall = Wall(6), Pieces = FullYard(Ps(Pc("B1_0", "Y", 2, 6)), (2, 7)) });
            var d = Drag(s, 0);
            var stay = d.Follow(2, 3);
            Assert.IsFalse(stay.Changed);
            Assert.AreEqual(N(2, 6), d.Current);
            var go = d.Follow(3, 9);
            CollectionAssert.AreEqual(new[] { "F(2,7)", "F(2,8)", "F(3,8)", "F(3,9)" }, Keys(go.Path));
            Assert.AreEqual(N(3, 9), d.Current);
        }

        [Test(Description = "K-08 the block never passes through the wall: B1 at (5,2) aimed at (6,2) climbs to row 6 (= height), crosses there and comes down")]
        public void K08_the_block_never_passes_through_the_wall()
        {
            var d = Drag(Init(new LevelSpec { Wall = Wall(6), Pieces = Ps(Pc("B1_0", "W", 5, 2)) }), 0);
            var r = d.Follow(6, 2);
            CollectionAssert.AreEqual(new[] { "F(5,3)", "F(5,4)", "F(5,5)", "F(5,6)", "F(6,6)", "F(6,5)", "F(6,4)", "F(6,3)", "F(6,2)" }, Keys(r.Path));
            Assert.IsTrue(r.CrossedWall);
            AssertDrop(d.Classify(), DropKinds.SiteFree, row: 6);
        }

        [Test(Description = "K-08 hysteresis works in both directions: a 0.1 improvement never moves the block back, a 0.3 improvement does")]
        public void K08_hysteresis_works_in_both_directions()
        {
            var d = Drag(Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 2, 2)) }), 0);
            Assert.AreEqual(N(3, 2), d.Follow(2.7, 2).Node); // 0.49 → 0.09
            Assert.IsFalse(d.Follow(2.5, 2).Changed); // exact tie: stays
            Assert.IsFalse(d.Follow(2.45, 2).Changed); // 0.3025 vs 0.2025: only 0.1 better
            Assert.AreEqual(N(3, 2), d.Current);
            Assert.AreEqual(N(2, 2), d.Follow(2.35, 2).Node); // 0.4225 vs 0.1225
        }

        [Test(Description = "K-08 tie-break 1 beats tie-breaks 3 and 4: at equal distance the node fewer BFS steps away wins even with larger x or larger y")]
        public void K08_tie_break_1_beats_tie_breaks_3_and_4()
        {
            var d = Drag(Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            d.MoveTo(N(5, 8));
            // (3,8) and (4,8) are both 0.25 away from p; (4,8) is 1 step from (5,8), (3,8) is 2
            Assert.AreEqual(1, d.DistanceFromCurrent(N(4, 8)));
            Assert.AreEqual(2, d.DistanceFromCurrent(N(3, 8)));
            Assert.AreEqual(N(4, 8), d.Follow(3.5, 8).Node);
            var e = Drag(Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            e.MoveTo(N(2, 9));
            // (2,7) and (2,8) are both 0.25 away; (2,8) is 1 step, (2,7) is 2
            Assert.AreEqual(N(2, 8), e.Follow(2, 7.5).Node);
        }

        [Test(Description = "K-08 tie-break 3 before 4: equal distance, equal BFS steps, same mode → the smaller y wins over the smaller x")]
        public void K08_tie_break_3_before_4()
        {
            var s = Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 3, 4), Pc("B1_0", "W", 1, 2), Pc("B1_0", "W", 2, 3)) });
            var d = Drag(s, 0);
            // p = (1.5, 2.5): (1,2) and (2,3) are blocked; (1,3) and (2,2) are both 0.5 away and 3 steps from (3,4)
            Assert.AreEqual(3, d.DistanceFromCurrent(N(1, 3)));
            Assert.AreEqual(3, d.DistanceFromCurrent(N(2, 2)));
            Assert.AreEqual(N(2, 2), d.Nearest(1.5, 2.5));
            Assert.AreEqual(N(2, 2), d.Follow(1.5, 2.5).Node);
        }

        [Test(Description = "K-08 a rail block does not snap to the FREE position with the same cells (tie-break 1 keeps it on the rail)")]
        public void K08_a_rail_block_does_not_snap_to_the_FREE_position_with_the_same_cells()
        {
            var d = Drag(Init(new LevelSpec { Wall = Wall(5, Gap(3, 1)), Pieces = Ps(Pc("B1_0", "W", 5, 3)) }), 0);
            Assert.AreEqual(R(0, 6, 3), d.Follow(6, 3).Node); // 1 step through the gap, FREE (6,3) is 5 steps over the wall
            Assert.IsTrue(d.IsReachable(N(6, 3)));
            Assert.IsFalse(d.Follow(6, 3.05).Changed);
            AssertDrop(d.Classify(), DropKinds.SiteRail, row: 7);
        }

        // --- K-09 pick gate ---------------------------------------------------------------------------------------------------

        [Test(Description = "K-09 (a) full yard (Bölüm 3 layout): exactly the blocks with an open top, a side above the wall or a face on the gap can be picked")]
        public void K09_a_full_yard_Bolum_3_layout_exactly_the_blocks_with_an_open_top_a_side_above_the_wall_or_a_face_on_the_gap_can_be_picked()
        {
            var s = Init(Level3());
            var pickable = Enumerable.Range(0, Level3Yard.Length).Where(id => Movement.TryBeginDrag(s, id).Ok).ToArray();
            // 0 O4 W (0,6) top, 2 D2_90 G (2,7) top, 4 D2_0 Y (4,6) top, 5 D2_0 G (5,6) top + side at rows 6–7,
            // 1 D2_90 Y (4,2) and 9 D2_90 G (4,3) face the gap rows 2–3
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 4, 5, 9 }, pickable);
            CollectionAssert.AreEqual(new[] { "F(4,2)", "R0(5,2)", "R0(6,2)" }, Keys(Drag(s, 1).ReachableNodes()));
            CollectionAssert.AreEqual(new[] { "F(4,3)", "R0(5,3)", "R0(6,3)" }, Keys(Drag(s, 9).ReachableNodes()));
            Assert.IsTrue(Drag(s, 5).IsReachable(N(6, 6)));
            foreach (int id in new[] { 3, 6, 7, 8, 10, 11, 12, 13, 14 }) AssertPickFails(Movement.TryBeginDrag(s, id), PickFailures.Immovable, "piece " + id);
        }

        [Test(Description = "K-09 (a) a block whose only free face is a closed wall row is stuck; facing an open gap it fits it moves; facing a gap it does not fit it is stuck")]
        public void K09_a_a_block_whose_only_free_face_is_a_closed_wall_row_is_stuck()
        {
            var closedRow = Init(new LevelSpec { Wall = Wall(6, Gap(2, 1)), Pieces = FullYard(Ps(Pc("B1_0", "Y", 5, 4))) });
            AssertPickFails(Movement.TryBeginDrag(closedRow, 0), PickFailures.Immovable);
            var fits = Init(new LevelSpec { Wall = Wall(6, Gap(2, 1)), Pieces = FullYard(Ps(Pc("B1_0", "Y", 5, 2))) });
            CollectionAssert.AreEqual(new[] { "F(5,2)", "R0(6,2)", "R0(7,2)" }, Keys(Drag(fits, 0).ReachableNodes()));
            var tooTall = Init(new LevelSpec { Wall = Wall(6, Gap(2, 1)), Pieces = FullYard(Ps(Pc("D2_0", "Y", 5, 2))) });
            AssertPickFails(Movement.TryBeginDrag(tooTall, 0), PickFailures.Immovable);
        }

        [Test(Description = "K-09 (a) a site block boxed in by the wall, a block above and a block beside cannot be picked; freeing the side lets it move")]
        public void K09_a_a_site_block_boxed_in_by_the_wall_a_block_above_and_a_block_beside_cannot_be_picked()
        {
            var boxed = Init(new LevelSpec
            {
                Wall = Wall(4),
                Plan = WW(2),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 0), new D("B1_0", "R", 6, 1), new D("B1_0", "R", 7, 0) },
            });
            AssertPickFails(Movement.TryBeginDrag(boxed, 1), PickFailures.Immovable);
            var open = Init(new LevelSpec
            {
                Wall = Wall(4),
                Plan = WW(2),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 0), new D("B1_0", "R", 6, 1) },
            });
            var d = Drag(open, 1);
            Assert.IsTrue(d.IsReachable(N(7, 0)));
            CollectionAssert.AreEqual(new[] { "F(7,0)" }, Keys(d.Neighbours(N(6, 0)))); // row 0 of the wall is closed, (6,1) is taken
        }

        // --- K-11 over the wall -------------------------------------------------------------------------------------------

        [Test(Description = "K-11 open sky is checked per covered column at its lowest cell: C3_180 hooks over a filled (6,0) down to anchor (6,0), D2_90 stops at (6,1)")]
        public void K11_open_sky_is_checked_per_covered_column_at_its_lowest_cell()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = WW(2),
                Pieces = Ps(Pc("C3_180", "W", 0, 0), Pc("D2_90", "W", 2, 0), Pc("C3_0", "W", 0, 3)),
                Debris = new[] { new D("B1_0", "R", 6, 0) },
            });
            Assert.IsTrue(Drag(s, 0).IsReachable(N(6, 0)));
            var d2 = Drag(s, 1);
            Assert.IsFalse(d2.IsReachable(N(6, 0)));
            Assert.IsTrue(d2.IsReachable(N(6, 1)));
            var c3 = Drag(s, 2);
            Assert.IsFalse(c3.IsReachable(N(6, 0)));
            Assert.IsTrue(c3.IsReachable(N(6, 1)));
        }

        [Test(Description = "K-11 the column with the higher silhouette decides: with (7,1) filled, C3_0 (its x=7 cell at the bottom) stops at (6,2), C3_90 (x=7 cell one up) at (6,1)")]
        public void K11_the_column_with_the_higher_silhouette_decides()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = WW(3),
                Pieces = Ps(Pc("C3_0", "W", 0, 0), Pc("C3_90", "W", 2, 0), Pc("B1_0", "W", 4, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 1) },
            });
            int Lowest(int id, int ix)
            {
                var d = Drag(s, id);
                for (int iy = 0; iy <= 9; iy++) if (d.IsReachable(N(ix, iy))) return iy;
                return -1;
            }
            Assert.AreEqual(2, Lowest(0, 6));
            Assert.AreEqual(1, Lowest(1, 6));
            Assert.AreEqual(0, Lowest(2, 6));
            Assert.AreEqual(2, Lowest(2, 7));
        }

        [Test(Description = "K-11 in every site column the block can be lowered exactly down to its shadow (computeFall landing), never below")]
        public void K11_in_every_site_column_the_block_can_be_lowered_exactly_down_to_its_shadow_never_below()
        {
            var shapes = new[] { "B1_0", "D2_90", "D2_0", "O4_0", "C3_0", "C3_90", "C3_180", "C3_270", "L4_0", "J4_180" };
            int checkedCount = 0;
            foreach (var shape in shapes)
            {
                var s = Init(new LevelSpec
                {
                    Wall = Wall(4),
                    Plan = WW(6),
                    Pieces = Ps(Pc(shape, "W", 0, 0)),
                    Debris = new[] { new D("B1_0", "R", 7, 3), new D("B1_0", "R", 6, 1) }, // an overhang: (7,0)–(7,2) stay empty
                });
                var d = Drag(s, 0);
                int w = d.Shape.W;
                int h = d.Shape.H;
                for (int ix = 6; ix + w <= 8; ix++)
                {
                    var landing = Gravity.ComputeFall(s, 0, N(ix, 10 - h)).Landing;
                    Assert.AreEqual(ix, landing.Ix, shape + " column " + ix);
                    var reachable = new List<int>();
                    for (int iy = 0; iy + h <= 10; iy++) if (d.IsReachable(N(ix, iy))) reachable.Add(iy);
                    var expected = new List<int>();
                    for (int iy = landing.Iy; iy + h <= 10; iy++) expected.Add(iy);
                    CollectionAssert.AreEqual(expected, reachable, shape + " column " + ix);
                    checkedCount++;
                }
            }
            Assert.AreEqual(12, checkedCount); // B1 and D2_0 have two anchor columns, the 2-wide shapes one
        }

        [Test(Description = "K-11 GDD example: empty site, plan h=3: D2_90 released at (6,8) lands at (6,0) with d = 8; lowered first to (6,1) it has d = 1")]
        public void K11_GDD_example_empty_site_plan_h3_D2_90_released_at_6_8_lands_at_6_0_with_d_8()
        {
            var s = Init(new LevelSpec { Wall = Wall(6), Plan = new[] { "WW", "WW", "YY" }, Pieces = Ps(Pc("D2_90", "Y", 0, 0)) });
            var d = Drag(s, 0);
            Assert.IsTrue(d.IsReachable(N(6, 8)));
            Assert.IsTrue(d.IsReachable(N(6, 1)));
            AssertDrop(d.Classify(N(6, 1)), DropKinds.SiteFree, row: 6);
            var high = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.AreEqual(FallResult.ModeFree, high.Mode);
            Assert.AreEqual(A(6, 0), high.Landing);
            Assert.AreEqual(8, high.Distance);
            var low = Gravity.ComputeFall(s, 0, N(6, 1));
            Assert.AreEqual(0, low.Landing.Iy);
            Assert.AreEqual(1, low.Distance);
        }

        [Test(Description = "K-11 a FREE block may cross back over the wall into the yard only in rows y ≥ height (debris at (6,4) over a 4-high wall: 1 step)")]
        public void K11_a_FREE_block_may_cross_back_over_the_wall_into_the_yard_only_in_rows_y_at_least_height()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(4),
                Plan = WW(5),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 4), new D("B1_0", "R", 6, 3) },
            });
            var yardPiece = Drag(s, 0);
            CollectionAssert.Contains(Keys(yardPiece.Neighbours(N(6, 5))), "F(5,5)");
            var debris = Drag(s, 1);
            CollectionAssert.Contains(Keys(debris.Neighbours(N(6, 4))), "F(5,4)");
            Assert.AreEqual(1, debris.DistanceFromStart(N(5, 4)));
            AssertDrop(debris.Classify(N(5, 4)), DropKinds.Yard, row: 2);
            // the debris below it sits in a closed wall row: no direct step left
            var low = Drag(s, 2);
            CollectionAssert.DoesNotContain(Keys(low.Neighbours(N(6, 3))), "F(5,3)");
        }

        [Test(Description = "K-11 GDD §14.1 overWall: the signal fires on the first FREE step that moves a cell across the boundary (already at the straddling node), once per drag, in either direction")]
        public void K11_GDD_14_1_overWall_the_signal_fires_on_the_first_FREE_step_that_moves_a_cell_across_the_boundary()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(4),
                Plan = WW(5),
                Pieces = Ps(Pc("D2_90", "W", 4, 7)),
                Debris = new[] { new D("B1_0", "R", 6, 4) },
            });
            var d = Drag(s, 0);
            Assert.IsFalse(d.Follow(4, 8).CrossedWall);
            var half = d.Follow(5, 8);
            CollectionAssert.AreEqual(new[] { "F(5,8)" }, Keys(half.Path));
            Assert.IsTrue(half.CrossedWall);
            Assert.IsFalse(d.Follow(6, 8).CrossedWall);
            Assert.IsFalse(d.Follow(4, 8).CrossedWall);
            var back = Drag(s, 1).Follow(5, 4); // site → yard over the 4-high wall
            Assert.AreEqual(N(5, 4), back.Node);
            Assert.IsTrue(back.CrossedWall);
            Assert.IsFalse(back.EnteredRail);
        }

        // --- K-12 gaps and rails ------------------------------------------------------------------------------------------

        [Test(Description = "K-12 alignment: a block enters a gap of size n iff its height h ≤ n (W3: only 1-row blocks pass a 1-row gap; K-44 Ağır Yük never; a block wider than the site never reaches it)")]
        public void K12_alignment_a_block_enters_a_gap_of_size_n_iff_its_height_h_at_most_n()
        {
            for (int size = 1; size <= 3; size++)
            {
                foreach (var shape in CanonicalShapes)
                {
                    var d = Drag(Init(new LevelSpec { Wall = Wall(8, Gap(2, size)), Pieces = Ps(Pc(shape.Id, "W", 0, 0)) }), 0);
                    bool fits = !Movement.IsCargoShape(shape) && shape.H <= size;
                    Assert.AreEqual(fits, d.CanEnterRail, shape.Id + " gap size " + size);
                    Assert.AreEqual(fits && shape.W <= 2, d.ReachableNodes().Any(n => n.Mode != Movement.Free && n.Ix >= 6), shape.Id + " on the site rail, gap size " + size);
                }
            }
        }

        [Test(Description = "K-12 the vertical position is locked on the rail, even inside a 2-row gap; on the site side the only way out is left")]
        public void K12_the_vertical_position_is_locked_on_the_rail_even_inside_a_2_row_gap()
        {
            var d = Drag(Init(new LevelSpec { Wall = Wall(6, Gap(2, 2)), Pieces = Ps(Pc("B1_0", "W", 5, 2)) }), 0);
            CollectionAssert.AreEqual(new[] { "F(5,2)", "R0(7,2)" }, Keys(d.Neighbours(R(0, 6, 2))));
            CollectionAssert.AreEqual(new[] { "R0(6,2)" }, Keys(d.Neighbours(R(0, 7, 2))));
            d.MoveTo(R(0, 6, 2));
            Assert.AreEqual(3, d.DistanceFromCurrent(R(0, 6, 3))); // left, up in the yard, right again
        }

        [Test(Description = "K-12 GDD example: a 2-wide block leaves the rail only when both cells are back in the yard; half-way it is still RAIL (a cancel)")]
        public void K12_GDD_example_a_2_wide_block_leaves_the_rail_only_when_both_cells_are_back_in_the_yard()
        {
            var d = Drag(Init(new LevelSpec { Wall = Wall(5, Gap(3, 1)), Pieces = Ps(Pc("D2_90", "W", 4, 3)) }), 0);
            CollectionAssert.AreEqual(new[] { "R0(5,3)" }, Keys(d.Neighbours(R(0, 6, 3))));
            CollectionAssert.AreEqual(new[] { "F(4,3)", "R0(6,3)" }, Keys(d.Neighbours(R(0, 5, 3))));
            Assert.IsFalse(d.IsReachable(N(5, 3))); // a FREE straddle in a closed (gap) row does not exist
            AssertCancel(d.Classify(R(0, 5, 3)), CancelReasons.Straddle, 4);
            AssertDrop(d.Classify(R(0, 6, 3)), DropKinds.SiteRail, row: 7, gap: 0);
        }

        [Test(Description = "K-12 N3 at wall height 8 a 3-tall block reaches the site only through a 3-row gap, and stays on the rail")]
        public void K12_N3_at_wall_height_8_a_3_tall_block_reaches_the_site_only_through_a_3_row_gap()
        {
            var s = Init(new LevelSpec { Wall = Wall(8, Gap(2, 3)), Pieces = Ps(Pc("I3_0", "W", 0, 0), Pc("L4_0", "W", 2, 0)) });
            foreach (int id in new[] { 0, 1 })
            {
                var d = Drag(s, id);
                CollectionAssert.IsEmpty(d.ReachableNodes().Where(n => n.Mode == Movement.Free && n.Ix + d.Shape.W - 1 >= 6).ToArray(), d.Shape.Id);
                Assert.IsTrue(d.IsReachable(R(0, 6, 2)), d.Shape.Id);
                AssertDrop(d.Classify(R(0, 6, 2)), DropKinds.SiteRail, row: 7, label: d.Shape.Id);
            }
        }

        [Test(Description = "K-12 debris exception: debris B1 at (7,3) in an open gap row starts on the rail too and is pulled to the yard in 2 steps; behind a closed gap it leaves only over the wall")]
        public void K12_debris_exception_debris_B1_at_7_3_in_an_open_gap_row_starts_on_the_rail_too()
        {
            var open = Init(new LevelSpec
            {
                Wall = Wall(5, Gap(3, 1)),
                Plan = WW(4),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 3) },
            });
            var d = Drag(open, 1);
            CollectionAssert.AreEqual(new[] { "F(7,3)", "R0(7,3)" }, Keys(d.StartNodes).OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.AreEqual(2, d.DistanceFromStart(N(5, 3)));
            AssertDrop(d.Classify(N(5, 3)), DropKinds.Yard, row: 2);
            AssertCancel(d.Classify(R(0, 7, 3)), CancelReasons.SameSpot, 1);
            AssertCancel(d.Classify(N(7, 3)), CancelReasons.SameSpot, 1);

            var closed = Init(new LevelSpec
            {
                Wall = Wall(5, LockedGap(3, 1, "k")),
                Plan = WW(4),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 3) },
                Obstacles = new[] { KeyItem(0, 0, "k") },
            });
            var c = Drag(closed, 1);
            CollectionAssert.AreEqual(new[] { "F(7,3)" }, Keys(c.StartNodes));
            Assert.AreEqual(6, c.DistanceFromStart(N(5, 3))); // up to row 5, over, down
        }

        [Test(Description = "K-12 debris exception needs all its rows inside the gap: D2_0 debris (rows 3–4) starts on the rail of a 2-row gap, not of a 1-row gap")]
        public void K12_debris_exception_needs_all_its_rows_inside_the_gap()
        {
            var twoRows = Init(new LevelSpec
            {
                Wall = Wall(6, Gap(3, 2)),
                Plan = WW(5),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("D2_0", "R", 6, 3) },
            });
            CollectionAssert.AreEqual(new[] { "F(6,3)", "R0(6,3)" }, Keys(Drag(twoRows, 1).StartNodes).OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.AreEqual(1, Drag(twoRows, 1).DistanceFromStart(N(5, 3)));
            var oneRow = Init(new LevelSpec
            {
                Wall = Wall(6, Gap(3, 1)),
                Plan = WW(5),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("D2_0", "R", 6, 3) },
            });
            CollectionAssert.AreEqual(new[] { "F(6,3)" }, Keys(Drag(oneRow, 1).StartNodes));
            Assert.IsFalse(Drag(oneRow, 1).IsReachable(R(0, 6, 3)));
        }

        [Test(Description = "K-12 two adjacent 1-row gaps are two gaps, not one 2-row gap: a D2_0 spanning both rows cannot pass, a B1 passes either")]
        public void K12_two_adjacent_1_row_gaps_are_two_gaps_not_one_2_row_gap()
        {
            var wall = Wall(4, Gap(1, 1), Gap(2, 1));
            var tall = Drag(Init(new LevelSpec { Wall = wall, Pieces = Ps(Pc("D2_0", "W", 0, 0)) }), 0);
            Assert.IsFalse(tall.CanEnterRail);
            CollectionAssert.IsEmpty(tall.ReachableNodes().Where(n => n.Mode != Movement.Free).ToArray()); // over the wall it still goes (h 2 ≤ 6)
            var small = Drag(Init(new LevelSpec { Wall = wall, Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            Assert.IsTrue(small.IsReachable(R(0, 6, 1)));
            Assert.IsTrue(small.IsReachable(R(1, 6, 2)));
            Assert.IsFalse(small.IsReachable(R(0, 6, 2)));
        }

        // --- round 2 — K-08 tie-breaks across modes and gaps ---------------------------------------------------------------

        [Test(Description = "K-08 tie-break 3 also orders RAIL nodes of two different gaps: at equal distance and equal steps the smaller y wins, whatever the gap order in the data")]
        public void K08_tie_break_3_also_orders_RAIL_nodes_of_two_different_gaps()
        {
            // GDD K-08: "(2) serbest kip ray kipinden önce, (3) y'si küçük olan, (4) x'i küçük olan" — between two RAIL nodes
            // only (3)/(4) are left; the gap index is not a GDD criterion.
            foreach (var ys in new[] { new[] { 1, 3 }, new[] { 3, 1 } })
            {
                var s = Init(new LevelSpec
                {
                    Wall = Wall(5, ys.Select(y => Gap(y)).ToArray()),
                    Plan = WW(3),
                    Pieces = Ps(Pc("B1_0", "W", 4, 2), Pc("B1_0", "W", 5, 2)),
                    Debris = new[] { new D("B1_0", "R", 6, 2) },
                });
                int low = Array.IndexOf(ys, 1);
                int high = 1 - low;
                string label = "gaps listed as y " + string.Join(", ", ys);
                var d = Drag(s, 0);
                // p = (6,2): (5,2) is a block and (6,2) debris; the rail nodes (6,1) and (6,3) are 1 away and 3 steps from (4,2)
                Assert.AreEqual(3, d.DistanceFromCurrent(R(low, 6, 1)), label);
                Assert.AreEqual(3, d.DistanceFromCurrent(R(high, 6, 3)), label);
                // the FREE nodes 1 away are only reachable over the wall (more steps); F(6,1) has no open sky under the debris
                Assert.Greater(d.DistanceFromCurrent(N(6, 3)), 3, label);
                Assert.Greater(d.DistanceFromCurrent(N(7, 2)), 3, label);
                Assert.IsFalse(d.IsReachable(N(6, 1)), label);
                Assert.AreEqual("R" + low + "(6,1)", Key(d.Nearest(6, 2)), label);
                Assert.AreEqual("R" + low + "(6,1)", Key(d.Follow(6, 2).Node), label);
            }
        }

        [Test(Description = "K-08 tie-break 2 comes before 3: FREE (5,4) beats RAIL (6,3) at equal distance and steps although the rail node has the smaller y")]
        public void K08_tie_break_2_comes_before_3_FREE_5_4_beats_RAIL_6_3()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(5, Gap(3)),
                Plan = WW(5),
                Pieces = Ps(Pc("B1_0", "W", 4, 3)),
                Debris = new[] { new D("B1_0", "R", 6, 4) }, // removes F(6,4) (d = 0) and, by open sky, F(6,3)
            });
            var d = Drag(s, 0);
            // p = (6,4): RAIL (6,3) and FREE (5,4) are both 1 away and 2 steps from (4,3)
            Assert.AreEqual(2, d.DistanceFromCurrent(R(0, 6, 3)));
            Assert.AreEqual(2, d.DistanceFromCurrent(N(5, 4)));
            Assert.IsFalse(d.IsReachable(N(6, 4)));
            Assert.IsFalse(d.IsReachable(N(6, 3)));
            Assert.AreEqual(4, d.DistanceFromCurrent(N(6, 5))); // the other node 1 away, over the wall
            Assert.AreEqual(N(5, 4), d.Nearest(6, 4));
            Assert.AreEqual(N(5, 4), d.Follow(6, 4).Node);
        }

        // --- round 2 — K-04 K-12 gap rows, gap hooks and two gaps -----------------------------------------------------------

        [Test(Description = "K-01 K-05 R-03 geometry: crane rows 8–9 are neither yard nor site, x 5 | 6 is the boundary, only a box over both 5 and 6 straddles")]
        public void K01_K05_R03_geometry_crane_rows_8_9_are_neither_yard_nor_site_x_5_6_is_the_boundary()
        {
            CollectionAssert.AreEqual(new[] { "yard", "site" }, new[] { Coords.SideOf(Geo, 5), Coords.SideOf(Geo, 6) });
            CollectionAssert.AreEqual(new[] { true, false, false }, new[] { Coords.IsYardCell(Geo, 5, 7), Coords.IsYardCell(Geo, 5, 8), Coords.IsYardCell(Geo, 6, 0) });
            CollectionAssert.AreEqual(new[] { true, true, false, false }, new[] { Coords.IsSiteCell(Geo, 6, 0), Coords.IsSiteCell(Geo, 7, 7), Coords.IsSiteCell(Geo, 6, 8), Coords.IsSiteCell(Geo, 5, 0) });
            CollectionAssert.AreEqual(new[] { true, true, false }, new[] { Coords.IsCraneCell(Geo, 0, 8), Coords.IsCraneCell(Geo, 7, 9), Coords.IsCraneCell(Geo, 7, 7) });
            // K-01: "Hiçbir bloğun hücresi y > 9, x < 0 ya da x > 7 olamaz"
            CollectionAssert.AreEqual(new[] { true, false, false, false, false },
                new[] { Coords.InGrid(Geo, 7, 9), Coords.InGrid(Geo, 7, 10), Coords.InGrid(Geo, 8, 0), Coords.InGrid(Geo, -1, 0), Coords.InGrid(Geo, 0, -1) });
            // K-07 row 4: cells on both sides of the boundary
            CollectionAssert.AreEqual(new[] { true, false, false }, new[] { Coords.StraddlesBoundary(Geo, 5, 2), Coords.StraddlesBoundary(Geo, 4, 2), Coords.StraddlesBoundary(Geo, 6, 2) });
            CollectionAssert.AreEqual(new[] { false, true, false }, new[] { Coords.StraddlesBoundary(Geo, 5, 1), Coords.StraddlesBoundary(Geo, 4, 3), Coords.StraddlesBoundary(Geo, 3, 3) });
            CollectionAssert.IsEmpty(Coords.Neighbors4(Geo, 3, 8)); // §0 neighbourhood is between board cells
        }

        [Test(Description = "K-04 K-12 a gap in the bottom row (y = 0, wall 2): B1 at (5,0) enters the rail in 1 step; FREE it needs 5 (up to row 2, over, down)")]
        public void K04_K12_a_gap_in_the_bottom_row_B1_at_5_0_enters_the_rail_in_1_step_FREE_it_needs_5()
        {
            Assert.AreEqual(0b10, Coords.ClosedBoundaryMask(Geo, 2, Coords.OpenRailMask(Geo, 0, 1, true)));
            var s = Init(new LevelSpec { Wall = Wall(2, Gap(0)), Pieces = Ps(Pc("B1_0", "W", 5, 0)) });
            var d = Drag(s, 0);
            CollectionAssert.Contains(Keys(d.Neighbours(N(5, 0))), "R0(6,0)");
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(5, 0))), "F(6,0)");
            Assert.AreEqual(1, d.DistanceFromStart(R(0, 6, 0)));
            Assert.AreEqual(5, d.DistanceFromStart(N(6, 0)));
            AssertDrop(d.Classify(R(0, 6, 0)), DropKinds.SiteRail, row: 7, gap: 0);
            AssertDrop(d.Classify(N(6, 0)), DropKinds.SiteFree, row: 6);
        }

        [Test(Description = "K-04 K-12 a gap closed by its rule (canPassGap false) is a closed boundary row for both modes; the other gap stays open")]
        public void K04_K12_a_gap_closed_by_its_rule_is_a_closed_boundary_row_for_both_modes_the_other_gap_stays_open()
        {
            var wall = Wall(6, Gap(1), Gap(4));
            Assert.AreEqual(0b111101, Coords.ClosedBoundaryMask(Geo, 6, Coords.OpenRailMask(Geo, 1, 1, true) | Coords.OpenRailMask(Geo, 4, 1, false)));
            var onlyFirst = new DragRules(canPassGap: (st, gap, id) => gap == 0);
            var s = Init(new LevelSpec { Wall = wall, Pieces = Ps(Pc("B1_0", "W", 5, 4)) });
            var d = Drag(s, 0, onlyFirst);
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(5, 4))), "F(6,4)");
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(N(5, 4))), "R1(6,4)");
            CollectionAssert.IsEmpty(d.ReachableNodes().Where(n => n.Mode == Movement.RailMode(1)).ToArray());
            Assert.AreEqual(4, d.DistanceFromStart(R(0, 6, 1))); // down to (5,1), then through the open first gap
            Assert.IsTrue(d.IsReachable(N(6, 4))); // over the wall at row 6 and down
            Assert.AreEqual(5, d.DistanceFromStart(N(6, 4)));
            // without the hook both gaps are open (W1 static gaps are always open)
            Assert.AreEqual(1, Drag(s, 0).DistanceFromStart(R(1, 6, 4)));
        }

        [Test(Description = "K-12 two gaps: on the rail the block never changes row, so going from gap 0 to gap 1 means back through the yard; the release reports its own gap")]
        public void K12_two_gaps_on_the_rail_the_block_never_changes_row()
        {
            var s = Init(new LevelSpec { Wall = Wall(6, Gap(1), Gap(4)), Pieces = Ps(Pc("B1_0", "W", 5, 1)) });
            var d = Drag(s, 0);
            d.MoveTo(R(0, 6, 1));
            CollectionAssert.AreEqual(new[] { "F(5,1)", "R0(7,1)" }, Keys(d.Neighbours(R(0, 6, 1))));
            Assert.AreEqual(5, d.DistanceFromCurrent(R(1, 6, 4))); // R0(6,1) → F(5,1) → F(5,2) → F(5,3) → F(5,4) → R1(6,4)
            Assert.IsFalse(d.IsReachable(R(0, 6, 4))); // row 4 is outside gap 0
            Assert.IsFalse(d.IsReachable(R(1, 6, 1)));
            AssertDrop(d.Classify(R(1, 6, 4)), DropKinds.SiteRail, row: 7, gap: 1);
            AssertDrop(d.Classify(R(0, 7, 1)), DropKinds.SiteRail, row: 7, gap: 0);
        }

        [Test(Description = "K-12 \"Şantiye tarafından ray kipine girilemez\": a 2-wide block lowered beside its gap row on the site cannot step back into the gap")]
        public void K12_a_2_wide_block_lowered_beside_its_gap_row_on_the_site_cannot_step_back_into_the_gap()
        {
            var s = Init(new LevelSpec { Wall = Wall(5, Gap(3)), Pieces = Ps(Pc("D2_90", "W", 0, 0)) });
            var d = Drag(s, 0);
            d.MoveTo(N(6, 3));
            // left would be the straddle (5,3)|(6,3): closed for FREE (row 3 < 5), and a FREE node on the site has no RAIL edge
            CollectionAssert.AreEqual(new[] { "F(6,2)", "F(6,4)" }, Keys(d.Neighbours(N(6, 3))));
            Assert.IsFalse(d.IsReachable(N(5, 3)));
            // the rail straddle exists, but only from the yard side: up, over the wall, down to (4,3), right
            Assert.AreEqual(7, d.DistanceFromCurrent(R(0, 5, 3)));
            var path = Keys(d.PathTo(R(0, 5, 3))!);
            Assert.AreEqual("F(4,3)", path[path.Length - 2]);
        }

        // --- round 2 — K-13 on the site ------------------------------------------------------------------------------------

        [Test(Description = "K-13 K-11 even over a low wall a block cannot slide sideways under an overhang: the straddles under debris at (6,4) are no positions")]
        public void K13_K11_even_over_a_low_wall_a_block_cannot_slide_sideways_under_an_overhang()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = WW(5),
                Pieces = Ps(Pc("D2_90", "W", 0, 2), Pc("B1_0", "W", 0, 4)),
                Debris = new[] { new D("B1_0", "R", 6, 4) },
            });
            var wide = Drag(s, 0);
            // the boundary rows 2–3 are open (≥ height 2), but (6,2)/(6,3) lie under the filled (6,4): no open sky
            Assert.IsTrue(wide.IsReachable(N(4, 2)));
            Assert.IsFalse(wide.IsReachable(N(5, 2)));
            Assert.IsFalse(wide.IsReachable(N(5, 3)));
            Assert.IsTrue(wide.IsReachable(N(5, 5)));
            for (int y = 0; y <= 4; y++) Assert.IsFalse(wide.IsReachable(N(6, y)), "D2_90 F(6," + y + ")");
            var small = Drag(s, 1);
            Assert.IsTrue(small.IsReachable(N(5, 2)));
            CollectionAssert.DoesNotContain(Keys(small.Neighbours(N(5, 2))), "F(6,2)");
            // from column 7 (empty, open sky) the block cannot slide left under the overhang either
            Assert.IsTrue(small.IsReachable(N(7, 2)));
            CollectionAssert.DoesNotContain(Keys(small.Neighbours(N(7, 2))), "F(6,2)");
            for (int y = 0; y <= 3; y++) Assert.IsFalse(small.IsReachable(N(6, y)), "B1 F(6," + y + ")");
        }

        // --- round 2 — GDD §14.1 drag signals with a debris rail start -------------------------------------------------------

        [Test(Description = "K-12 K-11 debris pulled out through its own open gap emits neither gapPass nor overWall (GDD §14.1/3 example); lifted over the wall it emits overWall once")]
        public void K12_K11_debris_pulled_out_through_its_own_open_gap_emits_neither_gapPass_nor_overWall()
        {
            LevelSpec Spec() => new LevelSpec
            {
                Wall = Wall(5, Gap(3)),
                Plan = WW(4),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("D2_90", "R", 6, 3) },
            };
            var s = Init(Spec());
            var viaRail = Drag(s, 1);
            Assert.IsTrue(viaRail.CanCrossWall);
            Assert.IsTrue(viaRail.CanEnterRail); // from (4,3) back into the gap
            var o = viaRail.Follow(4, 3);
            Assert.AreEqual(N(4, 3), o.Node);
            Assert.IsFalse(o.CrossedWall); // the cells crossed the boundary in RAIL mode
            Assert.IsFalse(o.EnteredRail);

            var overWall = Drag(s, 1);
            var up = overWall.MoveTo(N(6, 5));
            CollectionAssert.AreEqual(new[] { "F(6,4)", "F(6,5)" }, Keys(up.Path));
            Assert.IsFalse(up.CrossedWall);
            var half = overWall.MoveTo(N(5, 5));
            Assert.IsTrue(half.CrossedWall);
            Assert.IsFalse(half.EnteredRail);
            Assert.IsFalse(overWall.MoveTo(N(4, 5)).CrossedWall); // once per drag
            AssertDrop(overWall.Classify(), DropKinds.Yard, row: 2);
        }

        // --- round 3 — K-05 blockedByWallHeight ---------------------------------------------------------------------------

        [Test(Description = "K-05 the signal fires exactly for blocks taller than 10 − height: every non-heavy shape × wall heights 0–8, finger at the highest site anchor")]
        public void K05_the_signal_fires_exactly_for_blocks_taller_than_10_minus_height()
        {
            // GDD K-05: "Sınırı serbest kipte geçen her hücre y ≥ height olmalıdır … Yapışkan takibin (K-08) duvar tepesinde
            // durması bir sunum olayıdır (blockedByWallHeight)"; TECH §4.4: box height h > 10 − wall.height.
            int fired = 0;
            int quiet = 0;
            foreach (var shape in CanonicalShapes.Where(sh => !sh.Heavy && sh.W <= 2))
            {
                for (int height = 0; height <= 8; height++)
                {
                    string label = shape.Id + " height " + height;
                    var d = Drag(Init(new LevelSpec { Wall = Wall(height), Pieces = Ps(Pc(shape.Id, "W", 0, 0)) }), 0);
                    var target = N(6, 10 - shape.H);
                    var r = d.Follow(target.Ix, target.Iy);
                    bool tall = shape.H > 10 - height;
                    Assert.AreEqual(tall, r.BlockedByWallHeight, label);
                    if (tall)
                    {
                        fired++;
                        Assert.AreEqual(Movement.Free, r.Node.Mode, label);
                        Assert.Less(r.Node.Ix, 6, label); // the block waits at the wall, never on the site
                    }
                    else
                    {
                        quiet++;
                        Assert.AreEqual(target, r.Node, label);
                    }
                    // a presentation event: the rule is unchanged, the block sits on the K-08 nearest node
                    Assert.AreEqual(d.Nearest(target.Ix, target.Iy), r.Node, label);
                }
            }
            Assert.Greater(fired, 0);
            Assert.Greater(quiet, fired);
        }

        [Test(Description = "K-05 N3 a 3-tall block that reaches the site only by rail still gets blockedByWallHeight over the site crane, then rides the rail with no second signal")]
        public void K05_N3_a_3_tall_block_that_reaches_the_site_only_by_rail_still_gets_blockedByWallHeight()
        {
            // TECH §4.4 equivalence: "R'de sınırı FREE geçen düğüm yok ve h > 10 − height" — the rail does not change that.
            var s = Init(new LevelSpec { Wall = Wall(8, Gap(2, 3)), Plan = WW(3), Pieces = Ps(Pc("I3_0", "W", 0, 0)) });
            var d = Drag(s, 0);
            Assert.IsFalse(d.CanCrossWall);
            Assert.IsTrue(d.CanEnterRail);
            var over = d.Follow(6, 7);
            Assert.AreEqual(N(5, 7), over.Node); // stops at the top of the wall
            Assert.IsTrue(over.BlockedByWallHeight);
            Assert.IsFalse(over.CrossedWall);
            var rail = d.Follow(6, 2);
            CollectionAssert.AreEqual(new[] { "F(5,6)", "F(5,5)", "F(5,4)", "F(5,3)", "F(5,2)", "R0(6,2)" }, Keys(rail.Path));
            Assert.IsTrue(rail.EnteredRail);
            Assert.IsFalse(rail.BlockedByWallHeight);
            AssertDrop(d.Classify(), DropKinds.SiteRail, row: 7, gap: 0);
            var back = d.Follow(6.5, 9);
            Assert.AreEqual(N(5, 7), back.Node);
            Assert.IsFalse(back.BlockedByWallHeight); // at most once per drag
        }

        [Test(Description = "K-05 GDD §14.1 overWall: S4_0 and T4_0 hook over an 8-high wall top at (5,7) — a FREE boundary crossing (canCrossWall, crossedWall) that never becomes a site position; release there cancels (row 4)")]
        public void K05_GDD_14_1_overWall_S4_0_and_T4_0_hook_over_an_8_high_wall_top_at_5_7()
        {
            foreach (var shape in new[] { "S4_0", "T4_0" })
            {
                var d = Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc(shape, "W", 0, 0)) }), 0);
                CollectionAssert.IsEmpty(d.ReachableNodes().Where(n => n.Ix >= 6).ToArray(), shape);
                CollectionAssert.AreEqual(new[] { "F(5,7)" }, Keys(d.ReachableNodes().Where(n => n.Ix == 5)), shape);
                // the cells past the boundary are all in the crane rows (K-05: crossing cells y ≥ height)
                var past = d.Cells(N(5, 7)).Where(c => c.X >= 6).ToArray();
                Assert.Greater(past.Length, 0, shape);
                Assert.IsTrue(past.All(c => c.Y >= 8), shape);
                // "tutulan bloğun bir hücresi serbest kipte duvar sınırını ilk kez geçtiği an" → overWall
                Assert.IsTrue(d.CanCrossWall, shape);
                var r = d.Follow(6, 7);
                Assert.AreEqual(N(5, 7), r.Node, shape);
                Assert.IsTrue(r.CrossedWall, shape);
                Assert.IsTrue(r.BlockedByWallHeight, shape);
                AssertCancel(d.Classify(), CancelReasons.Straddle, 4, shape);
            }
            // Z4_0 has its 2-row column on the right: it cannot even start to cross
            Assert.IsFalse(Drag(Init(new LevelSpec { Wall = Wall(8), Pieces = Ps(Pc("Z4_0", "W", 0, 0)) }), 0).CanCrossWall);
        }

        [Test(Description = "K-05 a 1-tall block held back by a full yard (not by the wall height) gets no blockedByWallHeight")]
        public void K05_a_1_tall_block_held_back_by_a_full_yard_gets_no_blockedByWallHeight()
        {
            var s = Init(new LevelSpec { Wall = Wall(8), Pieces = FullYard(Ps(Pc("B1_0", "W", 1, 0)), (0, 0)) });
            var d = Drag(s, 0);
            CollectionAssert.AreEqual(new[] { "F(0,0)", "F(1,0)" }, Keys(d.ReachableNodes()));
            var r = d.Follow(6.5, 9);
            Assert.AreEqual(N(1, 0), r.Node);
            Assert.IsFalse(r.BlockedByWallHeight);
        }

        // --- round 3 — two-way edges, debris on the rail, N14 --------------------------------------------------------------

        [Test(Description = "K-08 K-07 row 1 K-13 debris under an overhang (FREE start without open sky) leaves sideways or through its gap and can always be brought back to its start, in either mode")]
        public void K08_K07_row_1_K13_debris_under_an_overhang_leaves_sideways_or_through_its_gap_and_can_always_be_brought_back()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(5, Gap(3)),
                Plan = WW(6),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 3), new D("B1_0", "R", 7, 5) }, // overhang: (7,4) stays empty
            });
            CollectionAssert.AreEqual(new sbyte[] { -1, 5 }, Grid.SiteColumnTops(s, 1));
            var d = Drag(s, 1);
            CollectionAssert.AreEqual(new[] { "F(7,3)", "R0(7,3)" }, Keys(d.StartNodes).OrderBy(k => k, StringComparer.Ordinal).ToArray());
            // K-13: no other FREE position under the overhang; column 7 is open again only above (7,5)
            var col7 = new List<int>();
            for (int y = 0; y <= 9; y++) if (d.IsReachable(N(7, y))) col7.Add(y);
            CollectionAssert.AreEqual(new[] { 3, 6, 7, 8, 9 }, col7);
            Assert.IsFalse(d.IsReachable(R(0, 7, 4)));
            Assert.AreEqual(1, d.DistanceFromStart(N(6, 3))); // sideways on the site into open sky
            Assert.AreEqual(2, d.DistanceFromStart(N(5, 3))); // R0(7,3) → R0(6,3) → F(5,3) (K-12 debris exception)
            d.MoveTo(N(4, 0));
            Assert.Greater(d.DistanceFromCurrent(N(7, 3)), 0);
            Assert.AreEqual(N(7, 3), d.MoveTo(N(7, 3)).Node);
            AssertCancel(d.Classify(), CancelReasons.SameSpot, 1);
            d.MoveTo(N(6, 9));
            Assert.AreEqual(R(0, 7, 3), d.MoveTo(R(0, 7, 3)).Node);
            AssertCancel(d.Classify(), CancelReasons.SameSpot, 1);
        }

        [Test(Description = "K-08 every edge is two-way: from any node of R every node of R is reachable and BFS distances are symmetric (Bölüm 3 yard, debris boards)")]
        public void K08_every_edge_is_two_way_from_any_node_of_R_every_node_of_R_is_reachable_and_BFS_distances_are_symmetric()
        {
            var boards = new (LevelSpec spec, int[] ids)[]
            {
                (Level3(), new[] { 0, 1, 2, 4, 5, 9 }),
                (new LevelSpec
                {
                    Wall = Wall(5, Gap(3)),
                    Plan = WW(6),
                    Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("D2_90", "Y", 2, 3)),
                    Debris = new[] { new D("B1_0", "R", 7, 3), new D("B1_0", "R", 7, 5) },
                }, new[] { 1, 2, 3 }),
                (new LevelSpec
                {
                    Wall = Wall(2),
                    Plan = WW(5),
                    Pieces = Ps(Pc("C3_0", "W", 0, 0)),
                    Debris = new[] { new D("B1_0", "R", 6, 1), new D("B1_0", "R", 6, 4) },
                }, new[] { 0, 1, 2 }),
            };
            int pairs = 0;
            foreach (var (spec, ids) in boards)
            {
                var s = Init(spec);
                foreach (int id in ids)
                {
                    var d = Drag(s, id);
                    var all = d.ReachableNodes();
                    int step = Math.Max(1, all.Length / 10);
                    var sample = all.Where((n, i) => i % step == 0).Concat(d.StartNodes).ToArray();
                    var dist = new Dictionary<string, int>();
                    var unreachable = new List<string>();
                    var jumps = new List<string>();
                    foreach (var a in sample)
                    {
                        // K-08 "ışınlanmaz": each step is a unit translation, or a mode switch on the same cells (debris start)
                        var prev = d.Current;
                        foreach (var n in d.MoveTo(a).Path)
                        {
                            int dx = Math.Abs(n.Ix - prev.Ix);
                            int dy = Math.Abs(n.Iy - prev.Iy);
                            if (!(dx + dy == 1 || (dx + dy == 0 && n.Mode != prev.Mode))) jumps.Add(Key(prev) + " → " + Key(n));
                            prev = n;
                        }
                        Assert.AreEqual(0, d.DistanceFromCurrent(a));
                        foreach (var n in all) if (d.DistanceFromCurrent(n) < 0) unreachable.Add(Key(a) + " → " + Key(n));
                        foreach (var b in sample) dist[Key(a) + ">" + Key(b)] = d.DistanceFromCurrent(b);
                    }
                    CollectionAssert.IsEmpty(unreachable, "piece " + id);
                    CollectionAssert.IsEmpty(jumps, "piece " + id);
                    var asym = new List<string>();
                    foreach (var a in sample)
                        foreach (var b in sample)
                        {
                            pairs++;
                            int ab = dist[Key(a) + ">" + Key(b)];
                            int ba = dist[Key(b) + ">" + Key(a)];
                            if (ab != ba) asym.Add(Key(a) + "↔" + Key(b) + ": " + ab + " vs " + ba);
                        }
                    CollectionAssert.IsEmpty(asym, "piece " + id);
                }
            }
            Assert.Greater(pairs, 500);
        }

        [Test(Description = "K-12 K-11 a FREE block lowered to a gap row on the site stays FREE (no rail from the site side) and falls on release; its rail twin is 6 steps away through the yard")]
        public void K12_K11_a_FREE_block_lowered_to_a_gap_row_on_the_site_stays_FREE_and_falls_on_release()
        {
            var s = Init(new LevelSpec { Wall = Wall(5, Gap(3)), Plan = WW(4), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            var d = Drag(s, 0);
            d.MoveTo(N(6, 3));
            CollectionAssert.AreEqual(new[] { "F(7,3)", "F(6,2)", "F(6,4)" }, Keys(d.Neighbours(N(6, 3))));
            Assert.IsFalse(d.Follow(6, 3.05).Changed);
            Assert.AreEqual(N(6, 3), d.Current);
            Assert.AreEqual(6, d.DistanceFromCurrent(R(0, 6, 3))); // (6,4) (6,5) (5,5) (5,4) (5,3) R0(6,3)
            AssertDrop(d.Classify(N(6, 3)), DropKinds.SiteFree, row: 6);
            AssertFall(Gravity.ComputeFall(s, 0, N(6, 3)), FallResult.ModeFree, A(6, 0), distance: 3);
            AssertFall(Gravity.ComputeFall(s, 0, R(0, 6, 3)), FallResult.ModeRail, A(6, 3), distance: 0);
        }

        [Test(Description = "K-08 E-06 the gap state is frozen at pick time: a canPassGap answer that changes during the drag changes nothing; the next pick sees it")]
        public void K08_E06_the_gap_state_is_frozen_at_pick_time()
        {
            bool open = true;
            var rules = new DragRules(canPassGap: (st, g, id) => open);
            var s = Init(new LevelSpec { Wall = Wall(5, Gap(3)), Pieces = Ps(Pc("B1_0", "W", 5, 3)) });
            var d = Drag(s, 0, rules);
            open = false;
            Assert.IsTrue(d.IsReachable(R(0, 6, 3)));
            Assert.AreEqual(R(0, 6, 3), d.Follow(6, 3).Node);
            AssertDrop(d.Classify(), DropKinds.SiteRail, row: 7, gap: 0);
            CollectionAssert.AreEqual(new[] { "F(5,3)", "R0(7,3)" }, Keys(d.Neighbours(R(0, 6, 3))));
            var next = Drag(s, 0, rules);
            Assert.IsFalse(next.CanEnterRail);
            Assert.IsFalse(next.IsReachable(R(0, 6, 3)));
            Assert.AreEqual(5, next.DistanceFromStart(N(6, 3))); // over the wall: (5,4) (5,5) (6,5) (6,4) (6,3)
        }

        // --- round 3 — K-12 three gaps ------------------------------------------------------------------------------------

        [Test(Description = "K-12 K-04 three gaps on an 8-high wall: each rail is entered only in its own rows, the rails are joined only through the yard, and each release reports its own gap")]
        public void K12_K04_three_gaps_on_an_8_high_wall()
        {
            var wall = Wall(8, Gap(0), Gap(2), Gap(4, 2));
            Assert.AreEqual(0b11001010, Coords.ClosedBoundaryMask(Geo, 8,
                Coords.OpenRailMask(Geo, 0, 1, true) | Coords.OpenRailMask(Geo, 2, 1, true) | Coords.OpenRailMask(Geo, 4, 2, true)));
            var d = Drag(Init(new LevelSpec { Wall = wall, Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            var rails = d.ReachableNodes().Where(n => n.Mode != Movement.Free).ToArray();
            CollectionAssert.AreEqual(new[] { "R0(6,0)", "R1(6,2)", "R2(6,4)", "R2(6,5)" }, Keys(rails.Where(n => n.Ix == 6)));
            Assert.AreEqual(6, d.DistanceFromStart(R(0, 6, 0)));
            Assert.AreEqual(8, d.DistanceFromStart(R(1, 6, 2)));
            Assert.AreEqual(11, d.DistanceFromStart(R(2, 6, 5)));
            d.MoveTo(R(1, 7, 2));
            AssertDrop(d.Classify(), DropKinds.SiteRail, row: 7, gap: 1);
            // to the next gap up: back left into the yard, up 2 rows, right again
            CollectionAssert.AreEqual(new[] { "R1(6,2)", "F(5,2)", "F(5,3)", "F(5,4)", "R2(6,4)" }, Keys(d.PathTo(R(2, 6, 4))!));
            Assert.AreEqual(R(2, 6, 4), d.Follow(6, 4).Node);
            AssertDrop(d.Classify(), DropKinds.SiteRail, row: 7, gap: 2);
            // a 2-tall block fits only the 2-row gap
            var tall = Drag(Init(new LevelSpec { Wall = wall, Pieces = Ps(Pc("D2_0", "W", 0, 0)) }), 0);
            CollectionAssert.AreEqual(new[] { "R2(6,4)" }, Keys(tall.ReachableNodes().Where(n => n.Mode != Movement.Free && n.Ix == 6)));
        }

        // --- round 3 — K-08 tie-break 4, off-grid targets, interleaved sessions --------------------------------------------

        [Test(Description = "K-08 tie-break 4: equal distance, equal BFS steps, same mode and same row → the smaller x wins")]
        public void K08_tie_break_4_equal_distance_equal_BFS_steps_same_mode_and_same_row_the_smaller_x_wins()
        {
            var s = Init(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 2, 3), Pc("B1_0", "W", 2, 0), Pc("B1_0", "W", 2, 1), Pc("B1_0", "W", 2, 2)) });
            var d = Drag(s, 0);
            // p = (2,0) is taken; (1,0) and (3,0) are 1 away and 4 steps from (2,3); (2,1) is taken too
            Assert.AreEqual(4, d.DistanceFromCurrent(N(1, 0)));
            Assert.AreEqual(4, d.DistanceFromCurrent(N(3, 0)));
            Assert.AreEqual(N(1, 0), d.Nearest(2, 0));
            Assert.AreEqual(N(1, 0), d.Follow(2, 0).Node);
        }

        [Test(Description = "K-01 K-08 a target far outside the grid snaps to the nearest node of R; the block never leaves the 8 × 10 grid")]
        public void K01_K08_a_target_far_outside_the_grid_snaps_to_the_nearest_node_of_R()
        {
            var d = Drag(Init(new LevelSpec { Wall = Wall(0), Pieces = Ps(Pc("B1_0", "W", 0, 0)) }), 0);
            var visited = new List<DragNode>();
            foreach (var (px, py, want) in new (double px, double py, DragNode want)[] { (-5, 20, N(0, 9)), (20, -5, N(7, 0)), (3.4, 100, N(3, 9)), (-100, -100, N(0, 0)) })
            {
                var r = d.Follow(px, py);
                Assert.AreEqual(want, r.Node, "p = (" + px + ", " + py + ")");
                visited.AddRange(r.Path);
            }
            Assert.IsTrue(visited.All(n => Coords.InGrid(Geo, n.Ix, n.Iy)));
            Assert.Greater(visited.Count, 20);
        }

        [Test(Description = "K-08 interleaved sessions on different levels (0, 1 and 2 gaps) answer exactly as when each runs alone")]
        public void K08_interleaved_sessions_on_different_levels_answer_exactly_as_when_each_runs_alone()
        {
            var twoGaps = new LevelSpec { Wall = Wall(7, Gap(1, 2), Gap(4, 2)), Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "R", 4, 5)) };
            var plain = new LevelSpec { Wall = Wall(3), Pieces = Ps(Pc("C3_0", "W", 1, 1)) };
            var cases = new (Func<LevelSpec> spec, int id)[] { (Level3, 1), (Level3, 0), (() => twoGaps, 1), (() => plain, 0) };
            var targets = new (int x, int y)[] { (6, 2), (6, 8), (0, 9), (6, 0), (4, 4), (7, 9), (5, 1), (6, 5) };
            string Trace(FollowResult r) => string.Join(" ", Keys(r.Path)) + "|" + (r.CrossedWall ? "W" : "") + (r.EnteredRail ? "R" : "") + (r.BlockedByWallHeight ? "B" : "");
            var alone = cases.Select(c =>
            {
                var d = Drag(Init(c.spec()), c.id);
                return targets.Select(t => Trace(d.Follow(t.x, t.y))).ToArray();
            }).ToArray();
            var states = cases.Select(c => Init(c.spec())).ToArray();
            var sessions = cases.Select((c, i) => Drag(states[i], c.id)).ToArray();
            var mixed = cases.Select(c => new List<string>()).ToArray();
            foreach (var (x, y) in targets)
            {
                for (int i = 0; i < sessions.Length; i++)
                {
                    mixed[i].Add(Trace(sessions[i].Follow(x, y)));
                    Drag(states[(i + 1) % states.Length], cases[(i + 1) % cases.Length].id); // a BFS in between
                }
            }
            for (int i = 0; i < cases.Length; i++) CollectionAssert.AreEqual(alone[i], mixed[i], "case " + i);
        }

        // --- round 3 — Bölüm 1–10 start boards (level data) ----------------------------------------------------------------

        [Test(Description = "K-09 (a) Bölüm 1–10: a block is pickable iff one of its four unit translations is a valid K-08 position (independent cell oracle)")]
        public void K09_a_Bolum_1_10_a_block_is_pickable_iff_one_of_its_four_unit_translations_is_a_valid_K08_position()
        {
            int pickable = 0;
            int stuck = 0;
            for (int id = 1; id <= 10; id++)
            {
                var s = LevelState(id);
                int height = s.Lvl.WallHeight;
                // K-49 board of the level: yard x 0 … wy − 1, site x wy … wy + ws − 1, rows 0 … H + 1 (crane area)
                int wy = s.Lvl.Geo.Wy;
                int hy = s.Lvl.Geo.Hy;
                int cols = s.Lvl.Geo.Cols;
                int rows = s.Lvl.Geo.Rows;
                var gaps = TestPaths.LoadLevelJson(id)["wall"]!["gaps"]!.Items.Select(g => (y: g["y"]!.AsInt, size: g["size"]!.AsInt)).ToArray();
                int n = s.Lvl.Layout.Counts.Pieces;
                var occ = new Dictionary<string, int>();
                BoardCell[] CellsOfPiece(int pid)
                {
                    if (PieceZone(s, pid) != Zone.Yard) return new BoardCell[0];
                    return Shapes.ShapeByIndex(PieceShape(s, pid)).Cells.Select(c => new BoardCell(PieceX(s, pid) + c.X, PieceY(s, pid) + c.Y)).ToArray();
                }
                for (int pid = 0; pid < n; pid++) foreach (var c in CellsOfPiece(pid)) occ[c.X + "," + c.Y] = pid;
                for (int pid = 0; pid < n; pid++)
                {
                    if (PieceZone(s, pid) != Zone.Yard) continue;
                    var cells = CellsOfPiece(pid);
                    // K-44 / DL-2R-08: the Ağır Yük only slides inside the yard (no air above it, no crane area, no site)
                    bool cargo = Movement.IsCargoShape(Shapes.ShapeByIndex(PieceShape(s, pid)));
                    bool Free(int x, int y)
                    {
                        bool inside = cargo ? x >= 0 && x < wy && y >= 0 && y < hy : x >= 0 && x < cols && y >= 0 && y < rows;
                        return inside && (!occ.TryGetValue(x + "," + y, out int o) || o == pid);
                    }
                    bool valid = false;
                    foreach (var (dx, dy) in new (int dx, int dy)[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        var moved = cells.Select(c => new BoardCell(c.X + dx, c.Y + dy)).ToArray();
                        if (!moved.All(c => Free(c.X, c.Y))) continue;
                        // FREE: every cell that changes side crosses in a row y ≥ height; every row cut by the block is open
                        bool crossOk = cells.All(c => !(c.X == wy - 1 && c.X + dx == wy) || c.Y >= height);
                        var rowsCut = new HashSet<int>(moved.Where(c => c.X < wy).Select(c => c.Y));
                        bool cutOk = moved.All(c => c.X < wy || !rowsCut.Contains(c.Y) || c.Y >= height);
                        if (crossOk && cutOk) valid = true;
                        // RAIL (K-12): right, from fully in the yard, every row of the block inside one gap
                        if (!cargo && dx == 1 && cells.All(c => c.X < wy) && moved.Any(c => c.X >= wy))
                            if (gaps.Any(g => cells.All(c => c.Y >= g.y && c.Y < g.y + g.size))) valid = true;
                    }
                    bool got = Movement.TryBeginDrag(s, pid).Ok;
                    Assert.AreEqual(valid, got, "Bölüm " + id + " piece " + pid);
                    if (valid) pickable++;
                    else stuck++;
                }
            }
            Assert.Greater(pickable, 10);
            Assert.Greater(stuck, 10);
        }
    }
}
