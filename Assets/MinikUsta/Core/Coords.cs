#nullable enable
// Port of src/core/coords.ts (web repo, source of truth).
using System;
using System.Collections.Generic;

namespace MinikUsta.Core
{
    /// <summary>
    /// Grid geometry and the wall boundary (docs/GDD.md §0, K-01…K-05, K-49; TECH_DESIGN §2.2 edge model, §2R.1).
    ///
    /// Internal coordinates are the global ones (no conversion) in the 8 × 10 MAXIMUM FRAME (Geometry.cs): cell and drag
    /// node codes never depend on the level. What a cell is comes from the level geometry `geo` (`s.Lvl.Geo`): x 0 … wy−1
    /// yard, x wy … wy+ws−1 site, y 0 … h−1 board, y h … h+1 crane area. The wall is the ZERO-WIDTH boundary between
    /// x = wy−1 and x = wy: it has no cells. A row of the boundary is described by bit `y` of a `geo.Rows`-bit row mask.
    ///
    /// Every region function takes `geo` first. The pre-2R constants below describe the DEFAULT 6×8 | 2×8 board only; they
    /// are a transition layer (TS: ESLint forbids importing them elsewhere, TECH §2R.1 acceptance) — do not use them in new code.
    /// </summary>
    public static class Coords
    {
        // --- deprecated default-board constants (TECH §2R.1 mapping table; do not use outside this file) ----------------

        [Obsolete("MAX_COLS (encoding) / geo.Cols (validity)")] public const int GridCols = Geometry.MaxCols;
        [Obsolete("MAX_ROWS (encoding) / geo.Rows (validity)")] public const int GridRows = Geometry.MaxRows;
        [Obsolete("encoding only: MAX_COLS * MAX_ROWS")] public const int GridCells = Geometry.MaxCols * Geometry.MaxRows;
        [Obsolete("geo.H (board height H)")] public static readonly int BoardRows = Geometry.DefaultGeo.H;
        [Obsolete("geo.CraneRow (K-05)")] public static readonly int CraneRow = Geometry.DefaultGeo.CraneRow;
        [Obsolete("geo.Wy")] public static readonly int YardCols = Geometry.DefaultGeo.Wy;
        [Obsolete("geo.SiteX")] public static readonly int SiteX = Geometry.DefaultGeo.SiteX;
        [Obsolete("geo.Ws")] public static readonly int SiteCols = Geometry.DefaultGeo.Ws;
        [Obsolete("geo.BoundaryX; the boundary lies between BoundaryX and SiteX")] public static readonly int BoundaryX = Geometry.DefaultGeo.BoundaryX;
        [Obsolete("geo.Wy * geo.Hy (yard cell count C, K-02)")] public static readonly int YardCells = Geometry.DefaultGeo.Wy * Geometry.DefaultGeo.Hy;
        [Obsolete("geo.SegCells (= ws · hs)")] public static readonly int SegmentCells = Geometry.DefaultGeo.SegCells;
        [Obsolete("geo.RowMaskAll")] public static readonly int RowMaskAll = Geometry.DefaultGeo.RowMaskAll;

        // --- frame encoding (level independent) ----------------------------------------------------------------------------

        /// <summary>Frame cell index `y * 8 + x` (TECH §2.1; the same for every geometry).</summary>
        public static int CellIndex(int x, int y) => y * Geometry.MaxCols + x;
        public static int CellX(int cell) => cell % Geometry.MaxCols;
        public static int CellY(int cell) => cell / Geometry.MaxCols;

        /// <summary>Integer code of a drag node: `(mode * 10 + iy) * 8 + ix` (TECH §2.1, §2R.0 item 2: the frame code, every geometry).</summary>
        public static int DragNodeCode(DragNode node) => (node.Mode * Geometry.MaxRows + node.Iy) * Geometry.MaxCols + node.Ix;

        public static DragNode DragNodeFromCode(int code)
        {
            int ix = code % Geometry.MaxCols;
            int rest = code / Geometry.MaxCols;
            return new DragNode(ix, rest % Geometry.MaxRows, rest / Geometry.MaxRows);
        }

        // --- regions (K-01…K-05, K-49) ---------------------------------------------------------------------------------------

