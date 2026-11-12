#nullable enable
// Paket V1: the v4 atlas contract (docs/ART_UNITY.md §3) and every frame name the Unity build asks for. The Unity layer only
// turns these numbers into Sprite.Create arguments, so they are pinned here: page rects flipped to Unity's bottom-left origin,
// pixels-per-unit = 100 × scale, 9-slice borders in Unity's (L, B, R, T) order, every page file a PNG of the size atlas.json says.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class ArtContractTests
    {
        private static AtlasData? atlas;

        public static AtlasData Atlas => atlas ??= AtlasData.Parse(PresentationPaths.ResourceText("art/atlas")!);

        [Test]
        public void ART_UNITY_atlas_json_parses_488_frames_on_at_most_4_pages_within_the_memory_budget()
        {
            var a = Atlas;
            Assert.AreEqual(4, a.Version);
            Assert.AreEqual(100, a.Ppu);
            // B1 (3D atlas, art/3d/blender/env/README.md): pages <= 2048², RGBA32 <= 48 MB so that atlas + art3d/fx + art3d/king <= 64 MB
            Assert.That(a.Pages.Count >= 1 && a.Pages.Count <= 4, "1..4 pages, got " + a.Pages.Count);
            long bytes = 0;
            foreach (var p in a.Pages)
            {
                Assert.That(p.W <= 2048 && p.H <= 2048, p.Resource + " ≤ 2048²");
                bytes += 4L * p.W * p.H;
            }
            Assert.LessOrEqual(bytes, 48L * 1024 * 1024, "RGBA32 page memory");
            Assert.AreEqual(488 + 16, a.Count, "ART_UNITY §1: 488 frames + 16 v3 trait frames (§9, tools/art/extend.mjs, page 3; P4 added pallet_slab, D-108 the sled run pieces pallet_cell_l / _m / _r)");
            foreach (var f in a.Frames)
            {
                var p = a.Pages[f.Page];
                Assert.That(f.X >= 0 && f.Y >= 0 && f.X + f.W <= p.W && f.Y + f.H <= p.H, f + " inside its page");
                Assert.That(f.PivotX >= 0 && f.PivotX <= 1 && f.PivotY >= 0 && f.PivotY <= 1, f + " pivot 0..1");
                Assert.That(f.BorderL + f.BorderR <= f.W && f.BorderT + f.BorderB <= f.H, f + " borders inside the frame");
            }
        }

        [Test]
        public void ART_UNITY_unity_rect_is_flipped_ppu_is_100_times_scale_border_is_L_B_R_T()
        {
            var a = Atlas;
            var blk = a["blk_y_00"];
            int pageH = a.Pages[blk.Page].H;
            blk.UnityRect(pageH, out float x, out float y, out float w, out float h);
            Assert.AreEqual((blk.X, pageH - blk.Y - blk.H, blk.W, blk.H), ((int)x, (int)y, (int)w, (int)h));
            Assert.AreEqual(125f, blk.PixelsPerUnit(a.Ppu), 1e-4, "B1 3D atlas: scale 1,25 → 125 px per unit: a 150 px tile spans 120 design px");
            Assert.AreEqual(120, blk.DesignW, 1e-9);
            Assert.IsTrue(blk.HasAnchor);
            var tray = a["board_tray"];
            tray.UnityBorder(out float l, out float b, out float r, out float t);
            Assert.AreEqual((tray.BorderL, tray.BorderB, tray.BorderR, tray.BorderT), ((int)l, (int)b, (int)r, (int)t));
            Assert.AreEqual((108, 132, 108, 108), ((int)l, (int)b, (int)r, (int)t), "ART_UNITY §5: board_tray 72 72 72 88 design px × 1,5");
            Assert.IsTrue(tray.Sliced);
            Assert.AreEqual(0.0, a["chr_tuna_idle"].PivotY, 0.05, "characters stand on their feet (pivot 0,5 0,02)");
        }

        [Test]
        public void ART_UNITY_every_page_is_a_png_resource_of_its_size_with_a_plain_resources_name()
        {
            foreach (var p in Atlas.Pages)
            {
                Assert.IsFalse(Path.GetFileName(p.Resource).Contains("."), "P3-13: no dotted Resources name (" + p.Resource + ")");
                string file = Path.Combine(PresentationPaths.ResourcesDir, p.Resource + ".bytes");
                Assert.IsTrue(File.Exists(file), file);
                var b = File.ReadAllBytes(file);
                Assert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, b.Take(4).ToArray(), "PNG signature");
                int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                Assert.AreEqual((p.W, p.H), (w, h), p.Resource + " IHDR");
            }
        }

        [Test]
        public void ART_UNITY_a_broken_contract_is_refused_and_an_empty_atlas_has_no_frames()
        {
            Assert.Throws<FormatException>(() => AtlasData.Parse("{\"version\":4,\"atlases\":[{\"file\":\"a\",\"w\":10,\"h\":10}],\"frames\":{\"x\":{\"atlas\":0,\"x\":5,\"y\":5,\"w\":10,\"h\":10}}}"));
            Assert.Throws<FormatException>(() => AtlasData.Parse("{\"version\":4,\"atlases\":[],\"frames\":{\"x\":{\"atlas\":0,\"x\":0,\"y\":0,\"w\":1,\"h\":1}}}"));
            Assert.AreEqual(0, AtlasData.Empty.Count);
            Assert.IsNull(AtlasData.Empty.Find("blk_y_00"));
        }

        [Test]
        public void ART_UNITY_every_block_tile_ghost_shadow_symbol_and_stud_name_exists()
        {
            var a = Atlas;
            for (int c = 0; c < ColorCodes.All.Length; c++)
            {
                for (int m = 0; m < 16; m++) Assert.IsTrue(a.Has(Frames.Block(c, m)), Frames.Block(c, m));
                Assert.IsTrue(a.Has(Frames.Ghost(c, false)) && a.Has(Frames.Ghost(c, true)));
                Assert.IsTrue(a.Has(Frames.Sym(c)) && a.Has(Frames.Stud(c)));
            }
            for (int m = 0; m < 16; m++)
                Assert.IsTrue(a.Has(Frames.ShadowFill(m)) && a.Has(Frames.ShadowLine(m, false)) && a.Has(Frames.ShadowLine(m, true)), "shadow " + m);
        }

        public static IEnumerable<BlockLook> Looks => new[] { BlockLook.Holdable, BlockLook.Dim, BlockLook.Placed };

        [Test]
        public void K09_K14_ART_UNITY_block_parts_of_every_shape_colour_and_look_are_atlas_frames_inside_the_piece_box()
        {
            var a = Atlas;
            foreach (var shape in Shapes.All)
                for (int c = 0; c < ColorCodes.All.Length; c++)
                    foreach (var look in Looks)
                        foreach (var p in BlockSkin.Block(shape, c, 144, look, a))
                        {
                            Assert.IsTrue(a.Has(p.Frame), shape.Id + " " + look + ": " + p.Frame);
                            Assert.GreaterOrEqual(p.Rect.X, -shape.W * 72.0 - 1e-6, p.ToString());
                            Assert.LessOrEqual(p.Rect.Right, shape.W * 72.0 + 1e-6, p.ToString());
                            Assert.GreaterOrEqual(p.Rect.Y, -shape.H * 72.0 - 1e-6, p.ToString());
                            Assert.LessOrEqual(p.Rect.Bottom, shape.H * 72.0 + 1e-6, p.ToString());
                        }
        }

        [Test]
        public void ART_UNITY_corner_fills_close_exactly_the_inner_vertices_of_a_piece()
        {
            int Corners(string id) => BlockSkin.Block(Shapes.ShapeById(id + "_0"), 1, 120, BlockLook.Holdable).Count(p => p.Role == PartRole.Corner);
            Assert.AreEqual(0, Corners("B1"));
            Assert.AreEqual(0, Corners("I4"), "a straight piece has no inner vertex");
            Assert.AreEqual(0, Corners("L4"), "L: the bend is concave, not a closed 2 × 2");
            Assert.AreEqual(0, Corners("T4"));
            Assert.AreEqual(4, Corners("O4"), "O4: one inner vertex, one fill in each of its four cells");
            Assert.AreEqual(16, Corners("Q9"), "Q9: four inner vertices × four cells");
            var o4 = BlockSkin.Block(Shapes.ShapeById("O4_0"), 1, 120, BlockLook.Holdable).Where(p => p.Role == PartRole.Corner).ToList();
            foreach (var p in o4)
            {
                Assert.AreEqual(32, p.Rect.W, 1e-9, "a corner fill is 32 design px of a 120 px cell");
                Assert.IsTrue(Math.Abs(p.Rect.CenterX) < 20 && Math.Abs(p.Rect.CenterY) < 20, "every O4 fill sits at the piece centre: " + p);
            }
        }

        [Test]
        public void K09_K14_ART_UNITY_holdable_gloss_dim_tint_and_placed_mortar()
        {
            var shape = Shapes.ShapeById("D2_0");
            var hold = BlockSkin.Block(shape, 2, 120, BlockLook.Holdable, Atlas);
            Assert.AreEqual(2, hold.Count(p => p.Frame == Frames.Gloss), "K-09: one gloss per stud");
            Assert.IsTrue(hold.Where(p => p.Role == PartRole.Body).All(p => p.Tint.Equals(Rgba.White)));
            var g = hold.First(p => p.Frame == Frames.Gloss);
            var f = Atlas[Frames.Block(2, BoardPose.NeighbourMask(shape, shape.Cells[0]))];
            Assert.AreEqual(BlockSkin.GlossPx * f.StudD / BlockSkin.GlossStud, g.Rect.W, 1e-6, "gloss scale studD / 64");
            var dim = BlockSkin.Block(shape, 2, 120, BlockLook.Dim, Atlas);
            Assert.AreEqual(0, dim.Count(p => p.Frame == Frames.Gloss), "not holdable: no gloss");
            Assert.IsTrue(dim.Where(p => p.Role == PartRole.Body).All(p => p.Tint.Equals(Rgba.Hex("#EBEBEB"))), "blockV2.notHoldableTint");
            var placed = BlockSkin.Block(shape, 2, 120, BlockLook.Placed, Atlas);
            Assert.AreEqual(2, placed.Count(p => p.Frame == Frames.Locked), "K-14: mortar dots on every placed cell");
            Assert.AreEqual(0, placed.Count(p => p.Frame == Frames.Gloss));
            Assert.IsTrue(placed.Where(p => p.Role == PartRole.Body).All(p => p.Tint.Equals(Rgba.Hex("#EFEFEF"))));
        }

        [Test]
        public void K18_ART_UNITY_shadow_outline_line_or_dash_fill_tinted_concave_corners_closed()
        {
            var a = Atlas;
            foreach (var shape in Shapes.All)
                foreach (var outline in new[] { ShadowOutline.Valid, ShadowOutline.Invalid, ShadowOutline.Neutral })
                {
                    var look = LookOf(outline);
                    foreach (var p in BlockSkin.Shadow(shape, 3, 120, look)) Assert.IsTrue(a.Has(p.Frame), shape.Id + " " + outline + ": " + p.Frame);
                }
            var l4 = BlockSkin.Shadow(Shapes.ShapeById("L4_0"), 3, 120, LookOf(ShadowOutline.Valid));
            Assert.AreEqual(1, l4.Count(p => p.Role == PartRole.ShadowCorner), "L4 has one concave corner");
            Assert.IsTrue(l4.Where(p => p.Role == PartRole.ShadowLine).All(p => p.Frame.StartsWith("shadow_line_") && p.Tint.Equals(Rgba.Hex("#5CF59A"))), "correct: green line");
            Assert.IsTrue(l4.Where(p => p.Role == PartRole.ShadowFill).All(p => p.Tint.A == (byte)Math.Round(BlockSkin.ShadowFillAlpha * 255)), "fill α 0,28");
            var bad = BlockSkin.Shadow(Shapes.ShapeById("O4_0"), 3, 120, LookOf(ShadowOutline.Invalid));
            Assert.AreEqual(0, bad.Count(p => p.Role == PartRole.ShadowCorner), "O4 has no concave corner");
            Assert.IsTrue(bad.Where(p => p.Role == PartRole.ShadowLine).All(p => p.Frame.StartsWith("shadow_dash_") && p.Tint.Equals(Rgba.Hex("#FF4A3D"))), "wrong: red dash");
            var neutral = BlockSkin.Shadow(Shapes.ShapeById("O4_0"), 3, 120, LookOf(ShadowOutline.Neutral));
            Assert.IsTrue(neutral.Where(p => p.Role == PartRole.ShadowLine).All(p => p.Frame.StartsWith("shadow_dash_") && p.Tint.R == 255 && p.Tint.A == 153), "neutral: white dash α 0,6");
        }

        /// <summary>A shadow look with `outline` and a body (the private constructor: the look's fields are what the skin reads).</summary>
        private static ShadowLook LookOf(ShadowOutline outline)
        {
            var ctor = typeof(ShadowLook).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
            return (ShadowLook)ctor.Invoke(new object[] { outline, true, ShadowBadge.None, false, new At[0], new BoardCell[0] });
        }

        [Test]
        public void K34_ART_UNITY_the_board_of_every_level_draws_only_atlas_frames_tray_band_wall_cap_and_build_front([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var a = Atlas;
            var lvl = Play.LevelFile(level);
            var s = State.CreateInitialState(lvl);
            foreach (var h in new[] { 1920.0, 2337.0 })
            {
                var settings = new LayoutSettings();
                var L = ScreenLayout.Create(settings, h, lvl.Geo);
                var scene = BoardScene.Build(s, L);
                foreach (var f in scene.FrameNames()) Assert.IsTrue(a.Has(f), "B" + level + ": " + f);
                Assert.AreEqual(1, scene.Count(BoardItemKind.Tray));
                var tray = scene.Items.First(i => i.Kind == BoardItemKind.Tray).Rect;
                Assert.GreaterOrEqual(tray.X, 0);
                Assert.LessOrEqual(tray.Right, L.W + 1e-6);
                Assert.AreEqual(L.Board.Board.Bottom + BoardScene.TrayGrowBottom, tray.Bottom, 1e-6, "ART_UNITY §4.3: 64 px under the board");
                Assert.AreEqual(lvl.Geo.Cols + 1, scene.Items.Count(i => i.Kind == BoardItemKind.Crane && i.Frame == Frames.CraneBand), "one band per column + the wall strip");
                Assert.AreEqual(lvl.WallHeight > 0 ? 1 : 0, scene.Count(BoardItemKind.WallCap));
                Assert.AreEqual(lvl.Geo.Wy * lvl.Geo.Hy, scene.Items.Count(i => i.Kind == BoardItemKind.YardCell && i.Frame != null && i.Frame.StartsWith("floor_yard_")));
                // K-34 build front: per site column of the visible segment the lowest plan cell (nothing is filled at the start)
                int seg = Grid.VisibleSegment(s);
                int fronts = 0;
                for (int sx = 0; sx < lvl.Geo.Ws; sx++) if (BoardScene.FrontRow(s, seg, sx) >= 0) fronts++;
                Assert.AreEqual(fronts, scene.Count(BoardItemKind.BuildFront) + scene.Items.Count(i => i.Kind == BoardItemKind.HiddenCell && IsFrontCell(s, seg, i)),
                    "one ghost_front per column with an open plan cell");
                Assert.IsTrue(scene.Items.Where(i => i.ArtOnly).All(i => i.Frame != null), "an art-only item names its frame");
            }
        }

        private static bool IsFrontCell(GameState s, int seg, BoardItem i) => BoardScene.FrontRow(s, seg, i.X - s.Lvl.Geo.SiteX) == i.Y - State.Hdr(s, H.Elev);

        [Test]
        public void ART_UNITY_a_missing_frame_falls_back_to_a_placeholder_tile_never_a_white_box()
        {
            var blk = FallbackTiles.Of("blk_r_05");
            Assert.IsNotNull(blk);
            Assert.AreEqual(Art.Atlas.Body(5), blk!.Value.Tile);
            Assert.AreEqual(Palette.Block(3), blk.Value.Tint);
            Assert.IsNull(FallbackTiles.Of("blk_r_05_cf_bl"), "no notch to close on the placeholder");
            Assert.AreEqual(Tile.Plan, FallbackTiles.Of("ghost_g")!.Value.Tile);
            Assert.IsTrue(FallbackTiles.Of("btn_primary")!.Value.Sliced);
            Assert.IsNull(FallbackTiles.Of("chr_tuna_idle"), "characters: nothing rather than a box");
        }
    }
}
