#nullable enable
// Port of tests/obstacles/S1_slidingSite.test.ts (web repo). Drags go through `Boards.Drop` / `Boards.Play` (steps 2–3, 8, 9);
// the event lists, the `won` flag / `outcome` and the YAO of the session test come with the K-35 / session packages.
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

        [Test(Description = "S1 K-22 completing the active segment shifts the site in step 8; the truck delivers in step 9 (level 5 move 5)")]
        public void S1_K22_completing_the_active_segment_shifts_the_site_in_step_8_the_truck_delivers_in_step_9()
        {
            var s = LevelState(5);
            Play(s, 5, 0, 4);
            var r = Play(s, HandMoves(5)[4]);
            Assert.AreEqual(0, r.Completed!.Seg);
            Assert.AreEqual(1, r.Completed.ShiftedTo);
            // FIFO (K-25): k1_0 … k1_3 land in the yard the move left empty; nothing waits (LEVELS §2 Bölüm 5)
            CollectionAssert.AreEqual(Truck, r.Delivery.Delivered.Select(d => d.PieceId).ToArray());
            Assert.AreEqual(0, r.Delivery.Queued);
            CollectionAssert.IsEmpty(QueueIds(s));
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
            Play(s, 5, 0, 7); // … then the two R roof blocks are parked (moves 6–7)
            var r = Play(s, HandMoves(5)[7]); // k1_0 (O4 Y) → (4,0)
            Assert.IsTrue(r.Correct);
            Assert.IsTrue(r.Outcome!.Cells!.All(c => c.Seg == 1));
            Assert.AreEqual(1, PieceSeg(s, 4));
            Assert.IsNull(r.Completed);
        }

        [Test(Description = "S1 K-22 after the last segment there is no shift: step 11 wins (level 5 canonical solution, 6 moves left)")]
        public void S1_K22_after_the_last_segment_there_is_no_shift_step_11_wins()
        {
            var s = LevelState(5);
            var moves = HandMoves(5);
            DropResult? last = null;
            foreach (var m in moves) last = Play(s, m);
            Assert.AreEqual(1, last!.Completed!.Seg);
            Assert.IsNull(last.Completed.ShiftedTo);
            Assert.IsTrue(Goals.LevelGoalsMet(s));
            Assert.AreEqual(6, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "S1 K-26 a truck block without room waits in the queue (\"Kamyonda: 1\") and comes in a later step 9")]
        public void S1_K26_a_truck_block_without_room_waits_in_the_queue()
        {
            var s = Init(FullYard());
            var shift = Drop(s, 0, 6, 8);
            Assert.AreEqual(1, shift.Completed!.ShiftedTo);
            CollectionAssert.AreEqual(new[] { 24 }, shift.Delivery.Delivered.Select(d => d.PieceId).ToArray());
            Assert.AreEqual(1, shift.Delivery.Queued);
            CollectionAssert.AreEqual(new[] { 25 }, QueueIds(s));
            Assert.AreEqual(Zone.Queue, PieceZone(s, 25));

            var next = Drop(s, 24, 6, 8); // frees the only two cells; the waiting block lands there
            CollectionAssert.AreEqual(new[] { 25 }, next.Delivery.Delivered.Select(d => d.PieceId).ToArray());
            Assert.AreEqual(0, next.Delivery.Queued);
            Assert.AreEqual(Zone.Yard, PieceZone(s, 25));
            Assert.IsFalse(Goals.LevelGoalsMet(s)); // K-48 (3): the fixture's filler blocks are still in the yard
            Assert.AreEqual(1, next.Completed!.Seg);
            ExpectConsistent(s);
        }
    }
}
