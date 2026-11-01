#nullable enable
// `TutorialController` (paket C1; web src/scenes/level/tutorial/TutorialController.ts; GDD K-53, §14.1; UX_FLOWS §13.1): the pure state machine
// over a level's `tutorial[]` steps — which step is ACTIVE and when it ends. What is on screen (wait / shown / hidden, glove, bubble) is
// `TutorialPresence`'s, the drawing is `TutorialView`'s. No engine, no clock of its own: the host passes the animation time.
//
// Light tutorial (K-53, R2-10):
//  - Steps never lock input: there is no required step and no pick gate. A `mode` other than "soft" is a validator error (L-31); data that
//    still carries one is played as soft.
//  - Steps run in order. A step with `startOn` WAITS (nothing shown, play is free) until its start event happens after the previous step
//    ended; without `startOn` it starts when the previous one ends (the first at level start). A `startOn` that never happens hides that
//    step and every later one (GDD §14.1/5).
//  - A step ends ONLY on its `done` event (K-53/3, GDD §14.1/3 vocabulary with its filters): the counter starts at 0 when the step starts,
//    `count` defaults to 1, a move-end event counts at most once per move, a drag signal at most once per drag. Hiding the step never ends it.
//  - A `tut.ctx.<topic>` text marks `seenContextTips.<topic>` the moment the step starts (GDD §14.1/2).
//  - K-53/6: a level the save marks `won` shows no step (`TutorialFactory.Create`).
//  - Data driven: nothing here knows a level; every step comes from the level JSON.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;

namespace MinikUsta.Presentation.Tutorial
{
    public interface ITutorialHost
    {
        /// <summary>The game state the move-end filters read (null while no attempt runs).</summary>
        GameState? CurrentState { get; }

        /// <summary>GDD §14.1/2: `seenContextTips.&lt;topic&gt;` (written at once).</summary>
        void MarkContextTip(string topic);

        /// <summary>A step ended on its `done` event (analytics `tutorial_step`).</summary>
        void StepEnded(int step, float now);

        /// <summary>A counted event of the active step that did not end it yet (`count` &gt; 1): the presence hides (K-53/3).</summary>
        void Progressed(int step, float now);
    }

    /// <summary>The active step (started, not ended: shown or hidden on screen, K-53/3).</summary>
    public sealed class ActiveStep
    {
        /// <summary>Index in the sorted steps.</summary>
        public readonly int Index;
        public readonly TutorialStepData Data;
        /// <summary>Highlighted blocks (`piece:` / `debris:` resolved through `CompiledLevel.TutorialPieceIds`).</summary>
        public readonly int[] Pieces;
        /// <summary>Animation time the step started.</summary>
        public readonly float Since;

        public ActiveStep(int index, TutorialStepData data, int[] pieces, float since)
        {
            Index = index;
            Data = data;
            Pieces = pieces;
            Since = since;
        }
    }

    /// <summary>Where the tutorial is: step `Index` of the sorted steps (`steps.Length` = finished), started (`Shown`) or waiting for its `startOn`, `Count` events toward its condition.</summary>
    public readonly struct TutorialPosition
    {
        public readonly int Index;
        public readonly bool Shown;
        public readonly int Count;

        public TutorialPosition(int index, bool shown, int count)
        {
            Index = index;
            Shown = shown;
            Count = count;
        }
    }

    public static class TutorialFactory
    {
        /// <summary>
        /// The tutorial of an attempt (K-53/6): null when the level has no steps or the player's save marks it won (replays of the 1–10
        /// loop show no step; contextual lines follow their own `seenContextTips` rule).
        /// </summary>
        public static TutorialController? Create(CompiledLevel lvl, ITutorialHost host, bool won)
        {
            if (won) return null;
            if (lvl.Data.Tutorial == null || lvl.Data.Tutorial.Length == 0) return null;
            return new TutorialController(lvl, host);
        }
    }

    public sealed class TutorialController
    {
        private const string CtxPrefix = "tut.ctx.";

        private enum Phase
        {
            Idle,
            Waiting,
            Active,
            Finished,
        }

        private readonly ITutorialHost host;
        private readonly HashSet<string> dragCounted = new HashSet<string>();
        private Phase phase = Phase.Idle;
        private int index = -1;
        private int count;
        private float since;
        private int[] pieces = new int[0];

