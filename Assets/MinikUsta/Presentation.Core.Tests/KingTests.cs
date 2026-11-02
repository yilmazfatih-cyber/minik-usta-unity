#nullable enable
// Paket B2 (D-100, D-101): Usta Kral on every screen and the 2.5D feel of the level — the whole game on fakes (`AppRig`: GameApp with
// the real LevelHud trees on a FakeSurface and the shipped king.json / fx.json) and the level player alone (`Rig`). Test names carry
// the GDD rule the reaction belongs to (K-12 correct placement, K-22 segment, K-28 win, K-29 out of moves, K-34 hook 5, K-53
// tutorial, K-55 fixed yard).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.I18n;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    public class KingTests
    {
        private static MemorySaveStore Store(int won, int coins = 1250)
        {
            var store = new MemorySaveStore();
            var m = SaveModel.Default(coins, 5);
            for (int n = 1; n <= won; n++) m.Level(n).Won = true;
            m.HighestLevel = won;
            m.Stars = won;
            m.FirstOfferGiftUsed = true;
            SaveCodec.Write(store, m);
            return store;
        }

        /// <summary>The index of the golden move of `level` that completes a segment (K-22 slide), −1 when none before the last.</summary>
        public static int SlideIndex(int level)
        {
            var drags = Play.SolverGolden(level).Drags;
            var probe = GameSession.Start(Play.LevelFile(level));
            for (int i = 0; i < drags.Length - 1; i++)
                if (probe.Apply(drags[i]).Events.Any(e => e is SiteShiftedEvent)) return i;
            return -1;
        }

        /// <summary>Ticks `ms` in 60 fps frames, recording every clip each slot played and the peak of running character clips.</summary>
        private static HashSet<string> Watch(AppRig rig, float ms, KingDirector.Slot slot)
        {
            var seen = new HashSet<string>();
            for (float t = 0; t < ms; t += Rig.FrameMs)
            {
                rig.App.Tick(Rig.FrameMs);
                if (slot.Playing != null) seen.Add(slot.Playing);
                Assert.LessOrEqual(rig.App.King.ActiveClips, 2, "ART_3D §7.5: at most two character clips at once");
            }
            return seen;
        }

        private static AppRig AtLevel(int won)
        {
            var rig = new AppRig(Store(won));
            rig.App.Boot();
            rig.Tap(HomeView.PlayId);
            if (rig.Screen == AppScreen.LevelStart) rig.TapOption(PanelAction.Play);
            Assert.AreEqual(AppScreen.Level, rig.Screen);
            return rig;
        }

        // --- home -------------------------------------------------------------------------------------------------------------

        [Test]
        public void B2_D101_the_home_screen_shows_usta_kral_who_waves_then_idles_and_cheers_when_tapped()
        {
            var rig = new AppRig(Store(0));
            rig.App.Boot();
            var king = rig.App.King;
            Assert.IsTrue(king.Enabled, "king.json loaded");
            var node = rig.Ui.Root(UiLayer.Home)!.Get(HomeView.KingId);
            Assert.IsTrue(king.Home.Bound);
            rig.App.Tick(Rig.FrameMs);
            Assert.AreEqual(KingDirector.WaveClip, king.Home.Playing, "JUICE #99: he greets with a wave");
            StringAssert.StartsWith("chr_king_", node.Frame, "the node draws the 3D king");
            var seen = Watch(rig, 4000, king.Home);
            Assert.AreEqual(KingDirector.HomeIdleClip, king.Home.Playing, "then the stage idle");
            Assert.IsTrue(seen.Contains(KingDirector.WaveClip));
            Assert.AreEqual(1, king.ActiveClips, "only the home king runs");
            rig.Tap(HomeView.KingId);
            rig.App.Tick(Rig.FrameMs);
            Assert.AreEqual(KingDirector.CheerClip, king.Home.Playing, "a tap → a cheer");
            Assert.AreEqual(AppScreen.Home, rig.Screen, "and nothing else: the play button is the one action");
            Watch(rig, 1500, king.Home);
            Assert.AreEqual(KingDirector.HomeIdleClip, king.Home.Playing, "back to idle");
            // the feet stay where the layout put them, whatever the drawing (placed by the pivot)
            var box = HomeView.KingBox(rig.Player.Viewport.H);
            var f = TestArt.King.Find(node.Frame!)!;
            Assert.AreEqual(box.Bottom, node.Rect.Bottom - f.PivotY * node.Rect.H, 2.0);
        }

        [Test]
        public void B2_the_home_background_is_the_seven_3D_layers_drifting_with_time_and_still_in_reduced_motion()
        {
            var rig = new AppRig(Store(0));
            rig.App.Boot();
            var home = rig.Ui.Root(UiLayer.Home)!;
            Assert.IsNotNull(home.Find("home.bg.bg_home_sky"));
            Assert.IsNull(home.Find("home.bg"));
            var ground = home.Get("home.bg.bg_home_ground");
            double x0 = ground.Rect.X;
            rig.Tick(4000);
            Assert.AreNotEqual(x0, ground.Rect.X, "the ground layer drifted");
            Assert.IsTrue(ground.Isolated, "a moving layer gets its own canvas in Unity");
            rig.Tap("home.settings");
            rig.Tap("set.motion");
            rig.Tap("set.close");
            double x1 = ground.Rect.X;
            rig.Tick(4000);
            Assert.AreEqual(x1, ground.Rect.X, 1e-9, "reduced motion: still");
        }

        // --- level corner -----------------------------------------------------------------------------------------------------

        [Test]
        public void B2_K12_a_correct_placement_cheers_the_corner_king_and_K22_a_finished_segment_brings_a_big_cheer()
        {
            var rig = AtLevel(10); // a replay of level 1: no tutorial, the king is calm
            var king = rig.App.King;
            Assert.IsTrue(king.Corner.Bound, "the HUD corner holds the king (Tuna's place)");
            Assert.IsNull(rig.Ui.Root(UiLayer.Hud)!.Find("hud.tuna"));
            rig.App.Tick(Rig.FrameMs);
            Assert.AreEqual(KingDirector.IdleClip, king.Corner.Playing);
            var drags = Play.SolverGolden(1).Drags;
            var seen = new HashSet<string>();
            foreach (var m in drags)
            {
                rig.Settle();
                rig.Player.Commit(m);
                seen.UnionWith(Watch(rig, 1200, king.Corner));
            }
            Assert.IsTrue(seen.Contains(KingDirector.CheerClip), "K-12: a short cheer");
            Assert.IsTrue(seen.Contains(KingDirector.BigCheerClip), "K-22 / K-28: the big cheer");
        }

        [Test]
        public void B2_K34_the_corner_king_thinks_after_5_s_without_a_touch_and_stops_at_the_next_touch()
        {
            var rig = AtLevel(10);
            var king = rig.App.King;
            Watch(rig, 4500, king.Corner);
            Assert.AreEqual(KingDirector.IdleClip, king.Corner.Playing, "4,5 s: still idle");
            Watch(rig, 1000, king.Corner);
            Assert.AreEqual(KingDirector.ThinkClip, king.Corner.Playing, "5 s without a touch: he thinks");
            rig.App.PointerDown(1, 5, 5); // a touch anywhere on the level screen
            rig.App.PointerUp(1);
            var seen = Watch(rig, 800, king.Corner);
            Assert.AreEqual(KingDirector.IdleClip, king.Corner.Playing, "the touch ends the thinking (through the out frames)");
        }

        [Test]
        public void B2_K53_the_tutorial_bubble_is_the_kings_he_points_with_the_glove_and_talks_with_the_bubble()
        {
            var rig = AtLevel(0); // level 1, first time: K-53 step 1 has a bubble and a drag glove
            var king = rig.App.King;
            var seen = Watch(rig, 1500, king.Corner);
            var tp = rig.Player.Tutorial!;
            Assert.IsTrue(tp.Presence!.Look().Bubble, "the bubble shows");
            Assert.IsTrue(seen.Contains(KingDirector.PointClip), "the glove plays: the king points (JUICE #103)");
            Assert.AreEqual("chr_king_bust_talk", tp.Settings.PortraitFrame, "D-101: the bubble portrait is the king's");
            var portrait = rig.Ui.Root(UiLayer.Tutorial)?.Find("tut.portrait");
            Assert.IsNotNull(portrait);
            Assert.IsTrue(TestArt.Has(portrait!.Frame!));
        }

        // --- windows ----------------------------------------------------------------------------------------------------------

        [Test]
        public void B2_K28_the_win_window_shows_the_king_big_cheering_and_the_corner_freezes()
        {
            var rig = AtLevel(2); // level 3 through its start window
            var king = rig.App.King;
            rig.PlayGolden(3);
            Assert.AreEqual(AppScreen.Win, rig.Screen);
            var w = rig.Ui.Root(UiLayer.Window)!;
            Assert.IsNotNull(w.Find(LevelHud.WindowKingId), "the king stands on the win card (Tuna's place)");
            Assert.IsNull(w.Find("win.chr"));
            Assert.IsTrue(king.Window.Bound);
            // JUICE #96: the star burst clip plays inside the window, behind the star (not the sprite stars, not over the star)
            var burst = w.Get(WindowViews.BurstId);
            var group = w.Get(WindowViews.GroupId);
            Assert.Less(group.Children.IndexOf(burst), group.Children.IndexOf(w.Get(WindowViews.StarId)), "drawn before the star");
            Assert.IsTrue(king.BurstPlaying || burst.Frame != null, "the burst started with the window");
            Assert.AreEqual(0, rig.View.SparkLog.Count(x => x.Kind == SparkKind.Star), "no sprite stars on top of the clip");
            var seen = Watch(rig, 2500, king.Window);
            Assert.IsFalse(king.BurstPlaying, "600 ms later it is over …");
            Assert.IsFalse(burst.Visible, "… and hidden");
            Assert.IsTrue(seen.SetEquals(new[] { KingDirector.BigCheerClip }), "the big cheer loops: " + string.Join(",", seen));
            Assert.AreEqual(1, king.ActiveClips, "the corner king is frozen under the window");
            rig.TapOption(PanelAction.Continue);
            Assert.IsFalse(king.Window.Bound, "the window closed: unbound");
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            Assert.IsTrue(king.Home.Bound);
            Assert.LessOrEqual(king.PeakActiveClips, 2);
        }

        [Test]
        public void B2_K29_out_of_moves_the_king_encourages_never_mocks_and_the_level_start_window_points()
        {
            var rig = new AppRig(Store(2));
            rig.App.Boot();
            var king = rig.App.King;
            rig.Tap(HomeView.PlayId);
            Assert.AreEqual(AppScreen.LevelStart, rig.Screen);
            var seen = Watch(rig, 1500, king.Window);
            Assert.IsTrue(seen.Contains(KingDirector.PointClip), "the level start king points at the play button");
            Assert.AreEqual(2, king.ActiveClips, "the home king and the window king: two clips, the budget");
            rig.TapOption(PanelAction.Play);
            rig.Settle();
            SetHdr(rig.Session.State, H.MovesLeft, 1);
            rig.Player.Commit(Play.SolverGolden(3).Drags[0]);
            rig.Settle();
            Assert.AreEqual(AppScreen.LoseOffer, rig.Screen);
            Assert.IsTrue(king.Window.Bound);
            seen = Watch(rig, 2500, king.Window);
            Assert.IsTrue(seen.Contains(KingDirector.EncourageClip), "JUICE #52 / #57: warm encouragement");
            Assert.IsFalse(seen.Contains(KingDirector.CheerClip) || seen.Contains(KingDirector.BigCheerClip), "no celebration on a loss");
            Assert.AreEqual(KingDirector.HomeIdleClip, king.Window.Playing, "then calm");
            Assert.LessOrEqual(king.PeakActiveClips, 2);
        }

        [Test]
        public void B2_reduced_motion_the_king_shows_still_poses_without_breathing()
        {
            var rig = new AppRig(Store(0));
            rig.App.Boot();
            rig.Tap("home.settings");
            rig.Tap("set.motion");
            rig.Tap("set.close");
            Assert.IsTrue(rig.App.King.Reduced);
            rig.Tap(HomeView.KingId);
            rig.App.Tick(Rig.FrameMs);
            var node = rig.Ui.Root(UiLayer.Home)!.Get(HomeView.KingId);
            Assert.AreEqual(TestArt.King.Aliases["chr_king_still_cheer"].Of, node.Frame, "ART_3D §7.1 rule 12: the still pose");
        }

        [Test]
        public void B2_D101_the_offer_gift_names_usta_kral_in_both_languages()
        {
            var i18n = new Localizer(PresentationPaths.ResourceText);
            StringAssert.Contains("Usta Kral", i18n.Text.Format("lose.offer.gift", 5));
            StringAssert.DoesNotContain("Dede", i18n.Text.Format("lose.offer.gift", 5));
            i18n.SetLang(Localizer.En);
            StringAssert.Contains("King", i18n.Text.Format("lose.offer.gift", 5));
            StringAssert.DoesNotContain("Grandpa", i18n.Text.Format("lose.offer.gift", 5));
        }

        // --- the 2.5D feel of the level ---------------------------------------------------------------------------------------

        [Test]
        public void B2_JUICE3_a_dragged_block_leans_with_its_speed_on_a_spring_and_stands_up_after_the_release()
        {
            var rig = new Rig(1080, 1920);
            rig.Load(3);
            var f = rig.Player.Feel;
            float liftPeak = 0;
            var m = Play.SolverGolden(3).Drags[0];
            rig.DragTo(m);
            Assert.Greater(rig.Player.MaxTiltDeg, 1f, "it leaned");
            Assert.LessOrEqual(rig.Player.MaxTiltDeg, f.DragTiltMaxDeg * 1.25f + 1e-3, "at most ±8° (+ the spring's small overshoot)");
            rig.Settle();
            foreach (var v in rig.View.Live.Values) Assert.AreEqual(0f, v.Rotation, 1e-4, "piece " + v.PieceId + " stands up again");
            // reduced motion: no lean (JUICE #3)
            var calm = new Rig(1080, 1920);
            calm.Player.Reduced = true;
            calm.Load(3);
            calm.DragTo(m);
            Assert.AreEqual(0f, calm.Player.MaxTiltDeg, "reduced: no lean");
            _ = liftPeak;
        }

        [Test]
        public void B2_JUICE1_the_lifted_shadow_drops_away_and_softens_as_the_block_rises()
        {
            var rig = new Rig(1080, 1920);
            rig.Load(3);
            var f = rig.Player.Feel;
            var m = Play.SolverGolden(3).Drags[0];
            var cell = Grid.PieceBoardCells(rig.Session.State, m.PieceId)[0];
            var r = rig.Player.Layout!.Grid.CellRect(cell.X, cell.Y);
            rig.Player.PointerDown(1, r.CenterX, r.CenterY);
            rig.Player.PointerMove(1, r.CenterX, r.CenterY - 30);
            rig.Tick(300);
            var v = rig.View.Live[m.PieceId];
            Assert.IsTrue(v.Lifted);
            Assert.AreEqual(1f, v.LiftHeight, 1e-4, "fully lifted");
            LiftShadow.Pose(v.LiftHeight, f, out float dy, out float sc, out float a);
            Assert.AreEqual(f.LiftShadowOffsetPx, dy, 1e-4, "JUICE #1: y +18");
            Assert.AreEqual(f.LiftShadowAlpha, a, 1e-4, "%30");
            Assert.Greater(sc, 1f, "wider and softer");
            rig.Player.PointerUp(1);
            Assert.AreEqual(0f, v.LiftHeight, "released: down again");
        }

        [Test]
        public void B2_K12_a_correct_placement_shines_on_every_stud_bottom_to_top_and_K11_the_landing_puffs_3D_dust()
        {
            var rig = new Rig(1080, 1920);
            rig.Load(1);
            var drags = Play.SolverGolden(1).Drags;
            rig.DragTo(drags[0]);
            rig.Tick(50);
            var shape = BoardPose.ShapeOf(rig.Session.State, drags[0].PieceId);
            var log = rig.Player.Fx.Log;
            Assert.AreEqual(shape.Cells.Length, log.Count(c => c == "fx_lock_shine"), "JUICE #12 / #92: one glint per stud");
            Assert.Contains("fx_dust_puff", log, "JUICE #11: the 3D dust cloud");
            var shines = rig.View.FxLog.Where(x => x.Frames.Count > 0 && x.Frames[0].StartsWith("fx_lock_shine")).ToList();
            for (int i = 1; i < shines.Count; i++) Assert.GreaterOrEqual(shines[i - 1].Position.Y, shines[i].Position.Y - 1e-3, "bottom → top");
            rig.Settle();
            Assert.AreEqual(0, rig.Player.Fx.Alive);
            Assert.AreEqual(0, rig.View.FxOut.Count);
        }

        [Test]
        public void B2_K22_a_finished_segment_flares_behind_the_site_and_the_camera_pushes_in()
        {
            var rig = new Rig(1080, 1920);
            rig.Load(5);
            var drags = Play.SolverGolden(5).Drags;
            int zooms = rig.Player.Zooms;
            int slide = SlideIndex(5);
            Assert.Greater(slide, 0);
            for (int i = 0; i <= slide; i++) rig.DragTo(drags[i]);
            rig.Tick(400);
            Assert.Contains("fx_segment_done", rig.Player.Fx.Log, "JUICE #18 / #93");
            Assert.IsTrue(rig.View.FxLog.Any(x => x.Depth == FxDepth.Under && x.Frames.Count > 0 && x.Frames[0].StartsWith("fx_segment_done")), "behind the blocks");
            Assert.Greater(rig.Player.Zooms, zooms, "the push-in");
            rig.Settle();
            Assert.AreEqual(Vec2.One, rig.View.CameraRig.Scale);
        }

        [Test]
        public void B2_K28_win_confetti_and_bonus_coins_turn_through_their_3D_cels()
        {
            var rig = new Rig(1080, 1920);
            rig.Load(1);
            var drags = Play.SolverGolden(1).Drags;
            foreach (var m in drags) rig.DragTo(m);
            rig.Settle();
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome);
            var confetti = rig.View.SparkLog.Where(s => s.Kind == SparkKind.Confetti || s.Kind == SparkKind.ConfettiRound).ToList();
            Assert.Greater(confetti.Count, 0);
            Assert.IsTrue(confetti.Any(s => s.Kind == SparkKind.ConfettiRound), "round pieces too");
            Assert.IsTrue(confetti.All(s => s.MaxCel > 0), "every piece flipped");
            var coins = rig.View.SparkLog.Where(s => s.Kind == SparkKind.Coin).ToList();
            Assert.Greater(coins.Count, 0);
            Assert.IsTrue(coins.All(s => s.MaxCel >= 6), "JUICE #56: the coin turns (fx_coin_spin)");
        }

        [Test]
        public void B2_K55_D099_the_truck_chip_shows_only_while_a_block_waits_in_the_queue()
        {
            var rig = AtLevel(4); // level 5: two segments on one fixed yard (D-099)
            var hud = rig.Ui.Root(UiLayer.Hud)!;
            Assert.AreEqual(1, rig.Session.Lvl.Batches.Length, "K-55: one batch, the whole yard");
            Assert.IsFalse(hud.Get("hud.truck").Visible, "no truck chip: the queue is empty");
            Assert.IsFalse(rig.View.HudState!.Truck);
        }
    }
}
