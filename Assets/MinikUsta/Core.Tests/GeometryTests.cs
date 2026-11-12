#nullable enable
// Port of tests/core/geometry.test.ts (K-49, TECH §2R.1 geometry set G) — the blocks that need only the P0 modules
// (geometry, coords, state, grid, compile, hash, ASCII) — and of tests/core/geometry.gate.test.ts (the number gate).
// The blocks that need the P1 modules (edge model ≡ cell model in G, E-56 classify half, K-05 short board, E-57 truck
// drops, K-17 step 2/3, K-44 cargo) are in GeometryTests.P1.cs (same partial class). Still deferred to the K-35 /
// session packages: the random-play invariants + replay, the E-56 applyMove half, the K-44 pipeline test.
// The ESLint half of the gate (no-restricted-imports) has no C# equivalent; its intent — no pre-2R board constant left
// outside the geometry — is checked by `K49_the_only_pre_2R_board_constant_left_is_obsolete_and_unreferenced`.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public partial class GeometryTests
    {
        // --- the geometry set G (TECH §2R.1) ---------------------------------------------------------------------------

        internal readonly struct GeoCase
        {
            public readonly int Wy;
            public readonly int Hy;
            public readonly int Ws;
            public readonly int Hs;

            public GeoCase(int wy, int hy, int ws, int hs)
            {
                Wy = wy;
                Hy = hy;
                Ws = ws;
                Hs = hs;
            }

            public string Label => Wy + "x" + Hy + "|" + Ws + "x" + Hs;
        }

        internal static readonly GeoCase[] G =
        {
            new GeoCase(4, 4, 2, 5),
            new GeoCase(4, 4, 2, 6),
            new GeoCase(4, 5, 2, 5),
            new GeoCase(4, 5, 2, 7),
            new GeoCase(6, 5, 2, 6),
            new GeoCase(6, 5, 2, 7), // WP-A brief: 6×5 | 2×7 (H 7, yard air y 5–6 over a full-width yard)
            new GeoCase(6, 8, 2, 8),
            new GeoCase(5, 7, 3, 7),
            new GeoCase(4, 6, 4, 6),
            new GeoCase(6, 8, 3, 5), // D-103 wide site (levels 6-10 v3c): 9 columns
        };

        private static BoardGeo GeoOf(GeoCase c, int eMax = 0) => Geometry.MakeGeo(new GeoParams(c.Wy, c.Hy, c.Ws, c.Hs, eMax));

        private static LevelSpec Sized(GeoCase c, LevelSpec? spec = null)
        {
            spec ??= new LevelSpec();
            spec.Yard = new SizeSpec(c.Wy, c.Hy);
            spec.Site = new SizeSpec(c.Ws, c.Hs);
            return spec;
        }

        private static readonly P[] One = { new P("B1_0", "W", 0, 0) };

        private static (int x, int y)[] CellsOf(ShapeDef shape, int ix, int iy) => shape.Cells.Select(c => (ix + c.X, iy + c.Y)).ToArray();

        /// <summary>One plan of `hs` rows × `ws` colours (top → bottom).</summary>
        private static string[] PlanRows(GeoCase c, IRng rng)
        {
            string[] colors = { "W", "Y" };
            var rows = new string[c.Hs];
            for (int r = 0; r < c.Hs; r++)
            {
                var row = new char[c.Ws];
                for (int k = 0; k < c.Ws; k++) row[k] = colors[rng.NextInt(2)][0];
                rows[r] = new string(row);
            }
            return rows;
        }

        /// <summary>Random non-overlapping yard pieces (B1, D2, O4, C3) filling about `fill` of the Wy × Hy yard.</summary>
        private static P[] YardPieces(GeoCase c, IRng rng, double fill)
        {
            var used = new HashSet<(int, int)>();
            var out_ = new List<P>();
            string[] shapes = { "B1_0", "D2_0", "D2_90", "O4_0", "C3_0", "C3_180" };
            for (int tries = 0; tries < 400 && used.Count < fill * c.Wy * c.Hy; tries++)
            {
                var shape = Shapes.ShapeById(shapes[rng.NextInt(shapes.Length)]);
                if (shape.W > c.Wy || shape.H > c.Hy) continue;
                int x = rng.NextInt(c.Wy - shape.W + 1);
                int y = rng.NextInt(c.Hy - shape.H + 1);
                var cells = CellsOf(shape, x, y);
                if (cells.Any(used.Contains)) continue;
                foreach (var cell in cells) used.Add(cell);
                out_.Add(new P(shape.Id, rng.Next() < 0.5 ? "W" : "Y", x, y));
            }
            return out_.ToArray();
        }

        /// <summary>A random schema-valid level of geometry `c` (one segment, an optional static gap, yard gravity on or off).</summary>
        internal static CompiledLevel RandomLevel(GeoCase c, IRng rng, int id)
        {
            int h = Math.Max(c.Hy, c.Hs);
            int height = rng.NextInt(h + 1);
            var gaps = new List<Json.JsonValue>();
            if (height >= 3 && rng.Next() < 0.5)
            {
                int y = rng.NextInt(height - 2);
                int size = 1 + rng.NextInt(2);
                if (y + size <= height - 1) gaps.Add(J.O("type", "static", "y", y, "size", size));
            }
            // same RNG order as the TS object literal: plan, then the fill ratio, then the pieces, then yard gravity
            var plan = PlanRows(c, rng);
            double fill = 0.55 + rng.Next() * 0.3;
            var pieces = YardPieces(c, rng, fill);
            bool yardGravity = rng.Next() < 0.5;
            return Builders.Compiled(Sized(c, new LevelSpec
            {
                Id = id,
                Moves = 60,
                Plan = plan,
                Pieces = pieces,
                Wall = new WallSpec { Height = height, Gaps = gaps.ToArray() },
                Gravity = new GravitySpec { Yard = yardGravity },
            }));
        }

        // --- geometry.ts -----------------------------------------------------------------------------------------------------

        [Test(Description = "K-49 DEFAULT_GEO is the pre-2R board: yard 6 × 8, site 2 × 8, H 8, crane rows 8–9, boundary 5 | 6")]
        public void K49_DefaultGeo_is_the_pre_2R_board_yard_6x8_site_2x8_H_8_crane_rows_8_9_boundary_5_6()
        {
            var g = Geometry.DefaultGeo;
            CollectionAssert.AreEqual(new[] { 6, 8, 2, 8, 0, 8, 10, 8 }, new[] { g.Wy, g.Hy, g.Ws, g.Hs, g.EMax, g.H, g.Rows, g.Cols });
            CollectionAssert.AreEqual(new[] { 6, 5, 8, 16 }, new[] { g.SiteX, g.BoundaryX, g.CraneRow, g.SegCells });
            CollectionAssert.AreEqual(new[] { 0b00111111, 0b11000000, 0b1111111111 }, new[] { g.YardBits, g.SiteBits, g.RowMaskAll });
            CollectionAssert.AreEqual(new[] { 9, 10 }, new[] { Geometry.MaxCols, Geometry.MaxRows });
            // TS `Object.isFrozen`: every BoardGeo field is readonly and the constructor is internal
            Assert.IsTrue(typeof(BoardGeo).GetFields(BindingFlags.Public | BindingFlags.Instance).All(f => f.IsInitOnly));
            Assert.IsTrue(Geometry.HasDefaultBoard(g));
            Assert.AreEqual("6x8|2x8 H8", Geometry.GeoLabel(g));
        }

        [Test(Description = "K-49 H = max(Hy, Hs + eMax): Bölüm 1 4×4 | 2×5 → H 5, crane y 5–6; Bölüm 4 4×4 | 2×6 → H 6, boundary 3 | 4, site x 4–5, yard air y 4–5")]
        public void K49_H_is_max_Hy_Hs_plus_eMax_Bolum_1_H5_Bolum_4_H6_boundary_3_4_site_x_4_5_yard_air_y_4_5()
        {
            var b1 = Geometry.MakeGeo(new GeoParams(4, 4, 2, 5, 0));
            CollectionAssert.AreEqual(new[] { 5, 7, 5, 6 }, new[] { b1.H, b1.Rows, b1.CraneRow, b1.Cols });
            var b4 = Geometry.MakeGeo(new GeoParams(4, 4, 2, 6, 0));
            CollectionAssert.AreEqual(new[] { 6, 6, 3, 4 }, new[] { b4.H, b4.CraneRow, b4.BoundaryX, b4.SiteX });
            CollectionAssert.AreEqual(new[] { true, true }, new[] { Coords.IsYardAir(b4, 0, 4), Coords.IsYardAir(b4, 0, 5) });
            CollectionAssert.AreEqual(new[] { false, false, false }, new[] { Coords.IsYardAir(b4, 0, 3), Coords.IsYardAir(b4, 0, 6), Coords.IsYardAir(b4, 4, 4) });
            CollectionAssert.AreEqual(new[] { true, true, false }, new[] { Coords.IsSiteCell(b4, 4, 0), Coords.IsSiteCell(b4, 5, 5), Coords.IsSiteCell(b4, 6, 0) });
            CollectionAssert.AreEqual(new[] { true, true, false }, new[] { Coords.IsCraneCell(b4, 0, 6), Coords.IsCraneCell(b4, 5, 7), Coords.IsCraneCell(b4, 0, 8) });
            // the elevator range top raises H (K-24): Hs 5 + b 2 = 7 > Hy 4
            Assert.AreEqual(7, Geometry.MakeGeo(new GeoParams(4, 4, 2, 5, 2)).H);
            Assert.AreEqual(8, Geometry.MakeGeo(new GeoParams(4, 8, 2, 5, 2)).H);
        }

        [Test(Description = "K-49 makeGeo rejects boards outside the 9 × 10 frame (Wy + Ws ≤ 9, Ws ≤ 4, Hy and Hs ≤ 8, positive sizes); 6 | 3 fits (D-103)")]
        public void K49_MakeGeo_rejects_boards_outside_the_9x10_frame()
        {
            Assert.AreEqual(9, Geometry.MakeGeo(new GeoParams(6, 8, 3, 8, 0)).Cols);
            // TS also rejects `wy: 4.5`; GeoParams is int-typed in C#, so a non-integer size cannot be expressed.
            var bad = new[]
            {
                new GeoParams(6, 8, 4, 8, 0), // board_too_wide
                new GeoParams(7, 8, 3, 8, 0),
                new GeoParams(3, 8, 5, 8, 0),
                new GeoParams(4, 9, 2, 8, 0),
                new GeoParams(4, 8, 2, 9, 0),
                new GeoParams(0, 4, 2, 4, 0),
                new GeoParams(4, 4, 0, 4, 0),
            };
            foreach (var p in bad)
            {
                var q = p;
                Assert.Throws<ArgumentOutOfRangeException>(() => Geometry.MakeGeo(q), q.Wy + "," + q.Hy + "," + q.Ws + "," + q.Hs + "," + q.EMax);
            }
            Assert.DoesNotThrow(() => Geometry.MakeGeo(new GeoParams(4, 4, 4, 8, 0)));
        }

        [Test(Description = "K-49 geoFromLevel reads yard.cols/rows, site.cols/rows and the elevator top b; missing fields → 6, 8, 2, 8")]
        public void K49_GeoFromLevel_reads_yard_and_site_sizes_and_the_elevator_top_missing_fields_default_to_6_8_2_8()
        {
            Assert.AreSame(Geometry.DefaultGeo, Geometry.GeoFromLevel(Builders.Level(new LevelSpec { Pieces = One })));
            var sized = Geometry.GeoFromLevel(Builders.Level(Sized(new GeoCase(4, 5, 2, 7), new LevelSpec { Plan = new[] { "WW" }, Pieces = One })));
            CollectionAssert.AreEqual(new[] { 4, 5, 2, 7, 7 }, new[] { sized.Wy, sized.Hy, sized.Ws, sized.Hs, sized.H });
            var el = Geometry.GeoFromLevel(Builders.Level(Sized(new GeoCase(4, 4, 2, 5), new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = One,
                Elevator = J.O("range", new[] { 0, 2 }, "start", 0, "dir", 1),
            })));
            CollectionAssert.AreEqual(new[] { 2, 7, 9 }, new[] { el.EMax, el.H, el.Rows });
        }

        [Test(Description = "K-49 pre-2R data: an elevator over the default 8-row site keeps H at 8 (the frame clamps; plan rows above H do not exist)")]
        public void K49_pre_2R_data_an_elevator_over_the_default_8_row_site_keeps_H_at_8()
        {
            var g = Geometry.GeoFromLevel(Builders.Level(new LevelSpec
            {
                Plan = new[] { "WW", "YY" },
                Pieces = One,
                Elevator = J.O("range", new[] { 0, 2 }, "start", 0, "dir", 1),
            }));
            CollectionAssert.AreEqual(new[] { 6, 8, 2, 8, 2, 8, 10 }, new[] { g.Wy, g.Hy, g.Ws, g.Hs, g.EMax, g.H, g.Rows });
            Assert.IsTrue(Geometry.HasDefaultBoard(g));
        }

        // --- regions and boundary masks ---------------------------------------------------------------------------------------

        [Test(Description = "K-01 K-02 K-03 K-05 K-49 yard, yard air, site columns and the crane area partition the board + crane frame; site air needs y ≥ Hs + e")]
        public void K01_K02_K03_K05_K49_yard_yard_air_site_columns_and_crane_area_partition_the_frame_site_air_needs_y_ge_Hs_plus_e()
        {
            foreach (var c in G)
            {
                var g = GeoOf(c);
                for (int y = -1; y <= Geometry.MaxRows; y++)
                {
                    for (int x = -1; x <= Geometry.MaxCols; x++)
                    {
                        string where = c.Label + " (" + x + "," + y + ")";
                        bool inside = x >= 0 && x < c.Wy + c.Ws && y >= 0 && y < g.H + 2;
                        Assert.AreEqual(inside, Coords.InGrid(g, x, y), where);
                        Assert.AreEqual(inside && y < g.H, Coords.OnBoard(g, x, y), where);
                        var parts = new[] { Coords.IsYardCell(g, x, y), Coords.IsYardAir(g, x, y), Coords.IsSiteCell(g, x, y), Coords.IsCraneCell(g, x, y) };
                        Assert.AreEqual(inside ? 1 : 0, parts.Count(p => p), where);
                        Assert.AreEqual(x >= 0 && x < c.Wy && y >= 0 && y < c.Hy, Coords.IsYardCell(g, x, y), where);
                        Assert.AreEqual(Coords.IsSiteCell(g, x, y) && y >= c.Hs, Coords.IsSiteAir(g, x, y), where);
                        if (inside) Assert.AreEqual(x <= c.Wy - 1 ? At.ZoneYard : At.ZoneSite, Coords.SideOf(g, x), where);
                    }
                }
                // the elevator offset e lifts the plan: site air starts at Hs + e
                int e = g.H - c.Hs;
                if (e > 0) Assert.IsFalse(Coords.IsSiteAir(g, g.SiteX, c.Hs, e), c.Label);
            }
        }

        [Test(Description = "E-46 K-49 the boundary x = Wy−1 | Wy never joins two neighbours, whatever the geometry")]
        public void E46_K49_the_boundary_x_Wy_minus_1_Wy_never_joins_two_neighbours_whatever_the_geometry()
        {
            foreach (var c in G)
            {
                var g = GeoOf(c);
                for (int y = 0; y < g.H; y++)
                {
                    for (int x = 0; x < g.Cols; x++)
                    {
                        foreach (var n in Coords.Neighbors4(g, x, y))
                        {
                            Assert.AreEqual(Coords.SideOf(g, x), Coords.SideOf(g, n.Ix), c.Label + " (" + x + "," + y + ") → (" + n.Ix + "," + n.Iy + ")");
                            Assert.AreEqual(1, Math.Abs(n.Ix - x) + Math.Abs(n.Iy - y));
                        }
                    }
                    if (y < c.Hy) Assert.IsFalse(Coords.Neighbors4(g, g.BoundaryX, y).Any(n => n.Ix == g.SiteX));
                    Assert.IsFalse(Coords.Neighbors4(g, g.SiteX, y).Any(n => n.Ix == g.BoundaryX));
                }
                // yard cells see yard cells only (no yard air), site cells see board cells of the site columns
                Assert.IsFalse(Coords.Neighbors4(g, 0, c.Hy - 1).Any(n => n.Iy >= c.Hy));
                CollectionAssert.AreEqual(new Anchor[0], Coords.Neighbors4(g, 0, g.CraneRow));
            }
        }

        [Test(Description = "K-04 K-05 K-49 closed rows y < height (no open gap), FREE crossing rows y ≥ height up to H + 1; a box straddles iff it has columns on both sides")]
        public void K04_K05_K49_closed_rows_below_height_FREE_crossing_rows_from_height_to_H_plus_1_straddle_iff_columns_on_both_sides()
        {
            foreach (var c in G)
            {
                var g = GeoOf(c);
                int[] RowsOf(int mask) => Enumerable.Range(0, Geometry.MaxRows).Where(y => ((mask >> y) & 1) != 0).ToArray();
                for (int height = 0; height <= g.H; height++)
                {
                    CollectionAssert.AreEqual(Enumerable.Range(height, g.Rows - height).ToArray(), RowsOf(Coords.OpenFreeMask(g, height)), c.Label + " height " + height);
                    CollectionAssert.AreEqual(Enumerable.Range(0, height).ToArray(), RowsOf(Coords.ClosedBoundaryMask(g, height, 0)));
                }
                for (int ix = 0; ix < g.Cols; ix++)
                    for (int w = 1; ix + w <= g.Cols; w++)
                        Assert.AreEqual(ix <= g.BoundaryX && ix + w - 1 >= g.SiteX, Coords.StraddlesBoundary(g, ix, w));
            }
        }

        // --- state, compile, hash, ASCII --------------------------------------------------------------------------------------

        [Test(Description = "K-49 compile stores CompiledLevel.geo; layout = yardOcc wy × rows, siteOcc S × ws × hs, filled/wrongOcc S × ws; plan index sy·ws + sx")]
        public void K49_compile_stores_geo_layout_yardOcc_wy_x_rows_siteOcc_S_x_ws_x_hs_filled_wrongOcc_S_x_ws_plan_index_sy_ws_plus_sx()
        {
            var lvl = Builders.Compiled(Sized(new GeoCase(5, 7, 3, 7), new LevelSpec
            {
                Plans = new[]
                {
                    new[] { "WWW", "WWW", "WWW", "WWW", "WWW", "YYY", "GWY" },
                    new[] { "WWW", "WWW", "WWW", "WWW", "WWW", "WWW", "WWW" },
                },
                Pieces = One,
            }));
            Assert.IsTrue(lvl.Geo.SameAs(Geometry.MakeGeo(new GeoParams(5, 7, 3, 7, 0))));
            var L = lvl.Layout;
            Assert.AreEqual(5 * 9, L.SiteOcc - L.YardOcc);
            Assert.AreEqual(2 * 3 * 7, L.Filled - L.SiteOcc);
            Assert.AreEqual(2 * 3, L.WrongOcc - L.Filled);
            Assert.AreEqual(2 * 3, L.Pieces - L.WrongOcc);
            var seg0 = lvl.Segments[0];
            // bottom row "GWY": sx 0 G, 1 W, 2 Y; row 1 "YYY"
            CollectionAssert.AreEqual(new[] { 2, 0, 1, 1, 1, 1 }, seg0.PlanColors.Take(6).Select(b => (int)b).ToArray());
            Assert.AreEqual(21, seg0.PlanColors.Length);
            CollectionAssert.AreEqual(new[] { 0b1111111, 0b1111111, 0b1111111 }, seg0.PlanMask);
            Assert.IsTrue(State.ComputeLayout(L.Counts, lvl.Geo).SameAs(L));
        }

        [Test(Description = "K-49 the default board keeps the pre-2R buffer layout word for word (yardOcc 6 × 10, siteOcc S × 16, filled S × 2)")]
        public void K49_the_default_board_keeps_the_pre_2R_buffer_layout_word_for_word()
        {
            var lvl = Builders.Compiled(new LevelSpec { Plans = new[] { new[] { "WW" }, new[] { "YY" } }, Pieces = One });
            var L = lvl.Layout;
            CollectionAssert.AreEqual(new[] { 18, 78, 110, 114, 118 }, new[] { L.YardOcc, L.SiteOcc, L.Filled, L.WrongOcc, L.Pieces });
        }

        [Test(Description = "K-49 Zobrist positions stay in the 9 × 10 frame for every geometry: yard y·9 + x, site 90 + seg·32 + sy·4 + sx, queue from 250")]
        public void K49_Zobrist_positions_stay_in_the_9x10_frame_for_every_geometry_yard_y9_x_site_90_seg32_sy4_sx_queue_from_250()
        {
            CollectionAssert.AreEqual(new[] { 90, 250 }, new[] { Hash.PosSite, Hash.PosQueue });
            foreach (var c in G)
            {
                var g = GeoOf(c);
                var seen = new HashSet<int>();
                for (int y = 0; y < c.Hy; y++)
                    for (int x = 0; x < c.Wy; x++) seen.Add(Hash.PiecePosition(g.SiteX, Zone.Yard, x, y, -1));
                for (int seg = 0; seg < Geometry.MaxSegments; seg++)
                    for (int sy = 0; sy < c.Hs; sy++)
                        for (int sx = 0; sx < c.Ws; sx++)
                        {
                            int p = Hash.PiecePosition(g.SiteX, Zone.Site, g.SiteX + sx, sy, seg);
                            Assert.GreaterOrEqual(p, Hash.PosSite, c.Label);
                            Assert.Less(p, Hash.PosQueue, c.Label);
                            seen.Add(p);
                        }
                Assert.AreEqual(c.Wy * c.Hy + Geometry.MaxSegments * c.Ws * c.Hs, seen.Count, c.Label);
            }
            Assert.AreEqual(90 + 32 + 12 + 1, Hash.PiecePosition(6, Zone.Site, 7, 3, 1));
        }

        [Test(Description = "K-49 ASCII: a non-default board prints `size Wy×Hy|Ws×Hs H`, rows H + 1 … 0 and `·` for yard and site air; ids text round-trips in every geometry")]
        public void K49_ASCII_non_default_board_prints_size_line_rows_H_plus_1_to_0_and_air_char_ids_text_round_trips_in_every_geometry()
        {
            var s = Builders.InitialState(Sized(new GeoCase(4, 4, 2, 5), new LevelSpec
            {
                Id = 1,
                Wall = new WallSpec { Height = 3 },
                Plan = new[] { "WW", "YY", "WW", "YW", "YW" },
                Pieces = new[] { new P("D2_0", "Y", 0, 2), new P("O4_0", "W", 2, 0) },
            }));
            string a = Ascii.AirChar;
            CollectionAssert.AreEqual(new[]
            {
                "L1 turn 0 moves 20 seg 1/1 elev 0",
                "size 4x4|2x5 H5",
                " y  0 1 2 3 | W | 4 5",
                " 6  . . . . | : | . .",
                " 5  . . . . | : | . .",
                " 4  " + a + " " + a + " " + a + " " + a + " | : | . .",
                " 3  y . . . | : | . .",
                " 2  y . . . | # | . .",
                " 1  . . w w | # | . .",
                " 0  . . w w | # | . .",
                "plan seg0 (top→bottom): WW YY WW YW YW",
            }, Ascii.ToAscii(s).Split('\n'));
            Assert.IsTrue(Ascii.ParseAscii(Ascii.ToAscii(s)).Size.SameAs(new AsciiSize(4, 4, 2, 5, 5)));
            // the default board: no size line (pre-2R fixtures read unchanged)
            Assert.AreEqual(" y  0 1 2 3 4 5 | W | 6 7", Ascii.ToAscii(Builders.InitialState(new LevelSpec { Pieces = One })).Split('\n')[1]);

            var rng = new Mulberry32(49_2026);
            foreach (var c in G)
            {
                var lvl = RandomLevel(c, rng, 3);
                var t = State.CreateInitialState(lvl);
                string text = Ascii.ToAscii(t, true);
                Assert.AreEqual(!Geometry.HasDefaultBoard(lvl.Geo), text.Split('\n')[1].StartsWith("size ", StringComparison.Ordinal), c.Label);
                CollectionAssert.AreEqual(t.Buf, Ascii.FromAscii(lvl, text).Buf, c.Label);
                if (!Geometry.HasDefaultBoard(lvl.Geo))
                {
                    var plain = Builders.Compiled(new LevelSpec { Id = 3, Plan = new[] { "WW" }, Pieces = One });
                    var ex = Assert.Throws<InvalidOperationException>(() => Ascii.FromAscii(plain, text), c.Label);
                    StringAssert.Contains("board", ex!.Message, c.Label);
                }
            }
        }

        [Test(Description = "K-49 site air prints `·` above Hs + e; the platform `_` below e (elevator, 4×4 | 2×5, b = 2 → H 7)")]
        public void K49_site_air_prints_air_above_Hs_plus_e_the_platform_below_e_elevator_4x4_2x5_b2_H7()
        {
            var s = Builders.InitialState(Sized(new GeoCase(4, 4, 2, 5), new LevelSpec
            {
                Id = 6,
                Plan = new[] { "WW", "YY", "WW", "YW", "YW" },
                Elevator = J.O("range", new[] { 0, 2 }, "start", 1, "dir", 1),
                Pieces = One,
            }));
            var rows = Ascii.ToAscii(s).Split('\n');
            Assert.AreEqual("size 4x4|2x5 H7", rows[1]);
            string Site(int y)
            {
                var row = rows.FirstOrDefault(r => r.StartsWith(" " + y + "  ", StringComparison.Ordinal)) ?? "";
                return row.Length >= 3 ? row.Substring(row.Length - 3) : "";
            }
            string a = Ascii.AirChar;
            CollectionAssert.AreEqual(new[] { "_ _", ". .", ". .", a + " " + a, ". .", ". ." },
                new[] { Site(0), Site(1), Site(5), Site(6), Site(7), Site(8) });
        }

        // --- typing helpers kept honest ----------------------------------------------------------------------------------------

        [Test(Description = "K-49 level fixtures with sizes compile through the current schema")]
        public void K49_level_fixtures_with_sizes_compile_through_the_current_schema()
        {
            var lvl = Builders.Compiled(Sized(new GeoCase(4, 6, 4, 6), new LevelSpec { Plan = new[] { "WWYY" }, Pieces = One }));
            CollectionAssert.AreEqual(new[] { 4, 4, 6 }, new[] { lvl.Geo.Wy, lvl.Geo.Ws, lvl.Geo.H });
            Assert.AreEqual(24, lvl.Segments[0].PlanColors.Length);
        }

        // --- the number gate (tests/core/geometry.gate.test.ts, TECH §2R.1, §2R.13 "gizli sabit" risk) -----------------------

        /// <summary>WP-A files (TECH §2R.12): every core file that reads the board geometry (TS `GEOMETRY_CORE`, same order).</summary>
        private static readonly string[] GeometryCore =
        {
            "Geometry.cs",
            "Coords.cs",
            "State.cs",
            "Grid.cs",
            "Movement.cs",
            "Gravity.cs",
            "Placement.cs",
            "Delivery.cs",
            "Hash.cs",
            "Ascii.cs",
            "Level/Compile.cs",
            "Level/Plan.cs",
        };

        private readonly struct Rule
        {
            public readonly Regex Re;
            public readonly string Why;

            public Rule(string pattern, string why)
            {
                Re = new Regex(pattern);
                Why = why;
            }
        }

        /// <summary>Justified literals per file: a line pattern and why the number is not a board size.</summary>
        private static readonly Dictionary<string, Rule[]> Justified = new Dictionary<string, Rule[]>
        {
            ["Geometry.cs"] = new[]
            {
                new Rule(@"^public const int MaxCols = 9;$", "the maximum frame (K-49 Wy + Ws ≤ 9, D-103): the single source"),
                new Rule(@"^public const int MaxRows = 10;$", "the maximum frame (K-49 H ≤ 8 + 2 crane rows): the single source"),
                new Rule(@"^public const int MaxSegments = 5;$", "GDD K-22: 1 ≤ S ≤ 5"),
                new Rule(@"^public static readonly GeoParams DefaultSizes = new GeoParams\(6, 8, 2, 8, 0\);$", "GDD K-49 defaults"),
            },
            ["State.cs"] = new[]
            {
                new Rule(@"^public const int \w+ = (5|6|7|8|9|10);$", "header (H) and piece record (PF) field offsets"),
                new Rule(@"^public const int PieceStride = 9;$", "piece record stride"),
                new Rule(@"^public const int \w+ = 1 << (5|6|7|8|9|10);$", "piece flag bit positions (FlagBit; v3 flags 8-11)"),
                new Rule(@"<< 16\)|>> 6\)|<< 6\)|\(16 - 8 \* k\)|>> 8\)|<< 8\)|<< 24\)", "base64 / little-endian byte packing of the buffer"),
            },
            ["Movement.cs"] = new[]
            {
                new Rule(@"D2Epsilon = 1e-9;$", "float tolerance"),
                new Rule(@"new short\[8\];$", "neighbour scratch (4 FREE steps + rails ≤ 3 + spare)"),
                new Rule(@"new DropClass\(DropKinds\.\w+, [\w.]+, (5|6|7), ", "GDD K-07 release table row numbers"),
            },
            ["Hash.cs"] = new[]
            {
                new Rule(@"^private const int HeaderTerms = 8;$", "eight hashed header fields"),
                new Rule(@"& 7\) << 6\)|& 255\) << 9\)|>> 8\) & 15\) << 25\)", "piece class bit packing (colour 3 bits, flags 8 bits, v3 flags bits 25-28)"),
                new Rule(@"^MixField\((5|6|7), ", "hashed header term index"),
            },
            ["Ascii.cs"] = new[]
            {
                new Rule(@"Groups\[(5|6|7)\]", "regex group index"),
                new Rule(@"Substring\((6|7|10)\)", "line prefix length (`queue:`, `locked:`, `obstacles:` …)"),
                new Rule(@"V3FlagEntry|PalletEntry", "v3 `traits:` entry patterns (pallet anchor digits 0-9)"),
            },
            ["Level/Compile.cs"] = new[] { new Rule(@"new Step10Timer\(""[^""]+"", (5|6)\)", "K-35 step-10 timer order") },
        };

        private static readonly Regex Number = new Regex(@"(?<![\w.$])(5|6|7|8|9|10)(?![\w.])");

        /// <summary>Code part of a line: comment lines dropped (`//`, `///`, `*`, `/*`), trailing `//` comments cut.</summary>
        private static string CodeOf(string line)
        {
            string t = line.Trim();
            if (t.StartsWith("*", StringComparison.Ordinal) || t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("/*", StringComparison.Ordinal)) return "";
            int cut = t.IndexOf(" // ", StringComparison.Ordinal);
            return (cut >= 0 ? t.Substring(0, cut) : t).Trim();
        }

        [Test(Description = "K-49 the gate sees board literals")]
        public void K49_the_gate_sees_board_literals()
        {
            foreach (var code in new[] { "if (x <= 5) return;", "for (int y = 0; y < 8; y++)", "int top = 10 - h;" })
                Assert.IsTrue(Number.IsMatch(CodeOf(code)), code);
            foreach (var code in new[] { "if (x <= geo.BoundaryX) return;", "// x <= 5", "const double a = 1e-9;", "f(x5, 0x7)" })
                Assert.AreEqual(code == "const double a = 1e-9;", Number.IsMatch(CodeOf(code)), code);
        }

        [Test(Description = "K-49 every literal 5–10 in the WP-A core files is geometry-bound or justified")]
        public void K49_every_literal_5_to_10_in_the_WP_A_core_files_is_geometry_bound_or_justified()
        {
            var unexplained = new List<string>();
            foreach (var file in GeometryCore)
            {
                string path = Path.Combine(TestPaths.CoreSourceDir, file.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(path), "geometry core file missing: " + file);
                var rules = Justified.TryGetValue(file, out var r) ? r : new Rule[0];
                var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = CodeOf(lines[i]);
                    if (!Number.IsMatch(code)) continue;
                    if (rules.Any(rule => rule.Re.IsMatch(code))) continue;
                    unexplained.Add(file + ":" + (i + 1) + ": " + code);
                }
            }
            CollectionAssert.AreEqual(new string[0], unexplained, string.Join("\n", unexplained));
        }

        [Test(Description = "K-49 the only pre-2R board constant left (State.YardOccRows) is [Obsolete] and referenced nowhere in Core")]
        public void K49_the_only_pre_2R_board_constant_left_is_obsolete_and_unreferenced()
        {
            var field = typeof(State).GetField("YardOccRows", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field);
            Assert.IsTrue(field!.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length == 1);
            var refs = new List<string>();
            foreach (var path in Directory.GetFiles(TestPaths.CoreSourceDir, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Contains("YardOccRows")) refs.Add(Path.GetFileName(path) + ":" + (i + 1) + ": " + lines[i].Trim());
            }
            Assert.AreEqual(1, refs.Count, string.Join("\n", refs));
            StringAssert.Contains("[Obsolete(", refs[0]);
        }
    }
}