        /// <summary>Board + crane area: K-01, no cell may have y &gt; h + 1, x &lt; 0 or x &gt; wy + ws − 1.</summary>
        public static bool InGrid(BoardGeo geo, int x, int y) => x >= 0 && x < geo.Cols && y >= 0 && y < geo.Rows;
        /// <summary>Inside the (wy + ws) × h board.</summary>
        public static bool OnBoard(BoardGeo geo, int x, int y) => x >= 0 && x < geo.Cols && y >= 0 && y < geo.H;
        /// <summary>Yard (K-02): x 0 … wy−1, y 0 … hy−1.</summary>
        public static bool IsYardCell(BoardGeo geo, int x, int y) => x >= 0 && x < geo.Wy && y >= 0 && y < geo.Hy;
        /// <summary>Yard air (§0, Faz 2R): x ≤ wy−1 and hy ≤ y ≤ h−1 (exists only when hy &lt; h). Passed while dragging, never a drop.</summary>
        public static bool IsYardAir(BoardGeo geo, int x, int y) => x >= 0 && x < geo.Wy && y >= geo.Hy && y < geo.H;
        /// <summary>A board cell of the site columns (K-03): x wy … wy+ws−1, y 0 … h−1 (platform, plan area and site air).</summary>
        public static bool IsSiteCell(BoardGeo geo, int x, int y) => x >= geo.SiteX && x < geo.Cols && y >= 0 && y < geo.H;
        /// <summary>Site air (K-03): a site-column board cell with y ≥ hs + e (`elev` = elevator offset, K-24).</summary>
        public static bool IsSiteAir(BoardGeo geo, int x, int y, int elev = 0) => IsSiteCell(geo, x, y) && y >= geo.Hs + elev;
        /// <summary>Crane area (K-05): rows h and h + 1 over every column.</summary>
        public static bool IsCraneCell(BoardGeo geo, int x, int y) => x >= 0 && x < geo.Cols && y >= geo.CraneRow && y < geo.Rows;
        /// <summary>Which side of the boundary a column is on: "yard" or "site".</summary>
        public static string SideOf(BoardGeo geo, int x) => x <= geo.BoundaryX ? At.ZoneYard : At.ZoneSite;

        /// <summary>A box `[ix, ix + w)` straddles the boundary when it has columns on both sides (K-07 row 4, E-06, E-28).</summary>
        public static bool StraddlesBoundary(BoardGeo geo, int ix, int w) => ix <= geo.BoundaryX && ix + w > geo.SiteX;

        /// <summary>Row mask of rows `[y, y + size)`, clipped to the level's frame rows `0 … rows − 1`.</summary>
        public static int RowRangeMask(BoardGeo geo, int y, int size)
        {
            if (size <= 0) return 0;
            int lo = Math.Max(0, y);
            int hi = Math.Min(geo.Rows, y + size);
            if (hi <= lo) return 0;
            return (((1 << (hi - lo)) - 1) << lo) & geo.RowMaskAll;
        }

        /// <summary>
        /// `openFree` (TECH §2.2): rows where a FREE-mode cell may cross the boundary = every row `y ≥ height`
        /// (air above the wall + crane area, K-05: crane rows h, h + 1 are always open).
        /// </summary>
        public static int OpenFreeMask(BoardGeo geo, int wallHeight) => RowRangeMask(geo, wallHeight, geo.Rows - wallHeight);

        /// <summary>`openRail[g]` (TECH §2.2): the rows of gap `g` when it is open, otherwise 0.</summary>
        public static int OpenRailMask(BoardGeo geo, int gapY, int gapSize, bool open) => open ? RowRangeMask(geo, gapY, gapSize) : 0;

        /// <summary>
        /// Closed boundary rows (GDD K-04): `y &lt; height` and not inside an OPEN gap. Crane rows are never closed.
        /// `openGapRows` = OR of the open gaps' row masks.
        /// </summary>
        public static int ClosedBoundaryMask(BoardGeo geo, int wallHeight, int openGapRows)
        {
            return RowRangeMask(geo, 0, Math.Min(wallHeight, geo.H)) & ~openGapRows & geo.RowMaskAll;
        }

        /// <summary>
        /// Crossing test for one horizontal step (TECH §2.2): every cell that changes side does so in its own row, and that
        /// row must be in the `open` mask of the current mode. `crossRows` is the row mask (already shifted by the anchor row)
        /// of the cells that change side. True when the step is allowed by the boundary. Pure bit logic (no geometry).
        /// </summary>
        public static bool BoundaryAllows(int crossRows, int open) => (crossRows & ~open) == 0;

        /// <summary>
        /// The 4-neighbours of a cell inside the same region (GDD §0: neighbourhood never crosses the wall boundary;
        /// x = wy−1 and x = wy are never neighbours whatever the wall height or gaps, E-46): yard cells see yard cells
        /// (x ≤ wy−1, y ≤ hy−1), site-column cells see site-column board cells. Order: left, right, down, up.
        /// </summary>
        public static List<Anchor> Neighbors4(BoardGeo geo, int x, int y)
        {
            var o = new List<Anchor>(4);
            bool yard = IsYardCell(geo, x, y);
            if (!yard && !IsSiteCell(geo, x, y)) return o;
            Add(geo, yard, x - 1, y, o);
            Add(geo, yard, x + 1, y, o);
            Add(geo, yard, x, y - 1, o);
            Add(geo, yard, x, y + 1, o);
            return o;
        }

        private static void Add(BoardGeo geo, bool yard, int nx, int ny, List<Anchor> o)
        {
            bool inRegion = yard ? IsYardCell(geo, nx, ny) : IsSiteCell(geo, nx, ny);
            if (inRegion) o.Add(new Anchor(nx, ny));
        }
    }
}
