#nullable enable
// Port of src/core/geometry.ts (web repo, source of truth).
using System;
using MinikUsta.Core.Level;

namespace MinikUsta.Core
{
    /// <summary>
    /// Board geometry of one level (docs/GDD.md K-49, §0; docs/TECH_DESIGN.md §2R.0 item 2, §2R.1). Pure, no state.
    ///
    /// Every encoding (drag node code, Zobrist position table, collision row masks) stays in the MAXIMUM FRAME of
    /// `MAX_COLS × MAX_ROWS` = 8 × 10 cells: node code `(mode·10 + iy)·8 + ix` and yard hash position `y·8 + x` never change
    /// with the level. What a cell MEANS (yard, site, wall boundary, yard air, site air, crane area) comes from the level's
    /// `BoardGeo` (`CompiledLevel.Geo`, read as `s.Lvl.Geo`). K-49 limits (`Wy + Ws ≤ 8`, `H ≤ 8`) fit the frame.
    ///
    /// Regions (global coordinates, x = 0 left, y = 0 bottom):
    /// - yard `x 0 … wy−1, y 0 … hy−1`; yard air `x ≤ wy−1, hy ≤ y ≤ h−1` (only when hy &lt; h);
    /// - site columns `x wy … wy+ws−1`; plan row `sy` is board row `sy + e` (elevator offset, K-24); site air `y ≥ hs + e`;
    /// - wall boundary between `x = wy−1` and `x = wy` (zero width, R-03);
    /// - crane area rows `h` and `h + 1` over every column (K-05).
    /// </summary>
    public static class Geometry
    {
        /// <summary>Maximum frame width: K-49 `Wy + Ws ≤ 9` (D-103 wide site: 6 yard + 3 site columns). A column bit mask needs 9 bits, so collision rows are `ushort`.</summary>
        public const int MaxCols = 9;
        /// <summary>Maximum frame height: K-49 `H ≤ 8` plus the 2 crane-area rows.</summary>
        public const int MaxRows = 10;
        /// <summary>Crane area rows above the board (K-05).</summary>
        public const int CraneRows = 2;
        /// <summary>Highest board height H (K-49); the crane area takes the remaining frame rows.</summary>
        public const int MaxBoardRows = MaxRows - CraneRows;
        /// <summary>Widest site the encodings support (K-49 code-lead limit: Ws 1–4; `ws · hs ≤ 32` bit masks per segment).</summary>
        public const int MaxSiteCols = 4;
        /// <summary>Zobrist / mask slots of one site segment in the maximum frame: `MAX_SITE_COLS × MAX_BOARD_ROWS` (§2R.1 hash).</summary>
        public const int SegmentSlots = MaxSiteCols * MaxBoardRows;
        /// <summary>Most segments of a plan (GDD K-22: 1 ≤ S ≤ 5); sizes the frame's site position range in the hash.</summary>
        public const int MaxSegments = 5;

        /// <summary>GDD K-49 defaults: yard 6 × 8, site 2 × 8, no elevator → H 8, rows 10 (the pre-2R constants).</summary>
        public static readonly GeoParams DefaultSizes = new GeoParams(6, 8, 2, 8, 0);
        public static readonly BoardGeo DefaultGeo = MakeGeo(DefaultSizes);

        private static void AssertInt(string name, int v, int lo, int hi)
        {
            if (v < lo || v > hi)
                throw new ArgumentOutOfRangeException(name, "makeGeo: " + name + " = " + v + " is outside " + lo + "…" + hi + " (K-49 frame)");
        }

        /// <summary>
        /// Builds a level geometry. Throws when the sizes do not fit the maximum frame (wy ≥ 1, ws 1–4, wy + ws ≤ 8, hy and hs
        /// 1–8, eMax 0–3); the validator's K-49 ranges (`size_out_of_range`, `board_too_wide`) are narrower.
        ///
        /// H is clamped to MAX_BOARD_ROWS: a valid level never needs the clamp (`elevator_overflow` asks `hs + b ≤ 8`), but
        /// pre-2R data without `site.rows` (default Hs 8) and an elevator would exceed the frame; with the clamp, plan rows that
        /// the elevator pushes above row H − 1 simply do not exist, which is exactly the pre-2R model (fixtures keep working).
        /// </summary>
        public static BoardGeo MakeGeo(GeoParams p)
        {
            AssertInt("wy", p.Wy, 1, MaxCols - 1);
            AssertInt("ws", p.Ws, 1, MaxSiteCols);
            AssertInt("wy + ws", p.Wy + p.Ws, 2, MaxCols);
            AssertInt("hy", p.Hy, 1, MaxBoardRows);
            AssertInt("hs", p.Hs, 1, MaxBoardRows);
            AssertInt("eMax", p.EMax, 0, MaxBoardRows - 1);
            int h = Math.Min(MaxBoardRows, Math.Max(p.Hy, p.Hs + p.EMax));
            int rows = h + CraneRows;
            return new BoardGeo(p.Wy, p.Hy, p.Ws, p.Hs, p.EMax, h, rows);
        }

