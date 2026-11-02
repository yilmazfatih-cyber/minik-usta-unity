#nullable enable
// The level windows as UI trees (paket V1; UX_FLOWS §4.1, §6.1, §7, §12; ART_REFERENCE §3.7 window anatomy: dim, 9-slice
// panel, title ribbon on the top edge, close × on the corner, options at the bottom in the thumb zone). Pure layouts in design
// px of the canvas W × H: the panel's bottom edge is anchored at H − popup.panelBottomPx (UX §0.1), so EXPAND screens move the
// whole window down. Every option is ≥ 128 px high (UX §0.3). Tap actions are "panel:<PanelAction>".
//
// The win window (Q1 open question b) never hides the finished structure: the structure is drawn INSIDE the window, on the
// card next to Tuna (`PlanThumb.Whole`, every segment as built).
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Text;

namespace MinikUsta.Presentation.Ui
{
    public static class WindowViews
    {
        public const string ActionPrefix = "panel:";
        /// <summary>ART_REFERENCE §3.7 dim: #05030D α 0,85 (win, level start) and a lighter one for the in-play windows.</summary>
        public const float DimStrong = 0.85f;
        public const float DimSoft = 0.72f;
        public const double PanelW = 1000;
        public const double RibbonW = 760;
        public const double RibbonH = 176;
        public const double CloseD = 136;
        /// <summary>Ids of the nodes the player animates.</summary>
        public const string GroupId = "win.group";
        public const string StarId = "win.star";
        public const string BurstId = "win.burst";

        public static string ActionOf(PanelAction a) => ActionPrefix + a;

        public static bool TryParse(string? action, out PanelAction a)
        {
            a = PanelAction.Resume;
            return action != null && action.StartsWith(ActionPrefix, StringComparison.Ordinal)
                && Enum.TryParse(action.Substring(ActionPrefix.Length), out a);
        }

        /// <summary>The window of `m` on a W × H canvas.</summary>
        public static UiNode Build(PanelModel m, double W, double H, LayoutSettings s, Strings t)
        {
            switch (m.Kind)
            {
                case PanelKind.Win when m.Detail != null:
                    return Win(m, W, H, s, t.Lang);
                case PanelKind.LevelStart:
                    return LevelStart(m, W, H, s, t.Lang);
                default:
                    return Stacked(m, W, H, s, t.Lang);
            }
        }

        /// <summary>The bottom edge of every window panel (UX §0.1 popup anchor).</summary>
        public static double PanelBottom(double H, LayoutSettings s) => H - s.PopupPanelBottomPx;

        // --- shared parts ---------------------------------------------------------------------------------------------

        private static (UiNode Root, UiNode Group) Shell(string kind, double W, double H, PxRect panel, string title, float dim)
        {
            var root = UiKit.Group("win." + kind, new PxRect(0, 0, W, H));
            root.Add(UiKit.Dim("win.dim", W, H, dim));
            var group = root.Add(UiKit.Group(GroupId, panel));
            group.Add(new UiNode("win.panel", panel) { Frame = "panel_window", Sliced = true, BlocksInput = true });
            if (title.Length > 0)
            {
                var ribbon = new PxRect(panel.CenterX - RibbonW / 2, panel.Y - RibbonH / 2 + 6, RibbonW, RibbonH);
                group.Add(UiKit.Image("win.ribbon", "ribbon_title", ribbon, true));
                group.Add(UiKit.Label("win.title", title, new PxRect(ribbon.X + 120, ribbon.Y + 18, ribbon.W - 240, ribbon.H - 56), TextStyle.Button, 76, TextAlign.Center, null,
                    Rgba.Hex("#8A3A06")));
            }
            return (root, group);
        }

        private static void Close(UiNode group, PxRect panel, PanelAction action)
        {
            var r = new PxRect(panel.Right - 60 - CloseD / 2, panel.Y + 50 - CloseD / 2, CloseD, CloseD);
            var b = group.Add(UiKit.Button("win.close", "btn_close", r, ActionOf(action), false));
            b.PreserveAspect = true;
        }

