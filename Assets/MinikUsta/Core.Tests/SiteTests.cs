#nullable enable
// Port of tests/core/site.test.ts: the K-22 / K-15 tests drive the strategy through `Moves.ApplyMove` (`Run`, events, win);
// `K22_completeIfDone_direct` is a C# addition that exercises the same strategy code directly.
using System;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class SiteTests
    {
        /// <summary>Two one-row segments; the W domino builds segment 0, the truck brings the Y domino for segment 1.</summary>
        private static LevelSpec TwoSegments() => new LevelSpec
        {
            Wall = Wall(2),
            Plans = new[] { new[] { "WW" }, new[] { "YY" } },
            Pieces = Ps(Pc("D2_90", "W", 0, 0)),
            Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("D2_90", "Y", 0, 8)) } },
        };

        [Test(Description = "K-22 a completed segment shifts the site, counts it and queues the next batch, delivered in step 9")]
        public void K22_a_completed_segment_shifts_the_site_counts_it_and_queues_the_next_batch_delivered_in_step_9()
        {
            var s = Init(TwoSegments());
            var site = Site.SiteStrategy(s.Lvl);
            Assert.AreEqual("segments", site.Mode);
            Assert.IsNull(site.CompleteIfDone(s));
            var r = Run(s, DragTo(0, 6, 8));
            CollectionAssert.AreEqual(
                new[] { "segmentCompleted:8", "goalProgress:8", "siteShifted:8", "deliveryArrived:9", "pieceFell:9" },
                r.Ev.Where(e => e.Step >= 8).Select(e => e.TypeName + ":" + e.Step).ToArray());
            var arrived = Find<DeliveryArrivedEvent>(r.Ev);
            Assert.AreEqual(1, arrived.Seg);
            CollectionAssert.AreEqual(new[] { 1 }, arrived.Pieces);
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, new[] { Hdr(s, H.ActiveSeg), Hdr(s, H.DeliveryCursor), Grid.VisibleSegment(s) });
            Assert.AreEqual(1, site.ActiveSegment(s));
            Assert.AreEqual(0, site.FrameOffset(s));
            // the delivered domino drops into the column its batch names (K-25 stage 1)
            CollectionAssert.AreEqual(new[] { Zone.Yard, 0, 0 }, new[] { PieceZone(s, 1), PieceX(s, 1), PieceY(s, 1) });
            // the completed segment keeps its locked block (panorama, K-06)
            CollectionAssert.AreEqual(new[] { Zone.Site, 1 }, new[] { PieceZone(s, 0), Hdr(s, H.Turn) });
            ExpectConsistent(s);
        }

        [Test(Description = "K-22 the last segment does not shift: the win is checked instead (K-28)")]
        public void K22_the_last_segment_does_not_shift_the_win_is_checked_instead()
        {
            var s = Init(TwoSegments());
            Run(s, DragTo(0, 6, 8));
            var r = Run(s, DragTo(1, 6, 8));
            CollectionAssert.AreEqual(new[] { "segmentCompleted", "goalProgress", "levelWon" }, Types(r.Ev).Skip(r.Ev.Count - 3).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, new[] { Hdr(s, H.ActiveSeg), Hdr(s, H.DeliveryCursor) });
            Assert.IsTrue(r.Res.Won);
        }

        [Test(Description = "K-15 a segment is complete only without any other block in its area: debris on a `.` cell holds it back")]
        public void K15_a_segment_is_complete_only_without_any_other_block_in_its_area()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "W." },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 0) },
            });
            var placed = Run(s, DragTo(0, 6, 8));
            Assert.AreEqual(0, Find<PlacementCorrectEvent>(placed.Ev).PieceId);
            CollectionAssert.DoesNotContain(Types(placed.Ev), "segmentCompleted");
            var cleared = Run(s, DragTo(1, 5, 2)); // the debris leaves the site → complete in the same move's step 8
            CollectionAssert.Contains(Types(cleared.Ev), "segmentCompleted");
            Assert.IsFalse(cleared.Res.Won); // K-48 (3): the debris is a material block, now in the yard
        }

        [Test(Description = "K-22 (direct, C# addition) a completed segment shifts the site, counts it and queues the next batch; the last segment does not shift")]
        public void K22_completeIfDone_direct_a_completed_segment_shifts_the_site_counts_it_and_queues_the_next_batch()
        {
            var s = Init(TwoSegments());
            var site = Site.SiteStrategy(s.Lvl);
            Assert.AreEqual("segments", site.Mode);
            Assert.IsNull(site.CompleteIfDone(s));
            var fall = Gravity.ComputeFall(s, 0, N(6, 8));
            Assert.AreEqual(PlacementOutcome.KindCorrect, Placement.SettlePlacement(s, 0, fall.Landing, fall.Verdict).Kind);
            var done = site.CompleteIfDone(s)!;
            Assert.AreEqual(0, done.Seg);
            Assert.AreEqual(1, done.Completed);
            Assert.AreEqual(1, done.ShiftedTo);
            CollectionAssert.AreEqual(new[] { 1 }, done.Enqueued);
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, new[] { Hdr(s, H.ActiveSeg), Hdr(s, H.DeliveryCursor), Grid.VisibleSegment(s) });
            Assert.AreEqual(1, site.ActiveSegment(s));
            Assert.AreEqual(0, site.FrameOffset(s));
            Assert.AreEqual(Zone.Queue, PieceZone(s, 1));
            // step 9 drops the domino into the column its batch names (K-25 stage 1)
            Delivery.DeliverQueue(s);
            CollectionAssert.AreEqual(new[] { Zone.Yard, 0, 0 }, new[] { PieceZone(s, 1), PieceX(s, 1), PieceY(s, 1) });
            // the completed segment keeps its locked block (panorama, K-06)
            Assert.AreEqual(Zone.Site, PieceZone(s, 0));
            ExpectConsistent(s);
            // the last segment does not shift: the win is checked instead (K-28)
            var last = Gravity.ComputeFall(s, 1, N(6, 8));
            Placement.SettlePlacement(s, 1, last.Landing, last.Verdict);
            var end = site.CompleteIfDone(s)!;
            Assert.AreEqual(1, end.Seg);
            Assert.IsNull(end.ShiftedTo);
            CollectionAssert.AreEqual(new[] { 1, 2 }, new[] { Hdr(s, H.ActiveSeg), Hdr(s, H.DeliveryCursor) });
            Assert.IsTrue(Movement.IsSiteClosed(s));
        }

        [Test(Description = "carousel completion / counter and the elevator tick throw \"Phase 3\"; reading the shown segment works")]
        public void carousel_completion_counter_and_the_elevator_tick_throw_Phase_3_reading_the_shown_segment_works()
        {
            var carousel = CreateInitialState(Builders.Compiled(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Mode = "carousel",
                CarouselEvery = 2,
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
            }));
            var site = Site.SiteStrategy(carousel.Lvl);
            Assert.AreEqual("carousel", site.Mode);
            Assert.AreEqual(0, site.ActiveSegment(carousel));
            StringAssert.Contains("Phase 3", Assert.Throws<NotImplementedException>(() => site.CompleteIfDone(carousel))!.Message);
            StringAssert.Contains("Phase 3", Assert.Throws<NotImplementedException>(() => site.CarouselTick(carousel))!.Message);
            var lifted = CreateInitialState(Builders.Compiled(new LevelSpec
            {
                Plan = new[] { "WW" },
                Elevator = Elevator(0, 2, 1, 1),
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
            }));
            Assert.AreEqual(1, Site.SiteStrategy(lifted.Lvl).FrameOffset(lifted));
            StringAssert.Contains("Phase 3", Assert.Throws<NotImplementedException>(() => Site.SiteStrategy(lifted.Lvl).ElevatorTick(lifted))!.Message);
        }
    }
}
