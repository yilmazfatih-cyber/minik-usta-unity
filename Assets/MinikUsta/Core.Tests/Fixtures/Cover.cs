#nullable enable
// Port of tests/fixtures/cover.ts (web repo): seeded full-cover levels (GDD K-47) for property tests. The RNG call order
// is the TS one call for call (the P2 vectors pin the level hashes). Not a test file.
using System;
using System.Collections.Generic;
using MinikUsta.Core.Json;

namespace MinikUsta.Core.Tests.Fixtures
{
    /// <summary>
    /// Seeded full-cover levels: the plan is painted FROM a random bottom-up tiling, so every level has supply = demand per
    /// colour and at least one K-34 build.
    /// - Board: yard 6 × 6, site 2 × Hs (Hs 3–5), 1–2 segments, colours W Y G.
    /// - Tiling per segment: the lowest (then leftmost) column takes a B1, D2_0, D2_90 or O4 that fits.
    /// - Batch 0 = the blocks of segment 0 (one of segment 1 may be carried early, LEVELS §2.0); batch 1 (forSegment 1) =
    ///   the rest of segment 1. Batch-0 blocks drop into random yard columns; batch-1 blocks get a random drop column.
    /// - Extras (seeded): an Ağır Yük (Q9 / I5) in the yard; a debris B1 of segment 0 parked on its top-left cell (wrong:
    ///   no support below, OBSTACLES S4).
    /// </summary>
    public static class Cover
    {
        private static readonly string[] Palette = { "W", "Y", "G" };
        private const int YardCols = 6;
        private const int YardRows = 6;

        private sealed class PlacedBlock
        {
            public readonly string Shape;
            public readonly string Color;
            public readonly int Sx;
            public readonly int Sy;

            public PlacedBlock(string shape, string color, int sx, int sy)
            {
                Shape = shape;
                Color = color;
                Sx = sx;
                Sy = sy;
            }
        }

