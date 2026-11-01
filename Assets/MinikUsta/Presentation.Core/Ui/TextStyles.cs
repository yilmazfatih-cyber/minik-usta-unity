#nullable enable
// Text styles of ART_REFERENCE §4 (and tools/art/preview.mjs `txt`), reduced to what uGUI Text draws without TMP (D-096):
// a fill colour, an outline (uGUI `Outline`, distance in design px) and a drop shadow (uGUI `Shadow`, offset down). Sizes
// are design px; every distance scales with the font size like the web kit (em fractions).
using System;
using MinikUsta.Presentation.Art;

namespace MinikUsta.Presentation.Ui
{
    public enum TextStyle
    {
        /// <summary>White, near-black outline + shadow (generic).</summary>
        Plain,
        /// <summary>Cream counter text with a dark outline (HUD numbers).</summary>
        Cream,
        /// <summary>Lavender capsule text with a dark drop (`Bölüm N`, labels in capsules).</summary>
        Lavender,
        /// <summary>The moves number (bigNumber): cream on a navy outline + drop.</summary>
        Big,
        /// <summary>Button / ribbon text: white with a coloured outline (`TextStroke`, default green) + drop.</summary>
        Button,
        /// <summary>Window title (titleTwoRing): white, indigo outline + drop.</summary>
        Title,
        /// <summary>Reward gold text.</summary>
        Gold,
        /// <summary>Flat ink text on light cards / bubbles.</summary>
        Card,
        /// <summary>White label with an indigo outline (window body lines).</summary>
        Label,
    }

    public readonly struct TextLook
    {
        public readonly Rgba Fill;
        public readonly Rgba Outline;
        /// <summary>Outline distance, design px (0 = none).</summary>
        public readonly double OutlinePx;
        public readonly Rgba Shadow;
        /// <summary>Shadow offset downwards, design px (0 = none).</summary>
        public readonly double ShadowPx;

        public TextLook(Rgba fill, Rgba outline, double outlinePx, Rgba shadow, double shadowPx)
        {
            Fill = fill;
            Outline = outline;
            OutlinePx = outlinePx;
            Shadow = shadow;
            ShadowPx = shadowPx;
        }
    }

    public static class TextStyles
    {
        public static TextLook Of(TextStyle style, double size, Rgba? fill = null, Rgba? stroke = null)
        {
            double em = size;
            switch (style)
            {
                case TextStyle.Cream:
                    return new TextLook(fill ?? Rgba.Hex("#FFF6E3"), stroke ?? Rgba.Hex("#0B0918"), 0.08 * em, Rgba.Clear, 0);
                case TextStyle.Lavender:
                    return new TextLook(fill ?? Rgba.Hex("#A09AFC"), Rgba.Clear, 0, stroke ?? Rgba.Hex("#0E0C2C"), 3);
                case TextStyle.Big:
                    return new TextLook(fill ?? Rgba.Hex("#FBF1BD"), stroke ?? Rgba.Hex("#00325A"), 0.08 * em, stroke ?? Rgba.Hex("#00325A"), 0.05 * em);
                case TextStyle.Button:
                    {
                        var c = stroke ?? Rgba.Hex("#0E5A14");
                        return new TextLook(fill ?? Rgba.White, c, Math.Max(4, 0.06 * em), c, 0.045 * em);
                    }
                case TextStyle.Title:
                    return new TextLook(fill ?? Rgba.White, stroke ?? Rgba.Hex("#3325A2"), 0.1 * em, Rgba.Hex("#211865"), 0.06 * em);
                case TextStyle.Gold:
                    return new TextLook(fill ?? Rgba.Hex("#FFD119"), stroke ?? Rgba.Hex("#B8430A"), 0.06 * em + 2, Rgba.Hex("#5E2206", 0.6f), 0.06 * em);
                case TextStyle.Card:
                    return new TextLook(fill ?? Rgba.Hex("#1A467B"), Rgba.Clear, 0, Rgba.Clear, 0);
                case TextStyle.Label:
                    return new TextLook(fill ?? Rgba.White, stroke ?? Rgba.Hex("#352496"), 0.06 * em, Rgba.Clear, 0);
                default:
                    return new TextLook(fill ?? Rgba.White, stroke ?? Rgba.Hex("#111111"), 0.07 * em, stroke ?? Rgba.Hex("#111111"), 0.05 * em);
            }
        }

        /// <summary>The style id the compose tool uses (tools/art/compose.mjs `txt`).</summary>
        public static string Id(TextStyle s)
        {
            switch (s)
            {
                case TextStyle.Cream: return "cream";
                case TextStyle.Lavender: return "lav";
                case TextStyle.Big: return "big";
                case TextStyle.Button: return "button";
                case TextStyle.Title: return "title";
                case TextStyle.Gold: return "gold";
                case TextStyle.Card: return "card";
                case TextStyle.Label: return "label";
                default: return "plain";
            }
        }
    }
}
