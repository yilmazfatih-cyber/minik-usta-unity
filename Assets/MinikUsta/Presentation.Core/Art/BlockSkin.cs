#nullable enable
// How a block and its K-18 fall shadow are put together from the v4 atlas (paket V1; docs/ART_UNITY.md §4.1–§4.2). Pure:
// a list of frames, each drawn into a design px rectangle relative to the piece box centre (y down). The Unity piece visual,
// the shadow view and the compose tool draw exactly these parts.
//
// Block (ART_UNITY §4.1): per cell `blk_<c>_<mm>` covering the cell; at a corner where both orthogonal neighbours AND the
// diagonal are in the piece the 32 px corner fill `…_cf_<k>` closes the inner notch, so connected cells read as one block
// (no inner edges). Holdable (K-09): `blk_gloss` on the stud anchor; not holdable: tint #EBEBEB, no gloss; placed (K-14):
// tint #EFEFEF + `blk_locked` (four mortar dots), no gloss.
// Shadow (§4.2): per cell `shadow_fill_<mm>` tinted with the block colour (α 0,28) + the outline `shadow_line_<mm>` (#5CF59A,
// correct) or `shadow_dash_<mm>` (#FF4A3D wrong, white α 0,6 neutral); the shadow tiles assume the diagonal is present, so a
// concave corner (two neighbours, no diagonal) adds `shadow_<line|dash>_<mm>_cc_<k>`.
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Game;

namespace MinikUsta.Presentation.Art
{
    public enum BlockLook
    {
        /// <summary>K-09 holdable: gloss on the stud.</summary>
        Holdable,
        /// <summary>K-09 not holdable (yard): tint #EBEBEB, no gloss.</summary>
        Dim,
        /// <summary>K-14 placed on the site: tint #EFEFEF + mortar dots.</summary>
        Placed,
    }

    public enum PartRole
    {
        Body,
        Corner,
        Gloss,
        Locked,
        ShadowFill,
        ShadowLine,
        ShadowCorner,
        /// <summary>v3 trait overlay over every cell (wrap net, frost sheen); drawn above the body, removable (`TraitFx`).</summary>
        Overlay,
        /// <summary>v3 trait badge on the top-left cell (knot, scissors, "+n" star, snowflake, layer count); removable (`TraitFx`).</summary>
        Badge,
        /// <summary>v3 Y10 pallet planks under the lowest layer (cell-sized plus a 10 px rim left, right and below).</summary>
        Planks,
    }

    /// <summary>One frame of a composed block / shadow: drawn INTO `Rect` (design px, relative to the piece box centre, y down).</summary>
    public readonly struct SpritePart
    {
        public readonly string Frame;
        public readonly PartRole Role;
        /// <summary>Index of the cell in `ShapeDef.Cells`.</summary>
        public readonly int Cell;
        public readonly PxRect Rect;
        public readonly Rgba Tint;
        /// <summary>Added to the visual's base sorting order (body 0, corner 1, gloss / locked 2).</summary>
        public readonly int Order;

        public SpritePart(string frame, PartRole role, int cell, PxRect rect, Rgba tint, int order)
        {
            Frame = frame;
            Role = role;
            Cell = cell;
            Rect = rect;
            Tint = tint;
            Order = order;
        }

        public override string ToString() => Frame + " " + Rect;
    }

    public static class BlockSkin
    {
        /// <summary>blockV2.notHoldableTint.</summary>
        public static readonly Rgba DimTint = Rgba.Hex("#EBEBEB");
        /// <summary>ART_UNITY §4.1 placed tint.</summary>
        public static readonly Rgba PlacedTint = Rgba.Hex("#EFEFEF");
        /// <summary>Corner fill / shadow corner edge, design px of a 120 px cell.</summary>
        public const double CornerPx = 32;
        /// <summary>`blk_gloss` design size (72²) and the stud it was drawn for (64): scale = studD / 64 × c / 120.</summary>
        public const double GlossPx = 72;
        public const double GlossStud = 64;
        /// <summary>Defaults when the atlas has no anchor (ART_UNITY §3 example: anchor 0,5 / 0,5833, stud 54).</summary>
        public const double DefaultAnchorX = 0.5;
        public const double DefaultAnchorY = 0.5833;
        public const double DefaultStudD = 54;
        /// <summary>UX §5.4 shadow fill alpha (0,25–0,30).</summary>
        public const float ShadowFillAlpha = 0.28f;
        public const float NeutralLineAlpha = 0.6f;

