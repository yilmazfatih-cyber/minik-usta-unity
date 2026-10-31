#nullable enable
// The block visuals of the board (web PieceLayer.ts): one pooled `IPieceVisual` per piece on the visible board, created
// and released from the state (`Sync`, TECH §1.4 "the view re-reads the state"), looked up by piece id by the step runner.
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Layout;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Game
{
    public sealed class PieceViews
    {
        private readonly ILevelView view;
        private readonly Dictionary<int, IPieceVisual> visuals = new Dictionary<int, IPieceVisual>();
        private readonly List<int> scratch = new List<int>();
        private double cellPx = 120;

        public PieceViews(ILevelView view)
        {
            this.view = view;
        }

        public int Count => visuals.Count;

        public IEnumerable<int> Ids => visuals.Keys;

        public IPieceVisual? Get(int id) => visuals.TryGetValue(id, out var v) ? v : null;

        /// <summary>The visual of `id`, acquired from the pool when missing (a truck delivery shows a pending block).</summary>
        public IPieceVisual Ensure(GameState s, int id)
        {
            if (visuals.TryGetValue(id, out var v)) return v;
            v = view.AcquirePiece(id, BoardPose.ShapeOf(s, id), PieceColor(s, id), cellPx);
            visuals[id] = v;
            return v;
        }

        public void Release(int id)
        {
            if (!visuals.TryGetValue(id, out var v)) return;
            visuals.Remove(id);
            view.ReleasePiece(v);
        }

        public void Clear()
        {
            scratch.Clear();
            scratch.AddRange(visuals.Keys);
            foreach (int id in scratch) Release(id);
        }

        /// <summary>
        /// Every block of the visible board at its rest pose (position, scale 1, no tint / flash / hop, opaque, not raised),
        /// with its colour and K-09 dim look; visuals of blocks that left the board go back to the pool. `skip` (the
        /// dragged block) keeps its pose.
        /// </summary>
        public void Sync(GameState s, ScreenLayout L, ICollection<int>? holdable, int skip = -1)
        {
            LastSnapPx = 0;
            LastSnapPiece = -1;
            var g = L.Grid;
            cellPx = g.CellPx;
            int n = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < n; id++)
            {
                if (id == skip) continue;
                if (!BoardPose.RestAnchor(s, id, out int ax, out int ay))
                {
                    Release(id);
                    continue;
                }
                var shape = BoardPose.ShapeOf(s, id);
                bool had = visuals.ContainsKey(id);
                var v = Ensure(s, id);
                var target = BoardPose.Center(g, ax, ay, shape);
                if (had && v.Visible)
                {
                    float dx = v.Position.X - target.X, dy = v.Position.Y - target.Y;
                    float snap = (float)System.Math.Sqrt(dx * dx + dy * dy) + System.Math.Abs(v.Scale.X - 1f) * (float)cellPx + System.Math.Abs(v.Scale.Y - 1f) * (float)cellPx;
                    if (snap > LastSnapPx)
                    {
                        LastSnapPx = snap;
                        LastSnapPiece = id;
                    }
                }
                v.Visible = true;
                v.Position = target;
                v.Scale = Vec2.One;
                v.Rotation = 0;
                v.Alpha = 1;
                v.Flash = 0;
                v.Tint = 0;
                v.Hop = 0;
                v.Lifted = false;
                v.Raised = false;
                bool dim = holdable != null && PieceZone(s, id) == Zone.Yard && !holdable.Contains(id);
                v.SetLook(PieceColor(s, id), dim);
            }
        }

        /// <summary>
        /// How far the last `Sync` had to move a visible block (position px + scale error × cell px): 0 when the steps of
        /// the plan already brought every block to its state pose (tests: the animations show the events, the re-sync only
        /// confirms them; debug overlay).
        /// </summary>
        public float LastSnapPx { get; private set; }
        public int LastSnapPiece { get; private set; } = -1;

        /// <summary>The cell size the visuals are built for (the level's adaptive cell, UX §5.8).</summary>
        public double CellPx
        {
            get => cellPx;
            set => cellPx = value;
        }
    }
}
