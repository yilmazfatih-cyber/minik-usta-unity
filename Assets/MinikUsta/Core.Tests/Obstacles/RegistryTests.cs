#nullable enable
// Port of tests/obstacles/registry.test.ts (web repo): the registry table (TECH 7.1, 7.3), its validation and the hook
// composition. Hooks the TS tests exercise through `applyMove` (cancelled pick, move cost event, neighbour scan order, step-10
// timer run, S6 Phase 3 throw inside a move) run through `Moves.ApplyMove` (`Run`, P3); the direct calls on the composed
// `MoveHooks` are kept as C# additions.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;
using static MinikUsta.Core.Tests.Fixtures.ObstacleFixtures;

namespace MinikUsta.Core.Tests.Obstacles
{
    [TestFixture]
    public class RegistryTests
    {
        private static readonly int[] Levels1To10 = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        [Test(Description = "TECH 7.1 ALL_RULES is a valid table: W1, Y3, Y9-Y13 (v3 block traits), S1 (Faz 2R: S2 out of the MVP), each in its zone hundred (W → Y → S → G)")]
        public void TECH_7_1_AllRules_is_a_valid_table()
        {
            CollectionAssert.IsEmpty(Registry.ValidateRules(Registry.AllRules));
            CollectionAssert.AreEqual(new[] { "W1", "Y3", "Y9", "Y10", "Y11", "Y12", "Y13", "S1" }, Registry.AllRules.Select(r => r.Id).ToArray());
            foreach (var r in Registry.AllRules)
            {
                Assert.AreEqual(RuleZones.Of(r.Id), r.Zone);
                int bse = RuleZones.OrderBase(r.Zone);
                Assert.Greater(r.Order, bse);
                Assert.Less(r.Order, bse + 100);
            }
            Assert.Less(RuleZones.OrderBase(RuleZones.Wall), RuleZones.OrderBase(RuleZones.Yard));
            Assert.Less(RuleZones.OrderBase(RuleZones.Yard), RuleZones.OrderBase(RuleZones.Site));
            Assert.Less(RuleZones.OrderBase(RuleZones.Site), RuleZones.OrderBase(RuleZones.Gravity));
        }

        [Test(Description = "K-45 rule activation equals the OBSTACLES data signature of levels 1–10 (W1 in 4 and 9, S1 in 5, 7, 10; Faz 2R: S2 never)")]
        public void K45_rule_activation_equals_the_data_signature_of_levels_1_10()
        {
            var expected = new Dictionary<int, string[]>
            {
                { 1, new string[0] }, { 2, new string[0] }, { 3, new string[0] }, { 4, new[] { "W1" } }, { 5, new[] { "S1" } },
                { 6, new string[0] }, { 7, new[] { "S1" } }, { 8, new string[0] }, { 9, new[] { "W1" } }, { 10, new[] { "S1" } },
            };
            var ruleIds = new HashSet<string>(Registry.AllRules.Select(r => r.Id));
            foreach (int id in Levels1To10)
            {
                var lvl = LevelFile(id);
                CollectionAssert.AreEqual(expected[id], Registry.ActiveRuleIds(lvl), "level " + id);
                CollectionAssert.AreEqual(lvl.Mechanics.Where(m => ruleIds.Contains(m)).ToArray(), Registry.ActiveRuleIds(lvl));
            }
        }

        [Test(Description = "TECH 7.1 Faz 2R levels 1–10 get empty hooks: W1, W2, W3, Y5 and S1 are core models")]
        public void TECH_7_1_levels_1_10_get_empty_hooks()
        {
            foreach (int id in Levels1To10) Assert.IsTrue(Registry.LevelHooks(LevelFile(id)).IsEmpty, "level " + id);
        }

