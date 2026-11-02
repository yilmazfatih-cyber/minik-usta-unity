#nullable enable
// Scene dump for tools/art/compose.mjs (paket V1): what the Unity layer would draw, as JSON — the world sprites (board items with
// their atlas frames, every block's `BlockSkin` parts at its pose, the K-18 shadow parts and badge, B2's 3D FX clip sprites) sorted by sorting
// order, and the visible UI layers as `UiNode` trees. The compose tool draws exactly this from the atlas PNG pages and the Baloo 2
// TTF: the preview is built from Presentation.Core's layout, not drawn by hand. It is NOT a Unity screenshot (no uGUI text
// layout, no shaders); it proves frame names, rectangles, order, tints and texts.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public static class SceneDump
    {
        private sealed class Sprite
        {
            public string Frame = "";
            public PxRect Rect;
            public int Order;
            public int Seq;
            public Rgba Tint = Rgba.White;
            public float Alpha = 1f;
            public bool Sliced;
            public bool FlipY;
        }

        private static JsonValue Hex(Rgba c) => JsonValue.Of(c.ToHex() + c.A.ToString("X2", CultureInfo.InvariantCulture));

        private static JsonValue N(double v) => JsonValue.Of(System.Math.Round(v, 2));

        /// <summary>The world of a FakeView: board (art items), blocks, shadow, truck.</summary>
        public static JsonValue World(FakeView v)
        {
            var list = new List<Sprite>();
            void Add(Sprite s)
            {
                s.Seq = list.Count;
                list.Add(s);
            }
            if (!v.WorldVisible) return JsonValue.Array();
            if (v.Board != null)
                foreach (var it in v.Board.Items)
                {
                    if (it.Frame == null) continue;
                    var r = it.Rect;
                    if (it.Kind == BoardItemKind.SitePanel || it.Kind == BoardItemKind.PlanCell || it.Kind == BoardItemKind.HatchCell || it.Kind == BoardItemKind.HiddenCell
                        || it.Kind == BoardItemKind.Platform || it.Kind == BoardItemKind.SiteFloor || it.Kind == BoardItemKind.BuildFront)
                        r = new PxRect(r.X + v.SiteLayer.Position.X, r.Y + v.SiteLayer.Position.Y, r.W, r.H);
                    Add(new Sprite { Frame = it.Frame, Rect = r, Order = it.Order, Tint = it.Tint, Sliced = it.Sliced || it.Kind == BoardItemKind.Tray, FlipY = it.FlipY });
                }
            double cell = v.Layout?.Grid.CellPx ?? 120;
            foreach (var p in v.Live.Values.OrderBy(p => p.PieceId))
            {
                if (!p.Visible) continue;
                var look = p.Placed ? BlockLook.Placed : p.Dim ? BlockLook.Dim : BlockLook.Holdable;
                int b = p.Raised ? Order.Raised : Order.Pieces;
                float sc = p.Scale.X * p.Grow;
                double cx = p.Position.X, cy = p.Position.Y - p.Hop;
                if (p.Lifted)
                {
                    // B2: the shadow drops away and softens with the lift height (UnityPieceVisual: LiftShadow.Pose)
                    LiftShadow.Pose(p.LiftHeight, new Feel.FeelSettings(), out float sdy, out float ssc, out float sa);
                    double bw = (p.Shape.W + 0.8) * cell * sc * ssc, bh = 0.5 * cell * sc * ssc;
                    Add(new Sprite { Frame = Frames.DropBlob, Rect = new PxRect(cx - bw / 2, cy + p.Shape.H * cell * sc / 2 - bh / 2 + Palette.LiftedShadowY + sdy, bw, bh), Order = b - 1, Alpha = sa });
                }
                foreach (var part in BlockSkin.Block(p.Shape, p.Color, cell, look, ArtContractTests.Atlas))
                {
                    var r = part.Rect;
                    Add(new Sprite
                    {
                        Frame = part.Frame,
                        Rect = new PxRect(cx + r.X * sc, cy + r.Y * sc, r.W * sc, r.H * sc),
                        Order = b + part.Order,
                        Tint = part.Tint,
                        Alpha = p.Alpha,
                    });
                }
            }
            var sh = v.Shadow;
            if (sh != null)
            {
                int ob = sh.Look.Body ? Order.Ghost : Order.Raised + 3;
                foreach (var part in BlockSkin.Shadow(sh.Shape, sh.ColorIndex, sh.CellPx, sh.Look))
                    Add(new Sprite { Frame = part.Frame, Rect = new PxRect(sh.Center.X + part.Rect.X, sh.Center.Y + part.Rect.Y, part.Rect.W, part.Rect.H), Order = ob + part.Order, Tint = part.Tint });
                if (sh.BadgeTile.HasValue)
                {
                    var c = sh.BadgeCenter(56);
                    Add(new Sprite { Frame = sh.Look.Badge == ShadowBadge.Ok ? "icon_check" : "badge_dot", Rect = new PxRect(c.X - 28, c.Y - 28, 56, 56), Order = Order.GhostBadge });
                }
            }
            // B2: the 3D FX clip sprites (world depths; the Ui depth is drawn over the windows by `Scene`)
            foreach (var fx in v.FxOut)
            {
                if (!fx.Visible || fx.Frame == null || fx.Depth == Anim.FxDepth.Ui) continue;
                var r = FxRect(fx);
                if (r == null) continue;
                Add(new Sprite { Frame = fx.Frame, Rect = r.Value, Order = fx.Depth == Anim.FxDepth.Under ? Order.Pieces - 8 : Order.Flyer + 4, Tint = fx.Tint, Alpha = fx.Alpha });
            }
            var o = JsonValue.Array();
            foreach (var s in list.OrderBy(s => s.Order).ThenBy(s => s.Seq))
            {
                var j = JsonValue.Object().Set("f", JsonValue.Of(s.Frame)).Set("x", N(s.Rect.X)).Set("y", N(s.Rect.Y)).Set("w", N(s.Rect.W)).Set("h", N(s.Rect.H));
                if (!s.Tint.Equals(Rgba.White)) j.Set("t", Hex(s.Tint));
                if (s.Alpha < 1f) j.Set("a", N(s.Alpha));
                if (s.Sliced) j.Set("s", JsonValue.True);
                if (s.FlipY) j.Set("fy", JsonValue.True);
                o.Add(j);
            }
            return o;
        }

        /// <summary>B2: the design rectangle of an FX sprite (its 3D frame placed by its pivot at `Position`, × `Scale`).</summary>
        public static PxRect? FxRect(FakeFx fx)
        {
            var f = fx.Frame != null ? TestArt.Set.Frame(fx.Frame) : null;
            if (f == null) return null;
            return Anim.ClipPlacement.Rect(f, fx.Position.X, fx.Position.Y, 1, fx.Scale.X, fx.Scale.Y);
        }

        public static JsonValue Node(UiNode n)
        {
            var j = JsonValue.Object().Set("id", JsonValue.Of(n.Id)).Set("x", N(n.Rect.X)).Set("y", N(n.Rect.Y)).Set("w", N(n.Rect.W)).Set("h", N(n.Rect.H));
            if (n.Frame != null) j.Set("f", JsonValue.Of(n.Frame));
            if (n.Sliced) j.Set("s", JsonValue.True);
            if (n.PreserveAspect) j.Set("pa", JsonValue.True);
            if (!n.Tint.Equals(Rgba.White)) j.Set("t", Hex(n.Tint));
            if (n.Alpha < 1f) j.Set("a", N(n.Alpha));
            if (n.Rotation != 0) j.Set("r", N(n.Rotation));
            if (!n.Visible) j.Set("hidden", JsonValue.True);
            if (n.Text != null)
            {
                int size = TextFit.SizeOf(n); // what the Unity layer draws (UiRenderer)
                var look = TextStyles.Of(n.Style, size, n.TextFill, n.TextStroke);
                j.Set("text", JsonValue.Of(n.Text)).Set("size", N(size)).Set("align", JsonValue.Of(n.Align.ToString().ToLowerInvariant()))
                    .Set("fill", Hex(look.Fill)).Set("outline", Hex(look.Outline)).Set("outlinePx", N(look.OutlinePx)).Set("shadow", Hex(look.Shadow)).Set("shadowPx", N(look.ShadowPx));
                if (n.Alpha < 1f) j.Set("ta", N(n.Alpha));
            }
            if (n.Children.Count > 0)
            {
                var c = JsonValue.Array();
                foreach (var ch in n.Children) c.Add(Node(ch));
                j.Set("children", c);
            }
            return j;
        }

        /// <summary>One scene: world + the visible UI layers (Home, Hud, Window, Overlay) of an AppRig.</summary>
        public static JsonValue Scene(string name, AppRig rig, IEnumerable<UiNode>? extra = null)
        {
            var ui = JsonValue.Array();
            foreach (var layer in new[] { UiLayer.Home, UiLayer.Hud, UiLayer.Window, UiLayer.Fx, UiLayer.Overlay })
            {
                if (rig.Ui.Visible(layer)) ui.Add(Node(rig.Ui.Root(layer)!));
                if (layer == UiLayer.Window)
                {
                    // B2: the uGUI FX (win star burst, trowel sparkle) sit on the Fx layer, over the window
                    var g = UiKit.Group("fx", new PxRect(0, 0, rig.Player.Viewport.W, rig.Player.Viewport.H));
                    foreach (var fx in rig.View.FxOut)
                    {
                        if (!fx.Visible || fx.Frame == null || fx.Depth != Anim.FxDepth.Ui) continue;
                        var r = FxRect(fx);
                        if (r != null) g.Add(new UiNode("fx." + g.Children.Count, r.Value) { Frame = fx.Frame, Tint = fx.Tint, Alpha = fx.Alpha });
                    }
                    if (g.Children.Count > 0) ui.Add(Node(g));
                }
            }
            if (extra != null) foreach (var n in extra) ui.Add(Node(n));
            var vp = rig.Player.Viewport;
            return JsonValue.Object()
                .Set("name", JsonValue.Of(name))
                .Set("canvas", JsonValue.Object().Set("w", N(vp.W)).Set("h", N(vp.H)))
                .Set("clear", JsonValue.Of(Palette.GameBottom))
                .Set("world", World(rig.View))
                .Set("ui", ui);
        }

        public static void Write(string dir, JsonValue scene)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, scene["name"]!.AsString + ".json"), JsonWriter.Write(scene, false));
        }
    }
}
