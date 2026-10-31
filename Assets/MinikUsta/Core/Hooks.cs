#nullable enable
// Port of the hook-side types of src/core/moves.ts (web repo, source of truth): MoveScratch, RuleContext, EntityRef,
// NeighborEffect, MoveHooks, HammerRules and NO_HOOKS. They are what obstacle rules (core/obstacles), boosters, the
// deadlock check and the turn summary see of the move pipeline; `applyMove` itself, the event sinks and `eventLogHash`
// follow with the K-35 package (TECH_DESIGN §6, §7.1).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;

namespace MinikUsta.Core
{
    /// <summary>Per-move scratch data (TECH §5.2 `MoveScratch`, §7.1): never stored in the state buffer nor hashed.</summary>
    public sealed class MoveScratch
    {
        /// <summary>Y8: the moved block was stuck at move start (written in step 0; the base cost reads it, step 4).</summary>
        public readonly bool WasStuck;
        /// <summary>S5: the front segment turned in this move's step 8 (the step-10 counter then does not tick, K-23).</summary>
        public bool RotatedAtStep8;
        /// <summary>Occupancy codes (`pieceId + 1`, `−(obstacle + 1)`) of entities already hit by a neighbour effect in this move.</summary>
        public readonly HashSet<int> Affected;

        public MoveScratch(bool wasStuck = false, bool rotatedAtStep8 = false, HashSet<int>? affected = null)
        {
            WasStuck = wasStuck;
            RotatedAtStep8 = rotatedAtStep8;
            Affected = affected ?? new HashSet<int>();
        }
    }

    /// <summary>
    /// What a rule hook sees (TECH §7.1 `RuleContext`). `Emit` writes at the current pipeline step (the pipeline's emitter
    /// numbers the event: `Seq`, `Step`). `Rng` is the state's mulberry32 (deterministic, K-30 truck help).
    /// </summary>
    public sealed class RuleContext
    {
        public readonly CompiledLevel Lvl;
        public readonly GameState S;
        public readonly IRng Rng;
        public readonly GravityProfile Gravity;
        public readonly MoveScratch Scratch;
        private readonly Action<GameEvent> emit;

        public RuleContext(GameState s, Action<GameEvent> emit, IRng rng, MoveScratch scratch)
        {
            Lvl = s.Lvl;
            S = s;
            this.emit = emit;
            Rng = rng;
            Gravity = s.Lvl.Gravity;
            Scratch = scratch;
        }

        /// <summary>A context over `s` whose RNG is the state buffer's (TS `makeContext` / tests' `ctxOf`).</summary>
        public static RuleContext Of(GameState s, Action<GameEvent> emit, MoveScratch? scratch = null) =>
            new RuleContext(s, emit, new BufferRng(s.Buf, H.Rng), scratch ?? new MoveScratch());

        public void Emit(GameEvent e) => emit(e);

        /// <summary>TS `{ ...ctx, emit }`: the same context with another event receiver.</summary>
        public RuleContext WithEmit(Action<GameEvent> other) => new RuleContext(S, other, Rng, Scratch);
    }

    /// <summary>A yard neighbour of a moved block (TECH §7.1 `EntityRef`): `obstacle` (index) or `piece` (id).</summary>
    public readonly struct EntityRef : IEquatable<EntityRef>
    {
        public const string KindObstacle = "obstacle";
        public const string KindPiece = "piece";

        public readonly string Kind;
        /// <summary>Obstacle index (`Kind == obstacle`).</summary>
        public readonly int Index;
        /// <summary>Piece id (`Kind == piece`).</summary>
        public readonly int Id;

        private EntityRef(string kind, int index, int id)
        {
            Kind = kind;
            Index = index;
            Id = id;
        }

        public static EntityRef Obstacle(int index) => new EntityRef(KindObstacle, index, -1);
        public static EntityRef Piece(int id) => new EntityRef(KindPiece, -1, id);

        public bool IsObstacle => Kind == KindObstacle;
        public bool IsPiece => Kind == KindPiece;

        public bool Equals(EntityRef o) => Kind == o.Kind && Index == o.Index && Id == o.Id;
        public override bool Equals(object? obj) => obj is EntityRef e && Equals(e);
        public override int GetHashCode() => (IsPiece ? 1 : 0) + 2 * (IsPiece ? Id : Index);
        public override string ToString() => IsPiece ? "piece " + Id : "obstacle " + Index;
    }

    /// <summary>
    /// Result of `onNeighborMoved`: `none` (not affected), `affected` (counted once for this move) or `freed` (affected and
    /// a yard cell became empty — bag torn, crate gone — so yard gravity runs again, E-12).
    /// </summary>
    public static class NeighborEffects
    {
        public const string None = "none";
        public const string Affected = "affected";
        public const string Freed = "freed";

        /// <summary>registry.ts `EFFECT_RANK`.</summary>
        public static int Rank(string effect) => effect == Freed ? 2 : effect == Affected ? 1 : 0;
    }

    /// <summary>Obstacle-rule hammer hooks (K-36), composed by the registry from the owners of the entity types.</summary>
    public sealed class HammerRules
    {
        private readonly Func<GameState, EntityRef, bool> canHit;
        private readonly Func<RuleContext, EntityRef, string> hit;

        public HammerRules(Func<GameState, EntityRef, bool> canHit, Func<RuleContext, EntityRef, string> hit)
        {
            this.canHit = canHit;
            this.hit = hit;
        }

        /// <summary>The hammer may hit `entity` now (a crate / bag that still stands, a chained yard block).</summary>
        public bool CanHit(GameState s, EntityRef entity) => canHit(s, entity);

