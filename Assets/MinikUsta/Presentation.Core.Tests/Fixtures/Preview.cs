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
            /// <summary>Degrees counter-clockwise on screen, about (Cx, Cy).</summary>
            public double Rot;
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

        public void Add(Tile t, double cx, double cy, double w, double h, Rgba color, int order, float alpha = 1f, bool sliced = false, double rot = 0) =>
            cmds.Add(new Cmd { Tile = t, Cx = cx, Cy = cy, W = w, H = h, Color = color, Alpha = alpha, Sliced = sliced, Order = order, Seq = cmds.Count, Rot = rot });

        public void Add(Tile t, PxRect r, Rgba color, int order, bool sliced = false) => Add(t, r.CenterX, r.CenterY, r.W, r.H, color, order, 1f, sliced);

        /// <summary>The whole fake view: board, blocks, shadow, HUD panels, the open window.</summary>
        public static Preview Of(FakeView v, LevelPlayer p, int screenW, int screenH)
        {
            var o = new Preview(screenW, screenH, p.Viewport);
            var L = p.Layout!;
            foreach (var item in v.Board!.Items)
            {
                if (item.ArtOnly) continue; // V1 atlas-only items (tray, hook, scaffold …): the procedural preview draws the placeholder look
                var rect = item.Rect;
                // Q2: the site layer (blueprint, plan, platform) slides with the segment change (UnityBoardView.siteRoot)
                if (item.Kind == BoardItemKind.SitePanel || item.Kind == BoardItemKind.PlanCell || item.Kind == BoardItemKind.HatchCell
                    || item.Kind == BoardItemKind.HiddenCell || item.Kind == BoardItemKind.Platform)
                    rect = new PxRect(rect.X + v.SiteLayer.Position.X, rect.Y + v.SiteLayer.Position.Y, rect.W, rect.H);
                o.Add(item.Tile, rect, item.Color, item.Order, item.Sliced);
            }
            double c = L.Grid.CellPx;
            foreach (var piece in v.Live.Values)
            {
                if (!piece.Visible) continue;
                int b = piece.Raised ? Order.Raised : Order.Pieces;
                var baseColor = Palette.Block(piece.Color);
                if (piece.Dim) baseColor = baseColor.Scale(0.92f);
                var body = Rgba.Lerp(baseColor, Rgba.White, piece.Flash);
                body = Rgba.Lerp(body, Rgba.Hex(Palette.GhostInvalid), piece.Tint);
                double gx = piece.Scale.X * piece.Grow, gy = piece.Scale.Y * piece.Grow;
                for (int ci = 0; ci < piece.Shape.Cells.Length; ci++)
                {
                    var cell = piece.Shape.Cells[ci];
                    var off = BoardPose.CellOffset(piece.Shape, cell, c);
                    // Q2: a cell squashed about the bottom centre of the block box (UnityPieceVisual.PlaceCell)
                    double csx = 1, csy = 1;
                    if (ci < piece.CellScales.Length)
                    {
                        csx = piece.CellScales[ci].X;
                        csy = piece.CellScales[ci].Y;
                    }
                    double bottom = piece.Shape.H * c / 2.0;
                    double lx = off.X * csx, ly = bottom + (off.Y - bottom) * csy;
                    double x = piece.Position.X + lx * gx, y = piece.Position.Y - piece.Hop + ly * gy;
                    double w = c * csx * gx, h = c * csy * gy;
                    var tile = Atlas.Body(BoardPose.NeighbourMask(piece.Shape, cell));
                    if (piece.Lifted) o.Add(tile, x, y + Palette.LiftedShadowY * piece.Scale.Y, w, h, Rgba.Black.WithAlpha(Palette.LiftedShadowAlpha), b - 1, piece.Alpha);
                    o.Add(tile, x, y, w, h, body, b, piece.Alpha);
                    o.Add(Tile.Gloss, x, y, w, h, Rgba.White, b + 1, piece.Alpha);
                    o.Add(Atlas.Symbol(piece.Color), x, y, w * 0.42, h * 0.42, Palette.SymbolInk(piece.Color), b + 2, piece.Alpha);
                }
            }
            // Q2: the truck (behind the blocks), sprite particles (over everything on the board)
            if (v.Truck.Visible)
            {
                var truck = v.Truck;
                double cp = truck.CellPx > 0 ? truck.CellPx : c;
                double th = truck.Rotation * Math.PI / 180.0;
                int z = 0;
                foreach (var part in TruckModel.Parts())
                {
                    double px = part.X * cp * truck.Scale.X, py = part.Y * cp * truck.Scale.Y;
                    // rotate the part's offset about the truck centre (counter-clockwise on screen, y down)
                    double rx = px * Math.Cos(th) + py * Math.Sin(th), ry = -px * Math.Sin(th) + py * Math.Cos(th);
                    o.Add(part.Tile, truck.Position.X + rx, truck.Position.Y + ry, part.W * cp * truck.Scale.X, part.H * cp * truck.Scale.Y, part.Color, 90 + z, truck.Alpha, part.Sliced, truck.Rotation);
                    z++;
                }
            }
            foreach (var sp in v.SparksOut)
            {
                if (!sp.Visible) continue;
                Tile tile;
                switch (sp.Kind)
                {
                    case SparkKind.Dust: tile = Tile.Soft; break;
                    case SparkKind.Star: tile = Tile.Star; break;
                    case SparkKind.Coin: tile = Tile.Coin; break;
                    case SparkKind.Confetti: tile = Tile.Plain; break;
                    case SparkKind.Ring: tile = Tile.Ring; break;
                    default: tile = Tile.Spark; break;
                }
                double aspect = sp.Kind == SparkKind.Confetti ? 0.5 : 1.0;
                o.Add(tile, sp.Position.X, sp.Position.Y, sp.SizePx * sp.Scale.X, sp.SizePx * aspect * sp.Scale.Y, sp.Color, Order.Flyer + (sp.Kind == SparkKind.Ring ? 1 : 2), sp.Alpha, false, sp.Rotation);
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
            // Q2: streak capsule (beads, trowel), gold chip
            o.Add(Tile.Panel, HudLayout.Streak(L), new Rgba(20, 15, 51, 199), hud, true);
            var goldC = Rgba.Hex(Palette.Gold);
            for (int i = 0; i < 4; i++)
            {
                var br = HudLayout.Bead(L, i);
                double bs = v.Beads[i].Scale.X;
                o.Add(Tile.Circle, br.CenterX, br.CenterY, br.W * bs, br.H * bs, i < v.StreakBeads ? goldC : Rgba.White.WithAlpha(0.25f), hud + 1);
            }
            var tr = HudLayout.TrowelIcon(L);
            double ts = v.TrowelIcon.Scale.X;
            o.Add(Tile.Trowel, tr.CenterX, tr.CenterY, tr.W * ts, tr.H * ts, v.StreakTrowels > 0 ? goldC : Rgba.White.WithAlpha(0.35f), hud + 1, 1f, false, v.TrowelIcon.Rotation);
            if (v.Gold >= 0)
            {
                var gc = HudLayout.GoldChip(L);
                double gs = v.GoldChip.Scale.X;
                o.Add(Tile.Panel, gc.CenterX, gc.CenterY, gc.W * gs, gc.H * gs, neutral, hud, 1f, true);
                o.Add(Tile.Coin, gc.X + 32 * gs + (gc.W * (1 - gs)) / 2, gc.CenterY, 44 * gs, 44 * gs, goldC, hud + 1);
            }
            if (v.Panel != null)
            {
                double extraH = (screenH / p.Viewport.Scale - L.H) / 2, extraW = (screenW / p.Viewport.Scale - L.W) / 2;
                o.Add(Tile.Plain, new PxRect(-extraW, -extraH, L.W + 2 * extraW, L.H + 2 * extraH), Rgba.Hex(Palette.Overlay, Palette.OverlayAlpha), hud + 10);
                var w = HudLayout.Window(L, p.LayoutSettings, v.Panel.Buttons.Length);
                o.Add(Tile.Panel, w.Panel, Rgba.Hex(Palette.Panel), hud + 11, true);
                for (int i = 0; i < v.Panel.Buttons.Length; i++)
                    o.Add(Tile.Panel, w.Options[i], v.Panel.Buttons[i].Primary ? Rgba.Hex(Palette.Primary) : Rgba.Hex(Palette.Neutral), hud + 12, true);
                if (v.Panel.Star)
                {
                    var sr = HudLayout.WindowStar(L, p.LayoutSettings, v.Panel.Buttons.Length);
                    double ss = v.StarTarget.Scale.X;
                    o.Add(Tile.Star, sr.CenterX, sr.CenterY, sr.W * ss, sr.H * ss, goldC, hud + 13);
                }
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
            double th = c.Rot * Math.PI / 180.0, cs = Math.Cos(th), sn = Math.Sin(th);
            double ccx = (c.Cx) * vp.Scale + vp.PillarboxPx / 2, ccy = (c.Cy) * vp.Scale + vp.LetterboxPx / 2;
            if (c.Rot != 0)
            {
                double rad = Math.Sqrt(sw * sw + sh * sh) / 2 + 1;
                x0 = Math.Max(0, (int)Math.Floor(ccx - rad));
                x1 = Math.Min(width, (int)Math.Ceiling(ccx + rad));
                y0 = Math.Max(0, (int)Math.Floor(ccy - rad));
                y1 = Math.Min(height, (int)Math.Ceiling(ccy + rad));
            }
            // 9-slice border: tile px → design px (sprite ppu = Content, world unit = 100 design px)
            double bd = Atlas.PanelBorder / (double)Atlas.Content * ScreenFit.DefaultPixelsPerUnit;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    double lx, ly;
                    if (c.Rot != 0)
                    {
                        // rotate the pixel back into the sprite's frame (counter-clockwise on screen, y down)
                        double dx = x + 0.5 - ccx, dy = y + 0.5 - ccy;
                        double qx = dx * cs - dy * sn, qy = dx * sn + dy * cs;
                        lx = (qx + sw / 2) / vp.Scale;
                        ly = (qy + sh / 2) / vp.Scale;
                    }
                    else
                    {
                        lx = (x + 0.5 - sx0) / vp.Scale;
                        ly = (y + 0.5 - sy0) / vp.Scale;
                    }
                    if (ly < 0 || ly >= c.H) continue;
                    double v = c.Sliced ? SliceMap(ly, c.H, bd) : ly / c.H * Atlas.Content;
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
