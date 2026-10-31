#nullable enable
// Port of tests/core/moves.test.ts (web repo): the K-35 move pipeline (`Moves.ApplyMove`) on the canonical solutions, the
// K-07 release table and cost, steps 2–3 (fall, validation, bounce), steps 5–6 (neighbour effects, yard gravity hooks),
// step 10 timers, step 12, steps 11 (K-28 / K-29), K-41 goals and the canonical JSON / FNV-1a helpers. `Run` = TS `run`
// (strict ArraySink), `DragTo` = TS `dragTo`, `HandMoves(n)` = TS `HAND[n]`.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class MovesTests
    {
        /// <summary>S3-like landing hook (the real one is the obstacle plugin, Phase 3): glass breaks when d > threshold.</summary>
        private static readonly FallRules GLASS = new FallRules(onLanded: (s, id, fall) =>
            HasFlag(s, id, FlagBit.Glass) && fall.Distance > s.Lvl.Gravity.GlassThreshold ? LandingEffect.Break(1) : LandingEffect.None);

        /// <summary>Y8-like placement hook: mortar blocks stick on a wrong placement.</summary>
        private static readonly PlacementRules MORTAR = new PlacementRules(onPlacement: (s, id, cells, verdict) =>
            HasFlag(s, id, FlagBit.Mortar) ? PlacementOverrides.Stick : PlacementOverrides.Default);

        /// <summary>Two W columns of height 2 (plan y0–y1) and four small yard blocks.</summary>
        private static LevelSpec TWO_ROWS(P[]? pieces = null) => new LevelSpec
        {
            Wall = Wall(2),
            Plan = new[] { "WW", "WW" },
            Pieces = pieces ?? Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0), Pc("D2_0", "Y", 2, 0), Pc("B1_0", "W", 3, 0)),
        };

        /// <summary>A full-cover board (K-47): plan 2 × 2 W, two D2_0 W blocks.</summary>
        private static LevelSpec FULL(P[]? extra = null) => new LevelSpec
        {
            Wall = Wall(2),
            Plan = new[] { "WW", "WW" },
            Pieces = Ps(Pc("D2_0", "W", 0, 0), Pc("D2_0", "W", 1, 0)).Plus(extra ?? new P[0]),
        };

        private static Move Hand(int level, int i) => new DragMove(HandMoves(level)[i].PieceId, HandMoves(level)[i].To);

        private static IEnumerable<Move> HandAll(int level) => HandMoves(level).Select(m => (Move)new DragMove(m.PieceId, m.To));

        private static string[] TypeSteps(IEnumerable<GameEvent> ev) => ev.Select(e => e.TypeName + "@" + e.Step).ToArray();

        private static string EventsText(IEnumerable<GameEvent> ev) => JsonWriter.Canonical(EventJson.ToJson(ev));

        // --- K-35 move pipeline on the canonical solutions (LEVELS §2) ----------------------------------------------------------

        [Test(Description = "K-35 level 5: segment shift, truck delivery and win with 6 moves left (K-22, K-25, K-28; LEVELS §2 Bölüm 5)")]
        public void K35_level_5_segment_shift_truck_delivery_and_win()
        {
            var s = LevelState(5);
            var moves = HandAll(5).ToArray();
            var ran = moves.Take(5).Select(m => Run(s, m)).ToArray();
            foreach (var r in ran) Assert.AreEqual(MoveStatuses.Applied, r.Res.Status);
            ExpectConsistent(s);
            var fifth = ran[4].Ev;
            // d completes Sol Oda: the 4th correct in a row earns a trowel (K-33), then steps 8 (shift) and 9 (delivery)
            CollectionAssert.AreEqual(new[]
            {
                "pieceMoved", "pieceFell", "placementCorrect", "comboChanged", "trowelEarned", "comboChanged", "movesChanged",
                "segmentCompleted", "goalProgress", "siteShifted", "deliveryArrived", "pieceFell", "pieceFell", "pieceFell", "pieceFell",
            }, Types(fifth));
            Assert.AreEqual(0, Find<SegmentCompletedEvent>(fifth).Seg);
            Assert.AreEqual(1, Find<SiteShiftedEvent>(fifth).ToSeg);
            var goal = Find<GoalProgressEvent>(fifth);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 8 }, new[] { goal.Goal, goal.Value, goal.Target, goal.Step });
            var arrived = Find<DeliveryArrivedEvent>(fifth);
            Assert.AreEqual(9, arrived.Step);
            Assert.AreEqual(1, arrived.Seg);
            CollectionAssert.AreEqual(new[] { 4, 5, 6, 7 }, arrived.Pieces);
            // LEVELS §2 Bölüm 5 "Kamyon partisi 1": k1_0 (0,0), k1_1 (0,2), k1_2 (1,2), k1_3 (2,0) — no queue
            CollectionAssert.AreEqual(
                new[] { new[] { 0, 0, Zone.Yard }, new[] { 0, 2, Zone.Yard }, new[] { 1, 2, Zone.Yard }, new[] { 2, 0, Zone.Yard } },
                new[] { 4, 5, 6, 7 }.Select(id => new[] { PieceX(s, id), PieceY(s, id), PieceZone(s, id) }).ToArray());
            Assert.AreEqual(0, Hdr(s, H.QueueLen));

            var rest = moves.Skip(5).Select(m => Run(s, m)).ToArray();
            var last = rest[rest.Length - 1];
            AssertResult(last.Res, MoveStatuses.Applied, null, true, false);
            var won = Find<LevelWonEvent>(last.Ev);
            CollectionAssert.AreEqual(new[] { 11, 6 }, new[] { won.Step, won.MovesLeft });
            Assert.AreEqual(11, Hdr(s, H.Turn));
            Assert.AreEqual(new YaoMeasure(8, 0, 1.0), Moves.MeasureYao(s));
            ExpectConsistent(s);
        }

        [Test(Description = "K-35 step numbers never go down inside a move and seq counts 0, 1, 2 …")]
        public void K35_step_numbers_never_go_down_and_seq_counts()
        {
            var s = LevelState(5);
            foreach (var m in HandAll(5))
            {
                var ev = Run(s, m).Ev;
                for (int i = 0; i < ev.Count; i++)
                {
                    Assert.AreEqual(i, ev[i].Seq);
                    if (i > 0) Assert.GreaterOrEqual(ev[i].Step, ev[i - 1].Step);
                }
            }
        }

        [Test(Description = "K-46 level 4: four over-wall placements and one rail placement → YAO 4/5 (K-12 rail entry, LEVELS §2 Bölüm 4)")]
        public void K46_level_4_four_over_wall_and_one_rail_placement()
        {
            var s = LevelState(4);
            Run(s, Hand(4, 0)); // e: shift
            Run(s, Hand(4, 1)); // c: shift (the corridor opens)
            var rail = Run(s, Hand(4, 2)).Ev; // a through the gap
            CollectionAssert.AreEqual(new[] { "pieceMoved", "placementCorrect", "comboChanged", "movesChanged" }, Types(rail));
            var moved = Find<PieceMovedEvent>(rail);
            Assert.AreEqual(EntryKinds.Gap, moved.Entry);
            Assert.AreEqual(0, moved.Gap);
            CollectionAssert.AreEqual(new[] { At.ZoneSite, "4", "0" }, new[] { moved.To.Zone, moved.To.X.ToString(), moved.To.Y.ToString() });
            Assert.IsFalse(Find<PlacementCorrectEvent>(rail).OverWall);
            var last = Run(s, Hand(4, 3));
            foreach (var m in HandAll(4).Skip(4)) last = Run(s, m);
            Assert.IsTrue(last.Res.Won);
            Assert.AreEqual(new YaoMeasure(4, 1, 4.0 / 5), Moves.MeasureYao(s));
        }

        [Test(Description = "K-35 the event log is a pure function of state and move (eventLogHash)")]
        public void K35_the_event_log_is_a_pure_function_of_state_and_move()
        {
            List<GameEvent> Play()
            {
                var s = LevelState(5);
                var sink = new ArraySink();
                foreach (var m in HandAll(5)) Moves.ApplyMove(s, m, sink, new ApplyOptions(strict: true));
                return sink.Events;
            }
            var a = Play();
            var b = Play();
            Assert.AreEqual(EventsText(a), EventsText(b));
            Assert.AreEqual(Moves.EventLogHash(a), Moves.EventLogHash(b));
            Assert.IsTrue(new Regex("^[0-9a-f]{16}$").IsMatch(Moves.EventLogHash(a)));
            Assert.AreNotEqual(Moves.EventLogHash(a), Moves.EventLogHash(a.Skip(1)));
        }

        [Test(Description = "K-35 NULL_SINK runs the same rules without events")]
        public void K35_NullSink_runs_the_same_rules_without_events()
        {
            var a = LevelState(5);
            var b = LevelState(5);
            foreach (var m in HandAll(5))
            {
                Moves.ApplyMove(a, m, NullSink.Instance, new ApplyOptions(strict: true));
                Run(b, m);
            }
            CollectionAssert.AreEqual(b.Buf, a.Buf);
        }

        // --- K-07 release table and move cost (K-35 steps 0 and 4) --------------------------------------------------------------

        private static void AssertSingleCancel(List<GameEvent> ev, int pieceId, string reason)
        {
            Assert.AreEqual(1, ev.Count, "one event");
            var e = Find<MoveCancelledEvent>(ev);
            CollectionAssert.AreEqual(new[] { 0, 0, pieceId }, new[] { e.Seq, e.Step, e.PieceId });
            Assert.AreEqual(reason, e.Reason);
        }

        [Test(Description = "K-07 row 1: release on the start cells cancels and changes nothing")]
        public void K07_row_1_release_on_the_start_cells_cancels()
        {
            var s = LevelState(5);
            var before = Snapshot(s);
            var r = Run(s, DragTo(1, 2, 0)); // b (D2_0 W) released where it lies
            AssertResult(r.Res, MoveStatuses.Cancelled, CancelReasons.SameSpot, false, false);
            AssertSingleCancel(r.Ev, 1, CancelReasons.SameSpot);
            CollectionAssert.AreEqual(before, s.Buf);
        }

        [Test(Description = "K-07 row 3: release over the yard with a cell in the crane area cancels at no cost (K-05)")]
        public void K07_row_3_release_over_the_yard_in_the_crane_area_cancels()
        {
            var s = LevelState(5);
            var before = Snapshot(s);
            var r = Run(s, DragTo(1, 2, 5)); // H = 5: rows 5–6 are the crane area
            Assert.AreEqual(CancelReasons.CraneOverYard, r.Res.Reason);
            CollectionAssert.AreEqual(before, s.Buf);
        }

        [Test(Description = "K-07 row 2: a yard move costs 1 move, m += 1, the block stays where released (K-10)")]
        public void K07_row_2_a_yard_move_costs_1_move()
        {
            var s = LevelState(5);
            var r = Run(s, DragTo(1, 2, 2)); // b up one block: hangs at (2,2) (gravity.yard false)
            Assert.AreEqual(MoveStatuses.Applied, r.Res.Status);
            CollectionAssert.AreEqual(new[] { "pieceMoved", "movesChanged" }, Types(r.Ev));
            var moved = Find<PieceMovedEvent>(r.Ev);
            Assert.AreEqual(1, moved.Step);
            Assert.AreEqual(EntryKinds.Yard, moved.Entry);
            Assert.AreEqual(At.Yard(2, 0), moved.From);
            Assert.AreEqual(At.Yard(2, 2), moved.To);
            var cost = Find<MovesChangedEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 4, 16, -1 }, new[] { cost.Step, cost.MovesLeft, cost.Delta });
            Assert.AreEqual(MovesChangedReasons.Move, cost.Reason);
            CollectionAssert.AreEqual(new[] { 1, 0 }, new[] { cost.Cost!.Value.Base, cost.Cost.Value.Glass });
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, new[] { PieceX(s, 1), PieceY(s, 1), Hdr(s, H.Turn) });
            ExpectConsistent(s);
        }

        [Test(Description = "K-07 costs add up (base + glass) and the counter stops at 0")]
        public void K07_costs_add_up_and_the_counter_stops_at_0()
        {
            var s = Init(TWO_ROWS(Ps(Pc("D2_0", "W", 0, 0, new[] { "glass" }))));
            SetHdr(s, H.MovesLeft, 1);
            var r = Run(s, DragTo(0, 6, 8), new MoveHooks(fall: GLASS));
            var cost = Find<MovesChangedEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 0, -1, 1, 1 }, new[] { cost.MovesLeft, cost.Delta, cost.Cost!.Value.Base, cost.Cost.Value.Glass });
            Assert.IsTrue(r.Res.OutOfMoves);
        }

        [Test(Description = "K-07 an invalid record (unpickable piece, unreachable node) is cancelled as invalid; strict throws")]
        public void K07_an_invalid_record_is_cancelled_as_invalid_strict_throws()
        {
            var s = LevelState(5);
            var before = Snapshot(s);
            var buried = Run(s, DragTo(0, 4, 5), strict: false); // a (O4 R) lies under d (K-09)
            AssertSingleCancel(buried.Ev, 0, CancelReasons.Invalid);
            Assert.AreEqual(CancelReasons.Invalid, Run(s, DragTo(2, 0, 0), strict: false).Res.Reason); // c onto a's cells
            CollectionAssert.AreEqual(before, s.Buf);
            var ex = Assert.Throws<InvalidOperationException>(() => Run(s, DragTo(2, 0, 0)));
            StringAssert.Contains("not reachable", ex!.Message);
        }

        [Test(Description = "K-19 normal gravity: a steer in the move record is invalid (no steering, commit on release)")]
        public void K19_normal_gravity_a_steer_in_the_record_is_invalid()
        {
            var s = Init(TWO_ROWS());
            var ex = Assert.Throws<InvalidOperationException>(() => Run(s, new DragMove(3, new DragNode(6, 8, 0), null, new Steer(1, 3))));
            StringAssert.Contains("steer", ex!.Message);
        }

        [Test(Description = "K-35 step 1: `via` must name a paint gate whose rail the drag can reach")]
        public void K35_step_1_via_must_name_a_paint_gate_on_the_reach()
        {
            var s = LevelState(4);
            Run(s, Hand(4, 0));
            Run(s, Hand(4, 1));
            // a reaches the rail of gap 0, but W1 is a static gap, not a W6 paint gate
            var ex = Assert.Throws<InvalidOperationException>(() => Run(s, new DragMove(0, new DragNode(4, 0, 1), 0)));
            StringAssert.Contains("via", ex!.Message);
            Assert.AreEqual(MoveStatuses.Applied, Run(s, Hand(4, 2)).Res.Status);
        }

        // --- K-35 steps 2–3: fall, validation, bounce ----------------------------------------------------------------------------

        [Test(Description = "K-11 an over-wall release falls onto the silhouette: pieceFell{release} rows = d")]
        public void K11_an_over_wall_release_falls_onto_the_silhouette()
        {
            var s = LevelState(5);
            Run(s, Hand(5, 0)); // d shifts away, a is free
            var ev = Run(s, DragTo(0, 4, 5)).Ev; // a released in the crane area above column 4 (H = 5)
            var moved = Find<PieceMovedEvent>(ev);
            Assert.AreEqual(EntryKinds.OverWall, moved.Entry);
            Assert.AreEqual(At.Site(4, 5, 0), moved.To);
            var fell = Find<PieceFellEvent>(ev);
            Assert.AreEqual(2, fell.Step);
            Assert.AreEqual(FallCauses.Release, fell.Cause);
            CollectionAssert.AreEqual(new[] { At.ZoneSite, "4", "5" }, new[] { fell.From.Zone, fell.From.X.ToString(), fell.From.Y.ToString() });
            Assert.AreEqual(At.Site(4, 0, 0), fell.To);
            Assert.AreEqual(5, fell.Rows);
            var placed = Find<PlacementCorrectEvent>(ev);
            Assert.AreEqual(3, placed.Step);
            Assert.IsTrue(placed.OverWall);
        }

        [Test(Description = "K-17 a wrong placement bounces back to the start, burns 1 move and resets the streak")]
        public void K17_a_wrong_placement_bounces_back_to_the_start()
        {
            var s = LevelState(5);
            Run(s, Hand(5, 0)); // d (D2_90 W) → (2,2)
            Run(s, Hand(5, 1)); // a (O4 R) → (4,0), streak 1
            Assert.AreEqual(1, Hdr(s, H.Combo));
            var r = Run(s, DragTo(1, 5, 5)); // b (D2_0 W) falls onto (5,2)–(5,3), plan Y Y
            CollectionAssert.AreEqual(new[] { "pieceMoved", "pieceFell", "placementWrong", "pieceBounced", "comboChanged", "movesChanged" }, Types(r.Ev));
            var wrong = Find<PlacementWrongEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { VerdictReasons.Color }, wrong.Reasons);
            CollectionAssert.IsEmpty(wrong.MissingSupport);
            var bounced = Find<PieceBouncedEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { At.ZoneSite, "5", "2" }, new[] { bounced.From.Zone, bounced.From.X.ToString(), bounced.From.Y.ToString() });
            Assert.AreEqual(At.Yard(2, 0), bounced.To);
            Assert.IsFalse(bounced.ViaDrop);
            Assert.AreEqual(VerdictReasons.Color, bounced.Reason);
            Assert.AreEqual(0, Find<ComboChangedEvent>(r.Ev).Combo);
            CollectionAssert.AreEqual(new[] { 2, 0, Zone.Yard }, new[] { PieceX(s, 1), PieceY(s, 1), PieceZone(s, 1) });
            CollectionAssert.AreEqual(new[] { 14, 0, 1 }, new[] { Hdr(s, H.MovesLeft), Hdr(s, H.Combo), Hdr(s, H.WrongCount) });
            ExpectConsistent(s);
        }

        [Test(Description = "K-35 step 2: a glass break returns the block, skips step 3, costs base + 1 and resets the streak")]
        public void K35_step_2_a_glass_break_returns_the_block()
        {
            var s = Init(TWO_ROWS(Ps(Pc("D2_0", "W", 0, 0, new[] { "glass" }), Pc("D2_0", "W", 1, 0))));
            var hooks = new MoveHooks(fall: GLASS);
            Run(s, DragTo(1, 7, 8), hooks);
            Assert.AreEqual(1, Hdr(s, H.Combo));
            var r = Run(s, DragTo(0, 6, 8), hooks);
            CollectionAssert.AreEqual(new[] { "pieceMoved", "pieceFell", "glassBroke", "pieceReturned", "comboChanged", "movesChanged" }, Types(r.Ev));
            var broke = Find<GlassBrokeEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 2, 1 }, new[] { broke.Step, broke.Penalty });
            Assert.AreEqual(At.Site(6, 0, 0), broke.At);
            var returned = Find<PieceReturnedEvent>(r.Ev);
            Assert.AreEqual(2, returned.Step);
            Assert.AreEqual(At.Yard(0, 0), returned.To);
            var cost = Find<MovesChangedEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { -2, 1, 1 }, new[] { cost.Delta, cost.Cost!.Value.Base, cost.Cost.Value.Glass });
            CollectionAssert.AreEqual(new[] { Zone.Yard, 0, 17 }, new[] { PieceZone(s, 0), Hdr(s, H.Combo), Hdr(s, H.MovesLeft) });
            ExpectConsistent(s);
        }

        [Test(Description = "K-35 step 3: a sticking hook keeps the block on the site; the next move of it reads MoveScratch.wasStuck")]
        public void K35_step_3_a_sticking_hook_keeps_the_block_on_the_site()
        {
            var s = Init(TWO_ROWS(Ps(Pc("B1_0", "Y", 0, 0, new[] { "mortar" }))));
            var hooks = new MoveHooks(placement: MORTAR, moveCost: (ctx, id) => ctx.Scratch.WasStuck ? 2 : (int?)null);
            var stick = Run(s, DragTo(0, 6, 8), hooks);
            CollectionAssert.AreEqual(new[] { "pieceMoved", "pieceFell", "placementWrong", "mortarStuck", "movesChanged" }, Types(stick.Ev));
            Assert.AreEqual(VerdictReasons.Color, Find<MortarStuckEvent>(stick.Ev).Reason);
            CollectionAssert.AreEqual(new object[] { Zone.Site, true, 1 }, new object[] { PieceZone(s, 0), HasFlag(s, 0, FlagBit.Stuck), Hdr(s, H.WrongCount) });
            var back = Run(s, DragTo(0, 3, 0), hooks);
            var cost = Find<MovesChangedEvent>(back.Ev);
            CollectionAssert.AreEqual(new[] { -2, 2, 0 }, new[] { cost.Delta, cost.Cost!.Value.Base, cost.Cost.Value.Glass });
            CollectionAssert.AreEqual(new object[] { Zone.Yard, false }, new object[] { PieceZone(s, 0), HasFlag(s, 0, FlagBit.Stuck) });
            ExpectConsistent(s);
        }

        // --- K-35 steps 5–6: neighbour effects and yard gravity hooks ------------------------------------------------------------

        /// <summary>Records every neighbour call (as "entity/moved") and emits a probe event (its `step` shows where the hook ran).</summary>
        private static (MoveHooks Hooks, List<string> Calls) Recorder()
        {
            var calls = new List<string>();
            var hooks = new MoveHooks(onNeighborMoved: (ctx, entity, moved) =>
            {
                ctx.Emit(new ChainReleasedEvent(-1));
                calls.Add(entity + "/" + moved);
                return NeighborEffects.Affected;
            });
            return (hooks, calls);
        }

        [Test(Description = "K-35 step 5: yard entities next to the START cells, (y, x) order, once each, never across the wall boundary")]
        public void K35_step_5_yard_entities_next_to_the_start_cells()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW", "WW" },
                Pieces = Ps(
                    Pc("D2_90", "W", 2, 2), // 0: moved, start cells (2,2) (3,2)
                    Pc("B1_0", "W", 2, 1), // 1
                    Pc("B1_0", "W", 3, 1), // 2
                    Pc("D2_90", "W", 2, 3), // 3: next to both start cells → once
                    Pc("B1_0", "W", 5, 4)), // 4
                Debris = new[] { new D("D2_0", "R", 6, 0) },
                Obstacles = new[] { Crate(1, 2, 2) },
            });
            var (hooks, calls) = Recorder();
            var r = Run(s, DragTo(0, 4, 2), hooks);
            CollectionAssert.AreEqual(new[] { "piece 1/0", "piece 2/0", "obstacle 0/0", "piece 3/0" }, calls);
            Assert.IsTrue(r.Ev.OfType<ChainReleasedEvent>().All(e => e.Step == 5));

            // a block at x = 5 has no neighbour on the site (x = 6), whatever is there (GDD §0)
            var t = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 5, 1), Pc("B1_0", "W", 4, 1)),
                Debris = new[] { new D("D2_0", "R", 6, 0) },
                Obstacles = new[] { Crate(5, 0, 1) },
            });
            var rec = Recorder();
            Run(t, DragTo(0, 5, 2), rec.Hooks);
            CollectionAssert.AreEqual(new[] { "obstacle 0/0", "piece 1/0" }, rec.Calls);
        }

        [Test(Description = "K-35 step 6: a block falling by yard gravity affects the neighbours of its pre-fall cells")]
        public void K35_step_6_a_block_falling_by_yard_gravity_affects_the_neighbours_of_its_pre_fall_cells()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW" },
                Gravity = new GravitySpec { Yard = true },
                Pieces = Ps(
                    Pc("B1_0", "W", 0, 0), // 0: moved away
                    Pc("B1_0", "W", 0, 1)), // 1: falls to (0,0)
                Obstacles = new[] { Crate(1, 1, 1) },
            });
            var (hooks, calls) = Recorder();
            var r = Run(s, DragTo(0, 2, 0), hooks);
            CollectionAssert.AreEqual(new[] { "piece 1/0", "obstacle 0/1" }, calls);
            var fell = Find<PieceFellEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 6, 1, 1 }, new[] { fell.Step, fell.PieceId, fell.Rows });
            Assert.AreEqual(FallCauses.YardGravity, fell.Cause);
            Assert.AreEqual(At.Yard(0, 1), fell.From);
            Assert.AreEqual(At.Yard(0, 0), fell.To);
            ExpectConsistent(s);
        }

        [Test(Description = "K-35 step 5: hidden item check #1 asks the rule once the item cell is empty")]
        public void K35_step_5_hidden_item_check_1_asks_the_rule_once_the_item_cell_is_empty()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Obstacles = new[] { Screw(0, 0) },
            });
            var seen = new List<string>();
            var hooks = new MoveHooks(onCellUncovered: (ctx, obstacle) => seen.Add(obstacle + "@" + Hdr(ctx.S, H.Turn)));
            Run(s, DragTo(0, 3, 0), hooks);
            // checks #1 (step 5) and #2 (step 6) both see the empty cell; the rule marks it collected (Phase 3, Y7)
            CollectionAssert.AreEqual(new[] { "0@1", "0@1" }, seen);
        }

        // --- K-35 step 10 timers and step 12 -------------------------------------------------------------------------------------

        private static LevelSpec TIMED() => new LevelSpec
        {
            Wall = Wall(6,
                J.O("type", "shutter", "y", 0, "size", 1, "period", 2),
                J.O("type", "slider", "y", 2, "size", 1, "range", new[] { 2, 3 })),
            Plan = new[] { "WW" },
            Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "wet" }, 3), Pc("B1_0", "W", 3, 0)),
        };

        [Test(Description = "K-35 step 10 timer order is W4 → W5 → Y4 → K-40 (lvl.step10, not the rule order)")]
        public void K35_step_10_timer_order_is_W4_W5_Y4_K40()
        {
            var s = Init(TIMED());
            var order = new List<string>();
            Action<RuleContext> Tick(string id) => ctx =>
            {
                order.Add(id);
                ctx.Emit(new GapChangedEvent(order.Count, true, 0));
            };
            var timers = new Dictionary<string, Action<RuleContext>> { ["K-40"] = Tick("K-40"), ["Y4"] = Tick("Y4"), ["W5"] = Tick("W5"), ["W4"] = Tick("W4") };
            var r = Run(s, DragTo(1, 4, 0), new MoveHooks(timers: timers));
            CollectionAssert.AreEqual(new[] { "W4", "W5", "Y4", "K-40" }, order);
            CollectionAssert.AreEqual(new[] { 10, 10, 10, 10 }, r.Ev.OfType<GapChangedEvent>().Select(e => e.Step).ToArray());
        }

        [Test(Description = "K-35 step 10: a level timer without a rule hook throws \"Phase 3\"")]
        public void K35_step_10_a_level_timer_without_a_rule_hook_throws_Phase_3()
        {
            var s = Init(TIMED());
            var ex = Assert.Throws<NotImplementedException>(() => Run(s, DragTo(1, 4, 0)));
            StringAssert.Contains("Phase 3", ex!.Message);
        }

        [Test(Description = "K-35 step 12 runs while the level goes on — at 0 moves too, after the out-of-moves event (Faz 2R, K-29) — never after a win or with noTruckHelp")]
        public void K35_step_12_runs_while_the_level_goes_on()
        {
            var calls = new List<int>();
            var hooks = new MoveHooks(deadlock: ctx =>
            {
                calls.Add(Hdr(ctx.S, H.Turn));
                ctx.Emit(new DeadlockDetectedEvent(DeadlockReasons.NoMoves));
            });
            var s = Init(FULL());
            var first = Run(s, DragTo(1, 7, 8), hooks);
            Assert.AreEqual(12, Find<DeadlockDetectedEvent>(first.Ev).Step);
            Run(s, DragTo(0, 3, 6), hooks, noTruckHelp: true);
            var win = Run(s, DragTo(0, 6, 8), hooks);
            Assert.IsTrue(win.Res.Won);
            CollectionAssert.AreEqual(new[] { 1 }, calls);

            var t = Init(FULL());
            SetHdr(t, H.MovesLeft, 1);
            var out1 = Run(t, DragTo(0, 3, 6), hooks); // a yard move with the last move
            Assert.IsTrue(out1.Res.OutOfMoves);
            CollectionAssert.AreEqual(new[] { "outOfMoves", "deadlockDetected" }, Types(out1.Ev).Skip(Types(out1.Ev).Length - 2).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1 }, calls);
        }

        // --- K-28 / K-29 step 11 -------------------------------------------------------------------------------------------------

        [Test(Description = "K-28 E-01 the last move completes the last segment: won with 0 moves left, no out-of-moves")]
        public void K28_E01_the_last_move_completes_the_last_segment_won_with_0_moves_left()
        {
            var s = Init(FULL());
            Run(s, DragTo(1, 7, 8));
            SetHdr(s, H.MovesLeft, 1);
            var r = Run(s, DragTo(0, 6, 8));
            AssertResult(r.Res, MoveStatuses.Applied, null, true, false);
            var won = Find<LevelWonEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 11, 0 }, new[] { won.Step, won.MovesLeft });
            CollectionAssert.DoesNotContain(Types(r.Ev), "outOfMoves");
            Assert.IsTrue(Moves.IsLevelWon(s));
        }

        [Test(Description = "K-28 a won level takes no further drag")]
        public void K28_a_won_level_takes_no_further_drag()
        {
            var s = Init(FULL(Ps(Pc("Q9_0", "W", 3, 0))));
            Run(s, DragTo(1, 7, 8));
            Assert.IsTrue(Run(s, DragTo(0, 6, 8)).Res.Won);
            var ex = Assert.Throws<InvalidOperationException>(() => Run(s, DragTo(2, 3, 3)));
            StringAssert.Contains("won", ex!.Message);
        }

        [Test(Description = "K-29 the counter reaches 0 without a win: outOfMoves at step 11")]
        public void K29_the_counter_reaches_0_without_a_win_outOfMoves_at_step_11()
        {
            var s = LevelState(1);
            SetHdr(s, H.MovesLeft, 1);
            var r = Run(s, Hand(1, 0));
            AssertResult(r.Res, MoveStatuses.Applied, null, false, true);
            var last = r.Ev[r.Ev.Count - 1];
            Assert.IsInstanceOf<OutOfMovesEvent>(last);
            Assert.AreEqual(11, last.Step);
        }

        // --- K-41 goals in the pipeline --------------------------------------------------------------------------------------------

        [Test(Description = "E-27 the build is done but a clear goal is open: the level goes on and a site drop cancels (K-07 row 5)")]
        public void E27_the_build_is_done_but_a_clear_goal_is_open()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW" },
                Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "crate", "count", 1) },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 0, 1)),
                Obstacles = new[] { Crate(5, 0, 1) },
            });
            var done = Run(s, DragTo(0, 6, 8));
            AssertResult(done.Res, MoveStatuses.Applied, null, false, false);
            CollectionAssert.Contains(Types(done.Ev), "segmentCompleted");
            CollectionAssert.AreEqual(new[] { 1, 0 }, new[] { GoalValue(s, 0), GoalValue(s, 1) });
            var closed = Run(s, DragTo(1, 6, 8));
            Assert.AreEqual(CancelReasons.SiteClosed, closed.Res.Reason);
            AssertSingleCancel(closed.Ev, 1, CancelReasons.SiteClosed);
        }

        [Test(Description = "K-41 step 7: debris that leaves the site counts once for `clear: debris`")]
        public void K41_step_7_debris_that_leaves_the_site_counts_once()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW", "WW" },
                Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "debris", "count", 1) },
                Pieces = Ps(Pc("D2_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 1) },
            });
            var out1 = Run(s, DragTo(1, 5, 2));
            var moved = Find<PieceMovedEvent>(out1.Ev);
            Assert.AreEqual(At.Site(7, 1, 0), moved.From);
            Assert.AreEqual(EntryKinds.Yard, moved.Entry);
            var goal = Find<GoalProgressEvent>(out1.Ev);
            CollectionAssert.AreEqual(new[] { 7, 1, 1, 1 }, new[] { goal.Step, goal.Goal, goal.Value, goal.Target });
            var again = Run(s, DragTo(1, 4, 2));
            CollectionAssert.DoesNotContain(Types(again.Ev), "goalProgress");
            Assert.AreEqual(1, GoalValue(s, 1));
            ExpectConsistent(s);
        }

        // --- canonical JSON and FNV-1a --------------------------------------------------------------------------------------------

        [Test(Description = "K-35 canonicalJson sorts keys and drops undefined; fnv1a64 matches the reference vectors")]
        public void K35_canonicalJson_sorts_keys_and_fnv1a64_matches_the_reference_vectors()
        {
            // `c: undefined` has no JsonValue: J.O omits a null value, as `canonicalJson` drops `undefined`
            var value = J.O("b", 1, "a", J.O("d", J.A(2, J.O("z", 1, "y", 2)), "c", null));
            Assert.AreEqual("{\"a\":{\"d\":[2,{\"y\":2,\"z\":1}]},\"b\":1}", Moves.CanonicalJson(value));
            Assert.AreEqual("cbf29ce484222325", Moves.Fnv1a64(""));
            Assert.AreEqual("af63dc4c8601ec8c", Moves.Fnv1a64("a"));
            Assert.AreEqual("85944171f73967e8", Moves.Fnv1a64("foobar"));
            Assert.AreNotEqual(Moves.Fnv1a64("i"), Moves.Fnv1a64("ı"));
        }
    }
}
