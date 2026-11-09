#nullable enable
// Port of tests/obstacles/S1_slidingSite.test.ts (web repo). Drags go through the K-35 pipeline (`Run`, `Boards.Play`) and the
// session test through `GameSession` (events with their steps, `won` / `outcome`, YAO; P3). The i18n card text check reads
// src/i18n of the web repo and stays there.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests.Obstacles
{
    [TestFixture]
    public class S1SlidingSiteTests
    {
        /// <summary>Level 5 truck batch (`k1_0` … `k1_3`, LEVELS §2 Bölüm 5 "Kamyon partisi 1").</summary>
        private static readonly int[] Truck = { 4, 5, 6, 7 };

        /// <summary>
        /// Two one-row segments `WW` → `RR`; the yard is full except for the W block on top (piece 0). The truck brings two
        /// R blocks (24, 25) but only the two cells the W block leaves have room.
        /// </summary>
        private static LevelSpec FullYard()
        {
            var fillers = new List<P>();
            for (int y = 0; y <= 6; y++) foreach (int x in new[] { 0, 2, 4 }) fillers.Add(Pc("D2_90", "G", x, y));
            fillers.Add(Pc("D2_90", "G", 0, 7));
            fillers.Add(Pc("D2_90", "G", 2, 7));
            var pieces = new List<P> { Pc("D2_90", "W", 4, 7) };
            pieces.AddRange(fillers);
            return new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "RR" } },
                Pieces = pieces.ToArray(),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("D2_90", "R", 4, 8), Pc("D2_90", "R", 0, 8)) } },
            };
        }

        [Test(Description = "S1 applies to segments mode with 2–5 segments only (levels 5, 7, 10), with the obs.s1.desc card (its TR/EN text lives in the web repo)")]
        public void S1_applies_to_segments_mode_with_2_5_segments_only()
        {
            foreach (int id in new[] { 5, 7, 10 }) Assert.IsTrue(S1_SlidingSite.Rule.AppliesTo(LevelFile(id)), "level " + id);
            CollectionAssert.AreEqual(new[] { "S1" }, Registry.ActiveRuleIds(LevelFile(5)));
            CollectionAssert.AreEqual(new[] { "obs.s1.desc" }, Registry.InfoKeysFor(LevelFile(5)));
            foreach (int id in new[] { 1, 2, 3, 4, 6, 8, 9}) Assert.IsFalse(S1_SlidingSite.Rule.AppliesTo(LevelFile(id)), "level " + id);
            var carousel = Builders.Compiled(new LevelSpec { Plans = new[] { new[] { "WW" }, new[] { "WW" } }, Mode = "carousel", CarouselEvery = 2, Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            Assert.IsFalse(S1_SlidingSite.Rule.AppliesTo(carousel)); // S5, not S1
        }

        [Test(Description = "S1 K-22 a level has 1–5 segments (schema)")]
        public void S1_K22_a_level_has_1_5_segments()
        {
            string[][] Plan(int n) => Enumerable.Range(0, n).Select(_ => new[] { "WW" }).ToArray();
            var pieces = Ps(Pc("B1_0", "W", 0, 0));
            Assert.IsNotNull(Logic.ValidateLevelJson(Builders.LevelJson(new LevelSpec { Plans = Plan(5), Pieces = pieces })).Level);
            var six = Logic.ValidateLevelJson(Builders.LevelJson(new LevelSpec { Plans = Plan(6), Pieces = pieces }));
            Assert.IsNull(six.Level);
            CollectionAssert.Contains(six.Issues.Select(i => i.Code).ToArray(), "schema_invalid");
        }

        [Test(Description = "S1 K-25 the truck batch of segment 2 stays off the board until segment 1 is complete")]
        public void S1_K25_the_truck_batch_of_segment_2_stays_off_the_board_until_segment_1_is_complete()
        {
            var s = LevelState(5);
            foreach (int id in Truck) Assert.AreEqual(Zone.Pending, PieceZone(s, id), "piece " + id);
            CollectionAssert.IsEmpty(QueueIds(s));
            Play(s, 5, 0, 4); // Sol Oda one block short
            foreach (int id in Truck) Assert.AreEqual(Zone.Pending, PieceZone(s, id), "piece " + id);
            Assert.AreEqual(0, Hdr(s, H.ActiveSeg));
        }

        private static Ran RunHand(GameState s, int level, int i)
        {
            var m = HandMoves(level)[i];
            return Run(s, new DragMove(m.PieceId, m.To));
        }

        [Test(Description = "S1 K-22 completing the active segment shifts the site in step 8; the truck delivers in step 9 (level 5 move 5)")]
        public void S1_K22_completing_the_active_segment_shifts_the_site_in_step_8_the_truck_delivers_in_step_9()
        {
            var s = LevelState(5);
            for (int i = 0; i < 4; i++) RunHand(s, 5, i);
            var ev = RunHand(s, 5, 4).Ev;
            var done = Find<SegmentCompletedEvent>(ev);
            CollectionAssert.AreEqual(new[] { 0, 8 }, new[] { done.Seg, done.Step });
            var shifted = Find<SiteShiftedEvent>(ev);
            CollectionAssert.AreEqual(new[] { 1, 8 }, new[] { shifted.ToSeg, shifted.Step });
            // FIFO (K-25): k1_0 … k1_3 land in the yard the move left empty; nothing waits (LEVELS §2 Bölüm 5)
            var arrived = Find<DeliveryArrivedEvent>(ev);
            CollectionAssert.AreEqual(new[] { 9, 1 }, new[] { arrived.Step, arrived.Seg });
            CollectionAssert.AreEqual(Truck, arrived.Pieces);
            CollectionAssert.DoesNotContain(Types(ev), "deliveryQueued");
            CollectionAssert.IsEmpty(QueueIds(s));
            Assert.Less(Array.IndexOf(Types(ev), "siteShifted"), Array.IndexOf(Types(ev), "deliveryArrived"));
            Assert.AreEqual(1, Hdr(s, H.ActiveSeg));
            // the new segment comes empty; the finished one keeps its locked blocks (panorama, K-06)
            for (int sy = 0; sy < s.Lvl.Geo.Hs; sy++) for (int sx = 0; sx < s.Lvl.Geo.Ws; sx++) Assert.AreEqual(0, SiteOcc(s, 1, sx, sy));
            foreach (int id in new[] { 0, 1, 2, 3 })
            {
                Assert.AreEqual(Zone.Site, PieceZone(s, id));
                Assert.AreEqual(0, PieceSeg(s, id));
            }
            ExpectConsistent(s);
        }

        [Test(Description = "S1 K-22 the next placement plays on segment 2 (GDD example: after the shift the player builds Sağ Oda)")]
        public void S1_K22_the_next_placement_plays_on_segment_2()
        {
            var s = LevelState(5);
            for (int i = 0; i < 7; i++) RunHand(s, 5, i); // … then the two R roof blocks are parked (moves 6–7)
            var ev = RunHand(s, 5, 7).Ev; // k1_0 (O4 Y) → (4,0)
            var placed = Find<PlacementCorrectEvent>(ev);
            Assert.AreEqual(4, placed.PieceId);
            Assert.IsTrue(placed.Cells.All(c => c.Seg == 1));
            Assert.AreEqual(1, PieceSeg(s, placed.PieceId));
            CollectionAssert.DoesNotContain(Types(ev), "siteShifted");
        }

        [Test(Description = "S1 K-22 after the last segment there is no shift: step 11 wins (level 5 canonical solution, 6 moves left)")]
        public void S1_K22_after_the_last_segment_there_is_no_shift_step_11_wins()
        {
            var session = GameSession.Start(LevelFile(5));
            var moves = HandMoves(5);
            for (int i = 0; i < moves.Length - 1; i++) Assert.AreEqual(MoveStatuses.Applied, session.Commit(new DragMove(moves[i].PieceId, moves[i].To)).Status);
            var sink = new ArraySink();
            var last = session.Commit(new DragMove(moves[moves.Length - 1].PieceId, moves[moves.Length - 1].To), sink);
            Assert.IsTrue(last.Won);
            Assert.AreEqual(BoosterOutcomes.Won, session.Outcome);
            Assert.AreEqual(6, session.MovesLeft);
            var done = Find<SegmentCompletedEvent>(sink.Events);
            CollectionAssert.AreEqual(new[] { 1, 8 }, new[] { done.Seg, done.Step });
            CollectionAssert.DoesNotContain(Types(sink.Events), "siteShifted");
            var won = Find<LevelWonEvent>(sink.Events);
            CollectionAssert.AreEqual(new[] { 11, 6 }, new[] { won.Step, won.MovesLeft });
            Assert.AreEqual(new YaoMeasure(8, 0, 1), session.Yao());
        }

        [Test(Description = "S1 K-26 a truck block without room waits in the queue (\"Kamyonda: 1\") and comes in a later step 9")]
        public void S1_K26_a_truck_block_without_room_waits_in_the_queue_and_comes_in_a_later_step_9()
        {
            var s = Init(FullYard());
            var shift = Run(s, DragTo(0, 6, 8));
            Assert.AreEqual(1, Find<SiteShiftedEvent>(shift.Ev).ToSeg);
            CollectionAssert.AreEqual(new[] { 24 }, Find<DeliveryArrivedEvent>(shift.Ev).Pieces);
            var queued = Find<DeliveryQueuedEvent>(shift.Ev);
            CollectionAssert.AreEqual(new[] { 1, 9 }, new[] { queued.Queued, queued.Step });
            CollectionAssert.AreEqual(new[] { 25 }, QueueIds(s));
            Assert.AreEqual(Zone.Queue, PieceZone(s, 25));
            var next = Run(s, DragTo(24, 6, 8), noTruckHelp: true); // frees the only two cells; the waiting block lands there
            var arrived = Find<DeliveryArrivedEvent>(next.Ev);
            CollectionAssert.AreEqual(new[] { 25 }, arrived.Pieces);
            Assert.AreEqual(9, arrived.Step);
            Assert.AreEqual(0, Find<DeliveryQueuedEvent>(next.Ev).Queued);
            Assert.AreEqual(Zone.Yard, PieceZone(s, 25));
            Assert.IsFalse(next.Res.Won); // K-48 (3): the fixture's filler blocks are still in the yard
            Assert.AreEqual(1, Find<SegmentCompletedEvent>(next.Ev).Seg);
            ExpectConsistent(s);
        }
    }
}
