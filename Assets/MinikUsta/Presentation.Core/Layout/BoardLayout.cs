#nullable enable
// Board and screen layout of the level screen — port of web src/theme/layout.ts (`adaptiveCellPx`, `boardLayout`,
// `createLayout`; TECH §2.2 "Ekran eşlemesi", §2R.1, §10.1; UX_FLOWS §5.1, §5.8; GDD K-49). Pure: no engine, no state.
// Global board coordinates of the level geometry (`BoardGeo`): x 0 … wy−1 yard, wy … cols−1 site; y 0 … h−1 board rows
// (0 = bottom), h … h+1 crane area (K-05). The wall is a zero-width boundary between x = wy−1 and x = wy (R-03), drawn as
// a `wallW` strip; dragged pieces slide over it (`PieceX`).
using System;
using MinikUsta.Core;

namespace MinikUsta.Presentation.Layout
{
    /// <summary>Cell ↔ design px of one level geometry (web `BoardGeometry`); every y already shifted for the design height.</summary>
    public sealed class BoardGeometry
    {
        public readonly BoardGeo Geo;
        public readonly double CellPx;
        /// <summary>Cell scale k = CellPx / 120 (TECH §2R.1): wall strip and hit slop scale with it.</summary>
        public readonly double K;
        public readonly double WallW;
        /// <summary>Left edge of yard column 0, of the wall strip, of the first site column; right edge of the last site column.</summary>
        public readonly double YardX;
        public readonly double WallX;
        public readonly double BuildX;
        public readonly double SiteRight;
        /// <summary>Top of the crane area, of the board (row h − 1), of the yard (row hy − 1); the board's bottom edge.</summary>
        public readonly double CraneTopY;
        public readonly double BoardTopY;
        public readonly double YardTopY;
        public readonly double BoardBottomY;

        internal BoardGeometry(BoardGeo geo, double c, double k, double wallW, double yardX, double boardBottomY)
        {
            Geo = geo;
            CellPx = c;
            K = k;
            WallW = wallW;
            YardX = yardX;
            WallX = yardX + geo.Wy * c;
            BuildX = WallX + wallW;
            SiteRight = BuildX + geo.Ws * c;
            BoardBottomY = boardBottomY;
            BoardTopY = boardBottomY - geo.H * c;
            YardTopY = boardBottomY - geo.Hy * c;
            CraneTopY = BoardTopY - Geometry.CraneRows * c;
        }

        /// <summary>Left edge of global column x (x &lt; wy yard, x ≥ wy site).</summary>
        public double ColLeft(int x) => x < Geo.Wy ? YardX + x * CellPx : BuildX + (x - Geo.Wy) * CellPx;

        /// <summary>Top edge of global row y (0 … h−1 board, h … h+1 crane area).</summary>
        public double RowTop(int y) => BoardBottomY - (y + 1) * CellPx;

        public PxRect CellRect(int x, int y) => new PxRect(ColLeft(x), RowTop(y), CellPx, CellPx);

        private double WallStart(int w) => Geo.Wy - w;

        /// <summary>
        /// Screen x of the left edge of a piece box with CONTINUOUS anchor `ax` and width `w`: `yardX + ax·c + s·wallW`,
        /// `s = clamp((ax − (wy − w)) / w, 0, 1)` — 0 while the box is fully in the yard, 1 fully in the site; a straddling
        /// box slides symmetrically over the wall strip.
        /// </summary>
        public double PieceX(double ax, int w)
        {
            double s = Math.Min(1, Math.Max(0, (ax - WallStart(w)) / w));
            return YardX + ax * CellPx + s * WallW;
        }

        /// <summary>Screen y of the top edge of a piece box with continuous anchor row `ay` and height `h`.</summary>
        public double PieceY(double ay, int h) => BoardBottomY - (ay + h) * CellPx;

        public PxRect PieceRect(double ax, double ay, int w, int h) => new PxRect(PieceX(ax, w), PieceY(ay, h), w * CellPx, h * CellPx);

        /// <summary>Inverse of `PieceX` (monotonic, piecewise linear).</summary>
        public double AnchorXAt(double screenX, int w)
        {
            double start = WallStart(w);
            double x0 = YardX + start * CellPx; // s = 0 up to here
            double x1 = YardX + Geo.Wy * CellPx + WallW; // s = 1 from here (ax = wy)
            if (screenX <= x0) return (screenX - YardX) / CellPx;
            if (screenX >= x1) return (screenX - YardX - WallW) / CellPx;
            return start + (screenX - x0) / (CellPx + WallW / w);
        }

        /// <summary>Inverse of `PieceY`.</summary>
        public double AnchorYAt(double screenY, int h) => (BoardBottomY - screenY) / CellPx - h;