        [Test(Description = "TECH 7.1 hooks are composed once per compiled level; the debug panel switch composes fresh ones")]
        public void TECH_7_1_hooks_are_composed_once_per_compiled_level()
        {
            var lvl = LevelFile(4);
            Assert.AreSame(Registry.LevelHooks(lvl), Registry.LevelHooks(lvl));
            var off = new RuleOptions(disabled: new HashSet<string> { "W1" });
            CollectionAssert.IsEmpty(Registry.ActiveRuleIds(lvl, off));
            CollectionAssert.IsEmpty(Registry.InfoKeysFor(lvl, off));
            Assert.AreNotSame(Registry.LevelHooks(lvl), Registry.LevelHooks(lvl, off));
            var costly = new[] { FakeRule("Y8", moveCost: (ctx, id) => 2) };
            Assert.IsNotNull(Registry.LevelHooks(lvl, new RuleOptions(rules: costly)).MoveCost);
            Assert.IsTrue(Registry.LevelHooks(lvl, new RuleOptions(rules: costly, disabled: new HashSet<string> { "Y8" })).IsEmpty);
        }

        [Test(Description = "TECH 7.1 info card keys follow the level mechanics (obs.{id}.desc, OBSTACLES R-08)")]
        public void TECH_7_1_info_card_keys_follow_the_level_mechanics()
        {
            CollectionAssert.IsEmpty(Registry.InfoKeysFor(LevelFile(1)));
            CollectionAssert.IsEmpty(Registry.InfoKeysFor(LevelFile(3)));
            CollectionAssert.AreEqual(new[] { "obs.w1.desc" }, Registry.InfoKeysFor(LevelFile(4)));
            CollectionAssert.AreEqual(new[] { "obs.s1.desc" }, Registry.InfoKeysFor(LevelFile(5)));
            // S7 covers two mechanics: only the card of the one the level uses is shown
            var s7 = FakeRule("S7", infoKeys: new[] { "obs.s7r.desc", "obs.s7m.desc" },
                appliesTo: l => l.Mechanics.Contains("S7-R") || l.Mechanics.Contains("S7-M"));
            var repeat = Builders.Compiled(new LevelSpec
            {
                Plans = new[] { new[] { "WY" }, new[] { "??" } },
                Hidden = new Json.JsonValue?[] { null, J.O("kind", "repeat", "period", 1) },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
            });
            CollectionAssert.AreEqual(new[] { "obs.s7r.desc" }, Registry.InfoKeysFor(repeat, new RuleOptions(rules: new[] { s7 })));
        }

        // --- rule table validation (TECH 7.1, 7.3) --------------------------------------------------------------------------------------

        private static string ErrorsOf(params ObstacleRule[] rules) => string.Join("\n", Registry.ValidateRules(rules));

        private static void AssertMatch(string pattern, string text) => Assert.IsTrue(Regex.IsMatch(text, pattern), "expected /" + pattern + "/ in:\n" + text);

        [Test(Description = "K-35 step 10: onMoveEnd needs moveEndOrder equal to its STEP10_TIMERS position (TECH 7.3)")]
        public void K35_step_10_onMoveEnd_needs_moveEndOrder_equal_to_its_Step10Timers_position()
        {
            AssertMatch("onMoveEnd needs moveEndOrder", ErrorsOf(FakeRule("W4", onMoveEnd: Noop)));
            AssertMatch("moveEndOrder 2 ≠ K-35 step 10 position 1", ErrorsOf(FakeRule("W4", onMoveEnd: Noop, moveEndOrder: 2)));
            AssertMatch("moveEndOrder without onMoveEnd", ErrorsOf(FakeRule("W4", moveEndOrder: 1)));
            AssertMatch("no rule timer", ErrorsOf(FakeRule("Y1", onMoveEnd: Noop, moveEndOrder: 5)));
            // S5 / S6 ticks belong to the site strategy
            AssertMatch("no rule timer", ErrorsOf(FakeRule("S5", onMoveEnd: Noop, moveEndOrder: 3)));
            CollectionAssert.IsEmpty(Registry.ValidateRules(new[]
            {
                FakeRule("W4", onMoveEnd: Noop, moveEndOrder: 1),
                FakeRule("W5", onMoveEnd: Noop, moveEndOrder: 2),
                FakeRule("Y4", onMoveEnd: Noop, moveEndOrder: 5),
            }));
        }