        private static readonly (Corner K, int Dx, int Dy)[] Corners = { (Corner.Tl, -1, 1), (Corner.Tr, 1, 1), (Corner.Bl, -1, -1), (Corner.Br, 1, -1) };

        private static bool Has(ShapeDef s, int x, int y)
        {
            foreach (var c in s.Cells) if (c.X == x && c.Y == y) return true;
            return false;
        }

        /// <summary>The cell rectangle (design px, relative to the piece box centre).</summary>
        public static PxRect CellRect(ShapeDef shape, Cell c, double cellPx)
        {
            var o = BoardPose.CellOffset(shape, c, cellPx);
            return new PxRect(o.X - cellPx / 2, o.Y - cellPx / 2, cellPx, cellPx);
        }

        /// <summary>The corner square `k` of a cell rectangle (edge `size`).</summary>
        public static PxRect CornerRect(PxRect cell, Corner k, double size)
        {
            bool left = k == Corner.Tl || k == Corner.Bl;
            bool top = k == Corner.Tl || k == Corner.Tr;
            return new PxRect(left ? cell.X : cell.Right - size, top ? cell.Y : cell.Bottom - size, size, size);
        }

        /// <summary>Badge size / offset (design px at a 120 px cell): it sits on the top-left cell's corner, slightly outside it, so the stud symbol stays visible.</summary>
        public const double BadgePx = 70;
        public const double BadgeDx = -8;
        public const double BadgeDy = -12;
        /// <summary>Badge slots go right along the top edge, this far apart (design px at a 120 px cell).</summary>
        public const double BadgeStep = 54;
        /// <summary>Pallet planks rim (design px at a 120 px cell) and the frame size `pallet_cell` was drawn for (140 × 130).</summary>
        public const double PalletRim = 10;
        public const double PalletW = 140;
        public const double PalletH = 130;
        /// <summary>Sorting: planks under the body, overlays and badges above it (added to the visual's base order).</summary>
        public const int PlanksOrder = -3;
        public const int OverlayOrder = 3;
        public const int BadgeOrder = 4;

        /// <summary>The cell a badge sits on: the top row's left-most cell.</summary>
        public static int BadgeCell(ShapeDef shape)
        {
            int best = 0;
            for (int i = 1; i < shape.Cells.Length; i++)
            {
                var c = shape.Cells[i];
                var b = shape.Cells[best];
                if (c.Y > b.Y || (c.Y == b.Y && c.X < b.X)) best = i;
            }
            return best;
        }

        /// <summary>The badge frames of `traits`, left to right (knot, scissors, bonus star, snowflake, layer count).</summary>
        public static List<string> BadgeFrames(BlockTraits traits)
        {
            var o = new List<string>(3);
            if (traits.Wrapped) o.Add(Frames.WrapKnot);
            if (traits.Scissors) o.Add(Frames.IcoScissors);
            if (traits.Bonus > 0) o.Add(Frames.IcoBonus(traits.Bonus));
            if (traits.Slippery) o.Add(Frames.IcoSlip);
            if (traits.PalletLayers >= 2) o.Add(Frames.IcoLayers(traits.PalletLayers));
            return o;
        }

        /// <summary>The parts of a block (body tiles, corner fills, gloss or mortar, v3 trait overlays and badges), back to front.</summary>
        public static List<SpritePart> Block(ShapeDef shape, int colorIndex, double cellPx, BlockLook look, AtlasData? atlas = null, BlockTraits? traits = null)
        {
            var o = new List<SpritePart>(shape.Cells.Length * 3);
            var tint = look == BlockLook.Placed ? PlacedTint : look == BlockLook.Dim ? DimTint : Rgba.White;
            double k = cellPx / 120.0;
            double cs = CornerPx * k;
            for (int i = 0; i < shape.Cells.Length; i++)
            {
                var c = shape.Cells[i];
                int mask = BoardPose.NeighbourMask(shape, c);
                var r = CellRect(shape, c, cellPx);
                // v3 Y9: a two-tone block draws every cell in its own colour (the joined tiles still read as one block)
                int ci = traits?.CellColors != null && i < traits.CellColors.Length ? traits.CellColors[i] : colorIndex;
                string body = Frames.Block(ci, mask);
                o.Add(new SpritePart(body, PartRole.Body, i, r, tint, 0));
                foreach (var (kk, dx, dy) in Corners)
                    if (Has(shape, c.X + dx, c.Y) && Has(shape, c.X, c.Y + dy) && Has(shape, c.X + dx, c.Y + dy))
                        o.Add(new SpritePart(Frames.BlockCorner(ci, mask, kk), PartRole.Corner, i, CornerRect(r, kk, cs), tint, 1));
                if (look == BlockLook.Holdable)
                {
                    var f = atlas?.Find(body);
                    double ax = f != null && f.HasAnchor ? f.AnchorX : DefaultAnchorX;
                    double ay = f != null && f.HasAnchor ? f.AnchorY : DefaultAnchorY;
                    double stud = f != null && f.StudD > 0 ? f.StudD : DefaultStudD;
                    double g = GlossPx * stud / GlossStud * k;
                    double cx = r.X + ax * cellPx, cy = r.Y + (1 - ay) * cellPx;
                    o.Add(new SpritePart(Frames.Gloss, PartRole.Gloss, i, new PxRect(cx - g / 2, cy - g / 2, g, g), Rgba.White, 2));
                }
                else if (look == BlockLook.Placed)
                {
                    o.Add(new SpritePart(Frames.Locked, PartRole.Locked, i, r, Rgba.White, 2));
                }
                if (traits != null && !traits.IsNone) CellTraitParts(o, traits, i, r, cellPx);
            }
            if (traits != null && !traits.IsNone) BadgeParts(o, shape, traits, cellPx);
            return o;
        }

