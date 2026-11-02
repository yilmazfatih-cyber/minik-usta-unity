#nullable enable
// Paket V1: the screens as JSON scenes for tools/art/compose.mjs (MINIK_PREVIEW_DIR=…/artifacts/preview dotnet test --filter
// ScreenDumpTests → <dir>/scenes/*.json, then node tools/art/compose.mjs). Without the variable the scenes are still built and
// checked (every frame in the atlas), nothing is written.
using System;
using System.IO;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Core.Json;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Input;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    public class ScreenDumpTests
    {
        private static string? Dir()
        {
            string? d = Environment.GetEnvironmentVariable("MINIK_PREVIEW_DIR");
            if (string.IsNullOrEmpty(d)) return null;
            // a relative path is taken from dotnet/ (the README command `cd dotnet && MINIK_PREVIEW_DIR=../artifacts/preview …`)
            string root = Path.IsPathRooted(d) ? d : Path.Combine(PresentationPaths.RepoRoot, "dotnet", d);
            return Path.Combine(Path.GetFullPath(root), "scenes");
        }

        private static MemorySaveStore Store(int won, int coins, bool giftUsed)
        {
            var store = new MemorySaveStore();
            var m = SaveModel.Default(coins, 5);
            for (int n = 1; n <= won; n++) m.Level(n).Won = true;
            m.HighestLevel = won;
            m.Stars = won;
            m.FirstOfferGiftUsed = giftUsed;
            SaveCodec.Write(store, m);
            return store;
        }

        private static void Emit(JsonValue scene)
        {
            foreach (var f in Frames(scene)) Assert.IsTrue(TestArt.Has(f), scene["name"]!.AsString + ": " + f);
            var dir = Dir();
            if (dir != null) SceneDump.Write(dir, scene);
        }

        private static System.Collections.Generic.IEnumerable<string> Frames(JsonValue v)
        {
            if (v.IsObject)
            {
                var f = v["f"];
                if (f != null && f.IsString) yield return f.AsString;
                foreach (var kv in v.Members) foreach (var x in Frames(kv.Value)) yield return x;
            }
            else if (v.IsArray) foreach (var i in v.Items) foreach (var x in Frames(i)) yield return x;
        }

        /// <summary>Level 3 mid-play: one block placed, the next one lifted over its correct landing (K-18 shadow ✓).</summary>
        private static AppRig MidLevel()
        {
            var rig = new AppRig(Store(2, 1250, true));
            rig.App.Boot();
            rig.Tap(HomeView.PlayId);
            rig.TapOption(PanelAction.Play);
            rig.PlayGolden(3, 1);
            var m = Play.SolverGolden(3).Drags[1];
            var p = rig.Player;
            var cell = Grid.PieceBoardCells(rig.Session.State, m.PieceId)[0];
            var r = p.Layout!.Grid.CellRect(cell.X, cell.Y);
            p.PointerDown(7, r.CenterX, r.CenterY);
            var drag = p.Mapper.Session!;
            var g = p.Layout.Grid;
            var grab = DragMath.GrabAt(g, r.CenterX, r.CenterY, drag.Start.Ix, drag.Start.Iy, drag.Shape.Cells);
            p.PointerMove(7, r.CenterX, r.CenterY - 10);
            rig.Tick(120);
            // stop at the highest node of the path: the block is carried over the crane area, its shadow on the landing below
            var path = drag.PathTo(m.To)!.ToList();
            int top = 0;
            for (int i = 0; i < path.Count; i++) if (path[i].Iy >= path[top].Iy) top = i;
            foreach (var node in path.Take(top + 1))
            {
                DragMath.FingerFor(g, grab, node.Ix, node.Iy, p.Feel.FingerOffsetCells, out double fx, out double fy);
                p.PointerMove(7, fx, fy);
                rig.Tick(Rig.FrameMs);
            }
            rig.Tick(200);
            return rig;
        }

        [Test]
        public void V1_scenes_level_home_win_lose_offer_settings_level_start_pause_are_built_from_atlas_frames()
        {
            var mid = MidLevel();
            Assert.IsNotNull(mid.View.Shadow, "the K-18 shadow is up");
            Emit(SceneDump.Scene("level", mid));

            var home = new AppRig(Store(3, 1250, true));
            home.App.Boot();
            Emit(SceneDump.Scene("home", home));
            home.Tap("home.settings");
            Emit(SceneDump.Scene("settings", home));
            home.Tap("set.close");
            home.Tap(HomeView.PlayId);
            Emit(SceneDump.Scene("levelstart", home));
            home.TapOption(PanelAction.Play);
            home.Tap("hud.pause");
            Emit(SceneDump.Scene("pause", home));

            var lose = new AppRig(Store(2, 1250, true));
            lose.App.Boot();
            lose.Tap(HomeView.PlayId);
            lose.TapOption(PanelAction.Play);
            lose.Settle();
            SetHdr(lose.Session.State, H.MovesLeft, 1);
            lose.Player.Commit(Play.SolverGolden(3).Drags[0]);
            lose.Settle();
            Assert.AreEqual(Flow.AppScreen.LoseOffer, lose.Screen);
            Emit(SceneDump.Scene("lose", lose));

            var win = new AppRig(Store(2, 1250, true));
            win.App.Boot();
            win.Tap(HomeView.PlayId);
            win.TapOption(PanelAction.Play);
            win.PlayGolden(3);
            Assert.AreEqual(Flow.AppScreen.Win, win.Screen);
            // the star pop and the window pop have finished: the scene shows the window at rest
            Emit(SceneDump.Scene("win", win));
        }

        /// <summary>Ticks until `done` (or `maxMs`) in 60 fps frames.</summary>
        private static void Until(AppRig rig, Func<bool> done, float maxMs = 6000)
        {
            for (float t = 0; t < maxMs && !done(); t += Rig.FrameMs) rig.App.Tick(Rig.FrameMs);
        }

        [Test]
        public void B2_scenes_the_3D_king_and_fx_clips_in_motion_are_built_from_atlas_and_art3d_frames()
        {
            // home: the king mid-wave over the drifting layers
            var home = new AppRig(Store(3, 1250, true));
            home.App.Boot();
            Until(home, () => home.App.King.Home.Actor.Player.Frame == "chr_king_wave_04");
            Emit(SceneDump.Scene("home-wave", home));

            // level: a correct placement — the stud glints (lock shine), the 3D dust, the corner king cheering
            var place = new AppRig(Store(10, 1250, true));
            place.App.Boot();
            place.Tap(HomeView.PlayId);
            place.Settle();
            place.Player.Commit(Play.SolverGolden(1).Drags[0]);
            Until(place, () => place.View.FxOut.Any(f => f.Frame != null && f.Frame.StartsWith("fx_lock_shine_0")) && place.App.King.Corner.Playing == "king_cheer");
            place.Tick(70);
            Emit(SceneDump.Scene("place", place));

            // level 5: the segment flare behind the finished structure, the big cheer in the corner
            var seg = new AppRig(Store(10, 1250, true));
            seg.App.Boot();
            seg.Tap(HomeView.PlayId);
            seg.App.DebugStartLevel(5);
            var d5 = Play.SolverGolden(5).Drags;
            int slide = KingTests.SlideIndex(5);
            for (int i = 0; i < slide; i++)
            {
                seg.Settle();
                seg.Player.Commit(d5[i]);
            }
            seg.Settle();
            seg.Player.Commit(d5[slide]);
            Until(seg, () => seg.View.FxOut.Any(f => f.Frame == "fx_segment_done_04"));
            Assert.IsTrue(seg.View.FxOut.Any(f => f.Frame == "fx_segment_done_04"), "the flare plays");
            Emit(SceneDump.Scene("segment", seg));

            // the win window while the star bursts (fx_star_burst inside the window, behind the star)
            var win = new AppRig(Store(2, 1250, true));
            win.App.Boot();
            win.Tap(HomeView.PlayId);
            win.TapOption(PanelAction.Play);
            win.PlayGolden(3, Play.SolverGolden(3).Drags.Length - 1);
            win.Player.Commit(Play.SolverGolden(3).Drags.Last());
            Until(win, () => win.Ui.Root(UiLayer.Window)?.Find(WindowViews.BurstId)?.Frame == "fx_star_burst_04", 12000);
            Assert.IsTrue(win.App.King.BurstPlaying, "the burst plays behind the star");
            Emit(SceneDump.Scene("win-burst", win));
        }
    }
}
