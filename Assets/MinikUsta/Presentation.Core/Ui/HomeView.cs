#nullable enable
// The home screen v2 as a UI tree (paket V1; UX_FLOWS §3 Faz 2R slice; ART_UNITY §4.4 "Ana sayfa"; the v4 mock): day sky
// background, top bar (lives in the heart, coins, settings), the logo frame (`logo_wordmark`, replaceable — D-033 / D-068),
// the area ribbon + progress (levels won / 10, UX §3 fallback), the chapter 1 tree house, Tuna, Usta Dede and Kepçe, the one
// primary button "Oyna · Bölüm N" and the bottom navigation (the four tabs other than home are locked, Faz 2R). EXPAND: the
// middle group moves down by (H − 1920) × 0,5, the button and the navigation stay at the bottom.
using System;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Meta;
using MinikUsta.Presentation.Text;

namespace MinikUsta.Presentation.Ui
{
    public sealed class HomeModel
    {
        public int Lives;
        public int LivesMax = 5;
        public int Coins;
        /// <summary>The level the button plays.</summary>
        public int NextLevel = 1;
        public int Won;
        public int Levels = 10;
        /// <summary>Every level of the slice is won: "Yeni bölümler yolda" over the button (UX §6 loop).</summary>
        public bool AllWon;
        public string Lang = "tr";
    }

    public static class HomeView
    {
        public const string PlayAction = "home:play";
        public const string SettingsAction = "home:settings";
        public const string PlayId = "home.play";

