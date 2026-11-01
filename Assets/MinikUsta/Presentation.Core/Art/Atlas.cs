#nullable enable
// The placeholder texture atlas (Faz 3 Q1; TECH §10.6 "dokular açılışta bir kez üretilir"): every block, plan, board and
// UI tile is rasterised ONCE at boot into one RGBA image, so every sprite shares one texture and one material (the sprite
// batcher draws the board in a few calls). Pure and deterministic: the same build gives the same bytes (AtlasTests); the
// Unity layer only copies the bytes into a Texture2D and cuts sprites at `TileRect`.
//
// Layout: `Size` × `Size` px, 8 × 8 tiles with a stride of `Stride` px; each tile draws in its `Content` px square in the
// middle and its edge pixels are extruded `Pad` px outward (bilinear sampling never pulls in a neighbour tile, and the
// flush edges of connected block cells stay seamless). Pixel rows run BOTTOM-UP (Unity `Texture2D.SetPixels32` order);
// tile (col, row) row 0 is the bottom row.
//
// The looks are deliberately simple (orchestrator brief: rounded boxes, simple symbols, no studs; correctness and feel
// first). Grey shading tiles are TINTED by the sprite colour (base × shade: tokens blockV2 `outlineFactor` 0,42,
// `darkFactor` 0,68, `bottomFactor` 0,92); white tiles take any colour.
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Art
{
    /// <summary>Tiles of the atlas. `Body0 + mask`: mask bits 1 left, 2 right, 4 down, 8 up = a neighbour cell of the same piece on that side.</summary>
    public enum Tile
    {
        Body0 = 0,
        Body15 = 15,
        /// <summary>Gloss highlight of a block cell (white; ART §3A gloss ellipse + dot).</summary>
        Gloss = 16,
        /// <summary>Colour symbols (white masks) in `ColorCodes.All` order (ART §3.1, simplified).</summary>
        SymW = 17,
        SymY = 18,
        SymG = 19,
        SymR = 20,
        SymO = 21,
        SymC = 22,
        SymB = 23,
        SymP = 24,
        /// <summary>Yard floor cell.</summary>
        Floor = 25,
        /// <summary>Plan cell (tinted with the plan colour; darker 4 px stroke, tokens plan.strokePx / strokeFactor).</summary>
        Plan = 26,
        /// <summary>`.` plan cell that must stay empty: 45° hatch (plan.hatchSpacingPx 16, hatchWidthPx 4).</summary>
        Hatch = 27,
        /// <summary>Unrevealed `?` plan cell (K-32).</summary>
        Hidden = 28,
        /// <summary>K-18 shadow outline of one cell (stroke only).</summary>
        Ghost = 29,
        /// <summary>Rounded panel for 9-slicing (border `PanelBorder`), bottom lip.</summary>
        Panel = 30,
        /// <summary>Opaque white square (bands, overlays, wall).</summary>
        Plain = 31,
        /// <summary>The indigo game background (color.scene gameTop → gameBottom), NOT tinted.</summary>
        Gradient = 32,
        Circle = 33,
        /// <summary>Shadow badges (UX §5.4): ✓ and !.</summary>
        Check = 34,
        Warn = 35,
        /// <summary>The pause tool icon (two bars).</summary>
        Pause = 36,
        /// <summary>Gap opening of the wall (W1 rails: two dark rails on a dark hole).</summary>
        Gap = 37,
        /// <summary>The cancel badge ↩ (UX §5.3).</summary>
        CancelBadge = 38,
        /// <summary>Booster bar icons (UX §5.1: hammer, crane, brush, undo; white masks).</summary>
        Hammer = 39,
        CraneHook = 40,
        Brush = 41,
        Undo = 42,
        /// <summary>Q2: four-point sparkle (fx_spark / fx_sparkle4; white mask, tinted by the particle colour).</summary>
        Spark = 43,
        /// <summary>Q2: soft round puff (dust; white, radial falloff).</summary>
        Soft = 44,
        /// <summary>Q2: five-point star (win star, star burst; white mask).</summary>
        Star = 45,
        /// <summary>Q2: coin (tinted gold): a disc with a darker rim and an inner ring.</summary>
        Coin = 46,
        /// <summary>Q2: the Golden Trowel icon (white mask).</summary>
        Trowel = 47,
        /// <summary>Q2: ring outline (star burst ring, lock flash; white).</summary>
        Ring = 48,
        /// <summary>
        /// C1: the tutorial highlight (K-53), a 9-slice frame (border `PanelBorder`): a rounded outline 10 px thick centred on a box that sits 20 px inside
        /// the tile, with a soft glow falling off outside it. White mask, tinted gold at use (`FallbackTiles` "tut_highlight").
        /// </summary>
        Highlight = 49,
    }

    public readonly struct TileRect
    {
        /// <summary>Content square, atlas px, origin bottom-left.</summary>
        public readonly int X;
        public readonly int Y;
        public readonly int W;
        public readonly int H;

        public TileRect(int x, int y, int w, int h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }

    public sealed class Atlas
    {
        public const int Columns = 8;
        public const int Content = 120;
        public const int Pad = 4;
        public const int Stride = Content + 2 * Pad;
        public const int Size = Columns * Stride;
        /// <summary>9-slice border of `Tile.Panel` (px of the tile).</summary>
        public const int PanelBorder = 40;
        /// <summary>Sprites cut from this atlas: one world unit = one tile (`Content` px).</summary>
        public const float PixelsPerUnit = Content;

        /// <summary>RGBA, `Size` × `Size`, rows bottom-up.</summary>
        public readonly byte[] Pixels;

        private Atlas(byte[] pixels)
        {
            Pixels = pixels;
        }

        /// <summary>The last tile (the atlas holds `Last + 1` tiles).</summary>
        public const Tile Last = Tile.Highlight;

        public static Tile Body(int mask) => (Tile)((int)Tile.Body0 + (mask & 15));

        public static Tile Symbol(int colorIndex) => (Tile)((int)Tile.SymW + (colorIndex >= 0 && colorIndex < 8 ? colorIndex : 0));

        public static TileRect RectOf(Tile t)
        {
            int i = (int)t;
            int col = i % Columns;
            int row = i / Columns;
            return new TileRect(col * Stride + Pad, row * Stride + Pad, Content, Content);
        }

        /// <summary>Every tile in index order.</summary>
        public static IEnumerable<Tile> All
        {
            get
            {
                for (int i = 0; i <= (int)Last; i++) yield return (Tile)i;
            }
        }

        /// <summary>Rasterises every tile (deterministic).</summary>
        public static Atlas Build()
        {
            var px = new byte[Size * Size * 4];
            var canvas = new Canvas(px, Size);
            foreach (var t in All)
            {
                var r = RectOf(t);
                Draw(canvas, t, r);
                Extrude(canvas, r);
            }
            return new Atlas(px);
        }

        /// <summary>RGBA of atlas pixel (x, y), y from the bottom.</summary>
        public Rgba At(int x, int y)
        {
            int i = (y * Size + x) * 4;
            return new Rgba(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
        }

        /// <summary>RGBA of tile-local pixel (u, v) of `t` (0 … Content − 1, v from the bottom).</summary>
        public Rgba At(Tile t, int u, int v)
        {
            var r = RectOf(t);
            return At(r.X + u, r.Y + v);
        }

        // --- drawing -----------------------------------------------------------------------------------------------------

        private sealed class Canvas
        {
            public readonly byte[] Px;
            public readonly int Size;

            public Canvas(byte[] px, int size)
            {
                Px = px;
                Size = size;
            }

            public void Set(int x, int y, float r, float g, float b, float a)
            {
                int i = (y * Size + x) * 4;
                Px[i] = Rgba.ToByte(r);
                Px[i + 1] = Rgba.ToByte(g);
                Px[i + 2] = Rgba.ToByte(b);
                Px[i + 3] = Rgba.ToByte(a);
            }

            public void Copy(int fx, int fy, int tx, int ty)
            {
                int a = (fy * Size + fx) * 4;
                int b = (ty * Size + tx) * 4;
                Px[b] = Px[a];
                Px[b + 1] = Px[a + 1];
                Px[b + 2] = Px[a + 2];
                Px[b + 3] = Px[a + 3];
            }
        }

        private delegate void Shader(float u, float v, out float r, out float g, out float b, out float a);

        /// <summary>Calls `shade` at every pixel centre of `r` (tile-local px, v up) and writes the result.</summary>
        private static void Fill(Canvas c, TileRect r, Shader shade)
        {
            for (int y = 0; y < r.H; y++)
            {
                for (int x = 0; x < r.W; x++)
                {
                    shade(x + 0.5f, y + 0.5f, out float cr, out float cg, out float cb, out float ca);
                    c.Set(r.X + x, r.Y + y, cr, cg, cb, ca);
                }
            }
        }

        private static void Extrude(Canvas c, TileRect r)
        {
            for (int y = r.Y; y < r.Y + r.H; y++)
            {
                for (int p = 1; p <= Pad; p++)
                {
                    c.Copy(r.X, y, r.X - p, y);
                    c.Copy(r.X + r.W - 1, y, r.X + r.W - 1 + p, y);
                }
            }
            for (int x = r.X - Pad; x < r.X + r.W + Pad; x++)
            {
                for (int p = 1; p <= Pad; p++)
                {
                    c.Copy(x, r.Y, x, r.Y - p);
                    c.Copy(x, r.Y + r.H - 1, x, r.Y + r.H - 1 + p);
                }
            }
        }

        private static void Draw(Canvas c, Tile t, TileRect r)
        {
            int i = (int)t;
            if (i <= (int)Tile.Body15)
            {
                DrawBody(c, r, i);
                return;
            }
            if (t >= Tile.SymW && t <= Tile.SymP)
            {
                var sym = Symbols.Of((int)t - (int)Tile.SymW);
                Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                {
                    cr = cg = cb = 1f;
                    ca = sym.Coverage(u / Content * 100f, 100f - v / Content * 100f, Content / 100f);
                });
                return;
            }
            switch (t)
            {
                case Tile.Gloss:
                    Fill(c, r, Gloss);
                    return;
                case Tile.Floor:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        float d = Sdf.RoundBox(u, v, 3, 3, Content - 3, Content - 3, 12, 12, 12, 12);
                        ca = Sdf.Cover(d);
                        float s = v < 14 ? 0.93f : 1f;
                        cr = cg = cb = s;
                    });
                    return;
                case Tile.Plan:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        // tokens plan: insetPx 8, cornerRadiusRatio 0,14, strokePx 4, strokeFactor 0,65
                        float rad = 0.14f * Content;
                        float d = Sdf.RoundBox(u, v, 8, 8, Content - 8, Content - 8, rad, rad, rad, rad);
                        ca = Sdf.Cover(d);
                        float s = -d < 4 ? 0.65f : 1f;
                        cr = cg = cb = s;
                    });
                    return;
                case Tile.Hatch:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        float rad = 0.14f * Content;
                        float d = Sdf.RoundBox(u, v, 8, 8, Content - 8, Content - 8, rad, rad, rad, rad);
                        float inside = Sdf.Cover(d);
                        float border = -d < 4 ? 1f : 0f;
                        float k = (u + v) % 16f;
                        float stripe = k < 4f ? 1f : 0f;
                        cr = cg = cb = 1f;
                        ca = inside * Math.Max(border, stripe);
                    });
                    return;
                case Tile.Hidden:
                    {
                        var q = Symbols.Question;
                        Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                        {
                            float rad = 0.14f * Content;
                            float d = Sdf.RoundBox(u, v, 8, 8, Content - 8, Content - 8, rad, rad, rad, rad);
                            float inside = Sdf.Cover(d);
                            float glyph = q.Coverage(u / Content * 100f, 100f - v / Content * 100f, Content / 100f);
                            // the pad is the plan underlay (tinted by the sprite colour); the "?" is cut out darker
                            float s = glyph > 0 ? ArtMath.Lerp(1f, 0.35f, glyph) : (-d < 4 ? 0.8f : 1f);
                            cr = cg = cb = s;
                            ca = inside;
                        });
                        return;
                    }
                case Tile.Ghost:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        float rad = 0.2f * Content;
                        float d = Sdf.RoundBox(u, v, 4, 4, Content - 4, Content - 4, rad, rad, rad, rad);
                        cr = cg = cb = 1f;
                        ca = Sdf.Cover(Math.Abs(d + 3.5f) - 3.5f);
                    });
                    return;
                case Tile.Panel:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        float d = Sdf.RoundBox(u, v, 2, 2, Content - 2, Content - 2, 30, 30, 30, 30);
                        ca = Sdf.Cover(d);
                        // shadow.panelLipPx 14 at the bottom (the lip reads as thickness)
                        float s = v < 16 ? 0.8f : 1f;
                        cr = cg = cb = s;
                    });
                    return;
                case Tile.Plain:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        cr = cg = cb = ca = 1f;
                    });
                    return;
                case Tile.Gradient:
                    {
                        var top = Rgba.Hex(Palette.GameTop);
                        var bottom = Rgba.Hex(Palette.GameBottom);
                        Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                        {
                            float k = v / Content;
                            cr = ArtMath.Lerp(bottom.Rf, top.Rf, k);
                            cg = ArtMath.Lerp(bottom.Gf, top.Gf, k);
                            cb = ArtMath.Lerp(bottom.Bf, top.Bf, k);
                            ca = 1f;
                        });
                        return;
                    }
                case Tile.Circle:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        cr = cg = cb = 1f;
                        ca = Sdf.Cover(ArtMath.Length(u - Content / 2f, v - Content / 2f) - (Content / 2f - 3));
                    });
                    return;
                case Tile.Check:
                    DrawBadge(c, r, Symbols.Check);
                    return;
                case Tile.Warn:
                    DrawBadge(c, r, Symbols.Warn);
                    return;
                case Tile.CancelBadge:
                    DrawBadge(c, r, Symbols.Cancel);
                    return;
                case Tile.Soft:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        // a puff: alpha falls off with the square of the distance from the centre (no hard edge)
                        float d = ArtMath.Length(u - Content / 2f, v - Content / 2f) / (Content / 2f);
                        float k = ArtMath.Clamp01(1f - d);
                        cr = cg = cb = 1f;
                        ca = k * k * (3f - 2f * k);
                    });
                    return;
                case Tile.Coin:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        float dist = ArtMath.Length(u - Content / 2f, v - Content / 2f);
                        float d = dist - (Content / 2f - 3);
                        ca = Sdf.Cover(d);
                        float rim = -d < 9 ? 0.72f : 1f;
                        float inner = Math.Abs(dist - 0.58f * Content / 2f) < 3f ? 0.8f : 1f;
                        float shade = ArtMath.Lerp(0.94f, 1f, ArtMath.Clamp01(v / Content));
                        cr = cg = cb = Math.Min(rim, inner) * shade;
                    });
                    return;
                case Tile.Ring:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        float d = ArtMath.Length(u - Content / 2f, v - Content / 2f) - (Content / 2f - 8);
                        cr = cg = cb = 1f;
                        ca = Sdf.Cover(Math.Abs(d + 4f) - 4f);
                    });
                    return;
                case Tile.Highlight:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        // box edge 20 px inside the tile, corner radius 20: the whole arc fits the 40 px 9-slice border
                        float d = Sdf.RoundBox(u, v, 20, 20, Content - 20, Content - 20, 20, 20, 20, 20);
                        float stroke = Sdf.Cover(Math.Abs(d) - 5f);
                        float glow = d > 5f ? 0.42f * (float)Math.Exp(-(d - 5f) / 5.5f) * ArtMath.Clamp01((20f - d) / 6f) : 0f;
                        cr = cg = cb = 1f;
                        ca = Math.Max(stroke, glow);
                    });
                    return;
                case Tile.Hammer:
                case Tile.CraneHook:
                case Tile.Brush:
                case Tile.Undo:
                case Tile.Pause:
                case Tile.Spark:
                case Tile.Star:
                case Tile.Trowel:
                    {
                        Glyph p;
                        switch (t)
                        {
                            case Tile.Pause: p = Symbols.PauseBars; break;
                            case Tile.Hammer: p = Symbols.Hammer; break;
                            case Tile.CraneHook: p = Symbols.CraneHook; break;
                            case Tile.Brush: p = Symbols.Brush; break;
                            case Tile.Spark: p = Symbols.Spark; break;
                            case Tile.Star: p = Symbols.Star; break;
                            case Tile.Trowel: p = Symbols.Trowel; break;
                            default: p = Symbols.Cancel; break;
                        }
                        Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                        {
                            cr = cg = cb = 1f;
                            ca = p.Coverage(u / Content * 100f, 100f - v / Content * 100f, Content / 100f);
                        });
                        return;
                    }
                case Tile.Gap:
                    Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
                    {
                        // a dark hole with two lighter rails along the left and right edges (W1, K-12)
                        bool rail = u < 14 || u > Content - 14;
                        cr = cg = cb = rail ? 1f : 0.45f;
                        ca = 1f;
                    });
                    return;
            }
            throw new ArgumentOutOfRangeException(nameof(t), t, "Atlas: no drawing for this tile");
        }

        /// <summary>White circle with a dark glyph (the badge colour comes from the sprite tint; the glyph stays dark).</summary>
        private static void DrawBadge(Canvas c, TileRect r, Glyph g)
        {
            Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
            {
                float d = ArtMath.Length(u - Content / 2f, v - Content / 2f) - (Content / 2f - 3);
                ca = Sdf.Cover(d);
                float glyph = g.Coverage(u / Content * 100f, 100f - v / Content * 100f, Content / 100f);
                float ring = -d < 6 ? 0.7f : 1f;
                float s = ArtMath.Lerp(ring, 0.15f, glyph);
                cr = cg = cb = s;
            });
        }

        /// <summary>
        /// One block cell (ART §3A, simplified): rounded on its open sides, flush where the piece continues; shading in grey
        /// (× the sprite tint): 6 px outline × 0,42 on the open outline, a 12 px bottom lip × 0,68 when the bottom is open,
        /// a vertical face gradient 0,92 → 1.
        /// </summary>
        private static void DrawBody(Canvas c, TileRect r, int mask)
        {
            bool left = (mask & 1) == 0, right = (mask & 2) == 0, down = (mask & 4) == 0, up = (mask & 8) == 0;
            const float inset = 3f; // blockV2.insetPx
            const float outline = 6f; // blockV2.outlinePx
            const float lip = 12f; // blockV2.lipPx
            float rad = 0.2f * Content; // blockV2.cornerRadiusRatio
            float x0 = left ? inset : -Content, x1 = right ? Content - inset : 2 * Content;
            float y0 = down ? inset : -Content, y1 = up ? Content - inset : 2 * Content;
            float rtl = left && up ? rad : 0, rtr = right && up ? rad : 0, rbl = left && down ? rad : 0, rbr = right && down ? rad : 0;
            Fill(c, r, (float u, float v, out float cr, out float cg, out float cb, out float ca) =>
            {
                float d = Sdf.RoundBox(u, v, x0, y0, x1, y1, rtl, rtr, rbl, rbr);
                ca = Sdf.Cover(d);
                float s;
                if (-d < outline) s = 0.42f;
                else if (down && v - y0 < lip + outline) s = 0.68f;
                else s = ArtMath.Lerp(0.92f, 1f, ArtMath.Clamp01(v / Content));
                cr = cg = cb = s;
            });
        }

        /// <summary>blockV2 glossEllipse [0,34; 0,13; 0,22; 0,06] (rotated −8°) at α 0,5 and glossDot [0,7; 0,12; 0,035] at α 0,8.</summary>
        private static void Gloss(float u, float v, out float r, out float g, out float b, out float a)
        {
            r = g = b = 1f;
            float fromTop = Content - v;
            float ex = u - 0.34f * Content, ey = fromTop - 0.13f * Content - 6;
            double rot = 8 * Math.PI / 180;
            float qx = (float)(ex * Math.Cos(rot) - ey * Math.Sin(rot));
            float qy = (float)(ex * Math.Sin(rot) + ey * Math.Cos(rot));
            float rx = 0.22f * Content, ry = 0.06f * Content;
            float e = (qx * qx) / (rx * rx) + (qy * qy) / (ry * ry);
            float ellipse = ArtMath.Clamp01((1f - e) * rx * 0.5f);
            float dot = Sdf.Cover(ArtMath.Length(u - 0.7f * Content, fromTop - 0.12f * Content - 6) - 0.035f * Content);
            a = Math.Max(ellipse * 0.5f, dot * 0.8f);
        }
    }

    /// <summary>Signed distance helpers (px; negative inside).</summary>
    internal static class Sdf
    {
        /// <summary>Antialiased coverage of a signed distance (1 px ramp).</summary>
        public static float Cover(float d) => ArtMath.Clamp01(0.5f - d);

        /// <summary>Box [x0, x1] × [y0, y1] (y up) with a radius per corner (top-left, top-right, bottom-left, bottom-right).</summary>
        public static float RoundBox(float px, float py, float x0, float y0, float x1, float y1, float rtl, float rtr, float rbl, float rbr)
        {
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            float hx = (x1 - x0) / 2, hy = (y1 - y0) / 2;
            float rad = px >= cx ? (py >= cy ? rtr : rbr) : (py >= cy ? rtl : rbl);
            float qx = Math.Abs(px - cx) - hx + rad;
            float qy = Math.Abs(py - cy) - hy + rad;
            float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            return ArtMath.Length(ox, oy) + Math.Min(Math.Max(qx, qy), 0) - rad;
        }

        /// <summary>Distance from p to segment ab.</summary>
        public static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float len = bax * bax + bay * bay;
            float h = len > 0 ? ArtMath.Clamp01((pax * bax + pay * bay) / len) : 0;
            return ArtMath.Length(pax - bax * h, pay - bay * h);
        }
    }
}
