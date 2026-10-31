#nullable enable
// Pointer → drag → Move (port of the engine-free half of web src/scenes/level/DragController.ts; TECH §1.4, §4.1,
// §4.4–§4.6, §10.5; UX_FLOWS §5.3; GDD K-07, K-08, K-09). The Unity layer feeds design-px pointer positions (one pointer)
// and its animation clock; this class runs the K-09 gate + ONE BFS on press (`Movement.TryBeginDrag`), the sticky follow
// on every move (`DragSession.Follow`, K-08 — tie-breaks and hysteresis are the core's), and turns the release into the
// core `DragMove` the session commits. It never decides a rule: reachability, release class, landing and verdict come
// from the core. One-frame response (TECH §4.6): `Move` computes the drawn anchor synchronously, the caller positions the
// view in the same event.
using System;
using MinikUsta.Core;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Input
{
    public enum PressOutcome
    {
        /// <summary>No press: a second finger, no board state, outside the board zone, or no block under the finger.</summary>
        None,
        /// <summary>K-09 / K-14: the block under the finger cannot be picked (`Reason` = `PickFailures`).</summary>
        PickFailed,
        /// <summary>A press on a pickable block; it becomes a drag after the threshold or the hold time.</summary>
        Pressed,
    }

    public readonly struct PressResult
    {
        public readonly PressOutcome Outcome;
        /// <summary>The press is inside the board zone (the host fast-forwards board animations, R-12).</summary>
        public readonly bool OnBoard;
        public readonly int PieceId;
        /// <summary>`PickFailures` reason for `PickFailed` (immovable / rule → shake, JUICE #2; locked → no reaction, K-14).</summary>
        public readonly string? Reason;

        public PressResult(PressOutcome outcome, bool onBoard, int pieceId, string? reason)
        {
            Outcome = outcome;
            OnBoard = onBoard;
            PieceId = pieceId;
            Reason = reason;
        }
    }

    public enum ReleaseKind
    {
        /// <summary>Not the pressing pointer, or no press.</summary>
        None,
        /// <summary>Released under the threshold: a tap (no move, no cancel; UX §5.3 hop).</summary>
        Tap,
        /// <summary>The system cancelled the touch (incoming call, edge gesture): the block goes home, nothing is committed.</summary>
        Aborted,
        /// <summary>A drag release: `Move` is the core move to commit (a cancelling class still commits: the core answers `moveCancelled`).</summary>
        Drag,
    }

    public readonly struct ReleaseResult
    {
        public readonly ReleaseKind Kind;
        public readonly int PieceId;
        public readonly DragMove? Move;
        /// <summary>K-07 class of the release node (shadow / cancel preview); null unless `Drag`.</summary>
        public readonly DropClass? Drop;

        public ReleaseResult(ReleaseKind kind, int pieceId, DragMove? move, DropClass? drop)
        {
            Kind = kind;
            PieceId = pieceId;
            Move = move;
            Drop = drop;
        }
    }

    /// <summary>What a pointer update did (one reused instance: no allocation per pointer move, TECH §10.6).</summary>
    public sealed class DragFrame
    {
        /// <summary>This update lifted the block (JUICE #1: scale, hop, glow, shadow).</summary>
        public bool Lifted;
        /// <summary>The current node changed (or the drag just started): `Drop` = the core's K-07 class of the new node.</summary>
        public bool NodeChanged;
        public DropClass? Drop;
        public DragNode Node;
        /// <summary>Drawn anchor (continuous cells): place the view at `Grid.PieceRect(Ax, Ay, w, h)`.</summary>
        public double Ax;
        public double Ay;
        /// <summary>Follow target (anchor units); the gap to (Ax, Ay) is the sticky-follow separation (JUICE #4 tether).</summary>
        public double Px;
        public double Py;
        /// <summary>Finger (design px).</summary>
        public double FingerX;
        public double FingerY;
        /// <summary>Drag signals of this update (tutorial `overWall` / `gapPass`, K-05 `blockedByWallHeight`, K-44).</summary>
        public bool CrossedWall;
        public bool EnteredRail;
        public bool BlockedByWallHeight;
        public bool BlockedCargo;

        internal void Reset()
        {
            Lifted = false;
            NodeChanged = false;
            Drop = null;
            CrossedWall = false;
            EnteredRail = false;
            BlockedByWallHeight = false;
            BlockedCargo = false;
        }
    }

    public sealed class DragMapper
    {
        private readonly FeelSettings feel;
        private readonly DragFrame frame = new DragFrame();

        private bool active;
        private int pointerId;
        private DragSession? session;
        private Grab grab;
        private double downX;
        private double downY;
        private float downAt;
        private double lastX;
        private double lastY;
        private bool lifted;
        private float liftAt;
        private Edges edges;
        private double ax;
        private double ay;
        // BFS path being followed after a non-adjacent node change (TECH §4.4)
        private double[] pathX = new double[16];
        private double[] pathY = new double[16];
        private int pathCount;
        private float pathStart;
        private double pathMs;

        public DragMapper(FeelSettings feel)
        {
            this.feel = feel ?? throw new ArgumentNullException(nameof(feel));
        }

        /// <summary>A press or a drag is in progress.</summary>
        public bool Active => active;

        /// <summary>The block is lifted (a drag, not a press).</summary>
        public bool Lifted => active && lifted;

        /// <summary>The drag session of the press (null when idle).</summary>
        public DragSession? Session => active ? session : null;

        public int PieceId => active && session != null ? session.PieceId : -1;

        /// <summary>
        /// Pointer down at design px (x, y). Picks the block under the finger (`HitTest`) and asks the core whether it may be
        /// picked; nothing moves yet. `rules` = `GameSession.Hooks.Drag` (K-09 (c), RAIL `canPassGap`, K-07 row 5).
        /// </summary>
        public PressResult Down(GameState? s, ScreenLayout layout, DragRules? rules, int pointer, double x, double y, float now)
        {
            if (active) return new PressResult(PressOutcome.None, false, -1, null); // second finger (TECH §4.6)
            bool onBoard = layout.OnBoard(x, y);
            if (!onBoard || s == null) return new PressResult(PressOutcome.None, onBoard, -1, null);
            int id = HitTest.PieceAtPoint(s, layout.Grid, x, y, layout.HitSlopPx);
            if (id < 0) return new PressResult(PressOutcome.None, true, -1, null);
            var attempt = Movement.TryBeginDrag(s, id, rules);
            if (!attempt.Ok) return new PressResult(PressOutcome.PickFailed, true, id, attempt.Reason);
            var sess = attempt.Session!;
            active = true;
            pointerId = pointer;
            session = sess;
            grab = DragMath.GrabAt(layout.Grid, x, y, sess.Start.Ix, sess.Start.Iy, sess.Shape.Cells);
            downX = x;
            downY = y;
            downAt = now;
            lastX = x;
            lastY = y;
            lifted = false;
            liftAt = now;
            edges = Edges.None;
            pathCount = 0;
            ax = sess.Start.Ix;
            ay = sess.Start.Iy;
            return new PressResult(PressOutcome.Pressed, true, id, null);
        }

        /// <summary>
        /// Pointer move. Before the lift: lifts once the finger moved `DragStartThresholdPx`. Lifted: K-08 follow + drawing.
        /// Null when the update changed nothing visible (another pointer, no press, still under the threshold).
        /// </summary>
        public DragFrame? Move(int pointer, double x, double y, float now, ScreenLayout layout)
        {
            if (!active || pointer != pointerId) return null;
            lastX = x;
            lastY = y;
            if (!lifted)
            {
                if (!DragMath.ExceedsThreshold(x - downX, y - downY, feel.DragStartThresholdPx)) return null;
                return Lift(now, layout);
            }
            frame.Reset();
            Follow(now, layout);
            return frame;
        }

        /// <summary>
        /// Per frame: hold-to-lift, and while lifted the follow every frame (the finger-offset glide and the BFS path run
        /// without pointer events; an unmoved finger returns the core's cached `stay` result).
        /// </summary>
        public DragFrame? Update(float now, ScreenLayout layout)
        {
            if (!active) return null;
            if (!lifted)
            {
                if (now - downAt >= feel.DragHoldMs) return Lift(now, layout);
                return null;
            }
            frame.Reset();
            Follow(now, layout);
            return frame;
        }

        /// <summary>
        /// Pointer up. Under the threshold = a tap; lifted = the core move `DragMove(pieceId, current node)` (TECH §6.1).
        /// `canceled` = the system cancelled the touch: nothing is committed (`Aborted`).
        /// </summary>
        public ReleaseResult Up(int pointer, bool canceled = false)
        {
            if (!active || pointer != pointerId) return new ReleaseResult(ReleaseKind.None, -1, null, null);
            var sess = session!;
            bool wasLifted = lifted;
            Clear();
            if (canceled) return new ReleaseResult(wasLifted ? ReleaseKind.Aborted : ReleaseKind.None, sess.PieceId, null, null);
            if (!wasLifted) return new ReleaseResult(ReleaseKind.Tap, sess.PieceId, null, null);
            var node = sess.Current;
            return new ReleaseResult(ReleaseKind.Drag, sess.PieceId, new DragMove(sess.PieceId, node), sess.Classify(node));
        }

        /// <summary>Drops the press / drag without committing (app hidden, resize, level change).</summary>
        public void Abort() => Clear();

        private void Clear()
        {
            active = false;
            session = null;
            lifted = false;
            pathCount = 0;
        }

        private DragFrame Lift(float now, ScreenLayout layout)
        {
            var sess = session!;
            lifted = true;
            liftAt = now;
            edges = DragMath.EdgesOf(sess.Current, sess.Neighbours(sess.Current));
            frame.Reset();
            frame.Lifted = true;
            frame.NodeChanged = true;
            frame.Drop = sess.Classify();
            Follow(now, layout);
            frame.Lifted = true;
            return frame;
        }

        /// <summary>Sticky follow + drawing (web DragController `follow`).</summary>
        private void Follow(float now, ScreenLayout layout)
        {
            var sess = session!;
            var g = layout.Grid;
            double t = feel.FingerOffsetMs > 0 ? (now - liftAt) / feel.FingerOffsetMs : 1;
            DragMath.TargetAnchor(g, grab, lastX, lastY, t, feel.FingerOffsetCells, out double px, out double py);
            var res = sess.Follow(px, py);
            if (res.Changed)
            {
                edges = DragMath.EdgesOf(res.Node, sess.Neighbours(res.Node));
                if (res.Path.Length > 1)
                {
                    EnsurePath(res.Path.Length + 1);
                    pathX[0] = ax;
                    pathY[0] = ay;
                    for (int i = 0; i < res.Path.Length; i++)
                    {
                        pathX[i + 1] = res.Path[i].Ix;
                        pathY[i + 1] = res.Path[i].Iy;
                    }
                    pathCount = res.Path.Length + 1;
                    pathStart = now;
                    pathMs = DragMath.PathDurationMs(res.Path.Length, feel.PathMsPerCell, feel.PathMaxMs);
                }
                else pathCount = 0;
                frame.NodeChanged = true;
                frame.Drop = sess.Classify(res.Node);
            }
            frame.CrossedWall |= res.CrossedWall;
            frame.EnteredRail |= res.EnteredRail;
            frame.BlockedByWallHeight |= res.BlockedByWallHeight;
            frame.BlockedCargo |= res.BlockedCargo;

            var node = sess.Current;
            DragMath.DrawAnchor(node, px, py, edges, out double rx, out double ry);
            double x = rx, y = ry;
            if (pathCount > 0)
            {
                double u = pathMs > 0 ? (now - pathStart) / pathMs : 1;
                if (u >= 1) pathCount = 0;
                else
                {
                    // the last path point is the resting draw position, not the bare node (web: `[...pts.slice(0, -1), rest]`)
                    double keepX = pathX[pathCount - 1], keepY = pathY[pathCount - 1];
                    pathX[pathCount - 1] = rx;
                    pathY[pathCount - 1] = ry;
                    DragMath.PathPoint(pathX, pathY, pathCount, u, out x, out y);
                    pathX[pathCount - 1] = keepX;
                    pathY[pathCount - 1] = keepY;
                }
            }
            ax = x;
            ay = y;
            frame.Node = node;
            frame.Ax = x;
            frame.Ay = y;
            frame.Px = px;
            frame.Py = py;
            frame.FingerX = lastX;
            frame.FingerY = lastY;
        }

        private void EnsurePath(int n)
        {
            if (pathX.Length >= n) return;
            int size = Math.Max(n, pathX.Length * 2);
            pathX = new double[size];
            pathY = new double[size];
        }
    }
}
