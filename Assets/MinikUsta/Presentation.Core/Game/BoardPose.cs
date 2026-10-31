#nullable enable
// Where a block is drawn when it rests (port of web src/scenes/level/pieceState.ts `statePose`; TECH §1.4: the view only
// reads the state). Yard blocks at their anchor; site blocks of the VISIBLE segment at (x, y + elevator offset) (K-22,
// K-24); everything else (queue, pending batches, gone, other segments) is not drawn on the board.
using MinikUsta.Core;
using MinikUsta.Presentation.Layout;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Game
{
    public static class BoardPose
    {
        /// <summary>The rest anchor (global board cells) of piece `id`; false when the piece is not on the visible board.</summary>
        public static bool RestAnchor(GameState s, int id, out int ax, out int ay)
        {
            ax = 0;
            ay = 0;
            int zone = PieceZone(s, id);
            if (zone == Zone.Yard)
            {
                ax = PieceX(s, id);
                ay = PieceY(s, id);
                return true;
            }
            if (zone == Zone.Site && PieceSeg(s, id) == Grid.VisibleSegment(s))
            {
                ax = PieceX(s, id);
                ay = PieceY(s, id) + Hdr(s, H.Elev);
                return true;
            }
            return false;
        }

        /// <summary>Centre (design px) of a piece box at continuous anchor (ax, ay): the `IPieceVisual.Position` convention.</summary>
        public static Vec2 Center(BoardGeometry g, double ax, double ay, ShapeDef shape)
        {
            var r = g.PieceRect(ax, ay, shape.W, shape.H);
            return new Vec2((float)r.CenterX, (float)r.CenterY);
        }

        /// <summary>Centre of a piece box at an event position (`At` = global board anchor; site rows include the elevator offset).</summary>
        public static Vec2 Center(BoardGeometry g, At at, ShapeDef shape) => Center(g, at.X, at.Y, shape);

        public static ShapeDef ShapeOf(GameState s, int id) => Shapes.ShapeByIndex(PieceShape(s, id));

        /// <summary>
        /// Neighbour mask of cell `c` of a shape (the `Atlas.Body` tile): bit 1 left, 2 right, 4 down, 8 up = another cell of
        /// the same piece on that side (cells are y-up, like `ShapeDef.Cells`).
        /// </summary>
        public static int NeighbourMask(ShapeDef shape, Cell c)
        {
            int m = 0;
            foreach (var o in shape.Cells)
            {
                if (o.Y == c.Y && o.X == c.X - 1) m |= 1;
                if (o.Y == c.Y && o.X == c.X + 1) m |= 2;
                if (o.X == c.X && o.Y == c.Y - 1) m |= 4;
                if (o.X == c.X && o.Y == c.Y + 1) m |= 8;
            }
            return m;
        }

        /// <summary>Offset (design px, y down) of cell `c`'s centre from the piece box centre.</summary>
        public static Vec2 CellOffset(ShapeDef shape, Cell c, double cellPx)
        {
            double x = (c.X + 0.5 - shape.W / 2.0) * cellPx;
            double y = -(c.Y + 0.5 - shape.H / 2.0) * cellPx;
            return new Vec2((float)x, (float)y);
        }
    }
}
