#nullable enable
// The animation planner (port of web tests/scenes/juice.test.ts "EventPlayer schedule" + Q0 extensions): K-35 step order
// (JUICE §0 rule 10) on the real golden solutions of levels 1–10, the locked sequences (#18, #19, #55–#57, #107), every one
// of the 43 event types gets a step (no event is dropped), JUICE §0 rule 14 (Söküm: no reward), the reduced variants, and
// purity (the plan never touches the state; the logic never waits for it).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Core.Json;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Presentation.Tests.Fixtures.Play;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class AnimationPlannerTests
    {
        private static readonly FeelSettings F = new FeelSettings();

        private static At SiteAt(int x, int y) => At.Site(x, y, 0);
        private static At YardAt(int x, int y) => At.Yard(x, y);

        private static PlanContext Ctx(int movesBefore = 10, int[]? queued = null, int? blocksLeft = null) => new PlanContext
        {
            Gravity = "normal",
            QueuedBefore = queued ?? new int[0],
            MovesBefore = movesBefore,
            PieceHeight = _ => 1,
            BlocksLeft = blocksLeft,
        };

        private static string[] Board(AnimationPlan plan) =>
            plan.Steps.Where(s => s.Kind != StepKind.Instant && s.Kind != StepKind.GoalTick && s.Kind != StepKind.GoalDone).Select(s => s.Kind.ToString()).ToArray();

        // --- K-35 order on the golden solutions --------------------------------------------------------------------------------

        [Test]
        public void K35_JUICE_rule10_level_1_first_move_release_fall_landing_correct_counter_then_end()
        {
            var plans = PlayCrane(1);
            var first = plans[0].Plan;
            CollectionAssert.AreEqual(
                new[] { "SiteFall", "Landing", "PlaceCorrect", "BlocksChipFly", "BuildFront", "StreakPip", "MovesTick", "End" },
                KindsWithoutInstant(first),
                "web ['10','11','12','83','15','50','end'] + the D-091 chip flight");
            Assert.AreEqual(StepKind.Instant, first.Steps[0].Kind, "pieceMoved over the wall: the drag already drew the block there");
            Assert.IsInstanceOf<PieceMovedEvent>(first.Steps[0].Event);
            var fall = first.Find(StepKind.SiteFall)!;
            int rows = ((PieceFellEvent)fall.Event!).Rows;
            Assert.Greater(rows, 0, "crane-area release: the block falls");
            Assert.AreEqual(FallMotion.ReleaseFallMs(rows, "normal", F), fall.Ms, 1e-3);
            Assert.AreEqual(fall.Ms, StartOf(first, StepKind.Landing), 1e-3, "#11 when the fall ends");
            Assert.AreEqual(fall.Ms, StartOf(first, StepKind.PlaceCorrect), 1e-3, "#12 with the landing");
            Assert.AreEqual(fall.Ms + F.PlaceOkMs, StartOf(first, StepKind.MovesTick), 1e-3, "#50 after the fill");
            Assert.AreEqual(fall.Ms + F.PlaceOkMs, StartOf(first, StepKind.BuildFront), 1e-3, "#83 after the fill");
            Assert.IsTrue(first.Steps.All(s => !s.Lock), "no locked step in an ordinary move");
            Assert.AreEqual(first.Ms, first.Steps.Last().At, "End is the last step at the plan end");
            Assert.AreEqual(1, first.Find(StepKind.PlaceCorrect)!.N, "streak value reached");
        }

        [Test]
        public void K35_golden_level_1_canonical_solution_step_list_of_every_move_with_the_default_feel()
        {
            // level_001.solver.json, released on the landing nodes (fall 0): the pinned schedule of the default FeelSettings
            string Line(AnimationPlan p) => string.Join(" | ", p.Steps.Select(s => s.Kind + "@" + Num(s.At) + "+" + Num(s.Ms) + (s.Lock ? " L" : "")));
            const string Place = "Instant@0+0 | SiteFall@0+0 | Landing@0+120 | PlaceCorrect@0+200 | BlocksChipFly@0+500 | BuildFront@200+160 | StreakPip@0+160";
            var want = new[]
            {
                Place + " | MovesTick@200+180 | End@500+0",
                Place + " | MovesTick@200+180 | End@500+0",
                Place + " | MovesTick@200+180 | End@500+0",
                Place + " | TrowelEarned@160+500 | MovesTick@200+180 | End@660+0",
                Place + " | MovesTick@200+180 | SegmentDone@380+600 L | GoalDone@380+300 | YardClear@980+600 L | Win@1580+2500 L | BonusBuild@4080+720 L | End@4800+0",
            };
            var plans = PlayMoves(LevelFile(1), SolverGolden(1).Drags);
            CollectionAssert.AreEqual(want, plans.Select(p => Line(p.Plan)).ToArray());
            // K-35 event order of the same moves (the plan's input): drag → fall → validation → counter (→ 8 → 11)
            CollectionAssert.AreEqual(
                new[] { "pieceMoved@1", "pieceFell@2", "placementCorrect@3", "comboChanged@3", "movesChanged@4", "segmentCompleted@8", "goalProgress@8", "levelWon@11" },
                plans.Last().Result.Events.Select(e => e.TypeName + "@" + e.Step).ToArray());
        }

        private static string Num(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        [Test]
        public void K12_W1_level_4_rail_park_23_precedes_the_validation()
        {
            var third = PlayCrane(4)[2].Plan;
            CollectionAssert.AreEqual(new[] { "RailClamp", "PlaceCorrect" }, KindsWithoutInstant(third).Take(2).ToArray());
            Assert.AreEqual(F.RailClampMs, StartOf(third, StepKind.PlaceCorrect), 1e-3);
            Assert.AreEqual(((PieceMovedEvent)third.Find(StepKind.RailClamp)!.Event!).Entry, EntryKinds.Gap);
        }

        [Test]
        public void K22_K28_K48_last_move_segment_slide_then_all_blocks_in_place_then_win_and_bonus_all_locked_and_last()
        {
            var last = PlayCrane(1).Last().Plan;
            var k = Board(last).ToList();
            Assert.Greater(k.IndexOf("SegmentDone"), k.IndexOf("MovesTick"));
            Assert.AreEqual(k.IndexOf("SegmentDone") + 1, k.IndexOf("YardClear"), "#18 → #94");
            Assert.AreEqual(k.IndexOf("YardClear") + 1, k.IndexOf("Win"), "#94 → #55");
            Assert.AreEqual(k.IndexOf("Win") + 1, k.IndexOf("BonusBuild"), "#55 → #56");
            Assert.AreEqual("End", k.Last());
            foreach (var s in last.Steps.Where(s => s.Kind == StepKind.SegmentDone || s.Kind == StepKind.YardClear || s.Kind == StepKind.Win || s.Kind == StepKind.BonusBuild))
                Assert.IsTrue(s.Lock, s + " locks input");
            Assert.AreEqual(StartOf(last, StepKind.SegmentDone) + F.SegmentMs, StartOf(last, StepKind.YardClear), 1e-3);
            var clear = last.Find(StepKind.YardClear)!;
            Assert.AreEqual(Math.Max(F.YardClearMs, F.YardClearRibbonMs), clear.Ms);
            Assert.AreEqual(clear.End, StartOf(last, StepKind.Win), 1e-3);
            var win = last.Find(StepKind.Win)!;
            Assert.AreEqual(F.WinMs, win.Ms);
            var bonus = last.Find(StepKind.BonusBuild)!;
            Assert.AreEqual(6, bonus.N, "level 1 golden: 6 moves left (K-28 Bonus İnşaat)");
            Assert.AreEqual(6 * F.BonusPerMoveMs, bonus.Ms, 1e-3);
            Assert.LessOrEqual(bonus.Ms, F.BonusMaxMs);
            Assert.IsTrue(last.HasLock);
        }

        [Test]
        public void K25_level_5_the_truck_19_locked_comes_after_the_slide_18_and_drops_every_block_staggered_no_queue_chip()
        {
            var fifth = PlayCrane(5)[4].Plan; // Sol Oda complete (LEVELS §2 Bölüm 5 move 5)
            var k = KindsWithoutInstant(fifth).ToList();
            Assert.Greater(k.IndexOf("TruckDelivery"), k.IndexOf("SegmentDone"));
            CollectionAssert.DoesNotContain(k, "QueueChip");
            var truck = fifth.Find(StepKind.TruckDelivery)!;
            Assert.IsTrue(truck.Lock);
            Assert.GreaterOrEqual(truck.Ms, F.TruckMs);
            Assert.AreEqual(4, truck.Drops!.Length);
            for (int i = 0; i < truck.Drops.Length; i++) Assert.AreEqual(F.TruckEnterMs + i * F.TruckDropStaggerMs, truck.Drops[i].Delay, 1e-3);
            Assert.AreEqual(4, truck.Also.Length, "the 4 delivery falls ride on the truck step");
            Assert.IsInstanceOf<DeliveryArrivedEvent>(truck.Event);
            Assert.IsTrue(fifth.Find(StepKind.SegmentDone)!.Flag, "a next segment slides in (siteShifted)");
            Assert.AreEqual(1, fifth.Find(StepKind.SegmentDone)!.ToSeg);
        }

        /// <summary>3×4 | 2×4 board of the web K-26 test: a carried D2_0 Y hangs at (1,2), the truck's O4 G has no room and waits.</summary>
        private const string QueueBoard = @"{
          ""schemaVersion"": 1, ""id"": 99, ""chapter"": 1, ""name"": { ""tr"": ""Test"", ""en"": ""Test"" }, ""difficulty"": ""easy"", ""moves"": 20,
          ""goals"": [{ ""type"": ""build"" }], ""gravity"": { ""build"": ""normal"", ""yard"": false }, ""wall"": { ""height"": 4, ""gaps"": [] },
          ""site"": { ""cols"": 2, ""rows"": 4 },
          ""build"": { ""mode"": ""segments"", ""segments"": [
            { ""name"": { ""tr"": ""A"", ""en"": ""A"" }, ""rows"": [""WW"", ""WW"", ""WW"", ""WW""] },
            { ""name"": { ""tr"": ""B"", ""en"": ""B"" }, ""rows"": [""GG"", ""GG"", ""YY"", ""YY""] } ] },
          ""yard"": { ""cols"": 3, ""rows"": 4, ""batches"": [
            { ""forSegment"": 0, ""pieces"": [
              { ""shape"": ""D2_0"", ""color"": ""W"", ""x"": 0, ""y"": 2 }, { ""shape"": ""D2_0"", ""color"": ""W"", ""x"": 0, ""y"": 0 },
              { ""shape"": ""D2_0"", ""color"": ""W"", ""x"": 2, ""y"": 2 }, { ""shape"": ""D2_0"", ""color"": ""W"", ""x"": 2, ""y"": 0 },
              { ""shape"": ""D2_0"", ""color"": ""Y"", ""x"": 1, ""y"": 2 } ] },
            { ""forSegment"": 1, ""pieces"": [
              { ""shape"": ""O4_0"", ""color"": ""G"", ""x"": 0, ""y"": 4 }, { ""shape"": ""D2_0"", ""color"": ""Y"", ""x"": 2, ""y"": 4 } ] } ] },
          ""obstacles"": []
        }";

        [Test]
        public void K26_a_truck_block_without_room_the_queue_chip_20_after_the_truck_and_a_later_move_delivers_through_the_chip()
        {
            var lvl = FromJson(QueueBoard);
            DragMove N4(int pieceId, int ix) => new DragMove(pieceId, new DragNode(ix, 4, Movement.Free));
            var plans = PlayMoves(lvl, new SessionAction[] { N4(0, 3), N4(1, 3), N4(2, 4), N4(3, 4), N4(4, 3) });
            var fourth = plans[3].Plan;
            var k = KindsWithoutInstant(fourth).ToList();
            Assert.Greater(k.IndexOf("TruckDelivery"), k.IndexOf("SegmentDone"));
            Assert.Greater(k.IndexOf("QueueChip"), k.IndexOf("TruckDelivery"));
            Assert.AreEqual(1, fourth.Find(StepKind.TruckDelivery)!.Drops!.Length);
            Assert.AreEqual(1, fourth.Find(StepKind.QueueChip)!.N, "Kamyonda: 1");
            var chip = plans[4].Plan.Find(StepKind.QueueChip)!;
            Assert.AreEqual(1, chip.Drops!.Length, "the queued block leaves through the chip (FIFO)");
            Assert.IsFalse(chip.Lock);
            Assert.AreEqual(F.BounceToQueueMs, chip.Drops[0].Ms, "the #88 flight out of the chip");
        }

        [Test]
        public void K17_wrong_placement_13_then_88_into_the_queue_and_84_for_the_missing_support_after_the_flight()
        {
            var bounce = Ev(new PieceBouncedEvent(2, SiteAt(6, 1), null, false, VerdictReasons.Support, new[] { SiteAt(6, 0) }), 3, 3);
            var wrong = Ev(new PlacementWrongEvent(2, new[] { VerdictReasons.Support }, new[] { SiteAt(6, 0) }), 3, 2);
            var events = new GameEvent[]
            {
                Ev(new PieceMovedEvent(2, YardAt(0, 0), SiteAt(6, 8), EntryKinds.OverWall), 1, 0),
                Ev(new PieceFellEvent(2, SiteAt(6, 8), SiteAt(6, 1), 7, FallCauses.Release), 2, 1),
                wrong,
                bounce,
                Ev(new MovesChangedEvent(3, -1, MovesChangedReasons.Move, new MoveCost(1, 0)), 4, 4),
                Ev(new DeliveryQueuedEvent(1), 9, 5),
            };
            var plan = AnimationPlanner.Plan(events, Ctx(movesBefore: 4), F);
            CollectionAssert.AreEqual(new[] { "SiteFall", "Landing", "PlaceWrong", "BounceToQueue", "SupportFlash", "MovesTick", "LastMoves", "QueueChip", "End" }, KindsWithoutInstant(plan));
            float t13 = StartOf(plan, StepKind.PlaceWrong);
            Assert.AreEqual(t13 + F.WrongShakeMs, StartOf(plan, StepKind.BounceToQueue), 1e-3);
            Assert.AreEqual(t13 + F.WrongShakeMs + F.BounceToQueueMs, StartOf(plan, StepKind.SupportFlash), 1e-3);
            Assert.IsFalse(plan.Find(StepKind.LastMoves)!.First, "already under 5 before the move: no second sound");
            Assert.IsTrue(plan.Find(StepKind.PlaceWrong)!.Flag, "the bounce lands in the truck queue");
            CollectionAssert.Contains(plan.Find(StepKind.PlaceWrong)!.Also, wrong, "placementWrong rides on the #13 step");
            CollectionAssert.AreEqual(new[] { SiteAt(6, 0) }, plan.Find(StepKind.SupportFlash)!.Cells);
        }

        [Test]
        public void K17_a_bounce_to_its_start_flies_the_arc_and_a_drop_above_the_yard_adds_the_yard_fall()
        {
            var direct = AnimationPlanner.Plan(new GameEvent[]
            {
                Ev(new PieceBouncedEvent(1, SiteAt(6, 0), YardAt(2, 3), false, VerdictReasons.Color, new At[0]), 3),
            }, Ctx(), F);
            Assert.AreEqual(F.WrongShakeMs + (F.PlaceBadMs - F.WrongShakeMs), direct.Find(StepKind.PlaceWrong)!.Ms, 1e-3, "#13 = 350 ms");
            var ctx = Ctx();
            ctx.PieceHeight = _ => 2;
            ctx.GridRows = 10;
            var viaDrop = AnimationPlanner.Plan(new GameEvent[]
            {
                Ev(new PieceBouncedEvent(1, SiteAt(6, 0), YardAt(2, 1), true, VerdictReasons.Color, new At[0]), 3),
            }, ctx, F);
            var step = viaDrop.Find(StepKind.PlaceWrong)!;
            Assert.AreEqual(10 - 2 - 1, step.N, "K-17 step 2: dropped from y0 = rows − h");
            Assert.AreEqual(F.PlaceBadMs + FallMotion.YardFallMs(7, F), step.Ms, 1e-3);
        }

        [Test]
        public void K33_the_trowel_16_follows_the_fourth_bead_and_the_combo_reset_rides_on_it()
        {
            var fourth = PlayCrane(1)[3].Plan;
            var pip = fourth.Find(StepKind.StreakPip)!;
            var trowel = fourth.Find(StepKind.TrowelEarned)!;
            Assert.AreEqual(pip.At + F.StreakPipMs, trowel.At, 1e-3);
            Assert.AreEqual(4, pip.N, "the 4th bead");
            Assert.AreEqual(1, trowel.N, "trowels held");
            Assert.IsTrue(trowel.Also.OfType<ComboChangedEvent>().Any(c => c.Combo == 0), "the beads empty into the trowel");
            CollectionAssert.DoesNotContain(KindsWithoutInstant(fourth), "StreakReset");
        }

        [Test]
        public void K07_a_cancelled_release_plays_the_cancel_return_8_and_spends_nothing()
        {
            var lvl = LevelFile(1);
            var session = GameSession.Start(lvl);
            var ctx = PlanContext.Capture(session);
            var res = session.Apply(new DragMove(0, new DragNode(0, 2, Movement.Free))); // its own start: sameSpot
            Assert.AreEqual(MoveStatuses.Cancelled, res.Status);
            var plan = AnimationPlanner.Plan(res, ctx, F);
            CollectionAssert.AreEqual(new[] { "Cancel", "End" }, Kinds(plan));
            var cancel = plan.Steps[0];
            Assert.AreEqual(8, cancel.Juice);
            Assert.AreEqual(F.CancelMs, cancel.Ms);
            Assert.AreEqual(F.CancelEase, cancel.Ease);
            Assert.IsTrue(cancel.Flag, "sameSpot");
            Assert.IsTrue(cancel.FastForward);
            Assert.AreEqual(0, cancel.PieceId);
            Assert.AreEqual(11, session.MovesLeft, "K-07: a cancel costs nothing");
        }

        [Test]
        public void JUICE_rule8_the_reduced_schedule_keeps_the_order_with_fades()
        {
            var full = PlayCrane(1).Last().Plan;
            var reduced = PlayCrane(1, reduced: true).Last().Plan;
            CollectionAssert.AreEqual(Kinds(full), Kinds(reduced));
            Assert.AreEqual(F.ReducedFadeMs, reduced.Find(StepKind.SegmentDone)!.Ms);
            Assert.AreEqual(0, reduced.Find(StepKind.MovesTick)!.Ms);
            Assert.AreEqual(F.ReducedFadeMs, reduced.Find(StepKind.PlaceCorrect)!.Ms);
            Assert.AreEqual(F.BonusReducedMs, reduced.Find(StepKind.BonusBuild)!.Ms);
            Assert.AreEqual(full.Find(StepKind.SiteFall)!.Ms, reduced.Find(StepKind.SiteFall)!.Ms, "rule durations (the fall) do not change");
            Assert.Less(reduced.Ms, full.Ms);
        }

        // --- every event type --------------------------------------------------------------------------------------------------

        /// <summary>One event of each of the 43 types (TS `GameEventBody['t']`), at its K-35 step, with the step kind it plays alone.</summary>
        private static List<(GameEvent Ev, StepKind Kind)> AllKinds()
        {
            var detail = JsonValue.Object().Set("target", JsonValue.Of(HammerTargetKinds.Crate)).Set("obstacle", JsonValue.Of(0));
            return new List<(GameEvent, StepKind)>
            {
                (Ev(new MoveCancelledEvent(0, CancelReasons.Straddle), 0), StepKind.Cancel),
                (Ev(new PieceMovedEvent(0, YardAt(0, 0), YardAt(1, 0), EntryKinds.Yard), 1), StepKind.YardDrop),
                (Ev(new PiecePaintedEvent(0, "W", "R", 1), 1), StepKind.PiecePaint),
                (Ev(new WindDriftEvent(0, 1), 2), StepKind.WindDrift),
                (Ev(new PieceFellEvent(0, SiteAt(6, 8), SiteAt(6, 0), 8, FallCauses.Release), 2), StepKind.SiteFall),
                (Ev(new BalloonRoseEvent(0, SiteAt(6, 0), SiteAt(6, 5), 5), 2), StepKind.BalloonGlide),
                (Ev(new SteeredEvent(0, 4, -1), 2), StepKind.Steer),
                (Ev(new GlassBrokeEvent(0, SiteAt(6, 0), 1), 2), StepKind.GlassBreak),
                (Ev(new PieceReturnedEvent(0, SiteAt(6, 0), YardAt(0, 0)), 2), StepKind.PieceReturn),
                (Ev(new PlacementCorrectEvent(0, new[] { SiteAt(6, 0) }, true), 3), StepKind.PlaceCorrect),
                (Ev(new CellsRevealedEvent(0, new[] { new RevealedCell(0, 1, "R") }), 3), StepKind.CellsReveal),
                (Ev(new ComboChangedEvent(2), 3), StepKind.StreakPip),
                (Ev(new TrowelEarnedEvent(1), 3), StepKind.TrowelEarned),
                (Ev(new PlacementWrongEvent(0, new[] { VerdictReasons.Color }, new At[0]), 3), StepKind.Instant),
                (Ev(new MortarStuckEvent(0, VerdictReasons.Support, new[] { SiteAt(6, 0) }), 3), StepKind.MortarStuck),
                (Ev(new PieceBouncedEvent(0, SiteAt(6, 0), YardAt(0, 0), false, VerdictReasons.Color, new At[0]), 3), StepKind.PlaceWrong),
                (Ev(new MovesChangedEvent(9, -1, MovesChangedReasons.Move, new MoveCost(1, 0)), 4), StepKind.MovesTick),
                (Ev(new CrateDamagedEvent(0, 1), 5), StepKind.CrateHit),
                (Ev(new CrateBrokenEvent(0), 5), StepKind.CrateBreak),
                (Ev(new BagTornEvent(1), 5), StepKind.BagTear),
                (Ev(new ChainReleasedEvent(3), 5), StepKind.ChainBreak),
                (Ev(new ScrewCollectedEvent(YardAt(2, 0), 1), 5), StepKind.ScrewCollect),
                (Ev(new KeyCollectedEvent("k1"), 5), StepKind.KeyCollect),
                (Ev(new GapUnlockedEvent(0), 5), StepKind.GapUnlock),
                (Ev(new GapChangedEvent(0, true, 2), 10), StepKind.GapChange),
                (Ev(new WetTickEvent(4, 2), 10), StepKind.WetTick),
                (Ev(new GoalProgressEvent(1, 1, 3), 7), StepKind.GoalTick),
                (Ev(new SegmentCompletedEvent(0), 8), StepKind.SegmentDone),
                (Ev(new SiteShiftedEvent(1), 8), StepKind.Instant),
                (Ev(new CarouselRotatedEvent(1), 10), StepKind.CarouselRotate),
                (Ev(new ElevatorMovedEvent(1), 10), StepKind.ElevatorMove),
                (Ev(new DeliveryArrivedEvent(1, new[] { 5 }), 9), StepKind.Instant),
                (Ev(new DeliveryQueuedEvent(2), 9), StepKind.QueueChip),
                (Ev(new LevelWonEvent(3), 11), StepKind.Win),
                (Ev(new OutOfMovesEvent(), 11), StepKind.OutOfMoves),
                (Ev(new DeadlockDetectedEvent(DeadlockReasons.NoMoves), 12), StepKind.Instant),
                (Ev(new TruckHelpEvent(TruckHelpKinds.Reshuffle, new PieceShift[0]), 12), StepKind.TruckHelp),
                (Ev(new BoosterAppliedEvent(BoosterNames.Hammer, detail), 1), StepKind.Instant),
                (Ev(new BoosterRejectedEvent(BoosterNames.Crane, BoosterRejects.NoTarget), 0), StepKind.BoosterRejected),
                (Ev(new PieceLiftedEvent(0, "crane", YardAt(0, 0), SiteAt(6, 0), "D2_0"), 1), StepKind.CraneLift),
                (Ev(new ColorsSwappedEvent(0, 1, "R", "W"), 1), StepKind.Brush),
                (Ev(new CargoSmashedEvent(2, YardAt(0, 0)), 1), StepKind.CargoSmash),
                (Ev(new TeardownEvent(3, new TeardownMove[0], TeardownCauses.Tiling), 12), StepKind.Teardown),
            };
        }

        [Test]
        public void K35_every_one_of_the_43_event_types_gets_its_step_alone_and_in_one_package_no_event_is_dropped()
        {
            var all = AllKinds();
            Assert.AreEqual(43, all.Count);
            CollectionAssert.AreEquivalent(EventTypes.All, all.Select(a => a.Ev.TypeName), "one event of every type");
            foreach (var (ev, kind) in all)
            {
                var plan = AnimationPlanner.Plan(new[] { ev }, Ctx(), F);
                var step = plan.Steps.FirstOrDefault(s => ReferenceEquals(s.Event, ev) || s.Also.Contains(ev));
                Assert.IsNotNull(step, ev.TypeName + " has a step");
                Assert.AreEqual(kind, step!.Kind, ev.TypeName);
                Assert.AreEqual(StepKind.End, plan.Steps.Last().Kind, ev.TypeName + ": End last");
                Assert.AreEqual(ev.Step, step.PipelineStep, ev.TypeName + ": the step carries its K-35 step");
            }
            // one package with all of them (in K-35 step order, as the core would emit them)
            var package = all.Select(a => a.Ev).OrderBy(e => e.Step).ToArray();
            for (int i = 0; i < package.Length; i++) package[i].Seq = i;
            var big = AnimationPlanner.Plan(package, Ctx(), F);
            var covered = big.CoveredEvents();
            foreach (var e in package) Assert.IsTrue(covered.Contains(e), e.TypeName + " covered in the full package");
            Assert.AreEqual(StepKind.End, big.Steps.Last().Kind);
        }

        [Test]
        public void K35_steps_follow_the_pipeline_order_and_out_of_moves_is_last_JUICE_rule10()
        {
            var package = AllKinds().Select(a => a.Ev).Where(e => !(e is LevelWonEvent)).OrderBy(e => e.Step).ToArray();
            var plan = AnimationPlanner.Plan(package, Ctx(), F);
            var withEvents = plan.Steps.Where(s => s.Event != null && s.Kind != StepKind.OutOfMoves).ToArray();
            for (int i = 1; i < withEvents.Length; i++)
                Assert.LessOrEqual(withEvents[i - 1].PipelineStep, withEvents[i].PipelineStep, withEvents[i - 1] + " before " + withEvents[i]);
            var board = Board(plan).ToList();
            Assert.Greater(board.IndexOf("OutOfMoves"), board.IndexOf("Teardown"), "#57 after the Söküm (level end last)");
            Assert.AreEqual(board.Count - 2, board.IndexOf("OutOfMoves"), "#57 is the last step before End");
            Assert.GreaterOrEqual(StartOf(plan, StepKind.OutOfMoves), plan.Find(StepKind.Teardown)!.End - 1e-3);
            Assert.GreaterOrEqual(StartOf(plan, StepKind.Teardown), plan.Find(StepKind.TruckHelp)!.End - 1e-3, "Kamyon Yardımı, then Söküm");
            foreach (var kind in new[] { StepKind.SegmentDone, StepKind.TruckHelp, StepKind.Teardown, StepKind.OutOfMoves })
                Assert.IsTrue(plan.Find(kind)!.Lock, kind + " locks input");
        }

        [Test]
        public void K30_JUICE_rule14_a_package_ending_in_a_teardown_plays_no_placement_reward()
        {
            var placed = Ev(new PlacementCorrectEvent(1, new[] { SiteAt(6, 0), SiteAt(6, 1) }, true), 3, 2);
            var combo = Ev(new ComboChangedEvent(3), 3, 3);
            var events = new GameEvent[]
            {
                Ev(new PieceMovedEvent(1, YardAt(0, 0), SiteAt(6, 5), EntryKinds.OverWall), 1, 0),
                Ev(new PieceFellEvent(1, SiteAt(6, 5), SiteAt(6, 0), 5, FallCauses.Release), 2, 1),
                placed,
                combo,
                Ev(new MovesChangedEvent(5, -1, MovesChangedReasons.Move, new MoveCost(1, 0)), 4, 4),
                Ev(new DeadlockDetectedEvent(DeadlockReasons.Tiling), 12, 5),
                Ev(new TeardownEvent(1, new[] { new TeardownMove(1, TeardownPlace.OnBoard(SiteAt(6, 0)), TeardownPlace.OnBoard(YardAt(0, 0))) }, TeardownCauses.Tiling), 12, 6),
            };
            var plan = AnimationPlanner.Plan(events, Ctx(), F);
            var kinds = KindsWithoutInstant(plan);
            foreach (var reward in new[] { "PlaceCorrect", "StreakPip", "TrowelEarned", "BuildFront", "BlocksChipFly" })
                CollectionAssert.DoesNotContain(kinds, reward);
            var covered = plan.CoveredEvents();
            Assert.IsTrue(covered.Contains(placed) && covered.Contains(combo), "the reward events are still applied (Instant), not dropped");
            var tear = plan.Find(StepKind.Teardown)!;
            Assert.AreEqual(F.TeardownSettleMs + F.TeardownMs, tear.Ms, 1e-3, "one block: settle 120 + 600");
            Assert.IsTrue(tear.Lock);
            Assert.AreEqual(107, tear.Juice);
            CollectionAssert.AreEqual(new[] { "Teardown", "Resync", "End" }, kinds.Skip(kinds.Length - 3).ToArray());
            // three blocks: 80 ms stagger
            var three = new TeardownEvent(1, new TeardownMove[3], TeardownCauses.Color);
            Assert.AreEqual(F.TeardownSettleMs + F.TeardownMs + 2 * F.TeardownStaggerMs, AnimationPlanner.Plan(new GameEvent[] { Ev(three, 12) }, Ctx(), F).Find(StepKind.Teardown)!.Ms, 1e-3);
        }

        [Test]
        public void K33_K36_K37_K38_K29_booster_and_offer_packages_play_their_rows_first()
        {
            var lift = Ev(new PieceLiftedEvent(4, "trowel", YardAt(0, 0), SiteAt(7, 3), "D2_90"), 1, 1);
            var trowel = AnimationPlanner.Plan(new GameEvent[] { Ev(new BoosterAppliedEvent(BoosterNames.Trowel, null), 1, 0), lift }, Ctx(), F);
            var flight = trowel.Find(StepKind.TrowelFlight)!;
            Assert.AreSame(lift, flight.Event, "the flight shows the trowel's pieceLifted (from, to, shape)");
            Assert.AreEqual((17, F.GoldTrowelMs, 4), (flight.Juice, flight.Ms, flight.PieceId));
            var crane = AnimationPlanner.Plan(new GameEvent[]
            {
                Ev(new BoosterAppliedEvent(BoosterNames.Crane, null), 1, 0), Ev(new PieceLiftedEvent(2, "crane", YardAt(0, 0), SiteAt(6, 0), "O4_0"), 1, 1),
            }, Ctx(), F);
            Assert.AreEqual(F.CraneBoosterMs, crane.Find(StepKind.CraneLift)!.Ms);
            var hammer = AnimationPlanner.Plan(new GameEvent[] { Ev(new BoosterAppliedEvent(BoosterNames.Hammer, null), 1, 0), Ev(new CargoSmashedEvent(7, YardAt(1, 0)), 1, 1) }, Ctx(), F);
            Assert.AreEqual(F.HammerMs, hammer.Find(StepKind.CargoSmash)!.Ms);
            var brush = AnimationPlanner.Plan(new GameEvent[] { Ev(new BoosterAppliedEvent(BoosterNames.Paint, null), 1, 0), Ev(new ColorsSwappedEvent(1, 2, "R", "W"), 1, 1) }, Ctx(), F);
            Assert.AreEqual(F.BrushMs, brush.Find(StepKind.Brush)!.Ms);
            var offer = AnimationPlanner.Plan(new GameEvent[] { Ev(new MovesChangedEvent(5, 5, MovesChangedReasons.Offer), 1, 0) }, Ctx(movesBefore: 0), F);
            var added = offer.Find(StepKind.MovesAdded)!;
            Assert.AreEqual((53, F.MovesAddMs, 5), (added.Juice, added.Ms, added.N));
        }

        [Test]
        public void UX_5_1_DL_2R_19_last_moves_warning_follows_moves_minus_blocks_left_and_fires_its_sound_once()
        {
            Assert.IsTrue(AnimationPlanner.LowMovesWarning(4, 3));
            Assert.IsFalse(AnimationPlanner.LowMovesWarning(5, 3));
            Assert.IsTrue(AnimationPlanner.LowMovesWarning(2, 0));
            GameEvent Tick(int left) => Ev(new MovesChangedEvent(left, -1, MovesChangedReasons.Move, new MoveCost(1, 0)), 4);
            var starts = AnimationPlanner.Plan(new[] { Tick(4) }, Ctx(movesBefore: 5, blocksLeft: 3), F).Find(StepKind.LastMoves);
            Assert.IsNotNull(starts);
            Assert.IsTrue(starts!.First, "5 − 3 = 2 before, 4 − 3 = 1 now: the warning starts");
            Assert.IsNull(AnimationPlanner.Plan(new[] { Tick(8) }, Ctx(movesBefore: 9, blocksLeft: 3), F).Find(StepKind.LastMoves));
            Assert.IsNull(AnimationPlanner.Plan(new[] { Tick(6) }, Ctx(movesBefore: 7), F).Find(StepKind.LastMoves), "Faz 2 rule without blocks left: from 5");
        }

        // --- golden solutions: coverage, groups, purity ------------------------------------------------------------------------

        [Test]
        public void K35_K43_golden_levels_1_to_10_every_plan_covers_every_event_groups_are_sequential_and_logic_never_waits([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var golden = SolverGolden(level);
            foreach (var moves in new[] { golden.Drags.Cast<SessionAction>().ToArray(), CraneMoves(level).Cast<SessionAction>().ToArray() })
            {
                var plans = PlayMoves(LevelFile(level), moves);
                foreach (var p in plans)
                {
                    var covered = p.Plan.CoveredEvents();
                    foreach (var e in p.Result.Events) Assert.IsTrue(covered.Contains(e), "B" + level + ": " + e.TypeName + " is in the plan");
                    Assert.AreEqual(StepKind.End, p.Plan.Steps.Last().Kind);
                    Assert.AreEqual(p.Plan.Ms, p.Plan.Steps.Max(s => s.End), 1e-3, "the plan ends with its last step");
                    var groups = p.Plan.Groups;
                    for (int g = 0; g < groups.Length; g++)
                    {
                        Assert.AreEqual(g, groups[g].Index);
                        if (g > 0) Assert.Greater(groups[g].At, groups[g - 1].At, "groups start in order");
                        foreach (var s in groups[g].Steps)
                        {
                            Assert.AreEqual(g, s.Group);
                            Assert.GreaterOrEqual(s.At, groups[g].At - 1e-3, s + " starts inside its group");
                            if (g + 1 < groups.Length) Assert.Less(s.At, groups[g + 1].At, s + " starts before the next group");
                        }
                    }
                    Assert.AreEqual(p.Plan.Steps.Length, groups.Sum(gr => gr.Steps.Length), "every step in exactly one group");
                }
            }
            // the logic never waited: the session state after the golden log is the TS one (event log hash bit for bit)
            var sink = new ArraySink();
            var session = GameSession.Start(LevelFile(level), null, 0, new SessionOptions(strict: true), sink);
            foreach (var m in golden.Drags)
            {
                var ctx = PlanContext.Capture(session);
                var res = session.Apply(m, sink);
                AnimationPlanner.Plan(res, ctx, F); // planning between moves changes nothing
            }
            Assert.AreEqual(golden.EventLogHash, Moves.EventLogHash(sink.Events), "B" + level);
        }

        [Test]
        public void K35_planning_is_pure_the_same_package_gives_the_same_plan_and_the_state_is_untouched()
        {
            var lvl = LevelFile(5);
            var session = GameSession.Start(lvl, null, 0, new SessionOptions(strict: true));
            var moves = CraneMoves(5);
            for (int i = 0; i < 4; i++) session.Apply(moves[i]);
            var ctx = PlanContext.Capture(session);
            var res = session.Apply(moves[4]); // segment + truck
            var before = MinikUsta.Core.Hash.HashState(session.State);
            var a = AnimationPlanner.Plan(res, ctx, F);
            var b = AnimationPlanner.Plan(res, ctx, F);
            CollectionAssert.AreEqual(before, MinikUsta.Core.Hash.HashState(session.State), "planning reads, never writes");
            Assert.AreEqual(a.Steps.Length, b.Steps.Length);
            for (int i = 0; i < a.Steps.Length; i++)
            {
                Assert.AreEqual((a.Steps[i].Kind, a.Steps[i].At, a.Steps[i].Ms, a.Steps[i].Lock, a.Steps[i].Group), (b.Steps[i].Kind, b.Steps[i].At, b.Steps[i].Ms, b.Steps[i].Lock, b.Steps[i].Group));
                Assert.AreSame(a.Steps[i].Event, b.Steps[i].Event);
            }
            // a tuned FeelSettings changes the timing, never the order
            var slow = new FeelSettings { SegmentMs = 1200, TruckMs = 1400, PlaceOkMs = 400 };
            var c = AnimationPlanner.Plan(res, ctx, slow);
            CollectionAssert.AreEqual(Kinds(a), Kinds(c));
            Assert.Greater(c.Ms, a.Ms);
        }
    }
}