        private static readonly Dictionary<string, int[][]> Cells = new Dictionary<string, int[][]>
        {
            { "B1_0", new[] { new[] { 0, 0 } } },
            { "D2_0", new[] { new[] { 0, 0 }, new[] { 0, 1 } } },
            { "D2_90", new[] { new[] { 0, 0 }, new[] { 1, 0 } } },
            { "O4_0", new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 1 } } },
        };

        /// <summary>One segment: a random bottom-up tiling and the plan rows it paints (top → bottom).</summary>
        private static void TileSegment(IRng rng, int hs, out List<PlacedBlock> pieces, out string[] rows)
        {
            var h = new[] { 0, 0 };
            var grid = new string[hs][];
            for (int y = 0; y < hs; y++) grid[y] = new[] { "W", "W" };
            pieces = new List<PlacedBlock>();
            for (int guard = 0; guard < 64 && (h[0] < hs || h[1] < hs); guard++)
            {
                int c = h[0] <= h[1] ? 0 : 1;
                int y = h[c];
                bool level = h[0] == h[1];
                var options = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("B1_0", c) };
                if (y + 2 <= hs) options.Add(new KeyValuePair<string, int>("D2_0", c));
                if (level) options.Add(new KeyValuePair<string, int>("D2_90", 0));
                if (level && y + 2 <= hs) options.Add(new KeyValuePair<string, int>("O4_0", 0));
                var pick = options[rng.NextInt(options.Count)];
                string color = Palette[rng.NextInt(Palette.Length)];
                foreach (var d in Cells[pick.Key])
                {
                    grid[y + d[1]][pick.Value + d[0]] = color;
                    h[pick.Value + d[0]] = Math.Max(h[pick.Value + d[0]], y + d[1] + 1);
                }
                pieces.Add(new PlacedBlock(pick.Key, color, pick.Value, y));
            }
            rows = new string[hs];
            for (int y = 0; y < hs; y++) rows[hs - 1 - y] = grid[y][0] + grid[y][1];
        }

        /// <summary>Drops blocks into the yard (heights per column), random columns where they fit; returns batch-0 specs.</summary>
        private static List<P> DropInYard(IRng rng, List<KeyValuePair<string, string>> blocks)
        {
            var height = new int[YardCols];
            var o = new List<P>();
            foreach (var b in blocks)
            {
                var cells = Cells[b.Key];
                int w = 0;
                int hh = 0;
                foreach (var c in cells)
                {
                    w = Math.Max(w, c[0] + 1);
                    hh = Math.Max(hh, c[1] + 1);
                }
                var fits = new List<int>();
                for (int x = 0; x + w <= YardCols; x++)
                {
                    int bse = 0;
                    for (int i = 0; i < w; i++) bse = Math.Max(bse, height[x + i]);
                    if (bse + hh <= YardRows) fits.Add(x);
                }
                int pick = rng.NextInt(Math.Max(1, fits.Count));
                int px = pick < fits.Count ? fits[pick] : 0;
                int baseRow = 0;
                for (int i = 0; i < w; i++) baseRow = Math.Max(baseRow, height[px + i]);
                for (int i = 0; i < w; i++) height[px + i] = baseRow + hh;
                o.Add(new P(b.Key, b.Value, px, baseRow));
            }
            return o;
        }

        /// <summary>A seeded full-cover level (see the class comment). TS `coverLevel(seed)`.</summary>
        public static LevelSpec CoverLevel(int seed)
        {
            var rng = new Mulberry32(unchecked(seed * 7919 + 13));
            int hs = 3 + rng.NextInt(3);
            int segCount = 1 + rng.NextInt(2);
            var segPieces = new List<List<PlacedBlock>>();
            var segRows = new List<string[]>();
            for (int i = 0; i < segCount; i++)
            {
                TileSegment(rng, hs, out var pieces, out var rows);
                segPieces.Add(pieces);
                segRows.Add(rows);
            }
            var seg0 = segPieces[0];
            var seg1 = segCount > 1 ? segPieces[1] : new List<PlacedBlock>();
            var debris = new List<D>();
            var yardBlocks = new List<KeyValuePair<string, string>>();
            foreach (var p in seg0) yardBlocks.Add(new KeyValuePair<string, string>(p.Shape, p.Color));
            // a debris B1 of segment 0 on its top-left cell (no support below → never correct there)
            int b1 = -1;
            for (int i = 0; i < seg0.Count; i++)
            {
                if (seg0[i].Shape == "B1_0")
                {
                    b1 = i;
                    break;
                }
            }
            if (b1 >= 0 && rng.Next() < 0.35 && hs >= 2)
            {
                debris.Add(new D("B1_0", seg0[b1].Color, 6, hs - 1, 0));
                yardBlocks.RemoveAt(b1);
            }
            var carried = new List<PlacedBlock>();
            if (seg1.Count > 1 && rng.Next() < 0.5) carried.Add(seg1[0]);
            var truck = seg1.GetRange(carried.Count, seg1.Count - carried.Count);
            foreach (var p in carried) yardBlocks.Add(new KeyValuePair<string, string>(p.Shape, p.Color));
            var pieces0 = DropInYard(rng, yardBlocks);
            if (rng.Next() < 0.3)
            {
                // an Ağır Yük on top of the yard where it fits (colour is ignored, K-44)
                var used = new HashSet<string>();
                foreach (var p in pieces0)
                    foreach (var d in Cells[p.Shape]) used.Add((p.X + d[0]) + "," + (p.Y + d[1]));
                bool q9 = rng.Next() < 0.5;
                int w = q9 ? 3 : 5;
                int hh = q9 ? 3 : 1;
                bool placed = false;
                for (int y = YardRows - hh; y >= 0 && !placed; y--)
                {
                    for (int x = 0; x + w <= YardCols && !placed; x++)
                    {
                        bool free = true;
                        for (int dx = 0; dx < w && free; dx++)
                            for (int dy = 0; dy < hh && free; dy++)
                                if (used.Contains((x + dx) + "," + (y + dy))) free = false;
                        if (free)
                        {
                            pieces0.Add(new P(q9 ? "Q9_0" : "I5_0", "W", x, y));
                            placed = true;
                        }
                    }
                }
            }
            var spec = new LevelSpec
            {
                Id = 1,
                Moves = 60,
                Yard = new SizeSpec(YardCols, YardRows),
                Site = new SizeSpec(2, hs),
                Wall = new WallSpec { Height = 2, Gaps = new JsonValue[0] },
                Plans = segRows.ToArray(),
                Pieces = pieces0.ToArray(),
            };
            if (truck.Count > 0)
            {
                var tp = new P[truck.Count];
                for (int i = 0; i < truck.Count; i++) tp[i] = new P(truck[i].Shape, truck[i].Color, rng.NextInt(YardCols - 1), YardRows);
                spec.Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = tp } };
            }
            if (debris.Count > 0) spec.Debris = debris.ToArray();
            return spec;
        }
    }
}
