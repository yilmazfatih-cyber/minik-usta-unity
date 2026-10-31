#nullable enable
// Port of tests/core/summary.test.ts (web repo): TECH 2R.15 presentation hooks (TurnSummary). The TS tests drive the state
// through `GameSession.apply`; here most drags go through `Boards.Drop` (the K-35 pipeline) and the summary is read directly;
// the package/summary coupling (`pack.Summary`, `pack.Teardown`, `game.Summary().BoosterTargets.Undo`) runs on `GameSession`.
using System.Linq;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class SummaryTests
    {
        /// <summary>LEVELS Bölüm 3 (Faz 2R draft): yard 4 × 4, site 2 × 5, wall 4; a D2_0 Y, b D2_0 W under c O4 G; d D2_90 Y.</summary>
        private static LevelSpec B3(string difficulty = "easy") => new LevelSpec
        {
            Id = 3,
            Difficulty = difficulty,
            Yard = new SizeSpec(4, 4),
            Site = new SizeSpec(2, 5),
            Wall = Wall(4),
            Plan = new[] { "YY", "GG", "GG", "YW", "YW" },
            Pieces = Ps(Pc("D2_0", "Y", 0, 0), Pc("D2_0", "W", 1, 0), Pc("O4_0", "G", 0, 2), Pc("D2_90", "Y", 2, 0)),
        };

        /// <summary>LEVELS Bölüm 9 (Faz 2R draft): no Ağır Yük, crate, bag, chain or debris.</summary>
        private static LevelSpec B9() => new LevelSpec
        {
            Id = 9,
            Difficulty = "normal",
            Yard = new SizeSpec(4, 4),
            Site = new SizeSpec(2, 6),
            Wall = Wall(4, Gap(0, 1)),
            Plan = new[] { "RR", "RR", "WW", "WW", "GG", "RR" },
            Pieces = Ps(Pc("D2_90", "R", 0, 0), Pc("D2_90", "G", 2, 0), Pc("O4_0", "R", 0, 1), Pc("O4_0", "W", 2, 2)),
        };

        private static int[] Pickable(GameState s)
        {
            var hooks = Registry.LevelHooks(s.Lvl);
            return Enumerable.Range(0, s.Lvl.Layout.Counts.Pieces).Where(id => Movement.TryBeginDrag(s, id, hooks.Drag).Ok).ToArray();
        }

        [Test(Description = "K-09 holdable list after every action and delivery: the summary equals K-09 (a)–(e) of the state after it")]
        public void K09_holdable_list_after_every_action_and_delivery()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 1),
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("O4_0", "Y", 2, 0)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "R", 4, 8)) } },
            });
            var hooks = Registry.LevelHooks(s.Lvl);
            CollectionAssert.AreEqual(Pickable(s), Summary.TurnSummary(s, hooks, false).Holdable);
            Drop(s, 1, 2, 2);
            CollectionAssert.AreEqual(Pickable(s), Summary.TurnSummary(s, hooks, true).Holdable);
            var shift = Drop(s, 0, 6, 8); // segment done → the truck block lands in the same package
            Assert.AreEqual(1, shift.Delivery.Delivered.Length);
            var holdable = Summary.TurnSummary(s, hooks, true).Holdable;
            CollectionAssert.Contains(holdable, 2);
            CollectionAssert.AreEqual(Pickable(s), holdable);
            SetHdr(s, H.MovesLeft, 0);
            CollectionAssert.IsEmpty(Summary.HoldableIds(s, hooks)); // (e) the counter
        }

        [Test(Description = "K-09 the µs up-step test never claims a block that the K-08 BFS cannot move (40 seeded boards)")]
        public void K09_the_us_up_step_test_never_claims_a_block_that_the_K08_BFS_cannot_move()
        {
            for (int n = 1; n <= 40; n++)
            {
                var s = Init(Cover.CoverLevel(n));
                var hooks = Registry.LevelHooks(s.Lvl);
                for (int id = 0; id < s.Lvl.Layout.Counts.Pieces; id++)
                    if (Deadlock.QuickHoldable(s, id, hooks.Drag)) Assert.IsTrue(Movement.TryBeginDrag(s, id, hooks.Drag).Ok, "level " + n + " piece " + id);
                CollectionAssert.AreEqual(Pickable(s), Summary.HoldableIds(s, hooks), "level " + n);
            }
        }

        [Test(Description = "K-34 hook 5 neededNow ignores access: Bölüm 3 start → {a, b}, both buried (no reachable correct release)")]
        public void K34_hook_5_neededNow_ignores_access()
        {
            var s = Init(B3());
            CollectionAssert.AreEqual(new[] { 0, 1 }, Deadlock.NeededNow(s));
            CollectionAssert.AreEqual(new[] { false, false }, new[] { Deadlock.HasReachableCorrect(s, 0), Deadlock.HasReachableCorrect(s, 1) });
            CollectionAssert.AreEqual(new[] { 0, 1 }, Summary.TurnSummary(s, Registry.LevelHooks(s.Lvl), false).NeededNow);
        }

        [Test(Description = "K-34 hook 5 unlockedNeeded after dig (GDD Bölüm 3 example): the c shift unlocks a, a's placement unlocks b; hard levels get nothing")]
        public void K34_hook_5_unlockedNeeded_after_dig()
        {
            var s = Init(B3());
            var hooks = Registry.LevelHooks(s.Lvl);
            var cache = new ReachCache();
            var prev = Summary.TurnSummary(s, hooks, false);
            CollectionAssert.IsEmpty(Summary.UnlockedNeeded(null, s, cache)); // level start only primes the cache
            Drop(s, 2, 1, 2);
            CollectionAssert.AreEqual(new[] { 0 }, Summary.UnlockedNeeded(prev, s, cache));
            prev = Summary.TurnSummary(s, hooks, true);
            Drop(s, 0, 4, 5); // a over the wall to (4,0)
            CollectionAssert.AreEqual(new[] { 1 }, Summary.UnlockedNeeded(prev, s, cache));

            var hard = Init(B3("hard"));
            var hc = new ReachCache();
            Summary.UnlockedNeeded(null, hard, hc);
            Drop(hard, 2, 1, 2);
            CollectionAssert.IsEmpty(Summary.UnlockedNeeded(null, hard, hc));
            Assert.IsFalse(hc.Primed);
        }

        [Test(Description = "K-26 queue entries in FIFO order with shape and colour; pending batches and their blocks for the \"+n\" badge (UX 5.9)")]
        public void K26_queue_entries_in_FIFO_order_with_shape_and_colour()
        {
            var s = Init(new LevelSpec
            {
                Yard = new SizeSpec(2, 2),
                Site = new SizeSpec(2, 1),
                Wall = Wall(1),
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("B1_0", "R", 0, 0), Pc("B1_0", "R", 1, 0), Pc("D2_90", "W", 0, 1)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("D2_90", "Y", 0, 2), Pc("B1_0", "G", 0, 2), Pc("B1_0", "Y", 1, 2)) } },
            });
            Assert.AreEqual(new PendingCounts(1, 3), Summary.PendingCounts(s));
            var r = Drop(s, 2, 2, 3); // segment 0 done; only row 1 is free: the D2_90 Y lands, the others wait
            CollectionAssert.AreEqual(new[] { 3 }, r.Delivery.Delivered.Select(d => d.PieceId).ToArray());
            CollectionAssert.AreEqual(new[] { new QueueEntry(4, "B1_0", "G"), new QueueEntry(5, "B1_0", "Y") }, Summary.QueueEntries(s));
            Assert.AreEqual(new PendingCounts(0, 0), Summary.PendingCounts(s));
        }

        [Test(Description = "UX 5.9 item 3 carryIds: a yard block bigger than what its colour still needs on the active segment (\"next floor\")")]
        public void UX_5_9_item_3_carryIds()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 1),
                Plans = new[] { new[] { "GW" }, new[] { "GG" } },
                Pieces = Ps(Pc("B1_0", "G", 0, 0), Pc("B1_0", "W", 1, 0), Pc("D2_90", "G", 2, 0)),
            });
            CollectionAssert.AreEqual(new[] { 2 }, Summary.CarryIds(s)); // G still needed on segment 0: 1 cell < 2
            Drop(s, 0, 6, 8);
            Drop(s, 1, 7, 8); // segment 1 comes
            CollectionAssert.IsEmpty(Summary.CarryIds(s));
        }

        [Test(Description = "K-30 teardown is the last event of the package; the summary describes the state after the Söküm")]
        public void K30_the_summary_describes_the_state_after_the_Sokum()
        {
            var game = GameSession.Start(Builders.Compiled(new LevelSpec
            {
                Wall = Wall(2),
                Site = new SizeSpec(2, 4),
                Plan = new[] { "WW", "WW", "YW", "YW" },
                Pieces = Ps(Pc("D2_0", "Y", 0, 0), Pc("D2_0", "W", 1, 0), Pc("O4_0", "W", 2, 0)),
            }));
            game.Apply(DragTo(0, 6, 8));
            var pack = game.Apply(DragTo(1, 6, 8));
            Assert.IsNotNull(pack.Teardown);
            Assert.AreSame(pack.Events[pack.Events.Length - 1], pack.Teardown);
            Assert.AreEqual(2, pack.Summary.BlocksLeft);
            CollectionAssert.Contains(pack.Summary.Holdable, 1);
            var ok = game.Apply(DragTo(1, 7, 8));
            Assert.IsNull(ok.Teardown);
        }

        [Test(Description = "E-61 hammer slot without target in level 9: grey, no \"+\", no purchase window (K-54); undo becomes a target after a drag")]
        public void E61_hammer_slot_without_target_in_level_9()
        {
            var game = GameSession.Start(Builders.Compiled(B9()));
            var t0 = game.Summary().BoosterTargets;
            Assert.IsFalse(t0.Hammer);
            string hammer = Summary.SlotState(true, t0.Hammer, 0);
            Assert.AreEqual(SlotStates.NoTarget, hammer);
            Assert.IsFalse(Summary.PurchaseOffered(hammer));
            Assert.IsFalse(t0.Undo);
            var pack = game.Apply(DragTo(1, 2, 1));
            Assert.IsTrue(pack.Summary.BoosterTargets.Undo);
            Assert.IsFalse(pack.Summary.BoosterTargets.Hammer);
        }
    }
}
