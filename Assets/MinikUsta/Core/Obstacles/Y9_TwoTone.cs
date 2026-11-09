#nullable enable
// Port of src/core/obstacles/Y9_twoTone.ts (web repo, source of truth).
namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// Y9 İki Renkli Blok — two-tone block (GDD §16 K-57): one colour per cell, checked per cell (core: CellColors, PieceCellColor).
    /// Core model (no hook): this plugin declares the rule for the level and its info card.
    /// </summary>
    public static class Y9_TwoTone
    {
        public static readonly ObstacleRule Rule = new ObstacleRule(
            "Y9",
            RuleZones.Yard,
            209,
            new[] { ObstacleRules.ObstacleInfoKey("Y9") },
            lvl => ObstacleRules.UsesMechanic(lvl, "Y9"));
    }
}
