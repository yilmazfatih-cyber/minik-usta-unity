#nullable enable
// Software preview of the Unity placeholder screen (Faz 3 Q1). No Unity Editor runs in the cloud, so this compositor
// draws exactly what the Unity layer draws — the same atlas bytes, tile per sprite, tint, alpha, sorting order, 9-slice of
// `Tile.Panel`, the block cell parts of UnityPieceVisual, the shadow of UnityShadowView and the HUD panels of UnityHud
// (texts are drawn by Unity's LegacyRuntime font and are NOT drawn here) — into a PNG at a phone size (FIT letterbox).
// It is a layout / colour check, not a Unity screenshot. Only APIs Unity's .NET profile has (DeflateStream, no ZLibStream).
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public sealed class Preview
    {
        private struct Cmd
        {
            public Tile Tile;
            public double Cx, Cy, W, H; // design px
            public Rgba Color;
            public float Alpha;
            public bool Sliced;
            public int Order;
            public int Seq;
        }

        private static Atlas? atlas;
        private readonly Atlas a;
        private readonly int width;
        private readonly int height;
        private readonly float[] px;
        private readonly DesignViewport vp;
        private readonly List<Cmd> cmds = new List<Cmd>();

        public Preview(int screenW, int screenH, DesignViewport viewport)
        {
            a = atlas ??= Atlas.Build();
            width = screenW;
            height = screenH;
            vp = viewport;
            px = new float[width * height * 3];
            var bg = Rgba.Hex(Palette.GameBottom); // camera background (letterbox)
            for (int i = 0; i < width * height; i++)
            {
                px[3 * i] = bg.Rf;
                px[3 * i + 1] = bg.Gf;
                px[3 * i + 2] = bg.Bf;
            }
        }

        public void Add(Tile t, double cx, double cy, double w, double h, Rgba color, int order, float alpha = 1f, bool sliced = false) =>
            cmds.Add(new Cmd { Tile = t, Cx = cx, Cy = cy, W = w, H = h, Color = color, Alpha = alpha, Sliced = sliced, Order = order, Seq = cmds.Count });

        public void Add(Tile t, PxRect r, Rgba color, int order, bool sliced = false) => Add(t, r.CenterX, r.CenterY, r.W, r.H, color, order, 1f, sliced);

        /// <summary>The whole fake view: board, blocks, shadow, HUD panels, the open window.</summary>
        public static Preview Of(FakeView v, LevelPlayer p, int screenW, int screenH)
        {
            var o = new Preview(screenW, screenH, p.Viewport);
            var L = p.Layout!;
            foreach (var item in v.Board!.Items) o.Add(item.Tile, item.Rect, item.Color, item.Order, item.Sliced);
            double c = L.Grid.CellPx;
            foreach (var piece in v.Live.Values)
            {
                if (!piece.Visible) continue;
                int b = piece.Raised ? Order.Raised : Order.Pieces;
                var baseColor = Palette.Block(piece.Color);
                if (piece.Dim) baseColor = baseColor.Scale(0.92f);
                var body = Rgba.Lerp(baseColor, Rgba.White, piece.Flash);
                body = Rgba.Lerp(body, Rgba.Hex(Palette.GhostInvalid), piece.Tint);
                foreach (var cell in piece.Shape.Cells)
                {
                    var off = BoardPose.CellOffset(piece.Shape, cell, c);
                    double x = piece.Position.X + off.X * piece.Scale.X, y = piece.Position.Y - piece.Hop + off.Y * piece.Scale.Y;
                    double w = c * piece.Scale.X, h = c * piece.Scale.Y;
                    var tile = Atlas.Body(BoardPose.NeighbourMask(piece.Shape, cell));
                    if (piece.Lifted) o.Add(tile, x, y + Palette.LiftedShadowY * piece.Scale.Y, w, h, Rgba.Black.WithAlpha(Palette.LiftedShadowAlpha), b - 1, piece.Alpha);
                    o.Add(tile, x, y, w, h, body, b, piece.Alpha);
                    o.Add(Tile.Gloss, x, y, w, h, Rgba.White, b + 1, piece.Alpha);
                    o.Add(Atlas.Symbol(piece.Color), x, y, w * 0.42, h * 0.42, Palette.SymbolInk(piece.Color), b + 2, piece.Alpha);
                }
            }
            var s = v.Shadow;
            if (s != null)
            {
                int outline = s.Look.Body ? Order.Ghost + 1 : Order.Raised + 3;
                foreach (var cell in s.Shape.Cells)
                {
                    var off = BoardPose.CellOffset(s.Shape, cell, s.CellPx);
                    double x = s.Center.X + off.X, y = s.Center.Y + off.Y;
                    if (s.Look.Body) o.Add(Atlas.Body(BoardPose.NeighbourMask(s.Shape, cell)), x, y, s.CellPx, s.CellPx, s.BodyColor, Order.Ghost);
                    o.Add(Tile.Ghost, x, y, s.CellPx, s.CellPx, s.OutlineColor, outline);
                }
                if (s.BadgeTile.HasValue)
                {
                    var bc = s.BadgeCenter(56);
                    o.Add(s.BadgeTile.Value, bc.X, bc.Y, 56, 56, s.BadgeColor, Order.GhostBadge);
                }
            }
            // uGUI overlay canvas (UnityHud): above every sprite
            const int hud = 10000;
            var neutral = Rgba.Hex(Palette.Neutral);
            o.Add(Tile.Panel, HudLayout.Capsule(L), neutral, hud, true);
            o.Add(Tile.Panel, HudLayout.BlocksChip(L), neutral, hud, true);
            o.Add(Tile.Panel, HudLayout.Segments(L), Rgba.Hex(Palette.BlueprintDeep), hud, true);
            o.Add(Tile.Panel, HudLayout.Moves(L), Rgba.Hex(Palette.MovesCream), hud, true);
            o.Add(Tile.Panel, HudLayout.MovesBand(L, p.LayoutSettings), Rgba.Hex(Palette.MovesBand), hud + 1, true);
            var pause = HudLayout.Pause(L);
            o.Add(Tile.Panel, pause, Rgba.Hex(Palette.Secondary), hud, true);
            o.Add(Tile.Pause, new PxRect(pause.X + 24, pause.Y + 24, pause.W - 48, pause.H - 48), Rgba.White, hud + 1);
            var icons = new[] { Tile.Hammer, Tile.CraneHook, Tile.Brush, Tile.Undo };
            for (int i = 0; i < L.Boosters.Length; i++)
            {
                var r = L.Boosters[i];
                o.Add(Tile.Panel, r, neutral.WithAlpha(0.55f), hud, true);
                o.Add(icons[i], new PxRect(r.X + 28, r.Y + 28, r.W - 56, r.H - 56), Rgba.Hex(Palette.Ink, 0.45f), hud + 1);
            }
            if (v.Panel != null)
            {
                double extraH = (screenH / p.Viewport.Scale - L.H) / 2, extraW = (screenW / p.Viewport.Scale - L.W) / 2;
                o.Add(Tile.Plain, new PxRect(-extraW, -extraH, L.W + 2 * extraW, L.H + 2 * extraH), Rgba.Hex(Palette.Overlay, Palette.OverlayAlpha), hud + 10);
                var w = HudLayout.Window(L, p.LayoutSettings, v.Panel.Buttons.Length);
                o.Add(Tile.Panel, w.Panel, Rgba.Hex(Palette.Panel), hud + 11, true);
                for (int i = 0; i < v.Panel.Buttons.Length; i++)
                    o.Add(Tile.Panel, w.Options[i], v.Panel.Buttons[i].Primary ? Rgba.Hex(Palette.Primary) : Rgba.Hex(Palette.Neutral), hud + 12, true);
            }
            return o;
        }

        private void Draw()
        {
            cmds.Sort((x, y) => x.Order != y.Order ? x.Order.CompareTo(y.Order) : x.Seq.CompareTo(y.Seq));
            foreach (var c in cmds) DrawOne(c);
        }

        private void DrawOne(Cmd c)
        {
            var r = Atlas.RectOf(c.Tile);
            double sx0 = (c.Cx - c.W / 2) * vp.Scale + vp.PillarboxPx / 2, sy0 = (c.Cy - c.H / 2) * vp.Scale + vp.LetterboxPx / 2;
            double sw = c.W * vp.Scale, sh = c.H * vp.Scale;
            int x0 = Math.Max(0, (int)Math.Floor(sx0)), x1 = Math.Min(width, (int)Math.Ceiling(sx0 + sw));
            int y0 = Math.Max(0, (int)Math.Floor(sy0)), y1 = Math.Min(height, (int)Math.Ceiling(sy0 + sh));
            // 9-slice border: tile px → design px (sprite ppu = Content, world unit = 100 design px)
            double bd = Atlas.PanelBorder / (double)Atlas.Content * ScreenFit.DefaultPixelsPerUnit;
            for (int y = y0; y < y1; y++)
            {
                double ly = (y + 0.5 - sy0) / vp.Scale; // design px from the sprite top
                if (ly < 0 || ly >= c.H) continue;
                double v = c.Sliced ? SliceMap(ly, c.H, bd) : ly / c.H * Atlas.Content;
                for (int x = x0; x < x1; x++)
                {
                    double lx = (x + 0.5 - sx0) / vp.Scale;
                    if (lx < 0 || lx >= c.W) continue;
                    double u = c.Sliced ? SliceMap(lx, c.W, bd) : lx / c.W * Atlas.Content;
                    Sample(r, u, Atlas.Content - v, out float sr, out float sg, out float sb, out float sa);
                    float alpha = sa * c.Color.Af * c.Alpha;
                    if (alpha <= 0) continue;
                    int i = 3 * (y * width + x);
                    px[i] = px[i] * (1 - alpha) + sr * c.Color.Rf * alpha;
                    px[i + 1] = px[i + 1] * (1 - alpha) + sg * c.Color.Gf * alpha;
                    px[i + 2] = px[i + 2] * (1 - alpha) + sb * c.Color.Bf * alpha;
                }
            }
        }

        private static double SliceMap(double l, double size, double bd)
        {
            double b = Atlas.PanelBorder;
            double k = Math.Min(1, size / (2 * bd));
            if (l < bd * k) return l / k / bd * b;
            if (l > size - bd * k) return Atlas.Content - (size - l) / k / bd * b;
            return b + (l - bd * k) / Math.Max(1e-6, size - 2 * bd * k) * (Atlas.Content - 2 * b);
        }

        /// <summary>Bilinear sample of tile-local (u, v) (v up), clamped into the extruded tile.</summary>
        private void Sample(TileRect r, double u, double v, out float cr, out float cg, out float cb, out float ca)
        {
            double fx = r.X + u - 0.5, fy = r.Y + v - 0.5;
            fx = Math.Max(r.X - Atlas.Pad, Math.Min(r.X + r.W + Atlas.Pad - 1.001, fx));
            fy = Math.Max(r.Y - Atlas.Pad, Math.Min(r.Y + r.H + Atlas.Pad - 1.001, fy));
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            float tx = (float)(fx - ix), ty = (float)(fy - iy);
            var p00 = a.At(ix, iy);
            var p10 = a.At(ix + 1, iy);
            var p01 = a.At(ix, iy + 1);
            var p11 = a.At(ix + 1, iy + 1);
            float W(byte b00, byte b10, byte b01, byte b11) =>
                ((b00 * (1 - tx) + b10 * tx) * (1 - ty) + (b01 * (1 - tx) + b11 * tx) * ty) / 255f;
            cr = W(p00.R, p10.R, p01.R, p11.R);
            cg = W(p00.G, p10.G, p01.G, p11.G);
            cb = W(p00.B, p10.B, p01.B, p11.B);
            ca = W(p00.A, p10.A, p01.A, p11.A);
        }

        /// <summary>Composites every sprite and returns the PNG (8-bit RGB).</summary>
        public byte[] Png()
        {
            Draw();
            var raw = new byte[height * (width * 3 + 1)];
            int k = 0;
            for (int y = 0; y < height; y++)
            {
                raw[k++] = 0;
                for (int x = 0; x < width; x++)
                    for (int ch = 0; ch < 3; ch++) raw[k++] = (byte)Math.Round(Math.Max(0, Math.Min(1, px[3 * (y * width + x) + ch])) * 255);
            }
            using var o = new MemoryStream();
            o.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13];
            Be(ihdr, 0, (uint)width);
            Be(ihdr, 4, (uint)height);
            ihdr[8] = 8;
            ihdr[9] = 2;
            Chunk(o, "IHDR", ihdr);
            Chunk(o, "IDAT", Zlib(raw));
            Chunk(o, "IEND", new byte[0]);
            return o.ToArray();
        }

        private static byte[] Zlib(byte[] data)
        {
            using var o = new MemoryStream();
            o.WriteByte(0x78);
            o.WriteByte(0x9C);
            using (var d = new DeflateStream(o, CompressionLevel.Optimal, true)) d.Write(data, 0, data.Length);
            uint s1 = 1, s2 = 0;
            foreach (byte b in data)
            {
                s1 = (s1 + b) % 65521;
                s2 = (s2 + s1) % 65521;
            }
            var adler = new byte[4];
            Be(adler, 0, (s2 << 16) | s1);
            o.Write(adler, 0, 4);
            return o.ToArray();
        }

        private static void Chunk(Stream o, string type, byte[] data)
        {
            var len = new byte[4];
            Be(len, 0, (uint)data.Length);
            o.Write(len, 0, 4);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Array.Copy(data, 0, td, 4, data.Length);
            o.Write(td, 0, td.Length);
            var crc = new byte[4];
            Be(crc, 0, Crc32(td));
            o.Write(crc, 0, 4);
        }

        private static uint[]? crcTable;

        private static uint Crc32(byte[] d)
        {
            var t = crcTable;
            if (t == null)
            {
                t = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    t[n] = c;
                }
                crcTable = t;
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in d) crc = t[(crc ^ b) & 0xff] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        private static void Be(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v >> 24);
            b[at + 1] = (byte)(v >> 16);
            b[at + 2] = (byte)(v >> 8);
            b[at + 3] = (byte)v;
        }
    }
}
