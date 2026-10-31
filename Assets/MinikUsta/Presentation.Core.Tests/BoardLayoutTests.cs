#nullable enable
// Board and screen layout (port of web tests/theme/boardLayout.test.ts; UX_FLOWS §5.1, §5.8; TECH §2R.1; GDD K-49):
// the UX §5.8 table of levels 1–10, the adaptive cell, EXPAND shifts, the board fitting FIT and EXPAND on the 9 K-49
// geometries, hit-test round trips, and the design ↔ screen ↔ world mapping of the Unity camera.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class BoardLayoutTests
    {
        private static readonly LayoutSettings S = new LayoutSettings();

        /// <summary>The same tokens without `layout.adaptive.cellMaxPx` (R2-02 "hücre 120 px korunur"; web T120).</summary>
        private static readonly LayoutSettings S120 = new LayoutSettings { CellMaxPx = 120 };

        private static BoardGeo Geo(int wy, int hy, int ws, int hs) => Geometry.MakeGeo(new GeoParams(wy, hy, ws, hs, 0));

        /// <summary>The geometry set G of TECH §2R.1 (+ WP-A's 6×5 | 2×7) = Core.Tests GeometryTests.G.</summary>
        public static readonly BoardGeo[] G =
        {
            Geo(4, 4, 2, 5), Geo(4, 4, 2, 6), Geo(4, 5, 2, 5), Geo(4, 5, 2, 7), Geo(6, 5, 2, 6), Geo(6, 5, 2, 7), Geo(6, 8, 2, 8), Geo(5, 7, 3, 7), Geo(4, 6, 4, 6),
        };

        private static string Label(BoardGeo g) => g.Wy + "x" + g.Hy + "|" + g.Ws + "x" + g.Hs;

        /// <summary>UX §5.8 example table (FIT, boardBottomY 1488, adaptive c with cellMaxPx 144).</summary>
        public sealed class Row
        {
            public string Name = "";
            public BoardGeo Geo = Geometry.DefaultGeo;
            public int Wall;
            public double C;
            public double[] X = new double[4];
            public double YardTop, BoardTop, CraneTop;
            public int Air, Notch;
            public double Scene, Gap390;
            public override string ToString() => Name;
        }

        public static readonly Row[] Table =
        {
            new Row { Name = "B1, B2, B3, B5", Geo = Geo(4, 4, 2, 5), Wall = 4, C = 144, X = new double[] { 936, 72, 648, 720 }, YardTop = 912, BoardTop = 768, CraneTop = 480, Air = 1, Notch = 3, Scene = 192, Gap390 = 424 },
            new Row { Name = "B4, B9", Geo = Geo(4, 4, 2, 6), Wall = 4, C = 144, X = new double[] { 936, 72, 648, 720 }, YardTop = 912, BoardTop = 624, CraneTop = 336, Air = 2, Notch = 4, Scene = 48, Gap390 = 280 },
            new Row { Name = "B6", Geo = Geo(4, 5, 2, 7), Wall = 7, C = 132, X = new double[] { 858, 111, 639, 705 }, YardTop = 828, BoardTop = 564, CraneTop = 300, Air = 2, Notch = 2, Scene = 12, Gap390 = 244 },
            new Row { Name = "B7", Geo = Geo(4, 5, 2, 5), Wall = 5, C = 144, X = new double[] { 936, 72, 648, 720 }, YardTop = 768, BoardTop = 768, CraneTop = 480, Air = 0, Notch = 2, Scene = 192, Gap390 = 424 },
            new Row { Name = "B8", Geo = Geo(6, 5, 2, 6), Wall = 5, C = 120, X = new double[] { 1020, 30, 750, 810 }, YardTop = 888, BoardTop = 768, CraneTop = 528, Air = 1, Notch = 3, Scene = 240, Gap390 = 472 },
            new Row { Name = "B10", Geo = Geo(6, 5, 2, 6), Wall = 6, C = 120, X = new double[] { 1020, 30, 750, 810 }, YardTop = 888, BoardTop = 768, CraneTop = 528, Air = 1, Notch = 2, Scene = 240, Gap390 = 472 },
            new Row { Name = "default (Faz 3)", Geo = Geometry.DefaultGeo, Wall = 8, C = 120, X = new double[] { 1020, 30, 750, 810 }, YardTop = 528, BoardTop = 528, CraneTop = 288, Air = 0, Notch = 2, Scene = 0, Gap390 = 232 },
        };

        [Test]
        public void UX_5_8_table_row_c_x_y_yard_air_notch_scene_above([ValueSource(nameof(Table))] Row row)
        {
            var L = ScreenLayout.Create(S, 1920, row.Geo);
            var g = L.Grid;
            double c = row.C;
            Assert.AreEqual(c, g.CellPx);
            Assert.AreEqual(c, BoardLayout.AdaptiveCellPx(S, row.Geo));
            CollectionAssert.AreEqual(row.X, new[] { L.Board.Board.W, g.YardX, g.WallX, g.BuildX });
            CollectionAssert.AreEqual(new[] { row.YardTop, row.BoardTop, row.CraneTop }, new[] { g.YardTopY, g.BoardTopY, g.CraneTopY });
            Assert.AreEqual(new PxRect(g.YardX, row.YardTop, row.Geo.Wy * c, row.Geo.Hy * c), L.Board.Yard);
            Assert.AreEqual(row.Air, L.Board.YardAir.H / c);
            Assert.AreEqual(new PxRect(g.YardX, row.CraneTop, row.X[0], 2 * c), L.Board.Crane);
            Assert.AreEqual(row.Notch, row.Geo.H + 2 - row.Wall);
            Assert.AreEqual(row.Wall * c, g.WallRect(row.Wall).H);
            // UX §5.8 rule 2: the scene opened above = crane-area top − 288 (the default board's crane top)
            Assert.AreEqual(row.Scene, g.CraneTopY - 288);
            Assert.GreaterOrEqual(g.CraneTopY - L.TopGroupBottomY, S.MinGapTopPx);
            // last column: HUD bottom → crane-area top at 390 × 844 (EXPAND, +208,5; UX writes the floor)
            var tall = ScreenLayout.Create(S, ScreenFit.DesignHeight(ScaleMode.Expand, 390, 844, S), row.Geo);
            Assert.AreEqual(row.Gap390, Math.Floor(tall.Grid.CraneTopY - tall.TopGroupBottomY));
        }

        [Test]
        public void UX_5_8_EXPAND_moves_every_board_y_by_H_minus_1920_times_0_5()
        {
            foreach (var (w, h, shift) in new[] { (390, 844, 208.0), (360, 800, 240.0) })
            {
                double H = ScreenFit.DesignHeight(ScaleMode.Expand, w, h, S);
                foreach (var row in Table)
                {
                    var g = ScreenLayout.Create(S, H, row.Geo).Grid;
                    Assert.LessOrEqual(Math.Abs(g.BoardTopY - row.BoardTop - shift), 0.5, row.Name + " " + w + "×" + h);
                    Assert.LessOrEqual(Math.Abs(g.CraneTopY - row.CraneTop - shift), 0.5, row.Name + " " + w + "×" + h);
                }
            }
            Assert.AreEqual(2337, ScreenFit.DesignHeight(ScaleMode.Expand, 390, 844, S), "1080 · 844 / 390 = 2337,2");
            Assert.AreEqual(2400, ScreenFit.DesignHeight(ScaleMode.Expand, 360, 800, S));
            Assert.AreEqual(1920, ScreenFit.DesignHeight(ScaleMode.Fit, 390, 844, S), "FIT keeps 1920");
            Assert.AreEqual(1920, ScreenFit.DesignHeight(ScaleMode.Expand, 1200, 1600, S), "a wide screen clamps to 1920");
        }

        [Test]
        public void UX_5_8_rule_5_the_site_ribbon_is_max_306_or_wall_plus_site_wide_over_wall_and_site_in_the_board_middle()
        {
            var def = ScreenLayout.Create(S, 1920).Board.SiteRibbon;
            Assert.AreEqual(new PxRect(750, 960, 306, 96), def, "the default board keeps the token rect");
            var b1 = ScreenLayout.Create(S, 1920, Geo(4, 4, 2, 5));
            Assert.AreEqual(360, b1.Board.SiteRibbon.W, "wallW 72 + 2 × 144");
            Assert.AreEqual(b1.Grid.WallX, b1.Board.SiteRibbon.X);
            Assert.AreEqual(b1.Grid.BoardTopY + 5 * 144 / 2.0, b1.Board.SiteRibbon.CenterY);
            Assert.AreEqual(306, ScreenLayout.Create(S120, 1920, Geo(4, 4, 2, 5)).Board.SiteRibbon.W, "at c = 120 the token width wins");
            var wide = ScreenLayout.Create(S, 1920, Geo(4, 6, 4, 6));
            Assert.AreEqual(60 + 4 * 120, wide.Board.SiteRibbon.W);
            Assert.AreEqual(-6, wide.Board.SiteRibbonTiltDeg);
        }

        [Test]
        public void UX_5_8_adaptive_cell_B1_to_B10_is_144_132_120_and_120_without_cellMaxPx()
        {
            var want = new Dictionary<int, double> { { 1, 144 }, { 2, 144 }, { 3, 144 }, { 4, 144 }, { 5, 144 }, { 6, 132 }, { 7, 144 }, { 8, 120 }, { 9, 144 }, { 10, 120 } };
            foreach (var n in Play.Levels)
            {
                var geo = Play.LevelFile(n).Geo;
                Assert.AreEqual(want[n], BoardLayout.AdaptiveCellPx(S, geo), "B" + n);
                Assert.AreEqual(120, BoardLayout.AdaptiveCellPx(S120, geo), "B" + n + " at 120");
            }
            Assert.AreEqual(120, BoardLayout.AdaptiveCellPx(S, Geometry.DefaultGeo));
            var L = ScreenLayout.Create(S, 1920, Geo(4, 4, 2, 5));
            Assert.AreEqual(936, L.Board.Board.W);
            Assert.AreEqual(72, L.Grid.YardX);
            Assert.AreEqual(72, 1080 - L.Board.Board.Right);
            Assert.AreEqual(1.2, L.Grid.K, 1e-12);
            Assert.AreEqual(72, L.Grid.WallW, 1e-9);
            Assert.AreEqual(30 * 1.2, L.HitSlopPx, 1e-9);
            foreach (var g in G) Assert.AreEqual(0, BoardLayout.AdaptiveCellPx(S, g) % S.CellStepPx, Label(g));
            // 360 × 800: a B1 cell is 144 · 360 / 1080 = 48 CSS px (TECH §2R.1 screenshot gate), 40 at 120
            double H = ScreenFit.DesignHeight(ScaleMode.Expand, 360, 800, S);
            Assert.AreEqual(48, ScreenLayout.Create(S, H, Geo(4, 4, 2, 5)).Grid.CellPx * 360 / 1080, 1e-9);
            Assert.AreEqual(40, ScreenLayout.Create(S120, H, Geo(4, 4, 2, 5)).Grid.CellPx * 360 / 1080, 1e-9);
        }

        [Test]
        public void K49_UX_5_8_the_board_fits_FIT_and_EXPAND_on_every_geometry([ValueSource(nameof(G))] BoardGeo geo)
        {
            var profiles = new (string, double)[]
            {
                ("FIT", 1920), ("390×844", ScreenFit.DesignHeight(ScaleMode.Expand, 390, 844, S)), ("360×800", ScreenFit.DesignHeight(ScaleMode.Expand, 360, 800, S)),
                ("tallest", S.ExpandMaxHeight),
            };
            foreach (var (name, H) in profiles)
            {
                foreach (var s in new[] { S120, S })
                {
                    var L = ScreenLayout.Create(s, H, geo);
                    var g = L.Grid;
                    string at = Label(geo) + " " + name + " c" + g.CellPx;
                    Assert.GreaterOrEqual(g.YardX, s.MinSideMarginPx, at + ": left margin");
                    Assert.GreaterOrEqual(L.W - g.SiteRight, s.MinSideMarginPx, at + ": right margin");
                    Assert.GreaterOrEqual(g.CraneTopY - L.TopGroupBottomY, s.MinGapTopPx, at + ": under the HUD");
                    Assert.AreEqual(g.BoardBottomY, L.Board.Board.Bottom, at);
                    Assert.GreaterOrEqual(L.Board.Status.Y, g.BoardBottomY, at + ": status strip under the board");
                    Assert.LessOrEqual(L.Board.Status.Bottom, L.BottomGroupTopY, at + ": status strip above the bottom group");
                    Assert.AreEqual(L.Board.Board.W, L.Board.Crane.W, at);
                    Assert.AreEqual(g.YardX, L.Board.Status.X, at);
                    // HUD and booster bar inside the canvas, booster bar under the status strip
                    foreach (var r in new[] { L.Pause, L.Panorama, L.Goals, L.Moves, L.LevelCapsule })
                        Assert.IsTrue(r.X >= 0 && r.Right <= L.W && r.Y >= 0 && r.Bottom <= L.TopGroupBottomY, at + ": HUD rect " + r);
                    Assert.AreEqual(s.BoosterSlots, L.Boosters.Length);
                    foreach (var r in L.Boosters)
                        Assert.IsTrue(r.X >= 0 && r.Right <= L.W && r.Y >= L.BottomGroupTopY && r.Bottom <= L.H, at + ": booster " + r);
                    Assert.GreaterOrEqual(L.Boosters[0].W, s.MinTargetPx, at + ": booster tap target");
                    Assert.AreEqual(new PxRect(0, 0, L.W, s.TopGroupBottomY), L.HudBand, at);
                    Assert.IsFalse(L.HudBand.Overlaps(L.Board.Crane), at + ": HUD band above the crane area");
                    Assert.IsFalse(L.BoosterBar.Overlaps(L.Board.Status), at + ": booster bar under the status strip");
                    Assert.AreEqual(L.Boosters[0].X, L.BoosterBar.X);
                    Assert.AreEqual(L.Boosters[L.Boosters.Length - 1].Right, L.BoosterBar.Right);
                }
            }
        }

        [Test]
        public void K49_hit_test_round_trip_cellAt_of_cellRect_is_the_cell_and_anchorXAt_inverts_pieceX([ValueSource(nameof(G))] BoardGeo geo)
        {
            foreach (var s in new[] { S120, S })
            {
                var L = BoardLayout.Create(s, geo, 1920).Grid;
                for (int y = 0; y < geo.Rows; y++)
                {
                    for (int x = 0; x < geo.Cols; x++)
                    {
                        var r = L.CellRect(x, y);
                        foreach (var (px, py) in new[] { (r.CenterX, r.CenterY), (r.X, r.Y), (r.X + r.W - 0.01, r.Y + r.H - 0.01) })
                        {
                            Assert.IsTrue(L.CellAt(px, py, out int cx, out int cy), Label(geo) + " cell " + x + "," + y);
                            Assert.AreEqual((x, y), (cx, cy), Label(geo) + " cell " + x + "," + y);
                        }
                    }
                }
                Assert.IsFalse(L.CellAt(L.WallX + L.WallW / 2, L.RowTop(0) + 1, out _, out _), "over the wall strip");
                Assert.IsFalse(L.CellAt(L.YardX - 1, L.RowTop(0) + 1, out _, out _), "left of the yard");
                Assert.IsFalse(L.CellAt(L.SiteRight + 1, L.RowTop(0) + 1, out _, out _), "right of the site");
                Assert.IsFalse(L.CellAt(L.YardX + 1, L.CraneTopY - 1, out _, out _), "above the crane area");
                Assert.IsFalse(L.CellAt(L.YardX + 1, L.BoardBottomY, out _, out _), "the bottom edge is below row 0");
                foreach (int w in new[] { 1, 2, 3 })
                    for (double ax = 0; ax <= geo.Cols - w; ax += 0.25)
                        Assert.AreEqual(ax, L.AnchorXAt(L.PieceX(ax, w), w), 1e-9, Label(geo) + " w " + w + " ax " + ax);
                Assert.AreEqual(2.5, L.AnchorYAt(L.PieceY(2.5, 2), 2), 1e-9);
                // R-03: continuous columns over the wall strip; the middle of the strip is the zero-width boundary x = wy
                Assert.AreEqual(2.5, L.PointColumn(L.ColLeft(2) + L.CellPx / 2), 1e-9);
                Assert.AreEqual(geo.Wy + 0.5, L.PointColumn(L.ColLeft(geo.Wy) + L.CellPx / 2), 1e-9);
                Assert.AreEqual(geo.Wy, L.PointColumn(L.WallX + L.WallW / 2), 1e-9);
                Assert.AreEqual(3.5, L.PointRow(L.RowTop(3) + L.CellPx / 2), 1e-9);
                L.PointOf(4.25, 6.75, out double sx, out double sy);
                Assert.AreEqual(4.25, L.PointColumn(sx), 1e-9);
                Assert.AreEqual(6.75, L.PointRow(sy), 1e-9);
            }
        }

        [Test]
        public void K49_every_yard_block_of_levels_1_to_10_is_picked_at_every_cell_centre_and_its_slop()
        {
            foreach (var n in Play.Levels)
            {
                var lvl = Play.LevelFile(n);
                var s = CreateInitialState(lvl);
                var L = ScreenLayout.Create(S, 1920, lvl.Geo);
                int checkedCells = 0;
                for (int id = 0; id < lvl.Layout.Counts.Pieces; id++)
                {
                    if (PieceZone(s, id) != Zone.Yard) continue;
                    foreach (var c in Grid.PieceBoardCells(s, id))
                    {
                        var r = L.Grid.CellRect(c.X, c.Y);
                        Assert.AreEqual(id, HitTest.PieceAtPoint(s, L.Grid, r.CenterX, r.CenterY, L.HitSlopPx), "B" + n + " piece " + id + " cell " + c.X + "," + c.Y);
                        checkedCells++;
                    }
                }
                Assert.Greater(checkedCells, 0, "B" + n);
                // an empty yard air cell picks nothing; a point inside the slop of a block's cell picks that block
                var air = L.Grid.CellRect(0, lvl.Geo.Rows - 1);
                Assert.AreEqual(-1, HitTest.PieceAtPoint(s, L.Grid, air.CenterX, air.CenterY, L.HitSlopPx), "B" + n + " crane row");
            }
        }

        [Test]
        public void TECH_10_3_the_slop_grown_hit_goes_to_the_nearest_cell_centre()
        {
            var lvl = Play.LevelFile(1); // a (0,2) and b (1,2): D2_0 verticals side by side
            var s = CreateInitialState(lvl);
            var L = ScreenLayout.Create(S, 1920, lvl.Geo);
            var a = L.Grid.CellRect(0, 2);
            Assert.AreEqual(0, HitTest.PieceAtPoint(s, L.Grid, a.Right - 1, a.CenterY, L.HitSlopPx), "inside a, near b");
            Assert.AreEqual(1, HitTest.PieceAtPoint(s, L.Grid, a.Right + 1, a.CenterY, L.HitSlopPx), "just inside b");
            // above the top block cell of a, inside its slop (the row above is empty yard air)
            var top = L.Grid.CellRect(0, 3);
            Assert.AreEqual(0, HitTest.PieceAtPoint(s, L.Grid, top.CenterX, top.Y - L.HitSlopPx + 1, L.HitSlopPx));
            Assert.AreEqual(-1, HitTest.PieceAtPoint(s, L.Grid, top.CenterX, top.Y - L.HitSlopPx - 1, L.HitSlopPx));
            Assert.IsTrue(L.OnBoard(L.Board.Crane.X - L.HitSlopPx, L.Board.Crane.Y));
            Assert.IsFalse(L.OnBoard(L.Moves.CenterX, L.Moves.CenterY), "the HUD is not the board zone");
            Assert.IsFalse(L.OnBoard(L.Boosters[0].CenterX, L.Boosters[0].CenterY), "the booster bar is not the board zone");
        }

        [Test]
        public void ScreenFit_design_screen_world_round_trips_and_the_camera_shows_the_whole_canvas()
        {
            foreach (var (w, h) in new[] { (390.0, 844.0), (360.0, 800.0), (1080.0, 1920.0), (768.0, 1024.0), (1170.0, 2532.0) })
            {
                foreach (var mode in new[] { ScaleMode.Fit, ScaleMode.Expand })
                {
                    var vp = ScreenFit.Viewport(mode, w, h, S);
                    string at = w + "×" + h + " " + mode;
                    Assert.LessOrEqual(vp.W * vp.Scale, w + 1e-9, at + ": canvas width fits");
                    Assert.LessOrEqual(vp.H * vp.Scale, h + 1e-9, at + ": canvas height fits");
                    Assert.IsTrue(Math.Abs(vp.W * vp.Scale - w) < 1e-6 || Math.Abs(vp.H * vp.Scale - h) < 1e-6, at + ": one side touches the screen");
                    Assert.AreEqual(w - vp.W * vp.Scale, vp.PillarboxPx, 1e-9);
                    Assert.AreEqual(h - vp.H * vp.Scale, vp.LetterboxPx, 1e-9);
                    foreach (var (dx, dy) in new[] { (0.0, 0.0), (540.0, 960.0), (1080.0, vp.H), (123.4, 1500.25) })
                    {
                        vp.DesignToScreen(dx, dy, out double sx, out double sy);
                        vp.ScreenToDesign(sx, sy, out double bx, out double by);
                        Assert.AreEqual(dx, bx, 1e-9, at);
                        Assert.AreEqual(dy, by, 1e-9, at);
                        vp.DesignToWorld(dx, dy, ScreenFit.DefaultPixelsPerUnit, out double wx, out double wy);
                        vp.WorldToDesign(wx, wy, ScreenFit.DefaultPixelsPerUnit, out double cx, out double cy);
                        Assert.AreEqual(dx, cx, 1e-9, at);
                        Assert.AreEqual(dy, cy, 1e-9, at);
                    }
                    // the canvas centre is the world origin; the top-left design corner is up-left
                    vp.DesignToWorld(vp.W / 2, vp.H / 2, 100, out double ox, out double oy);
                    Assert.AreEqual((0.0, 0.0), (ox, oy));
                    vp.DesignToWorld(0, 0, 100, out double tlx, out double tly);
                    Assert.IsTrue(tlx < 0 && tly > 0, at);
                    // orthographic size covers the full screen height; the canvas half height fits inside it
                    Assert.GreaterOrEqual(vp.OrthographicSize(100) * 100 * 2, vp.H - 1e-9, at);
                    Assert.AreEqual(h / vp.Scale / 2 / 100, vp.OrthographicSize(100), 1e-9, at);
                }
            }
            // 390 × 844 FIT: width-limited, 1080 → 390 px, 1920 · 0,3611 = 693 px tall, the rest is letterbox
            var fit = ScreenFit.Viewport(ScaleMode.Fit, 390, 844, S);
            Assert.AreEqual(390.0 / 1080, fit.Scale, 1e-12);
            Assert.AreEqual(844 - 1920 * 390.0 / 1080, fit.LetterboxPx, 1e-9);
            Assert.AreEqual(0, fit.PillarboxPx, 1e-9);
            // the bottom-left screen pixel of the canvas (above the lower letterbox band) is design (0, 1920)
            fit.ScreenToDesign(0, fit.LetterboxPx / 2, out double x0, out double y0);
            Assert.AreEqual((0.0, 1920.0), (Math.Round(x0, 9), Math.Round(y0, 9)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenLayout.Create(S, 1800));
        }
    }
}
