#nullable enable
// Design canvas ↔ screen ↔ world (port of web theme/layout.ts `designHeight` / `fitViewport`, TECH §10.1, R-06). The
// design canvas is 1080 wide and H high (FIT: 1920; EXPAND: 1920 … 2400 with the screen aspect). The Unity camera is
// orthographic and centred on the canvas; one world unit = `PixelsPerUnit` design px. Screen coordinates are Unity's
// (`Input.mousePosition`: px, origin bottom-left); design coordinates have their origin top-left, y down.
using System;

namespace MinikUsta.Presentation.Layout
{
    public readonly struct DesignViewport
    {
        /// <summary>Screen size (px).</summary>
        public readonly double ScreenW;
        public readonly double ScreenH;
        /// <summary>Design canvas size (design px): W 1080, H per `ScaleMode`.</summary>
        public readonly double W;
        public readonly double H;
        /// <summary>Screen px per design px (the canvas fits inside the screen).</summary>
        public readonly double Scale;
        /// <summary>Letterbox (top + bottom) and pillarbox (left + right) bands, screen px.</summary>
        public readonly double LetterboxPx;
        public readonly double PillarboxPx;

        public DesignViewport(double screenW, double screenH, double w, double h)
        {
            if (!(screenW > 0) || !(screenH > 0)) throw new ArgumentOutOfRangeException(nameof(screenW), "DesignViewport: the screen size must be > 0");
            ScreenW = screenW;
            ScreenH = screenH;
            W = w;
            H = h;
            Scale = Math.Min(screenW / w, screenH / h);
            LetterboxPx = screenH - h * Scale;
            PillarboxPx = screenW - w * Scale;
        }

        /// <summary>Unity screen point (origin bottom-left) → design px (origin top-left, y down). Outside the canvas too.</summary>
        public void ScreenToDesign(double sx, double sy, out double dx, out double dy)
        {
            dx = (sx - PillarboxPx / 2) / Scale;
            dy = H - (sy - LetterboxPx / 2) / Scale;
        }

        /// <summary>Design px → Unity screen point (origin bottom-left).</summary>
        public void DesignToScreen(double dx, double dy, out double sx, out double sy)
        {
            sx = dx * Scale + PillarboxPx / 2;
            sy = (H - dy) * Scale + LetterboxPx / 2;
        }

        /// <summary>Design px → world (camera at the canvas centre, y up): ((dx − W/2) / ppu, (H/2 − dy) / ppu).</summary>
        public static void DesignToWorld(double dx, double dy, double w, double h, double pixelsPerUnit, out double wx, out double wy)
        {
            wx = (dx - w / 2) / pixelsPerUnit;
            wy = (h / 2 - dy) / pixelsPerUnit;
        }

        public void DesignToWorld(double dx, double dy, double pixelsPerUnit, out double wx, out double wy) =>
            DesignToWorld(dx, dy, W, H, pixelsPerUnit, out wx, out wy);

        public void WorldToDesign(double wx, double wy, double pixelsPerUnit, out double dx, out double dy)
        {
            dx = wx * pixelsPerUnit + W / 2;
            dy = H / 2 - wy * pixelsPerUnit;
        }

        /// <summary>
        /// Orthographic size (half the visible world height) that shows the whole canvas: the full screen height in design
        /// px (bands included, they show the camera background) / 2 / ppu.
        /// </summary>
        public double OrthographicSize(double pixelsPerUnit) => ScreenH / Scale / 2 / pixelsPerUnit;
    }

    public static class ScreenFit
    {
        /// <summary>One world unit = 100 design px (Unity's default sprite pixels-per-unit).</summary>
        public const double DefaultPixelsPerUnit = 100;

        /// <summary>
        /// Design height for a screen (R-06): FIT always `DesignHeight` (1920); EXPAND = 1080 · screenH / screenW rounded,
        /// clamped to [1920, 2400] (taller screens letterbox the rest, wider ones pillarbox).
        /// </summary>
        public static double DesignHeight(ScaleMode mode, double screenW, double screenH, LayoutSettings s)
        {
            if (mode == ScaleMode.Fit) return s.DesignHeight;
            double natural = Math.Round(s.DesignWidth * screenH / screenW, MidpointRounding.AwayFromZero);
            return Math.Min(s.ExpandMaxHeight, Math.Max(s.DesignHeight, natural));
        }

        public static DesignViewport Viewport(ScaleMode mode, double screenW, double screenH, LayoutSettings s) =>
            new DesignViewport(screenW, screenH, s.DesignWidth, DesignHeight(mode, screenW, screenH, s));

        public static DesignViewport Viewport(double screenW, double screenH, LayoutSettings s) => Viewport(s.Mode, screenW, screenH, s);

        /// <summary>
        /// C1: `?scale=fit` or `?scale=expand` in the page URL (WebGL) picks the scale mode for that visit, so the two can be compared on a phone
        /// and in CI screenshots without a build; anything else (or no parameter) is null = the default of `LayoutSettings.Mode`.
        /// </summary>
        public static ScaleMode? ModeFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            int q = url!.IndexOf('?');
            if (q < 0) return null;
            int hash = url.IndexOf('#', q);
            string query = hash < 0 ? url.Substring(q + 1) : url.Substring(q + 1, hash - q - 1);
            foreach (var pair in query.Split('&'))
            {
                if (pair == "scale=expand") return ScaleMode.Expand;
                if (pair == "scale=fit") return ScaleMode.Fit;
            }
            return null;
        }
    }
}
