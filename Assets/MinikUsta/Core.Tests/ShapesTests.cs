#nullable enable
// Port of tests/core/shapes.test.ts
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class ShapesTests
    {
        /// <summary>TECH_DESIGN §3.3, transcribed literally: id → [cells, "w×h", heavy].</summary>
        private static readonly Dictionary<string, (string cells, string box, bool heavy)> TechTable = new Dictionary<string, (string, string, bool)>
        {
            { "B1_0", ("(0,0)", "1x1", false) },
            { "D2_0", ("(0,0)(0,1)", "1x2", false) },
            { "D2_90", ("(0,0)(1,0)", "2x1", false) },
            { "I3_0", ("(0,0)(0,1)(0,2)", "1x3", false) },
            { "I3_90", ("(0,0)(1,0)(2,0)", "3x1", true) },
            { "I4_0", ("(0,0)(0,1)(0,2)(0,3)", "1x4", false) },
            { "I4_90", ("(0,0)(1,0)(2,0)(3,0)", "4x1", true) },
            { "O4_0", ("(0,0)(1,0)(0,1)(1,1)", "2x2", false) },
            { "C3_0", ("(0,0)(1,0)(0,1)", "2x2", false) },
            { "C3_90", ("(0,0)(0,1)(1,1)", "2x2", false) },
            { "C3_180", ("(1,0)(0,1)(1,1)", "2x2", false) },
            { "C3_270", ("(0,0)(1,0)(1,1)", "2x2", false) },
            { "S4_0", ("(0,0)(0,1)(1,1)(1,2)", "2x3", false) },
            { "S4_90", ("(1,0)(2,0)(0,1)(1,1)", "3x2", true) },
            { "I5_0", ("(0,0)(1,0)(2,0)(3,0)(4,0)", "5x1", true) },
            { "I5_90", ("(0,0)(0,1)(0,2)(0,3)(0,4)", "1x5", true) },
            { "Q9_0", ("(0,0)(1,0)(2,0)(0,1)(1,1)(2,1)(0,2)(1,2)(2,2)", "3x3", true) },
            { "L4_0", ("(0,0)(1,0)(0,1)(0,2)", "2x3", false) },
            { "L4_90", ("(0,0)(0,1)(1,1)(2,1)", "3x2", true) },
            { "L4_180", ("(1,0)(1,1)(0,2)(1,2)", "2x3", false) },
            { "L4_270", ("(0,0)(1,0)(2,0)(2,1)", "3x2", true) },
            { "J4_0", ("(0,0)(1,0)(1,1)(1,2)", "2x3", false) },
            { "J4_90", ("(0,0)(1,0)(2,0)(0,1)", "3x2", true) },
            { "J4_180", ("(0,0)(0,1)(0,2)(1,2)", "2x3", false) },
            { "J4_270", ("(2,0)(0,1)(1,1)(2,1)", "3x2", true) },
            { "T4_0", ("(0,0)(0,1)(1,1)(0,2)", "2x3", false) },
            { "T4_90", ("(1,0)(0,1)(1,1)(2,1)", "3x2", true) },
            { "T4_180", ("(1,0)(0,1)(1,1)(1,2)", "2x3", false) },
            { "T4_270", ("(0,0)(1,0)(2,0)(1,1)", "3x2", true) },
            { "Z4_0", ("(1,0)(0,1)(1,1)(0,2)", "2x3", false) },
            { "Z4_90", ("(0,0)(1,0)(1,1)(2,1)", "3x2", true) },
        };

        /// <summary>TECH §3.3 "(=…)" aliases: same cell set as the listed canonical id.</summary>
        private static readonly Dictionary<string, string> TechAliases = new Dictionary<string, string>
        {
            { "B1_90", "B1_0" }, { "B1_180", "B1_0" }, { "B1_270", "B1_0" }, { "D2_180", "D2_0" }, { "D2_270", "D2_90" },
            { "I3_180", "I3_0" }, { "I3_270", "I3_90" }, { "I4_180", "I4_0" }, { "I4_270", "I4_90" }, { "O4_90", "O4_0" },
            { "O4_180", "O4_0" }, { "O4_270", "O4_0" }, { "S4_180", "S4_0" }, { "S4_270", "S4_90" }, { "Z4_180", "Z4_0" },
            { "Z4_270", "Z4_90" }, { "I5_180", "I5_0" }, { "I5_270", "I5_90" }, { "Q9_90", "Q9_0" }, { "Q9_180", "Q9_0" },
            { "Q9_270", "Q9_0" },
        };

        private static string CellsText(string id) => string.Join("", Shapes.ShapeById(id).Cells.Select(c => "(" + c.X + "," + c.Y + ")"));

        [Test(Description = "K-44 generates 52 shape ids in kind × rotation index order")]
        public void K44_generates_52_shape_ids_in_kind_x_rotation_index_order()
        {
            Assert.AreEqual(52, Shapes.All.Length);
            Assert.AreEqual(52, Shapes.Ids.Length);
            for (int i = 0; i < Shapes.All.Length; i++)
            {
                var s = Shapes.All[i];
                Assert.AreEqual(i, s.Index);
                Assert.AreSame(s, Shapes.ShapeByIndex(i));
                Assert.AreEqual(ShapeKinds.All[i / 4], s.Kind);
            }
            Assert.AreEqual(52, new HashSet<string>(Shapes.Ids).Count);
        }

        [Test(Description = "K-44 generated table equals TECH_DESIGN §3.3 cell by cell")]
        public void K44_generated_table_equals_TECH_DESIGN_3_3_cell_by_cell()
        {
            foreach (var kv in TechTable)
            {
                var s = Shapes.ShapeById(kv.Key);
                Assert.AreEqual(kv.Value.cells, CellsText(s.Id), kv.Key);
                Assert.AreEqual(kv.Value.box, s.W + "x" + s.H, kv.Key);
                Assert.AreEqual(kv.Value.heavy, s.Heavy, kv.Key);
            }
            foreach (var kv in TechAliases)
            {
                Assert.AreEqual(CellsText(kv.Value), CellsText(kv.Key), kv.Key);
                Assert.AreEqual(kv.Value, Shapes.ShapeById(kv.Key).Canonical, kv.Key);
            }
            Assert.AreEqual(52, TechTable.Count + TechAliases.Count);
        }

        [Test(Description = "K-44 0° cells come from BRIEF §5 and rotation is clockwise with bottom-left normalisation")]
        public void K44_zero_degree_cells_come_from_BRIEF_5_and_rotation_is_clockwise_with_bottom_left_normalisation()
        {
            foreach (var kind in ShapeKinds.All)
            {
                var s = Shapes.ShapeById(kind + "_0");
                var brief = Shapes.BaseCells[kind].Select(c => new Cell(c[0], c[1])).OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
                CollectionAssert.AreEqual(brief, s.Cells);
            }
            // (x, y) → (y, −x): L4_0 foot (1,0) goes below the column, so L4_90 is "X.. / XXX" (top row left).
            CollectionAssert.AreEqual(Shapes.ShapeById("L4_90").Cells, Shapes.RotateClockwise(Shapes.ShapeById("L4_0").Cells));
            // GDD K-44 example: C3_90 = (0,0)(0,1)(1,1), view "XX / X." (top row left).
            Assert.AreEqual("(0,0)(0,1)(1,1)", CellsText("C3_90"));
            // Four clockwise steps return to 0°.
            foreach (var kind in ShapeKinds.All)
            {
                Cell[] cells = Shapes.ShapeById(kind + "_0").Cells;
                for (int i = 0; i < 4; i++) cells = Shapes.RotateClockwise(cells);
                CollectionAssert.AreEqual(Shapes.ShapeById(kind + "_0").Cells, cells);
            }
        }

        [Test(Description = "K-44 BRIEF §5 notes hold for the generated table")]
        public void K44_BRIEF_5_notes_hold_for_the_generated_table()
        {
            Assert.AreEqual(2, Shapes.ShapeById("D2_90").W);
            Assert.AreEqual(1, Shapes.ShapeById("D2_90").H);
            Assert.IsTrue(Shapes.ShapeById("I3_90").Heavy && Shapes.ShapeById("I4_90").Heavy);
            Assert.IsFalse(Shapes.ShapeById("I3_0").Heavy || Shapes.ShapeById("I4_0").Heavy);
            foreach (int r in new[] { 0, 90, 180, 270 }) Assert.IsFalse(Shapes.ShapeById("C3_" + r).Heavy);
            foreach (var k in new[] { "L4", "J4", "T4", "S4", "Z4" })
            {
                foreach (int r in new[] { 0, 180 }) Assert.IsFalse(Shapes.ShapeById(k + "_" + r).Heavy, k + "_" + r);
                foreach (int r in new[] { 90, 270 }) Assert.IsTrue(Shapes.ShapeById(k + "_" + r).Heavy, k + "_" + r);
            }
            foreach (int r in new[] { 0, 90, 180, 270 })
            {
                Assert.IsTrue(Shapes.ShapeById("I5_" + r).Heavy);
                Assert.IsTrue(Shapes.ShapeById("Q9_" + r).Heavy);
            }
            Assert.AreEqual(5, Shapes.ShapeById("I5_0").W);
        }

        [Test(Description = "K-44 heavy = width ≥ 3 or kind I5/Q9, so every non-heavy shape is at most 2 wide")]
        public void K44_heavy_is_width_ge_3_or_kind_I5_Q9_so_every_non_heavy_shape_is_at_most_2_wide()
        {
            foreach (var s in Shapes.All)
            {
                Assert.AreEqual(s.W >= 3 || Shapes.AlwaysHeavy.Contains(s.Kind), s.Heavy);
                if (!s.Heavy) Assert.LessOrEqual(s.W, 2);
            }
            Assert.AreEqual(1, Shapes.ShapeById("I5_90").W);
            Assert.IsTrue(Shapes.ShapeById("I5_90").Heavy);
        }

        [Test(Description = "K-44 symmetric aliases reduce to the canonical id")]
        public void K44_symmetric_aliases_reduce_to_the_canonical_id()
        {
            Assert.AreEqual("O4_0", Shapes.ShapeById("O4_270").Canonical);
            Assert.AreEqual("D2_0", Shapes.ShapeById("D2_180").Canonical);
            Assert.AreEqual("C3_270", Shapes.ShapeById("C3_270").Canonical);
            foreach (var s in Shapes.All)
            {
                var c = Shapes.ShapeById(s.Canonical);
                Assert.AreEqual(c.Id, c.Canonical);
                Assert.AreEqual(s.CanonicalIndex, c.Index);
                CollectionAssert.AreEqual(s.Cells, c.Cells);
                Assert.LessOrEqual(c.Index, s.Index);
            }
        }

        [Test(Description = "K-44 I5_90 and I5_270 are the forbidden data orientations")]
        public void K44_I5_90_and_I5_270_are_the_forbidden_data_orientations()
        {
            var f = Shapes.ForbiddenShapes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "I5_270", "I5_90" }, f);
        }

        [Test(Description = "K-44 kind unlock table and heavy level threshold")]
        public void K44_kind_unlock_table_and_heavy_level_threshold()
        {
            var expected = new Dictionary<string, int>
            {
                { "B1", 1 }, { "D2", 1 }, { "O4", 1 }, { "C3", 1 }, { "I3", 2 }, { "L4", 2 }, { "J4", 2 }, { "T4", 3 }, { "S4", 3 },
                { "Z4", 3 }, { "I4", 4 }, { "I5", 1 }, { "Q9", 1 },
            };
            Assert.AreEqual(expected.Count, Shapes.KindFirstChapter.Count);
            foreach (var kv in expected) Assert.AreEqual(kv.Value, Shapes.KindFirstChapter[kv.Key], kv.Key);
            Assert.AreEqual(8, Shapes.HeavyMinLevelId);
        }

        [Test(Description = "K-44 row, column masks and column bounds agree with the cells")]
        public void K44_row_column_masks_and_column_bounds_agree_with_the_cells()
        {
            foreach (var s in Shapes.All)
            {
                Assert.AreEqual(s.H, s.Rows.Length);
                Assert.AreEqual(s.W, s.ColRows.Length);
                Assert.AreEqual(s.Cells.Length, s.CellCount);
                for (int x = 0; x < s.W; x++)
                {
                    var ys = s.Cells.Where(c => c.X == x).Select(c => c.Y).ToArray();
                    Assert.Greater(ys.Length, 0, s.Id + " column " + x + " occupied");
                    Assert.AreEqual(ys.Min(), s.ColBottom[x]);
                    Assert.AreEqual(ys.Max(), s.ColTop[x]);
                    Assert.AreEqual(ys.Aggregate(0, (m, y) => m | (1 << y)), s.ColRows[x]);
                }
                for (int y = 0; y < s.H; y++)
                    Assert.AreEqual(s.Cells.Where(c => c.Y == y).Aggregate(0, (m, c) => m | (1 << c.X)), s.Rows[y]);
            }
            CollectionAssert.AreEqual(new[] { 0, 1 }, Shapes.ShapeById("S4_0").ColBottom);
        }

        [Test(Description = "K-44 id helpers")]
        public void K44_id_helpers()
        {
            Assert.IsTrue(Shapes.IsShapeId("C3_90"));
            Assert.IsFalse(Shapes.IsShapeId("C3_45"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Shapes.ShapeById("X1_0"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Shapes.ShapeByIndex(52));
            Assert.AreEqual("L4_90", Shapes.RotateShapeId("L4_0", 1));
            Assert.AreEqual("L4_0", Shapes.RotateShapeId("L4_270", 1));
            Assert.AreEqual("L4_270", Shapes.RotateShapeId("L4_0", -1));
        }
    }
}