        public TutorialController(CompiledLevel lvl, ITutorialHost host)
        {
            Lvl = lvl ?? throw new ArgumentNullException(nameof(lvl));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            var list = new List<TutorialStepData>(lvl.Data.Tutorial ?? new TutorialStepData[0]);
            list.Sort((a, b) => a.Step.CompareTo(b.Step));
            Steps = list.ToArray();
        }

        public CompiledLevel Lvl { get; }
        public TutorialStepData[] Steps { get; }

        /// <summary>Bumped when the active step changes (a new presence starts then).</summary>
        public int Version { get; private set; }

        public bool Finished => phase == Phase.Finished;

        /// <summary>The active step (started and not ended), or null (waiting for `startOn`, idle or finished).</summary>
        public ActiveStep? Current
        {
            get
            {
                if (phase != Phase.Active || index < 0 || index >= Steps.Length) return null;
                return new ActiveStep(index, Steps[index], pieces, since);
            }
        }

        /// <summary>The step waiting for its `startOn` event (nothing on screen), if any.</summary>
        public TutorialStepData? Waiting => phase == Phase.Waiting && index >= 0 && index < Steps.Length ? Steps[index] : null;

        /// <summary>The position (for tests and a future save field); null before `Start`.</summary>
        public TutorialPosition? Position()
        {
            if (phase == Phase.Idle) return null;
            if (phase == Phase.Finished) return new TutorialPosition(Steps.Length, false, 0);
            return new TutorialPosition(index, phase == Phase.Active, count);
        }

        /// <summary>`piece:&lt;i&gt;`, `piece:k&lt;p&gt;_&lt;i&gt;`, `debris:&lt;i&gt;` of a step → piece ids (TECH §8.2 "Vurgu → blok çözümü").</summary>
        public static int[] HighlightedPieces(CompiledLevel lvl, string[] highlight)
        {
            var o = new List<int>();
            foreach (var h in highlight)
            {
                if (!h.StartsWith("piece:", StringComparison.Ordinal) && !h.StartsWith("debris:", StringComparison.Ordinal)) continue;
                if (lvl.TutorialPieceIds.TryGetValue(h, out int id) && !o.Contains(id)) o.Add(id);
            }
            return o.ToArray();
        }

        /// <summary>Level start: the first step (or its `startOn` wait).</summary>
        public void Start(float now)
        {
            index = -1;
            phase = Phase.Idle;
            Next(now);
        }

        /// <summary>Level over: nothing more is shown (GDD §14.1/5: steps whose `startOn` never came stay hidden).</summary>
        public void Stop()
        {
            if (phase == Phase.Finished) return;
            phase = Phase.Finished;
            Version++;
        }

        /// <summary>The player lifted a block (K-07 threshold passed): a new drag for the drag-signal counting.</summary>
        public void DragStarted() => dragCounted.Clear();

        /// <summary>Is `pieceId` one of the active step's highlighted blocks (presence `correctAction`)?</summary>
        public bool Highlights(int pieceId)
        {
            if (phase != Phase.Active) return false;
            foreach (int id in pieces) if (id == pieceId) return true;
            return false;
        }

        /// <summary>
        /// Drag signal (`overWall` = crossed the wall, `gapPass` = entered a rail, `holdOverBuild` from the hold timer); each counts at most
        /// once per drag, even when the drag is cancelled later. Any block counts (no gate).
        /// </summary>
        public void DragSignal(string kind, float now, float holdMs = 0)
        {
            if (dragCounted.Contains(kind)) return;
            var cond = ActiveCond();
            if (cond == null || cond.Event != kind) return;
            if (kind == TutorialEvents.HoldOverBuild && holdMs < (cond.MinMs ?? 0)) return;
            dragCounted.Add(kind);
            Hit(cond, now);
        }

        /// <summary>`holdOverBuild.minMs` the hold timer must reach now; null when no step listens for it.</summary>
        public int? HoldMinMs()
        {
            var cond = ActiveCond();
            return cond != null && cond.Event == TutorialEvents.HoldOverBuild ? cond.MinMs ?? 0 : (int?)null;
        }

        /// <summary>A tap (under the K-07 threshold) on block `pieceId`: counts for `tap` when the step highlights it.</summary>
        public void TappedPiece(int pieceId, float now)
        {
            var cond = ActiveCond();
            if (cond == null || cond.Event != "tap" || index < 0 || index >= Steps.Length) return;
            foreach (int id in HighlightedPieces(Lvl, Steps[index].Highlight))
                if (id == pieceId)
                {
                    Hit(cond, now);
                    return;
                }
        }

