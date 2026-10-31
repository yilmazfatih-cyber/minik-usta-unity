#nullable enable
// Port of src/core/site.ts (web repo, source of truth).
using System;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>What K-35 step 8 did.</summary>
    public sealed class SegmentCompletion
    {
        /// <summary>The segment that was completed.</summary>
        public readonly int Seg;
        /// <summary>Completed segments after this one (`deliveryCursor`).</summary>
        public readonly int Completed;
        /// <summary>Segment now on the site; null after the last segment (no shift, K-22).</summary>
        public readonly int? ShiftedTo;
        /// <summary>Truck pieces appended to the queue (K-25), delivered in step 9.</summary>
        public readonly int[] Enqueued;

        public SegmentCompletion(int seg, int completed, int? shiftedTo, int[] enqueued)
        {
            Seg = seg;
            Completed = completed;
            ShiftedTo = shiftedTo;
            Enqueued = enqueued;
        }
    }

    /// <summary>TECH_DESIGN §7.1 `SiteStrategy`: the site mode of a level (S1 segments; S5 carousel and S6 elevator are Phase 3).</summary>
    public abstract class SiteStrategy
    {
        /// <summary>`build.mode`: "segments" or "carousel".</summary>
        public abstract string Mode { get; }
        /// <summary>Segment shown on the site: active (segments) or front (carousel).</summary>
        public abstract int ActiveSegment(GameState s);
        /// <summary>Elevator offset `e` (K-24); 0 without elevator.</summary>
        public abstract int FrameOffset(GameState s);
        /// <summary>K-35 step 8: completes the shown segment when it is done (K-15); null when it is not.</summary>
        public abstract SegmentCompletion? CompleteIfDone(GameState s);
        /// <summary>K-35 step 10, timer S5 (K-23): returns the new front segment when the platform turned, else null.</summary>
        public abstract int? CarouselTick(GameState s);
        /// <summary>K-35 step 10, timer S6 (K-24): returns the new offset.</summary>
        public abstract int ElevatorTick(GameState s);

        protected static Exception Phase3(string what) => new NotImplementedException("Phase 3: " + what + " is not implemented yet (TECH_DESIGN §14.2)");
    }

    /// <summary>
    /// Site mode strategy (docs/GDD.md K-22; K-23 carousel and K-24 elevator are Phase 3; TECH_DESIGN §7.1 `SiteStrategy`,
    /// §6.2 steps 8 and 10, S1).
    ///
    /// `segments` (S1, K-22): one active segment at a time. When it is complete (K-15) in K-35 step 8, the completed count
    /// (`deliveryCursor`) goes up, the site shifts to the next segment, which comes empty, and the next truck batch joins the
    /// end of the queue (delivered in step 9, K-25). After the last segment there is no shift: step 11 checks the win.
    ///
    /// Phase 3 stubs: `carousel` (K-23) segment completion and counter tick, and the elevator (K-24) tick throw
    /// "Phase 3". Reading the visible segment and the frame offset works for every mode (other modules already use them).
    /// </summary>
    public static class Site
    {
        private sealed class SegmentsStrategy : SiteStrategy
        {
            public override string Mode => "segments";
            public override int ActiveSegment(GameState s) => Hdr(s, H.ActiveSeg);
            public override int FrameOffset(GameState s) => Hdr(s, H.Elev);

            public override SegmentCompletion? CompleteIfDone(GameState s)
            {
                int seg = Hdr(s, H.ActiveSeg);
                if (!Placement.IsSegmentComplete(s, seg)) return null;
                int completed = Hdr(s, H.DeliveryCursor) + 1;
                SetHdr(s, H.DeliveryCursor, completed);
                int? shiftedTo = null;
                if (seg + 1 < s.Lvl.Segments.Length)
                {
                    shiftedTo = seg + 1;
                    SetHdr(s, H.ActiveSeg, shiftedTo.Value);
                }
                var enqueued = Delivery.EnqueueBatchesFor(s, completed);
                return new SegmentCompletion(seg, completed, shiftedTo, enqueued);
            }

            public override int? CarouselTick(GameState s) => null;
            public override int ElevatorTick(GameState s) => throw Phase3("the elevator scaffold tick (S6, K-24)");
        }

        private sealed class CarouselStrategy : SiteStrategy
        {
            public override string Mode => "carousel";
            public override int ActiveSegment(GameState s) => Hdr(s, H.FrontSeg);
            public override int FrameOffset(GameState s) => Hdr(s, H.Elev);
            public override SegmentCompletion? CompleteIfDone(GameState s) => throw Phase3("carousel segment completion (S5, K-23)");
            public override int? CarouselTick(GameState s) => throw Phase3("the carousel counter (S5, K-23)");
            public override int ElevatorTick(GameState s) => throw Phase3("the elevator scaffold tick (S6, K-24)");
        }

        private static readonly SiteStrategy Segments = new SegmentsStrategy();
        private static readonly SiteStrategy Carousel = new CarouselStrategy();

        /// <summary>The strategy of a level's `build.mode` (S1 segments; S5 carousel stub). TS `siteStrategy`.</summary>
        public static SiteStrategy SiteStrategy(CompiledLevel lvl) => lvl.Mode == "carousel" ? Carousel : Segments;
    }
}
