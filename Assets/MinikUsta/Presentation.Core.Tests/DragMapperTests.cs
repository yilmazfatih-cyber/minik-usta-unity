#nullable enable
// Pointer → drag → core Move (port of web tests/scenes/dragMath.test.ts + the DragController press / lift / release rules;
// TECH §4.4–§4.6, UX_FLOWS §5.3, GDD K-07, K-08, K-09). The golden test drives every canonical move of levels 1–10 with a
// simulated finger through DragMapper and checks that the release is the very same core Move — and that committing those
// moves reproduces the TypeScript event log hash bit for bit.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Input;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class DragMapperTests
    {
        private static readonly LayoutSettings S = new LayoutSettings();
        private static readonly FeelSettings F = new FeelSettings();
        private static double Offset => F.FingerOffsetCells;

        // --- drag math (web dragMath.test.ts) ------------------------------------------------------------------------------

        [Test]
        public void UX_5_3_a_press_becomes_a_drag_at_the_8_px_threshold()
        {
            double th = F.DragStartThresholdPx;
            Assert.IsFalse(DragMath.ExceedsThreshold(th - 1, 0, th));
            Assert.IsTrue(DragMath.ExceedsThreshold(th, 0, th));
            Assert.IsTrue(DragMath.ExceedsThreshold(6, 6, th), "8.49 px diagonal");
        }

        private static readonly Cell[] Ell = { new Cell(0, 0), new Cell(1, 0), new Cell(1, 1) };

        [Test]
        public void TECH_4_4_no_jump_at_pick_at_t_0_the_target_is_the_block_anchor_wherever_the_finger_holds_it()
        {
            var g = ScreenLayout.Create(S, 1920).Grid;
            foreach (var (fu, fv) in new[] { (2.1, 3.9), (3.7, 3.2), (3.5, 4.8) })
            {
                g.PointOf(fu, fv, out double x, out double y);
                var grab = DragMath.GrabAt(g, x, y, 2, 3, Ell);
                DragMath.TargetAnchor(g, grab, x, y, 0, Offset, out double px, out double py);
                Assert.AreEqual(2, px, 1e-9);
                Assert.AreEqual(3, py, 1e-9);
            }
        }

        [Test]
        public void UX_5_3_finger_offset_at_t_1_the_grabbed_cell_centre_is_1_2_cells_above_the_finger()
        {
            var g = ScreenLayout.Create(S, 1920).Grid;
            g.PointOf(3.7, 4.8, out double x, out double y); // finger on cell (1, 1) of the block at (2, 3)
            var grab = DragMath.GrabAt(g, x, y, 2, 3, Ell);
            Assert.AreEqual((1, 1), (grab.CellX, grab.CellY));
            DragMath.TargetAnchor(g, grab, x, y, 1, Offset, out double px, out double py);
            Assert.AreEqual(3.7, px + grab.CellX + 0.5, 1e-9);
            Assert.AreEqual(4.8 + Offset, py + grab.CellY + 0.5, 1e-9);
            DragMath.TargetAnchor(g, grab, x, y, 0.5, Offset, out _, out double half);
            Assert.AreEqual((3 + py) / 2, half, 1e-9, "halfway through the glide the block is halfway up");
            DragMath.FingerFor(g, grab, px, py, Offset, out double fx, out double fy);
            Assert.AreEqual(x, fx, 1e-9, "FingerFor inverts TargetAnchor at t = 1");
            Assert.AreEqual(y, fy, 1e-9);
        }

        [Test]
        public void TECH_4_4_drawing_leaves_the_node_only_toward_an_edge_at_most_one_cell()
        {
            var node = new DragNode(3, 4, 0);
            DragMath.DrawAnchor(node, 3.4, 3.6, Edges.None, out double ax, out double ay);
            Assert.AreEqual((3.0, 4.0), (ax, ay));
            var edges = DragMath.EdgesOf(node, new[] { new DragNode(4, 4, 0), new DragNode(3, 3, 0) });
            Assert.AreEqual(new Edges(false, true, true, false), edges);
            DragMath.DrawAnchor(node, 3.4, 3.6, edges, out ax, out ay);
            Assert.AreEqual(3.4, ax, 1e-12);
            Assert.AreEqual(3.6, ay, 1e-12);
            DragMath.DrawAnchor(node, 2.5, 5.5, edges, out ax, out ay);
            Assert.AreEqual((3.0, 4.0), (ax, ay));
            DragMath.DrawAnchor(node, 9, 0, edges, out ax, out ay);
            Assert.AreEqual((4.0, 3.0), (ax, ay));
            // a rail entry to the right counts as a right edge; a mode switch in place is no direction
            Assert.AreEqual(new Edges(false, true, false, false), DragMath.EdgesOf(node, new[] { new DragNode(4, 4, 1), new DragNode(3, 4, 1) }));
        }

        [Test]
        public void TECH_4_4_BFS_path_following_12_ms_per_cell_at_most_120_ms_cell_by_cell()
        {
            Assert.AreEqual(36, DragMath.PathDurationMs(3, F.PathMsPerCell, F.PathMaxMs));
            Assert.AreEqual(120, DragMath.PathDurationMs(30, F.PathMsPerCell, F.PathMaxMs));
            var xs = new double[] { 2, 2, 2, 3 };
            var ys = new double[] { 6, 7, 8, 8 };
            DragMath.PathPoint(xs, ys, 4, 0, out double ax, out double ay);
            Assert.AreEqual((2.0, 6.0), (ax, ay));
            DragMath.PathPoint(xs, ys, 4, 1.0 / 3, out ax, out ay);
            Assert.AreEqual(2.0, ax, 1e-12);
            Assert.AreEqual(7.0, ay, 1e-12);
            DragMath.PathPoint(xs, ys, 4, 5.0 / 6, out ax, out ay);
            Assert.AreEqual(2.5, ax, 1e-12);
            Assert.AreEqual(8.0, ay, 1e-12);
            DragMath.PathPoint(xs, ys, 4, 1, out ax, out ay);
            Assert.AreEqual((3.0, 8.0), (ax, ay));
        }

        // --- DragMapper press / lift / release ---------------------------------------------------------------------------

        private sealed class Board
        {
            public readonly GameSession Session;
            public readonly ScreenLayout Layout;
            public readonly DragMapper Mapper = new DragMapper(F);
            public float Now;

            public Board(int level, double designH = 1920)
            {
                var lvl = Play.LevelFile(level);
                Session = GameSession.Start(lvl, null, 0, new SessionOptions(strict: true));
                Layout = ScreenLayout.Create(S, designH, lvl.Geo);
            }

            public GameState State => Session.State;
            public BoardGeometry Grid => Layout.Grid;

            /// <summary>Design point of the centre of the anchor cell's `cell` of a piece (its first shape cell by default).</summary>
            public (double X, double Y) CellCentre(int pieceId, int cellIndex = 0)
            {
                var bc = MinikUsta.Core.Grid.PieceBoardCells(State, pieceId)[cellIndex];
                var r = Grid.CellRect(bc.X, bc.Y);
                return (r.CenterX, r.CenterY);
            }

            public PressResult Down(double x, double y) => Mapper.Down(State, Layout, Session.Hooks.Drag, 1, x, y, Now);
        }

        [Test]
        public void UX_5_3_K07_a_release_under_the_threshold_is_a_tap_and_commits_nothing()
        {
            var b = new Board(1);
            var (x, y) = b.CellCentre(0);
            Assert.AreEqual(PressOutcome.Pressed, b.Down(x, y).Outcome);
            Assert.IsNull(b.Mapper.Move(1, x + 5, y, b.Now += 16, b.Layout), "5 px: still a press");
            var up = b.Mapper.Up(1);
            Assert.AreEqual(ReleaseKind.Tap, up.Kind);
            Assert.AreEqual(0, up.PieceId);
            Assert.IsNull(up.Move);
            Assert.IsFalse(b.Mapper.Active);
        }

        [Test]
        public void UX_5_3_a_press_lifts_after_8_px_or_after_100_ms_held()
        {
            var b = new Board(1);
            var (x, y) = b.CellCentre(0);
            b.Down(x, y);
            Assert.IsNull(b.Mapper.Update(b.Now + 99, b.Layout));
            var held = b.Mapper.Update(b.Now + 100, b.Layout);
            Assert.IsNotNull(held);
            Assert.IsTrue(held!.Lifted && held.NodeChanged);
            Assert.AreEqual(DropKinds.Cancel, held.Drop!.Kind, "lifted in place: same spot");
            Assert.AreEqual(CancelReasons.SameSpot, held.Drop.Reason);
            b.Mapper.Abort();

            var c = new Board(1);
            c.Down(x, y);
            var moved = c.Mapper.Move(1, x, y - F.DragStartThresholdPx, c.Now + 16, c.Layout);
            Assert.IsNotNull(moved);
            Assert.IsTrue(moved!.Lifted);
            Assert.IsTrue(c.Mapper.Lifted);
        }

        [Test]
        public void TECH_4_6_a_second_finger_is_ignored_and_a_system_cancel_aborts_without_a_move()
        {
            var b = new Board(1);
            var (x, y) = b.CellCentre(0);
            b.Down(x, y);
            var (x2, y2) = b.CellCentre(1);
            Assert.AreEqual(PressOutcome.None, b.Mapper.Down(b.State, b.Layout, b.Session.Hooks.Drag, 2, x2, y2, b.Now).Outcome, "second finger");
            Assert.IsNull(b.Mapper.Move(2, x2 + 50, y2, b.Now + 16, b.Layout), "the second finger moves nothing");
            Assert.AreEqual(ReleaseKind.None, b.Mapper.Up(2).Kind);
            b.Mapper.Move(1, x, y - 40, b.Now + 32, b.Layout);
            var up = b.Mapper.Up(1, canceled: true);
            Assert.AreEqual(ReleaseKind.Aborted, up.Kind);
            Assert.IsNull(up.Move);
        }

        [Test]
        public void K09_K14_a_press_on_a_block_the_core_refuses_reports_the_reason_and_starts_nothing()
        {
            // level 3: c (piece 2) lies on a and b — a buried block cannot be picked (K-09 immovable)
            var b = new Board(3);
            var s = b.State;
            int refused = -1;
            string? reason = null;
            for (int id = 0; id < s.Lvl.Layout.Counts.Pieces && refused < 0; id++)
            {
                if (PieceZone(s, id) != Zone.Yard) continue;
                var attempt = Movement.TryBeginDrag(s, id, b.Session.Hooks.Drag);
                if (!attempt.Ok)
                {
                    refused = id;
                    reason = attempt.Reason;
                }
            }
            Assert.GreaterOrEqual(refused, 0, "level 3 starts with a block that cannot be picked");
            var (x, y) = b.CellCentre(refused);
            var press = b.Down(x, y);
            Assert.AreEqual(PressOutcome.PickFailed, press.Outcome);
            Assert.AreEqual(refused, press.PieceId);
            Assert.AreEqual(reason, press.Reason);
            Assert.IsFalse(b.Mapper.Active);
            // outside the board zone: nothing, and not on the board
            var hud = b.Layout.Moves;
            var off = b.Down(hud.CenterX, hud.CenterY);
            Assert.AreEqual(PressOutcome.None, off.Outcome);
            Assert.IsFalse(off.OnBoard);
        }

        [Test]
        public void K08_a_finger_path_over_the_wall_brings_level_1_piece_a_to_its_tutorial_release_node_4_4()
        {
            // web dragMath.test.ts: tutorial step 1 glove, up above the yard, then over the wall ((0,2) → (0,4) → (4,4)); EXPAND 2337
            var b = new Board(1, ScreenFit.DesignHeight(ScaleMode.Expand, 390, 844, S));
            var (x, y) = b.CellCentre(0);
            Assert.AreEqual(PressOutcome.Pressed, b.Down(x, y).Outcome);
            var session = b.Mapper.Session!;
            var g = b.Grid;
            var grab = DragMath.GrabAt(g, x, y, session.Start.Ix, session.Start.Iy, session.Shape.Cells);
            b.Now = 0;
            b.Mapper.Update(b.Now = 100, b.Layout); // held: lifted
            b.Mapper.Update(b.Now = 200, b.Layout); // the finger offset glide is over
            var path = new List<(double X, double Y)>();
            foreach (var (ax, ay) in new[] { (0, 2), (0, 4), (4, 4) })
            {
                DragMath.FingerFor(g, grab, ax, ay, Offset, out double fx, out double fy);
                path.Add((fx, fy));
            }
            bool crossed = false;
            for (int i = 1; i < path.Count; i++)
                for (int k = 1; k <= 10; k++)
                {
                    double fx = path[i - 1].X + (path[i].X - path[i - 1].X) * k / 10;
                    double fy = path[i - 1].Y + (path[i].Y - path[i - 1].Y) * k / 10;
                    var frame = b.Mapper.Move(1, fx, fy, b.Now += 16, b.Layout)!;
                    crossed |= frame.CrossedWall;
                }
            Assert.AreEqual(new DragNode(4, 4, 0), session.Current);
            Assert.IsTrue(crossed, "the tutorial `overWall` signal fired on the way");
            var up = b.Mapper.Up(1);
            Assert.AreEqual(ReleaseKind.Drag, up.Kind);
            Assert.AreEqual(DropKinds.SiteFree, up.Drop!.Kind);
            Assert.AreEqual(6, up.Drop.Row, "K-07 row 6: falls (K-11)");
            Assert.IsTrue(SessionActionJson.SameAs(new DragMove(0, new DragNode(4, 4, 0)), up.Move!));
        }

        [Test]
        public void TECH_4_4_the_drawn_block_follows_the_finger_inside_its_node_and_never_shows_inside_a_wall()
        {
            var b = new Board(1);
            var (x, y) = b.CellCentre(0);
            b.Down(x, y);
            var session = b.Mapper.Session!;
            var grab = DragMath.GrabAt(b.Grid, x, y, session.Start.Ix, session.Start.Iy, session.Shape.Cells);
            b.Mapper.Update(100, b.Layout);
            b.Mapper.Update(200, b.Layout);
            // level 1: b (piece 1) stands at (1, 2)–(1, 3). At node (0, 3) the right neighbour is b: the block stays on its node
            DragMath.FingerFor(b.Grid, grab, 0.25, 3, Offset, out double fx, out double fy);
            var frame = b.Mapper.Move(1, fx, fy, 216, b.Layout)!;
            Assert.AreEqual(new DragNode(0, 3, 0), frame.Node);
            Assert.AreEqual(0, frame.Ax, 1e-9, "no edge toward b: never drawn inside a block");
            // one row up (yard air, over b) the right neighbour is free: drawn a quarter cell off the node toward it
            DragMath.FingerFor(b.Grid, grab, 0.25, 4, Offset, out fx, out fy);
            frame = b.Mapper.Move(1, fx, fy, 224, b.Layout)!;
            Assert.AreEqual(new DragNode(0, 4, 0), frame.Node);
            Assert.AreEqual(0.25, frame.Ax, 1e-9);
            Assert.AreEqual(4, frame.Ay, 1e-9);
            // finger far to the left of the yard: the block stays at x 0 (no left edge), never drawn outside the board
            DragMath.FingerFor(b.Grid, grab, -3, 4, Offset, out fx, out fy);
            frame = b.Mapper.Move(1, fx, fy, 232, b.Layout)!;
            Assert.AreEqual(0, frame.Ax, 1e-9);
            Assert.Less(frame.Px, -2, "the follow target is the finger's (sticky separation for the JUICE #4 tether)");
        }

        // --- golden: the simulated finger reproduces every canonical move of levels 1–10 -----------------------------------

        [Test]
        public void K07_K08_K35_drag_mapper_reproduces_every_canonical_move_and_the_golden_event_log_hash([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            foreach (var designH in new[] { 1920.0, ScreenFit.DesignHeight(ScaleMode.Expand, 390, 844, S) })
            {
                var golden = Play.SolverGolden(level);
                var b = new Board(level, designH);
                var sink = new ArraySink();
                var session = GameSession.Start(b.Session.Lvl, null, 0, new SessionOptions(strict: true), sink);
                var layout = b.Layout;
                var mapper = new DragMapper(F);
                float now = 0;
                int i = 0;
                foreach (var m in golden.Drags)
                {
                    string at = "B" + level + " H" + designH + " move " + (i++) + " (piece " + m.PieceId + ")";
                    var s = session.State;
                    // press on the piece's first cell
                    var cell = Grid.PieceBoardCells(s, m.PieceId)[0];
                    var r = layout.Grid.CellRect(cell.X, cell.Y);
                    var press = mapper.Down(s, layout, session.Hooks.Drag, 7, r.CenterX, r.CenterY, now);
                    Assert.AreEqual(PressOutcome.Pressed, press.Outcome, at + ": press");
                    Assert.AreEqual(m.PieceId, press.PieceId, at + ": hit test picks the golden piece");
                    var drag = mapper.Session!;
                    var grab = DragMath.GrabAt(layout.Grid, r.CenterX, r.CenterY, drag.Start.Ix, drag.Start.Iy, drag.Shape.Cells);
                    // lift by moving past the threshold, let the finger-offset glide finish
                    Assert.IsTrue(mapper.Move(7, r.CenterX, r.CenterY - 10, now += 16, layout)!.Lifted, at + ": lifted");
                    mapper.Update(now += 120, layout);
                    // follow the BFS path node by node with the finger (as a player would trace it)
                    var path = drag.PathTo(m.To);
                    Assert.IsNotNull(path, at + ": the golden node is reachable");
                    foreach (var node in path!)
                    {
                        DragMath.FingerFor(layout.Grid, grab, node.Ix, node.Iy, Offset, out double fx, out double fy);
                        mapper.Move(7, fx, fy, now += 16, layout);
                    }
                    Assert.AreEqual(m.To, drag.Current, at + ": the finger brought the block to the golden node");
                    var up = mapper.Up(7);
                    Assert.AreEqual(ReleaseKind.Drag, up.Kind, at);
                    Assert.IsTrue(SessionActionJson.SameAs(m, up.Move!), at + ": the release is the golden move " + SessionActionJson.ToJson(m) + ", got " + SessionActionJson.ToJson(up.Move!));
                    Assert.IsFalse(up.Drop!.IsCancelled, at + ": no cancel");
                    var res = session.Commit(up.Move!, sink);
                    Assert.AreEqual(MoveStatuses.Applied, res.Status, at);
                    now += 1000;
                }
                Assert.AreEqual(BoosterOutcomes.Won, session.Outcome, "B" + level + ": won");
                Assert.AreEqual(golden.EventLogHash, Moves.EventLogHash(sink.Events), "B" + level + " H" + designH + ": the mapped moves give the TypeScript event log");
            }
        }
    }
}