        /// <summary>One window option (kit button + label, optional second line and coin price).</summary>
        public static UiNode Option(string id, PanelButton b, PxRect r, string lang = "tr")
        {
            string frame;
            Rgba stroke;
            TextStyle style = TextStyle.Button;
            Rgba? fill = null;
            switch (b.Kind)
            {
                case ButtonKind.Primary:
                    frame = "btn_primary";
                    stroke = Rgba.Hex("#0E5A14");
                    break;
                case ButtonKind.Danger:
                    frame = "btn_danger";
                    stroke = Rgba.Hex("#7A0A05");
                    break;
                case ButtonKind.Offer:
                    frame = "btn_offer";
                    stroke = Rgba.Hex("#8A3A06");
                    break;
                case ButtonKind.Disabled:
                    frame = "btn_disabled";
                    stroke = Rgba.Hex("#55556A");
                    break;
                default:
                    frame = "btn_secondary";
                    stroke = Rgba.Clear;
                    style = TextStyle.Card;
                    fill = Rgba.Hex("#5B3A1A");
                    break;
            }
            var n = UiKit.Button(id, frame, r, ActionOf(b.Action), true, b.Enabled);
            bool price = b.Price >= 0;
            double labelRight = price ? r.Right - 250 : r.Right - 40;
            double size = r.H >= 170 ? 80 : 54;
            if (b.Line2 != null)
            {
                n.Add(UiKit.Label(id + ".label", b.Label, new PxRect(r.X + 40, r.Y + 14, labelRight - r.X - 40, r.H * 0.5), style, size * 0.88, price ? TextAlign.Left : TextAlign.Center, fill, stroke));
                n.Add(UiKit.Label(id + ".line2", b.Line2, new PxRect(r.X + 40, r.Y + r.H * 0.52, r.W - 80, r.H * 0.34), style, 32, price ? TextAlign.Right : TextAlign.Center, fill, stroke));
            }
            else
            {
                n.Add(UiKit.Label(id + ".label", b.Label, new PxRect(r.X + 40, r.Y + 8, labelRight - r.X - 40, r.H - 28), style, size, price ? TextAlign.Left : TextAlign.Center, fill, stroke));
            }
            if (price)
            {
                double cy = b.Line2 != null ? r.Y + r.H * 0.32 : r.Y + r.H * 0.44;
                n.Add(UiKit.Icon(id + ".coin", "icon_coin", r.Right - 220, cy, 72, 72));
                n.Add(UiKit.Label(id + ".price", Meta.Price.Grouped(b.Price, lang), new PxRect(r.Right - 180, cy - 34, 150, 68), TextStyle.Button, 50, TextAlign.Left, null, stroke));
            }
            return n;
        }

        // --- stacked windows: pause, exit, out of moves (+5 offer), loss, the Q1 text windows ----------------------------

        private static UiNode Stacked(PanelModel m, double W, double H, LayoutSettings s, string lang)
        {
            var d = m.Detail;
            int n = m.Buttons.Length;
            double optH = s.PopupOptionH, gap = s.PopupOptionGap;
            double bottom = PanelBottom(H, s);
            double optsTop = bottom - s.PopupOptionsBottomInsetPx - n * optH - Math.Max(0, n - 1) * gap;
            bool rich = d != null && (d.Character != null || d.King != null || d.Thumb != null || d.Lines.Length > 0);
            double counterH = d?.Counter != null ? 64 : 0;
            double bodyH = rich ? 330 : m.Body.Length > 0 ? s.PopupBodyH : 40;
            double top = optsTop - counterH - bodyH - 130;
            var panel = new PxRect((W - PanelW) / 2, top, PanelW, bottom - top);
            var (root, group) = Shell(m.Kind.ToString(), W, H, panel, m.Title, m.Kind == PanelKind.Win ? DimStrong : DimSoft);
            var body = new PxRect(panel.X + 60, top + 120, panel.W - 120, bodyH);
            if (rich)
            {
                double textX = body.X;
                if (d!.King != null)
                {
                    // B2 (D-101): Usta Kral beside the text (LevelHud binds his clip; the node holds the v4 still until then)
                    group.Add(KingNode(new PxRect(body.X - 16, body.Y - 40, 236, body.H + 40)));
                    textX = body.X + 220;
                }
                else if (d.Character != null)
                {
                    group.Add(UiKit.Image("win.chr", d.Character, new PxRect(body.X, body.Y, 200, body.H)).WithAspect());
                    textX = body.X + 220;
                }
                var textArea = new PxRect(textX, body.Y, body.Right - textX, body.H);
                var lines = new List<string>();
                if (m.Body.Length > 0) lines.Add(m.Body);
                lines.AddRange(d.Lines);
                double lineH = lines.Count > 0 ? Math.Min(84, (d.Thumb != null ? 84 * lines.Count : textArea.H) / lines.Count) : 0;
                for (int i = 0; i < lines.Count; i++)
                    group.Add(UiKit.Label("win.line" + i, lines[i], new PxRect(textArea.X, textArea.Y + 10 + i * lineH, textArea.W, lineH), TextStyle.Label, i == 0 ? 50 : 42));
                if (d.Thumb != null)
                {
                    // the plan picture takes the rest of the body (UX §7 preview "▣ ▣ önizleme")
                    double picTop = textArea.Y + 20 + lines.Count * lineH;
                    foreach (var c in d.Thumb.Nodes("win.thumb", new PxRect(textArea.X, picTop, textArea.W, textArea.Bottom - picTop - 6), 48)) group.Add(c);
                }
            }
            else if (m.Body.Length > 0)
            {
                group.Add(UiKit.Label("win.body", m.Body, body, TextStyle.Label, 48));
            }
            if (d?.Counter != null)
                group.Add(UiKit.Label("win.counter", d.Counter, new PxRect(panel.X + 40, optsTop - counterH, panel.W - 80, counterH - 8), TextStyle.Lavender, 38));
            for (int i = 0; i < n; i++)
            {
                double y = bottom - s.PopupOptionsBottomInsetPx - optH - i * (optH + gap);
                group.Add(Option("win.opt" + i, m.Buttons[i], new PxRect((W - s.PopupOptionW) / 2, y, s.PopupOptionW, optH), lang));
            }
            if (d?.CloseAction != null) Close(group, panel, d.CloseAction.Value);
            if (d != null && d.SettingsTool)
            {
                var r = new PxRect(panel.Right - 60 - 68, panel.Y + 50 - 68, 136, 136);
                var gear = group.Add(UiKit.Button("win.settings", "btn_tool", r, ActionOf(PanelAction.Settings), false));
                gear.Add(UiKit.Icon("win.settings.icon", "icon_settings", r.CenterX, r.CenterY - 4, 84, 84));
            }
            return root;
        }

