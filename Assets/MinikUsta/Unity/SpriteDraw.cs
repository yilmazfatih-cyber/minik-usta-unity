#nullable enable
// Draws an atlas frame INTO a design px rectangle with a SpriteRenderer (paket V1; ART_UNITY §3.1): the sprite's pivot goes to
// the matching point of the rectangle, the scale is rectangle / frame design size (a 9-sliced frame uses drawMode Sliced and
// `size` instead, so its borders keep their design px). The views compute the rectangle; this only turns it into a transform.
using MinikUsta.Presentation;
using MinikUsta.Presentation.Art;
using UnityEngine;

namespace MinikUsta.Unity
{
    public static class SpriteDraw
    {
        /// <summary>The design px point the sprite pivot goes to (rect origin top-left, y down).</summary>
        public static void PivotPoint(FrameSprite fs, PxRect r, out double px, out double py)
        {
            px = r.X + fs.PivotX * r.W;
            py = r.Y + (1 - fs.PivotY) * r.H;
        }

        /// <summary>
        /// Sets sprite, draw mode, size / scale and colour of `sr` for `fs` drawn into a `w` × `h` design px rectangle; the caller
        /// places the transform at `PivotPoint`. False (and the renderer disabled) when there is nothing to draw.
        /// </summary>
        public static bool Apply(SpriteRenderer sr, FrameSprite fs, double w, double h, bool sliced, Color tint)
        {
            if (!fs.Ok)
            {
                sr.enabled = false;
                return false;
            }
            sr.enabled = true;
            sr.sprite = fs.Sprite;
            var t = sr.transform;
            if (sliced && fs.Sliced)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(DesignSpace.Units(w), DesignSpace.Units(h));
                t.localScale = Vector3.one;
            }
            else
            {
                sr.drawMode = SpriteDrawMode.Simple;
                t.localScale = new Vector3((float)(w / fs.DesignW), (float)(h / fs.DesignH), 1f);
            }
            sr.color = Mul(tint, fs.Tint);
            return true;
        }

        public static Color Mul(Color a, Rgba b) => new Color(a.r * b.Rf, a.g * b.Gf, a.b * b.Bf, a.a * b.Af);
    }
}
