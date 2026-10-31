#nullable enable
// Port of src/core/ascii.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>Board size of a text (`size` line; default 6×8 | 2×8, H 8 when absent).</summary>
    public sealed class AsciiSize
    {
        public readonly int Wy;
        public readonly int Hy;
        public readonly int Ws;
        public readonly int Hs;
        public readonly int H;

        public AsciiSize(int wy, int hy, int ws, int hs, int h)
        {
            Wy = wy;
            Hy = hy;
            Ws = ws;
            Hs = hs;
            H = h;
        }

        public bool SameAs(AsciiSize o) => Wy == o.Wy && Hy == o.Hy && Ws == o.Ws && Hs == o.Hs && H == o.H;
    }

    /// <summary>Structural parse of either mode.</summary>
    public sealed class AsciiBoard
    {
        public readonly int LevelId;
        public readonly int Turn;
        public readonly int Moves;
        /// <summary>0-based visible segment.</summary>
        public readonly int Seg;
        public readonly int SegCount;
        public readonly int Elev;
        public readonly bool Ids;
        /// <summary>Board size (K-49).</summary>
        public readonly AsciiSize Size;
        /// <summary>`Cells[y][x]` for x 0 … wy+ws−1 (yard, then site), y 0 … h+1.</summary>
        public readonly string[][] Cells;
        /// <summary>`Wall[y]`.</summary>
        public readonly string[] Wall;
        public readonly string[] Plan;
        /// <summary>`hidden:` entries (null when the line is absent).</summary>
        public readonly string[]? Hidden;
        public readonly int[] Queue;
        public readonly string[] Obstacles;
        public readonly int[] Locked;
        public readonly int[] Stuck;

        public AsciiBoard(int levelId, int turn, int moves, int seg, int segCount, int elev, bool ids, AsciiSize size, string[][] cells,
            string[] wall, string[] plan, string[]? hidden, int[] queue, string[] obstacles, int[] locked, int[] stuck)
        {
            LevelId = levelId;
            Turn = turn;
            Moves = moves;
            Seg = seg;
            SegCount = segCount;
            Elev = elev;
            Ids = ids;
            Size = size;
            Cells = cells;
            Wall = wall;
            Plan = plan;
            Hidden = hidden;
            Queue = queue;
            Obstacles = obstacles;
            Locked = locked;
            Stuck = stuck;
        }
    }

    /// <summary>
    /// ASCII board (TECH_DESIGN Appendix A): debug "copy board", test snapshots, level preview.
    ///
    /// Yard: `.` empty, lower-case colour = block, `1`–`3` crate (hp), `%` cement bag. Site: upper case = locked correct
    /// block, lower case = unlocked (debris, stuck mortar), `_` platform (below the elevator offset), `*` trowel cell.
    /// Wall column (the boundary has no cells, R-03; printed for readability): `#` closed, `=` open gap, `x` closed gap,
    /// `:` air above the wall. Optional lines: `queue:` (FIFO ids). With `ids: true` the header ends in `ids`, cells show
    /// piece ids (0–9, a–z, A–Z = 0…61), crates/bags move to an `obstacles:` line and `locked:` / `stuck:` list flags, so
    /// `FromAscii` can rebuild the state.
    ///
    /// Geometry (K-49, TECH §2R.1): a board that differs from the default 6×8 | 2×8 (yard, site or H) gets a second header
    /// line `size 4x4|2x5 H5`; without it the default board is assumed, so pre-2R text reads unchanged. Rows run
    /// `rows − 1 … 0` (H + 1 … 0); yard air (x ≤ wy−1, hy ≤ y ≤ H−1) and site air (site columns, hs + e ≤ y ≤ H−1) print
    /// `·` (LEVELS §0); the crane area prints `.`.
    /// </summary>
    public static class Ascii
    {
        private const string IdChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        /// <summary>Yard air and site air (LEVELS §0 notation).</summary>
        public const string AirChar = "·";

        private static string IdChar(int id)
        {
            if (id < 0 || id >= IdChars.Length) throw new ArgumentOutOfRangeException(nameof(id), "ascii: piece id " + id + " has no single-character form (max 61)");
            return IdChars[id].ToString();
        }

        private static string ColorChar(GameState s, int id, bool upper)
        {
            int ci = PieceColor(s, id);
            string c = (ci >= 0 && ci < ColorCodes.All.Length ? ColorCodes.All[ci] : "W").ToLowerInvariant();
            return upper ? c.ToUpperInvariant() : c;
        }

        /// <summary>Wall boundary character of board row y.</summary>
        public static string WallChar(GameState s, int y)
        {
            var lvl = s.Lvl;
            if (y >= lvl.WallHeight) return ":";
            bool shutterOpen = Hdr(s, H.Turn) < Hdr(s, H.OpenShutterUntil);
            foreach (var g in lvl.Gaps)
            {
                int gy = GapField(s, g.Index, GF.Y);
                if (y < gy || y >= gy + g.Size) continue;
                bool open = GapField(s, g.Index, GF.Open) == 1 || (shutterOpen && (g.Type == "shutter" || g.Type == "locked"));
                return open ? "=" : "x";
            }
            return "#";
        }

        private static string YardChar(GameState s, int x, int y, bool ids)
        {
            var geo = s.Lvl.Geo;
            if (y >= geo.Hy && y < geo.H) return AirChar;
            int v = YardOcc(s, x, y);
            if (v > 0) return ids ? IdChar(v - 1) : ColorChar(s, v - 1, false);
            if (v < 0 && !ids)
            {
                int oi = -v - 1;
                var o = oi >= 0 && oi < s.Lvl.Obstacles.Length ? s.Lvl.Obstacles[oi] : null;
                if (o != null && o.IsCrate) return ObstacleField(s, o.Index, OF.Hp).ToString(CultureInfo.InvariantCulture);
                if (o != null && o.IsCementBag) return "%";
            }
            return ".";
        }

        private static string SiteChar(GameState s, int sx, int y, bool ids)
        {
            var geo = s.Lvl.Geo;
            int elev = Hdr(s, H.Elev);
            if (y < elev) return "_";
            if (y >= geo.H) return "."; // crane area
            int sy = y - elev;
            if (sy >= geo.Hs) return AirChar;
            int v = SiteOcc(s, Grid.VisibleSegment(s), sx, sy);
            if (v == SiteTrowel) return "*";
            if (v <= 0) return ".";
            if (ids) return IdChar(v - 1);
            return ColorChar(s, v - 1, (PieceFlagBits(s, v - 1) & FlagBit.Locked) != 0);
        }

        /// <summary>The board as Appendix A text (lines joined with `\n`, no trailing newline).</summary>
        public static string ToAscii(GameState s, bool ids = false)
        {
            var lvl = s.Lvl;
            var geo = lvl.Geo;
            int seg = Grid.VisibleSegment(s);
            var lines = new List<string>();
            lines.Add("L" + lvl.Id + " turn " + Hdr(s, H.Turn) + " moves " + Hdr(s, H.MovesLeft) + " seg " + (seg + 1) + "/" + lvl.Segments.Length
                + " elev " + Hdr(s, H.Elev) + (ids ? " ids" : ""));
            if (!Geometry.HasDefaultBoard(geo)) lines.Add("size " + Geometry.GeoLabel(geo));
            var yardCols = new string[geo.Wy];
            for (int x = 0; x < geo.Wy; x++) yardCols[x] = x.ToString(CultureInfo.InvariantCulture);
            var siteCols = new string[geo.Ws];
            for (int sx = 0; sx < geo.Ws; sx++) siteCols[sx] = (geo.SiteX + sx).ToString(CultureInfo.InvariantCulture);
            lines.Add(" y  " + string.Join(" ", yardCols) + " | W | " + string.Join(" ", siteCols));
            for (int y = geo.Rows - 1; y >= 0; y--)
            {
                var yard = new string[geo.Wy];
                for (int x = 0; x < geo.Wy; x++) yard[x] = YardChar(s, x, y, ids);
                var site = new string[geo.Ws];
                for (int sx = 0; sx < geo.Ws; sx++) site[sx] = SiteChar(s, sx, y, ids);
                lines.Add(" " + y + "  " + string.Join(" ", yard) + " | " + WallChar(s, y) + " | " + string.Join(" ", site));
            }
            var rows = seg < lvl.Data.Build.Segments.Length ? lvl.Data.Build.Segments[seg].Rows : new string[0];
            int revealed = RevealedMask(s, seg);
            int h = rows.Length;
            var shownRows = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var sb = new StringBuilder();
                string row = rows[i];
                for (int sx = 0; sx < row.Length; sx++)
                {
                    char ch = row[sx];
                    if (ch != '?')
                    {
                        sb.Append(ch);
                        continue;
                    }
                    int local = (h - 1 - i) * geo.Ws + sx;
                    int color = seg < lvl.Segments.Length && local >= 0 && local < lvl.Segments[seg].PlanColors.Length ? lvl.Segments[seg].PlanColors[local] : -2;
                    sb.Append(((revealed >> local) & 1) != 0 && color >= 0 ? ColorCodes.All[color] : "?");
                }
                shownRows[i] = sb.ToString();
            }
            lines.Add("plan seg" + seg + " (top→bottom): " + string.Join(" ", shownRows));
            if (lvl.HiddenItems.Length > 0)
            {
                var items = new List<string>();
                foreach (int index in lvl.HiddenItems)
                {
                    var o = index >= 0 && index < lvl.Obstacles.Length ? lvl.Obstacles[index] : null;
                    if (o == null || !o.IsHiddenItem) continue;
                    if (HiddenCollected(s, o.HiddenIndex)) continue;
                    items.Add(o.Type == "key" ? "key:" + (o.Id ?? "") + "@(" + o.X + "," + o.Y + ")" : "screw@(" + o.X + "," + o.Y + ")");
                }
                lines.Add("hidden: " + (items.Count > 0 ? string.Join(" ", items) : "-"));
            }
            var queue = QueueIds(s);
            if (queue.Length > 0) lines.Add("queue: " + string.Join(" ", Strings(queue)));
            if (ids)
            {
                var obs = new List<string>();
                foreach (var o in lvl.Obstacles)
                {
                    if (!o.IsCrate && !o.IsCementBag) continue;
                    int hp = ObstacleField(s, o.Index, OF.Hp);
                    if (hp <= 0) continue;
                    obs.Add(o.IsCrate ? "crate:" + hp + "@(" + o.X + "," + o.Y + ")" : "bag@(" + o.X + "," + o.Y + ")");
                }
                if (obs.Count > 0) lines.Add("obstacles: " + string.Join(" ", obs));
                var locked = new List<int>();
                var stuck = new List<int>();
                for (int id = 0; id < lvl.Layout.Counts.Pieces; id++)
                {
                    if (PieceZone(s, id) != Zone.Site) continue;
                    int f = PieceFlagBits(s, id);
                    if ((f & FlagBit.Locked) != 0) locked.Add(id);
                    if ((f & FlagBit.Stuck) != 0) stuck.Add(id);
                }
                if (locked.Count > 0) lines.Add("locked: " + string.Join(" ", Strings(locked.ToArray())));
                if (stuck.Count > 0) lines.Add("stuck: " + string.Join(" ", Strings(stuck.ToArray())));
            }
            return string.Join("\n", lines);
        }

        private static string[] Strings(int[] values)
        {
            var o = new string[values.Length];
            for (int i = 0; i < values.Length; i++) o[i] = values[i].ToString(CultureInfo.InvariantCulture);
            return o;
        }

        private static readonly AsciiSize DefaultAsciiSize = new AsciiSize(
            Geometry.DefaultGeo.Wy, Geometry.DefaultGeo.Hy, Geometry.DefaultGeo.Ws, Geometry.DefaultGeo.Hs, Geometry.DefaultGeo.H);

        // The TS literals of ascii.ts, translated with JS semantics (JsRegex: `$` = end of input only, `\d` = [0-9], `\s` = JS space set).
        private static readonly JsRegex HeaderRe = new JsRegex(@"^L(\d+) turn (\d+) moves (\d+) seg (\d+)/(\d+) elev (\d+)( ids)?$");
        private static readonly JsRegex SizeRe = new JsRegex(@"^size (\d)x(\d)\|(\d)x(\d) H(\d)$");
        private static readonly JsRegex ColsRe = new JsRegex(@"^ y {2}\S(?: \S)* \| W \| \S(?: \S)*$");
        private static readonly JsRegex RowRe = new JsRegex(@"^ (\d) {2}(\S(?: \S)*) \| (\S) \| (\S(?: \S)*)$");
        private static readonly JsRegex TrailingWs = new JsRegex(@"\s+$");
        private static readonly JsRegex Ws = new JsRegex(@"\s+");
        private static readonly JsRegex CrateRe = new JsRegex(@"^crate:(\d)");

        private static int Num(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        private static string[] Words(string rest)
        {
            var o = new List<string>();
            foreach (var w in Ws.Split(JsRegex.Trim(rest))) if (w.Length > 0) o.Add(w);
            return o.ToArray();
        }

        private static int[] Nums(string rest)
        {
            var w = Words(rest);
            var o = new int[w.Length];
            for (int i = 0; i < w.Length; i++) o[i] = Num(w[i]);
            return o;
        }

        public static AsciiBoard ParseAscii(string text)
        {
            var rawLines = text.Split('\n');
            var lines = new string[rawLines.Length];
            for (int i = 0; i < rawLines.Length; i++) lines[i] = TrailingWs.Replace(rawLines[i], "");
            var head = HeaderRe.Match(lines.Length > 0 ? lines[0] : "");
            if (!head.Success) throw new FormatException("ascii: bad header \"" + (lines.Length > 0 ? lines[0] : "") + "\"");
            var sized = SizeRe.Match(lines.Length > 1 ? lines[1] : "");
            var size = sized.Success
                ? new AsciiSize(Num(sized.Groups[1].Value), Num(sized.Groups[2].Value), Num(sized.Groups[3].Value), Num(sized.Groups[4].Value), Num(sized.Groups[5].Value))
                : DefaultAsciiSize;
            int rows = size.H + 2;
            int cols = size.Wy + size.Ws;
            var cells = new string[rows][];
            for (int y = 0; y < rows; y++)
            {
                cells[y] = new string[cols];
                for (int x = 0; x < cols; x++) cells[y][x] = ".";
            }
            var wall = new string[rows];
            for (int y = 0; y < rows; y++) wall[y] = "#";
            var seen = new HashSet<int>();
            var plan = new string[0];
            string[]? hidden = null;
            var queue = new int[0];
            var obstacles = new string[0];
            var locked = new int[0];
            var stuck = new int[0];
            for (int li = sized.Success ? 2 : 1; li < lines.Length; li++)
            {
                string line = lines[li];
                if (ColsRe.IsMatch(line)) continue;
                var row = RowRe.Match(line);
                if (row.Success)
                {
                    int y = Num(row.Groups[1].Value);
                    var yard = row.Groups[2].Value.Split(' ');
                    var site = row.Groups[4].Value.Split(' ');
                    if (y >= rows || yard.Length != size.Wy || site.Length != size.Ws)
                        throw new FormatException("ascii: row \"" + line + "\" does not fit the " + size.Wy + "|" + size.Ws + " × " + rows + " board");
                    var target = cells[y];
                    for (int x = 0; x < yard.Length; x++) target[x] = yard[x];
                    for (int sx = 0; sx < site.Length; sx++) target[size.Wy + sx] = site[sx];
                    wall[y] = row.Groups[3].Value;
                    seen.Add(y);
                    continue;
                }
                if (line.StartsWith("plan ", StringComparison.Ordinal)) plan = Words(line.Substring(line.IndexOf(':') + 1));
                else if (line.StartsWith("hidden:", StringComparison.Ordinal))
                {
                    string rest = JsRegex.Trim(line.Substring(7));
                    hidden = rest == "-" ? new string[0] : Words(rest);
                }
                else if (line.StartsWith("queue:", StringComparison.Ordinal)) queue = Nums(line.Substring(6));
                else if (line.StartsWith("obstacles:", StringComparison.Ordinal)) obstacles = Words(line.Substring(10));
                else if (line.StartsWith("locked:", StringComparison.Ordinal)) locked = Nums(line.Substring(7));
                else if (line.StartsWith("stuck:", StringComparison.Ordinal)) stuck = Nums(line.Substring(6));
                else if (JsRegex.Trim(line).Length != 0) throw new FormatException("ascii: unexpected line \"" + line + "\"");
            }
            if (seen.Count != rows) throw new FormatException("ascii: the board needs rows " + (rows - 1) + " … 0");
            return new AsciiBoard(
                Num(head.Groups[1].Value),
                Num(head.Groups[2].Value),
                Num(head.Groups[3].Value),
                Num(head.Groups[4].Value) - 1,
                Num(head.Groups[5].Value),
                Num(head.Groups[6].Value),
                head.Groups[7].Success,
                size,
                cells,
                wall,
                plan,
                hidden,
                queue,
                obstacles,
                locked,
                stuck);
        }

        /// <summary>
        /// Rebuilds a state from `ids` text (fixtures, debug "load from ASCII"). Starts from the level's initial state, then
        /// places every drawn piece (anchor = bottom-left of its cells), queue, locked/stuck flags, crates/bags and hidden
        /// items. Pieces that are neither drawn nor queued stay `pending` when their batch is not delivered yet, else `gone`.
        /// Colour-mode text cannot identify pieces: use `ParseAscii` for it.
        /// </summary>
        public static GameState FromAscii(CompiledLevel lvl, string text)
        {
            var board = ParseAscii(text);
            if (!board.Ids) throw new InvalidOperationException("fromAscii needs ids mode text (toAscii(state, { ids: true }))");
            if (board.LevelId != lvl.Id) throw new InvalidOperationException("fromAscii: text is level " + board.LevelId + ", compiled level is " + lvl.Id);
            var geo = lvl.Geo;
            var z = board.Size;
            if (z.Wy != geo.Wy || z.Hy != geo.Hy || z.Ws != geo.Ws || z.Hs != geo.Hs || z.H != geo.H)
                throw new InvalidOperationException("fromAscii: text board " + z.Wy + "x" + z.Hy + "|" + z.Ws + "x" + z.Hs + " H" + z.H + ", level " + Geometry.GeoLabel(geo));
            var s = CreateInitialState(lvl);
            int P = lvl.Layout.Counts.Pieces;
            // fresh occupancy grids (yardOcc, siteOcc, filled, wrongOcc precede the piece table in the layout)
            Array.Clear(s.Buf, lvl.Layout.YardOcc, lvl.Layout.Pieces - lvl.Layout.YardOcc);
            SetHdr(s, H.Turn, board.Turn);
            SetHdr(s, H.MovesLeft, board.Moves);
            SetHdr(s, H.Elev, board.Elev);
            if (lvl.Mode == "carousel") SetHdr(s, H.FrontSeg, board.Seg);
            else
            {
                SetHdr(s, H.ActiveSeg, board.Seg);
                SetHdr(s, H.DeliveryCursor, board.Seg);
            }
            var cellsById = new Dictionary<int, List<BoardCell>>();
            for (int y = 0; y < geo.Rows; y++)
            {
                for (int x = 0; x < geo.Cols; x++)
                {
                    string ch = y < board.Cells.Length && x < board.Cells[y].Length ? board.Cells[y][x] : ".";
                    if (x >= geo.SiteX && ch == "*" && y >= board.Elev) SetSiteOcc(s, board.Seg, x - geo.SiteX, y - board.Elev, SiteTrowel);
                    int id = ch.Length == 1 ? IdChars.IndexOf(ch[0]) : -1;
                    if (ch == "." || ch == "_" || ch == "*" || ch == AirChar || id < 0) continue;
                    if (!cellsById.TryGetValue(id, out var list))
                    {
                        list = new List<BoardCell>();
                        cellsById[id] = list;
                    }
                    list.Add(new BoardCell(x, y));
                }
            }
            int cursor = Hdr(s, H.DeliveryCursor);
            for (int id = 0; id < P; id++)
            {
                var compiled = id < lvl.Pieces.Length ? lvl.Pieces[id] : null;
                if (compiled == null) continue;
                if (!cellsById.TryGetValue(id, out var cells))
                {
                    var batch = compiled.Batch >= 0 && compiled.Batch < lvl.Batches.Length ? lvl.Batches[compiled.Batch] : null;
                    bool undelivered = compiled.Origin == PieceOrigins.Truck && batch != null && batch.ForSegment > cursor;
                    if (compiled.Origin == PieceOrigins.Help || undelivered) continue; // keep gone / pending
                    SetPieceField(s, id, PF.Zone, Zone.Gone);
                    continue;
                }
                int ax = int.MaxValue;
                int ay = int.MaxValue;
                foreach (var c in cells)
                {
                    if (c.X < ax) ax = c.X;
                    if (c.Y < ay) ay = c.Y;
                }
                var shape = Shapes.ShapeByIndex(compiled.ShapeIndex);
                var want = new List<string>();
                foreach (var c in shape.Cells) want.Add((ax + c.X) + "," + (ay + c.Y));
                var got = new List<string>();
                foreach (var c in cells) got.Add(c.X + "," + c.Y);
                want.Sort(string.CompareOrdinal);
                got.Sort(string.CompareOrdinal);
                if (string.Join(";", want) != string.Join(";", got))
                    throw new InvalidOperationException("fromAscii: piece " + id + " cells " + string.Join(" ", got) + " do not match " + shape.Id + " at (" + ax + "," + ay + ")");
                bool onSite = ax >= geo.SiteX;
                SetPieceField(s, id, PF.Zone, onSite ? Zone.Site : Zone.Yard);
                SetPieceField(s, id, PF.X, ax);
                SetPieceField(s, id, PF.Y, onSite ? ay - board.Elev : ay);
                SetPieceField(s, id, PF.Seg, onSite ? board.Seg : compiled.Segment);
                Grid.OccupyPiece(s, id);
            }
            foreach (int id in board.Queue) EnqueuePiece(s, id);
            foreach (int id in board.Locked) SetFlag(s, id, FlagBit.Locked, true);
            foreach (int id in board.Stuck) SetFlag(s, id, FlagBit.Stuck, true);
            // crates / bags
            foreach (var o in lvl.Obstacles)
            {
                if (!o.IsCrate && !o.IsCementBag) continue;
                string suffix = "@(" + o.X + "," + o.Y + ")";
                string? entry = null;
                foreach (var e in board.Obstacles)
                    if (e.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        entry = e;
                        break;
                    }
                int hp = 0;
                if (entry != null)
                {
                    if (o.IsCrate)
                    {
                        var m = CrateRe.Match(entry);
                        hp = m.Success ? Num(m.Groups[1].Value) : 0;
                    }
                    else hp = 1;
                }
                SetObstacleField(s, o.Index, OF.Hp, hp);
                if (hp > 0) SetYardOcc(s, o.X, o.Y, -(o.Index + 1));
            }
            // hidden items: listed = still hidden
            if (board.Hidden != null)
            {
                foreach (int index in lvl.HiddenItems)
                {
                    var o = index >= 0 && index < lvl.Obstacles.Length ? lvl.Obstacles[index] : null;
                    if (o == null || !o.IsHiddenItem) continue;
                    string suffix = "@(" + o.X + "," + o.Y + ")";
                    bool listed = false;
                    foreach (var e in board.Hidden) if (e.EndsWith(suffix, StringComparison.Ordinal)) listed = true;
                    SetHiddenCollected(s, o.HiddenIndex, !listed);
                    SetObstacleField(s, o.Index, OF.Hp, listed ? 1 : 0);
                }
            }
            for (int seg = 0; seg < lvl.Segments.Length; seg++) Grid.RefreshSiteMasks(s, seg);
            return s;
        }
    }
}
