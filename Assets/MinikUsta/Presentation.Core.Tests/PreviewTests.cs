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

        /// <summary>Q2 game-feel moments (a correct placement, a landing wave, the trowel, the segment slide, the truck, coins, confetti, the star).</summary>
        [Test]
        public void Preview_game_feel_moments_of_the_placeholder_level_screen()
        {
            string? dir = Environment.GetEnvironmentVariable("MINIK_PREVIEW_DIR");
            if (dir == null) return;
            Directory.CreateDirectory(dir);
            foreach (var (w, h) in Screens)
            {
                string tag = (w / 2) + "x" + (h / 2);
                void Save(string name, Rig r) => File.WriteAllBytes(Path.Combine(dir, name + "-" + tag + ".png"), Preview.Of(r.View, r.Player, w, h).Png());
                void Until(Rig r, Func<bool> cond, string what, float maxMs = 8000)
                {
                    float t = 0;
                    while (!cond() && t < maxMs)
                    {
                        r.Player.Tick(Rig.FrameMs / 2);
                        t += Rig.FrameMs / 2;
                    }
                    Assert.IsTrue(cond(), "reached: " + what);
                }

                var place = new Rig(w, h);
                place.Load(1);
                var m0 = Play.CraneMoves(1)[0];
                var pv = place.View.Live[m0.PieceId];
                place.DragTo(m0);
                Until(place, () => pv.Grow > 1.15f, "the placement pulse");
                Save("q2-place-burst", place);

                var land = new Rig(w, h);
                land.Load(1);
                land.DragTo(Play.CraneMoves(1)[0]);
                Until(land, () => land.View.Live.Values.Any(v => v.CellScales.Any(c => c.Y < 0.93f)), "a landing wave");
                Save("q2-landing-wave", land);

                var trowel = new Rig(w, h);
                trowel.Load(1);
                var drags = Play.SolverGolden(1).Drags;
                for (int i = 0; i < 3; i++) trowel.DragTo(drags[i]);
                trowel.DragTo(drags[3]);
                Until(trowel, () => trowel.View.TrowelIcon.Scale.X > 1.3f, "the trowel pop");
                Save("q2-trowel-pop", trowel);

                var slide = new Rig(w, h);
                slide.Load(5);
                var d5 = Play.SolverGolden(5).Drags;
                for (int i = 0; i < 4; i++) slide.DragTo(d5[i]);
                slide.DragTo(d5[4]);
                Until(slide, () => slide.View.SiteLayer.Position.X > 200f, "the segment slide");
                Save("q2-segment-slide", slide);
                Until(slide, () => slide.View.Truck.Visible && slide.View.Truck.Rotation > 8f, "the truck tipping");
                Save("q2-truck", slide);

                var win = new Rig(w, h);
                win.Load(1);
                var d1 = Play.SolverGolden(1).Drags;
                for (int i = 0; i < d1.Length - 1; i++) win.DragTo(d1[i]);
                win.DragTo(d1[d1.Length - 1]);
                Until(win, () => win.View.SparkLog.Count(sp => sp.Kind == SparkKind.Confetti) >= 40 && win.View.SparksOut.Count(sp => sp.Kind == SparkKind.Confetti) >= 20, "win confetti");
                Save("q2-win-confetti", win);
                Until(win, () => win.Player.CoinsInFlight >= 2, "bonus coins in flight");
                Save("q2-win-coins", win);
                Until(win, () => win.View.Panel != null, "the win window");
                win.Tick(180);
                Save("q2-win-star", win);
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
