#nullable enable
// The emergency look (paket V1): when an atlas frame is missing (atlas.json or a page did not load, a frame name the art pack
// does not have yet) the views draw the procedural placeholder tile of `Atlas` instead, tinted like the Faz 3 placeholder
// board — the game stays playable and readable, never a white box. Pure: frame name → (tile, tint); null = draw nothing.
using System;
using System.Globalization;

namespace MinikUsta.Presentation.Art
{
    public readonly struct Fallback
    {
        public readonly Tile Tile;
        public readonly Rgba Tint;
        /// <summary>9-slice the tile (`Tile.Panel`).</summary>
        public readonly bool Sliced;

        public Fallback(Tile tile, Rgba tint, bool sliced = false)
        {
            Tile = tile;
            Tint = tint;
            Sliced = sliced;
        }
    }

    public static class FallbackTiles
    {
        /// <summary>C1: the tutorial highlight frame (K-53). Not in atlas.json by design (procedural); an art pack that adds a frame of this name replaces it.</summary>
        public const string TutorialHighlight = "tut_highlight";

        /// <summary>Frames that are procedural on purpose: a missing atlas entry is no warning for them.</summary>
        public static bool IsProceduralOnly(string frame) => frame == TutorialHighlight;

        private static int ColorOf(string letter)
        {
            int i = MinikUsta.Core.ColorCodes.IndexOf(letter.ToUpperInvariant());
            return i < 0 ? 0 : i;
        }

        private static int MaskOf(string mm) => int.TryParse(mm, NumberStyles.None, CultureInfo.InvariantCulture, out int m) ? m & 15 : 0;

