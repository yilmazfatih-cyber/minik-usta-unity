#nullable enable
// Port of tests/core/site.test.ts. The two K-22 tests of the TS file drive the strategy through `applyMove` (events, win) and
// come with the K-35 package; `K22_completeIfDone_direct` is a C# addition that exercises the same strategy code directly.
using System;
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
