#nullable enable
// The static board of the level screen as a list of sprites (Faz 3 Q1; web BoardView.ts; UX_FLOWS §5.1, §5.8; ART §7).
// Paket V1: every item also names its v4 atlas frame (docs/ART_UNITY.md §4.2–§4.3): tray, crane band and hook, yard and
// site floors, plan ghosts with the K-34 build front, wall strip with its cap, W1 gaps with edges and rails, scaffold and
// ceiling beam. Pure: built from the state + the screen layout; the Unity view turns every item into a pooled
// SpriteRenderer. Items with a `Frame` are drawn from the atlas; `Tile` / `Color` are the procedural placeholder used only
// when the atlas is missing (ProceduralSprites, emergency fallback). `ArtOnly` items exist only with the atlas,
// `Frame == null` items only in the placeholder. Rebuilt after every package (`End`), on a segment change and on board events.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Layout;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Game
{
    /// <summary>Draw order bands (sprite sorting order; blocks start at `PieceViews.BaseOrder`).</summary>
    public static class Order
    {
        public const int Background = 0;
        /// <summary>V1: the board tray (9-slice frame around crane area + board).</summary>
        public const int Tray = 3;
        public const int Crane = 5;
        public const int Frame = 10;
        public const int Floor = 20;
        public const int Platform = 25;
        public const int Plan = 30;
        public const int Wall = 40;
        /// <summary>V1: scaffold pipes, beam, clamps, rails (over the wall strip, under the blocks).</summary>
        public const int Scaffold = 45;
        public const int Ghost = 60;
        public const int Pieces = 100;
        /// <summary>A lifted / flying block (above every resting block).</summary>
        public const int Raised = 400;
        public const int GhostBadge = 480;
        public const int Flyer = 500;
    }

    public enum BoardItemKind
    {
        Background,
        Crane,
        CraneLine,
        YardFrame,
        YardCell,
        SitePanel,
        PlanCell,
        HatchCell,
        HiddenCell,
        Platform,
        Wall,
        Gap,
        ClosedGap,
        // V1 art-only items
        Tray,
        CraneHook,
        YardAir,
        SiteFloor,
        BuildFront,
        WallCap,
        GapEdge,
        Rail,
        Scaffold,
        Beam,
        Clamp,
    }

    /// <summary>One sprite of the static board: a frame (or placeholder tile) drawn into a design px rectangle.</summary>
    public readonly struct BoardItem
    {
        public readonly BoardItemKind Kind;
        /// <summary>Placeholder tile (procedural atlas) and its colour.</summary>
        public readonly Tile Tile;
        public readonly PxRect Rect;
        public readonly Rgba Color;
        public readonly int Order;
        /// <summary>9-sliced (rounded corners keep their size): `Tile.Panel` / a frame with borders.</summary>
        public readonly bool Sliced;
        /// <summary>Board cell (x, y) of cell items; −1 otherwise.</summary>
        public readonly int X;
        public readonly int Y;
        /// <summary>V1: the atlas frame drawn into `Rect`; null = placeholder only.</summary>
        public readonly string? Frame;
        /// <summary>V1: tint of the atlas frame (white = as drawn).</summary>
        public readonly Rgba Tint;
        /// <summary>V1: drawn only with the atlas (no placeholder look).</summary>
        public readonly bool ArtOnly;
        /// <summary>V1: mirrored vertically (the bottom edge of a gap).</summary>
        public readonly bool FlipY;

        public BoardItem(BoardItemKind kind, Tile tile, PxRect rect, Rgba color, int order, bool sliced = false, int x = -1, int y = -1,
            string? frame = null, Rgba? tint = null, bool artOnly = false, bool flipY = false)
        {
            Kind = kind;
            Tile = tile;
            Rect = rect;
            Color = color;
            Order = order;
            Sliced = sliced;
            X = x;
            Y = y;
            Frame = frame;
            Tint = tint ?? Rgba.White;
            ArtOnly = artOnly;
            FlipY = flipY;
        }

        /// <summary>An item that exists only with the atlas.</summary>
        public static BoardItem Art(BoardItemKind kind, string frame, PxRect rect, int order, Rgba? tint = null, bool sliced = false, int x = -1, int y = -1, bool flipY = false) =>
            new BoardItem(kind, Tile.Plain, rect, Rgba.White, order, sliced, x, y, frame, tint, true, flipY);

        public override string ToString() => Kind + " " + (Frame ?? Tile.ToString()) + " " + Rect + " " + Color;
    }

    public sealed class BoardScene
    {
        /// <summary>ART_UNITY §4.3: the tray is the crane area ∪ board grown 48 px (left, top, right) and 64 px (bottom).</summary>
        public const double TrayGrow = 48;
        public const double TrayGrowBottom = 64;
        /// <summary>White α 0,08 over the yard air and the open air above the wall (ART_UNITY §4.3).</summary>
        public const float AirAlpha = 0.08f;

        public readonly List<BoardItem> Items = new List<BoardItem>();
        /// <summary>Visible segment drawn (`Grid.VisibleSegment`); ≥ segment count when every segment is done.</summary>
        public readonly int Segment;
        public readonly ScreenLayout Layout;

        private BoardScene(int segment, ScreenLayout layout)
        {
            Segment = segment;
            Layout = layout;
        }

        public int Count(BoardItemKind kind)
        {
            int n = 0;
            foreach (var i in Items) if (i.Kind == kind) n++;
            return n;
        }

        /// <summary>Every atlas frame the scene uses (tests: each exists in atlas.json).</summary>
        public IEnumerable<string> FrameNames()
        {
            foreach (var i in Items) if (i.Frame != null) yield return i.Frame;
        }

        /// <summary>The tray rectangle (ART_UNITY §4.3), clamped to the canvas and below the HUD group.</summary>
        public static PxRect TrayRect(ScreenLayout L)
        {
            var crane = L.Board.Crane;
            var board = L.Board.Board;
            double side = Math.Min(TrayGrow, Math.Max(0, crane.X));
            double top = Math.Min(TrayGrow, Math.Max(0, crane.Y - L.TopGroupBottomY));
            return new PxRect(crane.X - side, crane.Y - top, crane.W + 2 * side, board.Bottom - crane.Y + top + TrayGrowBottom);
        }

        /// <summary>K-34 build front of segment `seg`: per site column the lowest plan cell not yet filled (−1 = none).</summary>
        public static int FrontRow(GameState s, int seg, int sx)
        {
            var lvl = s.Lvl;
            if (seg < 0 || seg >= lvl.Segments.Length) return -1;
            var plan = lvl.Segments[seg];
            int need = sx < plan.PlanMask.Length ? plan.PlanMask[sx] : 0;
            int filled = FilledMask(s, seg, sx);
            for (int sy = 0; sy < lvl.Geo.Hs; sy++)
            {
                int bit = 1 << sy;
                if ((need & bit) != 0 && (filled & bit) == 0) return sy;
            }
            return -1;
        }

        public static BoardScene Build(GameState s, ScreenLayout L)
        {
            var lvl = s.Lvl;
            var geo = lvl.Geo;
            var g = L.Grid;
            double c = g.CellPx;
            double k = g.K;
            int seg = Grid.VisibleSegment(s);
            int elev = Hdr(s, H.Elev);
            var o = new BoardScene(seg, L);
            var items = o.Items;

            items.Add(new BoardItem(BoardItemKind.Background, Tile.Gradient, new PxRect(0, 0, L.W, L.H), Rgba.White, Order.Background, frame: Frames.BgLevel));
            items.Add(BoardItem.Art(BoardItemKind.Tray, Frames.Tray, TrayRect(L), Order.Tray, sliced: true));

            // crane area (K-05, rows h, h + 1): a faint sky band and its bottom line over the full board width; v4: crane_band
            // per column (2 c high; the wall strip gets a narrowed tile) with the dashed boundary at its bottom
            var crane = L.Board.Crane;
            items.Add(new BoardItem(BoardItemKind.Crane, Tile.Plain, crane, Rgba.Hex(Palette.CraneSky, Palette.CraneSkyAlpha), Order.Crane));
            items.Add(new BoardItem(BoardItemKind.CraneLine, Tile.Plain, new PxRect(crane.X, crane.Bottom - 2, crane.W, 4),
                Rgba.Hex(Palette.CraneSky, Palette.CraneLine * 0.5f), Order.Crane + 1));
            for (int x = 0; x < geo.Cols; x++)
            {
                items.Add(BoardItem.Art(BoardItemKind.Crane, Frames.CraneBand, new PxRect(g.ColLeft(x), crane.Y, c, crane.H), Order.Crane));
                if (x == geo.Wy - 1) items.Add(BoardItem.Art(BoardItemKind.Crane, Frames.CraneBand, new PxRect(g.WallX, crane.Y, g.WallW, crane.H), Order.Crane));
            }
            {
                double hw = 96 * 0.9 * k, hh = 160 * 0.9 * k;
                double cx = g.YardX + 0.5 * c;
                double hy = Math.Max(L.TopGroupBottomY, crane.Y - 30 * k); // the hook hangs from the tray's top edge, never into the HUD
                items.Add(BoardItem.Art(BoardItemKind.CraneHook, Frames.CraneHook, new PxRect(cx - hw / 2, hy, hw, hh), Order.Crane + 2));
            }

            // yard: wooden frame (placeholder) + floor cells (checker); air rows over the yard
            var yard = L.Board.Yard;
            double frame = 20 * g.K;
            items.Add(new BoardItem(BoardItemKind.YardFrame, Tile.Panel, yard.Grow(frame), Rgba.Hex(Palette.YardFrame), Order.Frame, sliced: true));
            for (int y = 0; y < geo.Hy; y++)
                for (int x = 0; x < geo.Wy; x++)
                    items.Add(new BoardItem(BoardItemKind.YardCell, Tile.Floor, g.CellRect(x, y), Rgba.Hex((x + y) % 2 == 0 ? Palette.YardFloor : Palette.YardFloorAlt),
                        Order.Floor, x: x, y: y, frame: (x + y) % 2 == 0 ? Frames.FloorYardA : Frames.FloorYardB));
            if (L.Board.YardAir.H > 0)
                items.Add(BoardItem.Art(BoardItemKind.YardAir, Frames.White, L.Board.YardAir, Order.Floor, Rgba.White.WithAlpha(AirAlpha)));
            if (lvl.WallHeight < geo.H)
            {
                var wallAir = new PxRect(g.WallX, g.BoardTopY, g.WallW, (geo.H - lvl.WallHeight) * c);
                items.Add(BoardItem.Art(BoardItemKind.YardAir, Frames.White, wallAir, Order.Floor, Rgba.White.WithAlpha(AirAlpha)));
            }

            // site: blueprint panel (placeholder) / blueprint floor per cell (v4) under the plan rows of the visible segment
            int planTop = Math.Min(geo.H, elev + geo.Hs);
            if (planTop > elev)
            {
                var top = g.CellRect(geo.SiteX, planTop - 1);
                var bottom = g.CellRect(geo.SiteX, elev);
                var panel = new PxRect(g.BuildX, top.Y, geo.Ws * c, bottom.Bottom - top.Y);
                items.Add(new BoardItem(BoardItemKind.SitePanel, Tile.Panel, panel.Grow(8 * g.K), Rgba.Hex(Palette.Blueprint), Order.Frame, sliced: true));
            }
            for (int y = elev; y < geo.H; y++)
                for (int sx = 0; sx < geo.Ws; sx++)
                    items.Add(BoardItem.Art(BoardItemKind.SiteFloor, Frames.FloorSite, g.CellRect(geo.SiteX + sx, y), Order.Floor, x: geo.SiteX + sx, y: y));
            for (int y = 0; y < elev && y < geo.H; y++)
                for (int sx = 0; sx < geo.Ws; sx++)
                    items.Add(new BoardItem(BoardItemKind.Platform, Tile.Plain, g.CellRect(geo.SiteX + sx, y), Rgba.Hex(Palette.WallDark), Order.Platform,
                        x: geo.SiteX + sx, y: y, frame: Frames.FloorSiteDeep));

            if (seg < lvl.Segments.Length)
            {
                var sg = lvl.Segments[seg];
                int revealed = RevealedMask(s, seg);
                for (int sx = 0; sx < geo.Ws; sx++)
                {
                    int front = FrontRow(s, seg, sx);
                    for (int sy = 0; sy < geo.Hs; sy++)
                    {
                        int y = sy + elev;
                        if (y >= geo.H) break;
                        int x = geo.SiteX + sx;
                        int pc = Grid.PlanColorAt(s, seg, sx, sy);
                        var rect = g.CellRect(x, y);
                        if (pc >= 0)
                        {
                            int local = sy * geo.Ws + sx;
                            bool hidden = ((sg.HiddenMask >> local) & 1) != 0 && ((revealed >> local) & 1) == 0;
                            bool isFront = sy == front;
                            if (hidden) items.Add(new BoardItem(BoardItemKind.HiddenCell, Tile.Hidden, rect, Rgba.Hex(Palette.PlanUnderlay), Order.Plan, x: x, y: y, frame: Frames.GhostHidden));
                            else
                            {
                                items.Add(new BoardItem(BoardItemKind.PlanCell, Tile.Plan, rect, Palette.Block(pc).WithAlpha(Palette.PlanFill), Order.Plan, x: x, y: y,
                                    frame: Frames.Ghost(pc, isFront)));
                                if (isFront) items.Add(BoardItem.Art(BoardItemKind.BuildFront, Frames.GhostFront, rect, Order.Plan + 1, x: x, y: y));
                            }
                        }
                        else if (pc == Compiler.PlanDot)
                        {
                            items.Add(new BoardItem(BoardItemKind.HatchCell, Tile.Hatch, rect, Rgba.White.WithAlpha(Palette.PlanEmptyHatch), Order.Plan, x: x, y: y,
                                frame: Frames.FloorSiteDeep));
                        }
                    }
                }
            }

            // scaffold (ART §4): pipes on both sides of the site, a ceiling beam over the plan top, clamps at its ends
            {
                double top = g.RowTop(Math.Max(elev, planTop - 1));
                double pipeW = 16 * k;
                double bottomY = g.BoardBottomY;
                foreach (double px in new[] { g.BuildX - 8 * k, g.SiteRight - 8 * k })
                {
                    double y0 = top - 16 * k;
                    double h = bottomY - y0;
                    int n = Math.Max(1, (int)Math.Ceiling(h / c - 1e-9));
                    for (int i = 0; i < n; i++)
                    {
                        double ty = y0 + i * c;
                        double th = Math.Min(c, bottomY - ty);
                        items.Add(BoardItem.Art(BoardItemKind.Scaffold, Frames.ScaffoldPipe, new PxRect(px, ty, pipeW, th), Order.Scaffold));
                    }
                }
                double beamX = g.BuildX - 10 * k, beamW = geo.Ws * c + 20 * k, beamH = 16 * k;
                for (int sx = 0; sx < geo.Ws; sx++)
                {
                    double bx = beamX + sx * beamW / geo.Ws;
                    items.Add(BoardItem.Art(BoardItemKind.Beam, Frames.Beam, new PxRect(bx, top - 10 * k, beamW / geo.Ws, beamH), Order.Scaffold + 1));
                }
                double cw = 28 * k, ch = 24 * k;
                foreach (double cx in new[] { g.BuildX, g.SiteRight })
                    items.Add(BoardItem.Art(BoardItemKind.Clamp, Frames.ScaffoldClamp, new PxRect(cx - cw / 2, top - 14 * k, cw, ch), Order.Scaffold + 2));
            }

            // wall strip (R-03: a zero-width boundary drawn as wallW): one tile per row below the wall height, gaps per
            // Ascii.WallChar (`=` open, `x` closed, `#` wall); v4: wall_seg / gap_seg, the cap on the wall top, gap edges and rails
            for (int y = 0; y < lvl.WallHeight && y < geo.H; y++)
            {
                var rect = new PxRect(g.WallX, g.RowTop(y), g.WallW, c);
                string ch = Ascii.WallChar(s, y);
                if (ch == "=")
                {
                    items.Add(new BoardItem(BoardItemKind.Gap, Tile.Gap, rect, Rgba.Hex(Palette.Rail), Order.Wall, x: -1, y: y, frame: Frames.GapSeg));
                    bool topEdge = y + 1 >= lvl.WallHeight || Ascii.WallChar(s, y + 1) != "=";
                    bool bottomEdge = y == 0 || Ascii.WallChar(s, y - 1) != "=";
                    double ew = g.WallW, eh = 22 * k;
                    if (topEdge)
                    {
                        items.Add(BoardItem.Art(BoardItemKind.GapEdge, Frames.GapEdge, new PxRect(g.WallX, rect.Y - eh / 2, ew, eh), Order.Wall + 2, y: y));
                        for (int sx = 0; sx < geo.Ws; sx++)
                            items.Add(BoardItem.Art(BoardItemKind.Rail, Frames.RailSeg, new PxRect(g.ColLeft(geo.SiteX + sx), rect.Y - 8 * k, c, 16 * k), Order.Scaffold, y: y));
                    }
                    if (bottomEdge)
                    {
                        items.Add(BoardItem.Art(BoardItemKind.GapEdge, Frames.GapEdge, new PxRect(g.WallX, rect.Bottom - eh / 2, ew, eh), Order.Wall + 2, y: y, flipY: true));
                        if (y > 0)
                            for (int sx = 0; sx < geo.Ws; sx++)
                                items.Add(BoardItem.Art(BoardItemKind.Rail, Frames.RailSeg, new PxRect(g.ColLeft(geo.SiteX + sx), rect.Bottom - 8 * k, c, 16 * k), Order.Scaffold, y: y));
                    }
                }
                else if (ch == "x")
                    items.Add(new BoardItem(BoardItemKind.ClosedGap, Tile.Plain, rect, Rgba.Hex(Palette.ShutterSlat), Order.Wall, x: -1, y: y, frame: Frames.GapSeg,
                        tint: Rgba.Hex(Palette.ShutterSlat)));
                else items.Add(new BoardItem(BoardItemKind.Wall, Tile.Plain, rect, Rgba.Hex(Palette.Wall), Order.Wall, x: -1, y: y, frame: Frames.WallSeg));
            }
            if (lvl.WallHeight > 0)
            {
                double cw = 64 * k, chh = 36 * k;
                double wallTop = g.RowTop(Math.Min(lvl.WallHeight, geo.H) - 1);
                items.Add(BoardItem.Art(BoardItemKind.WallCap, Frames.WallCap, new PxRect(g.WallX + g.WallW / 2 - cw / 2, wallTop + 6 * k - chh, cw, chh), Order.Wall + 1));
            }
            return o;
        }
    }
}
