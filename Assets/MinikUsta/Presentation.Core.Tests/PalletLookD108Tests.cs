#nullable enable
// D-108 (web docs/DECISIONS.md; ART_DIRECTION §3A.6, tokens check.palletV3c; GDD K-65 item 4) and CL-V3C-12: the pallet look in
// BlockSkin — the "×n" white circle badge first in the badge row, every badge ≥ 56 design px visible on the 9-column board,
// the grey-blue metal sled only under the lowest row, the covered-layer bands in the layer's own colour — and the shipped
// C3 / O4 pallets of levels 7–10 drawn that way. The atlas frames are art/src/trait/{ico_layers_n,pallet_cell,pallet_slab}.svg.
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class PalletLookD108Tests
    {
        /// <summary>The cell size of a 9-column board (3-column site, D-103): 108 design px, k = 0.9.</summary>
        private const double WideCell = 108;

        /// <summary>The visible disc of a round badge: 76 of its 88 px frame (outline Ø; the "×n" circle 82 of 88).</summary>
        private static double Visible(string frame, double size) => size * (frame.StartsWith("ico_layers_") ? 82.0 : 76.0) / 88.0;

        [Test(Description = "Y10 K-65 item 4 D-108 the layer count badge is the first badge of the row, the bonus star and the others follow it")]
        public void Y10_K65_item_4_D108_the_layer_count_badge_comes_first()
        {
            var t = new BlockTraits(null, false, false, 2, true, 4, 0, false);
            CollectionAssert.AreEqual(new[] { "ico_layers_4", "ico_bonus_2", Frames.IcoSlip }, BlockSkin.BadgeFrames(t));
            var parts = BlockSkin.Block(Shapes.ShapeById("C3_0"), 0, WideCell, BlockLook.Holdable, null, t).Where(p => p.Role == PartRole.Badge).ToList();
            CollectionAssert.AreEqual(new[] { "ico_layers_4", "ico_bonus_2", Frames.IcoSlip }, parts.Select(p => p.Frame).ToArray());
            Assert.Greater(parts[0].Rect.W, parts[1].Rect.W, "the ×n circle is the larger badge (Ø 76 against 70 at a 120 px cell)");
            for (int i = 1; i < parts.Count; i++)
                Assert.Greater(parts[i].Rect.X + parts[i].Rect.W / 2, parts[i - 1].Rect.X + parts[i - 1].Rect.W / 2, "slot " + i + " right of slot " + (i - 1));
            // every badge centre on one line
            var centres = parts.Select(p => p.Rect.Y + p.Rect.H / 2).ToList();
            Assert.Less(centres.Max() - centres.Min(), 1e-9);
        }

        [TestCase(120.0)]
        [TestCase(WideCell)]
        [TestCase(100.0)]
        [TestCase(72.0)]
        [Test(Description = "Y10 K-65 item 4 D-108 CL-V3C-12 no badge shows under 56 design px, the ×n circle is Ø 76 at a 120 px cell and its digit stays ≥ kit.badge.fontPx × k")]
        public void Y10_K65_item_4_D108_CL_V3C_12_badges_never_shrink_under_56_design_px(double cellPx)
        {
            foreach (string frame in new[] { "ico_layers_2", "ico_layers_3", "ico_layers_4", "ico_bonus_2", Frames.IcoSlip, Frames.IcoScissors, Frames.WrapKnot })
            {
                double size = BlockSkin.BadgeSize(frame, cellPx);
                Assert.GreaterOrEqual(Visible(frame, size), 56, frame + " at cell " + cellPx);
            }
            if (cellPx == 120) Assert.AreEqual(76, Visible("ico_layers_3", BlockSkin.BadgeSize("ico_layers_3", 120)), 0.5, "Ø 76 (tokens check.palletV3c.badge.diameterPx)");
            // the "×n" digit is 58 px Baloo 2 in the 88 px frame: font px on screen ≥ kit.badge.fontPx (36) × k (ART_REFERENCE k = 0.8955)
            double fontPx = 58 * BlockSkin.BadgeSize("ico_layers_3", cellPx) / 88;
            Assert.GreaterOrEqual(fontPx, 36 * 0.8955, "digit font px at cell " + cellPx);
        }

        [Test(Description = "Y10 K-65 item 4 D-108 the metal sled sits only under the lowest row (a T4 overhang floats no sled) and the covered-layer bands take the layer's colour, one band per layer, 6 px apart")]
        public void Y10_K65_item_4_D108_sled_under_the_lowest_row_and_bands_in_the_layer_colour()
        {
            var t4 = Shapes.ShapeById("T4_0"); // cells (0,0) (0,1) (1,1) (0,2): one lowest-row cell
            var deep = new BlockTraits(null, false, false, 0, false, 0, 3, true);
            var parts = BlockSkin.Block(t4, 2, 120, BlockLook.Dim, null, deep);
            var sled = parts.Where(p => Frames.IsPalletSled(p.Frame)).ToList();
            var bands = parts.Where(p => p.Frame == Frames.PalletSlab).ToList();
            Assert.AreEqual(1, sled.Count, "T4_0: one lowest-row cell, one sled frame");
            Assert.AreEqual(Frames.PalletCell, sled[0].Frame, "a one-cell run has both end caps");
            Assert.AreEqual(1, bands.Count, "one band per lowest-row cell for this layer");
            var low = BlockSkin.CellRect(t4, t4.Cells[0], 120);
            Assert.AreEqual(low.Bottom + 2 * BlockSkin.PalletSlabStep, bands[0].Rect.Y, 1e-9, "depth 3: the third band, two steps under the first");
            Assert.AreEqual(low.Bottom + 3 * BlockSkin.PalletSlabStep - BlockSkin.PalletTuck, sled[0].Rect.Y, 1e-9, "the sled under the last band, its top tucked under it");
            Assert.AreEqual(16, sled[0].Rect.Bottom - (low.Bottom + 3 * BlockSkin.PalletSlabStep), 1e-9, "a 16 px lip (tokens check.palletV3c.sled.lipPx)");
            Assert.AreEqual(Palette.Block(2), bands[0].Tint, "the band shows the layer's own colour");
            Assert.AreEqual(low.W, bands[0].Rect.W, 1e-9, "a band is as wide as its cell");
            Assert.IsTrue(sled.All(p => p.Order < bands[0].Order && p.Order < 0), "sled under the bands and under every body");
            // C3_0: two lowest-row cells, the upper-left cell draws neither
            var c3 = Shapes.ShapeById("C3_0");
            var c3parts = BlockSkin.Block(c3, 0, 120, BlockLook.Dim, null, new BlockTraits(null, false, false, 0, false, 0, 1, true));
            CollectionAssert.AreEqual(new[] { Frames.PalletCellL, Frames.PalletCellR }, c3parts.Where(p => Frames.IsPalletSled(p.Frame)).OrderBy(p => p.Rect.X).Select(p => p.Frame).ToArray());
            Assert.AreEqual(Frames.PalletCellM, Frames.PalletSled(true, true));
            Assert.AreEqual(2, c3parts.Count(p => p.Frame == Frames.PalletSlab));
        }

        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [Test(Description = "Y10 K-65 item 4 D-108 the shipped pallet of level N: its top layer shows ×n (n = the layers) as the first badge, the covered layers draw bands and the lowest the sled")]
        public void Y10_K65_item_4_D108_shipped_pallets_draw_the_badge_bands_and_sled(int level)
        {
            var rig = new Rig();
            rig.Load(level);
            rig.Settle();
            var s = rig.Session.State;
            var layers = Enumerable.Range(0, s.Lvl.Pieces.Length).Where(id => s.Lvl.Pieces[id].StackId >= 0 && PieceZone(s, id) == Zone.Yard).ToList();
            Assert.GreaterOrEqual(layers.Count, 2, "level " + level + " has a pallet");
            var top = layers.Single(id => rig.View.Live[id].Traits.PalletLayers > 0);
            var tt = rig.View.Live[top].Traits;
            Assert.AreEqual(layers.Count, tt.PalletLayers);
            var shape = Shapes.ShapeByIndex(PieceShape(s, top));
            var topBadges = BlockSkin.Block(shape, PieceColor(s, top), WideCell, BlockLook.Holdable, null, tt).Where(p => p.Role == PartRole.Badge).ToList();
            Assert.AreEqual(Frames.IcoLayers(layers.Count), topBadges[0].Frame);
            Assert.GreaterOrEqual(Visible(topBadges[0].Frame, topBadges[0].Rect.W), 56);
            int lowest = layers.Single(id => rig.View.Live[id].Traits.PalletBase);
            var lt = rig.View.Live[lowest].Traits;
            Assert.AreEqual(layers.Count - 1, lt.PalletDepth);
            var lowParts = BlockSkin.Block(shape, PieceColor(s, lowest), WideCell, BlockLook.Dim, null, lt);
            int bottomCells = shape.Cells.Count(c => shape.Cells.All(o => o.Y >= c.Y));
            Assert.AreEqual(bottomCells, lowParts.Count(p => Frames.IsPalletSled(p.Frame)), "one sled frame per lowest-row cell");
            Assert.AreEqual(bottomCells, lowParts.Count(p => p.Frame == Frames.PalletSlab), "the lowest covered layer's band");
        }
    }
}
