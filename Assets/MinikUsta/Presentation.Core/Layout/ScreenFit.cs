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

namespace MinikUsta.Presentation.Layout
{
    /// <summary>
    /// U2 (UX_UNITY P0-2 "Screen.safeArea içe çekme"): the part of the screen the game draws its design canvas into — Unity's
    /// `Screen.safeArea` (screen px, origin bottom-left; the notch / status bar and the gesture bar left out). The viewport is built
    /// from the safe size; pointers are shifted by the safe origin; the camera and the UI canvas move by the safe centre's offset, and
    /// the camera still covers the whole screen (the bands outside the safe area show the camera background). An empty or outside
    /// rectangle (desktop, the WebGL page) is the whole screen: nothing moves.
    /// </summary>
    public readonly struct SafeFrame
    {
        public readonly double ScreenW;
        public readonly double ScreenH;
        /// <summary>The safe rectangle (screen px, origin bottom-left).</summary>
        public readonly double X;
        public readonly double Y;
        public readonly double W;
        public readonly double H;

        private SafeFrame(double screenW, double screenH, double x, double y, double w, double h)
        {
            ScreenW = screenW;
            ScreenH = screenH;
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public static SafeFrame Of(double screenW, double screenH, double x, double y, double w, double h)
        {
            double x0 = Math.Max(0, x), y0 = Math.Max(0, y);
            double x1 = Math.Min(screenW, x + w), y1 = Math.Min(screenH, y + h);
            if (!(x1 - x0 >= 1) || !(y1 - y0 >= 1)) return new SafeFrame(screenW, screenH, 0, 0, screenW, screenH);
            return new SafeFrame(screenW, screenH, x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>Safe centre − screen centre (screen px; x right, y up).</summary>
        public double CenterOffsetX => X + W / 2 - ScreenW / 2;
        public double CenterOffsetY => Y + H / 2 - ScreenH / 2;

        public bool Inset => X > 0 || Y > 0 || W < ScreenW || H < ScreenH;

        /// <summary>A screen point (origin bottom-left) → design px of `vp` (a viewport built from `W` × `H`).</summary>
        public void ScreenToDesign(DesignViewport vp, double sx, double sy, out double dx, out double dy) => vp.ScreenToDesign(sx - X, sy - Y, out dx, out dy);

        /// <summary>World position of the camera (relative to its home) that puts the canvas centre on the safe centre.</summary>
        public void CameraOffset(DesignViewport vp, double pixelsPerUnit, out double wx, out double wy)
        {
            wx = -CenterOffsetX / vp.Scale / pixelsPerUnit;
            wy = -CenterOffsetY / vp.Scale / pixelsPerUnit;
        }

        /// <summary>Orthographic size that fills the WHOLE screen height at the viewport's scale.</summary>
        public double OrthographicSize(DesignViewport vp, double pixelsPerUnit) => ScreenH / vp.Scale / 2 / pixelsPerUnit;
    }
}