        // --- win (UX §6.1, JUICE #55/#56/#96) ---------------------------------------------------------------------------

        public sealed class WinRects
        {
            public PxRect Panel;
            public PxRect Star;
            public PxRect Card;
            public PxRect Structure;
            public PxRect Rewards;
            public PxRect Button;
        }

        /// <summary>The win window rectangles on a W × H canvas (bottom-anchored).</summary>
        public static WinRects WinLayout(double W, double H, LayoutSettings s)
        {
            double dy = PanelBottom(H, s) - 1624;
            double x0 = (W - PanelW) / 2;
            return new WinRects
            {
                Panel = new PxRect(x0, 560 + dy, PanelW, 1064),
                Star = new PxRect(W / 2 - 115, 640 + dy, 230, 230),
                Card = new PxRect(x0 + 60, 930 + dy, 880, 280),
                Structure = new PxRect(x0 + 290, 946 + dy, 330, 248),
                Rewards = new PxRect(x0 + 60, 1228 + dy, 880, 90),
                Button = new PxRect(W / 2 - 280, 1410 + dy, 560, 180),
            };
        }

        private static UiNode Win(PanelModel m, double W, double H, LayoutSettings s, string lang)
        {
            var d = m.Detail!;
            var L = WinLayout(W, H, s);
            var (root, group) = Shell("Win", W, H, L.Panel, m.Title, DimStrong);
            double cx = W / 2, cy = L.Star.CenterY;
            // light behind the star (ART_UNITY §4.4 win row): rays + glow, warm tint; under the panel content
            var rays = UiKit.Image("win.rays", Frames.FxRays, new PxRect(cx - 620, cy - 620, 1240, 1240), false, Rgba.Hex("#FFE7A3"), 0.35f);
            var glow = UiKit.Image("win.glow", Frames.FxGlow, new PxRect(cx - 360, cy - 360, 720, 720), false, Rgba.Hex("#FFE7A3"), 0.5f);
            root.Insert(1, rays);
            root.Insert(2, glow);
            if (m.Star)
            {
                // B2 / JUICE #96: the star burst clip plays here, behind the star and over the panel (KingDirector.PlayWindowBurst)
                group.Add(new UiNode(BurstId, L.Star) { Visible = false, Isolated = true });
                group.Add(UiKit.Image(StarId, "icon_star", L.Star));
                foreach (var (x, y, z, r) in new[] { (-260.0, -40.0, 52.0, 0f), (250.0, -20.0, 60.0, 20f), (-170.0, -120.0, 38.0, 0f), (190.0, -110.0, 42.0, 0f) })
                {
                    var sp = UiKit.Image("win.spark" + (int)x, Frames.FxSpark, new PxRect(cx + x - z / 2, cy + y - z / 2, z, z), false, Rgba.Hex("#FFF3C4"));
                    sp.Rotation = r;
                    group.Add(sp);
                }
            }
            group.Add(UiKit.Label("win.clear", m.Body, new PxRect(L.Panel.X + 40, L.Star.Bottom + 4, L.Panel.W - 80, 56), TextStyle.Label, 50, TextAlign.Center, null, Rgba.Hex("#2A1D8B")));
            group.Add(UiKit.Image("win.card", "panel_card", L.Card, true));
            if (d.King != null) group.Add(KingNode(new PxRect(L.Card.X - 4, L.Card.Y - 120, 280, L.Card.H + 116)));
            else if (d.Character != null) group.Add(UiKit.Image("win.chr", d.Character, new PxRect(L.Card.X + 20, L.Card.Y - 30, 230, 306)).WithAspect());
            if (d.Thumb != null)
                foreach (var c in d.Thumb.Nodes("win.thumb", L.Structure, 64)) group.Add(c);
            // the structure's name and "Yapı tamam!" beside it (the card tells what was built)
            var cap = new PxRect(L.Structure.Right + 10, L.Card.Y + 70, L.Card.Right - L.Structure.Right - 30, 64);
            if (d.LevelName != null) group.Add(UiKit.Label("win.name", d.LevelName, cap, TextStyle.Card, 44, TextAlign.Center, Rgba.Hex("#1A467B")));
            if (d.CardCaption != null)
                group.Add(UiKit.Label("win.caption", d.CardCaption, new PxRect(cap.X, cap.Bottom + 6, cap.W, 52), TextStyle.Card, 36, TextAlign.Center, Rgba.Hex("#2E7D32")));
            // rewards: coins capsule (+ star capsule on a first win)
            var coin = new PxRect(L.Rewards.X + 80, L.Rewards.Y + 6, 300, 78);
            group.Add(UiKit.Image("win.coins", "capsule_hud", coin, true));
            group.Add(UiKit.Icon("win.coins.icon", "icon_coin", coin.X + 10, coin.CenterY, 110, 110));
            group.Add(UiKit.Label("win.coins.text", "+" + Math.Max(0, d.Coins).ToString(System.Globalization.CultureInfo.InvariantCulture), new PxRect(coin.X + 70, coin.Y, coin.W - 80, coin.H),
                TextStyle.Cream, 52));
            if (m.Star)
            {
                var star = new PxRect(L.Rewards.Right - 380, L.Rewards.Y + 6, 300, 78);
                group.Add(UiKit.Image("win.stars", "capsule_hud", star, true));
                group.Add(UiKit.Icon("win.stars.icon", "icon_star", star.X + 10, star.CenterY, 100, 100));
                group.Add(UiKit.Label("win.stars.text", "+1", new PxRect(star.X + 70, star.Y, star.W - 80, star.H), TextStyle.Cream, 52));
            }
            double ly = L.Rewards.Bottom + 8;
            if (d.BonusLine != null)
            {
                group.Add(UiKit.Label("win.bonus", d.BonusLine, new PxRect(L.Panel.X + 40, ly, L.Panel.W - 80, 40), TextStyle.Label, 34, TextAlign.Center, Rgba.Hex("#FFE96A"), Rgba.Hex("#2A1D8B")));
                ly += 40;
            }
            if (d.TrowelLine != null)
                group.Add(UiKit.Label("win.trowel", d.TrowelLine, new PxRect(L.Panel.X + 40, ly, L.Panel.W - 80, 40), TextStyle.Label, 34, TextAlign.Center, Rgba.Hex("#FFE96A"), Rgba.Hex("#2A1D8B")));
            if (m.Buttons.Length > 0) group.Add(Option("win.opt0", m.Buttons[0], L.Button, lang));
            return root;
        }

