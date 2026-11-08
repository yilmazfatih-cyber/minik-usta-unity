#nullable enable
// Tutorial highlight ids → screen rectangles and the bubble dock (paket C1; web tutorial/highlights.ts; UX_FLOWS §13.1 "Vurgu kimlikleri" and
// "Balon", TECH §8.2). Pure: layout geometry + state reads, no engine.
//
// Light tutorial (K-53): a highlight is only a soft glow around its target — nothing dims, nothing is blocked. A highlight whose target is
// not on screen (the truck chip while the queue is empty, a block that left the board) gives no rectangle; the step runs.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Tutorial
{
    /// <summary>What the rectangle lookup needs from the level screen.</summary>
    public sealed class HighlightContext
    {
        public readonly ScreenLayout Layout;
        public readonly GameState State;
        public readonly CompiledLevel Level;
        /// <summary>The truck chip is on screen (queued + pending blocks &gt; 0, UX §5.1).</summary>
        public readonly bool TruckVisible;
        /// <summary>Rectangle of a block as the screen shows it now (the lifted block follows the finger); null = not on the board.</summary>
        public readonly Func<int, PxRect?> PieceRect;

        public HighlightContext(ScreenLayout layout, GameState state, CompiledLevel level, bool truckVisible, Func<int, PxRect?> pieceRect)
        {
            Layout = layout;
            State = state;
            Level = level;
            TruckVisible = truckVisible;
            PieceRect = pieceRect;
        }
    }

    public enum BubbleDock
    {
        Top,
        Bottom,
        /// <summary>U2 (UX_UNITY K2-B, P0-6): right above the board — its bottom edge 16 px over the crane area, the tail down to the glove.</summary>
        Board,
    }

    public readonly struct DockPlace
    {
        public readonly BubbleDock Dock;
        /// <summary>Portrait + bubble at its place.</summary>
        public readonly PxRect Rect;
        /// <summary>The tail points up to the HUD (a HUD highlight).</summary>
        public readonly bool TailUp;
        /// <summary>U2: board dock — the x the tail points down at (the glove / the highlighted block); NaN = no down tail.</summary>
        public readonly double TailX;

        public DockPlace(BubbleDock dock, PxRect rect, bool tailUp, double tailX = double.NaN)
        {
            Dock = dock;
            Rect = rect;
            TailUp = tailUp;
            TailX = tailX;
        }

        public bool TailDown => Dock == BubbleDock.Board && !double.IsNaN(TailX);
    }

    public static class TutorialLayout
    {
        /// <summary>HUD highlight ids: the bubble takes the top dock and its tail points up to the HUD.</summary>
        public static bool IsHudHighlight(string id) => id == "panorama" || id == "goals" || id == "blocks" || id == "moves";

        /// <summary>Rectangles of one highlight id (empty when the thing is not on screen).</summary>
        public static List<PxRect> Rects(string id, HighlightContext c)
        {
            var o = new List<PxRect>();
            var L = c.Layout;
            var g = L.Grid;
            if (id.StartsWith("piece:", StringComparison.Ordinal) || id.StartsWith("debris:", StringComparison.Ordinal))
            {
                if (c.Level.TutorialPieceIds.TryGetValue(id, out int pid))
                {
                    var r = c.PieceRect(pid);
                    if (r.HasValue) o.Add(r.Value);
                }
                return o;
            }
            if (id.StartsWith("cell:", StringComparison.Ordinal))
            {
                var parts = id.Substring(5).Split(',');
                if (parts.Length == 2
                    && int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int x)
                    && int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int y))
                    o.Add(g.CellRect(x, y));
                return o;
            }
            if (id.StartsWith("gap:", StringComparison.Ordinal))
            {
                if (int.TryParse(id.Substring(4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int i) && i < c.Level.Gaps.Length)
                    o.Add(g.GapRect(State.GapField(c.State, i, GF.Y), c.Level.Gaps[i].Size));
                return o;
            }
            if (id.StartsWith("booster:", StringComparison.Ordinal))
            {
                int slot = Array.IndexOf(HudState.SlotOrder, id.Substring("booster:".Length));
                if (slot >= 0 && slot < L.Boosters.Length) o.Add(L.Boosters[slot]);
                return o;
            }
            switch (id)
            {
                case "crane": o.Add(L.Board.Crane); break;
                case "build": o.Add(L.Board.Site); break;
                case "wall": o.Add(g.WallRect(c.Level.WallHeight)); break;
                case "front":
                    foreach (var a in Placement.BuildFront(c.State)) o.Add(g.CellRect(a.X, a.Y));
                    break;
                case "panorama": o.Add(L.Panorama); break;
                case "goals": o.Add(L.Goals); break;
                case "blocks": o.Add(HudLayout.BlocksChip(L)); break;
                case "moves": o.Add(L.Moves); break;
                case "truck":
                    if (c.TruckVisible) o.Add(HudLayout.TruckChip(L));
                    break;
                case "streak": o.Add(HudLayout.Streak(L)); break;
            }
            return o; // pre:*, obstacle:*, fan: are not on this screen
        }

        public static List<PxRect> All(string[] ids, HighlightContext c)
        {
            var o = new List<PxRect>();
            foreach (var id in ids) o.AddRange(Rects(id, c));
            return o;
        }

        public static PxRect Pad(PxRect r, double pad) => new PxRect(r.X - pad, r.Y - pad, r.W + 2 * pad, r.H + 2 * pad);

        /// <summary>Bounding box of `rects` (null when empty).</summary>
        public static PxRect? BoundsOf(IReadOnlyList<PxRect> rects)
        {
            if (rects.Count == 0) return null;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var r in rects)
            {
                x0 = Math.Min(x0, r.X);
                y0 = Math.Min(y0, r.Y);
                x1 = Math.Max(x1, r.Right);
                y1 = Math.Max(y1, r.Bottom);
            }
            return new PxRect(x0, y0, x1 - x0, y1 - y0);
        }

        // --- bubble dock (UX §13.1 "Balon") ----------------------------------------------------------------------------------------

        /// <summary>Top-left of the two docks: top = under the HUD group + gap, bottom = under the status strip + gap.</summary>
        public static PxRect DockRect(ScreenLayout L, BubbleDock dock, double w, double h, double margin, double gap)
        {
            double y = dock == BubbleDock.Top ? L.TopGroupBottomY + gap : dock == BubbleDock.Board ? L.Board.Crane.Y - gap - h : L.Board.Status.Bottom + gap;
            return new PxRect(margin, y, w, h);
        }

        /// <summary>
        /// U2 (UX_UNITY K2-B): the board dock — bottom edge `gap` over the crane area, centred on `anchorX` (the glove) within the side
        /// margins. Null when it would reach into the HUD group (+ gap).
        /// </summary>
        public static PxRect? BoardDock(ScreenLayout L, double w, double h, double margin, double gap, double anchorX)
        {
            double y = L.Board.Crane.Y - gap - h;
            if (y < L.TopGroupBottomY + gap) return null;
            double x = double.IsNaN(anchorX) ? margin : anchorX - w / 2;
            x = Math.Max(margin, Math.Min(L.W - margin - w, x));
            return new PxRect(x, y, w, h);
        }

        /// <summary>The bottom dock exists only when the bubble ends `gap` px over the bottom group (none at FIT 1080 × 1920).</summary>
        public static bool BottomDockValid(ScreenLayout L, double h, double gap) => L.Board.Status.Bottom + gap + h <= L.BottomGroupTopY - gap;

        /// <summary>Hard areas the bubble (padded by `gap`) never touches: the yard cells, the site cells, the status strip and the HUD.</summary>
        public static List<PxRect> HardAreas(ScreenLayout L) => new List<PxRect>
        {
            L.Board.Yard,
            L.Board.Site,
            L.Board.Status,
            new PxRect(0, 0, L.W, L.TopGroupBottomY),
        };

        private static bool Touches(PxRect r, List<PxRect> areas)
        {
            foreach (var a in areas) if (r.Overlaps(a)) return true;
            return false;
        }

        /// <summary>
        /// A HUD highlight takes the top dock (tail up). Else the docks in order top, bottom: the first meeting the hard condition (yard / site
        /// cells, status strip, HUD) and the soft one (the step's highlights, the glove path box); else the first meeting the hard one; else top.
        /// </summary>
        public static DockPlace PlaceBubble(ScreenLayout L, double w, double h, string[] ids, IReadOnlyList<PxRect> lit, PxRect? handBox, double margin, double gap,
            double anchorX = double.NaN)
        {
            var top = DockRect(L, BubbleDock.Top, w, h, margin, gap);
            foreach (var id in ids) if (IsHudHighlight(id)) return new DockPlace(BubbleDock.Top, top, true);
            // U2 (UX_UNITY K2-B, P0-6): the board dock first — the bubble right above the crane area, its tail down to the glove (or the
            // highlighted block), so hand and sentence are read in one look; the old top / bottom docks only when it does not fit
            var board = BoardDock(L, w, h, margin, gap, anchorX);
            if (board.HasValue && !Touches(Pad(board.Value, gap), HardAreas(L))) return new DockPlace(BubbleDock.Board, board.Value, false, anchorX);
            var docks = new List<DockPlace> { new DockPlace(BubbleDock.Top, top, false) };
            if (BottomDockValid(L, h, gap)) docks.Add(new DockPlace(BubbleDock.Bottom, DockRect(L, BubbleDock.Bottom, w, h, margin, gap), false));
            var hard = HardAreas(L);
            var soft = new List<PxRect>(lit);
            if (handBox.HasValue) soft.Add(handBox.Value);
            foreach (var d in docks)
                if (!Touches(Pad(d.Rect, gap), hard) && !Touches(Pad(d.Rect, gap), soft)) return d;
            foreach (var d in docks)
                if (!Touches(Pad(d.Rect, gap), hard)) return d;
            return docks[0];
        }
    }
}
