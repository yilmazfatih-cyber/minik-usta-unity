#nullable enable
// Port of src/core/obstacles/types.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;

namespace MinikUsta.Core.Obstacles
{
    /// <summary>Rule ids (OBSTACLES.md): one plugin per id. S7 covers both hidden-plan mechanics (S7-R, S7-M).</summary>
    public static class RuleIds
    {
        public static readonly string[] All =
        {
            "W1", "W2", "W3", "W4", "W5", "W6", "W7", "W8",
            "Y1", "Y2", "Y3", "Y4", "Y5", "Y6", "Y7", "Y8",
            "S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8",
            "G-H", "G-L",
        };

        public static bool IsRuleId(string id) => Array.IndexOf(All, id) >= 0;
    }

    /// <summary>OBSTACLES zone of a rule: W wall, Y yard, S site, G gravity. TS `RuleZone`, `RULE_ORDER_BASE`, `ruleZone`.</summary>
    public static class RuleZones
    {
        public const string Wall = "wall";
        public const string Yard = "yard";
        public const string Site = "site";
        public const string Gravity = "gravity";

        /// <summary>TECH §7.3: hook call order W (100s) → Y (200s) → S (300s) → G (400s). An `order` is `base + 1 … base + 99`.</summary>
        public static int OrderBase(string zone)
        {
            switch (zone)
            {
                case Wall: return 100;
                case Yard: return 200;
                case Site: return 300;
                case Gravity: return 400;
                default: throw new ArgumentOutOfRangeException(nameof(zone), "unknown rule zone " + zone);
            }
        }

        /// <summary>The zone a rule id belongs to (its first letter).</summary>
        public static string Of(string id)
        {
            char p = id.Length > 0 ? id[0] : ' ';
            return p == 'W' ? Wall : p == 'Y' ? Yard : p == 'S' ? Site : Gravity;
        }
    }

    /// <summary>
    /// Entities a rule owns (TECH §7.1 `owns`). The registry builds `RuleByObstacleType`, `RuleByPieceFlag` and
    /// `RuleByGapType` from it, so `onNeighborMoved`, `onCellUncovered`, `canPassGap` and `onPassGap` go straight to the
    /// owner. Each entity type has at most one owner.
    /// </summary>
    public sealed class RuleOwnership
    {
        /// <summary>crate | cement_bag | screw | key.</summary>
        public readonly string? Obstacle;
        /// <summary>A `PieceFlags` name.</summary>
        public readonly string? PieceFlag;
        /// <summary>static | shutter | slider | paint | locked.</summary>
        public readonly string? GapType;

        public RuleOwnership(string? obstacle = null, string? pieceFlag = null, string? gapType = null)
        {
            Obstacle = obstacle;
            PieceFlag = pieceFlag;
            GapType = gapType;
        }
    }

    /// <summary>
    /// One obstacle / gravity rule (TECH §7.1 `ObstacleRule`). Every hook is optional. Immutable once built (TS `defineRule`
    /// freezes the rule).
    ///
    /// Hook signatures follow the move pipeline (TECH §7.1, refined in Phase 2):
    /// - board hooks take the state, because the scene also calls them outside a move (drag preview `beginDrag`, shadow
    ///   `computeFall`): `CanPick`, `CanPassGap`, `ModifyFall`, `OnLanded`, `OnPlacement`;
    /// - move hooks take the `RuleContext` of the running move (events at the current K-35 step, the state's RNG, the
    ///   move scratch): `OnPassGap`, `OnYardRelease`, `MoveCost`, `OnNeighborMoved`, `OnCellUncovered`, `OnMoveEnd`.
    /// Obstacle state lives in the generic state-buffer fields (gap open / y / phase, obstacle hp, piece flags / counter),
    /// so a plugin writes no hashing or cloning code (Zobrist covers those fields, TECH §2.6).
    /// </summary>
    public sealed class ObstacleRule
    {
        public readonly string Id;
        public readonly string Zone;
        /// <summary>Hook call order inside one hook (§7.3); unique; inside the zone's hundred (`RuleZones.OrderBase`).</summary>
        public readonly int Order;
        /// <summary>Info card keys shown on the first encounter / on tap (OBSTACLES R-08); one per mechanic the rule covers.</summary>
        public readonly string[] InfoKeys;
        /// <summary>The level uses this rule (normally its OBSTACLES data signature, `ObstacleRules.UsesMechanic`).</summary>
        public readonly Func<CompiledLevel, bool> AppliesTo;
        public readonly RuleOwnership? Owns;

        // --- board hooks (state only; also called by the scene outside a move) ---
        /// <summary>K-09 (c): false forbids picking the block (Y3 chain, Y4 wet). All active rules must agree.</summary>
        public readonly Func<GameState, int, bool>? CanPick;
        /// <summary>RAIL condition 4 for gaps of the owned `GapType` (W4, W7, K-40 included); the owner decides alone. (s, gap, pieceId).</summary>
        public readonly Func<GameState, int, int, bool>? CanPassGap;
        /// <summary>K-35 step 2: wind drift / balloon direction (W8, S8); chained in rule order.</summary>
        public readonly Func<GameState, int, FallPlan, FallPlan>? ModifyFall;
        /// <summary>K-35 step 2: landing effect (S3 glass); the first non-`none` effect wins.</summary>
        public readonly Func<GameState, int, FallResult, LandingEffect>? OnLanded;
        /// <summary>K-35 step 3, wrong placements only: `stick` (Y8); the first `stick` wins. Returns a `PlacementOverrides` value.</summary>
        public readonly Func<GameState, int, BoardCell[], Verdict, string>? OnPlacement;

