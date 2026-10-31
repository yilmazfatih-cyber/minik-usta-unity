#nullable enable
// Colour symbols and badge glyphs of the placeholder art (ART §3.1, simplified; Faz 3 Q1). A glyph is a set of strokes
// (polylines with a width) and filled polygons / circles in the 100 × 100 SVG unit box of ART §3.1 (y DOWN, as in SVG);
// the curves of the SVG paths are flattened into short segments. "TABAN" carvings (G vein, P facets) are left out: the
// symbol is a single white mask tinted with the ink colour (ART §2.2).
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Art
{
    public sealed class Glyph
    {
        private readonly List<float[]> strokes = new List<float[]>(); // x0 y0 x1 y1 … + half width at the end
        private readonly List<float[]> polygons = new List<float[]>(); // closed, x y pairs
        private readonly List<float[]> circles = new List<float[]>(); // cx cy r
        // bounding boxes (min x, min y, max x, max y) of the strokes (grown by the half width) and polygons: a primitive
        // farther than one pixel cannot cover the point (atlas build time, TECH §10.6)
        private readonly List<float[]> strokeBoxes = new List<float[]>();
        private readonly List<float[]> polygonBoxes = new List<float[]>();

        public Glyph Stroke(float width, params float[] xy)
        {
            if (xy.Length < 4 || xy.Length % 2 != 0) throw new ArgumentException("Glyph.Stroke: x y pairs, at least two points");
            var a = new float[xy.Length + 1];
            Array.Copy(xy, a, xy.Length);
            a[xy.Length] = width / 2f;
            strokes.Add(a);
            strokeBoxes.Add(Box(xy, width / 2f));
            return this;
        }

        public Glyph Polygon(params float[] xy)
        {
            if (xy.Length < 6 || xy.Length % 2 != 0) throw new ArgumentException("Glyph.Polygon: x y pairs, at least three points");
            polygons.Add(xy);
            polygonBoxes.Add(Box(xy, 0f));
            return this;
        }

        public Glyph Disc(float cx, float cy, float r)
        {
            circles.Add(new[] { cx, cy, r });
            return this;
        }

        /// <summary>Coverage 0 … 1 at SVG unit point (x, y); `unitPx` = px per unit (the antialiasing ramp is one px).</summary>
        public float Coverage(float x, float y, float unitPx)
        {
            float px = 1f / unitPx; // one pixel in units
            float best = float.PositiveInfinity;
            for (int k = 0; k < strokes.Count; k++)
            {
                if (Outside(strokeBoxes[k], x, y, px)) continue;
                var s = strokes[k];
                float hw = s[s.Length - 1];
                for (int i = 0; i + 3 < s.Length - 1; i += 2)
                    best = Math.Min(best, Sdf.Segment(x, y, s[i], s[i + 1], s[i + 2], s[i + 3]) - hw);
            }
            foreach (var c in circles) best = Math.Min(best, ArtMath.Length(x - c[0], y - c[1]) - c[2]);
            for (int k = 0; k < polygons.Count; k++)
            {
                if (Outside(polygonBoxes[k], x, y, px)) continue;
                best = Math.Min(best, PolygonDistance(polygons[k], x, y));
            }
            if (float.IsPositiveInfinity(best)) return 0f;
            return Sdf.Cover(best * unitPx);
        }

        private static float[] Box(float[] xy, float grow)
        {
            float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                x0 = Math.Min(x0, xy[i]);
                y0 = Math.Min(y0, xy[i + 1]);
                x1 = Math.Max(x1, xy[i]);
                y1 = Math.Max(y1, xy[i + 1]);
            }
            return new[] { x0 - grow, y0 - grow, x1 + grow, y1 + grow };
        }

        private static bool Outside(float[] b, float x, float y, float margin) =>
            x < b[0] - margin || y < b[1] - margin || x > b[2] + margin || y > b[3] + margin;

        /// <summary>Signed distance to a closed polygon (even-odd inside test).</summary>
        private static float PolygonDistance(float[] p, float x, float y)
        {
            int n = p.Length / 2;
            float d = float.PositiveInfinity;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[2 * i], yi = p[2 * i + 1], xj = p[2 * j], yj = p[2 * j + 1];
                d = Math.Min(d, Sdf.Segment(x, y, xi, yi, xj, yj));
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside ? -d : d;
        }

        // --- curve flattening ------------------------------------------------------------------------------------------

        /// <summary>Cubic Bézier p0 → p3 as `n` + 1 points (x y pairs).</summary>
        public static float[] Cubic(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, int n = 12)
        {
            var o = new float[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, m = 1 - t;
                o[2 * i] = m * m * m * x0 + 3 * m * m * t * x1 + 3 * m * t * t * x2 + t * t * t * x3;
                o[2 * i + 1] = m * m * m * y0 + 3 * m * m * t * y1 + 3 * m * t * t * y2 + t * t * t * y3;
            }
            return o;
        }

        /// <summary>Quadratic Bézier p0 → p2 as `n` + 1 points.</summary>
        public static float[] Quad(float x0, float y0, float x1, float y1, float x2, float y2, int n = 8)
        {
            var o = new float[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, m = 1 - t;
                o[2 * i] = m * m * x0 + 2 * m * t * x1 + t * t * x2;
                o[2 * i + 1] = m * m * y0 + 2 * m * t * y1 + t * t * y2;
            }
            return o;
        }

        /// <summary>Ellipse outline as `n` + 1 points (closed).</summary>
        public static float[] Ellipse(float cx, float cy, float rx, float ry, int n = 20)
        {
            var o = new float[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                double a = 2 * Math.PI * i / n;
                o[2 * i] = cx + rx * (float)Math.Cos(a);
                o[2 * i + 1] = cy + ry * (float)Math.Sin(a);
            }
            return o;
        }

        /// <summary>Joins point runs (the first point of a run that repeats the previous last point is dropped).</summary>
        public static float[] Join(params float[][] runs)
        {
            var o = new List<float>();
            foreach (var r in runs)
            {
                int start = 0;
                if (o.Count >= 2 && r.Length >= 2 && Math.Abs(o[o.Count - 2] - r[0]) < 1e-4f && Math.Abs(o[o.Count - 1] - r[1]) < 1e-4f) start = 2;
                for (int i = start; i < r.Length; i++) o.Add(r[i]);
            }
            return o.ToArray();
        }
    }

    public static class Symbols
    {
        private static readonly Glyph[] colorSymbols = Build();

        /// <summary>The symbol of colour index 0 … 7 (ART §2.1 / §3.1).</summary>
        public static Glyph Of(int colorIndex) => colorSymbols[colorIndex >= 0 && colorIndex < colorSymbols.Length ? colorIndex : 0];

        /// <summary>K-32 hidden cell "?".</summary>
        public static readonly Glyph Question = new Glyph()
            .Stroke(13, Glyph.Join(Glyph.Cubic(32, 36, 32, 14, 68, 14, 68, 36), Glyph.Cubic(68, 36, 68, 50, 50, 50, 50, 62)))
            .Disc(50, 80, 8);

        /// <summary>Shadow badge ✓ (UX §5.4 `ok`).</summary>
        public static readonly Glyph Check = new Glyph().Stroke(14, 26, 52, 43, 69, 75, 34);

        /// <summary>Shadow badge ! (UX §5.4 `warn`).</summary>
        public static readonly Glyph Warn = new Glyph().Stroke(14, 50, 22, 50, 58).Disc(50, 77, 8);

        /// <summary>Cancel badge ↩ (UX §5.3).</summary>
        public static readonly Glyph Cancel = new Glyph()
            .Stroke(11, Glyph.Join(new float[] { 70, 72 }, Glyph.Cubic(70, 72, 82, 72, 82, 40, 62, 40), new float[] { 30, 40 }))
            .Stroke(11, 42, 26, 28, 40, 42, 54);

        /// <summary>Booster icons (placeholder silhouettes): hammer, crane hook, paint brush.</summary>
        public static readonly Glyph Hammer = new Glyph().Stroke(13, 28, 84, 60, 46).Stroke(24, 44, 26, 76, 58);

        public static readonly Glyph CraneHook = new Glyph()
            .Stroke(10, 22, 14, 80, 14)
            .Stroke(9, 50, 14, 50, 50)
            .Stroke(10, Glyph.Cubic(50, 50, 50, 84, 82, 84, 78, 60));

        public static readonly Glyph Brush = new Glyph().Stroke(13, 24, 84, 54, 54).Polygon(50, 50, 74, 18, 88, 32, 60, 60);

        /// <summary>Q2 fx_spark: a four-point sparkle with concave sides.</summary>
        public static readonly Glyph Spark = new Glyph().Polygon(50, 2, 58, 42, 98, 50, 58, 58, 50, 98, 42, 58, 2, 50, 42, 42);

        /// <summary>Q2 five-point star (win star, star burst): outer radius 47, inner 20, centred a little low so it reads upright.</summary>
        public static readonly Glyph Star = new Glyph().Polygon(StarPoints(50f, 54f, 47f, 20f));

        /// <summary>Q2 Golden Trowel icon: a kite-shaped blade, the tip at the top left, and a handle toward the bottom right.</summary>
        public static readonly Glyph Trowel = new Glyph().Polygon(8, 8, 58, 24, 76, 42, 42, 76, 24, 58).Stroke(15, 68, 68, 92, 92);

        /// <summary>Pause tool (two rounded bars).</summary>
        public static readonly Glyph PauseBars = new Glyph().Stroke(16, 36, 26, 36, 74).Stroke(16, 64, 26, 64, 74);

        private static float[] StarPoints(float cx, float cy, float outer, float inner)
        {
            var o = new float[20];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? outer : inner;
                o[2 * i] = cx + r * (float)Math.Cos(a);
                o[2 * i + 1] = cy + r * (float)Math.Sin(a);
            }
            return o;
        }

        private static Glyph[] Build()
        {
            var o = new Glyph[8];
            // W wood grain: three grain lines and a knot on the right (ART §3.1)
            o[0] = new Glyph()
                .Stroke(9, Glyph.Cubic(10, 24, 30, 14, 50, 34, 90, 22))
                .Stroke(9, Glyph.Cubic(10, 50, 22, 42, 34, 58, 48, 50))
                .Stroke(7, Glyph.Ellipse(70, 51, 11, 7))
                .Stroke(9, Glyph.Cubic(10, 76, 30, 66, 50, 86, 90, 74));
            // Y three dots
            o[1] = new Glyph().Disc(50, 28, 13).Disc(27, 70, 13).Disc(73, 70, 13);
            // G leaf (filled)
            o[2] = new Glyph().Polygon(Glyph.Join(Glyph.Cubic(18, 82, 18, 40, 46, 14, 86, 14), Glyph.Cubic(86, 14, 86, 54, 60, 82, 18, 82)));
            // R brick joints
            o[3] = new Glyph()
                .Stroke(8, 20, 16, 80, 16, 90, 26, 90, 74, 80, 84, 20, 84, 10, 74, 10, 26, 20, 16)
                .Stroke(8, 10, 50, 90, 50)
                .Stroke(8, 50, 16, 50, 50)
                .Stroke(8, 30, 50, 30, 84)
                .Stroke(8, 70, 50, 70, 84);
            // O roof tile waves
            o[4] = new Glyph()
                .Stroke(9, Glyph.Join(Glyph.Quad(8, 44, 22, 18, 36, 44), Glyph.Quad(36, 44, 50, 18, 64, 44), Glyph.Quad(64, 44, 78, 18, 92, 44)))
                .Stroke(9, Glyph.Join(Glyph.Quad(8, 80, 22, 54, 36, 80), Glyph.Quad(36, 80, 50, 54, 64, 80), Glyph.Quad(64, 80, 78, 54, 92, 80)));
            // C cross hatch
            o[5] = new Glyph()
                .Stroke(8, 18, 50, 50, 18).Stroke(8, 18, 82, 82, 18).Stroke(8, 50, 82, 82, 50)
                .Stroke(8, 18, 50, 50, 82).Stroke(8, 18, 18, 82, 82).Stroke(8, 50, 18, 82, 50);
            // B sparkle (four-point star + a small one)
            o[6] = new Glyph()
                .Polygon(Glyph.Join(
                    Glyph.Cubic(46, 8, 50, 36, 58, 44, 86, 48), Glyph.Cubic(86, 48, 58, 52, 50, 60, 46, 88),
                    Glyph.Cubic(46, 88, 42, 60, 34, 52, 6, 48), Glyph.Cubic(6, 48, 34, 44, 42, 36, 46, 8)))
                .Polygon(Glyph.Join(
                    Glyph.Cubic(82, 8, 83, 16, 85, 18, 93, 19, 6), Glyph.Cubic(93, 19, 85, 20, 83, 22, 82, 30, 6),
                    Glyph.Cubic(82, 30, 81, 22, 79, 20, 71, 19, 6), Glyph.Cubic(71, 19, 79, 18, 81, 16, 82, 8, 6)));
            // P diamond (filled)
            o[7] = new Glyph().Polygon(50, 8, 90, 42, 50, 92, 10, 42);
            return o;
        }
    }
}
