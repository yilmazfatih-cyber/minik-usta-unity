#nullable enable
// Text fitting without the engine (paket V1): uGUI's "best fit" with a 1,6 em line box (Baloo 2) would shrink or truncate
// labels unpredictably, so the Unity layer draws every label on ONE line at a fixed size chosen here from the font's own
// advance widths (`FontMetrics`, generated from the shipped TTF). The compose tool and the tests use the same size.
using System;

namespace MinikUsta.Presentation.Ui
{
    public static partial class FontMetrics
    {
        /// <summary>Advance of a code point the font does not map (≈ an average lower-case letter).</summary>
        public const int MissingAdvance = 550;

        /// <summary>Width of `text` at `size` design px (advance widths, no kerning).</summary>
        public static double Width(string text, double size)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            long units = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int cp = char.IsSurrogatePair(text, i) ? char.ConvertToUtf32(text, i++) : text[i];
                units += Advances.TryGetValue(cp, out int a) ? a : MissingAdvance;
            }
            return units * size / UnitsPerEm;
        }

        public static bool Has(int codePoint) => Advances.ContainsKey(codePoint);

        public static int Advance(int codePoint) => Advances.TryGetValue(codePoint, out int a) ? a : -1;

        /// <summary>The line box of uGUI (ascender − descender) at `size`.</summary>
        public static double LineHeight(double size) => (Ascender - Descender) * size / UnitsPerEm;

        /// <summary>
        /// The font size (integer px) that keeps `text` on one line within `maxWidth`: `size` when it fits, else scaled down
        /// (never below half of `size`, the old best-fit floor).
        /// </summary>
        public static int FittedSize(string text, double size, double maxWidth)
        {
            int full = (int)Math.Round(size);
            double w = Width(text, size);
            if (w <= maxWidth || w <= 0) return full;
            int fit = (int)Math.Floor(size * maxWidth / w);
            return Math.Max((int)Math.Ceiling(size / 2), Math.Min(full, fit));
        }
    }

    public static class TextFit
    {
        /// <summary>Horizontal room kept free inside a label rectangle (both sides together), design px.</summary>
        public const double Margin = 8;

        /// <summary>The size a node's text is drawn at: one line within its rectangle, outline included.</summary>
        public static int SizeOf(UiNode n)
        {
            if (n.Text == null) return (int)Math.Round(n.FontPx);
            var look = TextStyles.Of(n.Style, n.FontPx, n.TextFill, n.TextStroke);
            double room = n.Rect.W - Margin - 2 * look.OutlinePx;
            return FontMetrics.FittedSize(n.Text, n.FontPx, Math.Max(1, room));
        }
    }
}
