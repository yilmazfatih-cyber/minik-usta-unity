#nullable enable
// Paket U4: the closures of docs/review_inbox/ux-review.md (R-01 .. R-07), measured on real CI shots of U2. One test (or more) per
// finding; the names carry the finding id. The game runs on fakes (`AppRig`), the screens are the real trees.
using System;
using System.Linq;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class UxU4Tests
    {
        private static readonly (int W, int H)[] Phones = { (390, 844), (360, 800) };

        private static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private static double Lum(Rgba c) => 0.2126 * Lin(c.R / 255.0) + 0.7152 * Lin(c.G / 255.0) + 0.0722 * Lin(c.B / 255.0);

        private static double Contrast(Rgba a, Rgba b)
        {
            double x = Lum(a), y = Lum(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        private static Rgba Mul(Rgba a, Rgba t) => new Rgba((byte)(a.R * t.R / 255), (byte)(a.G * t.G / 255), (byte)(a.B * t.B / 255), 255);

        private static AppRig InLevel(int level, int w, int h)
        {
            var rig = new AppRig(null, w, h);
            rig.App.Boot();
            Assert.IsTrue(rig.App.StartLevel(level));
            rig.Settle();
            return rig;
        }

        // --- R-01 the scene band -------------------------------------------------------------------------------------------------

        [Test]
        public void U4_R01_scene_band_is_empty_at_H_1920()
        {
            foreach (int level in Play.Levels)
            {
                var L = ScreenLayout.Create(new LayoutSettings(), 1920, Play.LevelFile(level).Geo);
                Assert.AreEqual(0, L.SceneBand.H, "B" + level);
            }
        }

        [Test]
        public void U4_R01_scene_band_sits_24_px_over_the_crane_area_and_is_at_most_420_px()
        {
            var s = new LayoutSettings();
            foreach (var (w, h) in Phones)
                foreach (int level in Play.Levels)
                {
                    var rig = InLevel(level, w, h);
                    var L = rig.Player.Layout!;
                    Assert.Greater(L.H, 1920);
                    var b = L.SceneBand;
                    if (b.H == 0) continue;
                    Assert.LessOrEqual(b.H, 420, "B" + level);
                    Assert.GreaterOrEqual(b.H, s.SceneBandMinPx);
                    Assert.AreEqual(L.Grid.CraneTopY - 24, b.Bottom, 1e-6, "bottom edge 24 px above the crane area");
                    Assert.GreaterOrEqual(b.Y - L.TopGroupBottomY, 24 - 1e-6, "24 px under the HUD");
                    Assert.AreEqual(Math.Min(420, L.Grid.CraneTopY - 24 - L.TopGroupBottomY - 24), b.H, 1e-6);
                }
        }

        [Test]
        public void U4_R01_a_tall_phone_gets_a_band_in_every_test_level_and_the_hud_tree_draws_the_silhouette()
        {
            foreach (var (w, h) in Phones)
                foreach (int level in Play.Levels)
                {
                    var rig = InLevel(level, w, h);
                    var L = rig.Player.Layout!;
                    Assert.Greater(L.SceneBand.H, 0, w + "x" + h + " B" + level + ": the free band is used");
                    var hud = rig.Ui.Root(UiLayer.Hud)!;
                    var group = hud.Get(SceneBand.GroupId);
                    Assert.IsTrue(group.Shown);
                    var cells = hud.Get(SceneBand.PlanId).Children;
                    Assert.Greater(cells.Count, 0, "the structure silhouette is drawn");
                    foreach (var c in cells)
                        Assert.IsTrue(c.Rect.X >= L.SceneBand.X - 1e-6 && c.Rect.Right <= L.SceneBand.Right + 1e-6 && c.Rect.Y >= L.SceneBand.Y - 1e-6 && c.Rect.Bottom <= L.SceneBand.Bottom + 1e-6, "cell inside the band");
                    Assert.IsNotNull(group.Find("hud.scene.glow"), "radial glow");
                }
        }

        [Test]
        public void U4_R01_scene_band_hidden_at_1080x1920_and_cells_turn_from_silhouette_to_block_as_the_build_grows()
        {
            var flat = InLevel(1, 1080, 1920);
            Assert.IsFalse(flat.Ui.Root(UiLayer.Hud)!.Get(SceneBand.GroupId).Shown, "no band at H 1920");
            var rig = InLevel(1, 390, 844);
            var start = rig.Session.State;
            var empty = SceneBand.Cells(PlanThumb.Visible(start), rig.Player.Layout!.SceneBand);
            var whole = SceneBand.Cells(PlanThumb.Of(start, 0, 1, true), rig.Player.Layout!.SceneBand);
            Assert.AreEqual(empty.Count, whole.Count, "same cells, other frames");
            Assert.AreEqual(0, empty.Count(c => c.Frame!.StartsWith("blk_", StringComparison.Ordinal)), "nothing built at the start");
            Assert.AreEqual(whole.Count, whole.Count(c => c.Frame!.StartsWith("blk_", StringComparison.Ordinal)), "everything built");
            Assert.IsTrue(empty.All(c => c.Alpha < 1f), "the silhouette is a translucent dark placeholder");
        }

        // --- R-02 locked tab names -----------------------------------------------------------------------------------------------

        [Test]
        public void U4_R02_locked_tab_names_are_full_opacity_white_and_at_least_4_5_to_1_on_the_nav_bar()
        {
            var rig = new AppRig(null, 390, 844);
            rig.App.Boot();
            var home = rig.Ui.Root(UiLayer.Home)!;
            foreach (int i in new[] { 0, 1, 3, 4 })
            {
                var name = home.Get("home.tab" + i + ".text");
                Assert.AreEqual(1f, name.Alpha);
                Assert.AreEqual(Rgba.White, name.TextFill);
                Assert.GreaterOrEqual(Contrast(Rgba.White, Rgba.Hex("#6858F8")), 4.5, "white on the nav bar");
                Assert.AreEqual(0.55f, home.Get("home.tab" + i).Alpha, 1e-6, "only the icon fades");
            }
        }

        // --- R-03 offer counter ---------------------------------------------------------------------------------------------------

        [Test]
        public void U4_R03_offer_counter_is_cream_with_an_indigo_stroke_and_reads_4_5_to_1_on_the_window_plate()
        {
            var rig = new AppRig(null, 390, 844);
            rig.App.Boot();
            var d = new PanelDetail { Counter = "Teklif 2/3" };
            var m = new PanelModel(PanelKind.Lose, "t", "b", new[] { new PanelButton("x", PanelAction.Decline, ButtonKind.Secondary) }, false, d);
            var counter = WindowViews.Build(m, 1080, 2337, new LayoutSettings(), rig.I18n.Text).Get("win.counter");
            Assert.AreEqual(Rgba.Hex("#FFF6D2"), counter.TextFill);
            Assert.AreEqual(Rgba.Hex("#2A1D8B"), counter.TextStroke);
            Assert.GreaterOrEqual(Contrast(Rgba.Hex("#FFF6D2"), Rgba.Hex("#6058E0")), 4.5);
        }

        [Test]
        public void U4_R03_the_first_offer_shows_no_counter_and_the_window_is_shorter_by_it()
        {
            var rig = new AppRig(null, 390, 844);
            rig.App.Boot();
            var one = new PanelModel(PanelKind.Lose, "t", "b", new[] { new PanelButton("x", PanelAction.Decline, ButtonKind.Secondary) }, false, new PanelDetail { Counter = null });
            var two = new PanelModel(PanelKind.Lose, "t", "b", new[] { new PanelButton("x", PanelAction.Decline, ButtonKind.Secondary) }, false, new PanelDetail { Counter = "Teklif 2/3" });
            var s = new LayoutSettings();
            var a = WindowViews.Build(one, 1080, 2337, s, rig.I18n.Text);
            var b = WindowViews.Build(two, 1080, 2337, s, rig.I18n.Text);
            Assert.IsNull(a.Find("win.counter"));
            Assert.Greater(a.Get("win.panel").Rect.Y, b.Get("win.panel").Rect.Y);
        }

        // --- R-04 plate tint -----------------------------------------------------------------------------------------------------

        [Test]
        public void U4_R04_plate_tint_makes_white_text_at_least_4_5_to_1_on_the_light_purple_plate()
        {
            foreach (var baseColor in new[] { "#A090F8", "#9888F8" })
                Assert.GreaterOrEqual(Contrast(Rgba.White, Mul(Rgba.Hex(baseColor), UiKit.PlateTint)), 4.5, "white on " + baseColor + " x tint");
        }

        [Test]
        public void U4_R04_settings_rows_and_the_level_start_plate_use_the_plate_tint()
        {
            var rig = new AppRig(null, 390, 844);
            rig.App.Boot();
            rig.Tap("home.settings");
            var set = rig.Ui.Root(UiLayer.Overlay)!;
            foreach (var id in new[] { "set.sound", "set.haptics", "set.motion", "set.lang.bg" })
                Assert.AreEqual(UiKit.PlateTint, set.Get(id).Tint, id);
            var rig2 = new AppRig(null, 390, 844);
            rig2.App.Boot();
            for (int n = 1; n <= 2; n++) rig2.App.Save.Level(n).Won = true;
            rig2.Tap(HomeView.PlayId);
            Assert.AreEqual(UiKit.PlateTint, rig2.Ui.Root(UiLayer.Window)!.Get("win.plate").Tint);
        }

        // --- R-05 pre-level slots -------------------------------------------------------------------------------------------------

        private static UiNode LevelStartWindow(int level, out AppRig rig)
        {
            rig = new AppRig(null, 390, 844);
            rig.App.Boot();
            for (int n = 1; n < level; n++) rig.App.Save.Level(n).Won = true;
            rig.Tap(HomeView.PlayId);
            Assert.AreEqual(AppScreen.LevelStart, rig.Screen);
            return rig.Ui.Root(UiLayer.Window)!;
        }

        [Test]
        public void U4_R05_level_3_has_no_pre_level_slot_so_the_title_and_row_go_and_the_window_is_240_px_shorter()
        {
            var w = LevelStartWindow(3, out var rig);
            Assert.IsNull(w.Find("win.slot0"), "slots are 9 .. 17 levels away: K3 hides them");
            Assert.IsNull(w.Find("win.slots"), "the title goes with the row");
            var s = new LayoutSettings();
            Assert.AreEqual(WindowViews.LevelStartPanel(1080, ScreenFit.DesignHeight(s.Mode, 390, 844, s), s).H - 240, w.Get("win.panel").Rect.H);
            var plate = w.Get("win.plate").Rect;
            var thumb = w.All().Where(n => n.Id.StartsWith("win.thumb", StringComparison.Ordinal)).ToList();
            Assert.Greater(thumb.Count, 0);
            Assert.GreaterOrEqual(thumb.Min(n => n.Rect.Y), plate.Y);
            Assert.LessOrEqual(thumb.Max(n => n.Rect.Bottom), plate.Bottom);
        }

        [Test]
        public void U4_R05_a_slot_shows_only_two_levels_before_its_unlock_with_a_34_px_label_under_it()
        {
            var w = LevelStartWindow(10, out _); // thermos unlocks at 12
            Assert.IsNotNull(w.Find("win.slot0"));
            Assert.IsNull(w.Find("win.slot1"), "trowelStart (16) is 6 levels away");
            Assert.IsNotNull(w.Find("win.slots"));
            var slot = w.Get("win.slot0").Rect;
            var label = w.Get("win.slot0.text");
            Assert.GreaterOrEqual(label.FontPx, 34, "AC-5");
            Assert.AreEqual(slot.Bottom + 8, label.Rect.Y, 1e-6, "under the slot, not on its bevel");
            Assert.AreEqual("Bölüm 12", label.Text);
            var plate = w.Get("win.plate").Rect;
            Assert.LessOrEqual(label.Rect.Bottom, plate.Bottom, "inside the plate");
            var lock0 = w.Get("win.slot0.lock").Rect;
            Assert.AreEqual(slot.CenterY, lock0.CenterY, 1e-6, "lock icon in the middle of the slot");
        }

        // --- R-06 right-edge safety ----------------------------------------------------------------------------------------------

        [Test]
        public void U4_R06_hud_row_1_keeps_40_px_on_the_right_and_the_trowel_badge_stays_inside_the_pill()
        {
            foreach (var (w, h) in Phones)
                foreach (int level in Play.Levels)
                {
                    var rig = InLevel(level, w, h);
                    var L = rig.Player.Layout!;
                    Assert.AreEqual(1080 - 40, L.Goals.Right, 1e-6, "right inner padding 40");
                    var s = HudLayout.Streak(L);
                    var badge = HudLayout.TrowelCount(L);
                    Assert.LessOrEqual(badge.Right, s.Right - 8, "badge overflow 0");
                    Assert.GreaterOrEqual(badge.X, s.X);
                    Assert.LessOrEqual(HudLayout.TrowelIcon(L).Right, s.Right - 8);
                }
        }

        [Test]
        public void U4_R06_gold_chip_keeps_24_px_from_the_moves_plate_and_the_pause_tool()
        {
            foreach (int level in Play.Levels)
            {
                var L = ScreenLayout.Create(new LayoutSettings(), 2337, Play.LevelFile(level).Geo);
                var chip = HudLayout.GoldChip(L);
                Assert.GreaterOrEqual(chip.X - L.Moves.Right, 24 - 1e-6);
                Assert.GreaterOrEqual(L.Pause.X - chip.Right, 24 - 1e-6);
            }
        }

        // --- R-07 exit windows and the pause tool ----------------------------------------------------------------------------------

        [Test]
        public void U4_R07_the_pause_tool_is_144_px_and_leave_is_cream_secondary_like_quit()
        {
            foreach (var (w, h) in Phones)
            {
                var rig = InLevel(1, w, h);
                var pause = rig.Ui.Root(UiLayer.Hud)!.Get("hud.pause");
                Assert.AreEqual(144, pause.Rect.W);
                Assert.AreEqual(144, pause.Rect.H);
                Assert.LessOrEqual(pause.Rect.Right, 1080 - 48 + 1e-6, "still inside the HUD margin");
            }
            var r = new AppRig(null, 390, 844);
            r.App.Boot();
            r.Tap(HomeView.PlayId);
            Assert.AreEqual(AppScreen.Level, r.Screen);
            r.Tap("hud.pause");
            r.TapOption(PanelAction.Exit);
            Assert.AreEqual(AppScreen.ExitConfirm, r.Screen);
            Assert.AreEqual("btn_secondary", r.Option(PanelAction.Leave).Frame, "the same decision is drawn the same way in both windows");
            Assert.AreEqual("btn_primary", r.Option(PanelAction.Stay).Frame);
        }
    }
}