        /// <summary>A tap on a highlighted HUD target (`booster:hammer` …): counts for `tap` when the step highlights it.</summary>
        public void TappedTarget(string target, float now)
        {
            var cond = ActiveCond();
            if (cond == null || cond.Event != "tap" || index < 0 || index >= Steps.Length) return;
            if (Array.IndexOf(Steps[index].Highlight, target) >= 0) Hit(cond, now);
        }

        /// <summary>One committed move (drag or booster use) finished playing: a move-end event counts once.</summary>
        public void MoveEnded(IReadOnlyList<GameEvent> events, float now)
        {
            var cond = ActiveCond();
            if (cond != null && TutorialEvents.MoveMatches(cond, events, host.CurrentState)) Hit(cond, now);
        }

        // --- internals ---------------------------------------------------------------------------------------------------------

        /// <summary>The condition the controller listens for now: `startOn` while waiting, `done` while active.</summary>
        private TutCondition? ActiveCond()
        {
            if (index < 0 || index >= Steps.Length) return null;
            var step = Steps[index];
            if (phase == Phase.Waiting) return step.StartOn;
            if (phase == Phase.Active) return step.Done;
            return null;
        }

        private void Hit(TutCondition cond, float now)
        {
            count++;
            if (count < (cond.Count ?? 1))
            {
                if (phase == Phase.Active && index >= 0 && index < Steps.Length) host.Progressed(Steps[index].Step, now);
                return;
            }
            if (phase == Phase.Waiting) Activate(now);
            else Complete(now);
        }

        private void Next(float now)
        {
            index++;
            count = 0;
            dragCounted.Clear();
            if (index >= Steps.Length)
            {
                phase = Phase.Finished;
                Version++;
                return;
            }
            if (Steps[index].StartOn != null)
            {
                phase = Phase.Waiting;
                Version++;
                return;
            }
            Activate(now);
        }

        private void Activate(float now)
        {
            if (index < 0 || index >= Steps.Length) return;
            var step = Steps[index];
            phase = Phase.Active;
            count = 0;
            since = now;
            pieces = HighlightedPieces(Lvl, step.Highlight);
            if (step.TextKey.StartsWith(CtxPrefix, StringComparison.Ordinal)) host.MarkContextTip(step.TextKey.Substring(CtxPrefix.Length));
            Version++;
        }

        private void Complete(float now)
        {
            if (index >= 0 && index < Steps.Length) host.StepEnded(Steps[index].Step, now);
            Next(now);
        }
    }

    /// <summary>
    /// K-43 resume without a saved tutorial position (the Unity save keeps none): the tutorial is rebuilt from the attempt's log by
    /// feeding it the events of every replayed action like the live level screen did — a drag starts, gives the drag signals its result
    /// proves (from its `pieceMoved` event: `overWall` for a yard block released FREE over the site, `gapPass` for a block released on a
    /// rail) and ends with the move's events. `holdOverBuild` is never assumed (the log has no hold duration). Undo is skipped.
    /// </summary>
    public static class TutorialReplay
    {
        public static void Action(TutorialController tut, SessionAction action, IReadOnlyList<GameEvent> events, float now)
        {
            if (action is StartAction || action is UndoAction) return;
            if (action is DragMove drag)
            {
                tut.DragStarted();
                foreach (var e in events)
                {
                    if (!(e is PieceMovedEvent moved) || moved.PieceId != drag.PieceId) continue;
                    if (moved.Entry == EntryKinds.Gap) tut.DragSignal(TutorialEvents.GapPass, now);
                    if (moved.Entry == EntryKinds.OverWall && moved.From.Zone == At.ZoneYard) tut.DragSignal(TutorialEvents.OverWall, now);
                    break;
                }
            }
            tut.MoveEnded(events, now);
        }

        /// <summary>Replays `log` on a fresh session of `lvl` and feeds `tut` (already started at `now`); returns the replayed session.</summary>
        public static GameSession Rebuild(CompiledLevel lvl, IReadOnlyList<SessionAction> log, TutorialController tut, float now, Action<GameState?>? setState = null)
        {
            var sink = new ArraySink();
            return GameSession.Replay(lvl, log, null, sink, (i, action, session) =>
            {
                if (i > 0)
                {
                    setState?.Invoke(session.State); // the filters read the state right after the action
                    Action(tut, action, sink.Events, now);
                }
                sink.Clear();
            });
        }
    }
}
