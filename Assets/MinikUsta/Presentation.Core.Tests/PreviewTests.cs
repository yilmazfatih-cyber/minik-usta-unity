#nullable enable
// Software previews of the placeholder level screen (Faz 3 Q1; see Fixtures/Preview.cs — a layout / colour check, not a
// Unity screenshot). Set MINIK_PREVIEW_DIR to write the PNGs (390×844 and 360×800 CSS px × 2); without it one preview is
// composited in memory and checked for being a real image.
using System;
using System.IO;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Input;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public sealed class PreviewTests
    {
        private static readonly (int W, int H)[] Screens = { (780, 1688), (720, 1600) };

        [Test]
        public void Preview_screens_of_the_placeholder_level_screen()
        {
            string? dir = Environment.GetEnvironmentVariable("MINIK_PREVIEW_DIR");
            if (dir == null)
            {
                var rig = new Rig(390, 844);
                rig.Load(1);
                var png = Preview.Of(rig.View, rig.Player, 195, 422).Png();
                Assert.Greater(png.Length, 1000, "a composited PNG");
                return;
            }
            Directory.CreateDirectory(dir);
            foreach (var (w, h) in Screens)
            {
                string tag = (w / 2) + "x" + (h / 2);
                void Save(string name, Rig r) => File.WriteAllBytes(Path.Combine(dir, name + "-" + tag + ".png"), Preview.Of(r.View, r.Player, w, h).Png());

                foreach (int level in new[] { 1, 5, 6, 10 })
                {
                    var rig = new Rig(w, h);
                    rig.Load(level);
                    Save("l" + level + "-start", rig);
                }

                // level 1: a block held over the site, correct landing (K-18 valid shadow), then a wrong one
                var drag = new Rig(w, h);
                drag.Load(1);
                var m = Play.CraneMoves(1)[0];
                HoldOver(drag, m.PieceId, m.To);
                Save("l1-drag-correct", drag);
                drag.Player.PointerUp(3, canceled: true);
                drag.Settle();
                var wrong = Play.CraneMoves(1).First(x => x.PieceId != m.PieceId && State.PieceColor(drag.Session.State, x.PieceId) != State.PieceColor(drag.Session.State, m.PieceId));
                HoldOver(drag, wrong.PieceId, m.To);
                Save("l1-drag-wrong", drag);
                drag.Player.PointerUp(3, canceled: true);
                drag.Settle();

                // level 5 mid-game: half of the solution, truck delivered
                var mid = new Rig(w, h);
                mid.Load(5);
                var golden = Play.SolverGolden(5).Drags;
                for (int i = 0; i < golden.Length / 2; i++) mid.DragTo(golden[i]);
                mid.Settle();
                Save("l5-mid", mid);

                // win and out-of-moves windows
                var win = new Rig(w, h);
                win.Load(2);
                foreach (var d in Play.SolverGolden(2).Drags) win.DragTo(d);
                win.Settle();
                Save("l2-win", win);
                string text = Rig.ReadLevel(1)!.Replace("\"moves\": 11", "\"moves\": 1");
                var lose = new Rig(w, h, n => text);
                lose.Load(1);
                lose.DragTo(Play.SolverGolden(1).Drags[0]);
                lose.Settle();
                Save("l1-lose", lose);
            }
        }

        private static void HoldOver(Rig rig, int pieceId, DragNode to)
        {
            rig.PressPoint(pieceId, out double x, out double y);
            rig.Player.PointerDown(3, x, y);
            var drag = rig.Player.Mapper.Session!;
            var g = rig.Player.Layout!.Grid;
            var grab = DragMath.GrabAt(g, x, y, drag.Start.Ix, drag.Start.Iy, drag.Shape.Cells);
            rig.Player.PointerMove(3, x, y - 10);
            rig.Tick(150);
            var path = drag.PathTo(to) ?? drag.PathTo(new DragNode(to.Ix, to.Iy + 1, to.Mode))!;
            foreach (var node in path)
            {
                DragMath.FingerFor(g, grab, node.Ix, node.Iy, rig.Player.Feel.FingerOffsetCells, out double fx, out double fy);
                rig.Player.PointerMove(3, fx, fy);
                rig.Tick(Rig.FrameMs);
            }
            rig.Tick(200);
        }
    }
}
