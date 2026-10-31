#nullable enable
// The static board of the level screen as a list of sprites (Faz 3 Q1; web BoardView.ts in placeholder form; UX_FLOWS
// §5.1, §5.8; ART §7): background, crane area, yard frame + floor, site blueprint + the plan of the visible segment,
// wall strip with its gaps, elevator platform. Pure: built from the state + the screen layout; the Unity view only turns
// every item into a pooled SpriteRenderer. Rebuilt after every package (`End`), on a segment change and on board events.
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
        public const int Crane = 5;
        public const int Frame = 10;
        public const int Floor = 20;
        public const int Platform = 25;
        public const int Plan = 30;
        public const int Wall = 40;
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
    }

    /// <summary>One sprite of the static board: a tile stretched over a design px rectangle.</summary>
    public readonly struct BoardItem
    {
        public readonly BoardItemKind Kind;
        public readonly Tile Tile;
        public readonly PxRect Rect;
        public readonly Rgba Color;
        public readonly int Order;
        /// <summary>9-sliced (rounded corners keep their size): `Tile.Panel`.</summary>
        public readonly bool Sliced;
        /// <summary>Board cell (x, y) of cell items; −1 otherwise.</summary>
        public readonly int X;
        public readonly int Y;

        public BoardItem(BoardItemKind kind, Tile tile, PxRect rect, Rgba color, int order, bool sliced = false, int x = -1, int y = -1)
        {
            Kind = kind;
            Tile = tile;
            Rect = rect;
            Color = color;
            Order = order;
            Sliced = sliced;
            X = x;
            Y = y;
        }

        public override string ToString() => Kind + " " + Rect + " " + Color;
    }

    public sealed class BoardScene
    {
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

        public static BoardScene Build(GameState s, ScreenLayout L)
        {
            var lvl = s.Lvl;
            var geo = lvl.Geo;
            var g = L.Grid;
            double c = g.CellPx;
            int seg = Grid.VisibleSegment(s);
            int elev = Hdr(s, H.Elev);
            var o = new BoardScene(seg, L);
            var items = o.Items;

            items.Add(new BoardItem(BoardItemKind.Background, Tile.Gradient, new PxRect(0, 0, L.W, L.H), Rgba.White, Order.Background));

            // crane area (K-05, rows h, h + 1): a faint sky band and its bottom line over the full board width
            var crane = L.Board.Crane;
            items.Add(new BoardItem(BoardItemKind.Crane, Tile.Plain, crane, Rgba.Hex(Palette.CraneSky, Palette.CraneSkyAlpha), Order.Crane));
            items.Add(new BoardItem(BoardItemKind.CraneLine, Tile.Plain, new PxRect(crane.X, crane.Bottom - 2, crane.W, 4),
                Rgba.Hex(Palette.CraneSky, Palette.CraneLine * 0.5f), Order.Crane + 1));

            // yard: wooden frame (layout.adaptive.yardFramePx 20) + floor cells (checker of yardFloor / yardFloorAlt)
            var yard = L.Board.Yard;
            double frame = 20 * g.K;
            items.Add(new BoardItem(BoardItemKind.YardFrame, Tile.Panel, yard.Grow(frame), Rgba.Hex(Palette.YardFrame), Order.Frame, sliced: true));
            for (int y = 0; y < geo.Hy; y++)
                for (int x = 0; x < geo.Wy; x++)
                    items.Add(new BoardItem(BoardItemKind.YardCell, Tile.Floor, g.CellRect(x, y), Rgba.Hex((x + y) % 2 == 0 ? Palette.YardFloor : Palette.YardFloorAlt),
                        Order.Floor, x: x, y: y));

            // site: blueprint panel under the plan rows of the visible segment (K-24: rows elev … elev + hs − 1)
            int planTop = System.Math.Min(geo.H, elev + geo.Hs);
            if (planTop > elev)
            {
                var top = g.CellRect(geo.SiteX, planTop - 1);
                var bottom = g.CellRect(geo.SiteX, elev);
                var panel = new PxRect(g.BuildX, top.Y, geo.Ws * c, bottom.Bottom - top.Y);
                items.Add(new BoardItem(BoardItemKind.SitePanel, Tile.Panel, panel.Grow(8 * g.K), Rgba.Hex(Palette.Blueprint), Order.Frame, sliced: true));
            }
            for (int y = 0; y < elev && y < geo.H; y++)
                for (int sx = 0; sx < geo.Ws; sx++)
                    items.Add(new BoardItem(BoardItemKind.Platform, Tile.Plain, g.CellRect(geo.SiteX + sx, y), Rgba.Hex(Palette.WallDark), Order.Platform,
                        x: geo.SiteX + sx, y: y));

            if (seg < lvl.Segments.Length)
            {
                var sg = lvl.Segments[seg];
                int revealed = RevealedMask(s, seg);
                for (int sy = 0; sy < geo.Hs; sy++)
                {
                    int y = sy + elev;
                    if (y >= geo.H) break;
                    for (int sx = 0; sx < geo.Ws; sx++)
                    {
                        int x = geo.SiteX + sx;
                        int pc = Grid.PlanColorAt(s, seg, sx, sy);
                        var rect = g.CellRect(x, y);
                        if (pc >= 0)
                        {
                            int local = sy * geo.Ws + sx;
                            bool hidden = ((sg.HiddenMask >> local) & 1) != 0 && ((revealed >> local) & 1) == 0;
                            if (hidden) items.Add(new BoardItem(BoardItemKind.HiddenCell, Tile.Hidden, rect, Rgba.Hex(Palette.PlanUnderlay), Order.Plan, x: x, y: y));
                            else items.Add(new BoardItem(BoardItemKind.PlanCell, Tile.Plan, rect, Palette.Block(pc).WithAlpha(Palette.PlanFill), Order.Plan, x: x, y: y));
                        }
                        else if (pc == Compiler.PlanDot)
                        {
                            items.Add(new BoardItem(BoardItemKind.HatchCell, Tile.Hatch, rect, Rgba.White.WithAlpha(Palette.PlanEmptyHatch), Order.Plan, x: x, y: y));
                        }
                    }
                }
            }

            // wall strip (R-03: a zero-width boundary drawn as wallW): one tile per row below the wall height, gaps per
            // Ascii.WallChar (`=` open, `x` closed, `#` wall)
            for (int y = 0; y < lvl.WallHeight && y < geo.H; y++)
            {
                var rect = new PxRect(g.WallX, g.RowTop(y), g.WallW, c);
                string ch = Ascii.WallChar(s, y);
                if (ch == "=") items.Add(new BoardItem(BoardItemKind.Gap, Tile.Gap, rect, Rgba.Hex(Palette.Rail), Order.Wall, x: -1, y: y));
                else if (ch == "x") items.Add(new BoardItem(BoardItemKind.ClosedGap, Tile.Plain, rect, Rgba.Hex(Palette.ShutterSlat), Order.Wall, x: -1, y: y));
                else items.Add(new BoardItem(BoardItemKind.Wall, Tile.Plain, rect, Rgba.Hex(Palette.Wall), Order.Wall, x: -1, y: y));
            }
            return o;
        }
    }
}
