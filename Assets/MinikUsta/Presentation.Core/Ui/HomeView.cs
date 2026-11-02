#nullable enable
// The home screen v2 as a UI tree (paket V1; UX_FLOWS §3 Faz 2R slice; ART_UNITY §4.4 "Ana sayfa"; the v4 mock): day sky
// background, top bar (lives in the heart, coins, settings), the logo frame (`logo_wordmark`, replaceable — D-033 / D-068),
// the area ribbon + progress (levels won / 10, UX §3 fallback), the chapter 1 tree house, Usta Kral (B2, D-101: the main
// symbol, his clips drawn by `KingDirector`; a tap cheers), Tuna and Kepçe, the one primary button "Oyna · Bölüm N" and the
// bottom navigation (the four tabs other than home are locked, Faz 2R). B2: the 3D Renkli Tepe background as its 7 parallax
// layers (fx.json `layers`, drifting slowly with time — `HomeParallax`; no device tilt), else the flattened `bg_home`.
// Art turn 1 (ART_3D §14.3): the tree house grows with the levels won — the bare tree `town_ch1_s00` plus `town_ch1_p01 … p(won)` all
// drawn with their stage pivot at one anchor (fx.json `stages.town_ch1`; 10 / 10 equals `town_ch1_treehouse`); Tuna and Kepçe are 3D
// idle loops (crew.json, `CrewDirector`) standing on their old box's bottom centre; the locked tabs are α 0,7 with a 0,6× lock badge;
// the Play button throws a 14 px drop shadow (α 0,45) onto the cobbled square of `bg_home_ground`.
// EXPAND: the middle group moves down by (H − 1920) × 0,5, the button and the navigation stay at the bottom.
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Anim;
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
        /// <summary>B2: the 3D parallax layers of fx.json (null / empty = the flattened `bg_home`).</summary>
        public IReadOnlyList<ParallaxLayer>? Layers;
        /// <summary>Art turn 1: the staged tree house (fx.json `stages.town_ch1`); null = the finished `town_ch1_treehouse` still.</summary>
        public StageSet? Stage;
    }

    public static class HomeView
    {
        public const string PlayAction = "home:play";
        public const string SettingsAction = "home:settings";
        public const string PlayId = "home.play";
        public const string KingAction = "home:king";
        public const string KingId = "home.king";
        public const string LayerPrefix = "home.bg.";
        /// <summary>B2: the parallax drift amplitude (design px at parallax 1); every layer is drawn this much wider on each side × its factor.</summary>
        public const double ParallaxAmpPx = 22;

        /// <summary>The home king's box on a canvas `H` tall (feet at its bottom centre; B2).</summary>
        public static PxRect KingBox(double H) => new PxRect(468 - 190, 1342 + (H - 1920) * 0.5 - 600, 380, 600);

        /// <summary>Art turn 1: the v4 boxes of Tuna and Kepçe (the fallback stills sit in them); the 3D clips stand on the bottom centre.</summary>
        public static PxRect TunaBox(double H) => new PxRect(28, 966 + (H - 1920) * 0.5, 270, 360);

        public static PxRect KepceBox(double H) => new PxRect(170, 1196 + (H - 1920) * 0.5, 200, 138);

        /// <summary>Where Tuna's and Kepçe's feet stand (the bottom centre of their v4 boxes; design px, y down).</summary>
        public static Vec2 TunaFeet(double H) => new Vec2((float)TunaBox(H).CenterX, (float)TunaBox(H).Bottom);

        public static Vec2 KepceFeet(double H) => new Vec2((float)KepceBox(H).CenterX, (float)KepceBox(H).Bottom);

        /// <summary>The tree house: its stage pivot (the bottom centre of the old 760 × 820 box) and the size of the stage canvas on screen.</summary>
        public const double TownScale = 0.62;

        public static Vec2 TownAnchor(double H) => new Vec2(812, (float)(1236 + (H - 1920) * 0.5));

        /// <summary>Ids of the tree house nodes: `home.town` (the finished still) or `home.town.base` + `home.town.p01 …`.</summary>
        public const string TownId = "home.town";
        public const string TownBaseId = "home.town.base";
        public const string PlayShadowId = "home.play.shadow";
        /// <summary>Art turn 1: locked tab icons (α) and the lock badge (× size); the Play button's drop shadow (offset px, α).</summary>
        public const float LockedTabAlpha = 0.7f;
        public const double LockBadgeScale = 0.6;
        public const double PlayShadowPx = 14;
        public const float PlayShadowAlpha = 0.45f;

        public static UiNode Build(HomeModel m, double W, double H, Strings t)
        {
            double mid = (H - 1920) * 0.5;
            double bot = H - 1920;
            var r = UiKit.Group("home", new PxRect(0, 0, W, H));
            r.BlocksInput = true;
            // background: cover the canvas (keeps the 1080 × 1920 aspect, crops the sides on tall screens)
            double bw = Math.Max(W, H * 1080 / 1920), bh = bw * 1920 / 1080;
            double ox = (W - bw) / 2, oy = (H - bh) / 2, k = bw / 1080;
            if (m.Layers != null && m.Layers.Count > 0)
            {
                foreach (var l in m.Layers)
                {
                    var lr = l.TopLeftRect;
                    double over = l.Parallax * ParallaxAmpPx;
                    var n = UiKit.Image(LayerPrefix + l.Name, l.Frame, new PxRect(ox + (lr.X - over) * k, oy + lr.Y * k, (lr.W + 2 * over) * k, lr.H * k));
                    n.Isolated = l.Parallax > 0;
                    r.Add(n);
                }
            }
            else r.Add(UiKit.Image("home.bg", Frames.BgHome, new PxRect(ox, oy, bw, bh)));
            // tree house + characters (the calm middle, UX §3): Usta Kral in front, Tuna and Kepçe beside him
            if (m.Stage != null)
            {
                // art turn 1: the bare tree + one part per level won, every frame drawn by its stage pivot at one anchor (no offsets)
                var at = TownAnchor(H);
                var frames = m.Stage.Frames(m.Won);
                for (int i = 0; i < frames.Count; i++)
                {
                    string id = i == 0 ? TownBaseId : TownId + ".p" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                    r.Add(UiKit.Image(id, frames[i].Name, MinikUsta.Presentation.Anim.ClipPlacement.Rect(frames[i], at.X, at.Y, TownScale)));
                }
            }
            else
            {
                double tw = 760 * TownScale, th = 820 * TownScale;
                r.Add(UiKit.Image(TownId, "town_ch1_treehouse", new PxRect(812 - tw / 2, 1236 + mid - th, tw, th)));
            }
            var tuna = UiKit.Image(CrewDirector.TunaId, CrewDirector.TunaFallbackFrame, TunaBox(H));
            tuna.PreserveAspect = true;
            tuna.Isolated = true;
            r.Add(tuna);
            var kepce = UiKit.Image(CrewDirector.KepceId, CrewDirector.KepceFallbackFrame, KepceBox(H));
            kepce.PreserveAspect = true;
            kepce.Isolated = true;
            r.Add(kepce);
            var king = UiKit.Button(KingId, KingDirector.FallbackFrame, KingBox(H), KingAction, false);
            king.PreserveAspect = true;
            king.Isolated = true;
            r.Add(king);
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
            var playRect = new PxRect(210, 1384 + bot, 660, 200);
            // art turn 1: the button sits on the cobbled square; a soft drop shadow (14 px down, α 0,45) seats it there
            r.Add(UiKit.Image(PlayShadowId, "btn_primary", new PxRect(playRect.X, playRect.Y + PlayShadowPx, playRect.W, playRect.H), true, Rgba.Hex("#0B0918"), PlayShadowAlpha));
            var play = r.Add(UiKit.Button(PlayId, "btn_primary", playRect, PlayAction));
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
                    r.Add(UiKit.Icon("home.tab" + i, tabs[i], cx, 1810 + bot, 115, 115, LockedTabAlpha));
                    r.Add(UiKit.Icon("home.tab" + i + ".lock", "icon_lock", cx + 40, 1838 + bot, 52 * LockBadgeScale, 60 * LockBadgeScale));
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
            group.Add(UiKit.Button("set.close", "btn_close", new PxRect(panel.Right - 128, panel.Y - 18, 136, 136), Close, false));
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