        /// <summary>
        /// The global cell under a design point; false over the wall strip or outside the cols × rows grid. Row y owns
        /// `[RowTop(y), RowTop(y) + c)` (the top-edge pixel belongs to the cell; `BoardBottomY` is below row 0).
        /// </summary>
        public bool CellAt(double px, double py, out int x, out int y)
        {
            x = -1;
            y = -1;
            int rows = Geo.Rows;
            int fromTop = (int)Math.Floor((py - (BoardBottomY - rows * CellPx)) / CellPx);
            int row = rows - 1 - fromTop;
            if (row < 0 || row >= rows) return false;
            if (px >= YardX && px < WallX)
            {
                x = (int)Math.Floor((px - YardX) / CellPx);
                y = row;
                return true;
            }
            if (px >= BuildX && px < SiteRight)
            {
                x = Geo.Wy + (int)Math.Floor((px - BuildX) / CellPx);
                y = row;
                return true;
            }
            return false;
        }

        /// <summary>Wall strip rows [0, height): the zero-width boundary drawn as this strip (R-03).</summary>
        public PxRect WallRect(int height) => new PxRect(WallX, BoardBottomY - height * CellPx, WallW, height * CellPx);

        /// <summary>Opening of a gap with rows [y, y + size) on the wall strip.</summary>
        public PxRect GapRect(int y, int size) => new PxRect(WallX, BoardBottomY - (y + size) * CellPx, WallW, size * CellPx);

        /// <summary>
        /// Continuous column of a design x (web dragMath `pointColumn`): the centre of column c is c + 0.5; the middle of the
        /// wall strip is the boundary x = wy.
        /// </summary>
        public double PointColumn(double screenX) => AnchorXAt(screenX - CellPx / 2, 1) + 0.5;

        /// <summary>Continuous row of a design y (web `pointRow`): the centre of row r is r + 0.5.</summary>
        public double PointRow(double screenY) => (BoardBottomY - screenY) / CellPx;

        /// <summary>Inverse of (`PointColumn`, `PointRow`): the design point of continuous cell coordinates (u, v).</summary>
        public void PointOf(double u, double v, out double screenX, out double screenY)
        {
            screenX = PieceX(u - 0.5, 1) + CellPx / 2;
            screenY = BoardBottomY - v * CellPx;
        }
    }

    /// <summary>The board group of one level (UX §5.8 rules 1–5; web `BoardLayout`).</summary>
    public sealed class BoardLayout
    {
        public readonly BoardGeometry Grid;
        /// <summary>Crane area: rows h, h+1 over the full board width (K-05).</summary>
        public readonly PxRect Crane;
        /// <summary>Rows 0 … h−1 over the full board width (yard + wall + site).</summary>
        public readonly PxRect Board;
        /// <summary>Yard rows 0 … hy−1 ("malzeme sandığı").</summary>
        public readonly PxRect Yard;
        /// <summary>Yard air rows hy … h−1 over the yard columns (empty when hy = h).</summary>
        public readonly PxRect YardAir;
        /// <summary>Site columns, rows 0 … h−1.</summary>
        public readonly PxRect Site;
        /// <summary>Status strip (streak beads, truck chip), part of the board group.</summary>
        public readonly PxRect Status;
        /// <summary>"Yapı tamam!" ribbon over wall + site, in the middle of the board; tilt and notch from the settings.</summary>
        public readonly PxRect SiteRibbon;
        public readonly double SiteRibbonTiltDeg;
        public readonly double SiteRibbonNotchPx;

        private BoardLayout(BoardGeometry grid, PxRect crane, PxRect board, PxRect yard, PxRect yardAir, PxRect site, PxRect status, PxRect ribbon, double tilt, double notch)
        {
            Grid = grid;
            Crane = crane;
            Board = board;
            Yard = yard;
            YardAir = yardAir;
            Site = site;
            Status = status;
            SiteRibbon = ribbon;
            SiteRibbonTiltDeg = tilt;
            SiteRibbonNotchPx = notch;
        }

        /// <summary>
        /// DL-2R-14 / TECH §2R.1 cell size of a level, chosen once and device independent:
        /// `c = max(120, min(cellMaxPx, ⌊960 / cols / 12⌋·12, ⌊1200 / (h + 2) / 12⌋·12))`, 960 = 1080 − 2·60 and
        /// 1200 = boardBottomY − top.groupBottomY − minGapTopPx. B1–B5, B7, B9 → 144; B6 → 132; B8, B10, default → 120.
        /// </summary>
        public static double AdaptiveCellPx(LayoutSettings s, BoardGeo geo)
        {
            double step = s.CellStepPx;
            double budgetW = s.DesignWidth - 2 * s.SideReservePx;
            double budgetH = s.BoardBottomY - s.TopGroupBottomY - s.MinGapTopPx;
            double byW = Math.Floor(budgetW / geo.Cols / step) * step;
            double byH = Math.Floor(budgetH / (geo.H + Geometry.CraneRows) / step) * step;
            return Math.Max(s.CellPx, Math.Min(s.CellMaxPx, Math.Min(byW, byH)));
        }

