#nullable enable
// Port of tests/core/coords.test.ts
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class CoordsTests
    {
        private static readonly BoardGeo G = Geometry.DefaultGeo;

        private static int[] Rows(int mask) => Enumerable.Range(0, 10).Where(y => ((mask >> y) & 1) != 0).ToArray();

        [Test(Description = "K-01 K-49 the core grid is 9 × 10 (D-103): board y 0–7 plus crane rows 8–9; internal = global coordinates")]
        public void K01_the_core_grid_is_9x10_board_y_0_7_plus_crane_rows_8_9_internal_equals_global()
        {
            CollectionAssert.AreEqual(new[] { 9, 10, 90, 8 }, new[] { Geometry.MaxCols, Geometry.MaxRows, Geometry.MaxCols * Geometry.MaxRows, G.H });
            Assert.AreEqual(84, Coords.CellIndex(3, 9));
            Assert.AreEqual(3, Coords.CellX(84));
            Assert.AreEqual(9, Coords.CellY(84));
        }

        [Test(Description = "K-01 D2_0 anchored at (3,8) fits, anchored at (3,9) leaves the grid")]
        public void K01_D2_0_anchored_at_3_8_fits_anchored_at_3_9_leaves_the_grid()
        {
            var d2 = Shapes.ShapeById("D2_0");
            bool Fits(int ax, int ay) => d2.Cells.All(c => Coords.InGrid(G, ax + c.X, ay + c.Y));
            Assert.IsTrue(Fits(3, 8));
            Assert.IsFalse(Fits(3, 9));
            Assert.IsFalse(Coords.InGrid(G, -1, 0) || Coords.InGrid(G, 8, 0) || Coords.InGrid(G, 0, 10));
        }

        [Test(Description = "K-02 K-03 K-05 regions: yard x 0–5, site x 6–7, crane area y 8–9")]
        public void K02_K03_K05_regions_yard_x_0_5_site_x_6_7_crane_area_y_8_9()
        {
            Assert.IsTrue(Coords.IsYardCell(G, 5, 7) && !Coords.IsYardCell(G, 6, 7) && !Coords.IsYardCell(G, 5, 8));
            Assert.IsTrue(Coords.IsSiteCell(G, 6, 0) && Coords.IsSiteCell(G, 7, 7) && !Coords.IsSiteCell(G, 7, 8) && !Coords.IsSiteCell(G, 5, 0));
            Assert.IsTrue(Coords.IsCraneCell(G, 0, 8) && Coords.IsCraneCell(G, 7, 9) && !Coords.IsCraneCell(G, 0, 7));
        }

        [Test(Description = "K-04 height 6 with an open gap y=2 size 2: rows 0–1 closed, 2–3 gap, 4–5 closed, 6–9 open")]
        public void K04_height_6_with_an_open_gap_y2_size2_rows_0_1_closed_2_3_gap_4_5_closed_6_9_open()
        {
            int gap = Coords.OpenRailMask(G, 2, 2, true);
            CollectionAssert.AreEqual(new[] { 2, 3 }, Rows(gap));
            CollectionAssert.AreEqual(new[] { 0, 1, 4, 5 }, Rows(Coords.ClosedBoundaryMask(G, 6, gap)));
            CollectionAssert.AreEqual(new[] { 6, 7, 8, 9 }, Rows(Coords.OpenFreeMask(G, 6)));
        }

        [Test(Description = "K-04 a closed gap adds nothing; crane rows are always open")]
        public void K04_a_closed_gap_adds_nothing_crane_rows_are_always_open()
        {
            Assert.AreEqual(0, Coords.OpenRailMask(G, 2, 2, false));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, Rows(Coords.ClosedBoundaryMask(G, 8, 0)));
            CollectionAssert.AreEqual(new[] { 8, 9 }, Rows(Coords.OpenFreeMask(G, 8)));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, Rows(Coords.OpenFreeMask(G, 0)));
            Assert.AreEqual(0, Coords.ClosedBoundaryMask(G, 0, 0));
        }

        [Test(Description = "K-05 a 3-tall piece cannot clear an 8-high wall, a 2-tall one can (open height rule)")]
        public void K05_a_3_tall_piece_cannot_clear_an_8_high_wall_a_2_tall_one_can()
        {
            bool CanCross(string id, int height)
            {
                var s = Shapes.ShapeById(id);
                for (int iy = 0; iy + s.H <= G.Rows; iy++)
                    if (Coords.BoundaryAllows(s.ColRows[0] << iy, Coords.OpenFreeMask(G, height))) return true;
                return false;
            }
            Assert.IsFalse(CanCross("I3_0", 8));
            Assert.IsTrue(CanCross("D2_0", 8));
            Assert.IsTrue(CanCross("I3_0", 7));
        }

        [Test(Description = "K-04 boundary straddle test and row ranges")]
        public void K04_boundary_straddle_test_and_row_ranges()
        {
            Assert.IsTrue(Coords.StraddlesBoundary(G, 5, 2));
            Assert.IsFalse(Coords.StraddlesBoundary(G, 4, 2));
            Assert.IsFalse(Coords.StraddlesBoundary(G, 6, 2));
            Assert.IsFalse(Coords.StraddlesBoundary(G, 5, 1));
            Assert.AreEqual(Coords.RowRangeMask(G, 8, 2), Coords.RowRangeMask(G, 8, 5));
            Assert.AreEqual(0, Coords.RowRangeMask(G, 3, 0));
        }

        [Test(Description = "E-46 (5,y) and (6,y) are never neighbours")]
        public void E46_5y_and_6y_are_never_neighbours()
        {
            for (int y = 0; y < G.H; y++)
            {
                Assert.IsFalse(Coords.Neighbors4(G, 5, y).Any(n => n.Ix == 6));
                Assert.IsFalse(Coords.Neighbors4(G, 6, y).Any(n => n.Ix == 5));
            }
            CollectionAssert.AreEqual(new[] { new Anchor(4, 2), new Anchor(5, 1), new Anchor(5, 3) }, Coords.Neighbors4(G, 5, 2));
            CollectionAssert.AreEqual(new[] { new Anchor(1, 0), new Anchor(0, 1) }, Coords.Neighbors4(G, 0, 0));
            CollectionAssert.AreEqual(new Anchor[0], Coords.Neighbors4(G, 3, 8));
        }

        [Test(Description = "drag node codes round-trip (mode * 10 + iy) * 9 + ix (TECH §2.1, D-103)")]
        public void Drag_node_codes_round_trip()
        {
            foreach (int mode in new[] { 0, 1, 3 })
                for (int iy = 0; iy < 10; iy++)
                    for (int ix = 0; ix < 9; ix++)
                    {
                        var node = new DragNode(ix, iy, mode);
                        Assert.AreEqual(node, Coords.DragNodeFromCode(Coords.DragNodeCode(node)));
                    }
            Assert.AreEqual((1 * 10 + 3) * 9 + 6, Coords.DragNodeCode(new DragNode(6, 3, 1)));
        }
    }
}
