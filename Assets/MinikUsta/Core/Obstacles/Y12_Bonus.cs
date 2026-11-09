#nullable enable
// Port of src/core/obstacles/Y12_bonus.ts (web repo, source of truth).
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// Y12 Bonus Blok — bonus block (GDD §16 K-60, K-62): K-35 step 4, after the cost, a correct placement (drag, crane or Golden
    /// Trowel) of a `bonus` block adds its `bonusMoves` (1–3, default 2) to the counter (`movesChanged { reason: bonus }`) and the
    /// badge goes (the flag is cleared).
    /// </summary>
    public static class Y12_Bonus
    {
        public static readonly ObstacleRule Rule = new ObstacleRule(
            "Y12",
            RuleZones.Yard,
            212,
            new[] { ObstacleRules.ObstacleInfoKey("Y12") },
            lvl => ObstacleRules.UsesMechanic(lvl, "Y12"),
            placementBonus: (ctx, id) =>
            {
                if (!HasFlag(ctx.S, id, FlagBit.Bonus)) return 0;
                SetFlag(ctx.S, id, FlagBit.Bonus, false);
                int n = ctx.Lvl.Pieces[id].BonusMoves;
                return n > 0 ? n : Compiler.DefaultBonusMoves;
            });
    }
}