        [Test(Description = "TECH 7.3 ids, zones and orders: unique, known, inside the zone hundred")]
        public void TECH_7_3_ids_zones_and_orders()
        {
            AssertMatch("duplicate id", ErrorsOf(FakeRule("W1"), FakeRule("W1")));
            AssertMatch("order 101 is also used by W1", ErrorsOf(FakeRule("W1"), FakeRule("W2", order: 101)));
            AssertMatch("outside 201…299", ErrorsOf(FakeRule("Y1", order: 150)));
            AssertMatch("zone site ≠ wall", ErrorsOf(FakeRule("W1", zone: RuleZones.Site)));
            AssertMatch("unknown rule id", ErrorsOf(FakeRule("W1").WithId("X9")));
        }

        [Test(Description = "TECH 7.1 ownership: one owner per entity type; dispatched hooks need an owned type")]
        public void TECH_7_1_ownership()
        {
            AssertMatch("gapType static is already owned by W1", ErrorsOf(FakeRule("W1", owns: new RuleOwnership(gapType: "static")), FakeRule("W4", owns: new RuleOwnership(gapType: "static"))));
            AssertMatch("need owns.gapType", ErrorsOf(FakeRule("W4", canPassGap: (s, g, id) => true)));
            AssertMatch("need owns.gapType", ErrorsOf(FakeRule("W6", onPassGap: (ctx, g, id) => { })));
            AssertMatch("needs owns.obstacle or", ErrorsOf(FakeRule("Y1", onNeighborMoved: (ctx, e, m) => NeighborEffects.None)));
            AssertMatch("onCellUncovered needs owns.obstacle", ErrorsOf(FakeRule("Y7", onCellUncovered: (ctx, o) => { }, owns: new RuleOwnership(pieceFlag: "glass"))));
        }

        [Test(Description = "TECH 7.1 info card keys: at least one, obs.{id}….desc (OBSTACLES R-08)")]
        public void TECH_7_1_info_card_keys()
        {
            AssertMatch("no info card key", ErrorsOf(FakeRule("W1", infoKeys: new string[0])));
            AssertMatch("info card key obs.w2.desc", ErrorsOf(FakeRule("W1", infoKeys: new[] { "obs.w2.desc" })));
            Assert.AreEqual("", ErrorsOf(FakeRule("G-H")));
            CollectionAssert.AreEqual(new[] { "obs.gh.desc" }, FakeRule("G-H").InfoKeys);
        }

        [Test(Description = "TECH 7.1 a malformed rule table is refused before any hook is composed")]
        public void TECH_7_1_a_malformed_rule_table_is_refused()
        {
            var bad = new[] { FakeRule("W4", onMoveEnd: Noop) };
            var ex = Assert.Throws<InvalidOperationException>(() => Registry.AssertValidRules(bad));
            AssertMatch("obstacle registry", ex!.Message);
            var ex2 = Assert.Throws<InvalidOperationException>(() => Registry.LevelHooks(LevelFile(1), new RuleOptions(rules: bad)));
            AssertMatch("moveEndOrder", ex2!.Message);
        }

        // --- hook composition ---------------------------------------------------------------------------------------------------------

