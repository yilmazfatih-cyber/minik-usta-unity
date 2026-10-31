#nullable enable
// Colours of the placeholder art (Faz 3 Q1), straight from tokens.json (design-lead): `color.block`, `color.planInk`,
// `color.ghost`, `color.board`, `color.ui`, `color.scene` and the `alpha.*` values used here. `PaletteTests` checks every
// value against the tokens.json copy (Presentation.Core.Tests/Golden/tokens.json, tools/sync-from-web.sh).
using System;
using MinikUsta.Core;

namespace MinikUsta.Presentation.Art
{
    public static class Palette
    {
        /// <summary>color.block in `ColorCodes.All` order (W Y G R O C B P; BRIEF §6, ART §2.1).</summary>
        public static readonly string[] BlockHex = { "#764012", "#FFE23E", "#44D088", "#CF404E", "#EC8D20", "#C8CFDA", "#2984DE", "#5D3AAE" };

        /// <summary>
        /// ART §2.2 symbol ink: dark (base × 0,40, `block.symbolDarkFactor`) on light bases (L* ≥ 60: Y G O C), white
        /// (`alpha.symbolWhite` 0,85) on dark bases (W R B P).
        /// </summary>
        public static readonly bool[] DarkInk = { false, true, true, false, true, true, false, false };

        public const float SymbolDarkFactor = 0.4f;
        public const float SymbolWhiteAlpha = 0.85f;

        /// <summary>color.ghost.* (UX §5.4 shadow outlines, D-014).</summary>
        public const string GhostValid = "#5CF59A";
        public const string GhostInvalid = "#FF4A3D";
        public const string GhostNeutral = "#FFFFFF";
        public const string GhostSupport = "#FFC21A";

        /// <summary>color.board.*</summary>
        public const string YardFloor = "#E9C891";
        public const string YardFloorAlt = "#E2BD80";
        public const string YardFrame = "#B98A4E";
        public const string Blueprint = "#1F4F8F";
        public const string BlueprintDeep = "#173D70";
        public const string PlanUnderlay = "#BCCADD";
        public const string Wall = "#A9AFB8";
        public const string WallDark = "#7D848E";
        public const string Rail = "#3F454D";
        public const string ShutterSlat = "#7D8793";
        public const string CraneSky = "#FFFFFF";
        public const string BuildFront = "#FFFFFF";

        /// <summary>color.ui.*</summary>
        public const string Panel = "#FFF4D6";
        public const string PanelEdge = "#F2D9A0";
        public const string Ink = "#3B2A1A";
        public const string InkOnDark = "#FFFFFF";
        public const string Primary = "#45B649";
        public const string Secondary = "#FF9A1F";
        public const string Neutral = "#FFF9EA";
        public const string Danger = "#E8473B";
        public const string Gold = "#FFC21A";
        public const string Overlay = "#141828";

        /// <summary>kit.v3.screen.hud.movesBand / movesCream (HUD v3 moves plate).</summary>
        public const string MovesBand = "#26355A";
        public const string MovesCream = "#F9F3C3";

        /// <summary>color.scene.* (ART §7.1 indigo game scene, R2-12): vertical gradient gameTop → gameBottom.</summary>
        public const string GameTop = "#3B2C85";
        public const string GameBottom = "#1F1850";

        /// <summary>alpha.* used by the placeholder art.</summary>
        public const float PlanFill = 0.8f;
        public const float PlanEmptyHatch = 0.28f;
        public const float CraneSkyAlpha = 0.1f;
        public const float CraneLine = 0.45f;
        public const float GhostFill = 0.25f;
        public const float GhostNeutralStroke = 0.6f;
        public const float OverlayAlpha = 0.55f;
        public const float Gloss = 0.28f;

        /// <summary>shadow.lifted (JUICE #1 lifted silhouette): y +18 px, 30 % black.</summary>
        public const float LiftedShadowY = 18f;
        public const float LiftedShadowAlpha = 0.3f;

        /// <summary>Base colour of block colour index 0 … 7 (`ColorCodes.All`); an unknown index draws as W (State.ReadPiece).</summary>
        public static Rgba Block(int colorIndex) => Rgba.Hex(BlockHex[colorIndex >= 0 && colorIndex < BlockHex.Length ? colorIndex : 0]);

        /// <summary>Symbol ink of colour index 0 … 7 (ART §2.2).</summary>
        public static Rgba SymbolInk(int colorIndex)
        {
            int i = colorIndex >= 0 && colorIndex < BlockHex.Length ? colorIndex : 0;
            return DarkInk[i] ? Block(i).Scale(SymbolDarkFactor) : Rgba.White.WithAlpha(SymbolWhiteAlpha);
        }

        /// <summary>Colour index of a colour code ("W" … "P"); −1 for an unknown code.</summary>
        public static int IndexOf(string code) => ColorCodes.IndexOf(code);

        public static Rgba Of(string hex, float alpha = 1f) => Rgba.Hex(hex, alpha);

        /// <summary>Shadow outline colour (UX §5.4).</summary>
        public static Rgba GhostOutline(ShadowOutline o)
        {
            switch (o)
            {
                case ShadowOutline.Valid: return Rgba.Hex(GhostValid);
                case ShadowOutline.Invalid: return Rgba.Hex(GhostInvalid);
                default: return Rgba.Hex(GhostNeutral, GhostNeutralStroke);
            }
        }
    }

    /// <summary>The K-18 shadow outline (`color.ghost.*`).</summary>
    public enum ShadowOutline
    {
        Neutral,
        Valid,
        Invalid,
    }

    internal static class ArtMath
    {
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);
    }
}