        public static UiNode Build(HomeModel m, double W, double H, Strings t)
        {
            double mid = (H - 1920) * 0.5;
            double bot = H - 1920;
            var r = UiKit.Group("home", new PxRect(0, 0, W, H));
            r.BlocksInput = true;
            // background: cover the canvas (keeps the 1080 × 1920 aspect, crops the sides on tall screens)
            double bw = Math.Max(W, H * 1080 / 1920), bh = bw * 1920 / 1080;
            r.Add(UiKit.Image("home.bg", Frames.BgHome, new PxRect((W - bw) / 2, (H - bh) / 2, bw, bh)));
            // tree house + characters (the calm middle, UX §3)
            double tw = 760 * 0.62, th = 820 * 0.62;
            r.Add(UiKit.Image("home.town", "town_ch1_treehouse", new PxRect(812 - tw / 2, 1236 + mid - th, tw, th)));
            r.Add(UiKit.Image("home.dede", "chr_dede_idle", new PxRect(330, 846 + mid, 285, 456)));
            r.Add(UiKit.Image("home.kepce", "chr_kepce_idle", new PxRect(30, 1170 + mid, 290, 200)));
            r.Add(UiKit.Image("home.tuna", "chr_tuna_cheer", new PxRect(96, 880 + mid, 330, 440)));
            // top bar: lives (count in the heart), coins (+ locked shop), settings
            var lives = r.Add(UiKit.Image("home.lives", "capsule_home", new PxRect(160, 104, 300, 88), true));
            lives.Add(UiKit.Icon("home.lives.icon", "icon_heart", 182, 148, 112, 112));
            lives.Add(UiKit.Label("home.lives.n", m.Lives.ToString(System.Globalization.CultureInfo.InvariantCulture), new PxRect(142, 116, 80, 56), TextStyle.Button, 40,
                TextAlign.Center, null, Rgba.Hex("#B80E12")));
            string livesText = m.Lives >= m.LivesMax ? t.Get("hud.livesFull") : t.Format(Strings.Keys.Count, ("n", m.Lives), ("max", m.LivesMax));
            lives.Add(UiKit.Label("home.lives.text", livesText, new PxRect(240, 104, 200, 88), TextStyle.Cream, 44));
            var coins = r.Add(UiKit.Image("home.coins", "capsule_home", new PxRect(500, 104, 300, 88), true));
            coins.Add(UiKit.Icon("home.coins.icon", "icon_coin", 522, 148, 112, 112));
            coins.Add(UiKit.Label("home.coins.n", Price.Grouped(m.Coins, m.Lang), new PxRect(580, 104, 170, 88), TextStyle.Cream, 44));
            coins.Add(UiKit.Icon("home.coins.plus", "btn_plus", 790, 148, 64, 64));
            var gear = r.Add(UiKit.Button("home.settings", "btn_tool", new PxRect(908, 84, 128, 128), SettingsAction));
            gear.Add(UiKit.Icon("home.settings.icon", "icon_settings", 972, 144, 84, 84));
            // logo (replaceable frame) + area ribbon + progress
            double lw = 680 * 0.6, lh = 400 * 0.6;
            r.Add(UiKit.Image("home.logo", "logo_wordmark", new PxRect(W / 2 - lw / 2, 196, lw, lh)));
            var ribbon = r.Add(UiKit.Image("home.area", "ribbon_title", new PxRect(230, 440, 620, 120), true));
            ribbon.Add(UiKit.Label("home.area.text", t.Get("town.ch1.title"), new PxRect(330, 448, 420, 88), TextStyle.Button, 54, TextAlign.Center, null, Rgba.Hex("#8A3A06")));
            var track = new PxRect(300, 566, 480, 84);
            r.Add(UiKit.Image("home.progress", "progress_track", track, true));
            double frac = m.Levels > 0 ? Math.Max(0, Math.Min(1, (double)m.Won / m.Levels)) : 0;
            var fill = UiKit.Image("home.progress.fill", "progress_fill", new PxRect(track.X + 8, track.Y + 10, Math.Max(80, (track.W - 16) * frac), track.H - 20), true);
            fill.Visible = m.Won > 0;
            r.Add(fill);
            r.Add(UiKit.Label("home.progress.text", t.Format(Strings.Keys.Count, ("n", m.Won), ("max", m.Levels)), track, TextStyle.Big, 40));
            r.Add(UiKit.Icon("home.progress.star", "icon_star", track.Right - 20, track.CenterY - 4, 120, 120));
            // the one primary action (UX §0.2)
            if (m.AllWon)
                r.Add(UiKit.Label("home.moreSoon", t.Get("home.moreSoon"), new PxRect(140, 1310 + bot, 800, 60), TextStyle.Label, 40));
            var play = r.Add(UiKit.Button(PlayId, "btn_primary", new PxRect(210, 1384 + bot, 660, 200), PlayAction));
            play.Add(UiKit.Label("home.play.text", t.Format("home.playLevel", m.NextLevel), new PxRect(250, 1396 + bot, 580, 150), TextStyle.Button, 72));
            // bottom navigation: home selected, the other tabs locked ("Yakında", Faz 2R)
            r.Add(UiKit.Image("home.nav", "nav_bar", new PxRect(0, 1704 + bot, W, 216), true));
            string[] tabs = { "icon_nav_shop", "icon_nav_league", "icon_nav_home", "icon_nav_team", "icon_nav_album" };
            double tabW = W / 5;
            for (int i = 0; i < 5; i++)
            {
                double cx = tabW * (i + 0.5);
                if (i == 2)
                {
                    r.Add(UiKit.Image("home.tab.on", "nav_tab_on", new PxRect(cx - 150, 1620 + bot, 300, 300)));
                    r.Add(UiKit.Icon("home.tab2", tabs[i], cx, 1717 + bot, 134, 134));
                    r.Add(UiKit.Label("home.tab2.text", t.Get("nav.home"), new PxRect(cx - 140, 1800 + bot, 280, 60), TextStyle.Label, 40, TextAlign.Center, null, Rgba.Hex("#2C1E94")));
                }
                else
                {
                    r.Add(UiKit.Icon("home.tab" + i, tabs[i], cx, 1810 + bot, 115, 115, 0.55f));
                    r.Add(UiKit.Icon("home.tab" + i + ".lock", "icon_lock", cx + 40, 1838 + bot, 52, 60));
                }
                if (i == 1 || i == 4)
                    r.Add(UiKit.Image("home.tab.div" + i, Frames.White, new PxRect(tabW * (i == 1 ? 1 : 4) - 1.5, 1772 + bot, 3, 88), false, Rgba.Hex("#3624AA")));
            }
            return r;
        }
    }

    public sealed class SettingsModel
    {
        public bool Sound = true;
        public bool Haptics = true;
        public bool ReduceMotion;
        public string Lang = "tr";
    }

    /// <summary>
    /// The settings panel (UX §11 reduced to the V1 rows; ART_UNITY §4.4 "Ayarlar"): sound, vibration, reduce motion (JUICE §0
    /// rule 8 → `LevelPlayer.Reduced`) and the language (TR / EN flags). Over the home screen or the pause window (Overlay layer).
    /// </summary>
    public static class SettingsView
    {
        public const string Prefix = "set:";
        public const string Sound = "set:sound";
        public const string Haptics = "set:haptics";
        public const string Motion = "set:motion";
        public const string LangTr = "set:lang:tr";
        public const string LangEn = "set:lang:en";
        public const string Close = "set:close";

