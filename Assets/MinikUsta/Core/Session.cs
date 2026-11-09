#nullable enable
// Port of src/core/session.ts (web repo, source of truth). Behaviour follows the TS file line by line.
//
// `GameSession` (docs/GDD.md K-39, K-43, K-29, K-28, K-40; TECH_DESIGN §1.2, §6.1, §11.1): one attempt at one level.
// Pure (no clock, no storage): the scene commits actions, the save service stores the action log after each one.
//
// - State + move log: `log[0]` is the `start` action (pre-level boosters, streak tier); then every committed drag,
//   trowel use, accepted +5 offer and undo, in order (TECH §11.1 `InLevel.actions`). Cancelled drags and rejected
//   boosters change nothing and are not logged.
// - K-39 Undo: before each action the buffer is copied; the copy is the K-30 Söküm target of that action
//   (`ApplyOptions.PreAction`, no second copy) and, for a drag, the Undo snapshot. Undo restores it IN PLACE (the scene
//   keeps its state reference), so counter, `m`, `movesSpent` (−1), timers, streak, trowels, deliveries and queue, segment
//   shift, goals — and a truck help or Söküm of that move's step 12 (E-37) — all come back. Depth 1; not after a booster /
//   trowel / +5 offer; not while the out-of-moves window is open (Boosters.UndoBlock).
// - `Apply(action)` (TECH §2R.15): one package per action — the events in step order (a Söküm's `teardown` last), the
//   `teardown` short cut and the `TurnSummary` of the state after it (Summary.cs).
// - K-43 resume: `GameSession.Replay(level, log)` rebuilds the attempt deterministically, bit for bit (E-38), with the
//   same undo snapshot, offer count and out-of-moves window (UX §1 (a)). A replay that does not apply throws
//   `ReplayError` (the caller voids the attempt, like a level hash / rules version mismatch, K-43 item 4).
// - K-29: when a move ends with 0 moves and no win, the window offers `n = offersUsed + 1` (n ≤ 3; the ad option only
//   for n = 1). Accepting runs `addMoves` (+5, step 12 once); declining loses the level. With 3 offers used the level is
//   lost at once (no window).
// - K-43 exit: only with the player's confirmation; `movesSpent ≥ 1` is a loss, `movesSpent = 0` is free (life and
//   pre-level boosters are refunded by meta, the streak bonus is not consumed, E-41). Faz 2R: `movesSpent`, not `m`
//   (a Söküm gives `m` back but never `movesSpent`).
// - Attempt counting (`levels[id].attempts`, K-43: a resume is the same attempt) lives in the save service;
//   `Resumed` tells the caller which case it is.
using System;
using System.Collections.Generic;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>Win-streak level-start bonus of a tier (amounts: config/economy.json `winStreak`, META; read by meta).</summary>
    public readonly struct StreakBonus
    {
        public readonly int Moves;
        public readonly int Trowels;

        public StreakBonus(int moves, int trowels)
        {
            Moves = moves;
            Trowels = trowels;
        }
    }

    public sealed class SessionOptions
    {
        public static readonly SessionOptions Default = new SessionOptions();

        public readonly MoveHooks? Hooks;
        /// <summary>
        /// Bonus of streak tier 1–3. Core cannot read config/economy.json, so the caller passes it; the default throws for a
        /// tier above 0 (a silent zero would hide a wiring bug).
        /// </summary>
        public readonly Func<int, StreakBonus>? StreakBonus;
        /// <summary>Invalid move records throw (development) instead of becoming `moveCancelled{reason: 'invalid'}`.</summary>
        public readonly bool Strict;

        public SessionOptions(MoveHooks? hooks = null, Func<int, StreakBonus>? streakBonus = null, bool strict = false)
        {
            Hooks = hooks;
            StreakBonus = streakBonus;
            Strict = strict;
        }
    }

    /// <summary>The +5 offer the out-of-moves window shows (K-29).</summary>
    public readonly struct OfferInfo : IEquatable<OfferInfo>
    {
        /// <summary>Offer number of this attempt (1–3); the price step is `offerCosts[n − 1]` (meta).</summary>
        public readonly int N;
        /// <summary>The ad option exists only on the first offer of an attempt.</summary>
        public readonly bool AdAllowed;

        public OfferInfo(int n, bool adAllowed)
        {
            N = n;
            AdAllowed = adAllowed;
        }

        public bool Equals(OfferInfo o) => N == o.N && AdAllowed == o.AdAllowed;
        public override bool Equals(object? obj) => obj is OfferInfo o && Equals(o);
        public override int GetHashCode() => N * 2 + (AdAllowed ? 1 : 0);
        public override string ToString() => "offer " + N + (AdAllowed ? " (ad allowed)" : "");
    }

    public static class ExitKinds
    {
        public const string Free = "free";
        public const string Loss = "loss";
    }

    public sealed class ExitResult
    {
        /// <summary>`free`: `movesSpent` = 0 (K-43 item 2, E-41); `loss`: `movesSpent` ≥ 1.</summary>
        public readonly string Kind;
        public readonly int MovesMade;
        /// <summary>K-43 item 2 (Faz 2R): the counter the penalty reads.</summary>
        public readonly int MovesSpent;
        /// <summary>Free exit: pre-level boosters to give back (the reserved life is meta's). Empty on a loss.</summary>
        public readonly string[] RefundPreBoosters;
        /// <summary>The win-streak bonus counts as used (false on a free exit: given again next time).</summary>
        public readonly bool StreakBonusConsumed;

        public ExitResult(string kind, int movesMade, int movesSpent, string[] refundPreBoosters, bool streakBonusConsumed)
        {
            Kind = kind;
            MovesMade = movesMade;
            MovesSpent = movesSpent;
            RefundPreBoosters = refundPreBoosters;
            StreakBonusConsumed = streakBonusConsumed;
        }
    }

    /// <summary>A logged action that does not replay (K-43): the attempt cannot be rebuilt.</summary>
    public sealed class ReplayError : Exception
    {
        public readonly int Index;

        public ReplayError(int index, string message) : base("replay: action " + index + ": " + message)
        {
            Index = index;
        }
    }

    /// <summary>
    /// One action package (TECH §2R.15 item 3, DL-2R-15/3): the result, the events in step order (a Söküm's `teardown` is
    /// the last one), the `teardown` short cut (the scene skips the correct-placement rewards when it is set, JUICE §0
    /// rule 14) and the summary of the state after the action.
    /// </summary>
    public sealed class ActionResult : MoveResult
    {
        public readonly GameEvent[] Events;
        public readonly TeardownEvent? Teardown;
        public readonly TurnSummary Summary;

        public ActionResult(MoveResult res, GameEvent[] events, TeardownEvent? teardown, TurnSummary summary)
            : base(res.Status, res.Reason, res.Won, res.OutOfMoves, res.TeardownCause)
        {
            Events = events;
            Teardown = teardown;
            Summary = summary;
        }
    }

    /// <summary>TS `tee` sink of `GameSession.apply`: every event goes to the package and to the caller's sink.</summary>
    internal sealed class TeeSink : IEventSink
    {
        private readonly IEventSink a;
        private readonly IEventSink b;

        public TeeSink(IEventSink a, IEventSink b)
        {
            this.a = a;
            this.b = b;
        }

        public bool Enabled => true;

        public void Push(GameEvent e)
        {
            a.Push(e);
            b.Push(e);
        }
    }

    public sealed class GameSession
    {
        /// <summary>
        /// Core rules version (K-43 item 4): bump it whenever a rule change can change the replay of a saved log. 2 = Faz 2R
        /// (full cover K-47/K-48, sizes K-49, Söküm K-30, new boosters, Zobrist frame positions; TECH §2R.0 item 4). 3 = D-099
        /// (K-55 item 8, K-30: a D1 the rules cannot lift is a Söküm with cause `access`, never a reshuffle; after an accepted
        /// +5 offer the D1 Söküm goes back to the start of the last action). An attempt saved with 2 is voided (E-67).
        /// </summary>
        public const int RulesVersion = 4;
        /// <summary>K-29: moves of an accepted offer.</summary>
        public const int OfferMoves = 5;
        /// <summary>K-29: offers per attempt (ad or coins).</summary>
        public const int MaxOffers = 3;
        /// <summary>K-40 Termos: extra moves.</summary>
        public const int ThermosMoves = 3;
        /// <summary>K-40 Mala Başlangıcı: extra trowels.</summary>
        public const int TrowelStart = 1;
        /// <summary>K-40 Açık Kepenk: W4 / W7 count as open while `m &lt; 5`.</summary>
        public const int OpenShutterMoves = 5;

        private static readonly MoveResult NotPlaying = new MoveResult(MoveStatuses.Rejected, "notPlaying", false, false);

        public readonly CompiledLevel Lvl;
        /// <summary>Built by `Replay` (K-43 resume: the same attempt, `attempts` does not grow).</summary>
        public readonly bool Resumed;
        public readonly string[] PreBoosters;
        /// <summary>0 … 3.</summary>
        public readonly int StreakTier;

        private readonly GameState state;
        private readonly List<SessionAction> log;
        private readonly SessionOptions opts;
        /// <summary>Pre-move buffer of the last committed drag while it may be undone (K-39).</summary>
        private int[]? undoSnapshot;
        /// <summary>Start buffer of the last committed action (drag, booster, trowel): the D1 Söküm target after a +5 offer (K-29).</summary>
        private int[]? lastPre;
        private string outcome = BoosterOutcomes.Playing;
        private int offersUsed;
        private bool adOfferUsed;

        private GameSession(CompiledLevel lvl, StartAction start, SessionOptions opts, bool resumed, IEventSink sink)
        {
            Lvl = lvl;
            Resumed = resumed;
            this.opts = opts;
            PreBoosters = (string[])start.PreBoosters.Clone();
            StreakTier = start.StreakTier;
            state = CreateInitialState(lvl);
            log = new List<SessionAction> { SessionActionJson.Clone(start) };
            ApplyStart(sink);
        }

        /// <summary>A new attempt (K-40 bonuses applied: Termos, streak moves, trowels, Open Shutter).</summary>
        public static GameSession Start(CompiledLevel lvl, string[]? preBoosters = null, int streakTier = 0, SessionOptions? opts = null, IEventSink? sink = null)
        {
            var action = new StartAction(preBoosters != null ? (string[])preBoosters.Clone() : new string[0], streakTier);
            return new GameSession(lvl, action, opts ?? SessionOptions.Default, false, sink ?? NullSink.Instance);
        }

        /// <summary>`GameSession.replay` observer: called after action `index` of the log applied (`start` = index 0). TS `ReplayStep`.</summary>
        public delegate void ReplayStep(int index, SessionAction action, GameSession session);

        /// <summary>
        /// K-43 resume: rebuilds the attempt from its action log. Throws `ReplayError` when it does not apply. `sink` receives
        /// the events of every replayed action in log order (start included): the caller rebuilds per-attempt counters from
        /// them (`level_end.wrongPlacements`, `truckHelps`; review Faz 2 tur 1 #19). `step` is called after every action
        /// (`start` included, index 0) with the session as it is right then: the scene rebuilds its tutorial step from the log,
        /// each move seen with the state of its time (review Faz 2 tur 2 #8).
        /// </summary>
        public static GameSession Replay(CompiledLevel lvl, IReadOnlyList<SessionAction> log, SessionOptions? opts = null, IEventSink? sink = null, ReplayStep? step = null)
        {
            opts ??= SessionOptions.Default;
            sink ??= NullSink.Instance;
            if (log.Count == 0 || !(log[0] is StartAction first)) throw new ReplayError(0, "the log must start with a start action");
            var session = new GameSession(lvl, first, opts, true, sink);
            step?.Invoke(0, first, session);
            for (int i = 1; i < log.Count; i++)
            {
                var a = log[i];
                if (a is StartAction) throw new ReplayError(i, "a second start action");
                if (a is UndoAction)
                {
                    if (!session.Undo()) throw new ReplayError(i, "undo is not available (" + session.UndoBlock() + ")");
                }
                else if (a is AddMovesMove add)
                {
                    if (add.Source != AddMovesSource.OfferCoins && add.Source != AddMovesSource.OfferAd)
                        throw new ReplayError(i, "addMoves from " + add.Source + " belongs to the start action");
                    if (add.Amount != OfferMoves) throw new ReplayError(i, "an offer adds " + OfferMoves + " moves");
                    var res = session.AcceptOffer(add.Source, sink);
                    if (res.Status != MoveStatuses.Applied) throw new ReplayError(i, "offer not accepted (" + res.Reason + ")");
                }
                else
                {
                    var res = session.Commit((Move)a, sink);
                    if (res.Status != MoveStatuses.Applied) throw new ReplayError(i, a.Kind + " did not apply (" + res.Reason + ")");
                }
                step?.Invoke(i, a, session);
            }
            return session;
        }

        /// <summary>The live state. Read it; never change it outside the session (the scene only replays events, TECH §1.4).</summary>
        public GameState State => state;

        /// <summary>The action log (a copy), `start` first (TECH §11.1 `InLevel.actions`).</summary>
        public List<SessionAction> Log
        {
            get
            {
                var o = new List<SessionAction>(log.Count);
                foreach (var a in log) o.Add(SessionActionJson.Clone(a));
                return o;
            }
        }

        /// <summary>GDD `m`: completed moves (`level_resume.movesMade`); a Söküm gives it back.</summary>
        public int MovesMade => Hdr(state, H.Turn);

        /// <summary>GDD `movesSpent` (K-43 item 2): non-cancelled drag moves of this attempt; Undo −1, a Söküm keeps it.</summary>
        public int MovesSpent => Hdr(state, H.MovesSpent);

        /// <summary>The level's rule hooks (registry, or the options' hooks).</summary>
        public MoveHooks Hooks => opts.Hooks ?? Registry.LevelHooks(Lvl);

        /// <summary>TECH §2R.15: the summary of the current state (bölüm başı and after every package).</summary>
        public TurnSummary Summary() => Core.Summary.TurnSummary(state, Hooks, CanUndo());

        /// <summary>
        /// TECH §2R.15 `GameSession.apply(action) → ActionResult`: a drag, a booster, the Golden Trowel, Undo or an accepted +5
        /// offer, as one package. Events also go to `sink`. A refused / cancelled action changes nothing (`Status`).
        /// </summary>
        public ActionResult Apply(SessionAction action, IEventSink? sink = null)
        {
            sink ??= NullSink.Instance;
            var own = new ArraySink();
            IEventSink tee = !sink.Enabled ? (IEventSink)own : new TeeSink(own, sink);
            MoveResult res;
            if (action is UndoAction)
            {
                bool ok = Undo();
                res = ok
                    ? new MoveResult(MoveStatuses.Applied, null, false, false)
                    : new MoveResult(NotPlaying.Status, UndoBlock() ?? UndoBlocks.NoDragMove, NotPlaying.Won, NotPlaying.OutOfMoves);
            }
            else if (action is AddMovesMove add)
            {
                if (add.Source != AddMovesSource.OfferCoins && add.Source != AddMovesSource.OfferAd)
                    throw new InvalidOperationException("GameSession.apply: start bonuses belong to GameSession.start (K-40)");
                res = AcceptOffer(add.Source, tee);
            }
            else if (action is Move move)
            {
                res = Commit(move, tee);
            }
            else
            {
                throw new InvalidOperationException("GameSession.apply: a start action is not an input (K-40)");
            }
            var events = own.Events.ToArray();
            var last = events.Length > 0 ? events[events.Length - 1] : null;
            var teardown = last as TeardownEvent;
            return new ActionResult(res, events, teardown, Summary());
        }

        public int MovesLeft => Hdr(state, H.MovesLeft);

        /// <summary>`playing` · `outOfMoves` (K-29 window open) · `won` (K-28) · `lost` (declined / no offer left / exit with m ≥ 1) · `exited` (free exit at m = 0). `BoosterOutcomes` values.</summary>
        public string Outcome => outcome;

        /// <summary>K-29 offers accepted in this attempt (ad or coins).</summary>
        public int OffersUsed => offersUsed;

        public bool AdOfferUsed => adOfferUsed;

        /// <summary>K-46 YAO of this attempt (`level_end`).</summary>
        public YaoMeasure Yao() => Moves.MeasureYao(state);

        /// <summary>K-39 precondition (`canUseUndo`): null when Undo may be used (`UndoBlocks` value otherwise).</summary>
        public string? UndoBlock() => Boosters.UndoBlock(new UndoView(outcome, undoSnapshot != null));

        public bool CanUndo() => UndoBlock() == null;

        /// <summary>The offer of the open out-of-moves window (K-29); null when no window is open or 3 offers were used.</summary>
        public OfferInfo? NextOffer()
        {
            if (outcome != BoosterOutcomes.OutOfMoves || offersUsed >= MaxOffers) return null;
            return new OfferInfo(offersUsed + 1, offersUsed == 0);
        }

        /// <summary>
        /// Commits a drag, a booster (hammer, crane, paint brush) or the Golden Trowel. Cancelled / rejected moves change
        /// nothing and are not logged; a won level or an open out-of-moves window rejects every move. +5 offers go through
        /// `AcceptOffer`.
        /// </summary>
        public MoveResult Commit(Move move, IEventSink? sink = null)
        {
            if (move is AddMovesMove) throw new InvalidOperationException("GameSession.commit: use acceptOffer for +5 offers (K-29)");
            if (outcome != BoosterOutcomes.Playing) return NotPlaying;
            var snapshot = (int[])state.Buf.Clone();
            var res = Moves.ApplyMove(state, move, sink ?? NullSink.Instance, ApplyOptionsOf().WithPreAction(snapshot));
            if (res.Status != MoveStatuses.Applied) return res;
            log.Add(SessionActionJson.Clone(move));
            undoSnapshot = move is DragMove ? snapshot : null; // K-39: only a drag move is undone
            lastPre = snapshot;
            if (res.Won) outcome = BoosterOutcomes.Won;
            else if (res.OutOfMoves) outcome = offersUsed >= MaxOffers ? BoosterOutcomes.Lost : BoosterOutcomes.OutOfMoves;
            if (outcome == BoosterOutcomes.Lost) MarkLost();
            return res;
        }

        /// <summary>K-39: restores the state before the last drag (in place). Returns false when Undo is not available.</summary>
        public bool Undo()
        {
            var snapshot = undoSnapshot;
            if (UndoBlock() != null || snapshot == null) return false;
            Array.Copy(snapshot, state.Buf, snapshot.Length);
            undoSnapshot = null;
            lastPre = null;
            log.Add(new UndoAction());
            return true;
        }

        /// <summary>K-29: accepts offer `n` of the open window (+5 moves, step 12 once). Rejected when not allowed. `source`: offerCoins | offerAd.</summary>
        public MoveResult AcceptOffer(string source, IEventSink? sink = null)
        {
            var offer = NextOffer();
            if (!offer.HasValue) return new MoveResult(NotPlaying.Status, "noOffer", false, false);
            if (source == AddMovesSource.OfferAd && !offer.Value.AdAllowed) return new MoveResult(NotPlaying.Status, "adNotAllowed", false, false);
            var move = new AddMovesMove(OfferMoves, source);
            var applyOpts = ApplyOptionsOf();
            var pre = lastPre;
            var res = Moves.ApplyMove(state, move, sink ?? NullSink.Instance, pre != null ? applyOpts.WithPreAction(pre) : applyOpts);
            if (res.Status != MoveStatuses.Applied) return res;
            log.Add(move);
            lastPre = null;
            offersUsed += 1;
            if (source == AddMovesSource.OfferAd) adOfferUsed = true;
            undoSnapshot = null; // K-39: no Undo across a +5 offer
            outcome = BoosterOutcomes.Playing;
            return res;
        }

        /// <summary>K-29: the player declines the offer: the level is lost.</summary>
        public void DeclineOffer()
        {
            if (outcome != BoosterOutcomes.OutOfMoves)
                throw new InvalidOperationException("declineOffer: no out-of-moves window (" + outcome + ")");
            outcome = BoosterOutcomes.Lost;
            MarkLost();
        }

        /// <summary>K-43 item 2: confirmed exit. `movesSpent ≥ 1` → loss; `movesSpent = 0` → free (refunds, streak bonus kept).</summary>
        public ExitResult Exit()
        {
            if (outcome != BoosterOutcomes.Playing) throw new InvalidOperationException("exit: the attempt is not running (" + outcome + ")");
            int movesMade = MovesMade;
            int movesSpent = MovesSpent;
            if (movesSpent >= 1)
            {
                outcome = BoosterOutcomes.Lost;
                MarkLost();
                return new ExitResult(ExitKinds.Loss, movesMade, movesSpent, new string[0], true);
            }
            outcome = BoosterOutcomes.Exited;
            return new ExitResult(ExitKinds.Free, movesMade, movesSpent, (string[])PreBoosters.Clone(), false);
        }

        private ApplyOptions ApplyOptionsOf() => new ApplyOptions(opts.Hooks, false, opts.Strict);

        private void MarkLost() => SetHdr(state, H.Flags, Hdr(state, H.Flags) | StateFlag.Lost);

        private static StreakBonus NoStreakBonus(int tier) =>
            throw new InvalidOperationException("GameSession: streak tier " + tier + " needs options.streakBonus (META win-streak amounts)");

        private bool HasPreBooster(string name) => Array.IndexOf(PreBoosters, name) >= 0;

        /// <summary>K-40 level start: Termos, streak bonus, Mala Başlangıcı, Açık Kepenk (no step 12, no timers, `m` stays 0).</summary>
        private void ApplyStart(IEventSink sink)
        {
            var s = state;
            var o = ApplyOptionsOf();
            int trowels = 0;
            if (HasPreBooster(Core.PreBoosters.Thermos))
                Moves.ApplyMove(s, new AddMovesMove(ThermosMoves, AddMovesSource.Thermos), sink, o);
            if (StreakTier > 0)
            {
                var bonus = (opts.StreakBonus ?? NoStreakBonus)(StreakTier);
                if (bonus.Moves > 0)
                    Moves.ApplyMove(s, new AddMovesMove(bonus.Moves, AddMovesSource.Streak), sink, o);
                trowels += bonus.Trowels;
            }
            if (HasPreBooster(Core.PreBoosters.TrowelStart)) trowels += TrowelStart;
            if (trowels > 0) Combo.GrantTrowels(s, trowels);
            if (HasPreBooster(Core.PreBoosters.OpenShutter)) SetHdr(s, H.OpenShutterUntil, OpenShutterMoves);
        }

        /// <summary>
        /// K-43 item 4 `levelHash` (TECH §11.1): FNV-1a 64 of the canonical JSON of the level data, 16 hex digits. Hash the
        /// level as loaded (`CompiledLevel.Data`, a `LevelData`) at attempt start and at resume; any JSON value is accepted so
        /// tools and tests can hash raw data too. (`LevelJson.LevelHash(LevelData)` is the typed shortcut.)
        /// </summary>
        public static string LevelHash(JsonValue data) => Moves.Fnv1a64(Moves.CanonicalJson(data));

        public static string LevelHash(LevelData data) => LevelHash(data.ToJson());
    }
}