        // --- move hooks (RuleContext of the running move) ---
        /// <summary>K-35 step 1: the drag passed the rail of a gap of the owned `GapType` (`move.via`, W6). (ctx, gap, pieceId).</summary>
        public readonly Action<RuleContext, int, int>? OnPassGap;
        /// <summary>K-35 step 2: a block was released in the yard (S8 balloon rises).</summary>
        public readonly Action<RuleContext, int>? OnYardRelease;
        /// <summary>K-35 step 4 base cost (Y8 stuck: 2); null = no opinion. Several rules → the largest; none → 1 (K-07).</summary>
        public readonly Func<RuleContext, int, int?>? MoveCost;
        /// <summary>K-35 steps 5–6: a yard entity of the owned obstacle type / piece flag next to the moved or fallen block. Returns a `NeighborEffects` value.</summary>
        public readonly Func<RuleContext, EntityRef, int, string>? OnNeighborMoved;
        /// <summary>K-35 steps 5–6 (K-42): the cell of a hidden item of the owned obstacle type is empty (Y7 screw, W7 key).</summary>
        public readonly Action<RuleContext, int>? OnCellUncovered;
        /// <summary>K-35 step 10 timer; needs `MoveEndOrder` and an id of `Compiler.Step10Timers` (W4, W5, Y4).</summary>
        public readonly Action<RuleContext>? OnMoveEnd;
        /// <summary>GDD K-35 step 10 position of `OnMoveEnd` (= `Step10Timers` order of the id), independent of `Order`.</summary>
        public readonly int? MoveEndOrder;
        /// <summary>End of K-35 step 5, after the neighbour effects (Y3: release a chain with no neighbour left, E-53).</summary>
        public readonly Action<RuleContext>? AfterNeighbors;
        /// <summary>K-36: the hammer may hit this owned entity now (crate, bag, chained block). Needs `OnHammer`.</summary>
        public readonly Func<GameState, EntityRef, bool>? CanHammer;
        /// <summary>K-36: the hammer hit on an owned entity (step 1 of the mini pipeline). Returns the analytics target kind.</summary>
        public readonly Func<RuleContext, EntityRef, string>? OnHammer;
        /// <summary>K-30 D1 help, first stage: lift the rule's blockers (Y3 chains, Y4 wetness). True when something changed.</summary>
        public readonly Func<RuleContext, bool>? OnTruckHelp;

        public ObstacleRule(string id, string zone, int order, string[] infoKeys, Func<CompiledLevel, bool> appliesTo, RuleOwnership? owns = null,
            Func<GameState, int, bool>? canPick = null, Func<GameState, int, int, bool>? canPassGap = null,
            Func<GameState, int, FallPlan, FallPlan>? modifyFall = null, Func<GameState, int, FallResult, LandingEffect>? onLanded = null,
            Func<GameState, int, BoardCell[], Verdict, string>? onPlacement = null, Action<RuleContext, int, int>? onPassGap = null,
            Action<RuleContext, int>? onYardRelease = null, Func<RuleContext, int, int?>? moveCost = null,
            Func<RuleContext, EntityRef, int, string>? onNeighborMoved = null, Action<RuleContext, int>? onCellUncovered = null,
            Action<RuleContext>? onMoveEnd = null, int? moveEndOrder = null, Action<RuleContext>? afterNeighbors = null,
            Func<GameState, EntityRef, bool>? canHammer = null, Func<RuleContext, EntityRef, string>? onHammer = null,
            Func<RuleContext, bool>? onTruckHelp = null)
        {
            Id = id;
            Zone = zone;
            Order = order;
            InfoKeys = (string[])infoKeys.Clone();
            AppliesTo = appliesTo;
            Owns = owns;
            CanPick = canPick;
            CanPassGap = canPassGap;
            ModifyFall = modifyFall;
            OnLanded = onLanded;
            OnPlacement = onPlacement;
            OnPassGap = onPassGap;
            OnYardRelease = onYardRelease;
            MoveCost = moveCost;
            OnNeighborMoved = onNeighborMoved;
            OnCellUncovered = onCellUncovered;
            OnMoveEnd = onMoveEnd;
            MoveEndOrder = moveEndOrder;
            AfterNeighbors = afterNeighbors;
            CanHammer = canHammer;
            OnHammer = onHammer;
            OnTruckHelp = onTruckHelp;
        }

        /// <summary>A copy with another id (tests: `{ ...fakeRule('W1'), id: 'X9' }`).</summary>
        public ObstacleRule WithId(string id) => new ObstacleRule(id, Zone, Order, InfoKeys, AppliesTo, Owns, CanPick, CanPassGap, ModifyFall, OnLanded,
            OnPlacement, OnPassGap, OnYardRelease, MoveCost, OnNeighborMoved, OnCellUncovered, OnMoveEnd, MoveEndOrder, AfterNeighbors, CanHammer, OnHammer, OnTruckHelp);

        public override string ToString() => "rule " + Id;
    }

    /// <summary>Plugin helpers of types.ts (`obstacleInfoKey`, `usesMechanic`).</summary>
    public static class ObstacleRules
    {
        /// <summary>`W1` → `obs.w1.desc`, `G-H` → `obs.gh.desc`, `S7-R` → `obs.s7r.desc` (OBSTACLES R-08).</summary>
        public static string ObstacleInfoKey(string id) => "obs." + id.ToLowerInvariant().Replace("-", "") + ".desc";

        /// <summary>The level uses mechanic `id` (derived from its data signature only, K-45/9; Level/Mechanics.cs).</summary>
        public static bool UsesMechanic(CompiledLevel lvl, string id) => Array.IndexOf(lvl.Mechanics, id) >= 0;
    }
}
