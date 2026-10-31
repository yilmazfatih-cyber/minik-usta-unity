#nullable enable
// Port of src/core/panorama.ts (web repo, source of truth).
using System.Collections.Generic;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>`done` (K-15, full colour) · `active` (the segment on the site, framed) · `future` (faded plan). TS `PanoramaStatus`.</summary>
    public static class PanoramaStatus
    {
        public const string Done = "done";
        public const string Active = "active";
        public const string Future = "future";
    }

    public sealed class PanoramaSegment
    {
        public readonly int Index;
        public readonly string Status;
        /// <summary>
        /// Plan rows top → bottom (`height` rows), `ws` cells each (default: site columns 6, 7). A cell is its colour code,
        /// "?" (hidden, not revealed yet), "." (stays empty, S2) or null (outside the plan). TS `PanoramaCell`.
        /// </summary>
        public readonly string?[][] Rows;

        public PanoramaSegment(int index, string status, string?[][] rows)
        {
            Index = index;
            Status = status;
            Rows = rows;
        }
    }

    /// <summary>
    /// Build panorama view (docs/GDD.md K-06; UX_FLOWS §5.1; TECH_DESIGN §10.3): the small preview of every segment above
    /// the board. Completed segments are drawn in full colour, the active segment framed, future segments with plan colours
    /// at `alpha.panoramaFuture`. Hidden `?` cells stay `?` until they are revealed (K-32).
    ///
    /// A pure read of the state: building the view (and opening / closing the large preview that shows it) never changes
    /// the game state (K-06). The UI (`Panorama`, TECH §1.2) draws exactly this data.
    /// </summary>
    public static class Panorama
    {
        /// <summary>K-06: one entry per segment, in level order. Reads the state only.</summary>
        public static PanoramaSegment[] PanoramaView(GameState s)
        {
            int shown = Grid.VisibleSegment(s);
            int ws = s.Lvl.Geo.Ws;
            var o = new PanoramaSegment[s.Lvl.Segments.Length];
            for (int i = 0; i < o.Length; i++)
            {
                var seg = s.Lvl.Segments[i];
                string status = Placement.IsSegmentComplete(s, seg.Index)
                    ? PanoramaStatus.Done
                    : seg.Index == shown
                        ? PanoramaStatus.Active
                        : PanoramaStatus.Future;
                int revealed = RevealedMask(s, seg.Index);
                var rows = new List<string?[]>();
                for (int sy = seg.Height - 1; sy >= 0; sy--)
                {
                    var row = new string?[ws];
                    for (int sx = 0; sx < ws; sx++)
                    {
                        int local = sy * ws + sx;
                        int color = local < seg.PlanColors.Length ? seg.PlanColors[local] : Compiler.PlanOutside;
                        if (color == Compiler.PlanOutside) row[sx] = null;
                        else if (color == Compiler.PlanDot) row[sx] = Plan.Dot.ToString();
                        else if (((seg.HiddenMask >> local) & 1) != 0 && ((revealed >> local) & 1) == 0) row[sx] = Plan.Hidden.ToString();
                        else row[sx] = color >= 0 && color < ColorCodes.All.Length ? ColorCodes.All[color] : null;
                    }
                    rows.Add(row);
                }
                o[i] = new PanoramaSegment(seg.Index, status, rows.ToArray());
            }
            return o;
        }
    }
}
