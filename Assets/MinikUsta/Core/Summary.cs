#nullable enable
// Port of src/core/summary.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>One truck queue entry (K-26), FIFO order.</summary>
    public sealed class QueueEntry : IEquatable<QueueEntry>
    {
        public readonly int PieceId;
        public readonly string Shape;
        public readonly string Color;

        public QueueEntry(int pieceId, string shape, string color)
        {
            PieceId = pieceId;
            Shape = shape;
            Color = color;
        }

        public bool Equals(QueueEntry? o) => o != null && PieceId == o.PieceId && Shape == o.Shape && Color == o.Color;
        public override bool Equals(object? obj) => obj is QueueEntry q && Equals(q);
        public override int GetHashCode() => PieceId;
        public override string ToString() => PieceId + ":" + Shape + " " + Color;
    }

    /// <summary>Undelivered batches and their material blocks (zone `pending`).</summary>
    public readonly struct PendingCounts : IEquatable<PendingCounts>
    {
        public readonly int Batches;
        public readonly int Blocks;

        public PendingCounts(int batches, int blocks)
        {
            Batches = batches;
            Blocks = blocks;
        }

        public bool Equals(PendingCounts o) => Batches == o.Batches && Blocks == o.Blocks;
        public override bool Equals(object? obj) => obj is PendingCounts p && Equals(p);
        public override int GetHashCode() => Batches * 31 + Blocks;
        public override string ToString() => Batches + " batches, " + Blocks + " blocks";
    }

    /// <summary>The state after an action (and its Söküm, if any), for the scene (TECH §2R.15).</summary>
    public sealed class TurnSummary
    {
        /// <summary>K-09 (a)–(e) holdable blocks, id order (UX §5.3, ART §3A.2).</summary>
        public readonly int[] Holdable;
        /// <summary>K-26 truck queue, FIFO order (DL-2R-22 preview).</summary>
        public readonly QueueEntry[] Queue;
        /// <summary>Undelivered truck batches (UX §5.9).</summary>
        public readonly int PendingBatches;
        /// <summary>Material blocks of the undelivered batches (UX §5.9 item 2 "+n").</summary>
        public readonly int PendingBlocks;
        /// <summary>N − correctly placed material blocks (K-48, §2R.2).</summary>
        public readonly int BlocksLeft;
        /// <summary>K-34 hook 5 (access, wall and gaps ignored).</summary>
        public readonly int[] NeededNow;
        /// <summary>UX §5.9 item 3 "next floor" badge.</summary>
        public readonly int[] CarryIds;
        /// <summary>K-33 / UX §5.2: yard blocks with a non-empty `P` (pre-check excluded).</summary>
        public readonly int[] TrowelPieces;
        /// <summary>K-54 item 2.</summary>
        public readonly BoosterTargets BoosterTargets;

        public TurnSummary(int[] holdable, QueueEntry[] queue, int pendingBatches, int pendingBlocks, int blocksLeft, int[] neededNow,
            int[] carryIds, int[] trowelPieces, BoosterTargets boosterTargets)
        {
            Holdable = holdable;
            Queue = queue;
            PendingBatches = pendingBatches;
            PendingBlocks = pendingBlocks;
            BlocksLeft = blocksLeft;
            NeededNow = neededNow;
            CarryIds = carryIds;
            TrowelPieces = trowelPieces;
            BoosterTargets = boosterTargets;
        }
    }

    /// <summary>
    /// "Before" of `UnlockedNeeded`: per needed block of the previous state, whether its reach `R` held a release giving a
    /// correct placement. The scene keeps one per attempt and calls `UnlockedNeeded` after every action package (and once
    /// at level start: that first call only fills the cache).
    /// </summary>
    public sealed class ReachCache
    {
        private bool primed;
        private Dictionary<int, bool> reach = new Dictionary<int, bool>();

        public bool Primed => primed;

        /// <summary>Last known reach flag of `id`; null when it was not needed then.</summary>
        public bool? Reachable(int id) => reach.TryGetValue(id, out bool v) ? v : (bool?)null;

        internal void Store(Dictionary<int, bool> next)
        {
            reach = next;
            primed = true;
        }

        /// <summary>Forget everything (new attempt, Undo / Söküm packages may call it; the next call then only primes).</summary>
        public void Reset()
        {
            reach = new Dictionary<int, bool>();
            primed = false;
        }
    }

    /// <summary>K-54 slot states, in priority order. TS `SLOT_STATES`.</summary>
    public static class SlotStates
    {
        public const string Locked = "locked";
        public const string NoTarget = "noTarget";
        public const string Empty = "empty";
        public const string Ready = "ready";

        public static readonly string[] All = { Locked, NoTarget, Empty, Ready };
    }

    /// <summary>
    /// Presentation hooks of the core (docs/TECH_DESIGN.md §2R.15; GDD K-09, K-26, K-34 hook 5, K-48, K-54; UX §5.2, §5.3,
    /// §5.9; DL-2R-15). Pure, read-only queries over the state after an action: they never change it and never enter the
    /// event log or the replay. `GameSession.apply` hands one `TurnSummary` with every action package (core/session.ts).
    ///
    /// - `HoldableIds`: K-09 (a)–(e) blocks (glow texture vs `notHoldableTint`, UX §5.3; the tutorial glove).
    /// - `QueueEntries`: the truck queue in FIFO order with shape and colour (K-26; truck chip preview, DL-2R-22).
    /// - `PendingCounts`: undelivered batches and their material blocks (UX §5.9 "+n" badge).
    /// - `Goals.BlocksLeft`: N − correctly placed material blocks (K-48, UX §5.9 item 1).
    /// - `Deadlock.NeededNow` (K-34 hook 5) and `UnlockedNeeded` (one frame later, easy / normal only, `ReachCache`).
    /// - `CarryIds`: yard material blocks bigger than what their colour still needs on the active segment (UX §5.9 item 3).
    /// - `Boosters.TrowelPieces` (K-33 `P` non-empty, UX §5.2) and `Boosters.BoosterTargets` (K-54 item 2), `SlotState` (K-54 order).
    /// </summary>
    public static class Summary
    {
        /// <summary>K-09 (a)–(e): every block on the board (yard, visible segment) that can be held now, id order.</summary>
        public static int[] HoldableIds(GameState s, MoveHooks? hooks = null)
        {
            hooks ??= MoveHooks.None;
            var o = new List<int>();
            if (Hdr(s, H.MovesLeft) <= 0) return o.ToArray(); // K-09 (e)
            int P = s.Lvl.Layout.Counts.Pieces;
            int seg = Grid.VisibleSegment(s);
            for (int id = 0; id < P; id++)
            {
                int zone = PieceZone(s, id);
                if (zone != Zone.Yard && !(zone == Zone.Site && PieceSeg(s, id) == seg)) continue;
                if (Deadlock.QuickHoldable(s, id, hooks.Drag) || Movement.TryBeginDrag(s, id, hooks.Drag).Ok) o.Add(id);
            }
            return o.ToArray();
        }

        /// <summary>K-26: the queue with shape and colour, oldest first.</summary>
        public static QueueEntry[] QueueEntries(GameState s)
        {
            var ids = QueueIds(s);
            var o = new QueueEntry[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];
                o[i] = new QueueEntry(id, Shapes.ShapeByIndex(PieceShape(s, id)).Id, Boosters.ColorCodeOf(PieceColor(s, id)));
            }
            return o;
        }

        /// <summary>Undelivered batches and their material blocks (zone `pending`).</summary>
        public static PendingCounts PendingCounts(GameState s)
        {
            int batches = 0;
            int blocks = 0;
            foreach (var b in s.Lvl.Batches)
            {
                if (b.Index < 1) continue;
                int waiting = 0;
                foreach (int id in b.PieceIds)
                {
                    if (PieceZone(s, id) != Zone.Pending) continue;
                    waiting++;
                    if (Goals.IsMaterial(s, id)) blocks++;
                }
                if (waiting > 0) batches += 1;
            }
            return new PendingCounts(batches, blocks);
        }

        /// <summary>
        /// UX §5.9 item 3 (`carryIds`): yard material blocks whose cell count is larger than the cells of their colour still
        /// open on the active segment ("for the next floor"; every difficulty).
        /// </summary>
        public static int[] CarryIds(GameState s)
        {
            var o = new List<int>();
            int seg = Grid.VisibleSegment(s);
            if (seg < 0 || seg >= s.Lvl.Segments.Length) return o.ToArray();
            var plan = s.Lvl.Segments[seg];
            int ws = s.Lvl.Geo.Ws;
            int hs = s.Lvl.Geo.Hs;
            var open = new int[ColorCodes.All.Length];
            for (int sx = 0; sx < ws; sx++)
            {
                int filled = FilledMask(s, seg, sx);
                for (int sy = 0; sy < hs; sy++)
                {
                    int c = plan.PlanColors[sy * ws + sx];
                    if (c >= 0 && ((filled >> sy) & 1) == 0) open[c] += 1;
                }
            }
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
            {
                if (PieceZone(s, id) != Zone.Yard || !Goals.IsMaterial(s, id)) continue;
                if ((PieceFlagBits(s, id) & FlagBit.Locked) != 0) continue;
                int c = PieceColor(s, id);
                int openCells = c >= 0 && c < open.Length ? open[c] : 0;
                if (Shapes.ShapeByIndex(PieceShape(s, id)).CellCount > openCells) o.Add(id);
            }
            return o.ToArray();
        }

        /// <summary>The whole summary (≤ 20 blocks × one BFS each; µs to &lt; 1 ms). `hooks` = `LevelHooks(lvl)`; `undoable` K-39.</summary>
        public static TurnSummary TurnSummary(GameState s, MoveHooks hooks, bool undoable)
        {
            var pending = PendingCounts(s);
            return new TurnSummary(
                HoldableIds(s, hooks),
                QueueEntries(s),
                pending.Batches,
                pending.Blocks,
                Goals.BlocksLeft(s),
                Deadlock.NeededNow(s),
                CarryIds(s),
                Boosters.TrowelPieces(s, hooks),
                Boosters.BoosterTargets(s, hooks, undoable));
        }

        // --- K-34 hook 5: unlockedNeeded --------------------------------------------------------------------------------------

        /// <summary>
        /// GDD K-34 hook 5: blocks of `NeededNow` (of `s`) whose `R` now holds a release with a correct landing and did not
        /// before the action. "Before" comes from `cache` (a block not needed before had no correct spot at all, so no correct
        /// release). Easy and normal levels only (hard / superhard: always empty, nothing computed). One BFS per needed block;
        /// the scene calls it in the frame after it starts playing the package. `prev` = the previous package's summary.
        /// </summary>
        public static int[] UnlockedNeeded(TurnSummary? prev, GameState s, ReachCache cache, MoveHooks? hooks = null)
        {
            hooks ??= MoveHooks.None;
            string diff = s.Lvl.Difficulty;
            if (diff == "hard" || diff == "superhard") return new int[0];
            var needed = Deadlock.NeededNow(s);
            var next = new Dictionary<int, bool>();
            foreach (int id in needed) next[id] = Deadlock.HasReachableCorrect(s, id, hooks.Drag, hooks.Fall);
            bool primed = cache.Primed;
            var o = new List<int>();
            if (primed)
            {
                foreach (int id in needed)
                {
                    if (next[id] != true) continue;
                    bool? before = cache.Reachable(id);
                    if (before == null) before = prev != null && Array.IndexOf(prev.NeededNow, id) >= 0 ? (bool?)null : false;
                    if (before == false) o.Add(id);
                }
            }
            cache.Store(next);
            return o.ToArray();
        }

        // --- K-54 slot state ----------------------------------------------------------------------------------------------

        /// <summary>K-54: locked &gt; noTarget &gt; empty &gt; ready; the first that holds.</summary>
        public static string SlotState(bool unlocked, bool hasTarget, int count)
        {
            if (!unlocked) return SlotStates.Locked;
            if (!hasTarget) return SlotStates.NoTarget;
            if (count <= 0) return SlotStates.Empty;
            return SlotStates.Ready;
        }

        /// <summary>K-54 / BUSINESS E12: the "+" and the purchase window (and `offer_shown`) exist only for an `empty` slot.</summary>
        public static bool PurchaseOffered(string state) => state == SlotStates.Empty;
    }
}
