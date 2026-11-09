#nullable enable
// Port of src/core/obstacles/Y11_wrap.ts (web repo, source of truth).
using System.Collections.Generic;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// Y11 Sargılı Blok ve Makas — wrapped block and scissors (GDD §16 K-59, K-66): a `wrapped` block cannot be held (K-09 (c)), nor
    /// taken by the crane or the Golden Trowel; K-35 step 5 of the action whose correct placement puts the `scissors` block on the
    /// site: every wrap goes, and the scissors flag with it (`unwrapped { by: scissors }`); the hammer cuts one wrap (kind `wrap`).
    /// </summary>
    public static class Y11_Wrap
    {
        private static bool WrappedYard(GameState s, int id) =>
            PieceZone(s, id) == Zone.Yard && HasFlag(s, id, FlagBit.Wrapped) && !Grid.IsCovered(s, id);

        public static readonly ObstacleRule Rule = new ObstacleRule(
            "Y11",
            RuleZones.Yard,
            211,
            new[] { ObstacleRules.ObstacleInfoKey("Y11") },
            lvl => ObstacleRules.UsesMechanic(lvl, "Y11"),
            new RuleOwnership(pieceFlag: PieceFlags.Wrapped),
            canPick: (s, id) => !HasFlag(s, id, FlagBit.Wrapped),
            onCorrectPlacement: (ctx, id) =>
            {
                if (!HasFlag(ctx.S, id, FlagBit.Scissors)) return;
                SetFlag(ctx.S, id, FlagBit.Scissors, false);
                var pieces = new List<int>();
                int P = ctx.Lvl.Layout.Counts.Pieces;
                for (int p = 0; p < P; p++)
                {
                    if (!HasFlag(ctx.S, p, FlagBit.Wrapped)) continue;
                    SetFlag(ctx.S, p, FlagBit.Wrapped, false);
                    pieces.Add(p);
                }
                ctx.Emit(new UnwrappedEvent(UnwrapBys.Scissors, id, pieces.ToArray()));
            },
            hammerKind: HammerTargetKinds.Wrap,
            canHammer: (s, entity) => entity.IsPiece && WrappedYard(s, entity.Id),
            onHammer: (ctx, entity) =>
            {
                if (entity.IsPiece)
                {
                    SetFlag(ctx.S, entity.Id, FlagBit.Wrapped, false);
                    ctx.Emit(new UnwrappedEvent(UnwrapBys.Hammer, entity.Id, new[] { entity.Id }));
                }
                return HammerTargetKinds.Wrap;
            });
    }
}