        public static UiNode Build(SettingsModel m, double W, double H, Strings t, double panelBottom)
        {
            var root = UiKit.Group("set", new PxRect(0, 0, W, H));
            root.Add(UiKit.Dim("set.dim", W, H, WindowViews.DimSoft));
            var panel = new PxRect((W - 1000) / 2, panelBottom - 1040, 1000, 1040);
            var group = root.Add(UiKit.Group("set.group", panel));
            group.Add(new UiNode("set.panel", panel) { Frame = "panel_window", Sliced = true, BlocksInput = true });
            var ribbon = new PxRect(panel.CenterX - WindowViews.RibbonW / 2, panel.Y - WindowViews.RibbonH / 2 + 6, WindowViews.RibbonW, WindowViews.RibbonH);
            group.Add(UiKit.Image("set.ribbon", "ribbon_title", ribbon, true));
            group.Add(UiKit.Label("set.title", t.Get("settings.title"), new PxRect(ribbon.X + 120, ribbon.Y + 18, ribbon.W - 240, ribbon.H - 56), TextStyle.Button, 76,
                TextAlign.Center, null, Rgba.Hex("#8A3A06")));
            group.Add(UiKit.Button("set.close", "btn_close", new PxRect(panel.Right - 120, panel.Y - 10, 120, 120), Close, false));
            double y = panel.Y + 130;
            Row(group, "sound", m.Sound ? "icon_sound_on" : "icon_sound_off", t.Get("settings.sound"), m.Sound, Sound, panel, y);
            Row(group, "haptics", "icon_vibration", t.Get("settings.haptics"), m.Haptics, Haptics, panel, y + 156);
            Row(group, "motion", "icon_motion", t.Get("settings.reduceMotion"), m.ReduceMotion, Motion, panel, y + 312);
            // language row: two flags, the selected one on a slot frame
            double ly = y + 468;
            group.Add(UiKit.Image("set.lang.bg", "panel_plate", new PxRect(panel.X + 60, ly, 880, 140), true));
            group.Add(UiKit.Label("set.lang.label", t.Get("settings.language"), new PxRect(panel.X + 100, ly, 330, 140), TextStyle.Label, 48, TextAlign.Left));
            Flag(group, "tr", "flag_tr", t.Get("lang.tr"), m.Lang == "tr", LangTr, new PxRect(panel.X + 450, ly + 6, 230, 128));
            Flag(group, "en", "flag_en", t.Get("lang.en"), m.Lang == "en", LangEn, new PxRect(panel.X + 700, ly + 6, 230, 128));
            var ok = group.Add(UiKit.Button("set.ok", "btn_primary", new PxRect(W / 2 - 240, panel.Bottom - 64 - 176, 480, 176), Close));
            ok.Add(UiKit.Label("set.ok.text", t.Get("common.ok"), new PxRect(W / 2 - 220, panel.Bottom - 64 - 168, 440, 140), TextStyle.Button, 76));
            return root;
        }

        private static void Row(UiNode group, string id, string icon, string label, bool on, string action, PxRect panel, double y)
        {
            var row = group.Add(UiKit.Button("set." + id, "panel_plate", new PxRect(panel.X + 60, y, 880, 140), action));
            row.Pressable = false;
            row.Add(UiKit.Icon("set." + id + ".icon", icon, panel.X + 130, y + 70, 96, 96));
            row.Add(UiKit.Label("set." + id + ".label", label, new PxRect(panel.X + 200, y, 480, 140), TextStyle.Label, 46, TextAlign.Left));
            row.Add(UiKit.Icon("set." + id + ".toggle", on ? "toggle_on" : "toggle_off", panel.X + 830, y + 70, 176, 88));
        }

        private static void Flag(UiNode group, string id, string flag, string label, bool on, string action, PxRect r)
        {
            var b = group.Add(UiKit.Button("set.lang." + id, on ? "btn_slot" : "panel_card", r, action));
            b.Alpha = on ? 1f : 0.7f;
            b.Add(UiKit.Icon("set.lang." + id + ".flag", flag, r.X + 52, r.CenterY, 76, 76));
            b.Add(UiKit.Label("set.lang." + id + ".text", label, new PxRect(r.X + 92, r.Y, r.W - 100, r.H), on ? TextStyle.Label : TextStyle.Card, 34, TextAlign.Center));
        }
    }
}
