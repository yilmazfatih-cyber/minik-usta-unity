#nullable enable
// Port of tests/core/delivery.test.ts — the tests that need no move pipeline (K-25, K-26 direct calls). The E-03 queue
// board, the arrivedTurn and the level-5 replay go through `applyMove` and come with the K-35 package.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class DeliveryTests
    {
        private static (int zone, int x, int y) At_(GameState s, int id) => (PieceZone(s, id), PieceX(s, id), PieceY(s, id));

        /// <summary>The whole yard filled with B1 W blocks (id = y · 6 + x when there are no holes), except the cells in `holes`.</summary>
        private static P[] FullYard(params (int x, int y)[] holes)
        {
            var o = new List<P>();
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 6; x++)
                    if (!holes.Any(h => h.x == x && h.y == y)) o.Add(Pc("B1_0", "W", x, y));
            return o.ToArray();
        }

        [Test(Description = "K-25 GDD example: O4 G x = 4 lands on (4,4); B1 R with column 0 full falls in column 1 at (1,6)")]
        public void K25_GDD_example_O4_G_x_4_lands_on_4_4_B1_R_with_column_0_full_falls_in_column_1_at_1_6()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 0, 2), Pc("D2_0", "W", 0, 4), Pc("D2_0", "W", 0, 6), Pc("D2_0", "W", 1, 0), Pc("D2_0", "W", 1, 2),
                    Pc("D2_0", "W", 1, 4), Pc("O4_0", "W", 4, 0), Pc("O4_0", "W", 4, 2)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("O4_0", "G", 4, 8), Pc("B1_0", "R", 0, 8)) } },
            });
            CollectionAssert.AreEqual(new[] { 1 }, Delivery.BatchesFor(s, 1).Select(b => b.Index).ToArray());
            CollectionAssert.AreEqual(new[] { 9, 10 }, Delivery.EnqueueBatchesFor(s, 1));
            CollectionAssert.AreEqual(new[] { 9, 10 }, QueueIds(s));
            var res = Delivery.DeliverQueue(s);
            CollectionAssert.AreEqual(new[] { new[] { 9, 4, 4, 8, 4 }, new[] { 10, 1, 6, 9, 3 } },
                res.Delivered.Select(d => new[] { d.PieceId, d.To.Ix, d.To.Iy, d.From.Iy, d.Rows }).ToArray());
            Assert.AreEqual(0, res.Queued);
            ExpectConsistent(s);
        }

        [Test(Description = "K-25 column order: x, then dropColumns in list order, then the rest nearest to x (ties nearer the wall)")]
        public void K25_column_order_x_then_dropColumns_in_list_order_then_the_rest_nearest_to_x()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Batches = new[] { new BatchSpec { ForSegment = 1, DropColumns = new[] { 5, 0 }, Pieces = Ps(Pc("B1_0", "W", 2, 8)) } },
            });
            CollectionAssert.AreEqual(new[] { 2, 5, 0, 3, 1, 4 }, Delivery.DeliveryColumns(s, 1));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, Delivery.DeliveryColumns(s, 0)); // batch 0: no dropColumns
        }

        [Test(Description = "E-34 x and both dropColumns are full: stage 3 still runs; with no room at all the block stays queued")]
        public void E34_x_and_both_dropColumns_are_full_stage_3_still_runs_with_no_room_at_all_the_block_stays_queued()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = FullYard((2, 7)),
                Batches = new[]
                {
                    new BatchSpec { ForSegment = 1, DropColumns = new[] { 3, 0 }, Pieces = Ps(Pc("B1_0", "R", 1, 8)) },
                    new BatchSpec { ForSegment = 2, Pieces = Ps(Pc("B1_0", "G", 0, 8)) },
                },
            });
            int red = s.Lvl.Batches[1].PieceIds[0];
            Delivery.EnqueueBatchesFor(s, 1);
            Assert.AreEqual(A(2, 7), Delivery.DeliveryLanding(s, red));
            Assert.AreEqual(0, Delivery.DeliverQueue(s).Queued);
            Assert.AreEqual((Zone.Yard, 2, 7), At_(s, red));
            int green = s.Lvl.Batches[2].PieceIds[0];
            Delivery.EnqueueBatchesFor(s, 2);
            Assert.IsNull(Delivery.DeliveryLanding(s, green));
            var res = Delivery.DeliverQueue(s);
            CollectionAssert.IsEmpty(res.Delivered);
            Assert.AreEqual(1, res.Queued);
            Assert.AreEqual(Zone.Queue, PieceZone(s, green));
        }

        [Test(Description = "K-26 GDD example: queue [O4 W, B1 Y], only (5,7) free → O4 stays, B1 lands on (5,7), \"Kamyonda: 1\"")]
        public void K26_GDD_example_queue_O4_W_B1_Y_only_5_7_free_O4_stays_B1_lands_on_5_7_Kamyonda_1()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = FullYard((5, 7)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("O4_0", "W", 0, 8), Pc("B1_0", "Y", 0, 8)) } },
            });
            var ids = Delivery.EnqueueBatchesFor(s, 1);
            int o4 = ids[0];
            int b1 = ids[1];
            var res = Delivery.DeliverQueue(s);
            CollectionAssert.AreEqual(new[] { b1 }, res.Delivered.Select(d => d.PieceId).ToArray());
            Assert.AreEqual((Zone.Yard, 5, 7), At_(s, b1));
            Assert.AreEqual(1, res.Queued);
            CollectionAssert.AreEqual(new[] { o4 }, QueueIds(s));
        }

        [Test(Description = "K-26 older queued pieces deliver first; a new batch joins behind them")]
        public void K26_older_queued_pieces_deliver_first_a_new_batch_joins_behind_them()
        {
            var s = Init(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW" }, new[] { "WW" } },
                Pieces = FullYard(),
                Batches = new[]
                {
                    new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "R", 0, 8)) },
                    new BatchSpec { ForSegment = 2, Pieces = Ps(Pc("B1_0", "G", 5, 8)) },
                },
            });
            int old = Delivery.EnqueueBatchesFor(s, 1)[0];
            Assert.AreEqual(1, Delivery.DeliverQueue(s).Queued);
            int young = Delivery.EnqueueBatchesFor(s, 2)[0];
            CollectionAssert.AreEqual(new[] { old, young }, QueueIds(s));
            Placement.MovePiece(s, 47, PiecePlace.Gone(5, 7)); // frees (5,7): room for one block
            var res = Delivery.DeliverQueue(s);
            CollectionAssert.AreEqual(new[] { old }, res.Delivered.Select(d => d.PieceId).ToArray());
            Assert.AreEqual((Zone.Yard, 5, 7), At_(s, old));
            CollectionAssert.AreEqual(new[] { young }, QueueIds(s));
        }
    }
}
