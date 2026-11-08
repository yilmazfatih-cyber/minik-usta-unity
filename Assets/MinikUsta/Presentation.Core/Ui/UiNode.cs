#nullable enable
// The engine-independent UI tree (paket V1): every HUD element, window, the home screen and the settings panel are built here
// as `UiNode`s — a frame of the v4 atlas drawn into a design px rectangle, an optional text with an ART_REFERENCE §4 style and
// an optional tap action. The Unity layer (`UiRenderer`) turns a tree into uGUI objects one to one (Image Simple / Sliced,
// Text + Outline + Shadow, Button); the NUnit tests read the same tree (tap targets ≥ 128 px, frames exist, texts are i18n);
// `UiDump` writes it as JSON for tools/art/compose.mjs. Rectangles are ABSOLUTE design px (origin top-left of the design
// canvas, y down); the renderer makes them parent-relative.
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Art;

namespace MinikUsta.Presentation.Ui
{
    public enum TextAlign
    {
        Left,
        Center,
        Right,
    }

    /// <summary>The layers of the UI canvas, back to front.</summary>
    public enum UiLayer
    {
        Home = 0,
        Hud = 1,
        /// <summary>C1 (K-53): the light tutorial — glow, glove, bubble — above the HUD and below the windows; it takes no tap.</summary>
        Tutorial = 2,
        Window = 3,
        /// <summary>Particles above the open window (the win star burst).</summary>
        Fx = 4,
        /// <summary>The settings panel and the quit confirmation (over the home screen or the pause window).</summary>
        Overlay = 5,
        /// <summary>U2 (UX_UNITY §5.9): the `LockedHint` balloon above everything; it takes no tap.</summary>
        Hint = 6,
    }

    public sealed class UiNode
    {
        public readonly string Id;
        public UiNode? Parent { get; private set; }
        public readonly List<UiNode> Children = new List<UiNode>();

        /// <summary>Absolute design px rectangle.</summary>
        public PxRect Rect;
        /// <summary>Atlas frame drawn into `Rect`; null = no image (a group or a text).</summary>
        public string? Frame;
        /// <summary>9-slice the frame (its atlas borders keep their design size).</summary>
        public bool Sliced;
        public bool PreserveAspect;
        public Rgba Tint = Rgba.White;
        /// <summary>Multiplies the tint alpha (and the text alpha).</summary>
        public float Alpha = 1f;
        /// <summary>Degrees counter-clockwise about the rectangle centre.</summary>
        public float Rotation;

        public string? Text;
        public TextStyle Style = TextStyle.Plain;
        public double FontPx = 40;
        public TextAlign Align = TextAlign.Center;
        /// <summary>Style overrides (null = the style's colour).</summary>
        public Rgba? TextFill;
        public Rgba? TextStroke;

        /// <summary>Tap action id (null = not a button).</summary>
        public string? Action;
        /// <summary>A disabled button shows but does not fire (K-54 grey slots, "Altın yetmiyor").</summary>
        public bool Enabled = true;
        /// <summary>JUICE #69 press feel (0,94 pressed, Back.easeOut release).</summary>
        public bool Pressable;
        /// <summary>Eats taps that reach it (window dim, panels): nothing under it is touched.</summary>
        public bool BlocksInput;
        public bool Visible = true;
        /// <summary>
        /// B2: the node redraws every frame (a clip actor, a parallax layer): the Unity layer gives it its own nested canvas so its
        /// changes do not rebuild the batches of the whole screen.
        /// </summary>
        public bool Isolated;

        public UiNode(string id, PxRect rect)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Rect = rect;
        }

        public bool IsButton => Action != null;

        /// <summary>Inserts `child` at `index` (draw order: later children draw on top).</summary>
        public UiNode Insert(int index, UiNode child)
        {
            if (child.Parent != null) throw new InvalidOperationException("UiNode " + child.Id + " already has a parent");
            child.Parent = this;
            Children.Insert(Math.Max(0, Math.Min(index, Children.Count)), child);
            return child;
        }

        public UiNode Add(UiNode child)
        {
            if (child.Parent != null) throw new InvalidOperationException("UiNode " + child.Id + " already has a parent");
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        /// <summary>Depth-first, this node first.</summary>
        public IEnumerable<UiNode> All()
        {
            yield return this;
            foreach (var c in Children)
                foreach (var d in c.All()) yield return d;
        }

        public UiNode? Find(string id)
        {
            foreach (var n in All()) if (n.Id == id) return n;
            return null;
        }

        public UiNode Get(string id) => Find(id) ?? throw new KeyNotFoundException("UiNode: no " + id + " under " + Id);

        /// <summary>Visible along the whole parent chain.</summary>
        public bool Shown
        {
            get
            {
                for (var n = this; n != null; n = n.Parent) if (!n.Visible) return false;
                return true;
            }
        }

        public override string ToString() => Id + " " + Rect + (Frame != null ? " [" + Frame + "]" : "") + (Text != null ? " \"" + Text + "\"" : "") + (Action != null ? " →" + Action : "");
    }

    /// <summary>Small builders so the screens read like their wireframes.</summary>
    public static class UiKit
    {
        public static UiNode Group(string id, PxRect r) => new UiNode(id, r);

        public static UiNode Image(string id, string frame, PxRect r, bool sliced = false, Rgba? tint = null, float alpha = 1f)
        {
            var n = new UiNode(id, r) { Frame = frame, Sliced = sliced, Alpha = alpha };
            if (tint.HasValue) n.Tint = tint.Value;
            return n;
        }

        /// <summary>A frame at its own design size × `scale`, centred on (cx, cy).</summary>
        public static UiNode Icon(string id, string frame, double cx, double cy, double w, double h, float alpha = 1f) =>
            Image(id, frame, new PxRect(cx - w / 2, cy - h / 2, w, h), false, null, alpha);

        public static UiNode Label(string id, string text, PxRect r, TextStyle style, double size, TextAlign align = TextAlign.Center, Rgba? fill = null, Rgba? stroke = null) =>
            new UiNode(id, r) { Text = text, Style = style, FontPx = size, Align = align, TextFill = fill, TextStroke = stroke };

        public static UiNode Button(string id, string frame, PxRect r, string action, bool sliced = true, bool enabled = true) =>
            new UiNode(id, r) { Frame = frame, Sliced = sliced, Action = action, Enabled = enabled, Pressable = enabled, BlocksInput = true };

        /// <summary>A full-canvas dim (#05030D, `alpha`) that eats taps.</summary>
        public static UiNode Dim(string id, double w, double h, float alpha) =>
            new UiNode(id, new PxRect(0, 0, w, h)) { Frame = Frames.White, Tint = Rgba.Hex("#05030D"), Alpha = alpha, BlocksInput = true };
    }
}