        // --- level start (UX §4.1 v3) ------------------------------------------------------------------------------------

        public static PxRect LevelStartPanel(double W, double H, LayoutSettings s)
        {
            double bottom = PanelBottom(H, s);
            return new PxRect((W - PanelW) / 2, bottom - 1040, PanelW, 1040);
        }

        private static UiNode LevelStart(PanelModel m, double W, double H, LayoutSettings s, string lang)
        {
            var d = m.Detail ?? new PanelDetail();
            var panel = LevelStartPanel(W, H, s);
            var (root, group) = Shell("LevelStart", W, H, panel, "", DimStrong);
            double y0 = panel.Y;
            // title: titleTwoRing 112 on the panel's top edge, the difficulty pill beside it (hard / superhard only)
            group.Add(UiKit.Label("win.title", m.Title, new PxRect(panel.X + 150, y0 - 70, panel.W - 300, 120), TextStyle.Title, 104));
            if (d.DifficultyText != null)
            {
                var pill = new PxRect(panel.Right - 330, y0 - 36, 190, 72);
                group.Add(UiKit.Image("win.pill", "btn_danger", pill, true));
                group.Add(UiKit.Label("win.pill.text", d.DifficultyText, new PxRect(pill.X + 10, pill.Y + 4, pill.W - 20, pill.H - 16), TextStyle.Button, 36, TextAlign.Center, null, Rgba.Hex("#7A0A05")));
            }
            var plate = new PxRect(panel.X + 60, y0 + 100, 880, 560);
            group.Add(UiKit.Image("win.plate", "panel_plate", plate, true));
            if (d.Thumb != null)
                foreach (var c in d.Thumb.Nodes("win.thumb", new PxRect(plate.X + 30, plate.Y + 30, 220, 220), 44)) group.Add(c);
            group.Add(UiKit.Label("win.name", d.LevelName ?? "", new PxRect(plate.X + 280, plate.Y + 40, 560, 70), TextStyle.Label, 52, TextAlign.Left));
            if (d.Blocks >= 0)
            {
                var chip = new PxRect(plate.X + 320, plate.Y + 140, 230, 72);
                group.Add(UiKit.Image("win.blocks", "capsule_hud", chip, true));
                group.Add(UiKit.Icon("win.blocks.icon", "icon_blocks", chip.X + 6, chip.CenterY, 90, 90));
                group.Add(UiKit.Label("win.blocks.text", "×" + d.Blocks.ToString(System.Globalization.CultureInfo.InvariantCulture), new PxRect(chip.X + 60, chip.Y, chip.W - 70, chip.H),
                    TextStyle.Cream, 46));
            }
            if (d.MovesText != null)
            {
                var chip = new PxRect(plate.X + 580, plate.Y + 140, 260, 72);
                group.Add(UiKit.Image("win.moves", "capsule_hud", chip, true));
                group.Add(UiKit.Label("win.moves.text", d.MovesText, new PxRect(chip.X + 10, chip.Y, chip.W - 20, chip.H), TextStyle.Cream, 42));
            }
            if (d.SlotsTitle != null)
                group.Add(UiKit.Label("win.slots", d.SlotsTitle, new PxRect(plate.X + 20, plate.Y + 280, plate.W - 40, 70), TextStyle.Label, 50));
            for (int i = 0; i < d.LockedSlots.Length; i++)
            {
                var r = new PxRect(panel.X + 212 + i * 232, plate.Y + 360, 184, 184);
                group.Add(UiKit.Image("win.slot" + i, "btn_slot", r, true, null, 0.85f));
                group.Add(UiKit.Icon("win.slot" + i + ".lock", "icon_lock", r.CenterX, r.Y + 74, 64, 74));
                group.Add(UiKit.Label("win.slot" + i + ".text", d.LockedSlots[i], new PxRect(r.X + 6, r.Y + 120, r.W - 12, 48), TextStyle.Lavender, 30));
            }
            if (m.Buttons.Length > 0) group.Add(Option("win.opt0", m.Buttons[0], new PxRect(W / 2 - 280, panel.Bottom - 80 - 212, 560, 212), lang));
            // B2 (D-101): the king stands left of the play button and points at it (king_point faces screen right)
            if (d.King != null) group.Add(KingNode(new PxRect(panel.X - 40, panel.Bottom - 44 - 400, 260, 400)));
            if (d.CloseAction != null) Close(group, panel, d.CloseAction.Value);
            return root;
        }

        /// <summary>B2: the window's king node (`LevelHud.WindowKingId`): the box he stands in (feet at its bottom centre), the v4 still until bound.</summary>
        private static UiNode KingNode(PxRect box)
        {
            var n = UiKit.Image(LevelHud.WindowKingId, KingDirector.FallbackFrame, box);
            n.PreserveAspect = true;
            n.Isolated = true;
            return n;
        }

        private static UiNode WithAspect(this UiNode n)
        {
            n.PreserveAspect = true;
            return n;
        }
    }
}
