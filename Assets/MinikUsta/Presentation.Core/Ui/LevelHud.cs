#nullable enable
// The level HUD v3 as a UI tree (paket V1; ART_REFERENCE §9.4; ART_UNITY §4.4; UX_FLOWS §5.1, §5.9, §5.10; GDD K-54):
//   row 1  "Bölüm N" capsule · blocks-left chip (icon_blocks overflowing) · Usta Serisi capsule (4 beads + Golden Trowel ×n)
//   row 2  panorama well (level name, star, plan picture of the visible segment, segment counter) · moves plate · pause tool
//   bottom Usta Kral + Kepçe corner (B2, D-101: the king's clips; Tuna stays on the home screen) · 4 booster slots (K-54: locked /
//          no target / empty "+" / ready with a count) · truck chip (D-099: only while a block waits in the queue)
// It implements the HUD half of `ILevelView` over an `IUiSurface` (Unity: uGUI), so the Unity layer only forwards calls; the
// level windows are built by `WindowViews` on the Window layer. Pure: the tests drive it with a fake surface.
using System;
using System.Collections.Generic;
using System.Globalization;
using MinikUsta.Core;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Text;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Ui
{
    /// <summary>U2: a level view that draws its HUD with a `LevelHud` (the Unity view, the test fake): the app wires the hints to it.</summary>
    public interface ILevelHudHost
    {
        LevelHud? HudOrNull { get; }
    }

    /// <summary>
    /// UX_UNITY K3 / P0-7 (paket U2): which booster slots the HUD shows. A slot appears 2 levels before its unlock level (Hammer 8 → from
    /// Bölüm 6; no slot at all in Bölüm 1–5) and always when the inventory holds the booster; a hidden slot keeps its place (the others do
    /// not move). A locked slot carries "Bölüm N" under it.
    /// </summary>
    public static class BoosterSlotRules
    {
        public const int ShowLevelsBefore = 2;

        public static bool Visible(int level, int unlockLevel, int count) => count > 0 || level >= unlockLevel - ShowLevelsBefore;
    }

    public sealed class LevelHud
    {
        public const string PauseAction = "hud:pause";
        public const string KingId = "hud.king";
        public const string WindowKingId = "win.king";
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
        private readonly ProxyTarget movesBurst = new ProxyTarget();
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

        /// <summary>B2: Usta Kral's clips (the corner and the window king bind to it); null = the v4 still.</summary>
        public KingDirector? King { get; set; }

        /// <summary>The corner king's feet (design px) and box of a layout (B2): the box is the UX §5.10 character corner.</summary>
        public static PxRect KingBox(ScreenLayout L) => new PxRect(L.Character.X - 20, L.Character.Y - 18, 236, L.Character.H + 14);

        public UiNode? Root => root;
        public UiNode? Window => window;
        public PanelModel? Shown { get; private set; }
        public Vec2? StarCenter { get; private set; }

        public ITweenTarget MovesChip => moves;
        public ITweenTarget BlocksChip => blocks;
        /// <summary>P4 (D-105): the gold "+n" of a bonus placement on the moves plate (scale pops; shown only while `SetMovesBurst` has a text).</summary>
        public ITweenTarget MovesBurst => movesBurst;
        /// <summary>The "+n" burst is showing (CL-V3C-08).</summary>
        public bool MovesBurstOn { get; private set; }
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
            // U4 R-01: the scene band (behind everything else of the HUD; empty at H <= 1920)
            r.Add(SceneBand.Build(L));
            // row 1: level capsule
            var cap = r.Add(UiKit.Image("hud.level", "capsule_hud", HudLayout.Capsule(L), true));
            cap.Add(UiKit.Label("hud.level.text", "", HudLayout.Capsule(L), TextStyle.Lavender, 38));
            // blocks-left chip (the icon overflows the capsule's left end)
            var bc = HudLayout.BlocksChip(L);
            var chip = r.Add(UiKit.Image("hud.blocks", "capsule_hud", bc, true));
            chip.Add(UiKit.Icon("hud.blocks.icon", "icon_blocks", bc.X - 2, bc.CenterY - 2, 82, 82));
            chip.Add(UiKit.Label("hud.blocks.n", "", new PxRect(bc.X + 52, bc.Y, 84, bc.H), TextStyle.Cream, 44));
            chip.Add(UiKit.Label("hud.blocks.label", "", new PxRect(bc.X + 132, bc.Y + 2, bc.W - 140, bc.H), TextStyle.Lavender, HudLayout.MinTextPx, TextAlign.Left));
            // streak capsule
            var sc = HudLayout.Streak(L);
            var streak = r.Add(UiKit.Image("hud.streak", "capsule_hud", sc, true));
            streak.Add(UiKit.Label("hud.streak.label", "", HudLayout.StreakLabel(L), TextStyle.Lavender, HudLayout.MinTextPx));
            for (int i = 0; i < Beads; i++) streak.Add(UiKit.Image("hud.bead" + i, "bead_off", HudLayout.Bead(L, i)));
            streak.Add(UiKit.Image("hud.trowel", "icon_gold_trowel", HudLayout.TrowelIcon(L)));
            streak.Add(UiKit.Label("hud.trowel.n", "", HudLayout.TrowelCount(L), TextStyle.Cream, HudLayout.MinTextPx));
            // row 2: the goal card (U2, UX_UNITY §5.12: 340 × 176, name 36 px, star + segment counter, plan picture with cells ≤ 36 px)
            var pw = r.Add(UiKit.Image("hud.pano", "hud_well", L.Panorama, true));
            var nameR = HudLayout.LevelName(L);
            pw.Add(UiKit.Label("hud.pano.name", "", nameR, TextStyle.Lavender, HudLayout.GoalNamePx));
            var name2 = pw.Add(UiKit.Label("hud.pano.name2", "", new PxRect(nameR.X, nameR.Bottom - 8, nameR.W, nameR.H - 8), TextStyle.Lavender, HudLayout.MinTextPx));
            name2.Visible = false;
            var st = HudLayout.GoalStar(L);
            pw.Add(UiKit.Icon("hud.pano.star", "icon_star_empty", st.CenterX, st.CenterY, st.W, st.H));
            var seg = pw.Add(UiKit.Label("hud.pano.seg", "", HudLayout.GoalSegments(L), TextStyle.Cream, HudLayout.MinTextPx, TextAlign.Left));
            seg.Visible = false;
            pw.Add(UiKit.Group("hud.pano.plan", HudLayout.PanoramaPlan(L)));
            // moves plate
            var mr = HudLayout.Moves(L);
            var mp = r.Add(UiKit.Image("hud.moves", "hud_moves_plate", mr, true));
            mp.Add(UiKit.Label("hud.moves.label", "", new PxRect(mr.X, mr.Y + 2, mr.W, settings.MovesBandH - 2), TextStyle.Card, HudLayout.MinTextPx, TextAlign.Center, Rgba.Hex(Palette.MovesCream)));
            mp.Add(UiKit.Label("hud.moves.n", "", new PxRect(mr.X, mr.Y + settings.MovesBandH - 2, mr.W, mr.H - settings.MovesBandH - 6), TextStyle.Big, 80));
            // P4 (D-105): the bonus "+n" over the plate's top-right corner (hidden until a bonus placement pays; never blocks a touch)
            var burst = r.Add(UiKit.Label("hud.moves.burst", "", HudLayout.MovesBurst(L), TextStyle.Gold, HudLayout.MovesBurstPx));
            burst.Visible = false;
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
            // B2 (D-101): Usta Kral in the corner, Kepçe at his feet; the node carries the v4 still until the clips draw it
            var kb = KingBox(L);
            var kingNode = r.Add(UiKit.Image(KingId, KingDirector.FallbackFrame, kb));
            kingNode.PreserveAspect = true;
            kingNode.Isolated = true;
            r.Add(UiKit.Image("hud.kepce", "chr_kepce_idle", new PxRect(cr.X + 156, L.H - 118, 136, 94)));
            for (int i = 0; i < L.Boosters.Length && i < SlotIcons.Length; i++)
            {
                var b = L.Boosters[i];
                var slot = r.Add(UiKit.Button("hud.boost" + i, "btn_booster_off", b, BoosterAction + HudState.SlotOrder[i], true));
                slot.Add(UiKit.Image("hud.boost" + i + ".icon", SlotIcons[i], new PxRect(b.X + 24, b.Y + 14, 112, 112)));
                var badge = slot.Add(UiKit.Image("hud.boost" + i + ".badge", "badge_count", new PxRect(b.X + 112, b.Y + 110, 68, 72)));
                badge.Add(UiKit.Label("hud.boost" + i + ".n", "", new PxRect(b.X + 112, b.Y + 112, 68, 60), TextStyle.Button, 42, TextAlign.Center, null, Rgba.Hex("#7A0A05")));
                slot.Add(UiKit.Image("hud.boost" + i + ".plus", "btn_plus", new PxRect(b.X + 114, b.Y + 114, 64, 64)));
                slot.Add(UiKit.Image("hud.boost" + i + ".lock", "icon_lock", new PxRect(b.X + 106, b.Y + 100, 56, 64)));
                // U2 (UX_UNITY K3): "Bölüm N" under a locked slot (34 px); the slot itself starts hidden until the HUD state says so
                var at = slot.Add(UiKit.Label("hud.boost" + i + ".at", "", new PxRect(b.X - 14, b.Bottom + 4, b.W + 28, 46), TextStyle.Label, HudLayout.MinTextPx));
                at.Visible = false;
                slot.Visible = false;
            }
            var tc = HudLayout.TruckChip(L);
            var truck = r.Add(UiKit.Image("hud.truck", "capsule_hud", tc, true));
            truck.Add(UiKit.Image("hud.truck.icon", Frames.Truck, new PxRect(tc.X + 10, tc.Y - 4, 120, 67)));
            truck.Add(UiKit.Label("hud.truck.n", "", new PxRect(tc.X + 140, tc.Y, tc.W - 150, tc.H), TextStyle.Cream, 40));
            truck.Visible = false;
            root = r;
            surface.Show(UiLayer.Hud, r, OnTap);
            if (King != null) King.BindCorner(surface, kingNode, new Vec2((float)kb.CenterX, (float)(kb.Bottom - 6)), King.SizeFor(King.Corner, kb.H));
            RebindTargets();
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
            // UX_UNITY §4.2: the name on one line at 36 px; a name that would shrink under 34 px goes on two lines at 34 px (no "…")
            string one = name, two = "";
            if (root != null)
            {
                var n = N("hud.pano.name");
                double room = n.Rect.W - TextFit.Margin;
                if (FontMetrics.FittedSize(name, HudLayout.GoalNamePx, room) < HudLayout.MinTextPx) SplitName(name, HudLayout.MinTextPx, room, out one, out two);
            }
            Set("hud.pano.name", one);
            Set("hud.pano.name2", two);
            SetVisible("hud.pano.name2", two.Length > 0);
            if (root != null)
            {
                var n = N("hud.pano.name");
                double fs = two.Length > 0 ? HudLayout.MinTextPx : HudLayout.GoalNamePx;
                if (n.FontPx != fs)
                {
                    n.FontPx = fs;
                    surface.Refresh(n);
                }
            }
        }

        /// <summary>Splits `name` at the word break that keeps both lines narrowest (TR / EN names have spaces).</summary>
        public static void SplitName(string name, double size, double room, out string one, out string two)
        {
            one = name;
            two = "";
            double best = double.MaxValue;
            for (int i = name.IndexOf(' '); i > 0; i = name.IndexOf(' ', i + 1))
            {
                string a = name.Substring(0, i), b = name.Substring(i + 1);
                double w = Math.Max(FontMetrics.Width(a, size), FontMetrics.Width(b, size));
                if (w < best)
                {
                    best = w;
                    one = a;
                    two = b;
                }
            }
            _ = room;
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

        /// <summary>P4 (D-105): "+2" over the moves plate while a bonus placement pays; null / empty hides it.</summary>
        public void SetMovesBurst(string? textOrNull)
        {
            bool on = !string.IsNullOrEmpty(textOrNull);
            MovesBurstOn = on;
            if (on) Set("hud.moves.burst", textOrNull!);
            SetVisible("hud.moves.burst", on);
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
                int unlock = i < st.UnlockLevels.Length ? st.UnlockLevels[i] : int.MaxValue;
                int count = i < st.Counts.Length ? st.Counts[i] : 0;
                SetVisible("hud.boost" + i, BoosterSlotRules.Visible(st.Level, unlock, count));
                SetVisible("hud.boost" + i + ".at", locked);
                Set("hud.boost" + i + ".at", locked ? text.Format(BoosterLockedAtKey, ("n", unlock)) : "");
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
            bool rebuilt = SyncCells(N("hud.pano.plan"), st.Thumb.Nodes("hud.pano.cell", N("hud.pano.plan").Rect, settings.GoalPlanCellPx));
            var band = N(SceneBand.GroupId);
            if (band.Visible) rebuilt |= SyncCells(N(SceneBand.PlanId), SceneBand.Cells(st.Thumb, band.Rect));
            if (rebuilt)
            {
                surface.Show(UiLayer.Hud, root, OnTap);
                RebindTargets();
            }
        }

        /// <summary>Updates `plan`'s cells in place when the ids match (frame, tint, alpha), else replaces them; true when replaced.</summary>
        private bool SyncCells(UiNode plan, List<UiNode> cells)
        {
            bool same = plan.Children.Count == cells.Count;
            for (int i = 0; same && i < cells.Count; i++) same = plan.Children[i].Id == cells[i].Id;
            if (same)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    var have = plan.Children[i];
                    if (have.Frame == cells[i].Frame && have.Tint.Equals(cells[i].Tint) && have.Alpha == cells[i].Alpha) continue;
                    have.Frame = cells[i].Frame;
                    have.Tint = cells[i].Tint;
                    have.Alpha = cells[i].Alpha;
                    surface.Refresh(have);
                }
                return false;
            }
            plan.Children.Clear();
            foreach (var c in cells) plan.Add(c);
            return true;
        }

        /// <summary>
        /// Binds every tween target of the HUD to the nodes of the tree on screen. Called after EVERY `surface.Show(UiLayer.Hud, …)`: the
        /// Unity surface destroys the old RectTransforms, so a target left on them throws (C1: `movesBurst` was missing here, the bonus
        /// "+2" pop after a plan rebuild set the scale of a destroyed RectTransform every frame and froze the level).
        /// </summary>
        private void RebindTargets()
        {
            moves.Inner = surface.Target(N("hud.moves"));
            movesBurst.Inner = surface.Target(N("hud.moves.burst"));
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
                King?.UnbindWindow();
                surface.Show(UiLayer.Window, null, null);
                panel.Inner = null;
                star.Inner = null;
                return;
            }
            window = WindowViews.Build(model, canvasW, canvasH, settings, text);
            surface.Show(UiLayer.Window, window, OnWindowTap);
            var wk = window.Find(WindowKingId);
            var wb = window.Find(WindowViews.BurstId);
            if (King != null)
            {
                if (wk != null && model.Detail?.King != null)
                {
                    var box = wk.Rect;
                    King.BindWindow(surface, wk, new Vec2((float)box.CenterX, (float)box.Bottom), King.SizeFor(King.Window, box.H), model.Detail.King.Value);
                }
                else King.UnbindWindow();
                if (wb != null) King.BindWindowBurst(surface, wb, new Vec2((float)wb.Rect.CenterX, (float)wb.Rect.CenterY));
            }
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

        /// <summary>
        /// Taps on the HUD: the pause tool; a booster slot → `BoosterTapped(id)` (U2: the app answers with `HintSlot`, K6 — the boosters are
        /// not playable in this build yet, so every slot tap gets the hint balloon).
        /// </summary>
        public Action<string>? BoosterTapped { get; set; }

        /// <summary>UX_UNITY §11: "Bölüm {n}" under a locked slot.</summary>
        public const string BoosterLockedAtKey = "booster.lockedAt";

        /// <summary>U2: the shared hint balloon (the app sets it); null = no hints.</summary>
        public LockedHint? Hint { get; set; }

        /// <summary>
        /// U2 (UX_UNITY K6, §5.13): the hint of booster slot `id` — locked: "{n}. bölümde açılır" + the lock shakes; otherwise "Yakında" (the
        /// in-level boosters are not playable in this build). False when the slot is not on screen.
        /// </summary>
        public bool HintSlot(string id)
        {
            int i = Array.IndexOf(HudState.SlotOrder, id);
            if (Hint == null || root == null || layout == null || i < 0 || i >= layout.Boosters.Length) return false;
            var slot = root.Find("hud.boost" + i);
            if (slot == null || !slot.Shown) return false;
            var st = last;
            bool locked = st != null && i < st.Slots.Length && st.Slots[i] == SlotStates.Locked;
            string msg = locked ? text.Format("common.unlockAt", ("n", st!.UnlockLevels[i])) : text.Get("common.comingSoon");
            Hint.Show(layout.Boosters[i], msg, locked ? root.Find("hud.boost" + i + ".lock") : root.Find("hud.boost" + i + ".icon"), canvasW, canvasH);
            return true;
        }

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