        /// <summary>Step 1 of the mini pipeline: the hit (events, goal counts). Returns the analytics target kind (`HammerTargetKinds`).</summary>
        public string Hit(RuleContext ctx, EntityRef entity) => hit(ctx, entity);
    }

    /// <summary>
    /// Obstacle hooks of the pipeline (TECH §7.1 `ObstacleRule`, composed in rule order by core/obstacles Registry
    /// `LevelHooks`). Every field is optional; Phase 2 levels (W1, S1, S2) need none. TS `MoveHooks`; `None` = `NO_HOOKS`.
    /// </summary>
    public sealed class MoveHooks
    {
        /// <summary>No rule at all (tests, tools that want the bare core). TS `NO_HOOKS`.</summary>
        public static readonly MoveHooks None = new MoveHooks();

        /// <summary>K-09 (c) `canPick`, RAIL `canPassGap` (W4, W7, K-40), K-07 row 5 `siteClosed`.</summary>
        public readonly DragRules? Drag;
        /// <summary>Step 2 `modifyFall` (W8, S8) and `onLanded` (S3).</summary>
        public readonly FallRules? Fall;
        /// <summary>Step 3 `onPlacement` (Y8 stick).</summary>
        public readonly PlacementRules? Placement;
        /// <summary>Step 1: the drag passed the rail of paint gate `gap` (`move.via`, W6). (ctx, gap, pieceId).</summary>
        public readonly Action<RuleContext, int, int>? OnPassGap;
        /// <summary>Step 2: a block released in the yard (S8 balloon rises). (ctx, pieceId).</summary>
        public readonly Action<RuleContext, int>? OnYardRelease;
        /// <summary>Step 4 base cost (Y8: 2 when `ctx.Scratch.WasStuck`); null = 1. The glass penalty is added on top.</summary>
        public readonly Func<RuleContext, int, int?>? MoveCost;
        /// <summary>Steps 5–6: a yard entity next to the moved / fallen block (Y1 crate, Y2 bag, Y3 chain). (ctx, entity, movedPieceId) → NeighborEffects.</summary>
        public readonly Func<RuleContext, EntityRef, int, string>? OnNeighborMoved;
        /// <summary>Steps 5–6: the cell of hidden item `obstacle` (screw Y7, key W7) is empty and not collected yet (K-42).</summary>
        public readonly Action<RuleContext, int>? OnCellUncovered;
        /// <summary>End of step 5, after the neighbour effects (Y3: a chain with no neighbour left is released, E-53).</summary>
        public readonly Action<RuleContext>? AfterNeighbors;
        /// <summary>K-36: hammer targets owned by obstacle rules (crate Y1, bag Y2, chain Y3).</summary>
        public readonly HammerRules? Hammer;
        /// <summary>K-30 D1 help, first stage: rules lift chains (Y3) and wetness (Y4). True when something changed.</summary>
        public readonly Func<RuleContext, bool>? OnTruckHelp;
        /// <summary>Step 10 timers (rule `onMoveEnd`, site S5 / S6 ticks) by timer id, called in `lvl.Step10` order (§7.3).</summary>
        public readonly IReadOnlyDictionary<string, Action<RuleContext>>? Timers;
        /// <summary>Step 12 override (tests): replaces `RunStep12` (Deadlock.cs) when given.</summary>
        public readonly Action<RuleContext>? Deadlock;

        public MoveHooks(DragRules? drag = null, FallRules? fall = null, PlacementRules? placement = null,
            Action<RuleContext, int, int>? onPassGap = null, Action<RuleContext, int>? onYardRelease = null,
            Func<RuleContext, int, int?>? moveCost = null, Func<RuleContext, EntityRef, int, string>? onNeighborMoved = null,
            Action<RuleContext, int>? onCellUncovered = null, Action<RuleContext>? afterNeighbors = null, HammerRules? hammer = null,
            Func<RuleContext, bool>? onTruckHelp = null, IReadOnlyDictionary<string, Action<RuleContext>>? timers = null,
            Action<RuleContext>? deadlock = null)
        {
            Drag = drag;
            Fall = fall;
            Placement = placement;
            OnPassGap = onPassGap;
            OnYardRelease = onYardRelease;
            MoveCost = moveCost;
            OnNeighborMoved = onNeighborMoved;
            OnCellUncovered = onCellUncovered;
            AfterNeighbors = afterNeighbors;
            Hammer = hammer;
            OnTruckHelp = onTruckHelp;
            Timers = timers;
            Deadlock = deadlock;
        }

        /// <summary>TS `Object.keys(hooks)` sorted: the names of the hooks that are set (an empty array = TS `{}`).</summary>
        public string[] DefinedKeys()
        {
            var o = new List<string>();
            if (AfterNeighbors != null) o.Add("afterNeighbors");
            if (Deadlock != null) o.Add("deadlock");
            if (Drag != null) o.Add("drag");
            if (Fall != null) o.Add("fall");
            if (Hammer != null) o.Add("hammer");
            if (MoveCost != null) o.Add("moveCost");
            if (OnCellUncovered != null) o.Add("onCellUncovered");
            if (OnNeighborMoved != null) o.Add("onNeighborMoved");
            if (OnPassGap != null) o.Add("onPassGap");
            if (OnTruckHelp != null) o.Add("onTruckHelp");
            if (OnYardRelease != null) o.Add("onYardRelease");
            if (Placement != null) o.Add("placement");
            if (Timers != null) o.Add("timers");
            return o.ToArray();
        }

        /// <summary>TS `toEqual({})`.</summary>
        public bool IsEmpty => DefinedKeys().Length == 0;
    }
}