        public static Fallback? Of(string frame)
        {
            var p = frame.Split('_');
            if (frame.StartsWith("blk_", StringComparison.Ordinal) && p.Length >= 3 && p[1].Length == 1)
            {
                var c = Palette.Block(ColorOf(p[1]));
                if (p.Length == 3) return new Fallback(Atlas.Body(MaskOf(p[2])), c);
                return null; // corner fills: the placeholder bodies are rounded per open side, no notch to close
            }
            if (frame == Frames.Gloss) return new Fallback(Tile.Gloss, Rgba.White.WithAlpha(Palette.Gloss));
            if (frame == Frames.Locked) return null;
            if (frame.StartsWith("ghost_", StringComparison.Ordinal))
            {
                if (frame == Frames.GhostHidden) return new Fallback(Tile.Hidden, Rgba.Hex(Palette.PlanUnderlay));
                if (frame == Frames.GhostFront) return new Fallback(Tile.Ghost, Rgba.Hex(Palette.BuildFront));
                return new Fallback(Tile.Plan, Palette.Block(ColorOf(p[1])).WithAlpha(Palette.PlanFill));
            }
            if (frame.StartsWith("shadow_fill_", StringComparison.Ordinal)) return new Fallback(Atlas.Body(MaskOf(p[2])), Rgba.White);
            if (frame.StartsWith("shadow_", StringComparison.Ordinal)) return p.Length == 3 ? new Fallback(Tile.Ghost, Rgba.White) : (Fallback?)null;
            if (frame.StartsWith("sym_", StringComparison.Ordinal)) return new Fallback(Atlas.Symbol(ColorOf(p[1])), Rgba.White);
            switch (frame)
            {
                case Frames.BgLevel: return new Fallback(Tile.Gradient, Rgba.White);
                case Frames.BgHome: return new Fallback(Tile.Gradient, Rgba.White);
                case Frames.FloorYardA: return new Fallback(Tile.Floor, Rgba.Hex(Palette.YardFloor));
                case Frames.FloorYardB: return new Fallback(Tile.Floor, Rgba.Hex(Palette.YardFloorAlt));
                case Frames.FloorSite: return new Fallback(Tile.Plain, Rgba.Hex(Palette.Blueprint));
                case Frames.FloorSiteDeep: return new Fallback(Tile.Plain, Rgba.Hex(Palette.BlueprintDeep));
                case Frames.WallSeg: return new Fallback(Tile.Plain, Rgba.Hex(Palette.Wall));
                case Frames.GapSeg: return new Fallback(Tile.Gap, Rgba.Hex(Palette.Rail));
                case Frames.Tray: return new Fallback(Tile.Panel, Rgba.Hex("#3F35A0"), true);
                case Frames.White: return new Fallback(Tile.Plain, Rgba.White);
                case Frames.FxSpark: return new Fallback(Tile.Spark, Rgba.White);
                case Frames.FxDust: return new Fallback(Tile.Soft, Rgba.White);
                case Frames.FxStar: return new Fallback(Tile.Star, Rgba.White);
                case Frames.FxConfetti: return new Fallback(Tile.Plain, Rgba.White);
                case Frames.FxConfettiRound: return new Fallback(Tile.Circle, Rgba.White);
                case Frames.FxRing: return new Fallback(Tile.Ring, Rgba.White);
                case TutorialHighlight: return new Fallback(Tile.Highlight, Rgba.Hex(Palette.Gold), true);
                case Frames.DropBlob: return new Fallback(Tile.Soft, Rgba.Black.WithAlpha(Palette.LiftedShadowAlpha));
                case "icon_coin": return new Fallback(Tile.Coin, Rgba.Hex(Palette.Gold));
                case "icon_star": return new Fallback(Tile.Star, Rgba.Hex(Palette.Gold));
                case "icon_star_empty": return new Fallback(Tile.Star, Rgba.White.WithAlpha(0.3f));
                case "icon_gold_trowel": return new Fallback(Tile.Trowel, Rgba.Hex(Palette.Gold));
                case "icon_pause": return new Fallback(Tile.Pause, Rgba.White);
                case "icon_hammer": return new Fallback(Tile.Hammer, Rgba.Hex(Palette.Ink));
                case "icon_crane": return new Fallback(Tile.CraneHook, Rgba.Hex(Palette.Ink));
                case "icon_brush": return new Fallback(Tile.Brush, Rgba.Hex(Palette.Ink));
                case "icon_undo": return new Fallback(Tile.Undo, Rgba.Hex(Palette.Ink));
                case "icon_check": return new Fallback(Tile.Check, Rgba.Hex(Palette.GhostValid));
                case "badge_dot": return new Fallback(Tile.Warn, Rgba.Hex(Palette.GhostInvalid));
                case "bead_on": return new Fallback(Tile.Circle, Rgba.Hex(Palette.Gold));
                case "bead_off": return new Fallback(Tile.Circle, Rgba.White.WithAlpha(0.25f));
                case "btn_primary":
                case "btn_primary_m": return new Fallback(Tile.Panel, Rgba.Hex(Palette.Primary), true);
                case "btn_offer": return new Fallback(Tile.Panel, Rgba.Hex(Palette.Secondary), true);
                case "btn_danger":
                case "btn_close": return new Fallback(Tile.Panel, Rgba.Hex(Palette.Danger), true);
                case "btn_disabled":
                case "btn_booster_off": return new Fallback(Tile.Panel, Rgba.Hex("#9A9AA8"), true);
                case "panel_window":
                case "panel_plate":
                case "panel_card":
                case "btn_secondary":
                case "ribbon_title": return new Fallback(Tile.Panel, Rgba.Hex(Palette.Panel), true);
            }
            if (frame.StartsWith("btn_", StringComparison.Ordinal) || frame.StartsWith("capsule_", StringComparison.Ordinal) || frame.StartsWith("hud_", StringComparison.Ordinal)
                || frame.StartsWith("progress_", StringComparison.Ordinal) || frame.StartsWith("nav_", StringComparison.Ordinal))
                return new Fallback(Tile.Panel, Rgba.Hex(Palette.Neutral), true);
            return null; // characters, logos, decorations: nothing rather than a box
        }
    }
}
