#nullable enable
// The engine edge of the presentation types (Faz 3 Q1): design px (origin top-left, y down; Presentation.Core) ↔ Unity
// world units (camera at the canvas centre, y up, 100 design px = 1 unit; ScreenFit.DefaultPixelsPerUnit) and Rgba ↔ Color.
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Layout;
using UnityEngine;

namespace MinikUsta.Unity
{
    /// <summary>The current design canvas (shared by every view; replaced on a screen change).</summary>
    public sealed class DesignSpace
    {
        public const float PixelsPerUnit = (float)ScreenFit.DefaultPixelsPerUnit;

        public double W = 1080;
        public double H = 1920;

        public void Set(DesignViewport vp)
        {
            W = vp.W;
            H = vp.H;
        }

        /// <summary>Design px point → world position (z 0).</summary>
        public Vector3 World(double dx, double dy)
        {
            DesignViewport.DesignToWorld(dx, dy, W, H, PixelsPerUnit, out double wx, out double wy);
            return new Vector3((float)wx, (float)wy, 0f);
        }

        public Vector3 World(Vec2 p) => World(p.X, p.Y);

        /// <summary>Centre of a design px rectangle in world units.</summary>
        public Vector3 Center(PxRect r) => World(r.CenterX, r.CenterY);

        /// <summary>Design px length → world units.</summary>
        public static float Units(double px) => (float)(px / PixelsPerUnit);
    }

    public static class UnityConvert
    {
        public static Color ToColor(Rgba c) => new Color(c.Rf, c.Gf, c.Bf, c.Af);

        public static Color ToColor(Rgba c, float alphaScale) => new Color(c.Rf, c.Gf, c.Bf, c.Af * alphaScale);

        public static Color Lerp(Color a, Color b, float t)
        {
            float k = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * k, a.g + (b.g - a.g) * k, a.b + (b.b - a.b) * k, a.a + (b.a - a.a) * k);
        }
    }
}
