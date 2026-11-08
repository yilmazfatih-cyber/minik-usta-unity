#nullable enable
// UX_UNITY R-01 (paket U4): the scene band of an EXPAND screen. The free band between the HUD and the crane area shows this level's
// structure piece as a site silhouette: every plan cell is a dark placeholder block and turns into its coloured block when a correct
// placement fills it, so the building rises one storey per placement (the glow behind it is a radial `fx_glow`). Pure: the cells come
// from the `PlanThumb` the HUD already holds; the rectangle is `ScreenLayout.SceneBand` (zero height at H ≤ 1920).
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Ui
{
    public static class SceneBand
    {
        public const string GroupId = "hud.scene";
        public const string PlanId = "hud.scene.plan";
        public const string CellPrefix = "hud.scene.cell";
        /// <summary>Upper bound of a cell (design px); a tall plan fits smaller cells.</summary>
        public const double MaxCellPx = 64;
        public static readonly Rgba SilhouetteTint = Rgba.Hex("#0B0918");
        public const float SilhouetteAlpha = 0.45f;

        /// <summary>The band group: a warm radial glow and the (initially empty) plan group. Hidden when the layout has no band.</summary>
        public static UiNode Build(ScreenLayout L)
        {
            var r = L.SceneBand;
            var g = UiKit.Group(GroupId, r);
            g.Visible = r.H > 0;
            var glow = new PxRect(r.CenterX - r.H * 1.1, r.CenterY - r.H * 0.8, r.H * 2.2, r.H * 1.6);
            g.Add(UiKit.Image("hud.scene.glow", Frames.FxGlow, glow, false, Rgba.Hex("#FFE7A3"), 0.28f));
            g.Add(UiKit.Group(PlanId, r));
            return g;
        }

        /// <summary>The cells of `thumb` fitted into `area`: a coloured block when built, a dark silhouette when not.</summary>
        public static List<UiNode> Cells(PlanThumb thumb, PxRect area)
        {
            var o = new List<UiNode>();
            foreach (var n in thumb.Nodes(CellPrefix, area, MaxCellPx))
            {
                // Nodes() gives ghost / hidden / block frames; the band draws only built blocks and silhouettes
                bool built = n.Frame != null && n.Frame.StartsWith("blk_", StringComparison.Ordinal);
                if (!built)
                {
                    n.Frame = Frames.White;
                    n.Tint = SilhouetteTint;
                    n.Alpha = SilhouetteAlpha;
                }
                o.Add(n);
            }
            return o;
        }
    }
}
