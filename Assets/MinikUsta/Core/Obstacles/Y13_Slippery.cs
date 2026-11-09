#nullable enable
// Port of src/core/obstacles/Y13_slippery.ts (web repo, source of truth).
namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// Y13 Kaygan Blok — slippery block (GDD §16 K-61): a `slippery` yard block falls like a Y6 block even without Y6 (core: Gravity.YardEntities).
    /// Core model (no hook): this plugin declares the rule for the level and its info card.
    /// </summary>
    public static class Y13_Slippery
    {
        public static readonly ObstacleRule Rule = new ObstacleRule(
            "Y13",
            RuleZones.Yard,
            213,
            new[] { ObstacleRules.ObstacleInfoKey("Y13") },
            lvl => ObstacleRules.UsesMechanic(lvl, "Y13"));
    }
}
