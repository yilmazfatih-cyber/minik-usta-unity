#nullable enable
// A `LevelPlayer` on a `FakeView` driven like the Unity layer drives it: design-px pointer events and fixed frame times.
// `DragTo` traces the BFS path of a move with a simulated finger (the DragMapperTests golden technique), so the tests go
// through the very input path a player's finger takes: press → lift → K-08 follow → release → commit → plan → steps.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Input;
using MinikUsta.Presentation.Text;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public sealed class Rig
    {
        public const float FrameMs = 1000f / 60f;
        public readonly FakeView View = new FakeView();
        public readonly LevelPlayer Player;
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public int FastForwardedPresses;

        public Rig(double screenW = 1080, double screenH = 1920, Func<int, string?>? read = null)
        {
            Player = new LevelPlayer(View, new LevelCatalog(read ?? ReadLevel), Tr());
            Player.SetScreen(screenW, screenH);
        }

        public static string? ReadLevel(int n) => PresentationPaths.ResourceText(LevelCatalog.ResourceName(n));

        private static Strings? tr;

        /// <summary>tr.json as the Unity build reads it (Resources/i18n/tr.json).</summary>
        public static Strings Tr() => tr ??= Strings.FromText(PresentationPaths.ResourceText("i18n/tr"));

        public static Strings En() => Strings.FromText(PresentationPaths.ResourceText("i18n/en"));

        public GameSession Session => Player.Session!;

        public void Load(int n) => Assert.IsTrue(Player.Load(n), "level " + n + " loads: " + Player.LoadError);

        /// <summary>Frames of `FrameMs` until `ms` passed.</summary>
        public void Tick(float ms)
        {
            while (ms > 1e-4f)
            {
                float d = Math.Min(FrameMs, ms);
                Player.Tick(d);
                ms -= d;
            }
        }

        /// <summary>Frames until no step is pending and no tween runs (≤ `maxMs`).</summary>
        public float Settle(float maxMs = 30000)
        {
            float t = 0;
            while ((Player.Queue.Busy || Player.Clock.Count > 0) && t < maxMs)
            {
                Player.Tick(FrameMs);
                t += FrameMs;
            }
            Assert.IsFalse(Player.Queue.Busy, "the plan finished within " + maxMs + " ms");
            return t;
        }

        /// <summary>Design-px centre of a piece's first cell (where a finger presses it).</summary>
        public void PressPoint(int pieceId, out double x, out double y)
        {
            var cell = Grid.PieceBoardCells(Session.State, pieceId)[0];
            var r = Player.Layout!.Grid.CellRect(cell.X, cell.Y);
            x = r.CenterX;
            y = r.CenterY;
        }

        /// <summary>
        /// Drags `m.PieceId` to `m.To` with a finger and releases. `settleFirst` false = the press comes right after the
        /// previous release (R-12); a press refused by a lock window waits for it and presses again.
        /// </summary>
        public ActionResult DragTo(DragMove m, bool settleFirst = true, int pointer = 3)
        {
            if (settleFirst) Settle();
            PressPoint(m.PieceId, out double x, out double y);
            int before = Player.StepsRun;
            Player.PointerDown(pointer, x, y);
            if (!Player.Mapper.Active)
            {
                Assert.IsTrue(Player.Queue.Locked(Player.Clock.Now) || Player.Panel != PanelKind.None, "a refused press is a locked window");
                Settle();
                PressPoint(m.PieceId, out x, out y);
                Player.PointerDown(pointer, x, y);
            }
            else if (Player.StepsRun > before) FastForwardedPresses++;
            Assert.IsTrue(Player.Mapper.Active, "press on piece " + m.PieceId + " starts a drag");
            Assert.AreEqual(m.PieceId, Player.Mapper.PieceId, "the hit test picks piece " + m.PieceId);
            var drag = Player.Mapper.Session!;
            var g = Player.Layout!.Grid;
            var grab = DragMath.GrabAt(g, x, y, drag.Start.Ix, drag.Start.Iy, drag.Shape.Cells);
            Player.PointerMove(pointer, x, y - 10);
            Tick(120);
            var path = drag.PathTo(m.To);
            Assert.IsNotNull(path, "node " + m.To + " is reachable for piece " + m.PieceId);
            foreach (var node in path!)
            {
                DragMath.FingerFor(g, grab, node.Ix, node.Iy, Player.Feel.FingerOffsetCells, out double fx, out double fy);
                Player.PointerMove(pointer, fx, fy);
                Tick(FrameMs);
            }
            Assert.AreEqual(m.To, drag.Current, "the finger brought piece " + m.PieceId + " to " + m.To);
            int packages = Player.Packages;
            Player.PointerUp(pointer);
            Assert.AreEqual(packages + 1, Player.Packages, "the release committed one package");
            var res = Player.LastResult!;
            Events.AddRange(res.Events);
            return res;
        }

        /// <summary>Every block visual is at its rest pose of the state (after `End`), and only on-board blocks have one.</summary>
        public void AssertBoardMatchesState(string at)
        {
            var s = Session.State;
            var g = Player.Layout!.Grid;
            int n = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < n; id++)
            {
                bool on = BoardPose.RestAnchor(s, id, out int ax, out int ay);
                View.Live.TryGetValue(id, out var v);
                if (!on)
                {
                    Assert.IsNull(v, at + ": piece " + id + " is off the board but still has a visual");
                    continue;
                }
                Assert.IsNotNull(v, at + ": piece " + id + " is on the board but has no visual");
                var want = BoardPose.Center(g, ax, ay, BoardPose.ShapeOf(s, id));
                Assert.AreEqual(want.X, v!.Position.X, 1e-3, at + ": piece " + id + " x");
                Assert.AreEqual(want.Y, v.Position.Y, 1e-3, at + ": piece " + id + " y");
                Assert.AreEqual(1f, v.Scale.X, 1e-4, at + ": piece " + id + " scale");
                Assert.AreEqual(1f, v.Alpha, 1e-4, at + ": piece " + id + " alpha");
                Assert.IsTrue(v.Visible, at + ": piece " + id + " visible");
                Assert.AreEqual(State.PieceColor(s, id), v.Color, at + ": piece " + id + " colour");
                Assert.AreEqual(0f, v.Hop, 1e-4, at + ": piece " + id + " hop");
            }
        }
    }
}
