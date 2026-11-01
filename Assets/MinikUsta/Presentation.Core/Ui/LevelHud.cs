#nullable enable
// The level HUD v3 as a UI tree (paket V1; ART_REFERENCE §9.4; ART_UNITY §4.4; UX_FLOWS §5.1, §5.9, §5.10; GDD K-54):
//   row 1  "Bölüm N" capsule · blocks-left chip (icon_blocks overflowing) · Usta Serisi capsule (4 beads + Golden Trowel ×n)
//   row 2  panorama well (level name, star, plan picture of the visible segment, segment counter) · moves plate · pause tool
//   bottom Tuna + Kepçe corner · 4 booster slots (K-54: locked / no target / empty "+" / ready with a count) · truck chip
// It implements the HUD half of `ILevelView` over an `IUiSurface` (Unity: uGUI), so the Unity layer only forwards calls; the
// level windows are built by `WindowViews` on the Window layer. Pure: the tests drive it with a fake surface.
using System;
using System.Collections.Generic;
using System.Globalization;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Text;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Ui
{
    public sealed class LevelHud
    {
        public const string PauseAction = "hud:pause";
        public const string BoosterAction = "hud:booster:";
        /// <summary>UX §5.1 slot icons, `HudState.SlotOrder`.</summary>
        public static readonly string[] SlotIcons = { "icon_hammer", "icon_crane", "icon_brush", "icon_undo" };
        private const int Beads = 4;

        private readonly IUiSurface surface;
        private readonly Strings text;
        private readonly LayoutSettings settings;
        private readonly Action onPause;
        private readonly ProxyTarget moves = new ProxyTarget();
        private readonly ProxyTarget blocks = new ProxyTarget();
        private readonly ProxyTarget trowel = new ProxyTarget();
        private readonly ProxyTarget gold = new ProxyTarget();
        private readonly ProxyTarget panel = new ProxyTarget();
        private readonly ProxyTarget star = new ProxyTarget();
        private readonly ProxyTarget[] beads = new ProxyTarget[Beads];
        private UiNode? root;
        private UiNode? window;
        private ScreenLayout? layout;
        private double canvasW = 1080;
        private double canvasH = 1920;
        private Action<PanelAction>? onAction;
        private HudState? last;

        public LevelHud(IUiSurface surface, Strings text, LayoutSettings settings, Action onPause)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            this.text = text;
            this.settings = settings;
            this.onPause = onPause;
            for (int i = 0; i < Beads; i++) beads[i] = new ProxyTarget();
        }

        public UiNode? Root => root;
        public UiNode? Window => window;
        public PanelModel? Shown { get; private set; }
        public Vec2? StarCenter { get; private set; }

        public ITweenTarget MovesChip => moves;
        public ITweenTarget BlocksChip => blocks;
        public ITweenTarget TrowelIcon => trowel;
        public ITweenTarget GoldChip => gold;
        public ITweenTarget PanelTarget => panel;
        public ITweenTarget StarTarget => star;
        public ITweenTarget StreakBead(int i) => beads[Math.Max(0, Math.Min(Beads - 1, i))];

        private UiNode N(string id) => root!.Get(id);

        /// <summary>(Re)builds the HUD for a layout (level start, screen change).</summary>
        public void Layout(ScreenLayout L)
        {
            layout = L;
            canvasW = L.W;
            canvasH = L.H;
            var r = UiKit.Group("hud", new PxRect(0, 0, L.W, L.H));
            // row 1: level capsule
            var cap = r.Add(UiKit.Image("hud.level", "capsule_hud", HudLayout.Capsule(L), true));
            cap.Add(UiKit.Label("hud.level.text", "", HudLayout.Capsule(L), TextStyle.Lavender, 38));
            // blocks-left chip (the icon overflows the capsule's left end)
            var bc = HudLayout.BlocksChip(L);
            var chip = r.Add(UiKit.Image("hud.blocks", "capsule_hud", bc, true));
            chip.Add(UiKit.Icon("hud.blocks.icon", "icon_blocks", bc.X - 2, bc.CenterY - 2, 82, 82));
            chip.Add(UiKit.Label("hud.blocks.n", "", new PxRect(bc.X + 52, bc.Y, 84, bc.H), TextStyle.Cream, 44));
            chip.Add(UiKit.Label("hud.blocks.label", "", new PxRect(bc.X + 132, bc.Y + 2, bc.W - 140, bc.H), TextStyle.Lavender, 32, TextAlign.Left));
            // streak capsule
            var sc = HudLayout.Streak(L);
            var streak = r.Add(UiKit.Image("hud.streak", "capsule_hud", sc, true));
            streak.Add(UiKit.Label("hud.streak.label", "", HudLayout.StreakLabel(L), TextStyle.Lavender, 30));
            for (int i = 0; i < Beads; i++) streak.Add(UiKit.Image("hud.bead" + i, "bead_off", HudLayout.Bead(L, i)));
            streak.Add(UiKit.Image("hud.trowel", "icon_gold_trowel", HudLayout.TrowelIcon(L)));
            streak.Add(UiKit.Label("hud.trowel.n", "", HudLayout.TrowelCount(L), TextStyle.Cream, 28));
            // row 2: panorama well
            var pw = r.Add(UiKit.Image("hud.pano", "hud_well", L.Panorama, true));
            var nameR = HudLayout.LevelName(L);
            pw.Add(UiKit.Label("hud.pano.name", "", nameR, TextStyle.Lavender, 28));
            pw.Add(UiKit.Icon("hud.pano.star", "icon_star_empty", nameR.CenterX - 26, nameR.Bottom + 28, 48, 48));
            var seg = pw.Add(UiKit.Label("hud.pano.seg", "", new PxRect(nameR.CenterX - 6, nameR.Bottom + 6, 70, 46), TextStyle.Cream, 30, TextAlign.Left));
            seg.Visible = false;
            pw.Add(UiKit.Group("hud.pano.plan", HudLayout.PanoramaPlan(L)));
            // moves plate
            var mr = HudLayout.Moves(L);
            var mp = r.Add(UiKit.Image("hud.moves", "hud_moves_plate", mr, true));
            mp.Add(UiKit.Label("hud.moves.label", "", new PxRect(mr.X, mr.Y + 4, mr.W, settings.MovesBandH - 4), TextStyle.Card, 30, TextAlign.Center, Rgba.Hex(Palette.MovesCream)));
            mp.Add(UiKit.Label("hud.moves.n", "", new PxRect(mr.X, mr.Y + settings.MovesBandH - 2, mr.W, mr.H - settings.MovesBandH - 6), TextStyle.Big, 80));
            // gold chip (JUICE #56): hidden until a win counts gold
            var gc = HudLayout.GoldChip(L);
            var g = r.Add(UiKit.Image("hud.gold", "capsule_hud", gc, true));
            g.Add(UiKit.Icon("hud.gold.icon", "icon_coin", gc.X + 10, gc.CenterY, 64, 64));
            g.Add(UiKit.Label("hud.gold.n", "", new PxRect(gc.X + 46, gc.Y, gc.W - 54, gc.H), TextStyle.Cream, 40));
            g.Visible = false;
            // pause tool (UX §0.3: 136 px target)
            var pr = HudLayout.Pause(L);
            var pb = r.Add(UiKit.Button("hud.pause", "btn_tool", pr, PauseAction));
            pb.Add(UiKit.Icon("hud.pause.icon", "icon_pause", pr.CenterX, pr.CenterY - 4, 80, 80));
            // bottom: characters, booster slots, truck chip
            var cr = L.Character;
            r.Add(UiKit.Image("hud.tuna", "chr_tuna_idle", new PxRect(cr.X - 10, L.H - 336, 200, 320)));
            r.Add(UiKit.Image("hud.kepce", "chr_kepce_idle", new PxRect(cr.X + 104, L.H - 172, 190, 131)));
            for (int i = 0; i < L.Boosters.Length && i < SlotIcons.Length; i++)
            {
                var b = L.Boosters[i];
                var slot = r.Add(UiKit.Button("hud.boost" + i, "btn_booster_off", b, BoosterAction + HudState.SlotOrder[i], true));
                slot.Add(UiKit.Image("hud.boost" + i + ".icon", SlotIcons[i], new PxRect(b.X + 24, b.Y + 14, 112, 112)));
                var badge = slot.Add(UiKit.Image("hud.boost" + i + ".badge", "badge_count", new PxRect(b.X + 112, b.Y + 110, 68, 72)));
                badge.Add(UiKit.Label("hud.boost" + i + ".n", "", new PxRect(b.X + 112, b.Y + 112, 68, 60), TextStyle.Button, 42, TextAlign.Center, null, Rgba.Hex("#7A0A05")));
                slot.Add(UiKit.Image("hud.boost" + i + ".plus", "btn_plus", new PxRect(b.X + 114, b.Y + 114, 64, 64)));
                slot.Add(UiKit.Image("hud.boost" + i + ".lock", "icon_lock", new PxRect(b.X + 106, b.Y + 100, 56, 64)));
            }
            var tc = HudLayout.TruckChip(L);
            var truck = r.Add(UiKit.Image("hud.truck", "capsule_hud", tc, true));
            truck.Add(UiKit.Image("hud.truck.icon", Frames.Truck, new PxRect(tc.X + 10, tc.Y - 4, 120, 67)));
            truck.Add(UiKit.Label("hud.truck.n", "", new PxRect(tc.X + 140, tc.Y, tc.W - 150, tc.H), TextStyle.Cream, 40));
            truck.Visible = false;
            root = r;
            surface.Show(UiLayer.Hud, r, OnTap);
            moves.Inner = surface.Target(N("hud.moves"));
            blocks.Inner = surface.Target(N("hud.blocks"));
            trowel.Inner = surface.Target(N("hud.trowel"));
            gold.Inner = surface.Target(N("hud.gold"));
            for (int i = 0; i < Beads; i++) beads[i].Inner = surface.Target(N("hud.bead" + i));
            if (last != null) SetHudState(last);
            if (Shown != null && onAction != null) ShowPanel(Shown, onAction);
        }

        private void Set(string id, string value)
        {
            if (root == null) return;
            var n = N(id);
            if (n.Text == value) return;
            n.Text = value;
            surface.Refresh(n);
        }

        private void SetFrame(string id, string frame, float alpha = 1f)
        {
            if (root == null) return;
            var n = N(id);
            if (n.Frame == frame && n.Alpha == alpha) return;
            n.Frame = frame;
            n.Alpha = alpha;
            surface.Refresh(n);
        }

        private void SetVisible(string id, bool on)
        {
            if (root == null) return;
            var n = N(id);
            if (n.Visible == on) return;
            n.Visible = on;
            surface.Refresh(n);
        }

        private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

        // --- ILevelView HUD members -------------------------------------------------------------------------------------

        public void SetLevel(string label, string name)
        {
            Set("hud.level.text", label);
            Set("hud.pano.name", name);
        }

        public void SetMoves(int n, string label, bool danger)
        {
            Set("hud.moves.label", TrText.Upper(label));
            if (root == null) return;
            var num = N("hud.moves.n");
            var fill = danger ? Rgba.Hex(Palette.GhostInvalid) : (Rgba?)null;
            if (num.Text == Num(n) && Nullable.Equals(num.TextFill, fill)) return;
            num.Text = Num(n);
            num.TextFill = fill;
            surface.Refresh(num);
        }

        public void SetBlocks(int n, string label, string segments)
        {
            Set("hud.blocks.n", Num(n));
            Set("hud.blocks.label", label);
            Set("hud.pano.seg", segments);
        }

        public void SetStreak(int beadsOn, int trowels, string label, string trowelText)
        {
            Set("hud.streak.label", label);
            for (int i = 0; i < Beads; i++) SetFrame("hud.bead" + i, i < beadsOn ? "bead_on" : "bead_off");
            SetFrame("hud.trowel", "icon_gold_trowel", trowels > 0 ? 1f : 0.4f);
            Set("hud.trowel.n", trowels > 0 ? trowelText : "");
        }

        public void SetGold(int goldShown)
        {
            SetVisible("hud.gold", goldShown >= 0);
            if (goldShown >= 0) Set("hud.gold.n", Num(goldShown));
        }

        /// <summary>K-54 slots, the panorama picture, the truck chip and the level star.</summary>
        public void SetHudState(HudState st)
        {
            last = st;
            if (root == null || layout == null) return;
            for (int i = 0; i < SlotIcons.Length && i < st.Slots.Length; i++)
            {
                string state = st.Slots[i];
                bool ready = state == SlotStates.Ready, empty = state == SlotStates.Empty, locked = state == SlotStates.Locked;
                var slot = N("hud.boost" + i);
                string frame = ready || empty ? "btn_booster" : "btn_booster_off";
                if (slot.Frame != frame)
                {
                    slot.Frame = frame;
                    surface.Refresh(slot);
                }
                SetFrame("hud.boost" + i + ".icon", SlotIcons[i], ready || empty ? 1f : 0.55f);
                SetVisible("hud.boost" + i + ".badge", ready);
                Set("hud.boost" + i + ".n", ready ? Num(st.Counts[i]) : "");
                SetVisible("hud.boost" + i + ".plus", empty);
                SetVisible("hud.boost" + i + ".lock", locked);
            }
            SetFrame("hud.pano.star", st.Starred ? "icon_star" : "icon_star_empty");
            SetVisible("hud.pano.seg", st.SegmentCount > 1);
            SetVisible("hud.truck", st.Truck);
            Set("hud.truck.n", text.Format(Strings.Keys.Times, ("n", st.Queue)));
            // the plan picture: rebuilt in place (cells change frame as the build grows)
            var plan = N("hud.pano.plan");
            var cells = st.Thumb.Nodes("hud.pano.cell", plan.Rect, 26);
            bool same = plan.Children.Count == cells.Count;
            for (int i = 0; same && i < cells.Count; i++) same = plan.Children[i].Id == cells[i].Id;
            if (same)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    var have = plan.Children[i];
                    if (have.Frame == cells[i].Frame) continue;
                    have.Frame = cells[i].Frame;
                    surface.Refresh(have);
                }
            }
            else
            {
                plan.Children.Clear();
                foreach (var c in cells) plan.Add(c);
                surface.Show(UiLayer.Hud, root, OnTap);
                RebindTargets();
            }
        }

        private void RebindTargets()
        {
            moves.Inner = surface.Target(N("hud.moves"));
            blocks.Inner = surface.Target(N("hud.blocks"));
            trowel.Inner = surface.Target(N("hud.trowel"));
            gold.Inner = surface.Target(N("hud.gold"));
            for (int i = 0; i < Beads; i++) beads[i].Inner = surface.Target(N("hud.bead" + i));
        }

        public void ShowPanel(PanelModel? model, Action<PanelAction> action)
        {
            Shown = model;
            onAction = action;
            StarCenter = null;
            if (model == null)
            {
                window = null;
                surface.Show(UiLayer.Window, null, null);
                panel.Inner = null;
                star.Inner = null;
                return;
            }
            window = WindowViews.Build(model, canvasW, canvasH, settings, text);
            surface.Show(UiLayer.Window, window, OnWindowTap);
            panel.Inner = surface.Target(window.Get(WindowViews.GroupId));
            var s = window.Find(WindowViews.StarId);
            star.Inner = s != null ? surface.Target(s) : null;
            if (s != null) StarCenter = new Vec2((float)s.Rect.CenterX, (float)s.Rect.CenterY);
        }

        private void OnWindowTap(UiNode n)
        {
            if (!n.Enabled || onAction == null) return;
            if (WindowViews.TryParse(n.Action, out var a)) onAction(a);
        }

        /// <summary>Taps on the HUD: the pause tool; a booster slot (V1: not playable yet, nothing happens).</summary>
        public Action<string>? BoosterTapped { get; set; }

        private void OnTap(UiNode n)
        {
            if (!n.Enabled || n.Action == null) return;
            if (n.Action == PauseAction) onPause();
            else if (n.Action.StartsWith(BoosterAction, StringComparison.Ordinal)) BoosterTapped?.Invoke(n.Action.Substring(BoosterAction.Length));
        }

        /// <summary>Every frame the HUD and an open window use (tests: each is in the atlas).</summary>
        public IEnumerable<string> FrameNames()
        {
            if (root != null) foreach (var n in root.All()) if (n.Frame != null) yield return n.Frame;
            if (window != null) foreach (var n in window.All()) if (n.Frame != null) yield return n.Frame;
        }
    }
}
