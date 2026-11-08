#nullable enable
// What the tutorial draws (paket C1; web TutorialView.ts, reduced to the UiNode tree of the v4 atlas): a soft glow around the highlighted targets,
// a dotted trail and the glove along the hand path (`drag` / `hold`) or on the target (`tap`), and a speech bubble with the guide's portrait.
// Light tutorial (K-53): nothing here dims the board or takes a tap — the nodes have no action and do not block input.
//
// The tree lives on its own `UiLayer.Tutorial` (above the HUD, below the windows). It is rebuilt (`Build`, one `ui.Show`) when the CONTENT
// changes (a new step, the glove may play now, a new layout) and animated per frame through `Frame` (node visibility, rectangles of the
// glove and the glow that follows a dragged block, `ui.Target` scale pops). Pure of the engine: the tests read the same tree.
//
// Guide portrait (D-101): `TutorialSettings.PortraitFrame` names the atlas frame beside the bubble (today the v4 `chr_dede_bust`; the king's
// portrait replaces it by renaming this one string, nothing else changes).
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Tweening;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Tutorial
{
    /// <summary>One piece of static content on screen: what to say, where, and what to point at.</summary>
    public sealed class TutorialContent
    {
        /// <summary>Bubble text (already translated; `{ok}` marks the check icon). Empty = no bubble.</summary>
        public string Text = "";
        public DockPlace Dock;
        /// <summary>Rectangles of the highlighted targets (the glow).</summary>
        public List<PxRect> Lit = new List<PxRect>();
        /// <summary>null = no glove; "tap", "drag" or "hold".</summary>
        public string? HandKind;
        /// <summary>Rounded fingertip path (one point for a tap).</summary>
        public Vec2[] Path = new Vec2[0];
        /// <summary>Canvas size (design px) the tree is built for.</summary>
        public double CanvasW = 1080;
        public double CanvasH = 1920;
    }

    public sealed class TutorialView
    {
        public const string RootId = "tut.root";
        public const string BubbleId = "tut.bubble";
        public const string GloveId = "tut.glove";
        public const string TrailId = "tut.trail";
        public const string RingId = "tut.ring";
        public const string GlowPrefix = "tut.hl.";

        /// <summary>Press time of the glove at the start of its loop (web `handPauseMs` sine press).</summary>
        private const float GloveInMs = 160f;
        private const float RingStartPx = 30f;
        private const float RingMax = 1.8f;
        private const float HoldMs = 600f;
        private const double BubbleTextPx = 44; // tokens font.size.body
        private const double BubblePadX = 30;
        private const double BubblePadY = 22;
        private const double TailPx = 26;
        private const double IconPx = 46;

        private static readonly Rgba PortraitBack = Rgba.Hex("#FFF6E3");
        private static readonly Rgba Ink = Rgba.Hex("#1A467B");

        private readonly IUiSurface ui;
        private readonly TutorialSettings s;
        private TutorialContent? content;
        private UiNode? root;
        private UiNode? bubble;
        private UiNode? glove;
        private UiNode? trail;
        private UiNode? ring;
        private readonly List<UiNode> glows = new List<UiNode>();
        private readonly List<UiNode> bubbleNodes = new List<UiNode>();
        private bool bubbleOn;
        private bool gloveOn;
        private float bubbleEdge;
        private float gloveEdge;
        private bool lastFaded;

        public TutorialView(IUiSurface ui, TutorialSettings settings)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>The tree on screen (tests); null when nothing is shown.</summary>
        public UiNode? Root => root;

        /// <summary>The glove node (tests); null without a glove.</summary>
        public UiNode? Glove => glove;

        public UiNode? Bubble => bubble;

        public int GlowCount => glows.Count;

        /// <summary>Removes everything from the screen.</summary>
        public void Clear()
        {
            content = null;
            root = null;
            bubble = null;
            glove = null;
            trail = null;
            ring = null;
            glows.Clear();
            bubbleNodes.Clear();
            bubbleOn = false;
            gloveOn = false;
            ui.Show(UiLayer.Tutorial, null, null);
        }

        // --- build ---------------------------------------------------------------------------------------------------------------

        /// <summary>Builds the tree of `c` (replacing the previous one). All parts start hidden; `Frame` shows what the presence allows.</summary>
        public void Build(TutorialContent c)
        {
            content = c;
            glows.Clear();
            bubbleNodes.Clear();
            bubbleOn = false;
            gloveOn = false;
            lastFaded = false;
            var r = new UiNode(RootId, new PxRect(0, 0, c.CanvasW, c.CanvasH));
            root = r;
            for (int i = 0; i < c.Lit.Count; i++)
            {
                var g = BuildGlow(i, c.Lit[i]);
                glows.Add(g);
                r.Add(g);
            }
            trail = null;
            if (c.HandKind != null && c.HandKind != "tap" && c.Path.Length > 1)
            {
                trail = new UiNode(TrailId, new PxRect(0, 0, c.CanvasW, c.CanvasH)) { Visible = false };
                foreach (var p in TutorialGlove.TrailDots(c.Path, s.TrailGapPx))
                {
                    double d = s.TrailDotPx;
                    trail.Add(UiKit.Image("tut.dot", Frames.FxConfettiRound, new PxRect(p.X - d / 2, p.Y - d / 2, d, d), false, Rgba.White, s.TrailAlpha));
                }
                r.Add(trail);
            }
            bubble = c.Text.Length > 0 ? BuildBubble(c) : null;
            if (bubble != null) r.Add(bubble);
            ring = null;
            if (c.HandKind == "tap" && c.Path.Length > 0)
            {
                ring = UiKit.Icon("tut.ring", Frames.FxRing, c.Path[0].X, c.Path[0].Y, RingStartPx * 2, RingStartPx * 2);
                ring.Visible = false;
                r.Add(ring);
            }
            glove = null;
            if (c.HandKind != null && c.Path.Length > 0)
            {
                glove = UiKit.Image(GloveId, "ui_glove", GloveRect(c.Path[0]));
                glove.Visible = false;
                r.Add(glove);
            }
            ui.Show(UiLayer.Tutorial, r, null);
        }

        /// <summary>
        /// The highlight tile (`Tile.Highlight`) has its box edge 20 tile px inside the tile and Unity draws a sliced sprite at
        /// `referencePixelsPerUnit / spritePixelsPerUnit` = 100 / 120, so the frame sits 16,7 design px outside the box it surrounds.
        /// </summary>
        public const double HighlightInsetPx = 20 * 100.0 / 120.0;

        /// <summary>Soft glow: one sliced rounded gold outline with a halo around the padded target (`FallbackTiles.TutorialHighlight`).</summary>
        private UiNode BuildGlow(int index, PxRect target)
        {
            var all = GlowRect(target);
            var g = new UiNode(GlowPrefix + index, all) { Visible = false };
            g.Add(UiKit.Image("tut.hl.frame", FallbackTiles.TutorialHighlight, all, true));
            return g;
        }

        private PxRect GlowRect(PxRect target) => target.Grow(s.HighlightPadPx + HighlightInsetPx);

        private PxRect GloveRect(Vec2 tip)
        {
            // the fingertip is at (0.36 W, 8) of the frame (web gloveTip)
            return new PxRect(tip.X - s.GloveW * 0.36, tip.Y - 8, s.GloveW, s.GloveH);
        }

        // --- bubble ----------------------------------------------------------------------------------------------------------------

        /// <summary>A word of the bubble text: a run of letters or the check icon.</summary>
        private readonly struct Word
        {
            public readonly string Text;
            public readonly bool Icon;
            public readonly double Width;

            public Word(string text, bool icon, double width)
            {
                Text = text;
                Icon = icon;
                Width = width;
            }
        }

        private static List<Word> Words(string text, double size)
        {
            var o = new List<Word>();
            foreach (var w in text.Split(' '))
            {
                if (w.Length == 0) continue;
                if (w == "{ok}") o.Add(new Word(w, true, IconPx));
                else o.Add(new Word(w, false, FontMetrics.Width(w, size)));
            }
            return o;
        }

        /// <summary>Greedy word wrap (web `useAdvancedWrap` at `maxW`); a lone word wider than the line takes a line of its own.</summary>
        public static List<List<(string Text, bool Icon, double Width)>> Wrap(string text, double size, double maxW)
        {
            var lines = new List<List<(string, bool, double)>>();
            var cur = new List<(string, bool, double)>();
            double x = 0;
            double space = FontMetrics.Width(" ", size);
            foreach (var w in Words(text, size))
            {
                double add = (cur.Count == 0 ? 0 : space) + w.Width;
                if (cur.Count > 0 && x + add > maxW)
                {
                    lines.Add(cur);
                    cur = new List<(string, bool, double)>();
                    x = 0;
                    add = w.Width;
                }
                cur.Add((w.Text, w.Icon, w.Width));
                x += add;
            }
            if (cur.Count > 0) lines.Add(cur);
            return lines;
        }

        /// <summary>The size of the whole bubble (portrait + gap + tail + box) for `text`, design px.</summary>
        public Vec2 BubbleSize(string text)
        {
            var lines = Wrap(text, BubbleTextPx, s.BubbleMaxW - 2 * BubblePadX);
            double textW = 0;
            double space = FontMetrics.Width(" ", BubbleTextPx);
            foreach (var l in lines)
            {
                double w = 0;
                for (int i = 0; i < l.Count; i++) w += l[i].Width + (i > 0 ? space : 0);
                textW = Math.Max(textW, w);
            }
            double lineH = BubbleTextPx * 1.22;
            double boxW = Math.Min(s.BubbleMaxW, Math.Ceiling(textW) + 2 * BubblePadX);
            double boxH = Math.Max(Math.Max(s.BubbleMinH, 138), lines.Count * lineH + 2 * BubblePadY);
            double w2 = s.PortraitPx + s.PortraitGapPx + TailPx + boxW;
            return new Vec2((float)w2, (float)Math.Max(s.PortraitPx, boxH));
        }

        private UiNode BuildBubble(TutorialContent c)
        {
            var d = c.Dock.Rect;
            var group = new UiNode(BubbleId, d) { Visible = false };
            var lines = Wrap(c.Text, BubbleTextPx, s.BubbleMaxW - 2 * BubblePadX);
            double space = FontMetrics.Width(" ", BubbleTextPx);
            double lineH = BubbleTextPx * 1.22;
            double textW = 0;
            foreach (var l in lines)
            {
                double w = 0;
                for (int i = 0; i < l.Count; i++) w += l[i].Item3 + (i > 0 ? space : 0);
                textW = Math.Max(textW, w);
            }
            double boxW = Math.Min(s.BubbleMaxW, Math.Ceiling(textW) + 2 * BubblePadX);
            double boxH = Math.Max(Math.Max(s.BubbleMinH, 138), lines.Count * lineH + 2 * BubblePadY);
            double boxX = d.X + s.PortraitPx + s.PortraitGapPx + TailPx;
            double boxY = d.Y + (d.H - boxH) / 2;
            double portraitY = d.Y + (d.H - s.PortraitPx) / 2;

            // portrait: a cream disc with the guide's bust on it
            Add(group, UiKit.Image("tut.portrait.back", Frames.FxConfettiRound, new PxRect(d.X, portraitY, s.PortraitPx, s.PortraitPx), false, PortraitBack));
            Add(group, UiKit.Image("tut.portrait", s.PortraitFrame, new PxRect(d.X + 6, portraitY + 4, s.PortraitPx - 12, s.PortraitPx - 8)) .WithAspect());
            // tail: the atlas tail points down; turned to point at the portrait (left) or at the HUD (up)
            if (c.Dock.TailDown)
            {
                // U2 (K2-B): the board dock's tail points down at the glove (the atlas tail points down), kept on the box's bottom edge
                double tx = Math.Max(boxX + 24, Math.Min(boxX + boxW - 88, c.Dock.TailX - 32));
                Add(group, UiKit.Image("tut.tail", "bubble_tail", new PxRect(tx, boxY + boxH - 16, 64, 56)));
            }
            else if (c.Dock.TailUp)
            {
                var tail = UiKit.Image("tut.tail", "bubble_tail", new PxRect(boxX + 40, boxY - 40, 64, 56));
                tail.Rotation = 180f;
                Add(group, tail);
            }
            else
            {
                var tail = UiKit.Image("tut.tail", "bubble_tail", new PxRect(boxX - 40, boxY + boxH / 2 - 28, 64, 56));
                tail.Rotation = -90f;
                Add(group, tail);
            }
            Add(group, UiKit.Image("tut.box", "bubble_speech", new PxRect(boxX, boxY, boxW, boxH), true));

            // text, word by word (so the check icon can sit inside a line)
            double y = boxY + (boxH - lines.Count * lineH) / 2;
            foreach (var l in lines)
            {
                double lineW = 0;
                for (int i = 0; i < l.Count; i++) lineW += l[i].Item3 + (i > 0 ? space : 0);
                double x = boxX + (boxW - lineW) / 2;
                foreach (var w in l)
                {
                    if (w.Item2)
                        Add(group, UiKit.Icon("tut.ok", "icon_check", x + IconPx / 2, y + lineH / 2, IconPx, IconPx));
                    else
                    {
                        var label = UiKit.Label("tut.word", w.Item1, new PxRect(x - 6, y, w.Item3 + 12 + TextFit.Margin, lineH), TextStyle.Card, BubbleTextPx, TextAlign.Left);
                        Add(group, label);
                    }
                    x += w.Item3 + space;
                }
                y += lineH;
            }
            return group;
        }

        private void Add(UiNode group, UiNode child)
        {
            group.Add(child);
            bubbleNodes.Add(child);
        }

        // --- per frame ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// One frame of the animation (`now` = the tutorial clock, ms): `look` is what the presence allows, `glovePlays` the DL-2R-20
        /// play condition, `lit` the CURRENT rectangles of the highlighted targets (a dragged block moves), `reduced` = reduced motion.
        /// </summary>
        public void Frame(float now, PresenceLook look, bool glovePlays, IReadOnlyList<PxRect> lit, bool reduced)
        {
            var c = content;
            if (c == null || root == null) return;

            // glow
            for (int i = 0; i < glows.Count; i++)
            {
                var g = glows[i];
                bool on = look.Highlight && i < lit.Count;
                if (on && !RectEquals(lit[i], c.Lit[i]))
                {
                    c.Lit[i] = lit[i];
                    MoveGlow(g, lit[i]);
                }
                if (g.Visible != on)
                {
                    g.Visible = on;
                    ui.Refresh(g);
                }
                if (on)
                {
                    float pulse = reduced ? 1f : 1f + (s.HighlightPulseScale - 1f) * (0.5f - 0.5f * (float)Math.Cos(2 * Math.PI * (now % s.HighlightPulseMs) / s.HighlightPulseMs));
                    ui.Target(g).Scale = new Vec2(pulse, pulse);
                }
            }

            // bubble: pop in (0.8 → 1, Back.out), fades while a touch is held
            var b = bubble;
            if (b != null)
            {
                bool on = look.Bubble;
                if (on != bubbleOn)
                {
                    bubbleOn = on;
                    bubbleEdge = now;
                    b.Visible = on;
                    ui.Refresh(b);
                    if (on) lastFaded = !look.Faded; // force the alpha pass below
                }
                if (on)
                {
                    float u = Math.Min(1f, (now - bubbleEdge) / Math.Max(1f, reduced ? 150f : s.AppearMs));
                    float scale = reduced ? 1f : 0.8f + 0.2f * Easing.Evaluate(EaseType.OutBack, u);
                    ui.Target(b).Scale = new Vec2(scale, scale);
                    if (look.Faded != lastFaded)
                    {
                        lastFaded = look.Faded;
                        float a = look.Faded ? s.DragFadeAlpha : 1f;
                        foreach (var n in bubbleNodes)
                        {
                            n.Alpha = a;
                            ui.Refresh(n);
                        }
                    }
                }
            }

            // glove / trail / ring
            var gl = glove;
            if (gl != null)
            {
                bool on = look.Glove && glovePlays;
                if (on != gloveOn)
                {
                    gloveOn = on;
                    gloveEdge = now;
                    gl.Visible = on;
                    if (trail != null) trail.Visible = on;
                    if (!on && ring != null)
                    {
                        ring.Visible = false;
                        ui.Refresh(ring);
                    }
                    ui.Refresh(gl);
                    if (trail != null) ui.Refresh(trail);
                }
                if (on) DriveGlove(gl, now, reduced);
            }
        }

        private static bool RectEquals(PxRect a, PxRect b) => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.W - b.W) < 0.5 && Math.Abs(a.H - b.H) < 0.5;

        /// <summary>The glow follows a target that moved (a dragged block): the group and its frame get new rectangles.</summary>
        private void MoveGlow(UiNode g, PxRect target)
        {
            var all = GlowRect(target);
            g.Rect = all;
            g.Children[0].Rect = all;
            ui.Refresh(g);
            ui.Refresh(g.Children[0]);
        }

        /// <summary>The loop of the web glove: `HandLoopMs`, the last `HandPauseMs` a pause (the glove fades); a tap presses and a ring grows.</summary>
        private void DriveGlove(UiNode gl, float now, bool reduced)
        {
            var c = content!;
            var start = c.Path[0];
            string kind = c.HandKind ?? "tap";
            float loop = s.HandLoopMs;
            float pause = s.HandPauseMs;
            float move = loop - pause;
            float tIn = now - gloveEdge;
            float fadeIn = Math.Min(1f, tIn / GloveInMs);
            if (reduced)
            {
                gl.Rect = GloveRect(start);
                gl.Alpha = fadeIn;
                ui.Refresh(gl);
                ui.Target(gl).Scale = new Vec2(kind == "tap" ? s.GlovePressScale : 1f, kind == "tap" ? s.GlovePressScale : 1f);
                return;
            }
            float t = tIn % loop;
            if (kind == "tap")
            {
                gl.Rect = GloveRect(start);
                gl.Alpha = fadeIn;
                float press = t < pause ? (float)Math.Sin(Math.PI * t / pause) : 0f;
                float sc = 1f - (1f - s.GlovePressScale) * press;
                ui.Refresh(gl);
                ui.Target(gl).Scale = new Vec2(sc, sc);
                if (ring != null)
                {
                    float u = Math.Min(1f, t / move);
                    float rs = 1f + (RingMax - 1f) * u;
                    ring.Visible = true;
                    ring.Alpha = (1f - u) * fadeIn;
                    ui.Refresh(ring);
                    ui.Target(ring).Scale = new Vec2(rs, rs);
                }
                return;
            }
            float holdMs = kind == "hold" ? HoldMs : 0f;
            float travel = Math.Max(1f, move - holdMs);
            float uu = Math.Min(1f, t / travel);
            var p = TutorialGlove.PathAt(c.Path, uu);
            float fade = t < move ? 1f : 1f - (t - move) / pause;
            gl.Rect = GloveRect(p);
            gl.Alpha = fadeIn * Math.Max(0f, fade);
            ui.Refresh(gl);
            float ps = t < move ? s.GlovePressScale : 1f;
            ui.Target(gl).Scale = new Vec2(ps, ps);
        }
    }

    internal static class TutorialNodeExtensions
    {
        public static UiNode WithAspect(this UiNode n)
        {
            n.PreserveAspect = true;
            return n;
        }
    }
}
