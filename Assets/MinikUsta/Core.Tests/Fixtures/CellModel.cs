#nullable enable
// The cell-by-cell reference model of the drag graph (tests/core/movement.test.ts "edge model ≡ cell-by-cell model" and
// tests/core/geometry.test.ts "every geometry of G"): literal TECH §4.2 node rules over explicit cell lists (no masks, no
// shifts), the GDD K-07 table over cells and a plain multi-source BFS. The movement.test.ts model is this one with the
// default geometry. Not a test file.
using System;
using System.Collections.Generic;
using System.Linq;

namespace MinikUsta.Core.Tests.Fixtures
{
    public sealed class ModelGap
    {
        public readonly int Lo;
        public readonly int Hi;
        public readonly bool Open;

        public ModelGap(int lo, int hi, bool open)
        {
            Lo = lo;
            Hi = hi;
            Open = open;
        }
    }

    public sealed class CellModel
    {
        public readonly BoardGeo G;
        public readonly ShapeDef Shape;
        public readonly int Height;
        public readonly ModelGap[] Gaps;
        public readonly Func<int, int, bool> Blocked;
        /// <summary>Start node keys: the block may always return to its start cells (K-07 row 1).</summary>
        public readonly HashSet<string> Starts = new HashSet<string>();

        public CellModel(BoardGeo g, ShapeDef shape, int height, ModelGap[] gaps, Func<int, int, bool> blocked)
        {
            G = g;
            Shape = shape;
            Height = height;
            Gaps = gaps;
            Blocked = blocked;
        }

        public static string Key(DragNode n) => n.Mode + ":" + n.Ix + "," + n.Iy;
        private static DragNode F(int ix, int iy) => new DragNode(ix, iy, Movement.Free);
        private static DragNode R(int gap, int ix, int iy) => new DragNode(ix, iy, gap + 1);

        public (int x, int y)[] Cells(DragNode n) => Shape.Cells.Select(c => (n.Ix + c.X, n.Iy + c.Y)).ToArray();

        /// <summary>Literal GDD K-04, K-05, K-08 (Faz 2R cargo), K-11, K-12, K-13 node rules over explicit cells, any geometry.</summary>
        public bool Valid(DragNode n)
        {
            if (Starts.Contains(Key(n))) return true;
            var cells = Cells(n);
            foreach (var (x, y) in cells)
            {
                if (x < 0 || x >= G.Wy + G.Ws || y < 0 || y >= G.H + 2) return false;
                if (Movement.IsCargoShape(Shape) && (x > G.Wy - 1 || y > G.Hy - 1)) return false;
                if (Blocked(x, y)) return false;
            }
            var site = cells.Where(c => c.x >= G.Wy).ToArray();
            if (n.Mode == Movement.Free)
            {
                for (int y = 0; y < G.H + 2; y++)
                {
                    var row = cells.Where(c => c.y == y).ToArray();
                    if (row.Any(c => c.x <= G.Wy - 1) && row.Any(c => c.x >= G.Wy) && y < Height) return false;
                }
                for (int col = G.Wy; col < G.Wy + G.Ws; col++)
                {
                    var ys = site.Where(c => c.x == col).Select(c => c.y).ToArray();
                    if (ys.Length == 0) continue;
                    for (int y = ys.Min(); y < G.H + 2; y++) if (Blocked(col, y)) return false;
                }
                return true;
            }
            int gi = n.Mode - 1;
            if (gi >= Gaps.Length || !Gaps[gi].Open || site.Length == 0) return false;
            return cells.All(c => c.y >= Gaps[gi].Lo && c.y < Gaps[gi].Hi);
        }

        public bool RowOpen(int y, int mode)
        {
            if (mode == Movement.Free) return y >= Height;
            int gi = mode - 1;
            return gi < Gaps.Length && Gaps[gi].Open && y >= Gaps[gi].Lo && y < Gaps[gi].Hi;
        }

        public bool Step(DragNode from, DragNode to)
        {
            if (from.Iy != to.Iy) return true;
            int dx = to.Ix - from.Ix;
            int mode = from.Mode != Movement.Free ? from.Mode : to.Mode;
            int last = G.Wy - 1;
            foreach (var (x, y) in Cells(from))
            {
                if ((x <= last) != (x + dx <= last) && !RowOpen(y, mode)) return false;
            }
            return true;
        }

        public List<DragNode> Neighbours(DragNode n)
        {
            var o = new List<DragNode>();
            void Add(DragNode t)
            {
                if (Valid(t) && Step(n, t)) o.Add(t);
            }
            bool FullyYard(int ix) => ix + Shape.W - 1 <= G.Wy - 1;
            if (n.Mode == Movement.Free)
            {
                Add(F(n.Ix - 1, n.Iy));
                Add(F(n.Ix + 1, n.Iy));
                if (FullyYard(n.Ix)) for (int gi = 0; gi < Gaps.Length; gi++) Add(R(gi, n.Ix + 1, n.Iy));
                Add(F(n.Ix, n.Iy - 1));
                Add(F(n.Ix, n.Iy + 1));
            }
            else
            {
                Add(FullyYard(n.Ix - 1) ? F(n.Ix - 1, n.Iy) : new DragNode(n.Ix - 1, n.Iy, n.Mode));
                Add(new DragNode(n.Ix + 1, n.Iy, n.Mode));
            }
            return o;
        }

        /// <summary>Multi-source BFS from the start nodes; returns node key → distance.</summary>
        public Dictionary<string, int> Bfs(IList<DragNode> starts)
        {
            var dist = new Dictionary<string, int>();
            var queue = new List<DragNode>();
            foreach (var st in starts)
            {
                dist[Key(st)] = 0;
                queue.Add(st);
            }
            for (int i = 0; i < queue.Count; i++)
            {
                var u = queue[i];
                int du = dist[Key(u)];
                foreach (var v in Neighbours(u))
                {
                    if (dist.ContainsKey(Key(v))) continue;
                    dist[Key(v)] = du + 1;
                    queue.Add(v);
                }
            }
            return dist;
        }

        /// <summary>Literal GDD K-07 table over cells (row 5 is not exercised here: the site is open). Faz 2R: row 3 = all cells x ≤ Wy − 1 and one cell y ≥ Hy.</summary>
        public string Classify(IList<DragNode> starts, DragNode n)
        {
            var cells = Cells(n);
            if (starts.Any(st => Key(st) == Key(n))) return CancelReasons.SameSpot;
            bool yardSide = cells.All(c => c.x <= G.Wy - 1);
            bool siteSide = cells.All(c => c.x >= G.Wy);
            if (n.Mode == Movement.Free && yardSide) return cells.All(c => c.y <= G.Hy - 1) ? DropKinds.Yard : CancelReasons.CraneOverYard;
            if (!yardSide && !siteSide) return CancelReasons.Straddle;
            return n.Mode == Movement.Free ? DropKinds.SiteFree : DropKinds.SiteRail;
        }

        public static string DropName(DropClass d) => d.Kind == DropKinds.Cancel ? d.Reason! : d.Kind;

        /// <summary>`[...map.entries()].sort()` as comparable `key=dist` lines.</summary>
        public static string[] Entries(Dictionary<string, int> m) => m.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value).ToArray();
    }
}
