#nullable enable
// The tutorial glove (paket C1; web tutorial/glove.ts; UX_FLOWS §13.1 "Eldiven", GDD §14 "`hand.path` anlamı"): when it may play and the path
// its fingertip follows on screen. Pure: core reads + layout geometry, no engine.
//
// Play condition (checked when the step becomes active and after every move end — one BFS, `Movement.TryBeginDrag`): a `drag` / `hold` glove
// plays only when (a) `path[0]` is a cell of one of the step's highlighted blocks, (b) that block can be held now (K-09) and (c) the path walks
// through the block's reachable set (K-08) and ends on a release that does not cancel (K-07 rows 2, 6, 7; `hold` over the site columns).
// Otherwise there is no glove: the glow and the bubble stay. A `tap` glove needs only its target on screen.
//
// Fingertip path: `path[0]` is the touched cell's centre; every later point is the held cell's centre plus the drag's finger offset
// (`drag.fingerOffsetCells` rows under the block, K-08), so the glove moves the way a finger moves the block. Corners are rounded with
// `CornerRadiusPx` (UX §13.1: 24 px).
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Tutorial
{
    public static class TutorialGlove
    {
        /// <summary>A unit step of the drag graph to anchor (ax, ay): same mode first, then FREE, then a rail; else any reachable node there.</summary>
        private static bool StepTo(DragSession session, DragNode node, int ax, int ay, out DragNode next)
        {
            DragNode? same = null, free = null, any = null;
            foreach (var n in session.Neighbours(node))
            {
                if (n.Ix != ax || n.Iy != ay) continue;
                any ??= n;
                if (same == null && n.Mode == node.Mode) same = n;
                if (free == null && n.Mode == DragMode.Free) free = n;
            }
            if (same != null) { next = same.Value; return true; }
            if (free != null) { next = free.Value; return true; }
            if (any != null) { next = any.Value; return true; }
            DragNode? reachFree = null, reachAny = null;
            foreach (var n in session.ReachableNodes())
            {
                if (n.Ix != ax || n.Iy != ay) continue;
                reachAny ??= n;
                if (reachFree == null && n.Mode == DragMode.Free) reachFree = n;
            }
            var pick = reachFree ?? reachAny;
            if (pick == null)
            {
                next = default;
                return false;
            }
            next = pick.Value;
            return true;
        }

        /// <summary>
        /// May the step's glove play on state `s`? `pieces` = the step's highlighted blocks, `rules` the level's drag rules (K-09 (c) chain / wet).
        /// A step without `hand` has no glove.
        /// </summary>
        public static bool Plays(GameState s, TutorialStepData step, int[] pieces, DragRules? rules = null)
        {
            var hand = step.Hand;
            if (hand == null) return false;
            if (hand.Kind == "tap") return true;
            var path = hand.Path;
            if (path == null || path.Length == 0) return false;
            int[] p0 = path[0];
            int pieceId = -1;
            foreach (int id in pieces)
            {
                foreach (var c in Grid.PieceBoardCells(s, id))
                    if (c.X == p0[0] && c.Y == p0[1]) { pieceId = id; break; }
                if (pieceId >= 0) break;
            }
            if (pieceId < 0) return false; // (a) the block left path[0]
            var attempt = Movement.TryBeginDrag(s, pieceId, rules);
            if (!attempt.Ok || attempt.Session == null) return false; // (b) K-09
            var session = attempt.Session;
            int ox = p0[0] - session.Start.Ix;
            int oy = p0[1] - session.Start.Iy;
            DragNode node = session.Start;
            for (int i = 1; i < path.Length; i++)
            {
                int[] a = path[i - 1];
                int[] b = path[i];
                if (a[0] != b[0] && a[1] != b[1]) return false;
                int dx = Math.Sign(b[0] - a[0]);
                int dy = Math.Sign(b[1] - a[1]);
                int px = a[0];
                int py = a[1];
                while (px != b[0] || py != b[1])
                {
                    px += dx;
                    py += dy;
                    if (!StepTo(session, node, px - ox, py - oy, out node)) return false; // (c) outside the reachable set
                }
            }
            var drop = session.Classify(node);
            if (drop.Kind == DropKinds.Cancel) return false; // (c) a cancel
            return hand.Kind != "hold" || drop.Kind != DropKinds.Yard;
        }

        /// <summary>The fingertip points of a `drag` / `hold` path on screen (design px; see the file comment).</summary>
        public static Vec2[] FingerPoints(BoardGeometry g, float fingerOffsetCells, int[][] path)
        {
            float offset = (float)(fingerOffsetCells * g.CellPx);
            var o = new Vec2[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                var r = g.CellRect(path[i][0], path[i][1]);
                o[i] = new Vec2((float)r.CenterX, (float)r.CenterY + (i == 0 ? 0f : offset));
            }
            return o;
        }

        /// <summary>
        /// The polyline `pts` with every inner corner replaced by a circular arc of radius ≤ `radius` (shortened when a leg is short), sampled
        /// every ≤ 15°. The first and last points stay.
        /// </summary>
        public static Vec2[] RoundedPath(Vec2[] pts, float radius)
        {
            if (pts.Length < 3) return (Vec2[])pts.Clone();
            var o = new List<Vec2> { pts[0] };
            for (int i = 1; i + 1 < pts.Length; i++)
            {
                Vec2 a = pts[i - 1], b = pts[i], c = pts[i + 1];
                double l1 = Math.Sqrt((b.X - a.X) * (double)(b.X - a.X) + (b.Y - a.Y) * (double)(b.Y - a.Y));
                double l2 = Math.Sqrt((c.X - b.X) * (double)(c.X - b.X) + (c.Y - b.Y) * (double)(c.Y - b.Y));
                if (l1 == 0 || l2 == 0)
                {
                    o.Add(b);
                    continue;
                }
                double u1x = (b.X - a.X) / l1, u1y = (b.Y - a.Y) / l1;
                double u2x = (c.X - b.X) / l2, u2y = (c.Y - b.Y) / l2;
                double turn = Math.Acos(Math.Max(-1, Math.Min(1, u1x * u2x + u1y * u2y)));
                if (turn < 1e-6)
                {
                    o.Add(b);
                    continue;
                }
                // tangent length d = r · tan(turn / 2), at most half of each leg
                double r = Math.Min(radius, Math.Min(l1, l2) / 2 / Math.Tan(turn / 2));
                double d = r * Math.Tan(turn / 2);
                double p1x = b.X - u1x * d, p1y = b.Y - u1y * d;
                double cross = u1x * u2y - u1y * u2x;
                double side = cross > 0 ? 1 : -1;
                double cx = p1x - u1y * r * side, cy = p1y + u1x * r * side;
                double a0 = Math.Atan2(p1y - cy, p1x - cx);
                int steps = Math.Max(2, (int)Math.Ceiling(turn / (Math.PI / 12)));
                for (int k = 0; k <= steps; k++)
                {
                    double ang = a0 + side * turn * ((double)k / steps);
                    o.Add(new Vec2((float)(cx + r * Math.Cos(ang)), (float)(cy + r * Math.Sin(ang))));
                }
            }
            o.Add(pts[pts.Length - 1]);
            return o.ToArray();
        }

        public static float Length(Vec2[] path)
        {
            double total = 0;
            for (int i = 1; i < path.Length; i++) total += Dist(path[i - 1], path[i]);
            return (float)total;
        }

        private static double Dist(Vec2 a, Vec2 b) => Math.Sqrt((b.X - a.X) * (double)(b.X - a.X) + (b.Y - a.Y) * (double)(b.Y - a.Y));

        /// <summary>Point at fraction `u` (0 … 1) of a polyline, by length.</summary>
        public static Vec2 PathAt(Vec2[] path, float u)
        {
            if (path.Length == 0) return Vec2.Zero;
            if (path.Length == 1) return path[0];
            double total = Length(path);
            double d = Math.Max(0, Math.Min(1, u)) * total;
            for (int i = 1; i < path.Length; i++)
            {
                double l = Dist(path[i - 1], path[i]);
                if (d <= l || i == path.Length - 1)
                {
                    double k = l == 0 ? 1 : Math.Min(1, d / l);
                    return new Vec2((float)(path[i - 1].X + (path[i].X - path[i - 1].X) * k), (float)(path[i - 1].Y + (path[i].Y - path[i - 1].Y) * k));
                }
                d -= l;
            }
            return path[path.Length - 1];
        }

        /// <summary>Trail dot centres along `path`, every `gap` px from its start (UX §13.1: 12 px dots, 22 px apart).</summary>
        public static Vec2[] TrailDots(Vec2[] path, float gap)
        {
            double total = Length(path);
            if (total == 0 || !(gap > 0)) return path.Length > 0 ? new[] { path[0] } : new Vec2[0];
            int n = (int)Math.Floor(total / gap);
            var o = new Vec2[n + 1];
            for (int k = 0; k <= n; k++) o[k] = PathAt(path, (float)(k * gap / total));
            return o;
        }
    }
}
