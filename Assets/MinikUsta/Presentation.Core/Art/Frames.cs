#nullable enable
// Frame names of the v4 art atlas (paket V1; docs/ART_UNITY.md §4–§5, Ek A). One place builds every name the views ask
// for, so `FrameContractTests` can check that each of them exists in Resources/art/atlas.json.
using System.Globalization;
using MinikUsta.Core;

namespace MinikUsta.Presentation.Art
{
    /// <summary>A corner of a cell: `Tl` = left + up (y + 1), the ART_UNITY §4.1 naming.</summary>
    public enum Corner
    {
        Tl,
        Tr,
        Bl,
        Br,
    }

    public static class Frames
    {
        public const string BgLevel = "bg_level";
        public const string BgHome = "bg_home";
        public const string Tray = "board_tray";
        public const string CraneBand = "crane_band";
        public const string CraneHook = "crane_hook";
        public const string WallSeg = "wall_seg";
        public const string WallCap = "wall_cap";
        public const string GapSeg = "gap_seg";
        public const string GapEdge = "gap_edge";
        public const string RailSeg = "rail_seg";
        public const string ScaffoldPipe = "scaffold_pipe";
        public const string ScaffoldClamp = "scaffold_clamp";
        public const string Beam = "beam_seg";
        public const string FloorYardA = "floor_yard_a";
        public const string FloorYardB = "floor_yard_b";
        public const string FloorSite = "floor_site";
        public const string FloorSiteDeep = "floor_site_deep";
        public const string GhostFront = "ghost_front";
        public const string GhostHidden = "ghost_hidden";
        public const string Gloss = "blk_gloss";
        public const string Locked = "blk_locked";
        public const string Truck = "truck";
        public const string White = "px_white";
        public const string DropBlob = "fx_drop_blob";

        // v3 block traits (paket E3, docs/ART_UNITY.md section 9; the frames are in the extension pages of atlas.json, tools/art/extend.mjs)
        public const string OvlWrap = "ovl_wrap";
        public const string WrapKnot = "ovl_wrap_knot";
        public const string IcoScissors = "ico_scissors";
        public const string OvlIce = "ovl_ice";
        public const string IcoSlip = "ico_slip";
        public const string PalletCell = "pallet_cell";
        /// <summary>P4 Y10: one board of a covered pallet layer, seen from the front under the layer above (edge line of the stack depth).</summary>
        public const string PalletSlab = "pallet_slab";

        /// <summary>Y12 star badge "+n" (n 1–3).</summary>
        public static string IcoBonus(int n) => "ico_bonus_" + (n < 1 ? 1 : n > 3 ? 3 : n);

        /// <summary>Y10 layer badge (n 2–4 layers).</summary>
        public static string IcoLayers(int n) => "ico_layers_" + (n < 2 ? 2 : n > 4 ? 4 : n);

        // fx
        public const string FxSpark = "fx_spark";
        public const string FxDust = "fx_dust";
        public const string FxStar = "fx_star";
        public const string FxConfetti = "fx_confetti";
        public const string FxConfettiRound = "fx_confetti_round";
        public const string FxRing = "fx_ring";
        public const string FxRays = "fx_rays";
        public const string FxGlow = "fx_glow";

        /// <summary>
        /// B2: the v4 atlas stand-in of a 3D library frame when the library did not load (B1 re-rendered `chr_dede_*` as Usta Kral):
        /// a king bust → `chr_dede_bust`, any other king drawing → `chr_dede_idle`; null for the rest (fx frames have no stand-in).
        /// </summary>
        public static string? Fallback3d(string name)
        {
            if (name.StartsWith("chr_king_bust", System.StringComparison.Ordinal)) return "chr_dede_bust";
            if (name.StartsWith("chr_king_", System.StringComparison.Ordinal)) return "chr_dede_idle";
            return null;
        }

        /// <summary>B2 / JUICE #69: the pressed twin of a kit button frame (`btn_primary` → `btn_primary_pressed`), when the atlas has one.</summary>
        public static string? Pressed(string? frame, AtlasData atlas)
        {
            if (frame == null) return null;
            string p = frame + "_pressed";
            return atlas.Has(p) ? p : null;
        }

        /// <summary>Lower-case colour letter of a colour index (ColorCodes.All: w y g r o c b p).</summary>
        public static string Letter(int colorIndex)
        {
            int i = colorIndex >= 0 && colorIndex < ColorCodes.All.Length ? colorIndex : 0;
            return ColorCodes.All[i].ToLowerInvariant();
        }

        private static string Mask(int mask) => (mask & 15).ToString("00", CultureInfo.InvariantCulture);

        public static string CornerName(Corner k)
        {
            switch (k)
            {
                case Corner.Tl: return "tl";
                case Corner.Tr: return "tr";
                case Corner.Bl: return "bl";
                default: return "br";
            }
        }

        /// <summary>`blk_<c>_<mm>`: block cell tile (mask bits 1 left, 2 right, 4 down, 8 up).</summary>
        public static string Block(int colorIndex, int mask) => "blk_" + Letter(colorIndex) + "_" + Mask(mask);

        /// <summary>`blk_<c>_<mm>_cf_<k>`: the corner fill of a cell whose two neighbours and the diagonal are in the piece.</summary>
        public static string BlockCorner(int colorIndex, int mask, Corner k) => Block(colorIndex, mask) + "_cf_" + CornerName(k);

        /// <summary>`ghost_<c>` plan cell; `ghost_<c>_front` on the build front (K-34).</summary>
        public static string Ghost(int colorIndex, bool front) => "ghost_" + Letter(colorIndex) + (front ? "_front" : "");

        public static string Stud(int colorIndex) => "stud_" + Letter(colorIndex);

        public static string Sym(int colorIndex) => "sym_" + Letter(colorIndex);

        /// <summary>K-18 shadow body: `shadow_fill_<mm>` (white, tinted with the block colour).</summary>
        public static string ShadowFill(int mask) => "shadow_fill_" + Mask(mask);

        /// <summary>K-18 shadow outline: `shadow_line_<mm>` (correct) or `shadow_dash_<mm>` (wrong / neutral).</summary>
        public static string ShadowLine(int mask, bool dashed) => (dashed ? "shadow_dash_" : "shadow_line_") + Mask(mask);

        /// <summary>The concave inner corner of a shadow outline: `shadow_<line|dash>_<mm>_cc_<k>`.</summary>
        public static string ShadowCorner(int mask, bool dashed, Corner k) => ShadowLine(mask, dashed) + "_cc_" + CornerName(k);
    }
}
