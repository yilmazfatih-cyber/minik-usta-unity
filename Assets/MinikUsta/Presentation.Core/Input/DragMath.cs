#nullable enable
// Drag geometry (port of web src/scenes/level/dragMath.ts; TECH_DESIGN §4.4, §10.5; UX_FLOWS §5.3; GDD K-07, K-08). Pure.
//
// Units: "anchor units" are the core's continuous cell coordinates (x 0 … cols over yard + site, the wall strip compressed
// to the zero-width boundary, R-03; y from the board bottom). Screen ↔ anchor goes through `BoardGeometry`
// (`AnchorXAt` / `PointColumn` …), so a dragged block slides over the wall strip.
// - Grab (UX §5.3 "Parmak ofseti"): the grabbed cell's centre ends `FingerOffsetCells` (1,2) above the finger. The block
//   does not jump at pick time: the offset between where the finger holds the cell and that target glides away over
//   `FingerOffsetMs` (TECH §4.4).
// - Target point p = finger − grab + (0, offset·t); the core's sticky follow takes it (`DragSession.Follow`, K-08).
// - Drawing: the block sits at `node + clamp(p − node)`; an axis may leave the node only toward a neighbour the drag graph
//   has an edge to (the block slides along obstacles and never shows inside a wall or a block).
using System;
using MinikUsta.Core;
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Input
{
    /// <summary>How a piece is held: the grabbed cell (relative to the anchor) and the pick-time displacement to glide away.</summary>
    public readonly struct Grab
    {
        public readonly int CellX;
        public readonly int CellY;
        /// <summary>Anchor − (finger − cell centre) at pick time, anchor units.</summary>
        public readonly double Dx0;
        public readonly double Dy0;

        public Grab(int cellX, int cellY, double dx0, double dy0)
        {
            CellX = cellX;
            CellY = cellY;
            Dx0 = dx0;
            Dy0 = dy0;
        }
    }

    /// <summary>Directions in which the drag graph has an edge from a node.</summary>
    public readonly struct Edges : IEquatable<Edges>
    {
        public readonly bool Left;
        public readonly bool Right;
        public readonly bool Down;
        public readonly bool Up;

        public Edges(bool left, bool right, bool down, bool up)
        {
            Left = left;
            Right = right;
            Down = down;
            Up = up;
        }

        public static readonly Edges None = new Edges(false, false, false, false);

        public bool Equals(Edges o) => Left == o.Left && Right == o.Right && Down == o.Down && Up == o.Up;
        public override bool Equals(object? obj) => obj is Edges e && Equals(e);
        public override int GetHashCode() => (Left ? 1 : 0) | (Right ? 2 : 0) | (Down ? 4 : 0) | (Up ? 8 : 0);
        public override string ToString() => "{left " + Left + ", right " + Right + ", down " + Down + ", up " + Up + "}";
    }

    public static class DragMath
    {
        /// <summary>UX §5.3 / TECH §10.5: a press becomes a drag once the finger moved `thresholdPx` (design px).</summary>
        public static bool ExceedsThreshold(double dx, double dy, double thresholdPx) => dx * dx + dy * dy >= thresholdPx * thresholdPx;

        /// <summary>The cell of the piece nearest to the finger (cells relative to the anchor), and the displacement that keeps the block where it is at t = 0.</summary>
        public static Grab GrabAt(BoardGeometry g, double fingerX, double fingerY, int ix, int iy, Cell[] cells)
        {
            double u = g.PointColumn(fingerX);
            double v = g.PointRow(fingerY);
            int bx = cells.Length > 0 ? cells[0].X : 0;
            int by = cells.Length > 0 ? cells[0].Y : 0;
            double bestD = double.PositiveInfinity;
            foreach (var c in cells)
            {
                double dx = ix + c.X + 0.5 - u;
                double dy = iy + c.Y + 0.5 - v;
                double d = dx * dx + dy * dy;
                if (d < bestD)
                {
                    bestD = d;
                    bx = c.X;
                    by = c.Y;
                }
            }
            return new Grab(bx, by, ix - (u - bx - 0.5), iy - (v - by - 0.5));
        }

        /// <summary>
        /// Target anchor point p for the core's sticky follow. `t` ∈ [0, 1] = finger-offset glide progress (elapsed /
        /// `FingerOffsetMs`); at t = 1 the grabbed cell's centre is `offsetCells` above the finger.
        /// </summary>
        public static void TargetAnchor(BoardGeometry g, Grab grab, double fingerX, double fingerY, double t, double offsetCells, out double px, out double py)
        {
            double k = Math.Min(1, Math.Max(0, t));
            double u = g.PointColumn(fingerX);
            double v = g.PointRow(fingerY);
            px = u - grab.CellX - 0.5 + grab.Dx0 * (1 - k);
            py = v - grab.CellY - 0.5 + grab.Dy0 * (1 - k) + offsetCells * k;
        }

        /// <summary>
        /// The finger point (design px) that puts the target anchor on (ax, ay) once the offset glide is over (t = 1): the
        /// inverse of `TargetAnchor` (bots, tests, the debug panel's "play the solver solution").
        /// </summary>
        public static void FingerFor(BoardGeometry g, Grab grab, double ax, double ay, double offsetCells, out double fingerX, out double fingerY) =>
            g.PointOf(ax + grab.CellX + 0.5, ay + grab.CellY + 0.5 - offsetCells, out fingerX, out fingerY);

        /// <summary>Edges of `node` from its graph neighbours (`DragSession.Neighbours`); a mode switch in place is no direction.</summary>
        public static Edges EdgesOf(DragNode node, DragNode[] neighbours)
        {
            bool left = false, right = false, down = false, up = false;
            foreach (var n in neighbours)
            {
                if (n.Iy == node.Iy && n.Ix < node.Ix) left = true;
                else if (n.Iy == node.Iy && n.Ix > node.Ix) right = true;
                else if (n.Ix == node.Ix && n.Iy < node.Iy) down = true;
                else if (n.Ix == node.Ix && n.Iy > node.Iy) up = true;
            }
            return new Edges(left, right, down, up);
        }

        /// <summary>TECH §4.4 "Yumuşak çizim": `node + clamp(p − node)`, per axis only toward an edge, at most one cell.</summary>
        public static void DrawAnchor(DragNode node, double px, double py, Edges edges, out double ax, out double ay)
        {
            double dx = px - node.Ix;
            double dy = py - node.Iy;
            double ox = dx > 0 ? (edges.Right ? Math.Min(dx, 1) : 0) : edges.Left ? Math.Max(dx, -1) : 0;
            double oy = dy > 0 ? (edges.Up ? Math.Min(dy, 1) : 0) : edges.Down ? Math.Max(dy, -1) : 0;
            ax = node.Ix + ox;
            ay = node.Iy + oy;
        }

        /// <summary>TECH §4.4: duration of following a BFS path of `steps` unit steps (12 ms per cell, at most 120 ms).</summary>
        public static double PathDurationMs(int steps, double msPerCell, double maxMs) => Math.Min(maxMs, Math.Max(0, steps) * msPerCell);

        /// <summary>
        /// Point on a polyline (the visual position at the jump, then the path nodes) at progress u ∈ [0, 1], each segment
        /// taking the same time: the view travels the path cell by cell and never cuts through a wall or a block.
        /// </summary>
        public static void PathPoint(double[] xs, double[] ys, int count, double u, out double ax, out double ay)
        {
            if (count <= 0)
            {
                ax = 0;
                ay = 0;
                return;
            }
            int n = count - 1;
            if (n <= 0 || u <= 0)
            {
                ax = xs[0];
                ay = ys[0];
                return;
            }
            if (u >= 1)
            {
                ax = xs[n];
                ay = ys[n];
                return;
            }
            double f = u * n;
            int i = (int)Math.Floor(f);
            double k = f - i;
            ax = xs[i] + (xs[i + 1] - xs[i]) * k;
            ay = ys[i] + (ys[i + 1] - ys[i]) * k;
        }
    }
}
