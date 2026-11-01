#nullable enable
// The structure of a level as a small picture (paket V1): the HUD panorama well (ART_UNITY §4.4), the level start goal
// (UX §4.1), the lose preview (UX §7) and the win card (Q1 open question b: the finished structure is shown INSIDE the win
// window, so the window never hides it). Per segment a Ws × Hs grid of plan colours (−1 none, −2 `.`), with the cells already
// filled and the unrevealed `?` cells. Pure; read from the state.
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Ui;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Game
{
    public sealed class PlanThumb
    {
        public const int None = -1;
        public const int Dot = -2;

        public readonly int Ws;
        public readonly int Hs;
        /// <summary>First segment of the picture (the visible one for the HUD, 0 for the whole structure).</summary>
        public readonly int FirstSegment;
        /// <summary>[segment][sx, sy] colour index (sy 0 = bottom).</summary>
        public readonly List<int[,]> Colors = new List<int[,]>();
        public readonly List<bool[,]> Filled = new List<bool[,]>();
        public readonly List<bool[,]> Hidden = new List<bool[,]>();

        private PlanThumb(int ws, int hs, int first)
        {
            Ws = ws;
            Hs = hs;
            FirstSegment = first;
        }

        public int Segments => Colors.Count;

        /// <summary>
        /// The plan of segments [`from`, `from + count`) of the state (count &lt; 0 = to the end); `allFilled` draws every plan cell
        /// as built (the win card) and shows the `?` cells with their colour.
        /// </summary>
        public static PlanThumb Of(GameState s, int from, int count, bool allFilled)
        {
            var lvl = s.Lvl;
            var geo = lvl.Geo;
            int total = lvl.Segments.Length;
            if (from < 0) from = 0;
            if (from > total) from = total;
            int to = count < 0 ? total : System.Math.Min(total, from + count);
            var o = new PlanThumb(geo.Ws, geo.Hs, from);
            for (int seg = from; seg < to; seg++)
            {
                var sg = lvl.Segments[seg];
                int revealed = RevealedMask(s, seg);
                var colors = new int[geo.Ws, geo.Hs];
                var filled = new bool[geo.Ws, geo.Hs];
                var hidden = new bool[geo.Ws, geo.Hs];
                for (int sx = 0; sx < geo.Ws; sx++)
                {
                    int fm = FilledMask(s, seg, sx);
                    for (int sy = 0; sy < geo.Hs; sy++)
                    {
                        int pc = Grid.PlanColorAt(s, seg, sx, sy);
                        colors[sx, sy] = pc >= 0 ? pc : pc == Compiler.PlanDot ? Dot : None;
                        filled[sx, sy] = pc >= 0 && (allFilled || ((fm >> sy) & 1) != 0);
                        int local = sy * geo.Ws + sx;
                        hidden[sx, sy] = !allFilled && pc >= 0 && ((sg.HiddenMask >> local) & 1) != 0 && ((revealed >> local) & 1) == 0;
                    }
                }
                o.Colors.Add(colors);
                o.Filled.Add(filled);
                o.Hidden.Add(hidden);
            }
            return o;
        }

        /// <summary>The visible segment only (HUD well).</summary>
        public static PlanThumb Visible(GameState s) => Of(s, Grid.VisibleSegment(s), 1, false);

        /// <summary>The whole structure as built (win card).</summary>
        public static PlanThumb Whole(GameState s) => Of(s, 0, -1, true);

        /// <summary>The frame of a cell: a block tile when filled, the plan ghost when not, `ghost_hidden` for `?`; null = no cell.</summary>
        public string? CellFrame(int seg, int sx, int sy)
        {
            int c = Colors[seg][sx, sy];
            if (c < 0) return null;
            if (Hidden[seg][sx, sy]) return Frames.GhostHidden;
            return Filled[seg][sx, sy] ? Frames.Block(c, 0) : Frames.Ghost(c, false);
        }

        /// <summary>
        /// UI nodes of the picture fitted into `area` (segments side by side, 8 px apart, cells ≤ `maxCell`), centred;
        /// ids `prefix.<seg>.<sx>.<sy>`. Cells without a plan get no node.
        /// </summary>
        public List<UiNode> Nodes(string prefix, PxRect area, double maxCell)
        {
            var o = new List<UiNode>();
            int n = Segments;
            if (n == 0 || Ws == 0 || Hs == 0) return o;
            const double gap = 8;
            double cell = System.Math.Min(maxCell, System.Math.Min((area.W - gap * (n - 1)) / (n * Ws), area.H / Hs));
            cell = System.Math.Floor(cell);
            double w = n * Ws * cell + (n - 1) * gap, h = Hs * cell;
            double x0 = area.X + (area.W - w) / 2, y0 = area.Y + (area.H - h) / 2;
            for (int seg = 0; seg < n; seg++)
            {
                double sx0 = x0 + seg * (Ws * cell + gap);
                for (int sx = 0; sx < Ws; sx++)
                    for (int sy = 0; sy < Hs; sy++)
                    {
                        string? f = CellFrame(seg, sx, sy);
                        if (f == null) continue;
                        var r = new PxRect(sx0 + sx * cell, y0 + (Hs - 1 - sy) * cell, cell, cell);
                        o.Add(UiKit.Image(prefix + "." + seg + "." + sx + "." + sy, f, r));
                    }
            }
            return o;
        }
    }
}