        /// <summary>UX §5.8 rules 1–5 for level geometry `geo` at design height `designH` (FIT 1920; EXPAND 1920 … 2400).</summary>
        public static BoardLayout Create(LayoutSettings s, BoardGeo geo, double designH)
        {
            double boardShift = (designH - s.DesignHeight) * s.ExpandShare;
            double c = AdaptiveCellPx(s, geo);
            double k = c / s.CellPx;
            double wallW = s.WallW * k;
            double boardW = geo.Cols * c + wallW;
            double yardX = Math.Round((s.DesignWidth - boardW) / 2, MidpointRounding.AwayFromZero);
            var g = new BoardGeometry(geo, c, k, wallW, yardX, s.BoardBottomY + boardShift);

            // UX §5.8 rule 5: width max(306, wall + site), over wall + site, vertically in the middle of the board
            double ribbonW = Math.Max(s.SiteRibbonW, wallW + geo.Ws * c);
            double ribbonX = g.WallX + Math.Max(0, (wallW + geo.Ws * c - ribbonW) / 2);
            return new BoardLayout(
                g,
                new PxRect(yardX, g.CraneTopY, boardW, g.BoardTopY - g.CraneTopY),
                new PxRect(yardX, g.BoardTopY, boardW, g.BoardBottomY - g.BoardTopY),
                new PxRect(yardX, g.YardTopY, geo.Wy * c, geo.Hy * c),
                new PxRect(yardX, g.BoardTopY, geo.Wy * c, g.YardTopY - g.BoardTopY),
                new PxRect(g.BuildX, g.BoardTopY, geo.Ws * c, geo.H * c),
                new PxRect(yardX, s.StatusY + boardShift, boardW, s.StatusH),
                new PxRect(ribbonX, g.BoardTopY + geo.H * c / 2 - s.SiteRibbonH / 2, ribbonW, s.SiteRibbonH),
                s.SiteRibbonTiltDeg,
                s.SiteRibbonNotchPx);
        }
    }

    /// <summary>
    /// The whole level screen for design height H and level geometry (web `createLayout`): HUD v3 top group, the board
    /// group, the bottom group (Tuna corner + booster bar) and the touch sizes. Immutable; a resize builds a new one.
    /// </summary>
    public sealed class ScreenLayout
    {
        public readonly double W;
        public readonly double H;
        /// <summary>(H − 1920) × expandShare; 0 in FIT.</summary>
        public readonly double BoardShift;
        public readonly BoardLayout Board;

        // top group (HUD v3, ART_REFERENCE §9.4): row 1 = level capsule + goal chips, row 2 = panorama well, moves plate, pause
        public readonly PxRect Pause;
        public readonly PxRect Panorama;
        public readonly PxRect Goals;
        public readonly PxRect Moves;
        public readonly PxRect LevelCapsule;
        public readonly double TopGroupBottomY;
        /// <summary>
        /// UX_UNITY R-01 (paket U4): the free band between the HUD and the crane area of an EXPAND screen (H &gt; 1920) that shows this level's
        /// structure rising. Bottom edge `SceneBandGapPx` above the crane area, height min(gap − 2 × 24, 420); zero height (W 0, H 0) at
        /// H ≤ 1920 or when under `SceneBandMinPx`. Spans the board width.
        /// </summary>
        public readonly PxRect SceneBand;

        // bottom group
        public readonly PxRect Character;
        /// <summary>`BoosterSlots` slots, left → right (hammer, crane, brush, undo).</summary>
        public readonly PxRect[] Boosters;
        /// <summary>Top edge of the bottom group: H − groupTopFromBottomPx.</summary>
        public readonly double BottomGroupTopY;

        // touch
        public readonly double MinTargetPx;
        /// <summary>touch.hitSlopPx × k (120 → 180 at k = 1).</summary>
        public readonly double HitSlopPx;

        public BoardGeometry Grid => Board.Grid;

        /// <summary>The HUD band: full width, from the top to `TopGroupBottomY` (UX §5.1 top group).</summary>
        public PxRect HudBand => new PxRect(0, 0, W, TopGroupBottomY);

        /// <summary>The booster bar: the union of the booster slots (UX §5.10).</summary>
        public PxRect BoosterBar
        {
            get
            {
                var first = Boosters[0];
                var last = Boosters[Boosters.Length - 1];
                return new PxRect(first.X, first.Y, last.Right - first.X, first.H);
            }
        }