        private static void CellTraitParts(List<SpritePart> o, BlockTraits traits, int cell, PxRect r, double cellPx)
        {
            double k = cellPx / 120.0;
            if (traits.PalletBase)
                o.Add(new SpritePart(Frames.PalletCell, PartRole.Planks, cell, new PxRect(r.X - PalletRim * k, r.Y, PalletW * k, PalletH * k), Rgba.White, PlanksOrder));
            if (traits.Wrapped) o.Add(new SpritePart(Frames.OvlWrap, PartRole.Overlay, cell, r, Rgba.White, OverlayOrder));
            if (traits.Slippery) o.Add(new SpritePart(Frames.OvlIce, PartRole.Overlay, cell, r, Rgba.White, OverlayOrder));
        }

        private static void BadgeParts(List<SpritePart> o, ShapeDef shape, BlockTraits traits, double cellPx)
        {
            var frames = BadgeFrames(traits);
            if (frames.Count == 0) return;
            int bc = BadgeCell(shape);
            var r = CellRect(shape, shape.Cells[bc], cellPx);
            double k = cellPx / 120.0;
            for (int slot = 0; slot < frames.Count; slot++)
            {
                double s = BadgePx * k;
                o.Add(new SpritePart(frames[slot], PartRole.Badge, bc, new PxRect(r.X + (BadgeDx + slot * BadgeStep) * k, r.Y + BadgeDy * k, s, s), Rgba.White, BadgeOrder));
            }
        }

        /// <summary>The K-18 fall shadow parts for `look` (fill only when the shadow has a body; outline always).</summary>
        public static List<SpritePart> Shadow(ShapeDef shape, int colorIndex, double cellPx, ShadowLook look)
        {
            var o = new List<SpritePart>(shape.Cells.Length * 3);
            bool dashed = look.Outline != ShadowOutline.Valid;
            var line = look.Outline == ShadowOutline.Valid ? Rgba.Hex(Palette.GhostValid)
                : look.Outline == ShadowOutline.Invalid ? Rgba.Hex(Palette.GhostInvalid)
                : Rgba.White.WithAlpha(NeutralLineAlpha);
            var fill = Palette.Block(colorIndex).WithAlpha(ShadowFillAlpha);
            double cs = CornerPx * cellPx / 120.0;
            for (int i = 0; i < shape.Cells.Length; i++)
            {
                var c = shape.Cells[i];
                int mask = BoardPose.NeighbourMask(shape, c);
                var r = CellRect(shape, c, cellPx);
                if (look.Body) o.Add(new SpritePart(Frames.ShadowFill(mask), PartRole.ShadowFill, i, r, fill, 0));
                o.Add(new SpritePart(Frames.ShadowLine(mask, dashed), PartRole.ShadowLine, i, r, line, 1));
                foreach (var (kk, dx, dy) in Corners)
                    if (Has(shape, c.X + dx, c.Y) && Has(shape, c.X, c.Y + dy) && !Has(shape, c.X + dx, c.Y + dy))
                        o.Add(new SpritePart(Frames.ShadowCorner(mask, dashed, kk), PartRole.ShadowCorner, i, CornerRect(r, kk, cs), line, 2));
            }
            return o;
        }
    }
}