        [Test(Description = "K-09 a block can be picked only when every active rule allows it (canPick)")]
        public void K09_a_block_can_be_picked_only_when_every_active_rule_allows_it()
        {
            var lvl = Builders.Compiled(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "chained" }), Pc("B1_0", "W", 2, 0, new[] { "glass" }), Pc("B1_0", "W", 4, 0)) });
            var s = CreateInitialState(lvl);
            var rules = new[]
            {
                FakeRule("Y3", canPick: (st, id) => !HasFlag(st, id, FlagBit.Chained)),
                FakeRule("S3", canPick: (st, id) => !HasFlag(st, id, FlagBit.Glass)),
            };
            var drag = Registry.LevelHooks(lvl, new RuleOptions(rules: rules)).Drag;
            AssertPickFails(Movement.TryBeginDrag(s, 0, drag), PickFailures.Rule);
            AssertPickFails(Movement.TryBeginDrag(s, 1, drag), PickFailures.Rule);
            Assert.IsTrue(Movement.TryBeginDrag(s, 2, drag).Ok);
            // Faz 2R: the registry's own Y3 rule forbids the chained block too (K-09 (c))
            AssertPickFails(Movement.TryBeginDrag(s, 0, Registry.LevelHooks(lvl).Drag), PickFailures.Rule);
            var r = Run(s, DragTo(0, 3, 0), Registry.LevelHooks(lvl, new RuleOptions(rules: rules)), strict: false);
            AssertResult(r.Res, MoveStatuses.Cancelled, CancelReasons.Invalid, false, false);
        }

        [Test(Description = "W1 static gaps stay passable when another gap rule gates its own type (ruleByGapType)")]
        public void W1_static_gaps_stay_passable_when_another_gap_rule_gates_its_own_type()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Wall = Wall(6, Gap(1, 1), J.O("type", "shutter", "y", 3, "size", 1, "period", 2, "phase", 0)),
                Plan = WW(4),
                Pieces = Ps(Pc("D2_90", "W", 4, 1), Pc("D2_90", "W", 4, 3)),
            });
            var s = CreateInitialState(lvl);
            var closedShutter = FakeRule("W4", owns: new RuleOwnership(gapType: "shutter"), canPassGap: (st, g, id) => false);
            var set = Registry.GetRuleSet(lvl, new RuleOptions(rules: new[] { closedShutter }));
            Assert.AreEqual("W4", set.RuleByGapType["shutter"].Id);
            var drag = Registry.LevelHooks(lvl, new RuleOptions(rules: new[] { closedShutter })).Drag!;
            Assert.IsTrue(drag.CanPassGap!(s, 0, 0)); // static: its open field (W1 has no gate)
            Assert.IsFalse(drag.CanPassGap(s, 1, 1)); // shutter: its owner decides
            bool RailNodes(int id, int gap, DragRules? rules)
            {
                var a = Movement.TryBeginDrag(s, id, rules);
                return a.Ok && a.Session!.ReachableNodes().Any(n => n.Mode == Movement.RailMode(gap));
            }
            Assert.IsTrue(RailNodes(0, 0, drag));
            Assert.IsFalse(RailNodes(1, 1, drag));
            Assert.IsTrue(RailNodes(1, 1, MoveHooks.None.Drag)); // the open shutter without its rule
        }

        [Test(Description = "K-07 move cost: the largest rule base wins and the glass penalty of onLanded is added (Y8 + S3 = 3)")]
        public void K07_move_cost_the_largest_rule_base_wins_and_the_glass_penalty_is_added()
        {
            var lvl = Builders.Compiled(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 3, 0, new[] { "glass" })) });
            var order = new List<string>();
            var rules = new[]
            {
                FakeRule("W8", onLanded: (st, id, fall) =>
                {
                    order.Add("W8");
                    return LandingEffect.None;
                }),
                FakeRule("S3", onLanded: (st, id, fall) =>
                {
                    order.Add("S3");
                    return HasFlag(st, id, FlagBit.Glass) && fall.Distance > st.Lvl.Gravity.GlassThreshold ? LandingEffect.Break(1) : LandingEffect.None;
                }),
                FakeRule("Y8", moveCost: (ctx, id) => 2),
                FakeRule("Y4", moveCost: (ctx, id) => null),
            };
            var s = CreateInitialState(lvl);
            var hooks = Registry.LevelHooks(lvl, new RuleOptions(rules: rules));
            var fall = Gravity.ComputeFall(s, 0, N(6, 8), new FallOptions(null, hooks.Fall));
            CollectionAssert.AreEqual(new[] { "W8", "S3" }, order);
            Assert.AreEqual(LandingEffect.Break(1), fall.Effect);
            Assert.AreEqual(2, hooks.MoveCost!(CtxOf(s).Ctx, 0));
            // through the pipeline (TS `run`): the same hooks, once each, and the cost event
            order.Clear();
            var r = Run(s, DragTo(0, 6, 8), hooks);
            CollectionAssert.AreEqual(new[] { "W8", "S3" }, order);
            Assert.AreEqual(1, Find<GlassBrokeEvent>(r.Ev).Penalty);
            var cost = Find<MovesChangedEvent>(r.Ev);
            Assert.AreEqual(-3, cost.Delta);
            Assert.AreEqual(new MoveCost(2, 1), cost.Cost);
        }

        [Test(Description = "K-35 step 2 modifyFall hooks chain in rule order: W8 before S8 (TECH 7.3)")]
        public void K35_step_2_modifyFall_hooks_chain_in_rule_order()
        {
            var lvl = Builders.Compiled(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 3, 0)) });
            var s = CreateInitialState(lvl);
            var calls = new List<string>();
            Func<GameState, int, FallPlan, FallPlan> Tag(string id) => (st, pid, plan) =>
            {
                calls.Add(id);
                return id == "W8" ? new FallPlan(plan.Ix + 1, plan.Iy, plan.Dir, 1, plan.Steer) : plan;
            };
            // table order S8, W8: the registry sorts by `order`
            var rules = new[] { FakeRule("S8", modifyFall: Tag("S8")), FakeRule("W8", modifyFall: Tag("W8")) };
            var fall = Gravity.ComputeFall(s, 0, N(6, 8), new FallOptions(null, Registry.LevelHooks(lvl, new RuleOptions(rules: rules)).Fall));
            CollectionAssert.AreEqual(new[] { "W8", "S8" }, calls);
            Assert.AreEqual(A(7, 0), fall.Landing);
        }

        [Test(Description = "K-35 step 3 onPlacement: a rule override (Y8 stick) is not masked by a default of another rule")]
        public void K35_step_3_onPlacement_a_rule_override_is_not_masked_by_a_default_of_another_rule()
        {
            var lvl = Builders.Compiled(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "Y", 3, 0, new[] { "mortar" })) });
            var s = CreateInitialState(lvl);
            var rules = new[]
            {
                FakeRule("S7", onPlacement: (st, id, cells, v) => PlacementOverrides.Default),
                FakeRule("Y8", onPlacement: (st, id, cells, v) => HasFlag(st, id, FlagBit.Mortar) ? PlacementOverrides.Stick : PlacementOverrides.Default),
            };
            var r = Run(s, DragTo(0, 6, 8), Registry.LevelHooks(lvl, new RuleOptions(rules: rules)));
            var stuck = Find<MortarStuckEvent>(r.Ev);
            Assert.AreEqual(0, stuck.PieceId);
            Assert.AreEqual(VerdictReasons.Color, stuck.Reason);
        }

        [Test(Description = "K-35 step 5 onNeighborMoved goes to the owner of the obstacle type or block flag only (Y1 crate, Y3 chain)")]
        public void K35_step_5_onNeighborMoved_goes_to_the_owner_of_the_obstacle_type_or_block_flag_only()
        {
            // moved block M (1,1); crate under it (1,0); plain block P left (0,1); chained block C right (2,1)
            var lvl = Builders.Compiled(new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "W", 1, 1), Pc("B1_0", "W", 0, 1), Pc("B1_0", "W", 2, 1, new[] { "chained" })),
                Obstacles = new[] { Crate(1, 0, 1) },
            });
            var calls = new List<string>();
            var rules = new[]
            {
                FakeRule("Y1", owns: new RuleOwnership(obstacle: "crate"), onNeighborMoved: (ctx, e, m) =>
                {
                    calls.Add("Y1:" + e.Kind);
                    return NeighborEffects.Affected;
                }),
                FakeRule("Y3", owns: new RuleOwnership(pieceFlag: "chained"), onNeighborMoved: (ctx, e, m) =>
                {
                    calls.Add("Y3:" + (e.IsPiece ? e.Id : -1));
                    return NeighborEffects.Affected;
                }),
                FakeRule("S3", owns: new RuleOwnership(pieceFlag: "glass"), onNeighborMoved: (ctx, e, m) =>
                {
                    calls.Add("S3");
                    return NeighborEffects.Freed;
                }),
            };
            var s = CreateInitialState(lvl);
            var hook = Registry.LevelHooks(lvl, new RuleOptions(rules: rules)).OnNeighborMoved!;
            var ctx = CtxOf(s).Ctx;
            // the pipeline scans the start cells' neighbours in (y, x) order: crate (1,0), P (0,1), C (2,1)
            Assert.AreEqual(NeighborEffects.Affected, hook(ctx, EntityRef.Obstacle(0), 0));
            Assert.AreEqual(NeighborEffects.None, hook(ctx, EntityRef.Piece(1), 0));
            Assert.AreEqual(NeighborEffects.Affected, hook(ctx, EntityRef.Piece(2), 0));
            CollectionAssert.AreEqual(new[] { "Y1:obstacle", "Y3:2" }, calls); // P and glass untouched
            // through the pipeline (TS `run(s, dragTo(0, 4, 0))`): step 5 scans the start cells' neighbours itself
            calls.Clear();
            Run(s, DragTo(0, 4, 0), Registry.LevelHooks(lvl, new RuleOptions(rules: rules)));
            CollectionAssert.AreEqual(new[] { "Y1:obstacle", "Y3:2" }, calls); // (y, x) order of the neighbour cells; P and glass untouched
        }

        [Test(Description = "K-42 onCellUncovered goes to the owner of the hidden item type (Y7 screw) at hidden item check #1")]
        public void K42_onCellUncovered_goes_to_the_owner_of_the_hidden_item_type()
        {
            var lvl = Builders.Compiled(new LevelSpec { Plan = new[] { "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Obstacles = new[] { Screw(0, 0) } });
            var rules = new[]
            {
                FakeRule("Y7", owns: new RuleOwnership(obstacle: "screw"), onCellUncovered: (ctx, obstacle) =>
                {
                    var o = ctx.Lvl.Obstacles[obstacle];
                    if (o.Type != "screw") return;
                    SetHiddenCollected(ctx.S, o.HiddenIndex, true);
                    ctx.Emit(new ScrewCollectedEvent(At.Yard(o.X, o.Y), 1));
                }),
                FakeRule("W7", owns: new RuleOwnership(obstacle: "key"), onCellUncovered: (ctx, obstacle) => Assert.Fail("no key here")),
            };
            var s = CreateInitialState(lvl);
            var r = Run(s, DragTo(0, 3, 0), Registry.LevelHooks(lvl, new RuleOptions(rules: rules)));
            Assert.AreEqual(5, Find<ScrewCollectedEvent>(r.Ev).Step);
            Assert.AreEqual(1, r.Ev.OfType<ScrewCollectedEvent>().Count());
            Assert.IsTrue(HiddenCollected(s, 0));
        }

        [Test(Description = "W6 onPassGap goes to the owner of the gap type (paint), never to the static gap of W1")]
        public void W6_onPassGap_goes_to_the_owner_of_the_gap_type()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Wall = Wall(6, Gap(1, 1), J.O("type", "paint", "y", 3, "size", 1, "color", "R")),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
            });
            var passed = new List<int>();
            var rules = new[] { Registry.AllRules[0], FakeRule("W6", owns: new RuleOwnership(gapType: "paint"), onPassGap: (ctx, gap, id) => passed.Add(gap)) };
            var hooks = Registry.LevelHooks(lvl, new RuleOptions(rules: rules));
            var ctx = CtxOf(CreateInitialState(lvl)).Ctx;
            hooks.OnPassGap!(ctx, 0, 0);
            hooks.OnPassGap(ctx, 1, 0);
            CollectionAssert.AreEqual(new[] { 1 }, passed);
        }

        [Test(Description = "K-35 step 10 rule onMoveEnd hooks run as lvl.step10 timers: W5 before Y4 whatever the table order")]
        public void K35_step_10_rule_onMoveEnd_hooks_run_as_step10_timers()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Wall = Wall(6, J.O("type", "slider", "y", 1, "size", 1, "range", new[] { 1, 3 }, "dir", 1)),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "wet" }, 3), Pc("B1_0", "W", 3, 0)),
            });
            CollectionAssert.AreEqual(new[] { "W5", "Y4" }, lvl.Step10.Select(t => t.Id).ToArray());
            var rules = new[]
            {
                FakeRule("Y4", onMoveEnd: ctx => ctx.Emit(new WetTickEvent(0, 2)), moveEndOrder: 5),
                FakeRule("W5", onMoveEnd: ctx => ctx.Emit(new GapChangedEvent(0, true, 2)), moveEndOrder: 2),
            };
            var hooks = Registry.LevelHooks(lvl, new RuleOptions(rules: rules));
            CollectionAssert.AreEqual(new[] { "W5", "Y4" }, hooks.Timers!.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            var captured = CtxOf(CreateInitialState(lvl));
            foreach (var t in lvl.Step10) hooks.Timers[t.Id](captured.Ctx); // the pipeline's `runTimers`
            CollectionAssert.AreEqual(new[] { "gapChanged", "wetTick" }, captured.Types());
            var r = Run(CreateInitialState(lvl), DragTo(1, 4, 0), hooks);
            CollectionAssert.AreEqual(new[] { "gapChanged", "wetTick" }, r.Ev.Where(e => e.Step == 10).Select(e => e.TypeName).ToArray());
        }

        [Test(Description = "K-24 the S6 elevator tick of step 10 comes from the registry by default (site strategy stub until Phase 3)")]
        public void K24_the_S6_elevator_tick_of_step_10_comes_from_the_registry_by_default()
        {
            var spec = new LevelSpec { Plan = WW(2), Elevator = Elevator(0, 2, 0, 1), Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 3, 0)) };
            var lvl = Builders.Compiled(spec);
            var tick = Registry.LevelHooks(lvl).Timers!["S6"];
            Assert.IsNotNull(tick);
            // default hooks = registry: the site tick runs and its Phase 3 stub throws
            var ex = Assert.Throws<NotImplementedException>(() => tick(CtxOf(CreateInitialState(lvl)).Ctx));
            AssertMatch("elevator scaffold tick", ex!.Message);
            var inMove = Assert.Throws<NotImplementedException>(() => Run(Init(spec), DragTo(1, 4, 0)));
            AssertMatch("elevator scaffold tick", inMove!.Message);
            // bare core: no timer hook at all
            Assert.IsNull(MoveHooks.None.Timers);
            var bare = Assert.Throws<NotImplementedException>(() => Run(Init(spec), DragTo(1, 4, 0), MoveHooks.None));
            AssertMatch("step-10 timer S6 has no rule hook", bare!.Message);
        }

        [Test(Description = "K-23 the S5 carousel tick is skipped in a move whose step 8 turned the platform (registry timer)")]
        public void K23_the_S5_carousel_tick_is_skipped_in_a_move_whose_step_8_turned_the_platform()
        {
            var lvl = Builders.Compiled(new LevelSpec { Plans = new[] { new[] { "WW" }, new[] { "WW" } }, Mode = "carousel", CarouselEvery = 2, Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            var tick = Registry.LevelHooks(lvl).Timers!["S5"];
            Assert.IsNotNull(tick);
            var turned = CtxOf(CreateInitialState(lvl), true);
            tick(turned.Ctx);
            CollectionAssert.IsEmpty(turned.Events);
            var plain = CtxOf(CreateInitialState(lvl), false);
            var ex = Assert.Throws<NotImplementedException>(() => tick(plain.Ctx)); // Site.cs carousel counter stub
            AssertMatch("Phase 3", ex!.Message);
        }
    }
}
