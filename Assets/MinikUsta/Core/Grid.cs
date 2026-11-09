#nullable enable
// Port of src/core/grid.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    public readonly struct BoardCell : IEquatable<BoardCell>
    {
        public readonly int X;
        public readonly int Y;

        public BoardCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(BoardCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is BoardCell c && Equals(c);
        public override int GetHashCode() => X * 31 + Y;
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    /// <summary>
    /// Grid operations on a GameState (TECH_DESIGN §1.2, §2.4, §4.1): occupancy writes, collision masks for the drag BFS,
    /// site column tops, `filled` / `wrongOcc` maintenance and the state invariants used by tests (§12.4).
    /// </summary>
    public static class Grid
    {
        /// <summary>The segment shown on the site: front segment in carousel mode, active segment otherwise (K-22, K-23).</summary>
        public static int VisibleSegment(GameState s) => s.Lvl.Mode == "carousel" ? Hdr(s, H.FrontSeg) : Hdr(s, H.ActiveSeg);

        /// <summary>
        /// Cells of a piece. Yard: global board cells. Site: global column and BOARD row (`sy + elev`).
        /// Queue / pending / gone pieces have no cells.
        /// </summary>
        public static BoardCell[] PieceBoardCells(GameState s, int id)
        {
            int zone = PieceZone(s, id);
            if (zone != Zone.Yard && zone != Zone.Site) return new BoardCell[0];
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            int x = PieceX(s, id);
            int y = PieceY(s, id) + (zone == Zone.Site ? Hdr(s, H.Elev) : 0);
            var o = new BoardCell[shape.Cells.Length];
            for (int i = 0; i < o.Length; i++) o[i] = new BoardCell(x + shape.Cells[i].X, y + shape.Cells[i].Y);
            return o;
        }

        /// <summary>Writes the piece into the occupancy grids (yardOcc or its segment's siteOcc). Site masks: `RefreshSiteMasks`.</summary>
        public static void OccupyPiece(GameState s, int id) => WritePiece(s, id, id + 1);

        /// <summary>Clears the piece from the occupancy grids.</summary>
        public static void VacatePiece(GameState s, int id) => WritePiece(s, id, 0);

        // --- v3 Y10 pallets (GDD §16 K-58) ---------------------------------------------------------------------------------

        /// <summary>
        /// The pallet layer directly below `id` that still lies under it (same anchor, in the yard), or −1. Only the top layer of a
        /// pallet is in `yardOcc`; the layers below keep their record (zone yard, same anchor) and fill the same cells.
        /// </summary>
        public static int LayerBelow(GameState s, int id)
        {
            int b = id >= 0 && id < s.Lvl.Pieces.Length ? s.Lvl.Pieces[id].StackBelow : -1;
            if (b < 0 || PieceZone(s, id) != Zone.Yard || PieceZone(s, b) != Zone.Yard) return -1;
            return PieceX(s, b) == PieceX(s, id) && PieceY(s, b) == PieceY(s, id) ? b : -1;
        }

        /// <summary>K-58 item 3: a pallet layer under another layer (not the top). Its cells hold the top layer in `yardOcc`.</summary>
        public static bool IsCovered(GameState s, int id)
        {
            if (id < 0 || id >= s.Lvl.Pieces.Length) return false;
            var p = s.Lvl.Pieces[id];
            if (p.StackId < 0 || PieceZone(s, id) != Zone.Yard) return false;
            var cells = Shapes.ShapeByIndex(PieceShape(s, id)).Cells;
            if (cells.Length == 0) return false;
            var first = cells[0];
            return YardOcc(s, PieceX(s, id) + first.X, PieceY(s, id) + first.Y) != id + 1;
        }

        /// <summary>
        /// K-58 item 4: the cells of the held block count as empty for its drag (its own start), except a pallet layer with a layer
        /// below it: its cells stay full (the layer below). The occupancy code to skip, or 0 for none.
        /// </summary>
        public static int OwnCellCode(GameState s, int id) => LayerBelow(s, id) >= 0 ? 0 : id + 1;

        private static void WritePiece(GameState s, int id, int value)
        {
            int zone = PieceZone(s, id);
            if (zone != Zone.Yard && zone != Zone.Site) return;
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            int x = PieceX(s, id);
            int y = PieceY(s, id);
            if (zone == Zone.Yard)
            {
                // K-58 item 2: a pallet's cells stay full while a layer is left — vacating the top writes the layer below
                if (value == 0)
                {
                    int below = LayerBelow(s, id);
                    if (below >= 0) value = below + 1;
                }
                foreach (var c in shape.Cells) SetYardOcc(s, x + c.X, y + c.Y, value);
            }
            else
            {
                int seg = PieceSeg(s, id);
                int siteX = s.Lvl.Geo.SiteX;
                foreach (var c in shape.Cells) SetSiteOcc(s, seg, x - siteX + c.X, y + c.Y, value);
            }
        }

        /// <summary>
        /// Recomputes `filled` (locked blocks + Golden Trowel cells on plan cells) and `wrongOcc` (plan-area rows holding
        /// debris or a stuck mortar block, K-34 / E-43) of one segment from its siteOcc. O(ws · hs).
        /// </summary>
        public static void RefreshSiteMasks(GameState s, int seg)
        {
            if (seg < 0 || seg >= s.Lvl.Segments.Length) return;
            var plan = s.Lvl.Segments[seg];
            int ws = s.Lvl.Geo.Ws;
            int hs = s.Lvl.Geo.Hs;
            for (int sx = 0; sx < ws; sx++)
            {
                int filled = 0;
                int wrong = 0;
                int area = PlanAreaMask(s.Lvl, seg, sx);
                for (int sy = 0; sy < hs; sy++)
                {
                    if (((area >> sy) & 1) == 0) continue;
                    int v = SiteOcc(s, seg, sx, sy);
                    if (v == SiteTrowel) filled |= 1 << sy;
                    else if (v > 0)
                    {
                        int f = PieceFlagBits(s, v - 1);
                        if ((f & FlagBit.Locked) != 0) filled |= 1 << sy;
                        else if ((f & (FlagBit.Debris | FlagBit.Stuck)) != 0) wrong |= 1 << sy;
                    }
                }
                SetFilledMask(s, seg, sx, filled & (sx < plan.PlanMask.Length ? plan.PlanMask[sx] : 0));
                SetWrongOccMask(s, seg, sx, wrong);
            }
        }

        /// <summary>
        /// Collision rows for the drag BFS (TECH §4.1 step 2, §2R.1): `out[y]` bit x is set when board/crane cell (x, y) is
        /// blocked, rows `0 … geo.Rows − 1` of the frame. Yard occupancy (minus `exclude`; yard air and the crane area are
        /// empty), the visible segment shifted by the elevator offset (plan rows `sy &lt; hs` on the board), and the platform rows
        /// `y &lt; elev` of the site columns. Site air and the crane area are free. The wall boundary has no cells (R-03).
        /// </summary>
        public static byte[] CollisionMasks(GameState s, int exclude, byte[]? @out = null)
        {
            @out ??= new byte[Geometry.MaxRows];
            Array.Clear(@out, 0, @out.Length);
            var geo = s.Lvl.Geo;
            int wy = geo.Wy;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int h = geo.H;
            int rows = geo.Rows;
            int siteX = geo.SiteX;
            // K-58 item 4: a pallet layer's own cells stay blocked (the layer below fills them)
            int skip = exclude >= 0 && s.Lvl.HasStacks ? OwnCellCode(s, exclude) : exclude + 1;
            for (int y = 0; y < rows; y++)
            {
                int row = 0;
                for (int x = 0; x < wy; x++)
                {
                    int v = YardOcc(s, x, y);
                    if (v != 0 && v != skip) row |= 1 << x;
                }
                @out[y] = (byte)row;
            }
            int seg = VisibleSegment(s);
            int elev = Hdr(s, H.Elev);
            for (int y = 0; y < rows; y++)
            {
                for (int sx = 0; sx < ws; sx++)
                {
                    int sy = y - elev;
                    bool blocked = y < elev;
                    if (!blocked && sy < hs && y < h)
                    {
                        int v = SiteOcc(s, seg, sx, sy);
                        blocked = v != 0 && v != skip;
                    }
                    if (blocked) @out[y] = (byte)(@out[y] | (1 << (siteX + sx)));
                }
            }
            return @out;
        }

        /// <summary>
        /// `colTop` of the site columns (TECH §4.1): highest blocked board row of each site column (platform included), −1
        /// when the column is empty. `out[sx]` = column `geo.SiteX + sx` (default board: `out[0]` = x 6, `out[1]` = x 7).
        /// </summary>
        public static sbyte[] SiteColumnTops(GameState s, int exclude, sbyte[]? @out = null)
        {
            var geo = s.Lvl.Geo;
            @out ??= new sbyte[geo.Ws];
            int ws = geo.Ws;
            int hs = geo.Hs;
            int h = geo.H;
            int seg = VisibleSegment(s);
            int elev = Hdr(s, H.Elev);
            int skip = exclude + 1;
            for (int sx = 0; sx < ws; sx++)
            {
                int top = elev - 1;
                for (int sy = 0; sy < hs && sy + elev < h; sy++)
                {
                    int v = SiteOcc(s, seg, sx, sy);
                    if (v != 0 && v != skip) top = sy + elev;
                }
                @out[sx] = (sbyte)top;
            }
            return @out;
        }

        /// <summary>
        /// Structural invariants (TECH §12.4). Returns human-readable violations; empty = consistent.
        /// Occupancy ↔ piece table, obstacle cells, queue, `filled` / `wrongOcc` recomputation, and K-34 (no locked block
        /// above an empty non-`.` plan cell of its columns).
        /// </summary>
        public static List<string> StateInvariantErrors(GameState s)
        {
            var errors = new List<string>();
            var lvl = s.Lvl;
            var geo = lvl.Geo;
            int wy = geo.Wy;
            int hy = geo.Hy;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int rows = geo.Rows;
            int siteX = geo.SiteX;
            int P = lvl.Layout.Counts.Pieces;
            var expectYard = new Dictionary<int, int>();
            var expectSite = new Dictionary<string, int>();
            for (int id = 0; id < P; id++)
            {
                int zone = PieceZone(s, id);
                if (zone < Zone.Yard || zone > Zone.Pending) errors.Add("piece " + id + ": bad zone " + zone);
                if (zone != Zone.Yard && zone != Zone.Site) continue;
                var shape = Shapes.ShapeByIndex(PieceShape(s, id));
                int x = PieceX(s, id);
                int y = PieceY(s, id);
                // K-58: a covered pallet layer shares the cells of the layer above it (same pallet, same anchor, higher z)
                if (zone == Zone.Yard && IsCovered(s, id))
                {
                    var firstCell = shape.Cells.Length > 0 ? shape.Cells[0] : (Cell?)null;
                    int top = firstCell.HasValue ? YardOcc(s, x + firstCell.Value.X, y + firstCell.Value.Y) - 1 : -1;
                    bool ok = false;
                    if (top >= 0 && top < lvl.Pieces.Length)
                    {
                        var tp = lvl.Pieces[top];
                        var me = lvl.Pieces[id];
                        ok = tp.StackId == me.StackId && tp.StackZ > me.StackZ && PieceZone(s, top) == Zone.Yard && PieceX(s, top) == x && PieceY(s, top) == y;
                    }
                    if (!ok) errors.Add("piece " + id + ": covered pallet layer without its pallet's upper layer on it");
                    continue;
                }
                foreach (var c in shape.Cells)
                {
                    int cx = x + c.X;
                    int cy = y + c.Y;
                    if (zone == Zone.Yard)
                    {
                        if (cx < 0 || cx >= wy || cy < 0 || cy >= hy)
                        {
                            errors.Add("piece " + id + ": yard cell (" + cx + "," + cy + ") outside the yard");
                            continue;
                        }
                        int key = cy * wy + cx;
                        if (expectYard.TryGetValue(key, out int other))
                            errors.Add("piece " + id + ": overlaps piece " + other + " at (" + cx + "," + cy + ")");
                        expectYard[key] = id + 1;
                    }
                    else
                    {
                        int seg = PieceSeg(s, id);
                        int sx = cx - siteX;
                        if (seg < 0 || seg >= lvl.Segments.Length || sx < 0 || sx >= ws || cy < 0 || cy >= hs)
                        {
                            errors.Add("piece " + id + ": site cell (" + cx + "," + cy + ") seg " + seg + " outside the site");
                            continue;
                        }
                        string key = seg + ":" + sx + "," + cy;
                        if (expectSite.TryGetValue(key, out int other))
                            errors.Add("piece " + id + ": overlaps piece " + other + " at seg " + key);
                        expectSite[key] = id + 1;
                    }
                }
            }
            foreach (var o in lvl.Obstacles)
            {
                if (!o.IsCrate && !o.IsCementBag) continue;
                if (ObstacleField(s, o.Index, OF.Hp) <= 0) continue;
                int key = o.Y * wy + o.X;
                if (expectYard.ContainsKey(key)) errors.Add("obstacle " + o.Index + ": shares (" + o.X + "," + o.Y + ") with a piece");
                expectYard[key] = -(o.Index + 1);
            }
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < wy; x++)
                {
                    int got = YardOcc(s, x, y);
                    int want = expectYard.TryGetValue(y * wy + x, out int w) ? w : 0;
                    if (got != want) errors.Add("yardOcc (" + x + "," + y + ") = " + got + ", expected " + want);
                }
            }
            for (int seg = 0; seg < lvl.Segments.Length; seg++)
            {
                for (int sy = 0; sy < hs; sy++)
                {
                    for (int sx = 0; sx < ws; sx++)
                    {
                        int got = SiteOcc(s, seg, sx, sy);
                        string key = seg + ":" + sx + "," + sy;
                        if (got == SiteTrowel)
                        {
                            if (expectSite.ContainsKey(key)) errors.Add("trowel cell seg " + seg + " (" + sx + "," + sy + ") holds a piece");
                            continue;
                        }
                        int want = expectSite.TryGetValue(key, out int w) ? w : 0;
                        if (got != want) errors.Add("siteOcc seg " + seg + " (" + sx + "," + sy + ") = " + got + ", expected " + want);
                    }
                }
            }
            // filled / wrongOcc against a recomputation on a scratch copy
            var scratch = new GameState(lvl, (int[])s.Buf.Clone());
            for (int seg = 0; seg < lvl.Segments.Length; seg++)
            {
                RefreshSiteMasks(scratch, seg);
                for (int sx = 0; sx < ws; sx++)
                {
                    if (FilledMask(scratch, seg, sx) != FilledMask(s, seg, sx))
                        errors.Add("filled seg " + seg + " col " + sx + " = " + FilledMask(s, seg, sx) + ", expected " + FilledMask(scratch, seg, sx));
                    if (WrongOccMask(scratch, seg, sx) != WrongOccMask(s, seg, sx))
                        errors.Add("wrongOcc seg " + seg + " col " + sx + " = " + WrongOccMask(s, seg, sx) + ", expected " + WrongOccMask(scratch, seg, sx));
                }
            }
            // queue
            var queued = QueueIds(s);
            if (new HashSet<int>(queued).Count != queued.Length) errors.Add("queue holds a piece twice");
            foreach (int id in queued)
                if (PieceZone(s, id) != Zone.Queue) errors.Add("queued piece " + id + " is not in zone queue");
            for (int id = 0; id < P; id++)
                if (PieceZone(s, id) == Zone.Queue && Array.IndexOf(queued, id) < 0) errors.Add("piece " + id + " in zone queue but not queued");
            // K-34: under every locked block, each non-dot plan row of its columns is filled
            for (int id = 0; id < P; id++)
            {
                if (PieceZone(s, id) != Zone.Site || (PieceFlagBits(s, id) & FlagBit.Locked) == 0) continue;
                int seg = PieceSeg(s, id);
                if (seg < 0 || seg >= lvl.Segments.Length) continue;
                var plan = lvl.Segments[seg];
                var shape = Shapes.ShapeByIndex(PieceShape(s, id));
                for (int c = 0; c < shape.W; c++)
                {
                    int sx = PieceX(s, id) - siteX + c;
                    int r = PieceY(s, id) + shape.ColBottom[c];
                    int need = ((1 << r) - 1) & (sx >= 0 && sx < plan.PlanMask.Length ? plan.PlanMask[sx] : 0);
                    if ((need & ~FilledMask(s, seg, sx)) != 0) errors.Add("K-34: locked piece " + id + " has an empty plan cell below it");
                }
            }
            return errors;
        }

        /// <summary>Plan colour index at a local cell: 0–7, PlanDot (−1) or PlanOutside (−2).</summary>
        public static int PlanColorAt(GameState s, int seg, int sx, int sy)
        {
            var geo = s.Lvl.Geo;
            if (sx < 0 || sx >= geo.Ws || sy < 0 || sy >= geo.Hs) return Compiler.PlanOutside;
            if (seg < 0 || seg >= s.Lvl.Segments.Length) return Compiler.PlanOutside;
            return s.Lvl.Segments[seg].PlanColors[sy * geo.Ws + sx];
        }
    }
}
