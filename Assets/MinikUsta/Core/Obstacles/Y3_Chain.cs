#nullable enable
// Port of src/core/obstacles/Y3_chain.ts (web repo, source of truth).
using System.Collections.Generic;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Obstacles
{
    /// <summary>
    /// Y3 Zincir — chain (docs/OBSTACLES.md Y3; docs/GDD.md K-09 (c), K-35 step 5, K-36, K-41, K-30 D1, E-13, E-53; first
    /// level 24).
    ///
    /// Rule: a `chained` yard block cannot be held (K-09 (c)) nor taken by the crane, but falls with yard gravity. The chain
    /// goes (and `clear: chain` counts 1, K-41) when:
    /// - a neighbour moves (K-35 step 5 / 6 neighbour effect: a block whose start cells — or pre-fall cells — touch it);
    /// - Faz 2R (full cover, E-53): at the end of step 5 no block, Ağır Yük, crate or bag is left on its 4-neighbourhood
    ///   (inside the yard; the wall boundary is never crossed) — otherwise it could never be freed and the yard would never
    ///   empty (K-48);
    /// - the hammer hits it (K-36: the chain, not the block);
    /// - the truck help D1 lifts every chain (K-30).
    /// Chains are released in (y, x) order of the blocks' anchors.
    /// </summary>
    public static class Y3_Chain
    {
        private static bool ChainedYard(GameState s, int id) => PieceZone(s, id) == Zone.Yard && HasFlag(s, id, FlagBit.Chained);

        /// <summary>Removes the chain of `id`, counts `clear: chain` and emits `chainReleased` (K-41).</summary>
        private static void Release(RuleContext ctx, int id)
        {
            SetFlag(ctx.S, id, FlagBit.Chained, false);
            Goals.AddGoalCount(ctx.S, GoalKinds.Chain, 1);
            ctx.Emit(new ChainReleasedEvent(id));
        }

        /// <summary>Chained yard blocks in (y, x) order of their anchors (K-35 processing order).</summary>
        private static List<int> ChainedBlocks(GameState s)
        {
            var o = new List<int>();
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++) if (ChainedYard(s, id)) o.Add(id);
            o.Sort((a, b) =>
            {
                int d = PieceY(s, a) - PieceY(s, b);
                if (d != 0) return d;
                d = PieceX(s, a) - PieceX(s, b);
                return d != 0 ? d : a - b;
            });
            return o;
        }

        /// <summary>E-53: some yard cell next to the block holds another block, an Ağır Yük, a crate or a bag.</summary>
        private static bool HasNeighbour(GameState s, int id)
        {
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            int x0 = PieceX(s, id);
            int y0 = PieceY(s, id);
            int self = id + 1;
            foreach (var c in shape.Cells)
            {
                foreach (var n in Coords.Neighbors4(s.Lvl.Geo, x0 + c.X, y0 + c.Y))
                {
                    int v = YardOcc(s, n.Ix, n.Iy);
                    if (v != 0 && v != self) return true;
                }
            }
            return false;
        }

        public static readonly ObstacleRule Rule = new ObstacleRule(
            "Y3",
            RuleZones.Yard,
            203,
            new[] { ObstacleRules.ObstacleInfoKey("Y3") },
            lvl => ObstacleRules.UsesMechanic(lvl, "Y3"),
            new RuleOwnership(pieceFlag: PieceFlags.Chained),
            canPick: (s, id) => !HasFlag(s, id, FlagBit.Chained),
            onNeighborMoved: (ctx, entity, moved) =>
            {
                if (!entity.IsPiece || !ChainedYard(ctx.S, entity.Id)) return NeighborEffects.None;
                Release(ctx, entity.Id);
                return NeighborEffects.Affected;
            },
            afterNeighbors: ctx =>
            {
                foreach (int id in ChainedBlocks(ctx.S)) if (!HasNeighbour(ctx.S, id)) Release(ctx, id);
            },
            canHammer: (s, entity) => entity.IsPiece && ChainedYard(s, entity.Id),
            onHammer: (ctx, entity) =>
            {
                if (entity.IsPiece) Release(ctx, entity.Id);
                return HammerTargetKinds.Chain;
            },
            onTruckHelp: ctx =>
            {
                var ids = ChainedBlocks(ctx.S);
                foreach (int id in ids) Release(ctx, id);
                return ids.Count > 0;
            });
    }
}
