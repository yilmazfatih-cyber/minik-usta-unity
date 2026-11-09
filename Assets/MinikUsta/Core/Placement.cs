#nullable enable
// Port of src/core/placement.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>Where a piece is, as stored in its record (TECH §2.4). `Zone` is a zone name ("yard", "site", "queue", "gone", "pending").</summary>
    public readonly struct PiecePlace : IEquatable<PiecePlace>
    {
        public readonly string Zone;
        /// <summary>Yard: anchor x 0 … wy−1. Site: global column (wy …). Queue: the yard column the truck tries first (K-25 stage 1).</summary>
        public readonly int X;
        /// <summary>Yard: board row. Site: plan row in the segment frame (board row = y + elevator offset, K-24). Queue: hy.</summary>
        public readonly int Y;
        /// <summary>Site segment; −1 elsewhere.</summary>
        public readonly int Seg;

        public PiecePlace(string zone, int x, int y, int seg)
        {
            Zone = zone;
            X = x;
            Y = y;
            Seg = seg;
        }

        public static PiecePlace Yard(int x, int y) => new PiecePlace(Core.Zone.Names[Core.Zone.Yard], x, y, -1);
        public static PiecePlace Site(int x, int y, int seg) => new PiecePlace(Core.Zone.Names[Core.Zone.Site], x, y, seg);
        public static PiecePlace Queue(int x, int y) => new PiecePlace(Core.Zone.Names[Core.Zone.Queue], x, y, -1);
        public static PiecePlace Gone(int x, int y) => new PiecePlace(Core.Zone.Names[Core.Zone.Gone], x, y, -1);

        public bool Equals(PiecePlace o) => Zone == o.Zone && X == o.X && Y == o.Y && Seg == o.Seg;
        public override bool Equals(object? obj) => obj is PiecePlace p && Equals(p);
        public override int GetHashCode() => (Zone.GetHashCode() * 31 + X) * 31 + Y * 31 + Seg;
        public override string ToString() => Zone + "(" + X + "," + Y + (Seg >= 0 ? ",seg" + Seg : "") + ")";
    }

    /// <summary>Result of the K-17 search.</summary>
    public sealed class ReturnTarget : IEquatable<ReturnTarget>
    {
        /// <summary>GDD K-17 step that found the target: 1 start cells, 2 drop over the yard, 3 end of the truck queue.</summary>
        public readonly int Step;
        public readonly PiecePlace To;
        /// <summary>Step 2: the anchor the block falls from (`x`, (H + 2) − h); the bounce animation falls from here to `To`.</summary>
        public readonly Anchor? DropFrom;

        public ReturnTarget(int step, PiecePlace to, Anchor? dropFrom)
        {
            Step = step;
            To = to;
            DropFrom = dropFrom;
        }

        public bool Equals(ReturnTarget? o) => o != null && Step == o.Step && To.Equals(o.To) && Nullable.Equals(DropFrom, o.DropFrom);
        public override bool Equals(object? obj) => obj is ReturnTarget r && Equals(r);
        public override int GetHashCode() => Step * 31 + To.GetHashCode();
        public override string ToString() => "step " + Step + " to " + To + (DropFrom.HasValue ? " from " + DropFrom.Value : "");
    }

    public sealed class ReturnOptions
    {
        /// <summary>S3 broken glass whose start is on the site (only stuck glass mortar): skip step 1 (GDD K-17 exceptions).</summary>
        public readonly bool SkipStart;
        /// <summary>Start place; default = the piece record (call before the piece is moved).</summary>
        public readonly PiecePlace? Start;

        public ReturnOptions(bool skipStart = false, PiecePlace? start = null)
        {
            SkipStart = skipStart;
            Start = start;
        }
    }

    /// <summary>Rule hook result on a wrong site placement (TECH §7.1 `onPlacement`; Y8 mortar returns `stick`). TS `PlacementOverride`.</summary>
    public static class PlacementOverrides
    {
        public const string Default = "default";
        public const string Stick = "stick";
    }

    public sealed class PlacementRules
    {
        public static readonly PlacementRules Default = new PlacementRules();

        /// <summary>
        /// Called for WRONG placements only, before anything moves; `cells` = the landed board cells. `stick` is honoured only
        /// when every cell is inside the plan area (Y8 / E-08 guard, `AllCellsInPlanArea`). Default: always `default`.
        /// Arguments: (state, pieceId, cells, verdict) → `PlacementOverrides` value.
        /// </summary>
        public readonly Func<GameState, int, BoardCell[], Verdict, string>? OnPlacement;

        public PlacementRules(Func<GameState, int, BoardCell[], Verdict, string>? onPlacement = null)
        {
            OnPlacement = onPlacement;
        }
    }

    /// <summary>What K-35 step 3 did with a site placement. `Kind`: "correct" (At, Cells, Revealed), "stuck" (At, Reason, MissingSupport), "bounced" (Landing, Target, Reason, MissingSupport).</summary>
    public sealed class PlacementOutcome
    {
        public const string KindCorrect = "correct";
        public const string KindStuck = "stuck";
        public const string KindBounced = "bounced";

        public readonly string Kind;
        public readonly PiecePlace? At;
        public readonly At[]? Cells;
        public readonly RevealedCell[]? Revealed;
        public readonly string? Reason;
        public readonly At[]? MissingSupport;
        /// <summary>Where the block landed before bouncing (board anchor; may be above the board, e.g. on a full column).</summary>
        public readonly Anchor? Landing;
        public readonly ReturnTarget? Target;

        private PlacementOutcome(string kind, PiecePlace? at, At[]? cells, RevealedCell[]? revealed, string? reason, At[]? missingSupport, Anchor? landing, ReturnTarget? target)
        {
            Kind = kind;
            At = at;
            Cells = cells;
            Revealed = revealed;
            Reason = reason;
            MissingSupport = missingSupport;
            Landing = landing;
            Target = target;
        }

        public static PlacementOutcome Correct(PiecePlace at, At[] cells, RevealedCell[] revealed) => new PlacementOutcome(KindCorrect, at, cells, revealed, null, null, null, null);
        public static PlacementOutcome Stuck(PiecePlace at, string reason, At[] missingSupport) => new PlacementOutcome(KindStuck, at, null, null, reason, missingSupport, null, null);
        public static PlacementOutcome Bounced(Anchor landing, ReturnTarget target, string reason, At[] missingSupport) => new PlacementOutcome(KindBounced, null, null, null, reason, missingSupport, landing, target);

        public override string ToString() => Kind + (At.HasValue ? " at " + At.Value : "") + (Reason != null ? " " + Reason : "") + (Target != null ? " " + Target : "");
    }

    public sealed class SettleOptions
    {
        /// <summary>The piece's place at move start; default = its record (the piece has not moved yet).</summary>
        public readonly PiecePlace? Start;
        public readonly PlacementRules? Rules;

        public SettleOptions(PiecePlace? start = null, PlacementRules? rules = null)
        {
            Start = start;
            Rules = rules;
        }
    }

    /// <summary>
    /// Placement on the site (docs/GDD.md K-14…K-17, K-34; OBSTACLES S2, S3, S4, Y8; docs/TECH_DESIGN.md §5.2, R-01).
    ///
    /// - `IsCorrectPlacement` is the ONE correctness test (R-01): drop and rail placements (K-35 step 3), the fall shadow
    ///   (K-18, via `ComputeFall`), the crane target (K-37), the trowel cells (K-33, via `BuildFront`), the paint lock of a
    ///   stuck mortar block (K-38) and the solver's correct-move generator all call it. Every condition is evaluated; the
    ///   broken ones go to `Reasons` in the fixed GDD K-34 hook 2 order (`debris`, `outside`, `window`, `color`,
    ///   `support`), so `Reasons[0]` is the primary reason.
    /// - `BuildFront` (= `EligibleTrowelCells`) is GDD K-34 visibility hook 1.
    /// - `ReturnTarget` is the K-17 bounce-back search: (1) the start cells when they are empty, (2) a drop over the yard from
    ///   y = (H + 2) − h in the columns nearest the start x (ties: nearer the wall first; a landing needs every cell at
    ///   y ≤ Hy − 1, E-57), (3) the end of the truck queue. The wrong-placement bounce and the S3 broken-glass return
    ///   (`SkipStart`) share it.
    /// - `MovePiece` / `LockPiece` / `StickPiece` keep the piece table, the occupancy grids and the per-column `filled` /
    ///   `wrongOcc` masks in step (O(cells)) and own the Y8 `stuck` lifecycle (TECH §5.2): `stuck` turns true only through
    ///   `StickPiece`; it turns false when the piece leaves the site (yard, queue, gone — `MovePiece`) or locks
    ///   (`LockPiece`). A stuck block returned to its site start keeps `stuck`.
    ///
    /// Board coordinates: x 0 … wy+ws−1, board row y (site: plan row + elevator offset, K-24); sizes from `s.Lvl.Geo`
    /// (K-49). `At` values for the site carry the visible segment. A `PiecePlace` mirrors the piece record instead (site
    /// y = plan row of the segment frame).
    /// </summary>
    public static class Placement
    {
        private const int NoRow = 99;

        private static readonly Verdict OkVerdict = new Verdict(true, new string[0], new At[0]);
        private static readonly int[] NoRows = new int[0];

        /// <summary>The piece record as a place (snapshot it at move start: K-17 needs the start after the piece has moved).</summary>
        public static PiecePlace PiecePlace(GameState s, int id)
        {
            int zone = PieceZone(s, id);
            string name = zone >= 0 && zone < Zone.Names.Length ? Zone.Names[zone] : Zone.Names[Zone.Gone];
            return new PiecePlace(name, PieceX(s, id), PieceY(s, id), PieceSeg(s, id));
        }

        /// <summary>The site place of a block anchored at board `(ix, iy)` on the visible segment.</summary>
        public static PiecePlace SitePlace(GameState s, Anchor anchor) => Core.PiecePlace.Site(anchor.Ix, anchor.Iy - Hdr(s, H.Elev), Grid.VisibleSegment(s));

        /// <summary>The yard place of a block anchored at board `(ix, iy)`.</summary>
        public static PiecePlace YardPlace(Anchor anchor) => Core.PiecePlace.Yard(anchor.Ix, anchor.Iy);

        /// <summary>Event position of a yard or site place (TECH §6.3 `At`, board coordinates); null for queue / gone / pending.</summary>
        public static At? PlaceAt(GameState s, PiecePlace place)
        {
            if (place.Zone == At.ZoneYard) return At.Yard(place.X, place.Y);
            if (place.Zone == At.ZoneSite) return At.Site(place.X, place.Y + Hdr(s, H.Elev), place.Seg);
            return null;
        }

        // --- K-16 + K-34: the single correctness test -------------------------------------------------------------------------

        /// <summary>
        /// `isCorrectPlacement(state, pieceId, cells)` (TECH §5.2, GDD K-16, K-34). `cells` are board cells on the visible
        /// segment (the piece may still be at its start: its own `wrongOcc` bits are ignored, so a stuck block lifted from a
        /// `.` cell does not block itself). Conditions, all evaluated, reasons in the fixed order:
        /// 1. `debris` — Faz 2R: never produced (S4 debris is a material block; GDD K-16 (2) removed, K-34 hook 2).
        /// 2. `outside` — a cell is outside the active plan area (K-16 (1)).
        /// 3. `window` — a cell is on a `.` plan cell (K-16 (1), S2).
        /// 4. `color` — a plan colour (resolved `?` colour, K-32) differs from the block colour (K-16 (1)).
        /// 5. `support` — K-34: in every covered site column, every plan row below the block's lowest cell is correctly filled
        ///    or an EMPTY `.` cell. `MissingSupport` = the breaking plan cells: unfilled colour cells and `.` cells holding a
        ///    wrong object (debris, stuck mortar; E-43), column then row order.
        /// </summary>
        public static Verdict IsCorrectPlacement(GameState s, int pieceId, BoardCell[] cells)
        {
            int seg = Grid.VisibleSegment(s);
            var plan = seg >= 0 && seg < s.Lvl.Segments.Length ? s.Lvl.Segments[seg] : null;
            var geo = s.Lvl.Geo;
            int siteX = geo.SiteX;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int elev = Hdr(s, H.Elev);
            int color = PieceColor(s, pieceId);
            // v3 Y9 (GDD §16 K-57 item 1): a two-tone block is checked per cell; `cells` are in the shape's cell order
            bool twoTone = IsTwoTone(s, pieceId);
            bool outside = false;
            bool window = false;
            bool wrongColor = false;
            var low = new int[ws];
            for (int sx = 0; sx < ws; sx++) low[sx] = NoRow;
            for (int ci = 0; ci < cells.Length; ci++)
            {
                var c = cells[ci];
                int sx = c.X - siteX;
                int sy = c.Y - elev;
                if (sx < 0 || sx >= ws || sy < 0)
                {
                    outside = true;
                    continue;
                }
                low[sx] = Math.Min(low[sx], sy);
                // site air (sy ≥ hs, K-03) is outside the plan area
                int v = plan != null && sy < hs ? plan.PlanColors[sy * ws + sx] : Compiler.PlanOutside;
                if (v == Compiler.PlanOutside) outside = true;
                else if (v == Compiler.PlanDot) window = true;
                else if (v != (twoTone ? PieceCellColor(s, pieceId, ci) : color)) wrongColor = true;
            }

            // K-34: missing support per covered column (the piece's own cells never count as a wrong object below itself)
            var own = OwnSiteRows(s, pieceId, seg);
            var miss = new int[ws];
            bool support = false;
            if (plan != null)
            {
                for (int sx = 0; sx < ws; sx++)
                {
                    int r = low[sx];
                    if (r == NoRow || r <= 0) continue;
                    int dot = plan.DotMask[sx];
                    int area = plan.PlanMask[sx] | dot;
                    int wrong = WrongOccMask(s, seg, sx) & ~(sx < own.Length ? own[sx] : 0);
                    int counted = FilledMask(s, seg, sx) | (dot & ~wrong);
                    miss[sx] = RowsBelow(r) & area & ~counted;
                    if (miss[sx] != 0) support = true;
                }
            }
            // Faz 2R (OBSTACLES S4, GDD K-16 (2) removed): debris is a material block; `debris` is never produced (K-34 hook 2).
            if (!outside && !window && !wrongColor && !support) return OkVerdict;

            var reasons = new List<string>();
            if (outside) reasons.Add(VerdictReasons.Outside);
            if (window) reasons.Add(VerdictReasons.Window);
            if (wrongColor) reasons.Add(VerdictReasons.Color);
            if (support) reasons.Add(VerdictReasons.Support);
            var missingSupport = new List<At>();
            for (int sx = 0; sx < ws; sx++)
            {
                for (int m = miss[sx]; m != 0; m &= m - 1)
                {
                    missingSupport.Add(At.Site(siteX + sx, Bits.LowestBit(m) + elev, seg));
                }
            }
            return new Verdict(false, reasons.ToArray(), missingSupport.ToArray());
        }

        /// <summary>
        /// The cells behind one verdict reason (UX §5.4 45° hatch; review Faz 2 tur 1 #1): the `cells` of `IsCorrectPlacement`
        /// that break `reason` — `debris`: every cell; `outside`: the cells outside the active plan area; `window`: the cells on
        /// `.` plan cells; `color`: the cells whose plan colour (resolved `?` colour) differs from the block's. `support` has no
        /// cell of the block (its cells are `Verdict.MissingSupport`, under the block): empty. Board-cell order of `cells`.
        /// </summary>
        public static BoardCell[] ReasonCells(GameState s, int pieceId, BoardCell[] cells, string reason)
        {
            if (reason == VerdictReasons.Support) return new BoardCell[0];
            if (reason == VerdictReasons.Debris) return (BoardCell[])cells.Clone();
            int seg = Grid.VisibleSegment(s);
            var plan = seg >= 0 && seg < s.Lvl.Segments.Length ? s.Lvl.Segments[seg] : null;
            var geo = s.Lvl.Geo;
            int siteX = geo.SiteX;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int elev = Hdr(s, H.Elev);
            var o = new List<BoardCell>();
            for (int ci = 0; ci < cells.Length; ci++)
            {
                var c = cells[ci];
                int color = PieceCellColor(s, pieceId, ci);
                int sx = c.X - siteX;
                int sy = c.Y - elev;
                bool inside = sx >= 0 && sx < ws && sy >= 0;
                int v = inside && plan != null && sy < hs ? plan.PlanColors[sy * ws + sx] : Compiler.PlanOutside;
                bool hit = reason == VerdictReasons.Outside
                    ? v == Compiler.PlanOutside
                    : reason == VerdictReasons.Window
                        ? v == Compiler.PlanDot
                        : v != Compiler.PlanOutside && v != Compiler.PlanDot && v != color;
                if (hit) o.Add(new BoardCell(c.X, c.Y));
            }
            return o.ToArray();
        }

        /// <summary>Every cell is inside the visible segment's plan area (colour, `?` or `.`): the Y8 sticking condition (E-08).</summary>
        public static bool AllCellsInPlanArea(GameState s, BoardCell[] cells)
        {
            int seg = Grid.VisibleSegment(s);
            if (seg < 0 || seg >= s.Lvl.Segments.Length) return false;
            var plan = s.Lvl.Segments[seg];
            var geo = s.Lvl.Geo;
            int siteX = geo.SiteX;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int elev = Hdr(s, H.Elev);
            foreach (var c in cells)
            {
                int sx = c.X - siteX;
                int sy = c.Y - elev;
                if (sx < 0 || sx >= ws || sy < 0 || sy >= hs) return false;
                if (plan.PlanColors[sy * ws + sx] == Compiler.PlanOutside) return false;
            }
            return true;
        }

        // --- K-34 hook 1: build front ---------------------------------------------------------------------------------------

        /// <summary>
        /// `buildFront(state)` (GDD K-34 hook 1, TECH §5.2): per column of the visible segment, the lowest plan cell that is
        /// not correctly filled and not an empty `.` cell — when that cell is empty. A wrong object there (debris, stuck
        /// mortar; also on a `.` cell, E-43) or a complete column gives no front cell. First site column (x = wy) first.
        /// </summary>
        public static At[] BuildFront(GameState s)
        {
            int seg = Grid.VisibleSegment(s);
            var o = new List<At>();
            if (seg < 0 || seg >= s.Lvl.Segments.Length) return o.ToArray();
            var plan = s.Lvl.Segments[seg];
            int siteX = s.Lvl.Geo.SiteX;
            int ws = s.Lvl.Geo.Ws;
            int elev = Hdr(s, H.Elev);
            for (int sx = 0; sx < ws; sx++)
            {
                int dot = plan.DotMask[sx];
                int area = plan.PlanMask[sx] | dot;
                int dotFree = dot & ~WrongOccMask(s, seg, sx);
                int free = area & ~(FilledMask(s, seg, sx) | dotFree);
                if (free == 0) continue;
                int row = Bits.LowestBit(free);
                if (SiteOcc(s, seg, sx, row) != 0) continue;
                o.Add(At.Site(siteX + sx, row + elev, seg));
            }
            return o.ToArray();
        }

        /// <summary>Golden Trowel targets (K-33): exactly the build front (one function, TECH §5.2).</summary>
        public static At[] EligibleTrowelCells(GameState s) => BuildFront(s);

        // --- K-15 ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// GDD K-15: segment `seg` is complete when every non-`.` plan cell is correctly filled (locked block or trowel) and no
        /// other block is in the segment's site area (debris or a stuck mortar block anywhere in the ws × hs part, E-24).
        /// </summary>
        public static bool IsSegmentComplete(GameState s, int seg)
        {
            if (seg < 0 || seg >= s.Lvl.Segments.Length) return false;
            var plan = s.Lvl.Segments[seg];
            int ws = s.Lvl.Geo.Ws;
            int hs = s.Lvl.Geo.Hs;
            for (int sx = 0; sx < ws; sx++)
            {
                int need = plan.PlanMask[sx];
                if ((FilledMask(s, seg, sx) & need) != need) return false;
            }
            for (int sy = 0; sy < hs; sy++)
            {
                for (int sx = 0; sx < ws; sx++)
                {
                    int v = SiteOcc(s, seg, sx, sy);
                    if (v > 0 && (PieceFlagBits(s, v - 1) & FlagBit.Locked) == 0) return false;
                }
            }
            return true;
        }

        // --- K-17: bounce-back target ---------------------------------------------------------------------------------------

        /// <summary>
        /// Yard drop columns (K-17 step 2; same order as K-25 stage 3): every anchor column `0 … wy − w`, nearest to `x` first,
        /// ties to the larger x (nearer the wall).
        /// </summary>
        public static int[] NearestColumnsFirst(BoardGeo geo, int x, int w)
        {
            var o = new List<int>();
            for (int c = 0; c <= geo.Wy - w; c++) o.Add(c);
            o.Sort((a, b) =>
            {
                int d = Math.Abs(a - x) - Math.Abs(b - x);
                return d != 0 ? d : b - a;
            });
            return o.ToArray();
        }

        /// <summary>K-17 step 2 / K-25 drop start row: `(H + 2) − h` (the block's top cell in the top crane row; default 10 − h).</summary>
        public static int YardDropRow(BoardGeo geo, ShapeDef shape) => geo.Rows - shape.H;

        /// <summary>
        /// Drop over the yard (K-17 step 2, K-25): the block falls from `y = (H + 2) − h` in anchor column `ix`, whatever the
        /// yard gravity setting, onto its first support (open sky from the top: the highest occupied cell of each covered
        /// column; crates and bags support, hidden items do not). Returns the landing row, or −1 when a landed cell would be at
        /// y ≥ Hy (yard air or crane area; E-57) or the column range is invalid. `exclude` = a piece to ignore (the moving one).
        /// </summary>
        public static int DropIntoYard(GameState s, ShapeDef shape, int ix, int exclude = -1)
        {
            var geo = s.Lvl.Geo;
            int wy = geo.Wy;
            int hy = geo.Hy;
            int rows = geo.Rows;
            if (ix < 0 || ix + shape.W > wy) return -1;
            int skip = exclude + 1;
            int land = 0;
            for (int c = 0; c < shape.W; c++)
            {
                int top = -1;
                for (int y = rows - 1; y >= 0; y--)
                {
                    int v = YardOcc(s, ix + c, y);
                    if (v != 0 && v != skip)
                    {
                        top = y;
                        break;
                    }
                }
                land = Math.Max(land, top + 1 - shape.ColBottom[c]);
            }
            return land + shape.H <= hy ? land : -1;
        }

        /// <summary>
        /// `returnTarget(state, pieceId, opts)` (GDD K-17, TECH §5.2). Pure: finds the target, does not move the piece.
        /// Step 1: every start cell is empty (the piece itself does not count; the board is frozen during a drag, K-08, so this
        /// always holds for yard and debris starts). Step 2: `NearestColumnsFirst(start x, w)` with `DropIntoYard` — for a site
        /// start (x ≥ wy) the first candidate is the column nearest the wall (`wy − w`). Step 3: end of the truck queue (K-26);
        /// the queued block keeps the start column clamped into the yard as its first truck column.
        /// </summary>
        public static ReturnTarget ReturnTarget(GameState s, int pieceId, ReturnOptions? opts = null)
        {
            opts ??= new ReturnOptions();
            var start = opts.Start ?? PiecePlace(s, pieceId);
            var shape = Shapes.ShapeByIndex(PieceShape(s, pieceId));
            var geo = s.Lvl.Geo;
            if (!opts.SkipStart && StartCellsEmpty(s, pieceId, shape, start)) return new ReturnTarget(1, start, null);
            foreach (int x in NearestColumnsFirst(geo, start.X, shape.W))
            {
                int y = DropIntoYard(s, shape, x, pieceId);
                if (y >= 0) return new ReturnTarget(2, YardPlace(new Anchor(x, y)), new Anchor(x, YardDropRow(geo, shape)));
            }
            int qx = Math.Min(Math.Max(start.X, 0), geo.Wy - shape.W);
            return new ReturnTarget(3, Core.PiecePlace.Queue(qx, geo.Hy), null);
        }

        private static bool StartCellsEmpty(GameState s, int id, ShapeDef shape, PiecePlace start)
        {
            int self = id + 1;
            var geo = s.Lvl.Geo;
            int wy = geo.Wy;
            int hy = geo.Hy;
            int siteX = geo.SiteX;
            int ws = geo.Ws;
            int hs = geo.Hs;
            foreach (var c in shape.Cells)
            {
                int x = start.X + c.X;
                int y = start.Y + c.Y;
                if (start.Zone == At.ZoneYard)
                {
                    if (x < 0 || x >= wy || y < 0 || y >= hy) return false;
                    int v = YardOcc(s, x, y);
                    if (v != 0 && v != self) return false;
                }
                else if (start.Zone == At.ZoneSite)
                {
                    int sx = x - siteX;
                    if (sx < 0 || sx >= ws || y < 0 || y >= hs) return false;
                    int v = SiteOcc(s, start.Seg, sx, y);
                    if (v != 0 && v != self) return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        // --- state updates and the Y8 stuck lifecycle ---------------------------------------------------------------------------

        /// <summary>
        /// Moves a piece to `to` (yard, site, queue end or gone), keeping occupancy, `filled`, `wrongOcc` and the FIFO queue
        /// consistent (O(cells)). Leaving the site clears the piece's mask bits; entering it sets `wrongOcc` for debris and
        /// stuck blocks (plan area rows) and `filled` for locked ones. Y8 lifecycle (a), (c): any destination other than the site
        /// clears `stuck`. Throws when a yard / site destination has a cell outside the yard / the site frame.
        /// </summary>
        public static void MovePiece(GameState s, int id, PiecePlace to)
        {
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            AssertPlaceFits(s.Lvl.Geo, shape, to);
            LeaveBoard(s, id, shape);
            SetPieceField(s, id, PF.X, to.X);
            SetPieceField(s, id, PF.Y, to.Y);
            SetPieceField(s, id, PF.Seg, to.Zone == At.ZoneSite ? to.Seg : -1);
            if (to.Zone != At.ZoneSite) SetFlag(s, id, FlagBit.Stuck, false);
            if (to.Zone == Zone.Names[Zone.Queue])
            {
                EnqueuePiece(s, id);
                return;
            }
            SetPieceField(s, id, PF.Zone, Zone.IndexOf(to.Zone));
            EnterBoard(s, id, shape);
        }

        /// <summary>
        /// K-14 lock of a correctly placed site piece: `locked = true`, `stuck = false` (Y8 lifecycle (b)), its plan cells join
        /// `filled` and leave `wrongOcc`; hidden `?` cells under it open (K-32). Returns the newly revealed cells.
        /// </summary>
        public static RevealedCell[] LockPiece(GameState s, int id)
        {
            if (PieceZone(s, id) != Zone.Site) throw new ArgumentOutOfRangeException(nameof(id), "lockPiece: piece " + id + " is not on the site");
            SetFlag(s, id, FlagBit.Locked, true);
            SetFlag(s, id, FlagBit.Stuck, false);
            int seg = PieceSeg(s, id);
            var plan = seg >= 0 && seg < s.Lvl.Segments.Length ? s.Lvl.Segments[seg] : null;
            int siteX = s.Lvl.Geo.SiteX;
            int ws = s.Lvl.Geo.Ws;
            int elev = Hdr(s, H.Elev);
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            int revealed = RevealedMask(s, seg);
            var opened = new List<RevealedCell>();
            foreach (var c in shape.Cells)
            {
                int sx = PieceX(s, id) + c.X - siteX;
                int sy = PieceY(s, id) + c.Y;
                int bit = 1 << sy;
                SetWrongOccMask(s, seg, sx, WrongOccMask(s, seg, sx) & ~bit);
                SetFilledMask(s, seg, sx, FilledMask(s, seg, sx) | (bit & (plan != null ? plan.PlanMask[sx] : 0)));
                int local = sy * ws + sx;
                if (plan != null && ((plan.HiddenMask >> local) & 1) != 0 && ((revealed >> local) & 1) == 0)
                {
                    revealed |= 1 << local;
                    int colorIndex = plan.PlanColors[local];
                    opened.Add(new RevealedCell(siteX + sx, sy + elev, colorIndex >= 0 && colorIndex < ColorCodes.All.Length ? ColorCodes.All[colorIndex] : "W"));
                }
            }
            SetRevealedMask(s, seg, revealed);
            return opened.ToArray();
        }

        /// <summary>Y8 sticking of a wrongly placed site piece: `stuck = true`, its plan-area cells join `wrongOcc` (K-34, E-43).</summary>
        public static void StickPiece(GameState s, int id)
        {
            if (PieceZone(s, id) != Zone.Site) throw new ArgumentOutOfRangeException(nameof(id), "stickPiece: piece " + id + " is not on the site");
            SetFlag(s, id, FlagBit.Stuck, true);
            MarkSiteMasks(s, id, Shapes.ShapeByIndex(PieceShape(s, id)));
        }

        /// <summary>
        /// K-35 step 3 for a block that landed (fall or rail) at board anchor `landing` of the visible segment, with the
        /// `verdict` of `IsCorrectPlacement` (`ComputeFall` gives both). Moves the piece exactly once, to its final place:
        /// correct → lock (K-14, K-32); wrong + `stick` (Y8, all cells in the plan area) → stuck at the landing; otherwise the
        /// K-17 bounce to `ReturnTarget` (a stuck block bounced back to its site start stays stuck). Combo, move cost and events
        /// belong to the move pipeline.
        /// </summary>
        public static PlacementOutcome SettlePlacement(GameState s, int pieceId, Anchor landing, Verdict verdict, SettleOptions? opts = null)
        {
            opts ??= new SettleOptions();
            var start = opts.Start ?? PiecePlace(s, pieceId);
            var shape = Shapes.ShapeByIndex(PieceShape(s, pieceId));
            var cells = Movement.BlockCells(shape, landing.Ix, landing.Iy);
            var at = SitePlace(s, landing);
            if (verdict.Ok)
            {
                MovePiece(s, pieceId, at);
                var revealed = LockPiece(s, pieceId);
                int seg = at.Seg;
                var atCells = new At[cells.Length];
                for (int i = 0; i < cells.Length; i++) atCells[i] = At.Site(cells[i].X, cells[i].Y, seg);
                return PlacementOutcome.Correct(at, atCells, revealed);
            }
            string reason = verdict.Reasons.Length > 0 ? verdict.Reasons[0] : VerdictReasons.Outside;
            string? over = opts.Rules?.OnPlacement?.Invoke(s, pieceId, cells, verdict);
            if (over == PlacementOverrides.Stick && AllCellsInPlanArea(s, cells))
            {
                MovePiece(s, pieceId, at);
                StickPiece(s, pieceId);
                return PlacementOutcome.Stuck(at, reason, verdict.MissingSupport);
            }
            var target = ReturnTarget(s, pieceId, new ReturnOptions(start: start));
            MovePiece(s, pieceId, target.To);
            return PlacementOutcome.Bounced(landing, target, reason, verdict.MissingSupport);
        }

        /// <summary>
        /// S3 broken glass (K-35 step 2, GDD K-17 exceptions): the block returns by the K-17 search without a placement check;
        /// a site start (stuck glass mortar) skips step 1, so it goes to the yard (or the queue) and unsticks. Returns the target.
        /// </summary>
        public static ReturnTarget ReturnBrokenPiece(GameState s, int pieceId, PiecePlace? start = null)
        {
            var from = start ?? PiecePlace(s, pieceId);
            var target = ReturnTarget(s, pieceId, new ReturnOptions(skipStart: from.Zone == At.ZoneSite, start: from));
            MovePiece(s, pieceId, target.To);
            return target;
        }

        // --- internals -------------------------------------------------------------------------------------------------------

        private static int RowsBelow(int r) => r >= 31 ? 0x7fffffff : (1 << r) - 1;

        /// <summary>Plan-row bits of the piece's own cells per site column when it sits on segment `seg` (else none).</summary>
        private static int[] OwnSiteRows(GameState s, int id, int seg)
        {
            if (id < 0 || PieceZone(s, id) != Zone.Site || PieceSeg(s, id) != seg) return NoRows;
            var geo = s.Lvl.Geo;
            int siteX = geo.SiteX;
            int ws = geo.Ws;
            int hs = geo.Hs;
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            var rows = new int[ws];
            foreach (var c in shape.Cells)
            {
                int sx = PieceX(s, id) + c.X - siteX;
                int sy = PieceY(s, id) + c.Y;
                if (sx >= 0 && sx < ws && sy >= 0 && sy < hs) rows[sx] |= 1 << sy;
            }
            return rows;
        }

        private static void AssertPlaceFits(BoardGeo geo, ShapeDef shape, PiecePlace to)
        {
            if (to.Zone != At.ZoneYard && to.Zone != At.ZoneSite) return;
            foreach (var c in shape.Cells)
            {
                int x = to.X + c.X;
                int y = to.Y + c.Y;
                bool ok = to.Zone == At.ZoneYard
                    ? x >= 0 && x < geo.Wy && y >= 0 && y < geo.Hy
                    : x >= geo.SiteX && x < geo.Cols && y >= 0 && y < geo.Hs && to.Seg >= 0;
                if (!ok)
                    throw new ArgumentOutOfRangeException(nameof(to), "movePiece: " + shape.Id + " at " + to.Zone + " (" + to.X + "," + to.Y + ") leaves the " + to.Zone);
            }
        }

        /// <summary>Takes the piece off the occupancy grids, the site masks and the queue.</summary>
        private static void LeaveBoard(GameState s, int id, ShapeDef shape)
        {
            int zone = PieceZone(s, id);
            if (zone == Zone.Queue)
            {
                int n = Hdr(s, H.QueueLen);
                for (int i = 0; i < n; i++)
                {
                    if (s.Buf[s.Lvl.Layout.Queue + i] == id)
                    {
                        RemoveQueueAt(s, i);
                        break;
                    }
                }
                return;
            }
            if (zone != Zone.Yard && zone != Zone.Site) return;
            if (zone == Zone.Site)
            {
                int seg = PieceSeg(s, id);
                int siteX = s.Lvl.Geo.SiteX;
                foreach (var c in shape.Cells)
                {
                    int sx = PieceX(s, id) + c.X - siteX;
                    int bit = 1 << (PieceY(s, id) + c.Y);
                    SetWrongOccMask(s, seg, sx, WrongOccMask(s, seg, sx) & ~bit);
                    SetFilledMask(s, seg, sx, FilledMask(s, seg, sx) & ~bit);
                }
            }
            Grid.VacatePiece(s, id);
        }

        private static void EnterBoard(GameState s, int id, ShapeDef shape)
        {
            int zone = PieceZone(s, id);
            if (zone != Zone.Yard && zone != Zone.Site) return;
            Grid.OccupyPiece(s, id);
            if (zone == Zone.Site) MarkSiteMasks(s, id, shape);
        }

        /// <summary>Sets the piece's `filled` (locked) or `wrongOcc` (debris, stuck) bits on its plan-area cells.</summary>
        private static void MarkSiteMasks(GameState s, int id, ShapeDef shape)
        {
            int flags = PieceFlagBits(s, id);
            bool locked = (flags & FlagBit.Locked) != 0;
            bool wrong = !locked && (flags & (FlagBit.Debris | FlagBit.Stuck)) != 0;
            if (!locked && !wrong) return;
            int seg = PieceSeg(s, id);
            var plan = seg >= 0 && seg < s.Lvl.Segments.Length ? s.Lvl.Segments[seg] : null;
            int siteX = s.Lvl.Geo.SiteX;
            foreach (var c in shape.Cells)
            {
                int sx = PieceX(s, id) + c.X - siteX;
                int bit = 1 << (PieceY(s, id) + c.Y);
                if (locked) SetFilledMask(s, seg, sx, FilledMask(s, seg, sx) | (bit & (plan != null ? plan.PlanMask[sx] : 0)));
                else SetWrongOccMask(s, seg, sx, WrongOccMask(s, seg, sx) | (bit & PlanAreaMask(s.Lvl, seg, sx)));
            }
        }
    }
}
