#nullable enable
// Pure data of the animation planner (Faz 3 Q0): what the view plays for one committed action, in K-35 step order
// (JUICE §0 rule 10). Port and extension of web src/scenes/level/juice/plan.ts `MoveCue` / `MovePlan`.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Animation
{
    /// <summary>
    /// What a step shows. The JUICE.md row is in `AnimationStep.Juice` (0 = no row). Board motions are `FastForward`
    /// (a grab jumps them to their last frame, R-12); `Lock` steps close board input (JUICE §0 rule 3).
    /// </summary>
    public enum StepKind
    {
        /// <summary>An event with no animation of its own: the view applies it at once (snap / resync). No event is dropped.</summary>
        Instant,
        /// <summary>#8 the block returns to its start (K-05, K-07 cancel; `moveCancelled`). `Flag` = sameSpot.</summary>
        Cancel,
        /// <summary>#9 yard drop (K-10): settles into the cell, small squash.</summary>
        YardDrop,
        /// <summary>#23 rail park (K-12): the scaffold clamps hold the block.</summary>
        RailClamp,
        /// <summary>W6 paint gate repaint during the drag (`piecePainted`).</summary>
        PiecePaint,
        /// <summary>#17 Golden Trowel flight (K-33): a yard block flies to its P position.</summary>
        TrowelFlight,
        /// <summary>#62 crane flight (K-37, `pieceLifted` by crane).</summary>
        CraneLift,
        /// <summary>#61 hammer smashes an Ağır Yük (K-36, `cargoSmashed`).</summary>
        CargoSmash,
        /// <summary>A block goes back (glass break S3 / hammer on site debris or stuck mortar: `pieceReturned`).</summary>
        PieceReturn,
        /// <summary>#63 paint brush colour swap (K-38, `colorsSwapped`).</summary>
        Brush,
        /// <summary>#53 +5 moves added (K-29 accepted offer).</summary>
        MovesAdded,
        /// <summary>W8 fan drift before the fall (`windDrift`).</summary>
        WindDrift,
        /// <summary>#10 accelerated site fall (K-11; physics tokens).</summary>
        SiteFall,
        /// <summary>#11 landing squash x 1,12 / y 0,86 → 1 (Back.easeOut bounce).</summary>
        Landing,
        /// <summary>#43 balloon rise (S8) / a constant-speed glide.</summary>
        BalloonGlide,
        /// <summary>#46 G-L steering (K-19).</summary>
        Steer,
        /// <summary>#42 glass break (S3).</summary>
        GlassBreak,
        /// <summary>The streak beads empty (K-33 combo 0).</summary>
        StreakReset,
        /// <summary>#12 correct placement: the plan cells fill, white flash (the brief's "kilit parlaması") + sparks.</summary>
        PlaceCorrect,
        /// <summary>D-091 counterpart of "goal items fly to the HUD": a block icon arcs to the blocks-left chip, the chip pops (UX §5.9).</summary>
        BlocksChipFly,
        /// <summary>#83 the build front moves (K-34).</summary>
        BuildFront,
        /// <summary>#49 hidden `?` cells revealed (K-32).</summary>
        CellsReveal,
        /// <summary>#15 a streak bead fills (K-33).</summary>
        StreakPip,
        /// <summary>#16 Golden Trowel earned (K-33; the brief's "booster oluşumu").</summary>
        TrowelEarned,
        /// <summary>#13 wrong placement (K-17): red tint, 2 px shake, arc bounce to the K-17 target (the brief's "kötü geçiş geri sekmesi").</summary>
        PlaceWrong,
        /// <summary>#88 the bounced block shrinks into the truck chip (K-17 step 3).</summary>
        BounceToQueue,
        /// <summary>#84 missing support hatch blinks (K-34).</summary>
        SupportFlash,
        /// <summary>#14 mortar block sticks (Y8).</summary>
        MortarStuck,
        /// <summary>Bookkeeping: the view re-reads the state (after a mortar stick, after step 12).</summary>
        Resync,
        /// <summary>#50 moves counter roll.</summary>
        MovesTick,
        /// <summary>#51 last-moves warning pulse (`First` = the condition just started: sound + haptic once).</summary>
        LastMoves,
        /// <summary>#32 crate hit (Y1).</summary>
        CrateHit,
        /// <summary>#33 crate broken.</summary>
        CrateBreak,
        /// <summary>#34 cement bag torn (Y2).</summary>
        BagTear,
        /// <summary>#36 chain broken (Y3).</summary>
        ChainBreak,
        /// <summary>#40 screw collected (Y7).</summary>
        ScrewCollect,
        /// <summary>#39 key collected.</summary>
        KeyCollect,
        /// <summary>W7 lock opens (`gapUnlocked`).</summary>
        GapUnlock,
        /// <summary>#44 yard gravity cascade fall (K-20, Y6), staggered by column.</summary>
        YardFall,
        /// <summary>A clear / collect / build goal counter changed (`goalProgress`).</summary>
        GoalTick,
        /// <summary>#54 a goal is complete.</summary>
        GoalDone,
        /// <summary>#18 segment done (K-22): scaffold fades, the segment flies to the panorama, the next slides in. Locked.</summary>
        SegmentDone,
        /// <summary>#19 truck delivery (K-25): the truck enters, blocks drop staggered. Locked (the truck's 700 ms).</summary>
        TruckDelivery,
        /// <summary>#20 truck queue chip (K-26): bump, queued blocks fly out of it (`Drops`).</summary>
        QueueChip,
        /// <summary>#25 / #27 gap state change (W4 shutter, W5 slider).</summary>
        GapChange,
        /// <summary>#37 wet concrete counter tick (Y4).</summary>
        WetTick,
        /// <summary>#38 wet concrete dried.</summary>
        WetDry,
        /// <summary>#47 carousel rotation (S5).</summary>
        CarouselRotate,
        /// <summary>#48 elevator move (S6).</summary>
        ElevatorMove,
        /// <summary>#94 "Bütün bloklar yerinde" (K-48), before the win. Locked.</summary>
        YardClear,
        /// <summary>#55 win (K-28). Locked.</summary>
        Win,
        /// <summary>#56 Bonus İnşaat: left-over moves turn into gold one by one (K-28). Locked.</summary>
        BonusBuild,
        /// <summary>#57 "Hamleler bitti!" (K-29). Locked.</summary>
        OutOfMoves,
        /// <summary>#21a / #21c Kamyon Yardımı (K-30 D1: unchain / reshuffle). Locked.</summary>
        TruckHelp,
        /// <summary>#107 Söküm (K-30): torn-down blocks fly back, 80 ms apart. Locked.</summary>
        Teardown,
        /// <summary>#108 a booster without a valid target (`boosterRejected`).</summary>
        BoosterRejected,
        /// <summary>The plan's last step: the board shows the state again (level-end decisions, HUD sync).</summary>
        End,
    }

    /// <summary>One block entering the yard: from the truck bed (#19) or out of the queue chip (#20).</summary>
    public readonly struct Drop
    {
        public readonly int PieceId;
        public readonly At From;
        public readonly At To;
        public readonly int Rows;
        /// <summary>Offset from the step start (ms).</summary>
        public readonly float Delay;
        /// <summary>Fall / flight duration (ms).</summary>
        public readonly float Ms;

        public Drop(int pieceId, At from, At to, int rows, float delay, float ms)
        {
            PieceId = pieceId;
            From = from;
            To = to;
            Rows = rows;
            Delay = delay;
            Ms = ms;
        }
    }

    public sealed class AnimationStep
    {
        private static readonly GameEvent[] NoEvents = new GameEvent[0];

        public readonly StepKind Kind;
        /// <summary>JUICE.md row number (#8, #12 …); 0 = no row (bookkeeping, Instant, D-091 additions).</summary>
        public readonly int Juice;
        /// <summary>Start, ms from the plan start.</summary>
        public readonly float At;
        public readonly float Ms;
        public readonly EaseType Ease;
        /// <summary>Board input is closed while it plays (segment slide, truck, truck help, Söküm, level end).</summary>
        public readonly bool Lock;
        /// <summary>It moves the board: a grab (R-12) makes it jump to its last frame.</summary>
        public readonly bool FastForward;
        /// <summary>Beat of the plan (`AnimationPlan.Groups`): steps of one group start together or later inside it; groups start in order.</summary>
        public int Group { get; internal set; }
        /// <summary>The core event this step shows (null: bookkeeping steps).</summary>
        public readonly GameEvent? Event;
        /// <summary>More events this step covers (a bounce's `placementWrong`, the truck's `pieceFell` drops …).</summary>
        public readonly GameEvent[] Also;
        /// <summary>The piece it moves; −1 none.</summary>
        public readonly int PieceId;
        /// <summary>#84 missing-support cells, #12 placed cells.</summary>
        public readonly At[]? Cells;
        /// <summary>
        /// #12 / #15 streak value reached (1…4); #13 rows of the K-17 drop above the yard; #16 trowels held; #20 queued
        /// blocks; #50 / #51 / #53 moves left; #55 / #56 moves left at the win; yard cascade index.
        /// </summary>
        public readonly int? N;
        /// <summary>#51 the warning just started.</summary>
        public readonly bool First;
        /// <summary>#8 sameSpot; #13 the bounce lands in the truck queue; #18 a next segment slides in.</summary>
        public readonly bool Flag;
        /// <summary>#18 the segment shown next (null: last segment).</summary>
        public readonly int? ToSeg;
        /// <summary>#19 / #20 deliveries.</summary>
        public readonly Drop[]? Drops;

        internal AnimationStep(StepKind kind, int juice, float at, float ms, EaseType ease, bool lockInput, bool fastForward, GameEvent? ev,
            GameEvent[]? also, int pieceId, At[]? cells, int? n, bool first, bool flag, int? toSeg, Drop[]? drops)
        {
            Kind = kind;
            Juice = juice;
            At = at;
            Ms = ms;
            Ease = ease;
            Lock = lockInput;
            FastForward = fastForward;
            Event = ev;
            Also = also ?? NoEvents;
            PieceId = pieceId;
            Cells = cells;
            N = n;
            First = first;
            Flag = flag;
            ToSeg = toSeg;
            Drops = drops;
        }

        /// <summary>
        /// A step built by hand (Q1: level intro, Undo #64, tap hop, debug playback; tests). The planner builds its own; a
        /// plan of hand-made steps comes from `AnimationPlan.FromSteps`.
        /// </summary>
        public static AnimationStep Create(StepKind kind, float at, float ms, int juice = 0, EaseType ease = EaseType.Linear, bool lockInput = false,
            bool fastForward = false, GameEvent? ev = null, int pieceId = -1, At[]? cells = null, int? n = null, bool flag = false, Drop[]? drops = null)
        {
            if (!(at >= 0) || !(ms >= 0)) throw new ArgumentOutOfRangeException(nameof(at), "AnimationStep: at and ms must be ≥ 0");
            return new AnimationStep(kind, juice, at, ms, ease, lockInput, fastForward, ev, null, pieceId, cells, n, false, flag, null, drops);
        }

        public float End => At + Ms;

        /// <summary>K-35 step of its event (−1 for bookkeeping steps).</summary>
        public int PipelineStep => Event != null ? Event.Step : Also.Length > 0 ? Also[0].Step : -1;

        public override string ToString() => Kind + (Juice > 0 ? " #" + Juice : "") + " @" + Num.Text(At) + " +" + Num.Text(Ms) + (Lock ? " lock" : "") + (PieceId >= 0 ? " piece " + PieceId : "");
    }

    /// <summary>A beat of the plan: steps that start together (or later inside the beat), run in parallel.</summary>
    public sealed class AnimationGroup
    {
        public readonly int Index;
        public readonly float At;
        public readonly float End;
        public readonly AnimationStep[] Steps;

        internal AnimationGroup(int index, float at, float end, AnimationStep[] steps)
        {
            Index = index;
            At = at;
            End = end;
            Steps = steps;
        }
    }

    /// <summary>The schedule of one committed action. The core already decided everything; the plan never changes the state.</summary>
    public sealed class AnimationPlan
    {
        /// <summary>Steps in K-35 order (insertion order of the planner; `At` is non-decreasing inside each phase).</summary>
        public readonly AnimationStep[] Steps;
        /// <summary>Steps grouped by beat: groups start in order; a group's steps may outlast the next group's start (#11 squash under #12).</summary>
        public readonly AnimationGroup[] Groups;
        /// <summary>End of the last step (ms).</summary>
        public readonly float Ms;

        internal AnimationPlan(AnimationStep[] steps, AnimationGroup[] groups, float ms)
        {
            Steps = steps;
            Groups = groups;
            Ms = ms;
        }

        /// <summary>A plan of hand-made steps (kept in the given order); one group per distinct start time.</summary>
        public static AnimationPlan FromSteps(IReadOnlyList<AnimationStep> steps)
        {
            var arr = new AnimationStep[steps.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = steps[i];
            var starts = new SortedSet<float>();
            float ms = 0;
            foreach (var s in arr)
            {
                starts.Add(s.At);
                ms = Math.Max(ms, s.End);
            }
            var groups = new List<AnimationGroup>();
            foreach (float at in starts)
            {
                var members = new List<AnimationStep>();
                float end = at;
                foreach (var s in arr)
                {
                    if (s.At != at) continue;
                    s.Group = groups.Count;
                    members.Add(s);
                    end = Math.Max(end, s.End);
                }
                groups.Add(new AnimationGroup(groups.Count, at, end, members.ToArray()));
            }
            return new AnimationPlan(arr, groups.ToArray(), ms);
        }

        /// <summary>True when a step closes board input.</summary>
        public bool HasLock
        {
            get
            {
                foreach (var s in Steps) if (s.Lock && s.Ms > 0) return true;
                return false;
            }
        }

        /// <summary>Every event the plan covers (`Event` + `Also` of every step), by reference.</summary>
        public HashSet<GameEvent> CoveredEvents()
        {
            var o = new HashSet<GameEvent>(EventIdentity.Instance);
            foreach (var s in Steps)
            {
                if (s.Event != null) o.Add(s.Event);
                foreach (var e in s.Also) o.Add(e);
            }
            return o;
        }

        /// <summary>The first step of `kind`, or null.</summary>
        public AnimationStep? Find(StepKind kind)
        {
            foreach (var s in Steps) if (s.Kind == kind) return s;
            return null;
        }
    }

    /// <summary>Reference equality for events (two equal-looking events of one package stay distinct).</summary>
    internal sealed class EventIdentity : IEqualityComparer<GameEvent>
    {
        public static readonly EventIdentity Instance = new EventIdentity();

        public bool Equals(GameEvent? x, GameEvent? y) => ReferenceEquals(x, y);
        public int GetHashCode(GameEvent obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
