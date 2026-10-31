#nullable enable
// Which block a touch picks (port of web src/scenes/level/hitTest.ts; TECH §10.3; UX_FLOWS §0.1, §0.3). Pure: reads the
// state, changes nothing. One invisible board zone takes every touch; the block under the finger comes from the
// occupancy grids (yard, and the visible segment of the site), never from per-sprite colliders. Every block cell is grown
// by the hit slop (`ScreenLayout.HitSlopPx`); when several blocks cover the point, the one with the nearest cell centre
// wins. Whether that block may be picked is the core's decision (`Movement.TryBeginDrag`, K-09, K-14).
using MinikUsta.Core;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Layout
{
    public static class HitTest
    {
        /// <summary>The piece whose (slop-grown) cell holds (px, py) with the nearest cell centre; −1 when none.</summary>
        public static int PieceAtPoint(GameState s, BoardGeometry g, double px, double py, double slopPx)
        {
            int best = -1;
            double bestD = double.PositiveInfinity;
            var geo = s.Lvl.Geo;
            for (int y = 0; y < geo.Rows; y++)
            {
                for (int x = 0; x < geo.Wy; x++) Consider(YardOcc(s, x, y), x, y, g, px, py, slopPx, ref best, ref bestD);
            }
            int seg = Grid.VisibleSegment(s);
            if (seg < s.Lvl.Segments.Length)
            {
                int elev = Hdr(s, H.Elev);
                for (int sy = 0; sy < geo.Hs; sy++)
                {
                    int y = sy + elev;
                    if (y >= geo.H) break;
                    for (int sx = 0; sx < geo.Ws; sx++) Consider(SiteOcc(s, seg, sx, sy), geo.SiteX + sx, y, g, px, py, slopPx, ref best, ref bestD);
                }
            }
            return best;
        }

        private static void Consider(int occ, int x, int y, BoardGeometry g, double px, double py, double slop, ref int best, ref double bestD)
        {
            if (occ <= 0) return; // empty, obstacle (< 0) or Golden Trowel cell
            var r = g.CellRect(x, y);
            if (px < r.X - slop || px >= r.X + r.W + slop || py < r.Y - slop || py >= r.Y + r.H + slop) return;
            double dx = px - (r.X + r.W / 2);
            double dy = py - (r.Y + r.H / 2);
            double d = dx * dx + dy * dy;
            if (d < bestD)
            {
                bestD = d;
                best = occ - 1;
            }
        }
    }
}