        private ScreenLayout(LayoutSettings s, double h, BoardGeo geo)
        {
            W = s.DesignWidth;
            H = h;
            BoardShift = (h - s.DesignHeight) * s.ExpandShare;
            Board = BoardLayout.Create(s, geo, h);
            // UX_UNITY P0-5 (paket U2): the HUD group ends at 304 (the goal card 340 × 176 from y 112) whenever the crane area stays
            // minGapTopPx under it; a tall board at H 1920 shrinks it back towards the compact 264 (the card then 136 high). Row 2's
            // moves plate and pause tool sit vertically centred on the card; the card starts at the HUD margin so the gap to the
            // centred moves plate is ≥ 24 px.
            double room = Board.Grid.CraneTopY - s.MinGapTopPx;
            TopGroupBottomY = Math.Max(s.TopGroupBottomY, Math.Min(s.HudBottomY, room));
            double cardH = TopGroupBottomY - s.GoalCardBottomPadPx - s.HudRow2Y;
            double row2Y = s.HudRow2Y + (cardH - s.MovesH) / 2;
            Pause = new PxRect(s.ToolRightX + s.ToolPx - s.PauseToolPx, row2Y + (s.MovesH - s.PauseToolPx) / 2, s.PauseToolPx, s.PauseToolPx);
            Panorama = new PxRect(s.HudMarginX, s.HudRow2Y, s.GoalCardW, cardH);
            Goals = new PxRect(s.HudMarginX + s.LevelCapsuleW + s.HudRowGap, s.HudRow1Y, W - s.HudMarginX - s.HudRow1RightPadPx - s.LevelCapsuleW - s.HudRowGap, s.HudRow1H);
            Moves = new PxRect((W - s.MovesW) / 2, row2Y, s.MovesW, s.MovesH);
            LevelCapsule = new PxRect(s.HudMarginX, s.HudRow1Y, s.LevelCapsuleW, s.HudRow1H);
            double bandBottom = Board.Grid.CraneTopY - s.SceneBandGapPx;
            double bandH = h <= s.DesignHeight ? 0 : Math.Min(s.SceneBandMaxPx, bandBottom - TopGroupBottomY - s.SceneBandGapPx);
            SceneBand = bandH >= s.SceneBandMinPx ? new PxRect(Board.Board.X, bandBottom - bandH, Board.Board.W, bandH) : new PxRect(Board.Board.X, bandBottom, 0, 0);
            Character = new PxRect(s.CharacterX, h - s.CharacterBottomPx - s.CharacterH, s.CharacterW, s.CharacterH);
            Boosters = new PxRect[s.BoosterSlots];
            for (int i = 0; i < s.BoosterSlots; i++)
                Boosters[i] = new PxRect(s.BoosterX + i * (s.BoosterPx + s.BoosterGapPx), h - s.BoosterBottomPx - s.BoosterPx, s.BoosterPx, s.BoosterPx);
            BottomGroupTopY = h - s.GroupTopFromBottomPx;
            MinTargetPx = s.MinTargetPx;
            HitSlopPx = s.HitSlopPx * Board.Grid.K;
        }

        /// <summary>The layout for design height `designH` (FIT 1920; EXPAND 1920 … 2400) and level geometry `geo` (K-49).</summary>
        public static ScreenLayout Create(LayoutSettings s, double designH, BoardGeo? geo = null)
        {
            if (!(designH >= s.DesignHeight && designH <= s.ExpandMaxHeight))
                throw new ArgumentOutOfRangeException(nameof(designH), "ScreenLayout: H " + Num.Text(designH) + " outside [" + Num.Text(s.DesignHeight) + ", " + Num.Text(s.ExpandMaxHeight) + "]");
            return new ScreenLayout(s, designH, geo ?? Geometry.DefaultGeo);
        }

        /// <summary>UX §0.3 "Görsel + pay": every side shorter than `MinTargetPx` padded equally to it.</summary>
        public PxRect Hit(PxRect r)
        {
            double padX = Math.Max(0, (MinTargetPx - r.W) / 2);
            double padY = Math.Max(0, (MinTargetPx - r.H) / 2);
            return new PxRect(r.X - padX, r.Y - padY, r.W + 2 * padX, r.H + 2 * padY);
        }

        /// <summary>A block cell (or piece box) grown by `HitSlopPx` on every side (UX §0.1, TECH §10.3).</summary>
        public PxRect BlockHit(PxRect r) => r.Grow(HitSlopPx);

        /// <summary>
        /// The invisible board input zone (web DragController `onBoard`): crane area top … board bottom, over the board
        /// width, grown by the block hit slop.
        /// </summary>
        public bool OnBoard(double x, double y)
        {
            var crane = Board.Crane;
            var board = Board.Board;
            double slop = HitSlopPx;
            return x >= crane.X - slop && x < crane.Right + slop && y >= crane.Y - slop && y < board.Bottom + slop;
        }
    }
}
