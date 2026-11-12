#nullable enable
// Port of src/core/moves.ts (web repo, source of truth): the move pipeline. Behaviour follows the TS file line by line;
// the hook-side types live in Hooks.cs, the sinks / emitter / event JSON in Events.cs, `fnv1a64` in Fnv1a.cs and
// `canonicalJson` in Json/JsonWriter.cs.
//
// The move pipeline (docs/GDD.md §12 K-35; K-07, K-17, K-22, K-25, K-26, K-28, K-29, K-33, K-41, K-46; TECH_DESIGN §6).
//
// `ApplyMove(state, move, sink, opts)` runs ONE move on `state` (mutating it) and writes its deterministic event log to
// `sink`. A drag that is not cancelled runs every K-35 step once, in GDD order:
//   0 classify the release (K-07 table; a cancel stops here and changes nothing) · 1 move the block (W6 paint hook) ·
//   2 site FREE: wind → fall / balloon → G-L steer → landing (`ComputeFall`), S3 glass break returns the block and skips
//   step 3 · 3 site: `IsCorrectPlacement` (K-16 + K-34) → lock / Y8 stick / K-17 bounce, streak · 4 cost (base + glass,
//   counter ≥ 0), `m += 1` · 5 neighbour effects of the start cells (once per entity, (y, x) order, never across the
//   wall boundary) + hidden item check #1 · 6 yard gravity (`SettleYard`) + check #2 · 7 goal counters · 8 segment
//   completion (site strategy) + next batch queued · 9 FIFO delivery · 10 timers (`lvl.Step10` order) · 11 win (K-48),
//   else out of moves · 12 deadlock check and truck help (Deadlock.RunStep12; Faz 2R: also at 0 moves, before the
//   out-of-moves window; skipped by `NoTruckHelp`). K-55 (D-099, `schemaVersion` 2): a level has one batch, so
//   step 8 queues nothing, step 9 only retries K-17 / K-36 queue blocks and a segment change never touches the yard.
//   A Söküm (K-30) restores the action's start buffer
//   (`ApplyOptions.PreAction`) except the counter and `movesSpent`; its `teardown` event is the action's last event.
// Boosters run the mini pipeline (GDD §10): the direct effect as step 1, then steps 5 (hidden items only), 6, 7, 8, 9,
// 11, 12: hammer (K-36), crane (K-37), paint brush (K-38) and the Golden Trowel (K-33, `goldTrowel`); preconditions,
// effects and the D3a pre-check live in Boosters.cs. The Faz 2 cell trowel (`trowel`) is rejected.
// `addMoves` from an accepted +5 offer sets no timer and no streak and runs step 12 once: D1 only (K-29, E-42); a D1
// that the rules cannot lift is a Söküm of the last action before the window (`RulesVersion` 3, K-55 item 8).
//
// Obstacles: core has no obstacle-specific `if`. Every rule effect comes through `MoveHooks`; by default the level's
// hooks from the obstacle registry (Obstacles/Registry.cs `LevelHooks`, TECH §7). Step 10 calls one timer hook per
// `lvl.Step10` entry (rule `onMoveEnd`, site S5 / S6 ticks); a timer without a hook throws "Phase 3".
//
// Events: `GameEvent.Step` = the K-35 step, `Seq` = 0, 1, 2 … inside the move. The log is a pure function of
// (state, move); `EventLogHash` (FNV-1a 64 over canonical JSON) pins it in golden tests (TECH §6.3, §9.5).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    // --- results -----------------------------------------------------------------------------------------------------------

    /// <summary>TS `MoveStatus`.</summary>
    public static class MoveStatuses
    {
        public const string Applied = "applied";
        public const string Cancelled = "cancelled";
        public const string Rejected = "rejected";
    }

    public class MoveResult
    {
        /// <summary>`cancelled`: K-07 cancel row or an invalid record (state unchanged); `rejected`: booster precondition (unchanged).</summary>
        public readonly string Status;
        /// <summary>Cancel reason (`moveCancelled.reason`) or rejection reason; null when applied.</summary>
        public readonly string? Reason;
        /// <summary>K-28: won in this move's step 11.</summary>
        public readonly bool Won;
        /// <summary>K-29: step 11 found the counter at 0 without a win ("Hamleler bitti!" window, opened after step 12).</summary>
        public readonly bool OutOfMoves;
        /// <summary>K-30: step 12 ran a Söküm (the state is back at the action's start except the counter and `movesSpent`). TS optional `teardownCause`.</summary>
        public readonly string? TeardownCause;

        public MoveResult(string status, string? reason, bool won, bool outOfMoves, string? teardownCause = null)
        {
            Status = status;
            Reason = reason;
            Won = won;
            OutOfMoves = outOfMoves;
            TeardownCause = teardownCause;
        }

        public bool IsApplied => Status == MoveStatuses.Applied;

        /// <summary>TS `toEqual` on a result (`teardownCause` undefined ≡ null).</summary>
        public bool SameAs(MoveResult o) => Status == o.Status && Reason == o.Reason && Won == o.Won && OutOfMoves == o.OutOfMoves && TeardownCause == o.TeardownCause;

        public override string ToString() => Status + (Reason != null ? "/" + Reason : "") + (Won ? " won" : "") + (OutOfMoves ? " outOfMoves" : "") + (TeardownCause != null ? " teardown " + TeardownCause : "");

        internal static readonly MoveResult AppliedResult = new MoveResult(MoveStatuses.Applied, null, false, false);
    }

    public sealed class ApplyOptions
    {
        public static readonly ApplyOptions Default = new ApplyOptions();

        /// <summary>Rule hooks; default `Registry.LevelHooks(state.Lvl)` (the obstacle registry, TECH §7.1).</summary>
        public readonly MoveHooks? Hooks;
        /// <summary>Skip K-35 step 12 (solver, trap scan, bot planning, K-30 guarantee simulation; TECH §6.1). Never in GameSession.</summary>
        public readonly bool NoTruckHelp;
        /// <summary>Throw on an invalid move record instead of emitting `moveCancelled{reason: 'invalid'}` (development, tests).</summary>
        public readonly bool Strict;
        /// <summary>
        /// K-30 Söküm target: the buffer before this action. `GameSession` passes the copy it keeps for Undo; without it
        /// `ApplyMove` takes its own copy whenever step 12 may run. For an accepted +5 offer (`addMoves` from
        /// `offerCoins` / `offerAd`) it is the start buffer of the last action before the window: the target of a D1 Söküm
        /// (K-29, `RulesVersion` 3); absent = no Söküm after the offer.
        /// </summary>
        public readonly int[]? PreAction;
        /// <summary>K-30 D3b dead-state table of the level; null in Faz 2R (cut 1).</summary>
        public readonly IDeadTable? DeadTable;

        public ApplyOptions(MoveHooks? hooks = null, bool noTruckHelp = false, bool strict = false, int[]? preAction = null, IDeadTable? deadTable = null)
        {
            Hooks = hooks;
            NoTruckHelp = noTruckHelp;
            Strict = strict;
            PreAction = preAction;
            DeadTable = deadTable;
        }

        /// <summary>TS `{ ...opts, preAction }`.</summary>
        public ApplyOptions WithPreAction(int[]? preAction) => new ApplyOptions(Hooks, NoTruckHelp, Strict, preAction, DeadTable);
    }

    /// <summary>K-46 YAO measure (TS `YaoMeasure`).</summary>
    public sealed class YaoMeasure : IEquatable<YaoMeasure>
    {
        /// <summary>Correct drag placements that entered the site FREE (over the wall; balloon and G-L steering included).</summary>
        public readonly int OverWall;
        /// <summary>Correct rail placements (through a gap).</summary>
        public readonly int Rail;
        /// <summary>overWall / (overWall + rail); null before the first correct drag placement.</summary>
        public readonly double? Yao;

        public YaoMeasure(int overWall, int rail, double? yao)
        {
            OverWall = overWall;
            Rail = rail;
            Yao = yao;
        }

        public bool Equals(YaoMeasure? o) => o != null && OverWall == o.OverWall && Rail == o.Rail && Nullable.Equals(Yao, o.Yao);
        public override bool Equals(object? obj) => obj is YaoMeasure y && Equals(y);
        public override int GetHashCode() => OverWall * 31 + Rail;
        public override string ToString() => "overWall " + OverWall + " rail " + Rail + " yao " + (Yao.HasValue ? Yao.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null");
    }

    /// <summary>TS `applyMove` and its module-level helpers.</summary>
    public static class Moves
    {
        // --- applyMove ---------------------------------------------------------------------------------------------------------

        /// <summary>`applyMove(state, move, sink, opts)` (TECH §6.1): runs one move; see the file comment.</summary>
        public static MoveResult ApplyMove(GameState s, Move move, IEventSink? sink = null, ApplyOptions? opts = null)
        {
            sink ??= NullSink.Instance;
            opts ??= ApplyOptions.Default;
            var em = new Emitter(sink);
            var hooks = opts.Hooks ?? Registry.LevelHooks(s.Lvl);
            switch (move)
            {
                case DragMove drag:
                    return DragMoveRun(s, drag, em, hooks, opts);
                case AddMovesMove add:
                    return AddMovesMoveRun(s, add, em, hooks, opts);
                case TrowelMove _:
                    em.Emit(new BoosterRejectedEvent(BoosterNames.Trowel, BoosterRejects.LegacyTrowel));
                    return Rejected(BoosterRejects.LegacyTrowel);
                default:
                    return BoosterMoveRun(s, move, em, hooks, opts);
            }
        }

        /// <summary>K-28: the level is won (state flag).</summary>
        public static bool IsLevelWon(GameState s) => (Hdr(s, H.Flags) & StateFlag.Won) != 0;

        private static MoveResult DragMoveRun(GameState s, DragMove move, Emitter em, MoveHooks hooks, ApplyOptions opts)
        {
            int id = move.PieceId;
            var to = move.To;
            // --- step 0: release classification (K-07); an invalid record is a cancel too (TECH §6.1)
            em.Step = 0;
            if (IsLevelWon(s)) return Invalid(em, id, opts, "the level is already won");
            var attempt = Movement.TryBeginDrag(s, id, hooks.Drag);
            if (!attempt.Ok) return Invalid(em, id, opts, "piece " + id + " cannot be picked (" + attempt.Reason + ")");
            var drag = attempt.Session!;
            var drop = drag.Classify(to);
            if (drop.IsCancelled)
            {
                if (drop.Reason == CancelReasons.Invalid)
                    return Invalid(em, id, opts, "node (" + to.Ix + "," + to.Iy + "," + to.Mode + ") is not reachable");
                em.Emit(new MoveCancelledEvent(id, drop.Reason!));
                return new MoveResult(MoveStatuses.Cancelled, drop.Reason, false, false);
            }
            if (move.Via.HasValue && !ViaValid(s, drag, move.Via.Value))
                return Invalid(em, id, opts, "via " + move.Via.Value + " is not a paint gate on the drag's reach");
            if (move.Steer.HasValue && !SteerValid(s, drag, drop, move.Steer.Value))
                return Invalid(em, id, opts, "steer needs light gravity, a 1-wide block and a FREE site release");

            var pre = PreActionOf(s, opts);
            var start = Placement.PiecePlace(s, id);
            int startZone = PieceZone(s, id);
            var startCells = Grid.PieceBoardCells(s, id);
            var scratch = new MoveScratch((PieceFlagBits(s, id) & FlagBit.Stuck) != 0, false, new HashSet<int>());
            var ctx = MakeContext(s, em, scratch);
            var goalsBefore = Goals.GoalSnapshot(s);
            int queueBefore = Hdr(s, H.QueueLen);

            // --- step 1: the block goes to the release node (site blocks move once, in step 2/3)
            em.Step = 1;
            int seg = Grid.VisibleSegment(s);
            bool onSite = drop.Kind != DropKinds.Yard;
            em.Emit(new PieceMovedEvent(
                id,
                Placement.PlaceAt(s, start) ?? At.Yard(start.X, start.Y),
                onSite ? SiteAt(to.Ix, to.Iy, seg) : YardAt(to.Ix, to.Iy),
                drop.Kind == DropKinds.Yard ? EntryKinds.Yard : drop.Kind == DropKinds.SiteFree ? EntryKinds.OverWall : EntryKinds.Gap,
                drop.Kind == DropKinds.SiteRail ? drop.Gap : (int?)null));
            if (!onSite) Placement.MovePiece(s, id, Placement.YardPlace(new Anchor(to.Ix, to.Iy)));
            if (move.Via.HasValue) hooks.OnPassGap?.Invoke(ctx, move.Via.Value, id);

            // --- step 2: fall / balloon / steer on the site (K-11, K-19), S3 glass break; S8 in the yard
            em.Step = 2;
            int glass = 0;
            FallResult? landed = null;
            if (onSite)
            {
                var fall = Gravity.ComputeFall(s, id, to, new FallOptions(move.Steer, hooks.Fall));
                var landingAt = SiteAt(fall.Landing.Ix, fall.Landing.Iy, seg);
                if (fall.Mode == FallResult.ModeFree)
                {
                    if (fall.Drift != 0) em.Emit(new WindDriftEvent(id, fall.Drift));
                    if (fall.Dir < 0)
                        em.Emit(new PieceFellEvent(id, SiteAt(to.Ix, to.Iy, seg), landingAt, fall.Distance, FallCauses.Release));
                    else
                        em.Emit(new BalloonRoseEvent(id, SiteAt(to.Ix, to.Iy, seg), landingAt, fall.Distance));
                    if (fall.Steered.HasValue)
                        em.Emit(new SteeredEvent(id, fall.Steered.Value.AtRow, fall.Steered.Value.Dir));
                }
                if (fall.Effect.Kind == LandingEffect.KindBreak)
                {
                    glass = fall.Effect.Penalty;
                    em.Emit(new GlassBrokeEvent(id, landingAt, glass));
                    var target = Placement.ReturnBrokenPiece(s, id, start);
                    em.Emit(new PieceReturnedEvent(id, landingAt, TargetAt(s, target)));
                    if (Combo.ComboReset(s)) em.Emit(new ComboChangedEvent(0));
                }
                else
                {
                    landed = fall;
                }
            }
            else
            {
                hooks.OnYardRelease?.Invoke(ctx, id);
            }

            // --- step 3: validation on the site (K-16, K-34), lock / stick / bounce (K-14, K-17, Y8), streak (K-33)
            em.Step = 3;
            bool placed = landed != null && PlacementStep(s, em, hooks, id, landed, start, seg);

            // --- step 4: cost (K-07: base + glass penalty, never below 0), m += 1
            em.Step = 4;
            int? rawBase = hooks.MoveCost?.Invoke(ctx, id);
            int baseCost = rawBase ?? 1;
            int left = Hdr(s, H.MovesLeft);
            int delta = -Math.Min(left, baseCost + glass);
            SetHdr(s, H.MovesLeft, left + delta);
            SetHdr(s, H.Turn, Hdr(s, H.Turn) + 1);
            SetHdr(s, H.MovesSpent, Hdr(s, H.MovesSpent) + 1); // K-43 item 2: every non-cancelled drag move
            em.Emit(new MovesChangedEvent(left + delta, delta, MovesChangedReasons.Move, new MoveCost(baseCost >= 2 ? 2 : 1, glass > 0 ? 1 : 0)));
            if (placed) PlacementBonusStep(ctx, em, hooks, id);

            // --- step 5: neighbour effects of the START cells + hidden item check #1 (K-42)
            em.Step = 5;
            if (startZone == Zone.Yard) NeighbourEffects(ctx, hooks, startCells, id);
            hooks.AfterNeighbors?.Invoke(ctx);
            if (placed) hooks.OnCorrectPlacement?.Invoke(ctx, id);
            UncoverCheck(ctx, hooks);

            // --- steps 6–12
            YardGravityStep(ctx, em, hooks);
            em.Step = 7;
            Goals.CountDebrisLeftSite(s, id, startZone);
            EmitGoalChanges(s, em, goalsBefore);
            bool shifted = SegmentStep(s, em);
            bool delivered = DeliveryStep(s, em, queueBefore);
            em.Step = 10;
            RunTimers(ctx, hooks);
            return EndOfMove(ctx, em, hooks, opts, pre, placed || shifted || delivered);
        }

        /// <summary>v3 K-35 step 4 tail (Y12, GDD §16 K-60): the bonus of a correct placement, after the cost (`movesChanged{bonus}`).</summary>
        private static void PlacementBonusStep(RuleContext ctx, Emitter em, MoveHooks hooks, int id)
        {
            int bonus = hooks.PlacementBonus?.Invoke(ctx, id) ?? 0;
            if (bonus <= 0) return;
            var s = ctx.S;
            int left = Hdr(s, H.MovesLeft) + bonus;
            SetHdr(s, H.MovesLeft, left);
            em.Emit(new MovesChangedEvent(left, bonus, MovesChangedReasons.Bonus, null, id));
        }

        /// <summary>K-35 step 3 for a block that landed on the site (fall or rail). Returns true for a correct placement.</summary>
        private static bool PlacementStep(GameState s, Emitter em, MoveHooks hooks, int id, FallResult landed, PiecePlace start, int seg)
        {
            var landingAt = SiteAt(landed.Landing.Ix, landed.Landing.Iy, seg);
            var verdict = landed.Verdict;
            var outcome = Placement.SettlePlacement(s, id, landed.Landing, verdict, new SettleOptions(start, hooks.Placement));
            if (outcome.Kind == PlacementOutcome.KindCorrect)
            {
                bool overWall = landed.Mode == FallResult.ModeFree;
                em.Emit(new PlacementCorrectEvent(id, outcome.Cells!, overWall));
                if (outcome.Revealed!.Length > 0) em.Emit(new CellsRevealedEvent(outcome.At!.Value.Seg, outcome.Revealed));
                // K-46 counters: FREE entries (balloon and G-L included) are "over the wall", rail placements "through a gap"
                int counter = overWall ? H.OverWallCount : H.RailCount;
                SetHdr(s, counter, Hdr(s, counter) + 1);
                var step = Combo.ComboOnCorrect(s);
                em.Emit(new ComboChangedEvent(step.Reached));
                if (step.Earned)
                {
                    em.Emit(new TrowelEarnedEvent(step.Trowels));
                    em.Emit(new ComboChangedEvent(step.Combo));
                }
                return true;
            }
            SetHdr(s, H.WrongCount, Hdr(s, H.WrongCount) + 1);
            em.Emit(new PlacementWrongEvent(id, verdict.Reasons, verdict.MissingSupport));
            if (outcome.Kind == PlacementOutcome.KindStuck)
            {
                em.Emit(new MortarStuckEvent(id, outcome.Reason!, outcome.MissingSupport!));
            }
            else
            {
                em.Emit(new PieceBouncedEvent(id, landingAt, TargetAt(s, outcome.Target!), outcome.Target!.Step == 2, outcome.Reason!, outcome.MissingSupport!));
            }
            if (Combo.ComboReset(s)) em.Emit(new ComboChangedEvent(0));
            return false;
        }

        // --- Golden Trowel (K-33) and +moves ----------------------------------------------------------------------------------

        /// <summary>TS `BOOSTER_OF`: the `BoosterName` of a booster move kind.</summary>
        private static string BoosterOf(Move move)
        {
            switch (move.MoveType)
            {
                case MoveKind.Hammer: return BoosterNames.Hammer;
                case MoveKind.Crane: return BoosterNames.Crane;
                case MoveKind.Paint: return BoosterNames.Paint;
                case MoveKind.GoldTrowel: return BoosterNames.Trowel;
                default: throw new InvalidOperationException("not a booster move: " + move.Kind);
            }
        }

        private static MoveResult Rejected(string reason) => new MoveResult(MoveStatuses.Rejected, reason, false, false);

        /// <summary>
        /// Booster mini pipeline (GDD §10; K-33, K-36…K-38): step 0 precondition + D3a pre-check (a refused target spends
        /// nothing), step 1 the direct effect, then 5 (hidden items), 6, 7, 8, 9, 11, 12. No cost, `m`, `movesSpent`, timers
        /// or streak change; YAO never counts.
        /// </summary>
        private static MoveResult BoosterMoveRun(GameState s, Move move, Emitter em, MoveHooks hooks, ApplyOptions opts)
        {
            em.Step = 0;
            string booster = BoosterOf(move);
            string? refused = BoosterRejection(s, move, hooks);
            if (refused != null)
            {
                em.Emit(new BoosterRejectedEvent(booster, refused));
                return Rejected(refused);
            }
            var pre = PreActionOf(s, opts);
            var ctx = MakeContext(s, em, new MoveScratch(false, false, new HashSet<int>()));
            var goalsBefore = Goals.GoalSnapshot(s);
            int queueBefore = Hdr(s, H.QueueLen);
            em.Step = 1;
            var moved = BoosterEffect(ctx, em, move, hooks);
            // v3: a crane / Golden Trowel placement is a correct placement (K-60 bonus at step 4, K-59 scissors at step 5)
            int placedId = move is GoldTrowelMove gt ? gt.PieceId : move is CraneMove cm && cm.ToZone == At.ZoneSite ? cm.PieceId : -1;
            if (placedId >= 0)
            {
                em.Step = 4;
                PlacementBonusStep(ctx, em, hooks, placedId);
            }
            em.Step = 5;
            if (placedId >= 0) hooks.OnCorrectPlacement?.Invoke(ctx, placedId);
            UncoverCheck(ctx, hooks);
            YardGravityStep(ctx, em, hooks);
            em.Step = 7;
            if (moved.HasValue) Goals.CountDebrisLeftSite(s, moved.Value.Id, moved.Value.StartZone);
            EmitGoalChanges(s, em, goalsBefore);
            SegmentStep(s, em);
            DeliveryStep(s, em, queueBefore);
            return EndOfMove(ctx, em, hooks, opts, pre, true);
        }

        private static string? BoosterRejection(GameState s, Move move, MoveHooks hooks)
        {
            if (IsLevelWon(s)) return BoosterRejects.LevelOver;
            switch (move)
            {
                case HammerMove h:
                    return Boosters.HammerTargetKind(s, h.Target, hooks) == null ? BoosterRejects.NoTarget : null;
                case CraneMove c:
                    return Boosters.CraneRejection(s, c, hooks);
                case PaintMove p:
                    return Boosters.PaintRejection(s, p.A, p.B);
                case GoldTrowelMove g:
                    return Boosters.GoldTrowelRejection(s, g, hooks);
                default:
                    throw new InvalidOperationException("not a booster move: " + move.Kind);
            }
        }

        /// <summary>The piece a booster moved (debris count, step 7): TS `{ id, startZone }`.</summary>
        private readonly struct MovedPiece
        {
            public readonly int Id;
            public readonly int StartZone;

            public MovedPiece(int id, int startZone)
            {
                Id = id;
                StartZone = startZone;
            }
        }

        /// <summary>Step 1 of a booster: the effect and its events. Returns the piece that moved (debris count, step 7).</summary>
        private static MovedPiece? BoosterEffect(RuleContext ctx, Emitter em, Move move, MoveHooks hooks)
        {
            var s = ctx.S;
            switch (move)
            {
                case HammerMove hm:
                    {
                        var target = hm.Target;
                        int startZone = target.PieceId.HasValue ? PieceZone(s, target.PieceId.Value) : Zone.Yard;
                        // the rule's own events (chainReleased, crateBroken …) follow the booster event (K-36 direct hit, step 1)
                        var detail = JsonValue.Object().Set("target", JsonValue.Of(Boosters.HammerTargetKind(s, target, hooks)!));
                        if (target.PieceId.HasValue) detail.Set("pieceId", JsonValue.Of(target.PieceId.Value));
                        if (target.Obstacle.HasValue) detail.Set("obstacle", JsonValue.Of(target.Obstacle.Value));
                        em.Emit(new BoosterAppliedEvent(BoosterNames.Hammer, detail));
                        var hit = Boosters.ApplyHammer(ctx, target, hooks);
                        if (hit.Kind == HammerTargetKinds.Cargo)
                        {
                            em.Emit(new CargoSmashedEvent(hit.PieceId, hit.At!.Value));
                            return null;
                        }
                        if (hit.Kind == HammerTargetKinds.SiteDebris || hit.Kind == HammerTargetKinds.StuckMortar)
                        {
                            em.Emit(new PieceReturnedEvent(hit.PieceId, hit.From!.Value, TargetAt(s, hit.Target!)));
                            return new MovedPiece(hit.PieceId, startZone);
                        }
                        return null;
                    }
                case CraneMove cm:
                    {
                        int startZone = PieceZone(s, cm.PieceId);
                        var fx = Boosters.ApplyCraneEffect(s, cm);
                        em.Emit(new BoosterAppliedEvent(BoosterNames.Crane,
                            JsonValue.Object().Set("pieceId", JsonValue.Of(cm.PieceId)).Set("to", EventJson.AtJson(fx.To)).Set("rotation", JsonValue.Of(cm.Rotation))));
                        em.Emit(new PieceLiftedEvent(cm.PieceId, "crane", fx.From, fx.To, fx.Shape.Id));
                        if (fx.Revealed.Length > 0 && fx.To.Seg.HasValue)
                            em.Emit(new CellsRevealedEvent(fx.To.Seg.Value, fx.Revealed));
                        return new MovedPiece(cm.PieceId, startZone);
                    }
                case PaintMove pm:
                    {
                        Boosters.SwapColors(s, pm.A, pm.B);
                        string aColor = Boosters.ColorCodeOf(PieceColor(s, pm.A));
                        string bColor = Boosters.ColorCodeOf(PieceColor(s, pm.B));
                        em.Emit(new BoosterAppliedEvent(BoosterNames.Paint, JsonValue.Object().Set("a", JsonValue.Of(pm.A)).Set("b", JsonValue.Of(pm.B))));
                        em.Emit(new ColorsSwappedEvent(pm.A, pm.B, aColor, bColor));
                        return null;
                    }
                case GoldTrowelMove gm:
                    {
                        var fx = Boosters.ApplyTrowelEffect(s, gm.PieceId, new Anchor(gm.X, gm.Y));
                        em.Emit(new BoosterAppliedEvent(BoosterNames.Trowel,
                            JsonValue.Object().Set("pieceId", JsonValue.Of(gm.PieceId)).Set("to", EventJson.AtJson(fx.To)).Set("trowels", JsonValue.Of(fx.Trowels))));
                        em.Emit(new PieceLiftedEvent(gm.PieceId, "trowel", fx.From, fx.To, ReadShapeId(s, gm.PieceId)));
                        if (fx.Revealed.Length > 0 && fx.To.Seg.HasValue)
                            em.Emit(new CellsRevealedEvent(fx.To.Seg.Value, fx.Revealed));
                        return null;
                    }
                default:
                    throw new InvalidOperationException("not a booster move: " + move.Kind);
            }
        }

        /// <summary>
        /// `addMoves` (TECH §6.1): an accepted +5 offer (`offerCoins` / `offerAd`, K-29) adds the moves and runs step 12 once
        /// (E-42); `thermos` / `streak` are level-start bonuses (K-40) without step 12. `turn`, timers and the streak never
        /// change.
        /// </summary>
        private static MoveResult AddMovesMoveRun(GameState s, AddMovesMove move, Emitter em, MoveHooks hooks, ApplyOptions opts)
        {
            em.Step = 0;
            if (IsLevelWon(s) || move.Amount <= 0)
            {
                if (opts.Strict) throw new InvalidOperationException("applyMove: invalid addMoves (" + move.Amount + ", won " + (IsLevelWon(s) ? "true" : "false") + ")");
                return new MoveResult(MoveStatuses.Rejected, CancelReasons.Invalid, false, false);
            }
            bool offer = move.Source == AddMovesSource.OfferCoins || move.Source == AddMovesSource.OfferAd;
            em.Step = 1;
            int movesLeft = Hdr(s, H.MovesLeft) + move.Amount;
            SetHdr(s, H.MovesLeft, movesLeft);
            em.Emit(new MovesChangedEvent(movesLeft, move.Amount, offer ? MovesChangedReasons.Offer : MovesChangedReasons.Booster));
            if (offer && !opts.NoTruckHelp)
            {
                em.Step = 12;
                var ctx = MakeContext(s, em, new MoveScratch(false, false, new HashSet<int>()));
                if (hooks.Deadlock != null) hooks.Deadlock(ctx);
                else
                {
                    // K-29 (RulesVersion 3): a D1 Söküm goes back to the start of the last action before the window
                    var cause = Deadlock.RunStep12(ctx, hooks, new Step12Options(opts.PreAction, false, movesLeft <= 0, opts.DeadTable, true));
                    if (cause != null) return new MoveResult(MoveStatuses.Applied, null, false, false, cause);
                }
            }
            return MoveResult.AppliedResult;
        }

        // --- shared steps ------------------------------------------------------------------------------------------------------

        private static RuleContext MakeContext(GameState s, Emitter em, MoveScratch scratch) =>
            new RuleContext(s, em.Emit, new BufferRng(s.Buf, H.Rng), scratch);

        /// <summary>K-35 step 6: yard gravity loop (bags always, Y6 blocks and balloons) + hidden item check #2.</summary>
        private static void YardGravityStep(RuleContext ctx, Emitter em, MoveHooks hooks)
        {
            var s = ctx.S;
            em.Step = 6;
            var onNeighborMoved = hooks.OnNeighborMoved;
            var motions = Gravity.SettleYard(s, onNeighborMoved != null
                ? new SettleHooks((_, fallen) =>
                {
                    bool freed = false;
                    foreach (var f in fallen) if (NeighbourEffects(ctx, hooks, f.Cells, f.PieceId)) freed = true;
                    return freed;
                })
                : null);
            foreach (var m in motions)
            {
                if (m.Kind != YardMotion.KindPiece) continue; // a falling bag has no event of its own yet (Phase 3, Y2)
                var from = YardAt(m.From.Ix, m.From.Iy);
                var to = YardAt(m.To.Ix, m.To.Iy);
                if (m.Dy < 0) em.Emit(new PieceFellEvent(m.Id, from, to, -m.Dy, FallCauses.YardGravity));
                else em.Emit(new BalloonRoseEvent(m.Id, from, to, m.Dy));
            }
            UncoverCheck(ctx, hooks);
        }

        /// <summary>
        /// K-35 step 5 rule (also used for the falls of step 6): every yard entity next to `cells` (4-neighbourhood inside the
        /// yard, never across the wall boundary — `Neighbors4`, E-46), in (y, x) order of the neighbour cells, at most once per
        /// move. Returns true when a yard cell was freed (bag torn, crate gone: gravity runs again, E-12).
        /// </summary>
        private static bool NeighbourEffects(RuleContext ctx, MoveHooks hooks, BoardCell[] cells, int moved)
        {
            var hook = hooks.OnNeighborMoved;
            if (hook == null) return false;
            var around = new List<BoardCell>();
            var geo = ctx.S.Lvl.Geo;
            foreach (var c in cells)
            {
                if (c.X >= geo.Wy || c.Y >= geo.Hy) continue;
                foreach (var n in Coords.Neighbors4(geo, c.X, c.Y)) around.Add(new BoardCell(n.Ix, n.Iy));
            }
            StableSort(around, (a, b) => a.Y != b.Y ? a.Y - b.Y : a.X - b.X);
            bool freed = false;
            foreach (var n in around)
            {
                int v = YardOcc(ctx.S, n.X, n.Y);
                if (v == 0 || v == moved + 1 || ctx.Scratch.Affected.Contains(v)) continue;
                var entity = v > 0 ? EntityRef.Piece(v - 1) : EntityRef.Obstacle(-v - 1);
                string effect = hook(ctx, entity, moved);
                if (effect != NeighborEffects.None) ctx.Scratch.Affected.Add(v);
                if (effect == NeighborEffects.Freed) freed = true;
            }
            return freed;
        }

        /// <summary>Hidden item checks #1 / #2 (K-42): every uncollected screw / key whose yard cell is empty, in (y, x) order.</summary>
        private static void UncoverCheck(RuleContext ctx, MoveHooks hooks)
        {
            var hook = hooks.OnCellUncovered;
            if (hook == null) return;
            var lvl = ctx.Lvl;
            var items = new List<CompiledObstacle>();
            foreach (int i in lvl.HiddenItems)
            {
                if (i < 0 || i >= lvl.Obstacles.Length) continue;
                var o = lvl.Obstacles[i];
                if (o.Type == "screw" || o.Type == "key") items.Add(o);
            }
            StableSort(items, (a, b) => a.Y != b.Y ? a.Y - b.Y : a.X - b.X);
            foreach (var o in items)
            {
                if (!HiddenCollected(ctx.S, o.HiddenIndex) && YardOcc(ctx.S, o.X, o.Y) == 0) hook(ctx, o.Index);
            }
        }

        /// <summary>`Array.prototype.sort` is stable: insertion sort keeps equal elements in order.</summary>
        private static void StableSort<T>(List<T> list, Comparison<T> cmp)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var x = list[i];
                int j = i - 1;
                while (j >= 0 && cmp(list[j], x) > 0)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = x;
            }
        }

        /// <summary>K-35 step 7: `goalProgress` for the clear / collect goals that changed in this move.</summary>
        private static void EmitGoalChanges(GameState s, Emitter em, int[] before)
        {
            foreach (int i in Goals.ChangedCountGoals(s, before))
                em.Emit(new GoalProgressEvent(i, GoalValue(s, i), Goals.GoalTarget(s.Lvl, i)));
        }

        /// <summary>K-35 step 8 (K-22): segment completion through the site strategy; the next batch joins the queue (K-25).</summary>
        private static bool SegmentStep(GameState s, Emitter em)
        {
            em.Step = 8;
            var done = Site.SiteStrategy(s.Lvl).CompleteIfDone(s);
            if (done == null) return false;
            em.Emit(new SegmentCompletedEvent(done.Seg));
            foreach (int i in Goals.SetBuildProgress(s, done.Completed))
                em.Emit(new GoalProgressEvent(i, GoalValue(s, i), Goals.GoalTarget(s.Lvl, i)));
            if (done.ShiftedTo.HasValue) em.Emit(new SiteShiftedEvent(done.ShiftedTo.Value));
            return true;
        }

        /// <summary>K-35 step 9 (K-25, K-26): the single delivery point; "Kamyonda: N" when N changed during the move.</summary>
        private static bool DeliveryStep(GameState s, Emitter em, int queueBefore)
        {
            em.Step = 9;
            var res = Delivery.DeliverQueue(s);
            if (res.Delivered.Length > 0)
            {
                var ids = new int[res.Delivered.Length];
                for (int i = 0; i < ids.Length; i++) ids[i] = res.Delivered[i].PieceId;
                em.Emit(new DeliveryArrivedEvent(Grid.VisibleSegment(s), ids));
                foreach (var d in res.Delivered)
                    em.Emit(new PieceFellEvent(d.PieceId, YardAt(d.From.Ix, d.From.Iy), YardAt(d.To.Ix, d.To.Iy), d.Rows, FallCauses.Delivery));
            }
            if (res.Queued != queueBefore) em.Emit(new DeliveryQueuedEvent(res.Queued));
            return res.Delivered.Length > 0;
        }

        /// <summary>
        /// K-35 step 10: one timer hook per `lvl.Step10` entry, in that order (W4 → W5 → S5 → S6 → Y4 → K-40, TECH §7.3). The
        /// registry binds rule `onMoveEnd` hooks and the site strategy's S5 / S6 ticks (K-23 skip included).
        /// </summary>
        private static void RunTimers(RuleContext ctx, MoveHooks hooks)
        {
            foreach (var t in ctx.Lvl.Step10)
            {
                Action<RuleContext>? run = null;
                if (hooks.Timers != null) hooks.Timers.TryGetValue(t.Id, out run);
                if (run == null) throw new NotImplementedException("Phase 3: step-10 timer " + t.Id + " has no rule hook yet (TECH_DESIGN §7.3)");
                run(ctx);
            }
        }

        /// <summary>
        /// K-35 steps 11–12: win (K-28, K-48) before out of moves (K-29); while the level is not won step 12 always runs (Faz
        /// 2R: at 0 moves too, before the out-of-moves window) — D1 / D2 / D3 and the Söküm of Deadlock.cs. `pre` is the
        /// Söküm target (null when step 12 is off); `tiling`: the action placed correctly, shifted the site, delivered, or was
        /// a booster / trowel (D3a runs).
        /// </summary>
        private static MoveResult EndOfMove(RuleContext ctx, Emitter em, MoveHooks hooks, ApplyOptions opts, int[]? pre, bool tiling)
        {
            var s = ctx.S;
            em.Step = 11;
            int movesLeft = Hdr(s, H.MovesLeft);
            if (Goals.LevelGoalsMet(s))
            {
                SetHdr(s, H.Flags, Hdr(s, H.Flags) | StateFlag.Won);
                em.Emit(new LevelWonEvent(movesLeft));
                return new MoveResult(MoveStatuses.Applied, null, true, false);
            }
            bool outOfMoves = movesLeft <= 0;
            if (outOfMoves) em.Emit(new OutOfMovesEvent());
            em.Step = 12;
            string? teardown = null;
            if (!opts.NoTruckHelp)
            {
                if (hooks.Deadlock != null) hooks.Deadlock(ctx);
                else teardown = Deadlock.RunStep12(ctx, hooks, new Step12Options(pre, tiling, outOfMoves, opts.DeadTable));
            }
            if (teardown != null) return new MoveResult(MoveStatuses.Applied, null, false, outOfMoves, teardown);
            return outOfMoves ? new MoveResult(MoveStatuses.Applied, null, false, true) : MoveResult.AppliedResult;
        }

        /// <summary>The action's start buffer for a Söküm: the caller's copy, else a fresh one; null when step 12 is off.</summary>
        private static int[]? PreActionOf(GameState s, ApplyOptions opts)
        {
            if (opts.NoTruckHelp) return null;
            return opts.PreAction ?? (int[])s.Buf.Clone();
        }

        private static string ReadShapeId(GameState s, int id) => Shapes.ShapeByIndex(PieceShape(s, id)).Id;

        private static MoveResult Invalid(Emitter em, int pieceId, ApplyOptions opts, string why)
        {
            if (opts.Strict) throw new InvalidOperationException("applyMove: invalid drag of piece " + pieceId + ": " + why);
            em.Emit(new MoveCancelledEvent(pieceId, CancelReasons.Invalid));
            return new MoveResult(MoveStatuses.Cancelled, CancelReasons.Invalid, false, false);
        }

        /// <summary>W6 `via`: a paint gate whose rail lies on the drag's reach (every reachable node is on some path, edges are 2-way).</summary>
        private static bool ViaValid(GameState s, DragSession drag, int via)
        {
            if (via < 0 || via >= s.Lvl.Gaps.Length) return false;
            var gap = s.Lvl.Gaps[via];
            if (gap.Type != "paint") return false;
            int mode = Movement.RailMode(via);
            foreach (var n in drag.ReachableNodes()) if (n.Mode == mode) return true;
            return false;
        }

        /// <summary>K-19 `steer`: only on light gravity (G-L), for a 1-wide block released FREE over the site.</summary>
        private static bool SteerValid(GameState s, DragSession drag, DropClass drop, Steer steer) =>
            s.Lvl.Gravity.Steerable && drop.Kind == DropKinds.SiteFree && drag.Shape.W == 1 && (steer.Dir == 1 || steer.Dir == -1);

        private static At SiteAt(int ix, int iy, int seg) => At.Site(ix, iy, seg);

        private static At YardAt(int ix, int iy) => At.Yard(ix, iy);

        /// <summary>TS `targetAt`: `placeAt(s, target.to) ?? 'queue'` (null = queue).</summary>
        private static At? TargetAt(GameState s, ReturnTarget target) => Placement.PlaceAt(s, target.To);

        // --- K-46 YAO -----------------------------------------------------------------------------------------------------------

        /// <summary>K-46: the player's YAO so far (trowel, crane and truck help never count; `level_end` analytics).</summary>
        public static YaoMeasure MeasureYao(GameState s)
        {
            int overWall = Hdr(s, H.OverWallCount);
            int rail = Hdr(s, H.RailCount);
            int total = overWall + rail;
            return new YaoMeasure(overWall, rail, total == 0 ? (double?)null : (double)overWall / total);
        }

        // --- canonical JSON and FNV-1a (event log hash, level hash) ---------------------------------------------------------------

        /// <summary>JSON with object keys sorted (recursively): one text per value (TS `canonicalJson`; `undefined` has no JsonValue, so nothing to drop).</summary>
        public static string CanonicalJson(JsonValue value) => JsonWriter.Canonical(value);

        /// <summary>FNV-1a 64 over the UTF-8 bytes of `text`, as 16 lowercase hex digits (TS `fnv1a64`).</summary>
        public static string Fnv1a64(string text) => Fnv1a.Hash64Hex(text);

        /// <summary>`eventLogHash` (TECH §6.3): FNV-1a 64 of the canonical JSON of the events (golden tests, §9.5).</summary>
        public static string EventLogHash(IEnumerable<GameEvent> events) => Fnv1a64(CanonicalJson(EventJson.ToJson(events)));
    }
}
