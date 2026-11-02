#nullable enable
// A clip drawn on a UI node (paket B2): an `AnimPlayer` + the placement math of the trimmed 3D frames. Every frame of king.json /
// fx.json is trimmed to its alpha box, so frames differ in size and offset; a clip is placed by its PIVOT (the king's feet, the
// canvas centre of an fx), never box-fitted (art/3d/blender/king/README.md, "code-lead"). The actor writes the node's `Frame` and
// `Rect` and refreshes it only when something changed; the 2.5D layer rides on top: the idle breathing (ART_3D §7.1 rule 2:
// scaleY 1 + 0,015 sin(2πt / 2 s), scaleX 1 − 0,006 sin(…), about the feet), a settle squash after a hard cut, a pop scale.
using System;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Anim
{
    public static class ClipPlacement
    {
        /// <summary>
        /// The design rectangle of frame `f` drawn with its pivot at (`ax`, `ay`) (design px, y down) at `k` × its design size,
        /// squashed `sx` × `sy` about the pivot.
        /// </summary>
        public static PxRect Rect(AtlasFrame f, double ax, double ay, double k, double sx = 1, double sy = 1)
        {
            double w = f.DesignW * k * sx, h = f.DesignH * k * sy;
            return new PxRect(ax - f.PivotX * w, ay - (1 - f.PivotY) * h, w, h);
        }

        /// <summary>Frame design px → clip design px: a stage clip (scale 0,705) draws the corner drawings (0,88125) 1,25× larger.</summary>
        public static double ClipFactor(AtlasFrame f, AnimClip clip) => f.Scale / clip.Scale;

        /// <summary>The size factor that makes `clip`'s canvas `heightPx` design px tall.</summary>
        public static double FitHeight(AnimClip clip, double heightPx) => clip.CanvasH > 0 ? heightPx / clip.CanvasH : 1;
    }

    public sealed class ClipActor
    {
        private readonly ClipLibrary? lib;
        private IUiSurface? surface;
        private UiNode? node;
        private float time;
        private float settle;
        private string? shownFrame;
        private PxRect shownRect;

        public ClipActor(ClipLibrary? library)
        {
            lib = library;
            if (lib != null) Player.StillOf = c => lib.StillOf(c);
        }

        public AnimPlayer Player { get; } = new AnimPlayer();

        /// <summary>The pivot point (design px, y down): the king's feet.</summary>
        public Vec2 Anchor { get; set; }

        /// <summary>× the clip's nominal size (1 = the clip canvas at its own scale).</summary>
        public double Size { get; set; } = 1;

        /// <summary>
        /// The clip scale every clip of this actor is drawn at (null = each clip's own): a slot mixes corner and stage clips (the home
        /// king cheers with the corner drawings) at ONE size, so the king never grows or shrinks between clips.
        /// </summary>
        public double? BaseScale { get; set; }

        /// <summary>A scale pop on top (1 = rest), about the pivot (tap on the home king, JUICE #69 feel).</summary>
        public float Pop { get; set; } = 1f;

        /// <summary>ART_3D §7.1 rule 2: the idle breathing tween (on while an idle clip plays, off in reduced motion).</summary>
        public float BreathAmpY { get; set; } = 0.015f;
        public float BreathAmpX { get; set; } = 0.006f;
        public float BreathPeriodMs { get; set; } = 2000f;
        /// <summary>A hard cut (`AnimPlayer.Cut`) settles with a squash of this depth over `SettleMs`.</summary>
        public float SettleDepth { get; set; } = 0.05f;
        public float SettleMs { get; set; } = 160f;

        public bool Bound => node != null && surface != null;
        public UiNode? Node => node;
        public ClipLibrary? Library => lib;

        /// <summary>Refreshes written since construction (tests: the actor only writes changes).</summary>
        public int Writes { get; private set; }

        /// <summary>Binds the actor to a node on `surface`; the node is drawn by the actor from now on.</summary>
        public void Bind(IUiSurface s, UiNode n, Vec2 anchor, double size)
        {
            surface = s;
            node = n;
            Anchor = anchor;
            Size = size;
            n.PreserveAspect = false;
            shownFrame = null;
            Apply(true);
        }

        /// <summary>Re-applies the node (visibility changed by the owner).</summary>
        public void Refresh()
        {
            if (node != null && surface != null) surface.Refresh(node);
        }

        public void Unbind()
        {
            surface = null;
            node = null;
            shownFrame = null;
        }

        /// <summary>One frame: the player, the breathing clock, the settle squash; then the node.</summary>
        public void Advance(float dtMs)
        {
            if (!(dtMs > 0)) dtMs = 0;
            Player.Advance(dtMs);
            time += dtMs;
            if (Player.Cut)
            {
                Player.Cut = false;
                if (!Player.Reduced) settle = SettleMs;
            }
            else if (settle > 0) settle = Math.Max(0f, settle - dtMs);
            Apply(false);
        }

        /// <summary>The breathing / settle factors now (sx, sy) about the pivot.</summary>
        public void Squash(out double sx, out double sy)
        {
            sx = 1;
            sy = 1;
            var c = Player.Clip;
            if (!Player.Reduced && c != null && Player.Phase == AnimPhase.Main && (c.AliasOf ?? c.Name) == "king_idle")
            {
                double s = Math.Sin(2 * Math.PI * time / Math.Max(1f, BreathPeriodMs));
                sy += BreathAmpY * s;
                sx -= BreathAmpX * s;
            }
            if (settle > 0 && SettleMs > 0)
            {
                double u = settle / SettleMs; // 1 → 0
                double d = SettleDepth * Math.Sin(Math.PI * (1 - u)) * u;
                sy -= d;
                sx += d * 0.5;
            }
            sx *= Pop;
            sy *= Pop;
        }

        /// <summary>The rectangle the current frame is drawn in (null when there is nothing to draw).</summary>
        public PxRect? RectNow()
        {
            var c = Player.Clip;
            string? name = Player.Frame;
            if (lib == null || c == null || name == null) return null;
            var f = lib.Find(name);
            if (f == null) return null;
            Squash(out double sx, out double sy);
            double k = BaseScale.HasValue ? f.Scale / BaseScale.Value : ClipPlacement.ClipFactor(f, c);
            return ClipPlacement.Rect(f, Anchor.X, Anchor.Y, k * Size, sx, sy);
        }

        private void Apply(bool force)
        {
            var n = node;
            var s = surface;
            if (n == null || s == null || lib == null) return;
            string? name = Player.Frame;
            var r = RectNow();
            if (name == null || !r.HasValue) return;
            if (!force && name == shownFrame && r.Value.Equals(shownRect)) return;
            n.Frame = name;
            n.Rect = r.Value;
            shownFrame = name;
            shownRect = r.Value;
            Writes++;
            s.Refresh(n);
        }
    }
}
