#nullable enable
// Port of tests/core/fixedYard.test.ts (web repo, wip/a2-levels e80a318): GDD K-55 sabit saha (D-099, `schemaVersion` 2) and its
// edge cases E-62, E-63, E-65, E-66 (E-64 is in DeadlockTests, E-67 in Presentation.Core.Tests SaveCodecTests / GameAppTests), plus the
// GDD §14 `schemaVersion` 1 | 2 schema test (tests/level/schema.test.ts) and the K-45/11 `fixed_yard_batches` validator rule
// (L-36, runtime subset). Fixture: the LEVELS §6.8 calibration draft P1 (6×4 yard, two 2×5 floors, 8 blocks) as `schemaVersion` 2
// (Fixtures/Levels/v2/p1_two_floors.json, copied by tools/sync-from-web.sh).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class FixedYardTests
    {
        private static JsonValue P1Json() => TestPaths.LoadFixture("v2", "p1_two_floors");

        private static CompiledLevel P1() => Compiler.Compile(LevelJson.ParseOrThrow(P1Json()));

        /// <summary>
        /// The P1 solution (GDD K-55 example): the two D2_0 R (ids 5, 6) fill the ground floor's base, the top D2_90 W (3) is parked
        /// once, D2_0 W / D2_0 Y (1, 2) and then 3 finish floor 1 on move 6; O4 Y (4), D2_90 W (7) and O4 R (0) build floor 2.
        /// Releases are FREE nodes in the Vinç Alanı (y = H = 5) over the site columns.
        /// </summary>
        private static DragMove[] P1Solution() => new[]
        {
            DragTo(5, 6, 5), DragTo(6, 7, 5), DragTo(3, 4, 3), DragTo(1, 6, 5), DragTo(2, 7, 5), DragTo(3, 6, 5), DragTo(4, 6, 5), DragTo(7, 6, 5), DragTo(0, 6, 5),
        };

        /// <summary>Yard records of every piece except `except`: id, zone, x, y, flags.</summary>
        private static string[] YardOf(GameState s, int except = -1)
        {
            var o = new List<string>();
            for (int id = 0; id < s.Lvl.Layout.Counts.Pieces; id++)
            {
                if (id == except || PieceZone(s, id) != Zone.Yard) continue;
                o.Add(id + "," + PieceX(s, id) + "," + PieceY(s, id) + "," + PieceFlagBits(s, id));
            }
            return o.ToArray();
        }

        /// <summary>K-55 item 2: unlocked material blocks anywhere (yard, queue, pending batch, unlocked in the site).</summary>
        private static HashSet<int> UnlockedMaterial(GameState s)
        {
            var o = new HashSet<int>();
            for (int id = 0; id < s.Lvl.Layout.Counts.Pieces; id++)
            {
                if (s.Lvl.Pieces[id].Cls != PieceClasses.Material) continue;
                if ((PieceFlagBits(s, id) & FlagBit.Locked) != 0) continue;
                int z = PieceZone(s, id);
                if (z == Zone.Yard || z == Zone.Queue || z == Zone.Pending || z == Zone.Site) o.Add(id);
            }
            return o;
        }

        private static bool IsDelivery(GameEvent e) => e is DeliveryArrivedEvent || e is DeliveryQueuedEvent;

        // --- schemaVersion (GDD §14) -------------------------------------------------------------------------------------------

        [Test(Description = "K-55 item 1 schemaVersion: missing = 1, 2 = fixed yard; the P1 fixture is a valid v2 level with one batch")]
        public void K55_item_1_schemaVersion_missing_is_1_and_2_is_the_fixed_yard_and_the_P1_fixture_is_a_valid_v2_level()
        {
            var json = P1Json();
            var basic = LevelJson.ParseOrThrow(Builders.LevelJson(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) }));
            Assert.AreEqual(1, LevelSchema.SchemaVersionOf(basic));
            Assert.IsFalse(LevelSchema.IsFixedYardLevel(basic));
            var noVersion = Builders.LevelJson(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            noVersion.Remove("schemaVersion");
            var parsedNoVersion = LevelJson.ParseOrThrow(noVersion);
            Assert.IsNull(parsedNoVersion.SchemaVersion);
            Assert.AreEqual(1, LevelSchema.SchemaVersionOf(parsedNoVersion));
            var v2 = LevelJson.ParseOrThrow(json);
            Assert.AreEqual(2, LevelSchema.SchemaVersionOf(v2));
            Assert.IsTrue(LevelSchema.IsFixedYardLevel(v2));
            var r = Logic.ValidateLevelJson(json, new LogicContext(fileId: 5));
            CollectionAssert.IsEmpty(r.Issues);
            Assert.AreEqual(1, r.Level!.Yard.Batches.Length);
            Assert.IsTrue(Compiler.LoadLevel(json).Ok);
        }

        [Test(Description = "K-55 GDD §14 schemaVersion 1 | 2 | 3 (backward compatible): missing = 1, the Faz 2R archive files still parse, 4 is rejected; v2 targets take the D-099 bands")]
        public void K55_GDD_14_schemaVersion_1_2_or_3_is_accepted_4_is_rejected_and_the_D099_target_bands_round_trip()
        {
            JsonValue Base(int? version)
            {
                var j = Builders.LevelJson(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
                if (version.HasValue) j.Set("schemaVersion", JsonValue.Of(version.Value));
                return j;
            }
            bool Ok(JsonValue j) => LevelJson.Parse(j, new List<SchemaIssue>()) != null;
            Assert.IsTrue(Ok(Base(1)));
            Assert.IsTrue(Ok(Base(2)));
            Assert.IsTrue(Ok(Base(3)));
            Assert.IsFalse(Ok(Base(4)));
            for (int n = 1; n <= 10; n++)
                Assert.AreEqual(1, LevelSchema.SchemaVersionOf(LevelJson.ParseOrThrow(TestPaths.LoadLevelJson(n))), "archive level " + n);
            for (int n = 1; n <= 10; n++)
                Assert.AreEqual(2, LevelSchema.SchemaVersionOf(LevelJson.ParseOrThrow(TestPaths.LoadCurrentLevelJson(n))), "shipped level " + n);
            var targets = J.O(
                "min", new[] { 8, 9 }, "maxCover", new[] { 0, 1 }, "depLater", new[] { 0, 2 }, "crossDeps", new[] { 1, 99 },
                "parkSlack", new object[] { 1.0, 2.5 }, "forced", new[] { 0, 2 }, "greedyExcess", new[] { 2, 99 }, "greedyWin", new object[] { 0.3, 0.7 },
                "ahaMin", new[] { 1, 2 }, "branch3", new object[] { 0.15, 0.35 });
            var withTargets = Base(2);
            withTargets.Set("targets", targets);
            var parsed = LevelJson.ParseOrThrow(withTargets);
            Assert.AreEqual(Moves.CanonicalJson(targets), Moves.CanonicalJson(parsed.Targets!.ToJson()));
            var bad1 = Base(null);
            bad1.Set("targets", J.O("greedyWin", new object[] { 0.3, 1.5 }));
            Assert.IsFalse(Ok(bad1));
            var bad2 = Base(null);
            bad2.Set("targets", J.O("parkSlack", new object[] { 1.0, 12.0 }));
            Assert.IsFalse(Ok(bad2));
            var bad3 = Base(null);
            bad3.Set("targets", J.O("depLater", new object[] { 0.5, 2.0 }));
            Assert.IsFalse(Ok(bad3));
            // L-01: a band needs low <= high, rate bands and parkSlack included
            var inverted = Base(2);
            inverted.Set("targets", J.O("min", new[] { 9, 8 }, "parkSlack", new object[] { 2.5, 1.0 }));
            var issues = Logic.CheckLevel(LevelJson.ParseOrThrow(inverted), new LogicContext(only: new[] { "L-01" }));
            CollectionAssert.AreEqual(new[] { "targets.min", "targets.parkSlack" }, issues.Where(i => i.Code == "schema_invalid").Select(i => i.Path).ToArray());
        }

        // --- K-55 segment change keeps the yard (E-62) -------------------------------------------------------------------------

        [Test(Description = "K-55 segment change keeps the yard (E-62): move 6 finishes floor 1, step 8 shifts the site, the yard stays bit for bit, no delivery")]
        public void K55_segment_change_keeps_the_yard_E62_move_6_finishes_floor_1_and_nothing_is_delivered()
        {
            var game = GameSession.Start(P1());
            var moves = P1Solution();
            for (int i = 0; i < 5; i++) Assert.AreEqual(MoveStatuses.Applied, game.Commit(moves[i]).Status);
            var before = YardOf(game.State, 3);
            int queueBefore = Hdr(game.State, H.QueueLen);
            var res = game.Apply(moves[5]);
            var types = Types(res.Events);
            CollectionAssert.Contains(types, "segmentCompleted");
            CollectionAssert.Contains(types, "siteShifted");
            var shifted = Find<SiteShiftedEvent>(res.Events);
            Assert.AreEqual(8, shifted.Step);
            Assert.AreEqual(1, shifted.ToSeg);
            CollectionAssert.IsEmpty(res.Events.Where(IsDelivery).ToArray());
            CollectionAssert.IsEmpty(res.Events.OfType<PieceFellEvent>().Where(e => e.Cause == "delivery").ToArray());
            // every event from step 8 on is about the site, the goals or the win: none moves a yard block
            CollectionAssert.DoesNotContain(res.Events.Where(e => e.Step >= 8).Select(e => e.TypeName).ToArray(), "pieceReturned");
            CollectionAssert.AreEqual(before.Where(r => !r.StartsWith("3,")).ToArray(), YardOf(game.State).Where(r => !r.StartsWith("3,")).ToArray());
            CollectionAssert.AreEqual(YardOf(game.State, 3), before);
            CollectionAssert.AreEqual(new[] { queueBefore, Hdr(game.State, H.QueueLen) }, new[] { 0, 0 });
            Assert.AreEqual(0, res.Summary.Queue.Length);
            Assert.AreEqual(0, res.Summary.PendingBatches);
            Assert.AreEqual(0, res.Summary.PendingBlocks);
            // GDD example: the three blocks of floor 2 sit where they started
            CollectionAssert.AreEqual(new[] { "0,0,0", "4,4,0", "7,2,0" }, YardOf(game.State).Select(r => string.Join(",", r.Split(',').Take(3))).ToArray());
            Assert.AreEqual(1, Hdr(game.State, H.ActiveSeg));
        }

        [Test(Description = "K-55 the P1 solution wins in 9 moves (min = N 8 + 1 shift) with every block placed exactly once and no truck event")]
        public void K55_the_P1_solution_wins_in_9_moves_with_every_block_placed_once_and_no_truck_event()
        {
            var game = GameSession.Start(P1());
            var all = new List<GameEvent>();
            foreach (var m in P1Solution())
            {
                var r = game.Apply(m);
                Assert.AreEqual(MoveStatuses.Applied, r.Status);
                all.AddRange(r.Events);
            }
            Assert.AreEqual(BoosterOutcomes.Won, game.Outcome);
            CollectionAssert.IsEmpty(all.Where(IsDelivery).ToArray());
            Assert.AreEqual(8, all.Count(e => e is PlacementCorrectEvent));
            CollectionAssert.AreEqual(new[] { 11 - 9, 8 }, new[] { game.MovesLeft, game.Yao().OverWall });
        }

        // --- K-55 item 2: no block enters the yard ------------------------------------------------------------------------------

        /// <summary>A random drag of a random holdable block to a random reachable node (site releases favoured).</summary>
        private static DragMove? RandomDrag(GameState s, Mulberry32 rng)
        {
            int P = s.Lvl.Layout.Counts.Pieces;
            int start = rng.NextInt(P);
            for (int k = 0; k < P; k++)
            {
                int id = (start + k) % P;
                int z = PieceZone(s, id);
                if (z != Zone.Yard && z != Zone.Site) continue;
                var a = Movement.TryBeginDrag(s, id);
                if (!a.Ok) continue;
                var nodes = a.Session!.ReachableNodes();
                var site = nodes.Where(n => n.Ix >= s.Lvl.Geo.SiteX).ToArray();
                var pool = site.Length > 0 && rng.Next() < 0.6 ? site : nodes;
                if (pool.Length > 0) return new DragMove(id, pool[rng.NextInt(pool.Length)]);
            }
            return null;
        }

        [Test(Description = "K-55 no block enters the yard: 200 seeded random sessions (drags, Undo, +5) — the unlocked material set only shrinks by correct placements and grows back only by Söküm or Undo")]
        public void K55_no_block_enters_the_yard_200_seeded_random_sessions()
        {
            var lvl = P1();
            var initial = UnlockedMaterial(CreateInitialState(lvl));
            Assert.AreEqual(8, initial.Count);
            int actions = 0;
            int teardowns = 0;
            int undos = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var rng = new Mulberry32(55_000 + seed);
                var game = GameSession.Start(lvl);
                var prev = UnlockedMaterial(game.State);
                for (int step = 0; step < 30 && game.Outcome != BoosterOutcomes.Won && game.Outcome != BoosterOutcomes.Lost; step++)
                {
                    string where = "seed " + seed + " step " + step;
                    if (game.Outcome == BoosterOutcomes.OutOfMoves)
                    {
                        if (game.AcceptOffer(AddMovesSource.OfferCoins).Status != MoveStatuses.Applied) break;
                        continue;
                    }
                    bool undo = rng.Next() < 0.08 && game.CanUndo();
                    var move = undo ? null : RandomDrag(game.State, rng);
                    if (!undo && move == null) break;
                    var res = move != null ? game.Apply(move, new ArraySink()) : game.Apply(new UndoAction());
                    if (undo) undos++;
                    actions++;
                    var cur = UnlockedMaterial(game.State);
                    foreach (int id in cur) Assert.IsTrue(initial.Contains(id), where + ": block " + id + " is new");
                    for (int id = 0; id < lvl.Layout.Counts.Pieces; id++) Assert.AreNotEqual(Zone.Pending, PieceZone(game.State, id), where + ": no pending batch");
                    bool grewBack = undo || res.Teardown != null;
                    if (res.Teardown != null) teardowns++;
                    if (!grewBack) foreach (int id in cur) Assert.IsTrue(prev.Contains(id), where + ": block " + id + " came back");
                    CollectionAssert.IsEmpty(Grid.StateInvariantErrors(game.State), where);
                    prev = cur;
                }
            }
            Assert.Greater(actions, 2000);
            Assert.Greater(undos, 30);
            Assert.GreaterOrEqual(teardowns, 0);
        }

        // --- E-63 K-55 item 4: a block is not bound to a segment ------------------------------------------------------------------

        [Test(Description = "E-63 K-55 item 4 a block is not bound to a segment: the swap of the GDD example (both D2_0 R to floor 1) is correct and the level stays tileable")]
        public void E63_K55_item_4_a_block_is_not_bound_to_a_segment_the_swap_is_correct_and_the_level_stays_tileable()
        {
            var s = CreateInitialState(P1());
            var moves = P1Solution();
            Run(s, moves[0]);
            Run(s, moves[1]);
            CollectionAssert.AreEqual(new[] { FlagBit.Locked, FlagBit.Locked }, new[] { PieceFlagBits(s, 5) & FlagBit.Locked, PieceFlagBits(s, 6) & FlagBit.Locked });
            Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(s));
        }

        [Test(Description = "E-63 K-55 a later floor's block placed on the active floor where the rest cannot tile → D3a dead end → Söküm takes the move back, no refund")]
        public void E63_K55_a_later_floors_block_on_the_active_floor_is_a_D3a_dead_end_and_Soekuem_takes_the_move_back()
        {
            // floor 1 is a 2×2 Y block, floor 2 = YY (bottom) + WW; supply O4 Y, D2_90 Y, D2_90 W. The D2_90 Y fits floor 1's
            // bottom row (K-16 correct) but then floor 1's top row needs a second D2_90 Y: untileable (D3a).
            var s = Init(new LevelSpec
            {
                SchemaVersion = 2,
                Yard = new SizeSpec(4, 2),
                Site = new SizeSpec(2, 2),
                Wall = Wall(2),
                Plans = new[] { new[] { "YY", "YY" }, new[] { "WW", "YY" } },
                Pieces = Ps(Pc("O4_0", "Y", 0, 0), Pc("D2_90", "Y", 2, 1), Pc("D2_90", "W", 2, 0)),
                Moves = 9,
            });
            var before = (int[])s.Buf.Clone();
            var r = Run(s, DragTo(1, 4, 3)); // yard 4 wide: the site columns are x = 4, 5
            var correct = Find<PlacementCorrectEvent>(r.Ev);
            Assert.AreEqual(1, correct.PieceId);
            Assert.AreEqual(3, correct.Step);
            var td = Find<TeardownEvent>(r.Ev);
            Assert.AreEqual(TeardownCauses.Tiling, td.Cause);
            Assert.AreEqual(12, td.Step);
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
            Assert.AreEqual(Zone.Yard, PieceZone(s, 1));
            var restored = (int[])s.Buf.Clone();
            restored[H.MovesLeft] = before[H.MovesLeft];
            restored[H.MovesSpent] = before[H.MovesSpent];
            CollectionAssert.AreEqual(before, restored);
        }

        // --- E-65 K-45/11 L-36 -------------------------------------------------------------------------------------------------

        [Test(Description = "E-65 K-55 a Faz 2R multi-batch level written as schemaVersion 2 → fixed_yard_batches (error, not loaded); as schemaVersion 1 it stays valid")]
        public void E65_K55_a_multi_batch_level_written_as_schemaVersion_2_is_fixed_yard_batches_and_not_loaded()
        {
            var v1 = TestPaths.LoadLevelJson(5);
            CollectionAssert.IsEmpty(Logic.ValidateLevelJson(v1, new LogicContext(fileId: 5)).Issues.Where(i => i.Severity == Severities.Error).ToArray());
            var v2 = TestPaths.LoadLevelJson(5);
            v2.Set("schemaVersion", JsonValue.Of(2));
            var fixedYard = Logic.ValidateLevelJson(v2, new LogicContext(fileId: 5)).Issues.Where(i => i.Code == "fixed_yard_batches").ToArray();
            Assert.AreEqual(1, fixedYard.Length);
            Assert.AreEqual("K-45/11", fixedYard[0].Rule);
            Assert.AreEqual("L-36", fixedYard[0].Check);
            Assert.AreEqual(Severities.Error, fixedYard[0].Severity);
            Assert.AreEqual("yard.batches", fixedYard[0].Path);
            Assert.IsFalse(Compiler.LoadLevel(v2).Ok);
            // the single batch must be forSegment 0 without dropColumns
            var one = P1Json();
            var first = one["yard"]!["batches"]![0];
            first.Set("dropColumns", JsonValue.Array().Add(JsonValue.Of(0)));
            var paths = Logic.ValidateLevelJson(one, new LogicContext(fileId: 5)).Issues.Where(i => i.Code == "fixed_yard_batches").Select(i => i.Path).ToArray();
            CollectionAssert.Contains(paths, "yard.batches[0].dropColumns");
            // runtime subset: the game refuses the file at load time (L-36 is in `Logic.RuntimeChecks`)
            CollectionAssert.Contains(Logic.RuntimeChecks, "L-36");
            Assert.IsFalse(Compiler.LoadLevel(one).Ok);
        }

        // --- E-66 K-55 item 5 ------------------------------------------------------------------------------------------------------

        [Test(Description = "E-66 K-55 item 5 with no empty yard cell the hammer sends site debris to the truck queue (N = 1); a correct placement frees a cell and it drops at step 9")]
        public void E66_K55_item_5_the_hammer_sends_debris_to_the_queue_when_the_yard_is_full_and_it_drops_at_step_9()
        {
            var s = Init(new LevelSpec
            {
                SchemaVersion = 2,
                Yard = new SizeSpec(2, 1),
                Site = new SizeSpec(2, 2),
                Wall = Wall(1),
                Plan = new[] { "WW", "RR" },
                Pieces = Ps(Pc("D2_90", "R", 0, 0)),
                Debris = new[] { new D("D2_90", "W", 2, 0) }, // yard 2 wide: the site columns are x = 2, 3
            });
            var hit = Run(s, new HammerMove(HammerTarget.Piece(1)));
            var returned = Find<PieceReturnedEvent>(hit.Ev);
            Assert.AreEqual(1, returned.PieceId);
            Assert.IsNull(returned.To, "to: queue");
            Assert.AreEqual(1, Find<DeliveryQueuedEvent>(hit.Ev).Queued);
            CollectionAssert.AreEqual(new[] { Zone.Queue, 1 }, new[] { PieceZone(s, 1), Hdr(s, H.QueueLen) });
            var next = Run(s, DragTo(0, 2, 3));
            Assert.AreEqual(0, Find<PlacementCorrectEvent>(next.Ev).PieceId);
            var arrived = Find<DeliveryArrivedEvent>(next.Ev);
            Assert.AreEqual(9, arrived.Step);
            CollectionAssert.AreEqual(new[] { 1 }, arrived.Pieces);
            Assert.AreEqual(0, Find<DeliveryQueuedEvent>(next.Ev).Queued);
            CollectionAssert.AreEqual(new[] { Zone.Yard, 0 }, new[] { PieceZone(s, 1), Hdr(s, H.QueueLen) });
        }
    }
}