        /// <summary>
        /// K-49 geometry of a level: `yard.cols`, `yard.rows`, `site.cols`, `site.rows` (missing → 6, 8, 2, 8) and the
        /// elevator range top `b` (K-24). Pure; compile stores the result as `CompiledLevel.Geo`.
        /// </summary>
        public static BoardGeo GeoFromLevel(LevelData level)
        {
            var el = level.Build.Elevator;
            var p = new GeoParams(
                level.Yard.Cols ?? DefaultSizes.Wy,
                level.Yard.Rows ?? DefaultSizes.Hy,
                level.Site?.Cols ?? DefaultSizes.Ws,
                level.Site?.Rows ?? DefaultSizes.Hs,
                el != null ? Math.Max(0, el.Range[1]) : 0);
            return IsDefaultSizes(p) ? DefaultGeo : MakeGeo(p);
        }

        private static bool IsDefaultSizes(GeoParams p)
        {
            return p.Wy == DefaultSizes.Wy
                && p.Hy == DefaultSizes.Hy
                && p.Ws == DefaultSizes.Ws
                && p.Hs == DefaultSizes.Hs
                && p.EMax == DefaultSizes.EMax;
        }

        /// <summary>Same yard, site and board height as the default 6×8 | 2×8 board (Appendix A: no `size` header line).</summary>
        public static bool HasDefaultBoard(BoardGeo geo)
        {
            return geo.Wy == DefaultGeo.Wy
                && geo.Hy == DefaultGeo.Hy
                && geo.Ws == DefaultGeo.Ws
                && geo.Hs == DefaultGeo.Hs
                && geo.H == DefaultGeo.H;
        }

        /// <summary>`4x4|2x5 H5`: the Appendix A size label (debug, test names, the ASCII `size` header line).</summary>
        public static string GeoLabel(BoardGeo geo)
        {
            return geo.Wy + "x" + geo.Hy + "|" + geo.Ws + "x" + geo.Hs + " H" + geo.H;
        }
    }

    /// <summary>Level size parameters (GDD K-49): yard `Wy × Hy`, site `Ws × Hs`, elevator range top `b` (0 without elevator).</summary>
    public readonly struct GeoParams
    {
        public readonly int Wy;
        public readonly int Hy;
        public readonly int Ws;
        public readonly int Hs;
        public readonly int EMax;

        public GeoParams(int wy, int hy, int ws, int hs, int eMax)
        {
            Wy = wy;
            Hy = hy;
            Ws = ws;
            Hs = hs;
            EMax = eMax;
        }
    }

    /// <summary>The geometry of one level. Immutable; shared by every state of the level (`CompiledLevel.Geo`).</summary>
    public sealed class BoardGeo
    {
        /// <summary>Yard width Wy and height Hy (K-02, K-49).</summary>
        public readonly int Wy;
        public readonly int Hy;
        /// <summary>Site width Ws and plan height Hs (K-03, K-15, K-49).</summary>
        public readonly int Ws;
        public readonly int Hs;
        /// <summary>Top `b` of the elevator range (K-24); 0 without elevator.</summary>
        public readonly int EMax;
        /// <summary>Board height H = max(hy, hs + eMax) (K-49), at most MAX_BOARD_ROWS (see `MakeGeo`).</summary>
        public readonly int H;
        /// <summary>h + 2: board rows plus the crane area (K-05).</summary>
        public readonly int Rows;
        /// <summary>wy + ws.</summary>
        public readonly int Cols;
        /// <summary>= wy: first site column.</summary>
        public readonly int SiteX;
        /// <summary>= wy − 1: last yard column; the wall boundary lies between `BoundaryX` and `SiteX`.</summary>
        public readonly int BoundaryX;
        /// <summary>= h: first crane-area row (K-05).</summary>
        public readonly int CraneRow;
        /// <summary>Column bits of the yard in a frame row mask: (1 &lt;&lt; wy) − 1.</summary>
        public readonly int YardBits;
        /// <summary>Column bits of the site in a frame row mask: ((1 &lt;&lt; ws) − 1) &lt;&lt; wy.</summary>
        public readonly int SiteBits;
        /// <summary>Row bits of every frame row of this level: (1 &lt;&lt; rows) − 1.</summary>
        public readonly int RowMaskAll;
        /// <summary>ws · hs: cells (and local plan indices `sy · ws + sx`) of one segment.</summary>
        public readonly int SegCells;

        internal BoardGeo(int wy, int hy, int ws, int hs, int eMax, int h, int rows)
        {
            Wy = wy;
            Hy = hy;
            Ws = ws;
            Hs = hs;
            EMax = eMax;
            H = h;
            Rows = rows;
            Cols = wy + ws;
            SiteX = wy;
            BoundaryX = wy - 1;
            CraneRow = h;
            YardBits = (1 << wy) - 1;
            SiteBits = ((1 << ws) - 1) << wy;
            RowMaskAll = (1 << rows) - 1;
            SegCells = ws * hs;
        }

        /// <summary>Structural equality (TS `toEqual` on the frozen object).</summary>
        public bool SameAs(BoardGeo o)
        {
            return Wy == o.Wy && Hy == o.Hy && Ws == o.Ws && Hs == o.Hs && EMax == o.EMax && H == o.H && Rows == o.Rows;
        }

        public override string ToString() => Geometry.GeoLabel(this);
    }
}
