#nullable enable
// Port of src/core/shapes.ts (web repo, source of truth).
using System;
using System.Collections.Generic;

namespace MinikUsta.Core
{
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => X * 31 + Y;
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    public sealed class ShapeDef
    {
        /// <summary>0..55 = kindIndex * 4 + rotationIndex.</summary>
        public readonly int Index;
        public readonly string Id;
        public readonly string Kind;
        public readonly int Rotation;
        /// <summary>Normalised cells sorted by (y, x).</summary>
        public readonly Cell[] Cells;
        public readonly int W;
        public readonly int H;
        public readonly int CellCount;
        /// <summary>`Rows[r]`: bit mask of the x offsets occupied in row r (collision).</summary>
        public readonly int[] Rows;
        /// <summary>`ColRows[c]`: bit mask of the y offsets occupied in column c (boundary crossing, TECH §2.2).</summary>
        public readonly int[] ColRows;
        /// <summary>Lowest / highest occupied y offset of column c (open sky and falling, K-11).</summary>
        public readonly int[] ColBottom;
        public readonly int[] ColTop;
        /// <summary>Ağır Yük (Y5): kind I5 or Q9 only (GDD K-44, K-49 item 4 / D-103). Heavy pieces never cross the boundary.</summary>
        public readonly bool Heavy;
        /// <summary>First id (in rotation order 0, 90, 180, 270) with the same cell set, e.g. `O4_90 → O4_0`.</summary>
        public readonly string Canonical;
        public readonly int CanonicalIndex;

        internal ShapeDef(int index, string id, string kind, int rotation, Cell[] cells, int w, int h, int[] rows,
            int[] colRows, int[] colBottom, int[] colTop, bool heavy, string canonical, int canonicalIndex)
        {
            Index = index;
            Id = id;
            Kind = kind;
            Rotation = rotation;
            Cells = cells;
            W = w;
            H = h;
            CellCount = cells.Length;
            Rows = rows;
            ColRows = colRows;
            ColBottom = colBottom;
            ColTop = colTop;
            Heavy = heavy;
            Canonical = canonical;
            CanonicalIndex = canonicalIndex;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Shapes (GDD K-44, D-051; TECH_DESIGN §3).
    ///
    /// Single source: the 0° cells of BRIEF §5. The other orientations are generated at type load: one 90° step is a
    /// CLOCKWISE turn on screen with y pointing up, `(x, y) → (y, −x)`, followed by normalising to the bottom-left corner
    /// (`x −= min x`, `y −= min y`) and sorting the cells by (y, x). 180° and 270° apply the step repeatedly.
    /// </summary>
    public static class Shapes
    {
        /// <summary>BRIEF §5: cells at 0°, bottom-left origin (keyed by kind, `ShapeKinds.All` order).</summary>
        public static readonly IReadOnlyDictionary<string, int[][]> BaseCells = new Dictionary<string, int[][]>
        {
            { "B1", new[] { new[] { 0, 0 } } },
            { "D2", new[] { new[] { 0, 0 }, new[] { 0, 1 } } },
            { "I3", new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 } } },
            { "I4", new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 } } },
            { "O4", new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 1 } } },
            { "C3", new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 } } },
            { "L4", new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 0, 2 } } },
            { "J4", new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 } } },
            { "T4", new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 1 } } },
            { "S4", new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 1, 2 } } },
            { "Z4", new[] { new[] { 1, 0 }, new[] { 1, 1 }, new[] { 0, 1 }, new[] { 0, 2 } } },
            { "I5", new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 4, 0 } } },
            {
                "Q9",
                new[]
                {
                    new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 2, 1 },
                    new[] { 0, 2 }, new[] { 1, 2 }, new[] { 2, 2 },
                }
            },
            // the plus (GDD K-56 D-103 annex): every rotation is the same cell set, canonical P5_0
            { "P5", new[] { new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 2, 1 }, new[] { 1, 2 } } },
        };

        /// <summary>Always heavy, whatever the orientation (GDD K-44; a crane turn keeps the weight, K-37).</summary>
        public static readonly HashSet<string> AlwaysHeavy = new HashSet<string> { "I5", "Q9" };

        /// <summary>Forbidden in level data (GDD K-44, K-45/5 `shape_forbidden`).</summary>
        public static readonly HashSet<string> ForbiddenShapes = new HashSet<string> { "I5_90", "I5_270" };

        /// <summary>Kinds unlocked per story chapter (GDD K-44): 1 → B1 D2 O4 C3; 2 → + I3 L4 J4; 3 → + T4 S4 Z4; 4–5 → + I4.</summary>
        public static readonly IReadOnlyDictionary<string, int> KindFirstChapter = new Dictionary<string, int>
        {
            { "B1", 1 }, { "D2", 1 }, { "O4", 1 }, { "C3", 1 }, { "I3", 2 }, { "L4", 2 }, { "J4", 2 }, { "T4", 3 },
            { "S4", 3 }, { "Z4", 3 }, { "I4", 4 }, { "I5", 1 }, { "Q9", 1 }, { "P5", 2 },
        };

        /// <summary>
        /// v3 shape unlocks by level id (GDD §16 K-56; schemaVersion 3 files only — earlier files keep the K-44 chapter table):
        /// L4/J4 at 2, T4 at 6, I3 at 8, S4/Z4 at 11, I4 at 21. Ağır Yük (I5, Q9) stays apart (0).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> KindFirstLevelV3 = new Dictionary<string, int>
        {
            { "B1", 1 }, { "D2", 1 }, { "O4", 1 }, { "C3", 1 }, { "L4", 2 }, { "J4", 2 }, { "T4", 6 }, { "I3", 8 },
            { "S4", 11 }, { "Z4", 11 }, { "I4", 21 }, { "I5", 0 }, { "Q9", 0 }, { "P5", 16 },
        };

        /// <summary>
        /// GDD K-56 D-103 annex item 2: on a 3-column site (Ws = 3) these kinds are free of the kind lock (`shape_locked` is not
        /// raised); the T4 (6+) and I3 (8+) level locks, I4 and Ağır Yük stay.
        /// </summary>
        public static readonly HashSet<string> WideSiteFreeKinds = new HashSet<string> { "S4", "Z4", "P5" };

        /// <summary>Heavy shapes (Ağır Yük: I5, Q9 only; width never makes a block heavy, D-103) are allowed only from this level id (GDD K-44 "8. bölüm").</summary>
        public const int HeavyMinLevelId = 8;

        /// <summary>All 56 shapes, indexed by `ShapeDef.Index`.</summary>
        public static readonly ShapeDef[] All;
        /// <summary>All 56 ids in index order.</summary>
        public static readonly string[] Ids;
        private static readonly Dictionary<string, ShapeDef> byId;

        static Shapes()
        {
            All = BuildShapes();
            Ids = new string[All.Length];
            byId = new Dictionary<string, ShapeDef>(All.Length);
            for (int i = 0; i < All.Length; i++)
            {
                Ids[i] = All[i].Id;
                byId[All[i].Id] = All[i];
            }
        }

        private static Cell[] Normalise(IReadOnlyList<Cell> cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (var c in cells)
            {
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
            }
            var o = new Cell[cells.Count];
            for (int i = 0; i < o.Length; i++) o[i] = new Cell(cells[i].X - minX, cells[i].Y - minY);
            Array.Sort(o, (a, b) => a.Y != b.Y ? a.Y - b.Y : a.X - b.X);
            return o;
        }

        /// <summary>One clockwise 90° step: `(x, y) → (y, −x)`, then normalise.</summary>
        public static Cell[] RotateClockwise(IReadOnlyList<Cell> cells)
        {
            var turned = new Cell[cells.Count];
            for (int i = 0; i < turned.Length; i++) turned[i] = new Cell(cells[i].Y, -cells[i].X);
            return Normalise(turned);
        }

        private static string CellKey(Cell[] cells)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(cells[i].X).Append(',').Append(cells[i].Y);
            }
            return sb.ToString();
        }

        private static ShapeDef[] BuildShapes()
        {
            var o = new List<ShapeDef>(56);
            for (int kindIndex = 0; kindIndex < ShapeKinds.All.Length; kindIndex++)
            {
                string kind = ShapeKinds.All[kindIndex];
                var raw = BaseCells[kind];
                var start = new Cell[raw.Length];
                for (int i = 0; i < raw.Length; i++) start[i] = new Cell(raw[i][0], raw[i][1]);
                Cell[] cells = Normalise(start);
                var firstByKey = new Dictionary<string, int>();
                for (int rotIndex = 0; rotIndex < Rotations.All.Length; rotIndex++)
                {
                    int rotation = Rotations.All[rotIndex];
                    if (rotIndex > 0) cells = RotateClockwise(cells);
                    int w = 0;
                    int h = 0;
                    foreach (var c in cells)
                    {
                        if (c.X + 1 > w) w = c.X + 1;
                        if (c.Y + 1 > h) h = c.Y + 1;
                    }
                    var rows = new int[h];
                    var colRows = new int[w];
                    var colBottom = new int[w];
                    var colTop = new int[w];
                    for (int c = 0; c < w; c++)
                    {
                        colBottom[c] = h;
                        colTop[c] = -1;
                    }
                    foreach (var c in cells)
                    {
                        rows[c.Y] |= 1 << c.X;
                        colRows[c.X] |= 1 << c.Y;
                        colBottom[c.X] = Math.Min(colBottom[c.X], c.Y);
                        colTop[c.X] = Math.Max(colTop[c.X], c.Y);
                    }
                    int index = kindIndex * 4 + rotIndex;
                    string key = CellKey(cells);
                    int canonicalIndex = firstByKey.TryGetValue(key, out int first) ? first : index;
                    if (!firstByKey.ContainsKey(key)) firstByKey[key] = index;
                    int canonicalRotation = Rotations.All[canonicalIndex - kindIndex * 4];
                    o.Add(new ShapeDef(
                        index,
                        kind + "_" + rotation,
                        kind,
                        rotation,
                        (Cell[])cells.Clone(),
                        w,
                        h,
                        rows,
                        colRows,
                        colBottom,
                        colTop,
                        AlwaysHeavy.Contains(kind),
                        kind + "_" + canonicalRotation,
                        canonicalIndex));
                }
            }
            return o.ToArray();
        }

        public static bool IsShapeId(string? value) => value != null && byId.ContainsKey(value);

        /// <summary>Shape by id; throws on an unknown id (level data is validated before compile).</summary>
        public static ShapeDef ShapeById(string id)
        {
            if (!byId.TryGetValue(id, out var s)) throw new ArgumentOutOfRangeException(nameof(id), "unknown shape id " + id);
            return s;
        }

        /// <summary>Shape by index 0..55; throws when out of range (TS `shapeByIndex`).</summary>
        public static ShapeDef ShapeByIndex(int index)
        {
            if (index < 0 || index >= All.Length) throw new ArgumentOutOfRangeException(nameof(index), "unknown shape index " + index);
            return All[index];
        }

        /// <summary>The orientation reached by turning `id` clockwise by `quarterTurns` × 90° (crane booster, K-37).</summary>
        public static string RotateShapeId(string id, int quarterTurns)
        {
            var s = ShapeById(id);
            int rotIndex = (((Rotations.IndexOf(s.Rotation) + quarterTurns) % 4) + 4) % 4;
            return s.Kind + "_" + Rotations.All[rotIndex];
        }
    }
}
