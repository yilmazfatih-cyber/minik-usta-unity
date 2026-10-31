#nullable enable
// Port of src/core/delivery.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>One block the truck dropped (`pieceFell{cause: 'delivery'}`).</summary>
    public sealed class Delivered : IEquatable<Delivered>
    {
        public readonly int PieceId;
        /// <summary>Drop start: (column, (H + 2) − h).</summary>
        public readonly Anchor From;
        public readonly Anchor To;
        public readonly int Rows;

        public Delivered(int pieceId, Anchor from, Anchor to, int rows)
        {
            PieceId = pieceId;
            From = from;
            To = to;
            Rows = rows;
        }

        public bool Equals(Delivered? o) => o != null && PieceId == o.PieceId && From.Equals(o.From) && To.Equals(o.To) && Rows == o.Rows;
        public override bool Equals(object? obj) => obj is Delivered d && Equals(d);
        public override int GetHashCode() => PieceId * 31 + Rows;
        public override string ToString() => "piece " + PieceId + " " + From + "→" + To + " rows " + Rows;
    }

    public sealed class DeliveryResult
    {
        /// <summary>Delivered blocks in FIFO order.</summary>
        public readonly Delivered[] Delivered;
        /// <summary>Pieces still in the truck ("Kamyonda: N", K-26).</summary>
        public readonly int Queued;

        public DeliveryResult(Delivered[] delivered, int queued)
        {
            Delivered = delivered;
            Queued = queued;
        }
    }

    /// <summary>
    /// Truck batches and the FIFO truck queue (docs/GDD.md K-25, K-26, K-27, K-17 step 3, E-03, E-04, E-34; TECH_DESIGN
    /// §6.2 steps 8–9).
    ///
    /// - Batch 0 starts in the yard (compile). Batch k ≥ 1 is queued — its pieces appended to the END of the truck queue in
    ///   array order — when its segment comes (`forSegment = k` once k segments are complete, K-35 step 8). Nothing is
    ///   delivered in step 8.
    /// - Step 9 is the single delivery point (the K-30 D2 help of step 12 is the only written exception, Phase 3): every
    ///   queued piece is tried once, oldest first; a piece that finds no room stays queued in its order and does not hold
    ///   back the later ones (K-26).
    /// - Columns of one piece (K-25): (1) its `x`; (2) its batch's `dropColumns` in list order (columns already tried are
    ///   skipped); (3) every other valid anchor column `0 … wy − w`, nearest to `x` first, ties to the larger x (nearer the
    ///   wall). The last stage is never skipped (E-34). In each column the block drops from y = (H + 2) − h onto its first
    ///   support whatever the gravity settings; the first column where every landed cell has y ≤ Hy − 1 wins (yard air is
    ///   no landing, E-57). The drop moves no other block and triggers no neighbour effect; balloons do not rise (E-36).
    /// - Bounced blocks queued by K-17 step 3 keep their (clamped) start column as `x` and join the same FIFO.
    /// </summary>
    public static class Delivery
    {
        /// <summary>Truck batches (k ≥ 1) that come with segment `completed` (= number of completed segments, K-22, K-23, K-25).</summary>
        public static CompiledBatch[] BatchesFor(GameState s, int completed)
        {
            var o = new List<CompiledBatch>();
            foreach (var b in s.Lvl.Batches) if (b.Index >= 1 && b.ForSegment == completed) o.Add(b);
            return o.ToArray();
        }

        /// <summary>
        /// K-35 step 8: appends the pieces of the batches for `completed` to the end of the truck queue, in batch then array
        /// order (zone `pending` → `queue`). Returns the queued piece ids.
        /// </summary>
        public static int[] EnqueueBatchesFor(GameState s, int completed)
        {
            var o = new List<int>();
            foreach (var batch in BatchesFor(s, completed))
            {
                foreach (int id in batch.PieceIds)
                {
                    Placement.MovePiece(s, id, PiecePlace.Queue(PieceX(s, id), s.Lvl.Geo.Hy));
                    o.Add(id);
                }
            }
            return o.ToArray();
        }

        /// <summary>`dropColumns` of the truck batch a piece came with (none for batch 0, debris and help pieces).</summary>
        private static int[] BatchDropColumns(GameState s, int id)
        {
            if (id < 0 || id >= s.Lvl.Pieces.Length) return new int[0];
            var piece = s.Lvl.Pieces[id];
            if (piece.Origin != PieceOrigins.Truck) return new int[0];
            return piece.Batch >= 0 && piece.Batch < s.Lvl.Batches.Length ? s.Lvl.Batches[piece.Batch].DropColumns : new int[0];
        }

        /// <summary>K-25 candidate anchor columns of a piece, in trial order, without repeats (invalid columns included in stage 1–2).</summary>
        public static int[] DeliveryColumns(GameState s, int id)
        {
            int w = Shapes.ShapeByIndex(PieceShape(s, id)).W;
            int x = PieceX(s, id);
            var o = new List<int> { x };
            foreach (int c in BatchDropColumns(s, id)) if (!o.Contains(c)) o.Add(c);
            foreach (int c in Placement.NearestColumnsFirst(s.Lvl.Geo, x, w)) if (!o.Contains(c)) o.Add(c);
            return o.ToArray();
        }

        /// <summary>Where a queued piece would land now (K-25), or null when no column has room (it stays queued, K-26).</summary>
        public static Anchor? DeliveryLanding(GameState s, int id)
        {
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            int wy = s.Lvl.Geo.Wy;
            foreach (int x in DeliveryColumns(s, id))
            {
                if (x < 0 || x + shape.W > wy) continue;
                int y = Placement.DropIntoYard(s, shape, x, id);
                if (y >= 0) return new Anchor(x, y);
            }
            return null;
        }

        /// <summary>
        /// K-35 step 9 (K-25, K-26): tries every queued piece once, oldest first. A delivered piece lands in the yard and gets
        /// `arrivedTurn = turn` (Y4 / E-31: it does not dry in this move's step 10).
        /// </summary>
        public static DeliveryResult DeliverQueue(GameState s)
        {
            var delivered = new List<Delivered>();
            int turn = Hdr(s, H.Turn);
            foreach (int id in QueueIds(s))
            {
                var to = DeliveryLanding(s, id);
                if (!to.HasValue) continue;
                var shape = Shapes.ShapeByIndex(PieceShape(s, id));
                Placement.MovePiece(s, id, Placement.YardPlace(to.Value));
                SetPieceField(s, id, PF.ArrivedTurn, turn);
                var from = new Anchor(to.Value.Ix, Placement.YardDropRow(s.Lvl.Geo, shape));
                delivered.Add(new Delivered(id, from, to.Value, from.Iy - to.Value.Iy));
            }
            return new DeliveryResult(delivered.ToArray(), Hdr(s, H.QueueLen));
        }
    }
}
