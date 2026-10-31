#nullable enable
// Action → animation schedule (Faz 3 Q0). Port of web src/scenes/level/juice/plan.ts `planMove` (TECH §6.3 "Sahne
// tarafı"; JUICE §0 rule 10 "Oynatma sırası = K-35 adımı"), extended to every one of the 43 event types: the web plays
// only the Faz 2 P0 rows; here every Phase 3 event (obstacles, timers, boosters, truck help) gets its JUICE row, and an
// event without an animation becomes an `Instant` step — no event is ever dropped.
//
// Pure: the core has already decided everything (TECH §1.4); the plan only times the package. The game state is final the
// moment `GameSession.Apply` returns — logic never waits for animation, and the view never changes logic.
//
// Order (JUICE §0 rule 10): step 0 cancel (#8) · step 1 release (#9 yard drop / #23 rail park), trowel (#17), crane,
// hammer, brush, +5 (#53) · step 2 fall (#10) → landing (#11), balloon, glass · step 3 validation (#12 correct + #83 build
// front + the D-091 blocks-left chip flight / #13 wrong → #88 into the truck chip, #84 missing support; #15–16 streak) ·
// step 4 counter (#50, #51) — steps 1–4 one after the other · step 5 neighbour effects together · step 6 yard cascade
// staggered by `YardCascadeStaggerMs` · step 7 goal counters (HUD, own pace) · step 8 segment slide (#18, LOCKED) → step 9
// truck (#19, LOCKED; longer drops land unlocked) and queue chip (#20) · step 10 timers together · step 11 win (#94 →
// #55 → #56, LOCKED) · step 12 truck help (#21a / #21c) and Söküm (#107), LOCKED · out of moves (#57) LAST — JUICE §0
// rule 10 "kazanma ya da kaybetme en son" (the web plan.ts plays #57 before #107; see the Faz 3 Q0 report) · `End`.
// JUICE §0 rule 14: a package that ends in a Söküm plays no correct-placement reward (#12, #15, #16, #83, the chip flight
// become `Instant`).
//
// Durations come from `FeelSettings`; falls from its physics values (`FallMotion`), never ms per row.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Animation
{
    /// <summary>What the planner needs besides the events: values from BEFORE the action (web `PlanContext`).</summary>
    public sealed class PlanContext
    {
        /// <summary>"Animasyonları azalt" (JUICE §0 rule 8): fades instead of slides; rule durations stay.</summary>
        public bool Reduced;
        /// <summary>Level gravity profile (`CompiledLevel.Gravity.Build`: low | normal | high; JUICE §0.1).</summary>
        public string Gravity = "normal";
        /// <summary>Pieces in the truck queue before the action (K-26: they leave through the chip, oldest first).</summary>
        public int[] QueuedBefore = new int[0];
        /// <summary>Moves left before the action (#51 threshold crossing).</summary>
        public int MovesBefore;
        /// <summary>Box height (rows) of a piece, for the K-17 drop above the yard; null = 1.</summary>
        public Func<int, int>? PieceHeight;
        /// <summary>Rows of the level grid incl. the crane area (`geo.Rows`; default board 10).</summary>
        public int GridRows = Geometry.DefaultGeo.Rows;
        /// <summary>Blocks left AFTER the action (`TurnSummary.BlocksLeft`, K-48): #51 follows UX §5.1 Faz 2R; null = the Faz 2 "last 5 moves" rule.</summary>
        public int? BlocksLeft;
        /// <summary>economy.json `bonusMaxMovesCounted` (META): #56 animates at most this many moves.</summary>
        public int BonusMaxMoves = 10;

        /// <summary>The context of the next action of `session`, read before `Apply` (set `BlocksLeft` from the result).</summary>
        public static PlanContext Capture(GameSession session, bool reduced = false)
        {
            var s = session.State;
            int n = s.Lvl.Layout.Counts.Pieces;
            var heights = new int[n];
            for (int i = 0; i < n; i++) heights[i] = Shapes.ShapeByIndex(State.PieceShape(s, i)).H;
            return new PlanContext
            {
                Reduced = reduced,
                Gravity = session.Lvl.Gravity.Build,
                QueuedBefore = State.QueueIds(s),
                MovesBefore = session.MovesLeft,
                PieceHeight = id => id >= 0 && id < heights.Length ? heights[id] : 1,
                GridRows = session.Lvl.Geo.Rows,
            };
        }
    }

    public static class AnimationPlanner
    {
        /// <summary>UX §5.1 Faz 2R / DL-2R-19 last-moves warning: `movesLeft − blocksLeft ≤ 1` or `movesLeft ≤ 2` (web ui/remaining.ts).</summary>
        public static bool LowMovesWarning(int movesLeft, int blocksLeft) => movesLeft - blocksLeft <= 1 || movesLeft <= 2;

        /// <summary>The Faz 2 trigger when the blocks left are unknown: the last 5 moves (web JUICE_VIEW.lastMovesAt).</summary>
        public const int LastMovesAt = 5;

        /// <summary>The plan of an applied action package (`GameSession.Apply`); `BlocksLeft` comes from its summary.</summary>
        public static AnimationPlan Plan(ActionResult result, PlanContext before, FeelSettings feel)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            before.BlocksLeft = result.Summary.BlocksLeft;
            return Plan(result.Events, before, feel);
        }

        /// <summary>The plan of one action's events (a whole package: TS `ActionResult.events`).</summary>
        public static AnimationPlan Plan(IReadOnlyList<GameEvent> events, PlanContext ctx, FeelSettings feel)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (feel == null) throw new ArgumentNullException(nameof(feel));
            return new Builder(events, ctx, feel).Build();
        }

        // --- durations of the JUICE P0 rows in both motion modes (web juice/catalog.ts `juiceMs`) ----------------------------

        internal static float JuiceMs(int id, bool reduced, FeelSettings f)
        {
            switch (id)
            {
                case 8: return reduced ? f.ReducedFadeMs : f.CancelMs;
                case 9: return f.YardDropMs;
                case 10: return 0;
                case 11: return f.LandMs;
                case 12: return reduced ? f.ReducedFadeMs : f.PlaceOkMs;
                case 13: return reduced ? f.CancelMs : f.PlaceBadMs;
                case 15: return f.StreakPipMs;
                case 16: return f.ComboMs;
                case 17: return reduced ? f.ReducedFadeMs : f.GoldTrowelMs;
                case 18: return reduced ? f.ReducedFadeMs : f.SegmentMs;
                case 19: return f.TruckMs;
                case 20: return f.QueueChipMs;
                case 23: return f.RailClampMs;
                case 50: return reduced ? 0 : f.MovesTickMs;
                case 51: return reduced ? 0 : f.LastMovesPulseMs;
                case 53: return reduced ? f.ReducedFadeMs : f.MovesAddMs;
                case 55: return f.WinMs;
                case 56: return reduced ? f.BonusReducedMs : f.BonusPerMoveMs;
                case 57: return reduced ? f.ReducedFadeMs : f.OutOfMovesMs;
                case 83: return reduced ? 0 : f.FrontShiftMs;
                case 84: return f.SupportFlashMs;
                case 88: return reduced ? f.ReducedFadeMs : f.BounceToQueueMs;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "not a planned JUICE P0 row");
            }
        }

        /// <summary>JUICE #13 bounce flight after the wrong flash (web plan.ts `bounceMs`): arc to the target, or above it and down (K-17 step 2).</summary>
        public static float BounceMs(bool reduced, bool viaDrop, int rows, FeelSettings f)
        {
            float flight = reduced ? JuiceMs(13, true, f) : JuiceMs(13, false, f) - f.WrongShakeMs;
            return flight + (viaDrop ? FallMotion.YardFallMs(rows, f) : 0);
        }

        /// <summary>#56: animated bonus moves (≤ `BonusMaxMoves`, ≤ `BonusMaxMs`; web plan.ts `bonusMs`).</summary>
        public static float BonusMs(int movesLeft, int maxCounted, bool reduced, FeelSettings f)
        {
            if (movesLeft <= 0) return 0;
            if (reduced) return f.BonusReducedMs;
            return Math.Min(f.BonusMaxMs, Math.Min(movesLeft, maxCounted) * f.BonusPerMoveMs);
        }

        private sealed class Builder
        {
            private readonly IReadOnlyList<GameEvent> events;
            private readonly PlanContext ctx;
            private readonly FeelSettings f;
            private readonly bool r;
            private readonly List<AnimationStep> steps = new List<AnimationStep>();
            /// <summary>Start times of the beats (the cursor positions); a step belongs to the beat in which it starts.</summary>
            private readonly List<float> beatStarts = new List<float> { 0f };
            private readonly HashSet<GameEvent> used = new HashSet<GameEvent>(EventIdentity.Instance);
            private float t;
            private float end;

            public Builder(IReadOnlyList<GameEvent> events, PlanContext ctx, FeelSettings feel)
            {
                this.events = events;
                this.ctx = ctx;
                f = feel;
                r = ctx.Reduced;
            }

            // --- helpers ---------------------------------------------------------------------------------------------------

            private AnimationStep Add(StepKind kind, int juice, float at, float ms, EaseType ease, GameEvent? ev = null, bool lockInput = false,
                bool fastForward = false, int pieceId = -1, At[]? cells = null, int? n = null, bool first = false, bool flag = false,
                int? toSeg = null, Drop[]? drops = null, GameEvent[]? also = null)
            {
                var step = new AnimationStep(kind, juice, at, ms, ease, lockInput, fastForward, ev, also, pieceId, cells, n, first, flag, toSeg, drops);
                steps.Add(step);
                if (ev != null) used.Add(ev);
                if (also != null) foreach (var e in also) used.Add(e);
                end = Math.Max(end, at + ms);
                return step;
            }

            /// <summary>Moves the beat cursor; the next steps start a new group.</summary>
            private void Advance(float ms)
            {
                if (ms <= 0) return;
                t += ms;
                beatStarts.Add(t);
            }

            private void AdvanceTo(float at)
            {
                if (at > t) Advance(at - t);
            }

            private List<T> Of<T>(int step = -1) where T : GameEvent
            {
                var o = new List<T>();
                foreach (var e in events)
                    if (e is T x && (step < 0 || e.Step == step)) o.Add(x);
                return o;
            }

            private bool Used(GameEvent e) => used.Contains(e);

            private float Ms(int juice) => JuiceMs(juice, r, f);

            private float Reduce(float ms) => r ? Math.Min(ms, f.ReducedFadeMs) : ms;

            private int HeightOf(int id) => ctx.PieceHeight != null ? ctx.PieceHeight(id) : 1;

            // --- the plan --------------------------------------------------------------------------------------------------

            public AnimationPlan Build()
            {
                Step0();
                Step1();
                Step2();
                Step3();
                Step4();
                Leftovers(5, true);
                Step6();
                Leftovers(7, false);
                Step8();
                Step9();
                Leftovers(10, true);
                Step11();
                Step12();
                LevelEndLoss();
                // events of steps the pipeline does not use (safety net): Instant at the end, nothing is dropped
                foreach (var e in events)
                    if (!Used(e)) Add(StepKind.Instant, 0, t, 0, EaseType.Linear, e);
                float last = Math.Max(t, end);
                AdvanceTo(last);
                Add(StepKind.End, 0, last, 0, EaseType.Linear);
                SuppressRewardsOnTeardown();
                return Finish(last);
            }

            private void Step0()
            {
                float start = t;
                float span = 0;
                foreach (var e in Of<MoveCancelledEvent>(0))
                {
                    float ms = Ms(8);
                    Add(StepKind.Cancel, 8, start, ms, r ? EaseType.Linear : f.CancelEase, e, fastForward: true, pieceId: e.PieceId,
                        flag: e.Reason == CancelReasons.SameSpot);
                    span = Math.Max(span, ms);
                }
                foreach (var e in Of<BoosterRejectedEvent>(0))
                {
                    float ms = Reduce(f.BlockedShakeMs);
                    Add(StepKind.BoosterRejected, 108, start, ms, f.BlockedShakeEase, e);
                    span = Math.Max(span, ms);
                }
                Advance(span);
                Leftovers(0, true, start);
            }

            private void Step1()
            {
                float start = t;
                foreach (var e in Of<PieceMovedEvent>(1))
                {
                    if (e.Entry == EntryKinds.Yard)
                    {
                        Add(StepKind.YardDrop, 9, t, Ms(9), f.YardDropEase, e, fastForward: true, pieceId: e.PieceId);
                        Advance(Ms(9));
                    }
                    else if (e.Entry == EntryKinds.Gap)
                    {
                        Add(StepKind.RailClamp, 23, t, Ms(23), f.RailClampEase, e, fastForward: true, pieceId: e.PieceId);
                        Advance(Ms(23));
                    }
                    else
                    {
                        // over the wall: the view is already at the release node (the drag drew it there); the fall follows
                        Add(StepKind.Instant, 0, t, 0, EaseType.Linear, e, pieceId: e.PieceId);
                    }
                }
                foreach (var e in Of<PiecePaintedEvent>(1))
                    Add(StepKind.PiecePaint, 0, start, Reduce(f.PaintGateMs), EaseType.InOutSine, e, fastForward: true, pieceId: e.PieceId);
                // K-33 Golden Trowel: the flight shows the trowel's pieceLifted (from, to, shape); boosterApplied rides along
                var lifts = Of<PieceLiftedEvent>(1);
                foreach (var e in Of<BoosterAppliedEvent>(1))
                {
                    if (e.Booster != BoosterNames.Trowel) continue;
                    PieceLiftedEvent? lift = null;
                    foreach (var l in lifts)
                        if (l.By == "trowel" && !Used(l))
                        {
                            lift = l;
                            break;
                        }
                    float ms = Ms(17);
                    Add(StepKind.TrowelFlight, 17, t, ms, r ? EaseType.Linear : f.GoldTrowelEase, (GameEvent?)lift ?? e, fastForward: true,
                        pieceId: lift?.PieceId ?? -1, also: lift != null ? new GameEvent[] { e } : null);
                    Advance(ms);
                }
                foreach (var e in lifts)
                {
                    if (e.By != "crane" || Used(e)) continue;
                    float fly = r ? f.ReducedFadeMs : f.CraneBoosterMs;
                    Add(StepKind.CraneLift, 62, t, fly, EaseType.InOutSine, e, fastForward: true, pieceId: e.PieceId);
                    Advance(fly);
                }
                foreach (var e in Of<CargoSmashedEvent>(1))
                {
                    float hit = r ? f.ReducedFadeMs : f.HammerMs;
                    Add(StepKind.CargoSmash, 61, t, hit, EaseType.OutExpo, e, fastForward: true, pieceId: e.PieceId);
                    Advance(hit);
                }
                foreach (var e in Of<PieceReturnedEvent>(1))
                {
                    Add(StepKind.PieceReturn, 61, t, f.PieceReturnMs, f.BounceEase, e, fastForward: true, pieceId: e.PieceId);
                    Advance(f.PieceReturnMs);
                }
                foreach (var e in Of<ColorsSwappedEvent>(1))
                {
                    float ms = r ? f.ReducedFadeMs : f.BrushMs;
                    Add(StepKind.Brush, 63, t, ms, EaseType.InOutSine, e, fastForward: true, pieceId: e.A);
                    Advance(ms);
                }
                foreach (var e in Of<MovesChangedEvent>())
                {
                    if (e.Reason != MovesChangedReasons.Offer) continue;
                    Add(StepKind.MovesAdded, 53, t, Ms(53), f.MovesAddEase, e, n: e.MovesLeft);
                    Advance(Ms(53));
                }
                Leftovers(1, true, start);
            }

            private void Step2()
            {
                float start = t;
                float fallAt = t;
                foreach (var e in events)
                {
                    if (e.Step != 2) continue;
                    switch (e)
                    {
                        case WindDriftEvent w:
                            {
                                float ms = Reduce(f.WindDriftMs);
                                Add(StepKind.WindDrift, 0, t, ms, EaseType.OutQuad, w, fastForward: true, pieceId: w.PieceId);
                                Advance(ms);
                                break;
                            }
                        case PieceFellEvent fell when fell.Cause == FallCauses.Release:
                            {
                                float fall = FallMotion.ReleaseFallMs(fell.Rows, ctx.Gravity, f);
                                fallAt = t;
                                Add(StepKind.SiteFall, 10, t, fall, EaseType.Linear, fell, fastForward: true, pieceId: fell.PieceId, n: fell.Rows);
                                Add(StepKind.Landing, 11, t + fall, Ms(11), f.LandEase, null, fastForward: true, pieceId: fell.PieceId, n: fell.Rows);
                                Advance(fall);
                                break;
                            }
                        case BalloonRoseEvent b:
                            {
                                float rise = (float)FallMotion.GlideMs(b.Rows, f.BalloonRiseSpeed);
                                fallAt = t;
                                Add(StepKind.BalloonGlide, 43, t, rise, EaseType.OutSine, b, fastForward: true, pieceId: b.PieceId, n: b.Rows);
                                Advance(rise);
                                break;
                            }
                        case SteeredEvent st:
                            // K-19: the steer happens during the fall that was just scheduled
                            Add(StepKind.Steer, 46, fallAt, f.SteerMs, EaseType.OutQuad, st, fastForward: true, pieceId: st.PieceId);
                            break;
                        case GlassBrokeEvent g:
                            Add(StepKind.GlassBreak, 42, t, Reduce(f.GlassBreakMs), EaseType.OutExpo, g, fastForward: true, pieceId: g.PieceId);
                            break;
                        case PieceReturnedEvent back:
                            Add(StepKind.PieceReturn, 42, t, f.PieceReturnMs, f.BounceEase, back, fastForward: true, pieceId: back.PieceId);
                            Advance(f.PieceReturnMs);
                            break;
                        case ComboChangedEvent c when c.Combo == 0:
                            Add(StepKind.StreakReset, 0, t, 0, EaseType.Linear, c);
                            break;
                    }
                }
                Leftovers(2, true, start);
            }

            private void Step3()
            {
                float start = t;
                float span = 0;
                ComboChangedEvent? reached = null;
                foreach (var e in Of<ComboChangedEvent>(3))
                    if (e.Combo > 0)
                    {
                        reached = e;
                        break;
                    }
                var earnedList = Of<TrowelEarnedEvent>(3);
                var earned = earnedList.Count > 0 ? earnedList[0] : null;
                foreach (var e in Of<PlacementCorrectEvent>(3))
                {
                    Add(StepKind.PlaceCorrect, 12, t, Ms(12), r ? EaseType.Linear : f.PlaceOkEase, e, fastForward: true, pieceId: e.PieceId, cells: e.Cells,
                        n: reached?.Combo ?? 1);
                    // D-091 / UX §5.9: the blocks-left chip counts the placement; a block icon flies there (HUD, own pace)
                    Add(StepKind.BlocksChipFly, 0, t, r ? f.ReducedFadeMs : f.BlocksChipFlyMs, f.BlocksChipFlyEase, null, pieceId: e.PieceId,
                        n: ctx.BlocksLeft);
                    Add(StepKind.BuildFront, 83, t + Ms(12), Ms(83), f.FrontShiftEase, null, fastForward: true, pieceId: e.PieceId, cells: e.Cells);
                    span = Math.Max(span, Ms(12));
                }
                foreach (var e in Of<CellsRevealedEvent>(3))
                    Add(StepKind.CellsReveal, 49, t + Ms(12), Reduce(f.RevealMs), EaseType.OutBack, e);
                if (reached != null)
                {
                    Add(StepKind.StreakPip, 15, t, Ms(15), EaseType.OutBack, reached, n: reached.Combo);
                    if (earned != null)
                    {
                        // the bead row empties into the trowel: the combo reset after `trowelEarned` belongs to #16
                        var resets = new List<GameEvent>();
                        foreach (var c in Of<ComboChangedEvent>(3))
                            if (!ReferenceEquals(c, reached) && c.Seq > earned.Seq) resets.Add(c);
                        Add(StepKind.TrowelEarned, 16, t + Ms(15), Ms(16), f.ComboEase, earned, n: earned.Trowels, also: resets.ToArray());
                    }
                }
                var wrongs = Of<PlacementWrongEvent>(3);
                foreach (var e in Of<PieceBouncedEvent>(3))
                {
                    bool toQueue = !e.To.HasValue;
                    int rows = toQueue ? 0 : FallMotion.DropRows(e.To!.Value.Y, HeightOf(e.PieceId), ctx.GridRows);
                    float flight = toQueue ? 0 : BounceMs(r, e.ViaDrop, rows, f);
                    float wrongMs = (r ? 0 : f.WrongShakeMs) + flight;
                    Add(StepKind.PlaceWrong, 13, t, wrongMs, f.BounceEase, e, fastForward: true, pieceId: e.PieceId, n: rows, flag: toQueue,
                        also: WrongOf(wrongs, e.PieceId));
                    float done = t + wrongMs;
                    if (toQueue)
                    {
                        float q = r ? 0 : f.WrongShakeMs;
                        Add(StepKind.BounceToQueue, 88, t + q, Ms(88), f.BounceToQueueEase, null, fastForward: true, pieceId: e.PieceId);
                        done = t + q + Ms(88);
                    }
                    if (e.MissingSupport.Length > 0) Add(StepKind.SupportFlash, 84, done, Ms(84), f.SupportFlashEase, null, cells: e.MissingSupport);
                    span = Math.Max(span, done - t);
                }
                foreach (var e in Of<MortarStuckEvent>(3))
                {
                    Add(StepKind.MortarStuck, 14, t, Reduce(f.MortarStickMs), f.MortarStickEase, e, fastForward: true, pieceId: e.PieceId, also: WrongOf(wrongs, e.PieceId));
                    if (e.MissingSupport.Length > 0) Add(StepKind.SupportFlash, 84, t, Ms(84), f.SupportFlashEase, null, cells: e.MissingSupport);
                    Add(StepKind.Resync, 0, t, 0, EaseType.Linear);
                }
                foreach (var e in Of<ComboChangedEvent>(3))
                    if (e.Combo == 0 && earned == null && !Used(e)) Add(StepKind.StreakReset, 0, t, 0, EaseType.Linear, e);
                Advance(span);
                Leftovers(3, true, start);
            }

            private GameEvent[]? WrongOf(List<PlacementWrongEvent> wrongs, int pieceId)
            {
                foreach (var w in wrongs)
                    if (w.PieceId == pieceId && !Used(w)) return new GameEvent[] { w };
                return null;
            }

            private void Step4()
            {
                float start = t;
                foreach (var e in Of<MovesChangedEvent>(4))
                {
                    if (e.Reason != MovesChangedReasons.Move) continue;
                    Add(StepKind.MovesTick, 50, t, Ms(50), f.MovesTickEase, e, n: e.MovesLeft);
                    if (Warn(e.MovesLeft))
                        Add(StepKind.LastMoves, 51, t, Ms(51), EaseType.InOutSine, null, n: e.MovesLeft, first: !Warn(ctx.MovesBefore));
                    Advance(Ms(50));
                }
                Leftovers(4, false, start);
            }

            private bool Warn(int moves) => ctx.BlocksLeft.HasValue ? LowMovesWarning(moves, ctx.BlocksLeft.Value) : moves <= LastMovesAt;

            private void Step6()
            {
                float start = t;
                // K-20 yard cascade (falls and Y6 balloon rises), column by column (x, then y)
                var cascade = new List<GameEvent>();
                foreach (var e in events)
                {
                    if (e.Step != 6) continue;
                    if (e is PieceFellEvent fe && fe.Cause == FallCauses.YardGravity) cascade.Add(e);
                    else if (e is BalloonRoseEvent) cascade.Add(e);
                }
                cascade.Sort((a, b) =>
                {
                    var fa = FromOf(a);
                    var fb = FromOf(b);
                    int c = fa.X.CompareTo(fb.X);
                    if (c != 0) return c;
                    c = fa.Y.CompareTo(fb.Y);
                    return c != 0 ? c : a.Seq.CompareTo(b.Seq);
                });
                float span = 0;
                for (int i = 0; i < cascade.Count; i++)
                {
                    var e = cascade[i];
                    float delay = i * f.YardCascadeStaggerMs;
                    if (e is PieceFellEvent fe)
                    {
                        float fall = FallMotion.YardFallMs(fe.Rows, f);
                        Add(StepKind.YardFall, 44, start + delay, fall, EaseType.Linear, fe, fastForward: true, pieceId: fe.PieceId, n: i);
                        span = Math.Max(span, delay + fall);
                    }
                    else
                    {
                        var b = (BalloonRoseEvent)e;
                        float rise = (float)FallMotion.GlideMs(b.Rows, f.BalloonRiseSpeed);
                        Add(StepKind.BalloonGlide, 43, start + delay, rise, EaseType.OutSine, b, fastForward: true, pieceId: b.PieceId, n: i);
                        span = Math.Max(span, delay + rise);
                    }
                }
                // neighbour effects of the falls (crate, bag, chain) and hidden item check #2 play with the cascade
                float effects = LeftoverSpan(6, start);
                Advance(Math.Max(span, effects));
            }

            private static At FromOf(GameEvent e) => e is PieceFellEvent f ? f.From : ((BalloonRoseEvent)e).From;

            private void Step8()
            {
                float start = t;
                var shifted = Of<SiteShiftedEvent>(8);
                foreach (var e in Of<SegmentCompletedEvent>(8))
                {
                    var sh = shifted.Count > 0 && !Used(shifted[0]) ? shifted[0] : null;
                    float ms = Ms(18);
                    Add(StepKind.SegmentDone, 18, t, ms, r ? EaseType.Linear : f.SegmentEase, e, lockInput: true, flag: sh != null, toSeg: sh?.ToSeg,
                        also: sh != null ? new GameEvent[] { sh } : null);
                    Advance(ms);
                }
                Leftovers(8, false, start);
            }

            private void Step9()
            {
                float start = t;
                var delivered = new List<PieceFellEvent>();
                foreach (var e in Of<PieceFellEvent>(9)) if (e.Cause == FallCauses.Delivery) delivered.Add(e);
                var fromQueue = new List<PieceFellEvent>();
                var fromTruck = new List<PieceFellEvent>();
                foreach (var e in delivered) (Array.IndexOf(ctx.QueuedBefore, e.PieceId) >= 0 ? fromQueue : fromTruck).Add(e);
                var queuedList = Of<DeliveryQueuedEvent>(9);
                var queued = queuedList.Count > 0 ? queuedList[0] : null;
                var arrivedList = Of<DeliveryArrivedEvent>(9);
                var arrived = arrivedList.Count > 0 ? arrivedList[0] : null;
                if (fromTruck.Count > 0)
                {
                    var drops = new Drop[fromTruck.Count];
                    float lastLand = 0;
                    for (int i = 0; i < drops.Length; i++)
                    {
                        var e = fromTruck[i];
                        drops[i] = new Drop(e.PieceId, e.From, e.To, e.Rows, f.TruckEnterMs + i * f.TruckDropStaggerMs, FallMotion.YardFallMs(e.Rows, f));
                        lastLand = Math.Max(lastLand, drops[i].Delay + drops[i].Ms);
                    }
                    // JUICE §0 rule 3: only the truck itself (#19, 700 ms) locks the board; a longer drop lands unlocked, the
                    // next steps still wait for the last landing (web review Faz 2 tur 2 #16)
                    Add(StepKind.TruckDelivery, 19, t, Ms(19), f.TruckEnterEase, arrived, lockInput: true, drops: drops, also: fromTruck.ToArray());
                    Advance(Math.Max(Ms(19), lastLand));
                }
                if (fromQueue.Count > 0 || queued != null)
                {
                    var drops = new Drop[fromQueue.Count];
                    float flights = 0;
                    for (int i = 0; i < drops.Length; i++)
                    {
                        var e = fromQueue[i];
                        drops[i] = new Drop(e.PieceId, e.From, e.To, e.Rows, i * f.TruckDropStaggerMs, Ms(88));
                        flights = Math.Max(flights, drops[i].Delay + drops[i].Ms);
                    }
                    float chipMs = Math.Max(Ms(20), flights);
                    var also = new List<GameEvent>(fromQueue);
                    if (arrived != null && fromTruck.Count == 0) also.Add(arrived);
                    Add(StepKind.QueueChip, 20, t, chipMs, f.QueueChipEase, queued, fastForward: true, n: queued?.Queued, drops: drops, also: also.ToArray());
                    Advance(chipMs);
                }
                Leftovers(9, true, start);
            }

            private void Step11()
            {
                float start = t;
                // JUICE #94 first: K-48 reached (every block in place) → the yard sweep; then #55 and #56 (all locked)
                foreach (var e in Of<LevelWonEvent>())
                {
                    float clear = r ? f.ReducedFadeMs : Math.Max(f.YardClearMs, f.YardClearRibbonMs);
                    Add(StepKind.YardClear, 94, t, clear, EaseType.InOutSine, null, lockInput: true);
                    Advance(clear);
                    Add(StepKind.Win, 55, t, Ms(55), f.WinTitleEase, e, lockInput: true, n: e.MovesLeft);
                    Advance(Ms(55));
                    float bonus = BonusMs(e.MovesLeft, ctx.BonusMaxMoves, r, f);
                    if (bonus > 0)
                    {
                        Add(StepKind.BonusBuild, 56, t, bonus, f.BonusEase, null, lockInput: true, n: e.MovesLeft);
                        Advance(bonus);
                    }
                }
                // out of moves (#57) is played after step 12 (JUICE §0 rule 10: level end last)
                foreach (var e in Of<OutOfMovesEvent>()) used.Add(e);
                Leftovers(11, false, start);
                foreach (var e in Of<OutOfMovesEvent>()) used.Remove(e);
            }

            private void Step12()
            {
                float start = t;
                bool any = false;
                foreach (var e in events) if (e.Step == 12) any = true;
                // K-30 D1 truck help (#21a unchain, #21c reshuffle; together: #21a over #21c's start, ≤ 900 ms, JUICE #21a)
                float help = 0;
                foreach (var e in Of<TruckHelpEvent>(12))
                {
                    bool unchain = e.HelpKind == TruckHelpKinds.Unchain;
                    float ms = r ? f.ReducedFadeMs : unchain ? f.TruckHelpUnchainMs : f.ReshuffleMs;
                    Add(StepKind.TruckHelp, 21, start, ms, unchain ? EaseType.InCubic : EaseType.InOutSine, e, lockInput: true, flag: unchain);
                    help = Math.Max(help, ms);
                }
                float effects = LeftoverSpan(12, start, exclude: typeof(TeardownEvent));
                Advance(Math.Max(help, effects));
                // K-30 Söküm (#107): the package's last board step (TECH §2R.15 item 3)
                var tornList = Of<TeardownEvent>();
                if (tornList.Count > 0)
                {
                    var torn = tornList[0];
                    int n = Math.Max(1, torn.Pieces.Length);
                    float tear = r ? f.ReducedFadeMs : f.TeardownSettleMs + f.TeardownMs + (n - 1) * f.TeardownStaggerMs;
                    Add(StepKind.Teardown, 107, t, tear, EaseType.InOutQuad, torn, lockInput: true, n: torn.Pieces.Length);
                    Advance(tear);
                }
                if (any) Add(StepKind.Resync, 0, t, 0, EaseType.Linear);
            }

            private void LevelEndLoss()
            {
                foreach (var e in Of<OutOfMovesEvent>())
                {
                    Add(StepKind.OutOfMoves, 57, t, Ms(57), EaseType.OutBack, e, lockInput: true);
                    Advance(Ms(57));
                }
            }

            /// <summary>
            /// Every event of `pipelineStep` not shown yet: its JUICE row (obstacles, timers, HUD) in parallel at `start`, or an
            /// `Instant` step. `advances`: the beat cursor moves past the board steps it added.
            /// </summary>
            private void Leftovers(int pipelineStep, bool advances, float? startAt = null)
            {
                float start = startAt ?? t;
                float span = LeftoverSpan(pipelineStep, start);
                if (advances) AdvanceTo(start + span);
            }

            /// <summary>Adds the leftover steps of `pipelineStep` at `start`; returns the span of the board steps among them.</summary>
            private float LeftoverSpan(int pipelineStep, float start, Type? exclude = null)
            {
                float span = 0;
                foreach (var e in events)
                {
                    if (e.Step != pipelineStep || Used(e)) continue;
                    if (exclude != null && exclude.IsInstanceOfType(e)) continue;
                    float ms = Generic(e, start, out bool board);
                    if (board) span = Math.Max(span, ms);
                }
                return span;
            }

            /// <summary>The JUICE row of an event outside the specific phases (mostly Phase 3 obstacles and timers).</summary>
            private float Generic(GameEvent e, float at, out bool board)
            {
                board = true;
                switch (e)
                {
                    case CrateDamagedEvent x:
                        return Add(StepKind.CrateHit, 32, at, Reduce(f.CrateHitMs), EaseType.OutBack, x, fastForward: true).Ms;
                    case CrateBrokenEvent x:
                        return Add(StepKind.CrateBreak, 33, at, Reduce(f.CrateBreakMs), EaseType.OutQuad, x, fastForward: true).Ms;
                    case BagTornEvent x:
                        return Add(StepKind.BagTear, 34, at, Reduce(f.BagTearMs), EaseType.OutQuad, x, fastForward: true).Ms;
                    case ChainReleasedEvent x:
                        return Add(StepKind.ChainBreak, 36, at, Reduce(f.ChainBreakMs), EaseType.InCubic, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case GapUnlockedEvent x:
                        return Add(StepKind.GapUnlock, 0, at, Reduce(f.UnlockMs), EaseType.OutBack, x, fastForward: true).Ms;
                    case GapChangedEvent x:
                        return Add(StepKind.GapChange, 25, at, Reduce(f.ShutterMs), EaseType.InOutCubic, x, fastForward: true).Ms;
                    case WetTickEvent x when x.Left <= 0:
                        return Add(StepKind.WetDry, 38, at, Reduce(f.WetDryMs), EaseType.OutQuad, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case WetTickEvent x:
                        return Add(StepKind.WetTick, 37, at, Reduce(f.TickMs), EaseType.OutQuad, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case CarouselRotatedEvent x:
                        return Add(StepKind.CarouselRotate, 47, at, Reduce(f.CarouselMs), EaseType.InOutCubic, x, fastForward: true).Ms;
                    case ElevatorMovedEvent x:
                        return Add(StepKind.ElevatorMove, 48, at, Reduce(f.ElevatorMs), EaseType.InOutSine, x, fastForward: true).Ms;
                    case ColorsSwappedEvent x:
                        return Add(StepKind.Brush, 63, at, Reduce(f.BrushMs), EaseType.InOutSine, x, fastForward: true, pieceId: x.A).Ms;
                    case PieceReturnedEvent x:
                        return Add(StepKind.PieceReturn, 0, at, f.PieceReturnMs, f.BounceEase, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case PieceLiftedEvent x:
                        return Add(StepKind.CraneLift, 62, at, r ? f.ReducedFadeMs : f.CraneBoosterMs, EaseType.InOutSine, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case CargoSmashedEvent x:
                        return Add(StepKind.CargoSmash, 61, at, r ? f.ReducedFadeMs : f.HammerMs, EaseType.OutExpo, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case PieceFellEvent x:
                        return Add(StepKind.YardFall, 44, at, FallMotion.YardFallMs(x.Rows, f), EaseType.Linear, x, fastForward: true, pieceId: x.PieceId, n: x.Rows).Ms;
                    case BalloonRoseEvent x:
                        return Add(StepKind.BalloonGlide, 43, at, (float)FallMotion.GlideMs(x.Rows, f.BalloonRiseSpeed), EaseType.OutSine, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case GlassBrokeEvent x:
                        return Add(StepKind.GlassBreak, 42, at, Reduce(f.GlassBreakMs), EaseType.OutExpo, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case MortarStuckEvent x:
                        return Add(StepKind.MortarStuck, 14, at, Reduce(f.MortarStickMs), f.MortarStickEase, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case PlacementCorrectEvent x:
                        return Add(StepKind.PlaceCorrect, 12, at, Ms(12), f.PlaceOkEase, x, fastForward: true, pieceId: x.PieceId, cells: x.Cells).Ms;
                    case CellsRevealedEvent x:
                        board = false;
                        return Add(StepKind.CellsReveal, 49, at, Reduce(f.RevealMs), EaseType.OutBack, x).Ms;
                    case ScrewCollectedEvent x:
                        board = false;
                        return Add(StepKind.ScrewCollect, 40, at, Reduce(f.ScrewCollectMs), EaseType.InOutQuad, x).Ms;
                    case KeyCollectedEvent x:
                        board = false;
                        return Add(StepKind.KeyCollect, 39, at, Reduce(f.KeyPickupMs), EaseType.OutBack, x).Ms;
                    case GoalProgressEvent x:
                        board = false;
                        return x.Value >= x.Target
                            ? Add(StepKind.GoalDone, 54, at, Reduce(f.GoalDoneMs), EaseType.OutBack, x, n: x.Value).Ms
                            : Add(StepKind.GoalTick, 0, at, Reduce(f.TickMs), EaseType.OutQuad, x, n: x.Value).Ms;
                    case MovesChangedEvent x:
                        board = false;
                        return Add(StepKind.MovesTick, 50, at, Ms(50), f.MovesTickEase, x, n: x.MovesLeft).Ms;
                    case ComboChangedEvent x when x.Combo == 0:
                        board = false;
                        return Add(StepKind.StreakReset, 0, at, 0, EaseType.Linear, x).Ms;
                    case ComboChangedEvent x:
                        board = false;
                        return Add(StepKind.StreakPip, 15, at, Ms(15), EaseType.OutBack, x, n: x.Combo).Ms;
                    case TrowelEarnedEvent x:
                        board = false;
                        return Add(StepKind.TrowelEarned, 16, at, Ms(16), f.ComboEase, x, n: x.Trowels).Ms;
                    case BoosterRejectedEvent x:
                        board = false;
                        return Add(StepKind.BoosterRejected, 108, at, Reduce(f.BlockedShakeMs), f.BlockedShakeEase, x).Ms;
                    case DeliveryQueuedEvent x:
                        board = false;
                        return Add(StepKind.QueueChip, 20, at, Ms(20), f.QueueChipEase, x, n: x.Queued).Ms;
                    case PiecePaintedEvent x:
                        return Add(StepKind.PiecePaint, 0, at, Reduce(f.PaintGateMs), EaseType.InOutSine, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case WindDriftEvent x:
                        return Add(StepKind.WindDrift, 0, at, Reduce(f.WindDriftMs), EaseType.OutQuad, x, fastForward: true, pieceId: x.PieceId).Ms;
                    case SteeredEvent x:
                        return Add(StepKind.Steer, 46, at, f.SteerMs, EaseType.OutQuad, x, fastForward: true, pieceId: x.PieceId).Ms;
                    default:
                        // pieceMoved over the wall, boosterApplied (hammer / crane / paint detail), siteShifted without a
                        // segment, deliveryArrived from the queue, deadlockDetected, … : applied at once
                        board = false;
                        Add(StepKind.Instant, 0, at, 0, EaseType.Linear, e, pieceId: PieceOf(e));
                        return 0;
                }
            }

            private static int PieceOf(GameEvent e)
            {
                switch (e)
                {
                    case PieceMovedEvent x: return x.PieceId;
                    case MoveCancelledEvent x: return x.PieceId;
                    case ChainReleasedEvent x: return x.PieceId;
                    case PieceLiftedEvent x: return x.PieceId;
                    default: return -1;
                }
            }

            /// <summary>JUICE §0 rule 14 (DL-2R-24): with a `teardown` in the package the correct-placement rewards become Instant.</summary>
            private void SuppressRewardsOnTeardown()
            {
                bool torn = false;
                foreach (var e in events) if (e is TeardownEvent) torn = true;
                if (!torn) return;
                for (int i = 0; i < steps.Count; i++)
                {
                    var s = steps[i];
                    if (s.Kind != StepKind.PlaceCorrect && s.Kind != StepKind.StreakPip && s.Kind != StepKind.TrowelEarned && s.Kind != StepKind.BuildFront
                        && s.Kind != StepKind.BlocksChipFly) continue;
                    steps[i] = new AnimationStep(StepKind.Instant, 0, s.At, 0, EaseType.Linear, false, false, s.Event, s.Also, s.PieceId, s.Cells, s.N, false,
                        false, null, null);
                }
            }

            private AnimationPlan Finish(float last)
            {
                var arr = steps.ToArray();
                // beat of a step = the last cursor position at or before its start; groups keep the steps' list order
                var members = new List<AnimationStep>[beatStarts.Count];
                foreach (var s in arr)
                {
                    int k = beatStarts.Count - 1;
                    while (k > 0 && beatStarts[k] > s.At + 1e-3f) k--;
                    (members[k] ??= new List<AnimationStep>()).Add(s);
                }
                var groups = new List<AnimationGroup>();
                for (int k = 0; k < members.Length; k++)
                {
                    var list = members[k];
                    if (list == null) continue;
                    float to = 0;
                    foreach (var s in list)
                    {
                        s.Group = groups.Count;
                        to = Math.Max(to, s.End);
                    }
                    groups.Add(new AnimationGroup(groups.Count, beatStarts[k], to, list.ToArray()));
                }
                return new AnimationPlan(arr, groups.ToArray(), last);
            }
        }
    }
}
