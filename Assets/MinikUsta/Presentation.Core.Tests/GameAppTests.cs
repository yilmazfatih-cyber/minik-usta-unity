#nullable enable
// Paket V1: the whole game on fakes (`AppRig`): first launch → Bölüm 1, win rewards and save, K-43 resume and void, the K-29
// +5 offer (gift, escalation, short of coins, decline → loss), K-43/2 exit from the pause window, settings that persist, the
// K-54 booster slots, and the screen geometry rules every screen must keep (UX §0.3 tap targets ≥ 128 px, inside the canvas).
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Meta;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    public class GameAppTests
    {
        /// <summary>A store whose save has levels 1 … won-1 won (the home button plays `won`).</summary>
        private static MemorySaveStore StoreWithWon(int upTo, int coins = 500)
        {
            var store = new MemorySaveStore();
            var m = SaveModel.Default(coins, 5);
            for (int n = 1; n <= upTo; n++) m.Level(n).Won = true;
            m.HighestLevel = upTo;
            SaveCodec.Write(store, m);
            return store;
        }

        private static AppRig Booted(MemorySaveStore? store = null, double w = 1080, double h = 1920)
        {
            var rig = new AppRig(store, w, h);
            rig.App.Boot();
            return rig;
        }

        private static string Text(AppRig rig, UiLayer layer, string id) => rig.Ui.Root(layer)!.Get(id).Text ?? "";

        [Test]
        public void UX21_first_launch_reaches_level_1_in_at_most_3_taps()
        {
            var rig = Booted();
            Assert.AreEqual(SaveLoadStatus.Fresh, rig.App.LoadStatus);
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            Assert.AreEqual("Oyna · Bölüm 1", Text(rig, UiLayer.Home, "home.play.text"));
            Assert.AreEqual("Dolu", Text(rig, UiLayer.Home, "home.lives.text"));
            Assert.AreEqual("500", Text(rig, UiLayer.Home, "home.coins.n"));
            Assert.IsFalse(rig.View.WorldVisible, "no board on the home screen");
            rig.Tap(HomeView.PlayId);
            Assert.AreEqual(AppScreen.Level, rig.Screen);
            Assert.AreEqual(1, rig.Player.LevelNumber);
            Assert.LessOrEqual(rig.App.Taps, 3, "UX §2.1: ≤ 3 taps to Bölüm 1");
            Assert.IsTrue(rig.View.WorldVisible);
            Assert.IsTrue(rig.Ui.Visible(UiLayer.Hud));
            Assert.IsFalse(rig.Ui.Visible(UiLayer.Home));
            Assert.AreEqual(4, rig.App.Save.Lives, "META §2: a life is reserved at the start");
            Assert.IsNotNull(rig.App.Save.InLevel);
            Assert.AreEqual(1, rig.App.Save.InLevel!.Actions.Count, "the start action is saved at once");
        }

        [Test]
        public void K28_winning_level_1_pays_the_rewards_saves_the_star_shows_the_structure_and_continue_goes_home()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            rig.PlayGolden(1);
            Assert.AreEqual(AppScreen.Win, rig.Screen);
            var s = rig.Session;
            var r = Rewards.Win(s.Lvl.Difficulty, s.MovesLeft, Combo.TrowelsOf(s.State), true, AppRig.Economy());
            var save = rig.App.Save;
            Assert.AreEqual(500 + r.TotalCoins, save.Coins, "K-28 + META §3.1: base + Bonus İnşaat + trowels");
            Assert.AreEqual(1, save.Stars);
            Assert.IsTrue(save.Won(1));
            Assert.AreEqual(5, save.Lives, "the reserved life comes back on a win");
            Assert.IsNull(save.InLevel, "the win is written in the commit that made it");
            Assert.AreEqual(1, save.WinStreak);
            var w = rig.Ui.Root(UiLayer.Window)!;
            Assert.IsNotNull(w.Find(WindowViews.StarId), "first win: the level star");
            Assert.AreEqual("+" + r.TotalCoins, w.Get("win.coins.text").Text);
            // Q1 open question b: the finished structure is drawn INSIDE the window (on the card), never hidden by it
            var card = w.Get("win.card").Rect;
            var cells = w.All().Where(n => n.Id.StartsWith("win.thumb.")).ToList();
            int planCells = 0;
            foreach (var seg in s.Lvl.Segments) foreach (int m in seg.PlanMask) for (int b = m; b != 0; b &= b - 1) planCells++;
            Assert.AreEqual(planCells, cells.Count, "every plan cell of every segment, built");
            Assert.IsTrue(cells.All(c => c.Frame!.StartsWith("blk_") && card.Contains(c.Rect.CenterX, c.Rect.CenterY)), "built blocks on the card");
            Assert.IsNotNull(rig.View.StarCenter);
            rig.TapOption(PanelAction.Continue);
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            Assert.AreEqual("Oyna · Bölüm 2", Text(rig, UiLayer.Home, "home.play.text"));
            Assert.AreEqual("1/10", Text(rig, UiLayer.Home, "home.progress.text"));
            Assert.IsNull(rig.Ui.Root(UiLayer.Window), "the window is closed");
        }

        [Test]
        public void K43_a_killed_attempt_resumes_bit_identical_and_a_changed_level_is_voided_penalty_free()
        {
            var store = new MemorySaveStore();
            var rig = Booted(store);
            rig.Tap(HomeView.PlayId);
            rig.PlayGolden(1, 1);
            var buf = (int[])rig.Session.State.Buf.Clone();
            // the app is killed here: a new app on the same store
            var again = Booted(store);
            Assert.AreEqual(AppScreen.Level, again.Screen, "K-43: the attempt resumes");
            Assert.AreEqual(1, again.Player.LevelNumber);
            Assert.AreEqual(buf, again.Session.State.Buf);
            Assert.IsTrue(again.Session.Resumed);
            Assert.AreEqual(4, again.App.Save.Lives, "the reserved life stays reserved");
            // a level file that changed since: the attempt cannot replay → voided, the life comes back
            var m = SaveCodec.Load(store, SaveModel.Default(500, 5)).Data;
            m.InLevel!.LevelHash = "0000000000000000";
            SaveCodec.Write(store, m);
            var voided = Booted(store);
            Assert.AreEqual(AppScreen.Home, voided.Screen);
            StringAssert.Contains("voided", voided.App.BootNote);
            Assert.AreEqual(5, voided.App.Save.Lives);
            Assert.IsNull(voided.App.Save.InLevel);
        }

        [Test]
        public void E67_K43_K55_an_attempt_saved_with_rulesVersion_2_is_closed_without_penalty_under_RULES_VERSION_6()
        {
            var store = new MemorySaveStore();
            var rig = Booted(store);
            rig.Tap(HomeView.PlayId);
            rig.PlayGolden(1, 1);
            Assert.AreEqual(4, rig.App.Save.Lives, "the attempt holds a reserved life");
            // the record as the Faz 2R app wrote it: rulesVersion 2 and a +5 paid with 90 coins
            var m = SaveCodec.Load(store, SaveModel.Default(500, 5)).Data;
            int coins = m.Coins;
            m.InLevel!.RulesVersion = 2;
            m.InLevel.OfferSpendCoins = 90;
            SaveCodec.Write(store, m);
            Assert.AreEqual(6, GameSession.RulesVersion, "D-099 (K-55 item 8) bumped the rules version to 3, levels v3 (GDD §16) to 4, levels v3c (D-103..D-105) to 5, D-106 (pallet layer leaves its own stack) to 6");
            var voided = Booted(store);
            Assert.AreEqual(AppScreen.Home, voided.Screen, "the attempt is not replayed; the player is back on the home screen");
            StringAssert.Contains("voided", voided.App.BootNote);
            StringAssert.Contains("rules version 2", voided.App.BootNote);
            Assert.AreEqual(5, voided.App.Save.Lives, "the reserved life comes back");
            Assert.AreEqual(coins + 90, voided.App.Save.Coins, "the +5 coins come back");
            Assert.IsNull(voided.App.Save.InLevel);
        }

        /// <summary>Makes the next committed move the last one (moves left 1), then commits golden move `i` of level `n`.</summary>
        private static void LastMove(AppRig rig, int n, int i)
        {
            rig.Settle();
            SetHdr(rig.Session.State, H.MovesLeft, 1);
            var r = rig.Player.Commit(Play.SolverGolden(n).Drags[i]);
            Assert.AreEqual(MoveStatuses.Applied, r.Status);
            rig.Settle();
        }

        [Test]
        public void K29_out_of_moves_gift_first_then_escalation_short_of_coins_passive_decline_is_a_loss()
        {
            var rig = Booted(StoreWithWon(2));
            Assert.AreEqual("Oyna · Bölüm 3", Text(rig, UiLayer.Home, "home.play.text"));
            rig.Tap(HomeView.PlayId);
            Assert.AreEqual(AppScreen.LevelStart, rig.Screen, "UX §4: from Bölüm 3 the level start window");
            Assert.AreEqual("Bölüm 3", rig.Ui.Root(UiLayer.Window)!.Get("win.title").Text);
            rig.TapOption(PanelAction.Play);
            Assert.AreEqual(AppScreen.Level, rig.Screen);
            Assert.LessOrEqual(rig.App.Taps, 3);
            LastMove(rig, 3, 0);
            Assert.AreEqual(AppScreen.LoseOffer, rig.Screen, "K-29: the +5 window");
            var gift = rig.Option(PanelAction.BuyMoves);
            Assert.AreEqual("+5 hamle · Usta Kral'dan hediye", gift.Get(gift.Id + ".label").Text, "META §3.2: the lifetime-first offer is a gift (B2, D-101: from Usta Kral)");
            Assert.IsNull(rig.Ui.Root(UiLayer.Window)!.All().FirstOrDefault(x => x.Action == WindowViews.ActionOf(PanelAction.AdMoves)), "no ad option on the gift");
            Assert.IsNull(rig.Ui.Root(UiLayer.Window)!.Find("win.counter"), "U4 R-03: no \"Teklif 1/3\" on the first offer");
            rig.TapOption(PanelAction.BuyMoves);
            Assert.AreEqual(AppScreen.Level, rig.Screen);
            rig.Settle();
            Assert.AreEqual(5, rig.Session.MovesLeft);
            Assert.IsTrue(rig.App.Save.FirstOfferGiftUsed);
            Assert.AreEqual(500, rig.App.Save.Coins, "the gift costs nothing");
            Assert.AreEqual(1, rig.App.Save.InLevel!.OffersUsed);
            LastMove(rig, 3, 1);
            Assert.AreEqual(AppScreen.LoseOffer, rig.Screen);
            var offer = rig.Option(PanelAction.BuyMoves);
            Assert.IsFalse(offer.Enabled, "offer 2 costs 1350 > 500 coins: grey and passive");
            Assert.AreEqual("Altın yetmiyor", offer.Get(offer.Id + ".label").Text);
            Assert.IsFalse(rig.Ui.Tap(offer.Id), "a passive option does nothing");
            Assert.AreEqual("Teklif 2/3", rig.Ui.Root(UiLayer.Window)!.Get("win.counter").Text);
            rig.TapOption(PanelAction.Decline);
            Assert.AreEqual(AppScreen.LoseFinal, rig.Screen);
            Assert.IsNull(rig.App.Save.InLevel);
            Assert.AreEqual(4, rig.App.Save.Lives, "the reserved life is the life lost");
            Assert.AreEqual(500, rig.App.Save.Coins);
            rig.TapOption(PanelAction.Retry);
            Assert.AreEqual(AppScreen.LevelStart, rig.Screen, "UX §12: retry → the level start window (Bölüm ≥ 3)");
            rig.TapOption(PanelAction.Close);
            Assert.AreEqual(AppScreen.Home, rig.Screen);
        }

        [Test]
        public void K29_a_bought_offer_charges_the_coins_and_the_save_remembers_what_was_paid()
        {
            var store = StoreWithWon(2, 5000);
            var m = SaveCodec.Load(store, SaveModel.Default(500, 5)).Data;
            m.FirstOfferGiftUsed = true;
            SaveCodec.Write(store, m);
            var rig = Booted(store);
            rig.Tap(HomeView.PlayId);
            rig.TapOption(PanelAction.Play);
            LastMove(rig, 3, 0);
            var offer = rig.Option(PanelAction.BuyMoves);
            Assert.IsTrue(offer.Enabled);
            Assert.AreEqual("≈ ₺81", offer.Get(offer.Id + ".line2").Text, "UX §0.3 PriceLabel");
            Assert.AreEqual("900", offer.Get(offer.Id + ".price").Text);
            var ad = rig.Option(PanelAction.AdMoves);
            Assert.IsFalse(ad.Enabled, "no ad provider: grey, never hidden");
            Assert.AreEqual("Şu an reklam yok", ad.Get(ad.Id + ".line2").Text);
            rig.TapOption(PanelAction.BuyMoves);
            Assert.AreEqual(4100, rig.App.Save.Coins);
            Assert.AreEqual(900, rig.App.Save.InLevel!.OfferSpendCoins);
        }

        /// <summary>Level 3 with a move budget of `moves` (the same file otherwise; its level hash is that of this text).</summary>
        private static string? ShortLevel(int n, int moves)
        {
            string? text = Rig.ReadLevel(n);
            if (n != 3 || text == null) return text;
            var j = MinikUsta.Core.Json.JsonReader.Parse(text);
            j.Set("moves", MinikUsta.Core.Json.JsonValue.Of(moves));
            return MinikUsta.Core.Json.JsonWriter.Write(j, false);
        }

        [Test]
        public void K43_K29_killed_with_the_offer_window_open_resumes_into_the_same_offer()
        {
            var store = StoreWithWon(2);
            System.Func<int, string?> read = n => ShortLevel(n, 1);
            var rig = new AppRig(store, 1080, 1920, read);
            rig.App.Boot();
            rig.Tap(HomeView.PlayId);
            rig.TapOption(PanelAction.Play);
            Assert.AreEqual(1, rig.Session.MovesLeft, "the test level has one move");
            rig.PlayGolden(3, 1);
            Assert.AreEqual(AppScreen.LoseOffer, rig.Screen, "K-29: really out of moves");
            Assert.AreEqual("outOfMoves", rig.App.Save.InLevel!.OutcomeWindow);
            var again = new AppRig(store, 1080, 1920, read);
            again.App.Boot();
            Assert.AreEqual(AppScreen.LoseOffer, again.Screen, "K-43/3: the window that was open comes back");
            Assert.AreEqual(0, again.Session.MovesLeft);
            Assert.IsNull(again.Ui.Root(UiLayer.Window)!.Find("win.counter"), "U4 R-03");
            again.TapOption(PanelAction.BuyMoves);
            Assert.AreEqual(AppScreen.Level, again.Screen);
            again.Settle();
            Assert.AreEqual(5, again.Session.MovesLeft);
        }

        [Test]
        public void K43_pause_exit_before_a_move_is_free_after_a_move_a_loss_and_restart_starts_a_new_attempt()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            rig.Tap("hud.pause");
            Assert.AreEqual(AppScreen.Paused, rig.Screen);
            rig.TapOption(PanelAction.Exit);
            Assert.AreEqual(AppScreen.ExitConfirm, rig.Screen);
            Assert.AreEqual("Henüz hamle yapmadın; can gitmez.", rig.Ui.Root(UiLayer.Window)!.Get("win.line0").Text);
            rig.TapOption(PanelAction.Stay);
            Assert.AreEqual(AppScreen.Paused, rig.Screen);
            rig.TapOption(PanelAction.Exit);
            rig.TapOption(PanelAction.Leave);
            Assert.AreEqual(AppScreen.Home, rig.Screen, "K-43/2: movesSpent 0 → free");
            Assert.AreEqual(5, rig.App.Save.Lives);
            rig.Tap(HomeView.PlayId);
            rig.PlayGolden(1, 1);
            rig.Tap("hud.pause");
            rig.TapOption(PanelAction.Restart);
            Assert.AreEqual("Çıkarsan 1 can gider.", rig.Ui.Root(UiLayer.Window)!.Get("win.line0").Text);
            rig.TapOption(PanelAction.Leave);
            Assert.AreEqual(AppScreen.Level, rig.Screen, "restart: a new attempt at once");
            Assert.AreEqual(3, rig.App.Save.Lives, "the loss took one life, the new attempt reserved another");
            Assert.AreEqual(3, rig.App.Save.Level(1).Attempts);
            rig.Tap("hud.pause");
            rig.TapOption(PanelAction.Exit);
            rig.TapOption(PanelAction.Leave);
            Assert.AreEqual(AppScreen.Home, rig.Screen, "no move in the new attempt: free again");
            Assert.AreEqual(4, rig.App.Save.Lives);
        }

        [Test]
        public void UX11_JUICE0_settings_language_reduce_motion_and_vibration_apply_at_once_and_persist()
        {
            var store = new MemorySaveStore();
            var rig = Booted(store);
            rig.Tap("home.settings");
            Assert.IsTrue(rig.App.Flow.SettingsOpen);
            Assert.AreEqual("Ayarlar", Text(rig, UiLayer.Overlay, "set.title"));
            rig.Tap("set.lang.en");
            Assert.AreEqual("Settings", Text(rig, UiLayer.Overlay, "set.title"));
            Assert.AreEqual("Play · Level 1", Text(rig, UiLayer.Home, "home.play.text"));
            rig.Tap("set.motion");
            Assert.IsTrue(rig.Player.Reduced, "JUICE §0 rule 8");
            Assert.AreEqual("toggle_on", rig.Ui.Root(UiLayer.Overlay)!.Get("set.motion.toggle").Frame);
            rig.Tap("set.haptics");
            Assert.IsFalse(rig.Player.Haptics.Enabled);
            rig.Tap("set.sound");
            Assert.IsFalse(rig.App.Save.Settings.Sound);
            rig.Tap("set.close");
            Assert.IsNull(rig.Ui.Root(UiLayer.Overlay));
            var again = Booted(store);
            Assert.AreEqual("en", again.I18n.Lang);
            Assert.IsTrue(again.Player.Reduced);
            Assert.IsFalse(again.Player.Haptics.Enabled);
            Assert.AreEqual("Play · Level 1", Text(again, UiLayer.Home, "home.play.text"));
            // settings over the pause window
            again.Tap(HomeView.PlayId);
            again.Tap("hud.pause");
            again.Tap("win.settings");
            Assert.IsTrue(again.App.Flow.SettingsOpen);
            again.Tap("set.lang.tr");
            Assert.AreEqual("Mola", again.Ui.Root(UiLayer.Window)!.Get("win.title").Text, "the pause window under it switched too");
            Assert.AreEqual("HAMLE", again.Ui.Root(UiLayer.Hud)!.Get("hud.moves.label").Text);
        }

        [Test]
        public void K54_hud_booster_slots_locked_below_their_level_and_ready_with_the_free_trials_from_it()
        {
            var rig = Booted(StoreWithWon(9));
            rig.Tap(HomeView.PlayId);
            rig.TapOption(PanelAction.Play);
            Assert.AreEqual(10, rig.Player.LevelNumber);
            var st = rig.View.HudState!;
            Assert.AreEqual(new[] { SlotStates.Ready, SlotStates.Ready, SlotStates.Locked, SlotStates.Locked }, st.Slots,
                "GDD K-54: Bölüm 10 opens the hammer (3 trials; the level has wraps to cut) and the crane (2 trials) together (CL-V3-07)");
            Assert.AreEqual(3, st.Counts[0]);
            Assert.AreEqual(2, st.Counts[1]);
            var hud = rig.Ui.Root(UiLayer.Hud)!;
            Assert.AreNotEqual("btn_booster_off", hud.Get("hud.boost0").Frame, "an open slot with a target is not dimmed");
            Assert.AreEqual("btn_booster_off", hud.Get("hud.boost2").Frame);
            Assert.IsTrue(hud.Get("hud.boost2.lock").Visible, "locked: the padlock");
            Assert.IsFalse(hud.Get("hud.boost2.badge").Visible);
            Assert.AreEqual(3, rig.App.Save.BoosterCount("hammer"), "META §4: free trials granted once at unlock");
            Assert.AreEqual(2, rig.App.Save.BoosterCount("crane"));
            Assert.IsTrue(rig.App.Save.FreeTrialsGranted.Contains("hammer"));
            Assert.IsTrue(rig.App.Save.FreeTrialsGranted.Contains("crane"));
        }

        [Test]
        public void K54_level_1_has_every_slot_locked_and_the_hud_shows_the_counters()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            var hud = rig.Ui.Root(UiLayer.Hud)!;
            Assert.IsTrue(rig.View.HudState!.Slots.All(x => x == SlotStates.Locked));
            Assert.AreEqual("Bölüm 1", hud.Get("hud.level.text").Text);
            Assert.AreEqual(rig.Session.MovesLeft.ToString(), hud.Get("hud.moves.n").Text);
            Assert.AreEqual("HAMLE", hud.Get("hud.moves.label").Text);
            Assert.AreEqual(rig.Player.BlocksLeft.ToString(), hud.Get("hud.blocks.n").Text);
            Assert.IsTrue(hud.All().Any(n => n.Id.StartsWith("hud.pano.cell.")), "the panorama well shows the plan");
            rig.PlayGolden(1, 1);
            Assert.IsTrue(hud.Get("hud.pano.plan").Children.Any(c => c.Frame!.StartsWith("blk_")), "a built cell turns into a block");
        }

        // --- screen geometry ------------------------------------------------------------------------------------------------

        private static IEnumerable<(string Screen, UiNode Root)> EveryScreen(double w, double h, string lang = "tr")
        {
            var store = StoreWithWon(2, 5000);
            var m = SaveCodec.Load(store, SaveModel.Default(500, 5)).Data;
            m.Settings.Lang = lang;
            SaveCodec.Write(store, m);
            var rig = Booted(store, w, h);
            yield return ("home", rig.Ui.Root(UiLayer.Home)!);
            rig.Tap("home.settings");
            yield return ("settings", rig.Ui.Root(UiLayer.Overlay)!);
            rig.Tap("set.close");
            rig.Tap(HomeView.PlayId);
            yield return ("levelStart", rig.Ui.Root(UiLayer.Window)!);
            rig.TapOption(PanelAction.Play);
            yield return ("hud", rig.Ui.Root(UiLayer.Hud)!);
            rig.Tap("hud.pause");
            yield return ("pause", rig.Ui.Root(UiLayer.Window)!);
            rig.TapOption(PanelAction.Exit);
            yield return ("exit", rig.Ui.Root(UiLayer.Window)!);
            rig.TapOption(PanelAction.Stay);
            rig.TapOption(PanelAction.Resume);
            LastMove(rig, 3, 0);
            yield return ("lose", rig.Ui.Root(UiLayer.Window)!);
            rig.TapOption(PanelAction.Decline);
            yield return ("loseFinal", rig.Ui.Root(UiLayer.Window)!);
            var winStore = new MemorySaveStore();
            var wm = SaveModel.Default(500, 5);
            wm.Settings.Lang = lang;
            SaveCodec.Write(winStore, wm);
            var win = Booted(winStore, w, h);
            win.Tap(HomeView.PlayId);
            win.PlayGolden(1);
            yield return ("win", win.Ui.Root(UiLayer.Window)!);
        }

        [Test]
        public void UX03_every_tap_target_of_every_screen_is_at_least_128_px_and_inside_the_canvas()
        {
            var seen = new List<string>();
            foreach (var (name, root) in EveryScreen(1080, 1920))
            {
                seen.Add(name);
                foreach (var n in root.All())
                {
                    if (n.Action == null || !n.Shown) continue;
                    Assert.GreaterOrEqual(System.Math.Min(n.Rect.W, n.Rect.H), 128 - 1e-9, name + ": " + n);
                    Assert.That(n.Rect.X >= -1e-9 && n.Rect.Right <= 1080 + 1e-9 && n.Rect.Y >= -1e-9 && n.Rect.Bottom <= 1920 + 1e-9, name + " inside: " + n);
                }
            }
            Assert.AreEqual(new[] { "home", "settings", "levelStart", "hud", "pause", "exit", "lose", "loseFinal", "win" }, seen.ToArray());
            // an EXPAND canvas (UX §3 example 390 × 844 → H 2337): the windows anchor at H − 296, the home button and navigation at the bottom
            const double designH = 2337;
            var s = new LayoutSettings();
            var t = new MinikUsta.Presentation.I18n.Localizer(PresentationPaths.ResourceText).Text;
            var home = HomeView.Build(new HomeModel(), 1080, designH, t);
            Assert.LessOrEqual(home.Get("home.nav").Rect.Bottom, designH + 1e-9);
            Assert.AreEqual(designH - 1920, home.Get(HomeView.PlayId).Rect.Y - 1384, 1e-9);
            var pause = WindowViews.Build(new PanelModel(PanelKind.Pause, "Mola", "", new[] { new PanelButton("Devam", PanelAction.Resume, true) }), 1080, designH, s, t);
            Assert.AreEqual(designH - s.PopupPanelBottomPx, pause.Get("win.panel").Rect.Bottom, 1e-9, "UX §0.1: the panel bottom is anchored");
        }

        [Test]
        public void UX02_window_options_are_in_the_thumb_zone_and_the_primary_one_is_lowest()
        {
            foreach (var (name, root) in EveryScreen(1080, 1920))
            {
                if (name == "home" || name == "hud" || name == "settings") continue;
                var opts = root.All().Where(n => n.Action != null && n.Id.StartsWith("win.opt")).ToList();
                Assert.IsNotEmpty(opts, name);
                foreach (var o in opts) Assert.GreaterOrEqual(o.Rect.Y, 1056 - 1e-9, name + ": UX §0.1 comfortable zone " + o);
                var primary = opts.Where(o => o.Frame == "btn_primary").ToList();
                if (primary.Count > 0) Assert.AreEqual(opts.Max(o => o.Rect.Y), primary.Max(o => o.Rect.Y), 1e-9, name + ": the primary option is the lowest");
                for (int i = 0; i < opts.Count; i++)
                    for (int j = i + 1; j < opts.Count; j++) Assert.IsFalse(opts[i].Rect.Overlaps(opts[j].Rect), name + ": options overlap");
            }
        }

        [Test]
        public void UX03_every_text_of_every_screen_fits_its_rectangle_on_one_line_in_tr_and_en([Values("tr", "en")] string lang)
        {
            foreach (var (name, root) in EveryScreen(1080, 1920, lang))
                foreach (var n in root.All())
                {
                    if (n.Text == null || !n.Shown || n.Text.Length == 0) continue;
                    int size = TextFit.SizeOf(n);
                    var look = TextStyles.Of(n.Style, size, n.TextFill, n.TextStroke);
                    Assert.LessOrEqual(FontMetrics.Width(n.Text, size), n.Rect.W - 2 * look.OutlinePx + 1e-6, lang + " " + name + ": \"" + n.Text + "\" overflows " + n.Id);
                    Assert.GreaterOrEqual(size, n.FontPx * 0.7 - 1e-9, lang + " " + name + ": \"" + n.Text + "\" shrinks below 70 % in " + n.Id);
                }
        }

        [Test]
        public void ART_UNITY_every_frame_of_every_screen_is_in_the_atlas_and_every_text_is_filled()
        {
            var a = ArtContractTests.Atlas;
            foreach (var (name, root) in EveryScreen(1080, 1920))
                foreach (var n in root.All())
                {
                    if (n.Frame != null) Assert.IsTrue(a.Has(n.Frame) || TestArt.Set.Has(n.Frame), name + ": frame " + n.Frame);
                    if (n.Text != null && n.Shown && n.Id.EndsWith(".label")) Assert.IsNotEmpty(n.Text, name + ": " + n.Id);
                    if (n.Text != null) Assert.IsFalse(n.Text.Contains("{"), name + ": unfilled placeholder in " + n);
                }
        }
    }
}
