#nullable enable
// Port of src/core/obstacles/Y10_pallet.ts (web repo, source of truth).
namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// Y10 Palet — pallet (GDD §16 K-58): stacked layers on the same cells, only the top one can be held (core: Grid.LayerBelow / IsCovered / OwnCellCode, Movement `covered`).
    /// Core model (no hook): this plugin declares the rule for the level and its info card.
    /// </summary>
    public static class Y10_Pallet
    {
        public static readonly ObstacleRule Rule = new ObstacleRule(
            "Y10",
            RuleZones.Yard,
            210,
            new[] { ObstacleRules.ObstacleInfoKey("Y10") },
            lvl => ObstacleRules.UsesMechanic(lvl, "Y10"));
    }
}
