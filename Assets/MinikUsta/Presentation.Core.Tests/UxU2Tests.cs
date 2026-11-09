#nullable enable
// Paket U2: the P0 list of docs/UX_UNITY.md §12 (studio ux-design method; evidence docs/review_inbox/ux-audit.md). One test (or more) per
// P0 item; the names carry the item id. The game runs on fakes (`AppRig`), the screens are the real trees.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.I18n;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Tutorial;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class UxU2Tests
    {
        private static readonly (int W, int H)[] Phones = { (390, 844), (360, 800) };

        private static AppRig Booted(int w = 390, int h = 844, string lang = "tr")
        {
            var rig = new AppRig(null, w, h);
            rig.App.Boot();
            if (lang != "tr")
            {
                rig.App.Save.Settings.Lang = lang;
                rig.I18n.SetLang(lang);
            }
            return rig;
        }

        private static AppRig InLevel(int level, int w = 390, int h = 844, string lang = "tr")
        {
            var rig = Booted(w, h, lang);
            Assert.IsTrue(rig.App.StartLevel(level));
            rig.Settle();
            return rig;
        }

        private static UiNode Hud(AppRig rig) => rig.Ui.Root(UiLayer.Hud)!;

        /// <summary>Spends the moves with yard-only drags until the +5 offer opens (as the shots scenario does).</summary>
        private static void BurnMoves(AppRig rig)
        {
            for (int guard = 0; guard < 200 && rig.Screen == AppScreen.Level; guard++)
            {
                rig.Settle();
                if (rig.Screen != AppScreen.Level) break;
                var s = rig.Session.State;
                bool moved = false;
                foreach (int id in Summary.HoldableIds(s, rig.Session.Hooks))
                {
                    var drag = Movement.BeginDrag(s, id, rig.Session.Hooks.Drag);
                    if (drag == null) continue;
                    foreach (var node in drag.ReachableNodes())
                    {
                        if (drag.Classify(node).Kind != DropKinds.Yard) continue;
                        if (rig.Player.Commit(new DragMove(id, node)).Status != MoveStatuses.Applied) continue;
                        moved = true;
                        break;
                    }
                    if (moved) break;
                }
                Assert.IsTrue(moved, "a yard move is left: level " + rig.Player.LevelNumber + " outcome " + rig.Session.Outcome + " moves " + rig.Session.MovesLeft + " screen " + rig.Screen + " panel " + rig.Player.Panel);
            }
            rig.Settle();
        }

        // --- P0-1 EXPAND (K1) ----------------------------------------------------------------------------------------------------

        [Test]
        public void U2_P0_1_EXPAND_is_the_default_and_phones_have_no_dead_bands()
        {
            Assert.AreEqual(ScaleMode.Expand, new LayoutSettings().Mode, "UX_UNITY K1: EXPAND by default");
            foreach (var (w, h) in Phones)
            {
                var vp = ScreenFit.Viewport(w, h, new LayoutSettings());
                Assert.Less(Math.Abs(vp.LetterboxPx), 1.0, w + "×" + h + ": no band top / bottom (screen px)");
                Assert.Less(Math.Abs(vp.PillarboxPx), 1.0, w + "×" + h + ": no band left / right");
                var rig = InLevel(1, w, h);
                var L = rig.Player.Layout!;
                Assert.AreEqual(vp.H, L.H);
                foreach (var b in L.Boosters) Assert.AreEqual(L.H - 120, b.Bottom, 1e-6, "the bottom group is anchored 120 px over the bottom edge");
                Assert.AreEqual(112, L.Panorama.Y, "the top group stays at the top");
                Assert.AreEqual((L.H - 1920) * 0.5, L.BoardShift, 1e-6, "the board group moves by (H − 1920) × 0,5");
                var home = rig.Ui.Root(UiLayer.Home) ?? HomeView.Build(rig.App.HomeModelOf(), L.W, L.H, rig.I18n.Text);
                Assert.AreEqual(L.H - 1920 + 1384, home.Get(HomeView.PlayId).Rect.Y, 1e-6, "home: the play button anchored to the bottom");
            }
            Assert.AreEqual(ScaleMode.Fit, ScreenFit.ModeFromUrl("https://x/?scale=fit"), "FIT stays reachable for comparison shots");
        }

        [Test]
        public void U2_P0_1_safe_area_insets_the_design_canvas()
        {
            // a 1080 × 2400 phone with a 96 px status bar and a 48 px gesture bar (Unity's Screen.safeArea, origin bottom-left)
            var f = SafeFrame.Of(1080, 2400, 0, 48, 1080, 2400 - 96 - 48);
            Assert.AreEqual(1080, f.W);
            Assert.AreEqual(2256, f.H);
            var vp = ScreenFit.Viewport(f.W, f.H, new LayoutSettings());
            f.ScreenToDesign(vp, 540, 48 + 2256, out double dx, out double dy);
            Assert.AreEqual(540, dx, 1e-6);
            Assert.AreEqual(0, dy, 1e-6, "the top of the safe area is design y 0 (the HUD stays out of the notch)");
            f.ScreenToDesign(vp, 540, 48, out _, out dy);
            Assert.AreEqual(vp.H, dy, 1e-6, "the bottom of the safe area is design y H");
            Assert.AreEqual(-24, f.CenterOffsetY, 1e-6, "the safe centre is 24 screen px under the screen centre");
            var full = SafeFrame.Of(390, 844, 0, 0, 390, 844);
            Assert.AreEqual(0, full.CenterOffsetX);
            Assert.AreEqual(0, full.CenterOffsetY, "no inset (desktop, WebGL): nothing moves");
            Assert.AreEqual(SafeFrame.Of(390, 844, 0, 0, 0, 0).W, 390, "an empty safe area falls back to the whole screen");
        }

        // --- P0-2 back stack (K5) --------------------------------------------------------------------------------------------------

        [Test]
        public void U2_P0_2_AC7_every_screen_but_boot_has_a_back_move_of_the_flow_table()
        {
            foreach (AppScreen screen in Enum.GetValues(typeof(AppScreen)))
            {
                var e = ScreenFlow.BackEvent(screen);
                if (screen == AppScreen.Boot)
                {
                    Assert.IsNull(e);
                    continue;
                }
                Assert.IsNotNull(e, screen + " has a back event");
                Assert.IsTrue(ScreenFlow.Moves().Any(m => m.From == screen && m.Event == e!.Value), screen + " --" + e + "--> is in the table");
            }
            var to = ScreenFlow.Moves().ToDictionary(m => (m.From, m.Event), m => m.To);
            Assert.AreEqual(AppScreen.QuitConfirm, to[(AppScreen.Home, FlowEvent.Back)]);
            Assert.AreEqual(AppScreen.Home, to[(AppScreen.QuitConfirm, FlowEvent.Stay)]);
            Assert.AreEqual(AppScreen.Paused, to[(AppScreen.Level, FlowEvent.Pause)]);
            Assert.AreEqual(AppScreen.Paused, to[(AppScreen.ExitConfirm, FlowEvent.Stay)], "back on the exit confirmation = Kal");
        }

        [Test]
        public void U2_P0_2_back_on_home_asks_to_quit_and_stay_or_back_return_home()
        {
            var rig = Booted();
            bool quit = false;
            rig.App.QuitRequested = () => quit = true;
            Assert.IsTrue(rig.App.Back());
            Assert.AreEqual(AppScreen.QuitConfirm, rig.Screen);
            var w = rig.Ui.Root(UiLayer.Overlay)!;
            Assert.AreEqual("Oyundan çıkılsın mı?", w.Get("win.title").Text);
            Assert.IsTrue(rig.App.Back(), "back again = Kal");
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            Assert.IsNull(rig.Ui.Root(UiLayer.Overlay));
            Assert.IsFalse(quit);
            rig.App.Back();
            rig.Tap(rig.Ui.Root(UiLayer.Overlay)!.All().First(n => n.Action == WindowViews.ActionOf(PanelAction.Stay)).Id);
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            Assert.IsFalse(quit);
            rig.App.Back();
            rig.Tap(rig.Ui.Root(UiLayer.Overlay)!.All().First(n => n.Action == WindowViews.ActionOf(PanelAction.Leave)).Id);
            Assert.IsTrue(quit, "Çık asks the app to close");
            Assert.AreEqual(AppScreen.Home, rig.Screen, "a WebGL tab cannot close: the home screen stays usable");
            Assert.IsTrue(rig.Ui.Tap(HomeView.PlayId), "and the play button works again");
        }

        [Test]
        public void U2_P0_2_back_closes_the_top_window_and_never_spends_a_life_or_coins()
        {
            var rig = Booted();
            int lives0 = rig.App.Save.Lives, coins0 = rig.App.Save.Coins;
            // settings over home: back closes the overlay only
            rig.Tap("home.settings");
            Assert.IsTrue(rig.App.Back());
            Assert.IsFalse(rig.App.Flow.SettingsOpen);
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            // level → pause → back → level
            rig.Tap(HomeView.PlayId);
            rig.Settle();
            Assert.AreEqual(AppScreen.Level, rig.Screen);
            Assert.IsTrue(rig.App.Back());
            Assert.AreEqual(AppScreen.Paused, rig.Screen, "back in a level opens the pause window");
            Assert.IsTrue(rig.App.Back());
            Assert.AreEqual(AppScreen.Level, rig.Screen, "back on pause = Devam");
            Assert.AreEqual(PanelKind.None, rig.Player.Panel);
            // pause → home → exit confirmation → back = Kal → pause
            rig.App.Back();
            rig.TapOption(PanelAction.Exit);
            Assert.AreEqual(AppScreen.ExitConfirm, rig.Screen);
            rig.App.Back();
            Assert.AreEqual(AppScreen.Paused, rig.Screen);
            rig.App.Back();
            Assert.AreEqual(AppScreen.Level, rig.Screen);
            Assert.AreEqual(lives0 - 1, rig.App.Save.Lives, "only the level start reserved a life");
            Assert.AreEqual(coins0, rig.App.Save.Coins);
            Assert.AreEqual(1, rig.Session.Log.Count, "no move was played by a back press");
        }

        [Test]
        public void U2_P0_2_back_on_level_start_goes_home_without_reserving_a_life_and_on_win_it_continues()
        {
            var rig = Booted();
            rig.App.Save.Level(1).Won = true;
            rig.App.Save.Level(2).Won = true;
            rig.App.SetScreen(390, 844); // rebuild the home tree with the new progress
            int lives0 = rig.App.Save.Lives;
            rig.Tap(HomeView.PlayId);
            Assert.AreEqual(AppScreen.LevelStart, rig.Screen);
            Assert.IsTrue(rig.App.Back());
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            Assert.AreEqual(lives0, rig.App.Save.Lives, "back on the level start window costs nothing");

            var win = Booted();
            win.Tap(HomeView.PlayId);
            win.PlayGolden(1);
            Assert.AreEqual(AppScreen.Win, win.Screen);
            int coins = win.App.Save.Coins;
            Assert.IsTrue(win.App.Back());
            Assert.AreEqual(AppScreen.Home, win.Screen, "back on the win window = Devam (the reward is already booked)");
            Assert.AreEqual(coins, win.App.Save.Coins);
            Assert.IsTrue(win.App.Save.Won(1));
        }

        [Test]
        public void U2_P0_2_back_on_the_offer_declines_without_paying()
        {
            var rig = Booted();
            rig.App.Save.Level(1).Won = true;
            rig.App.Save.Level(2).Won = true;
            rig.App.SetScreen(390, 844);
            rig.Tap(HomeView.PlayId);
            rig.TapOption(PanelAction.Play); // Bölüm 3: its yard leaves room for yard-only moves (Bölüm 1 has none)
            rig.Settle();
            BurnMoves(rig);
            Assert.AreEqual(AppScreen.LoseOffer, rig.Screen);
            int coins = rig.App.Save.Coins;
            Assert.IsTrue(rig.App.Back());
            Assert.AreEqual(AppScreen.LoseFinal, rig.Screen, "back on the +5 offer = Hayır, teşekkürler");
            Assert.AreEqual(coins, rig.App.Save.Coins, "no coins spent");
            Assert.IsTrue(rig.App.Back());
            Assert.AreEqual(AppScreen.Home, rig.Screen, "back on the loss window = Ana sayfa");
        }

        // --- P0-3 LockedHint (K6) -------------------------------------------------------------------------------------------------

        [Test]
        public void U2_P0_3_AC8_every_home_button_answers_a_tap()
        {
            foreach (var id in Booted().Ui.Root(UiLayer.Home)!.All().Where(n => n.IsButton && n.Shown).Select(n => n.Id).ToList())
            {
                var rig = Booted();
                var screen0 = rig.Screen;
                int hints = rig.App.Hint.Shows;
                rig.Tap(id);
                bool reacted = rig.Screen != screen0 || rig.App.Flow.SettingsOpen || rig.App.Hint.Shows > hints || id == HomeView.KingId;
                Assert.IsTrue(reacted, id + ": a tap changes the screen, opens a panel or shows the hint balloon");
            }
        }

        [TestCase("home.tab0.hit", "home.tab0.lock")]
        [TestCase("home.tab1.hit", "home.tab1.lock")]
        [TestCase("home.tab3.hit", "home.tab3.lock")]
        [TestCase("home.tab4.hit", "home.tab4.lock")]
        [TestCase("home.coins.hit", "home.coins.plus")]
        [TestCase("home.lives.hit", "home.lives.icon")]
        public void U2_P0_3_a_locked_home_element_shows_the_hint_at_once_and_its_lock_shakes(string hit, string lockId)
        {
            var rig = Booted();
            rig.Tap(hit);
            var h = rig.App.Hint;
            Assert.IsTrue(h.Visible, "the balloon is up in the same frame as the tap (≤ 100 ms, K6)");
            Assert.AreEqual("Yakında", h.Text);
            var target = rig.Ui.Root(UiLayer.Home)!.Get(hit).Rect;
            var b = h.Balloon;
            Assert.LessOrEqual(b.W, LockedHint.MaxW);
            Assert.AreEqual(LockedHint.BalloonH, b.H);
            Assert.IsTrue(Math.Abs(b.Bottom + LockedHint.GapPx - target.Y) < 1e-6 || Math.Abs(b.Y - LockedHint.GapPx - target.Bottom) < 1e-6,
                "16 px from the target (above, or below in the top bar)");
            Assert.IsTrue(b.X >= 0 && b.Right <= 1080);
            rig.Tick(50);
            Assert.AreNotEqual(0f, h.LockRotation, "the lock shakes");
            Assert.LessOrEqual(Math.Abs(h.LockRotation), LockedHint.ShakeDeg + 1e-3);
            Assert.AreEqual(h.LockRotation, rig.Ui.Target(rig.Ui.Root(UiLayer.Home)!.Get(lockId)).Rotation, 1e-6);
            rig.Tick(400);
            Assert.AreEqual(0f, h.LockRotation, "3 shakes in 360 ms, then still");
            rig.Tick(800);
            Assert.IsFalse(h.Visible, "gone after 1200 ms");
            Assert.IsNull(rig.Ui.Root(UiLayer.Hint));
        }

        [Test]
        public void U2_P0_3_a_booster_slot_tap_reaches_the_app_and_shows_unlock_level_or_coming_soon()
        {
            var rig = InLevel(8);
            var hud = Hud(rig);
            Assert.IsTrue(hud.Get("hud.boost0").Shown, "Bölüm 8: the hammer slot is on screen (2 levels before it opens at 10)");
            rig.Tap("hud.boost0");
            Assert.AreEqual("10. bölümde açılır", rig.App.Hint.Text);
            Assert.AreEqual(rig.Player.Layout!.Boosters[0], rig.App.Hint.Target);
            var en = InLevel(10, 390, 844, "en");
            en.Tap("hud.boost0");
            Assert.AreEqual("Coming soon", en.App.Hint.Text, "an unlocked slot: the boosters are not playable in this build yet");
            Assert.IsFalse(en.App.Hint.Text!.Contains("{"), "no raw placeholder");
        }

        [Test]
        public void U2_P0_3_reduced_motion_keeps_the_lock_still()
        {
            var rig = Booted();
            rig.Tap("home.settings");
            rig.Tap("set.motion");
            rig.Tap("set.ok");
            rig.Tap("home.tab0.hit");
            rig.Tick(50);
            Assert.IsTrue(rig.App.Hint.Visible);
            Assert.AreEqual(0f, rig.App.Hint.LockRotation);
        }

        // --- AC-3: touch targets ------------------------------------------------------------------------------------------------

        private static IEnumerable<(string Screen, UiNode Root)> Trees(AppRig rig)
        {
            yield return ("home", rig.Ui.Root(UiLayer.Home)!);
            rig.Tap("home.settings");
            yield return ("settings", rig.Ui.Root(UiLayer.Overlay)!);
            rig.Tap("set.ok");
            rig.App.Back();
            yield return ("quit", rig.Ui.Root(UiLayer.Overlay)!);
            rig.App.Back();
            rig.Tap(HomeView.PlayId);
            rig.Settle();
            yield return ("level", rig.Ui.Root(UiLayer.Hud)!);
            rig.App.Back();
            yield return ("pause", rig.Ui.Root(UiLayer.Window)!);
            rig.TapOption(PanelAction.Exit);
            yield return ("exit", rig.Ui.Root(UiLayer.Window)!);
        }

        [Test]
        public void U2_AC3_every_visible_tap_target_is_at_least_128_px_and_44_pt_at_390()
        {
            double pt390 = 1080.0 / 390; // design px per pt on a 390 pt wide phone
            foreach (var (w, h) in Phones)
            {
                var rig = Booted(w, h);
                foreach (var (screen, root) in Trees(rig))
                    foreach (var n in root.All().Where(n => n.IsButton && n.Shown))
                    {
                        Assert.GreaterOrEqual(Math.Min(n.Rect.W, n.Rect.H), 128 - 1e-6, w + "×" + h + " " + screen + ": " + n);
                        Assert.GreaterOrEqual(Math.Min(n.Rect.W, n.Rect.H), 44 * pt390 - 1e-6, screen + ": " + n + " ≥ 44 pt");
                    }
            }
        }

        // --- P0-4 HUD text ≥ 34 px -----------------------------------------------------------------------------------------------

        [Test]
        public void U2_P0_4_AC5_no_HUD_text_is_drawn_under_34_px([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            foreach (var lang in new[] { "tr", "en" })
                foreach (var (w, h) in Phones.Concat(new[] { (1080, 1920) }))
                {
                    var rig = InLevel(level, w, h, lang);
                    rig.View.Hud!.SetStreak(2, 12, rig.I18n.Text.Get("hud.streak"), rig.I18n.Text.Format(MinikUsta.Presentation.Text.Strings.Keys.Times, ("n", 12)));
                    var labels = Hud(rig).All().Where(n => n.Shown && !string.IsNullOrEmpty(n.Text)).ToList();
                    Assert.Greater(labels.Count, 6);
                    foreach (var n in labels)
                        Assert.GreaterOrEqual(TextFit.SizeOf(n), 34, lang + " " + w + "×" + h + " B" + level + ": " + n + " drawn at " + TextFit.SizeOf(n));
                    var hud = Hud(rig);
                    Assert.AreEqual(36, TextFit.SizeOf(hud.Get("hud.pano.name")), "the level name at 36 px on one line: " + hud.Get("hud.pano.name").Text);
                    foreach (var id in new[] { "hud.streak.label", "hud.moves.label", "hud.trowel.n", "hud.blocks.label" })
                        Assert.AreEqual(34, TextFit.SizeOf(hud.Get(id)), id);
                }
        }

        [Test]
        public void U2_P0_4_a_long_level_name_goes_on_two_lines_at_34_px_never_shrinks_below()
        {
            var rig = InLevel(1);
            rig.View.Hud!.SetLevel("Bölüm 1", "Kuzey Kulesinin Ahşap Merdivenleri");
            var hud = Hud(rig);
            var one = hud.Get("hud.pano.name");
            var two = hud.Get("hud.pano.name2");
            Assert.IsTrue(two.Shown);
            Assert.AreEqual(34, TextFit.SizeOf(one), one.Text);
            Assert.AreEqual(34, TextFit.SizeOf(two), two.Text);
            Assert.AreEqual("Kuzey Kulesinin Ahşap Merdivenleri", one.Text + " " + two.Text);
            rig.View.Hud!.SetLevel("Bölüm 1", "Platform");
            Assert.IsFalse(hud.Get("hud.pano.name2").Shown);
            Assert.AreEqual(36, TextFit.SizeOf(hud.Get("hud.pano.name")));
        }

        // --- P0-5 GoalCard -------------------------------------------------------------------------------------------------------

        [Test]
        public void U2_P0_5_goal_card_is_340_by_176_with_36_px_plan_cells_and_24_px_from_the_moves_plate()
        {
            foreach (var (w, h) in Phones)
                foreach (int level in Play.Levels)
                {
                    var rig = InLevel(level, w, h);
                    var L = rig.Player.Layout!;
                    Assert.AreEqual(304, L.TopGroupBottomY, "UX_UNITY P0-5: the HUD ends at 304");
                    Assert.AreEqual(new PxRect(24, 112, 340, 176), L.Panorama, "GoalCard 340 × 176");
                    Assert.GreaterOrEqual(L.Moves.X - L.Panorama.Right, 24, "≥ 24 px between the goal card and the moves plate");
                    Assert.AreEqual(300, L.Moves.W);
                    Assert.AreEqual(136, L.Moves.H);
                    Assert.AreEqual(L.Panorama.CenterY, L.Moves.CenterY, 1e-6, "the moves plate sits on the card's middle");
                    Assert.AreEqual(888, L.Pause.X, "U4 R-07: the pause tool is 144 px, its right edge stays at 1032");
                    Assert.AreEqual(144, L.Pause.W);
                    var cells = Hud(rig).Get("hud.pano.plan").Children;
                    Assert.Greater(cells.Count, 0);
                    double cell = cells[0].Rect.W;
                    Assert.LessOrEqual(cell, 36);
                    var plan = HudLayout.PanoramaPlan(L);
                    foreach (var c in cells) Assert.IsTrue(c.Rect.X >= plan.X - 1e-6 && c.Rect.Right <= plan.Right + 1e-6 && c.Rect.Y >= plan.Y - 1e-6 && c.Rect.Bottom <= plan.Bottom + 1e-6, "B" + level + " cell inside the card");
                    Assert.IsTrue(plan.X >= L.Panorama.X && plan.Right <= L.Panorama.Right && plan.Y >= L.Panorama.Y && plan.Bottom <= L.Panorama.Bottom, "plan area inside the card");
                }
        }

        [Test]
        public void U2_P0_5_at_H_1920_a_tall_board_shrinks_the_card_back_but_never_under_the_compact_hud()
        {
            var s = new LayoutSettings();
            foreach (int level in Play.Levels)
            {
                var L = ScreenLayout.Create(s, 1920, Play.LevelFile(level).Geo);
                Assert.That(L.TopGroupBottomY, Is.InRange(264.0, 304.0));
                Assert.GreaterOrEqual(L.Grid.CraneTopY - L.TopGroupBottomY, s.MinGapTopPx, "B" + level + ": the crane area stays under the HUD");
                Assert.GreaterOrEqual(L.Panorama.H, 136, "the card never gets smaller than before");
                Assert.AreEqual(L.TopGroupBottomY - 16, L.Panorama.Bottom, 1e-6);
            }
        }

        // --- P0-6 the tutorial bubble over the board (K2-B) ----------------------------------------------------------------------

        [TestCase(390, 844)]
        [TestCase(360, 800)]
        [TestCase(1080, 1920)]
        public void U2_P0_6_level_1_step_1_bubble_sits_16_px_over_the_crane_area_close_to_the_glove(int w, int h)
        {
            var rig = Booted(w, h);
            rig.Tap(HomeView.PlayId);
            rig.Tick(3000);
            var tp = rig.Player.Tutorial!;
            var L = rig.Player.Layout!;
            var bubble = tp.View.Bubble!;
            Assert.IsTrue(bubble.Visible);
            var box = bubble.Get("tut.box").Rect;
            Assert.AreEqual(L.Board.Crane.Y - 16, bubble.Rect.Bottom, 1e-6, "the bubble's bottom edge 16 px over the crane area");
            Assert.GreaterOrEqual(bubble.Rect.Y, L.TopGroupBottomY + 16, "…and under the HUD");
            Assert.IsFalse(bubble.Rect.Overlaps(L.Board.Yard) || bubble.Rect.Overlaps(L.Board.Site), "it covers no cell");
            // the tail points down from the box's bottom edge
            var tail = bubble.Get("tut.tail").Rect;
            Assert.AreEqual(0f, bubble.Get("tut.tail").Rotation);
            Assert.AreEqual(box.Bottom - 16, tail.Y, 1e-6);
            // the glove over a whole loop: its centre stays ≤ 420 px under the bubble's bottom edge (the tail base)
            double far = 0, near = double.MaxValue;
            for (int i = 0; i < 2000 / 16; i++)
            {
                rig.Tick(16);
                var g = tp.View.Glove!;
                if (!g.Shown) continue;
                double d = g.Rect.CenterY - bubble.Rect.Bottom;
                far = Math.Max(far, d);
                near = Math.Min(near, d);
            }
            Assert.Greater(far, 0, "the glove was on screen");
            Assert.LessOrEqual(far, 420, "bubble ↔ glove ≤ 420 px (UX_UNITY P0-6)");
            TestContext.Out.WriteLine(w + "×" + h + ": bubble bottom → glove centre " + near.ToString("0") + "…" + far.ToString("0") + " px");
        }

        [Test]
        public void U2_P0_6_a_HUD_highlight_keeps_the_top_dock_with_the_tail_up()
        {
            var L = ScreenLayout.Create(new LayoutSettings(), 2337, Play.LevelFile(1).Geo);
            var place = TutorialLayout.PlaceBubble(L, 600, 138, new[] { "goals" }, new List<PxRect>(), null, 24, 16, 500);
            Assert.AreEqual(BubbleDock.Top, place.Dock);
            Assert.IsTrue(place.TailUp);
            Assert.AreEqual(L.TopGroupBottomY + 16, place.Rect.Y);
            var board = TutorialLayout.PlaceBubble(L, 600, 138, new[] { "crane" }, new List<PxRect>(), null, 24, 16, 500);
            Assert.AreEqual(BubbleDock.Board, board.Dock);
            Assert.IsTrue(board.TailDown);
            Assert.AreEqual(500 - 300, board.Rect.X, 1e-6, "centred on the anchor");
        }

        // --- P0-7 booster slot visibility (K3) -----------------------------------------------------------------------------------

        [Test]
        public void U2_P0_7_no_booster_slot_in_levels_1_to_7_the_hammer_appears_locked_at_level_8()
        {
            for (int level = 1; level <= 7; level++)
            {
                var hud = Hud(InLevel(level));
                for (int i = 0; i < 4; i++) Assert.IsFalse(hud.Get("hud.boost" + i).Shown, "B" + level + " slot " + i);
            }
            var rig = InLevel(8);
            var h6 = Hud(rig);
            Assert.IsTrue(h6.Get("hud.boost0").Shown, "the hammer (unlock 10) shows 2 levels early");
            Assert.IsTrue(h6.Get("hud.boost0.lock").Shown);
            Assert.AreEqual("Bölüm 10", h6.Get("hud.boost0.at").Text);
            Assert.IsTrue(h6.Get("hud.boost0.at").Shown);
            Assert.AreEqual(34, TextFit.SizeOf(h6.Get("hud.boost0.at")));
            Assert.IsTrue(h6.Get("hud.boost1").Shown, "the crane (unlock 10) shows 2 levels early as well");
            for (int i = 2; i < 4; i++) Assert.IsFalse(h6.Get("hud.boost" + i).Shown, "slot " + i + " stays hidden at Bölüm 8");
            Assert.IsTrue(BoosterSlotRules.Visible(8, 10, 0), "the crane slot from Bölüm 8");
            Assert.IsTrue(BoosterSlotRules.Visible(11, 13, 0), "undo from 11");
            Assert.IsFalse(BoosterSlotRules.Visible(19, 22, 0), "the brush from 20");
            Assert.IsTrue(BoosterSlotRules.Visible(1, 22, 1), "a slot with a count is always shown");
        }

        [Test]
        public void U2_P0_7_slots_keep_their_x_and_a_held_booster_shows_its_slot()
        {
            var rig = Booted();
            rig.App.Save.Boosters["crane"] = 2;
            Assert.IsTrue(rig.App.StartLevel(2));
            rig.Settle();
            var hud = Hud(rig);
            var L = rig.Player.Layout!;
            Assert.IsTrue(hud.Get("hud.boost1").Shown, "the crane is held (a chest gift): its slot shows at Bölüm 2");
            Assert.IsTrue(hud.Get("hud.boost1.lock").Shown, "…locked until its unlock level");
            Assert.IsFalse(hud.Get("hud.boost0").Shown);
            for (int i = 0; i < 4; i++) Assert.AreEqual(L.Boosters[i], hud.Get("hud.boost" + i).Rect, "slot " + i + " keeps its place");
            Assert.AreEqual(304 + 1 * (160 + 32), L.Boosters[1].X);
        }

        // --- P0-8 nav tab names (K4) ---------------------------------------------------------------------------------------------

        [TestCase("tr")]
        [TestCase("en")]
        public void U2_P0_8_all_five_nav_tabs_show_their_name_and_locked_ones_are_faded(string lang)
        {
            var rig = Booted(390, 844, lang);
            rig.App.SetScreen(390, 844);
            var home = rig.Ui.Root(UiLayer.Home)!;
            for (int i = 0; i < 5; i++)
            {
                var name = home.Get("home.tab" + i + ".text");
                Assert.AreEqual(rig.I18n.Text.Get(HomeView.TabKeys[i]), name.Text);
                Assert.GreaterOrEqual(TextFit.SizeOf(name), 34, name.ToString());
                if (i == 2) continue;
                Assert.AreEqual(1f, name.Alpha, "U4 R-02: a locked tab's name is NOT faded (AC-4)");
                Assert.AreEqual(0.55f, home.Get("home.tab" + i).Alpha, 1e-6, "icon at 55 %");
                Assert.AreEqual(48, home.Get("home.tab" + i + ".lock").Rect.W, "48 px lock");
                var hit = home.Get("home.tab" + i + ".hit");
                Assert.IsTrue(hit.IsButton);
                for (int j = i + 1; j < 5; j++)
                {
                    if (j == 2) continue;
                    var other = home.Get("home.tab" + j + ".hit").Rect;
                    Assert.GreaterOrEqual(Math.Max(other.X - hit.Rect.Right, hit.Rect.X - other.Right), 16 - 1e-6, "≥ 16 px between tab targets");
                }
            }
        }

        // --- P0-9 SegmentedPill --------------------------------------------------------------------------------------------------

        [TestCase("tr")]
        [TestCase("en")]
        public void U2_P0_9_language_pills_keep_16_px_around_their_text(string lang)
        {
            var rig = Booted(360, 800, lang);
            rig.Tap("home.settings");
            var set = rig.Ui.Root(UiLayer.Overlay)!;
            var row = set.Get("set.lang.bg").Rect;
            foreach (var id in new[] { "tr", "en" })
            {
                var pill = set.Get("set.lang." + id).Rect;
                var text = set.Get("set.lang." + id + ".text");
                Assert.AreEqual(SettingsView.PillTextBox(pill), text.Rect);
                Assert.GreaterOrEqual(text.Rect.X - pill.X, 16 + 60, "flag + 16 px on the left");
                Assert.GreaterOrEqual(pill.Right - text.Rect.Right, 16, "16 px on the right");
                Assert.GreaterOrEqual(text.Rect.W, FontMetrics.Width(text.Text!, SettingsView.PillTextPx) + TextFit.Margin, text.Text + " fits");
                Assert.AreEqual((int)SettingsView.PillTextPx, TextFit.SizeOf(text), "\"" + text.Text + "\" is not shrunk or clipped");
                Assert.GreaterOrEqual(pill.H, 128);
                Assert.IsTrue(pill.X >= row.X && pill.Right <= row.Right, "inside the language row");
            }
            Assert.IsFalse(set.Get("set.lang.tr").Rect.Overlaps(set.Get("set.lang.en").Rect));
            Assert.IsFalse(set.Get("set.lang.label").Rect.Overlaps(set.Get("set.lang.tr").Rect), "the row label stays clear of the pills");
        }

        // --- P0-10 texts ---------------------------------------------------------------------------------------------------------

        [Test]
        public void U2_P0_10_AC13_no_form_of_address_and_the_new_keys_in_both_languages()
        {
            var tr = new Localizer(PresentationPaths.ResourceText);
            var en = new Localizer(PresentationPaths.ResourceText);
            en.SetLang(Localizer.En);
            foreach (var l in new[] { tr, en })
                foreach (var key in l.Text.AllKeys)
                {
                    string v = l.Text.Get(key).ToLowerInvariant();
                    foreach (var bad in new[] { "evlat", "kiddo", "yavrum" }) Assert.IsFalse(v.Contains(bad), l.Lang + " " + key + ": \"" + v + "\"");
                }
            Assert.AreEqual("Tahta bıraktığın gibi duruyor.", tr.Text.Get("tut.ctx.resume"));
            Assert.AreEqual("Önce alttaki boşluğu doldur.", tr.Text.Get("tut.ctx.support"));
            Assert.AreEqual("Just as you left it.", en.Text.Get("tut.ctx.resume"));
            Assert.AreEqual("Fill the gap below first.", en.Text.Get("tut.ctx.support"));
            Assert.AreEqual("Oyundan çıkılsın mı?", tr.Text.Get("app.quit.title"));
            Assert.AreEqual("Quit the game?", en.Text.Get("app.quit.title"));
            Assert.AreEqual("Bölüm 8", tr.Text.Format("booster.lockedAt", ("n", 8)));
            Assert.AreEqual("Level 8", en.Text.Format("booster.lockedAt", ("n", 8)));
            CollectionAssert.AreEquivalent(tr.Text.AllKeys.ToList(), en.Text.AllKeys.ToList(), "TR and EN have the same keys");
            var utr = new MinikUsta.Presentation.Text.Strings();
            utr.Merge(PresentationPaths.ResourceText("i18n/unity_tr"));
            var uen = new MinikUsta.Presentation.Text.Strings();
            uen.Merge(PresentationPaths.ResourceText("i18n/unity_en"));
            CollectionAssert.AreEquivalent(utr.AllKeys.ToList(), uen.AllKeys.ToList(), "unity_tr / unity_en key match");
        }

        // --- P0-11 the CI shot list ----------------------------------------------------------------------------------------------

        [Test]
        public void U2_P0_11_the_shot_list_has_the_four_new_windows_in_both_places()
        {
            foreach (var name in new[] { "pause", "exit_confirm", "level_start_window", "lose_offer" })
                CollectionAssert.Contains(ShotNamesOf(), name);
            var json = MinikUsta.Core.Json.JsonReader.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(PresentationPaths.RepoRoot, "tools", "ci-shots", "shots.json")));
            var listed = new List<string>();
            foreach (var v in json["shots"]!.Items) listed.Add(v.AsString);
            CollectionAssert.AreEqual(ShotNamesOf(), listed);
        }

        private static string[] ShotNamesOf() => MinikUsta.Presentation.Shots.ShotScenario.ShotNames;
    }
}
