#nullable enable
// Port of src/core/obstacles/W1_staticGap.ts (web repo, source of truth).
namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// W1 Sabit Geçit — static gap (docs/OBSTACLES.md W1; docs/GDD.md K-04, K-12; first level 3).
    ///
    /// Rule: a gap `{ y, size, type: "static" }` in the wall that is always open. A block whose rows all lie inside the gap
    /// rows enters it by moving right from a position fully in the yard (RAIL mode: horizontal moves only, vertical
    /// position locked); it may leave to the left onto a position fully in the yard (back to FREE). Released with every
    /// cell on the site it is held by the scaffold and does not fall, even over empty cells. Data rule
    /// `y + size ≤ height − 1` (K-04) is the validator's `gap_touches_top` (L-09).
    ///
    /// The rail is the core movement model (Movement.cs RAIL nodes; Gravity.cs lands a rail block where it is), so this
    /// plugin has no hook: a static gap's `open` field is 1 from the level start and nothing closes it. It owns the `static`
    /// gap type so the gap hooks of other gap rules (W4–W7) never reach it.
    /// </summary>
    public static class W1_StaticGap
    {
        public static readonly ObstacleRule Rule = new ObstacleRule(
            "W1",
            RuleZones.Wall,
            101,
            new[] { ObstacleRules.ObstacleInfoKey("W1") },
            lvl => ObstacleRules.UsesMechanic(lvl, "W1"),
            new RuleOwnership(gapType: "static"));
    }
}
