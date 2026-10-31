#nullable enable
// Port of tests/obstacles/Y3_chain.test.ts (web repo): drags and the hammer go through the K-35 pipeline (`Boards.Run` = TS
// `run`), the step-5 neighbour effect and the end-of-step-5 sweep are the pipeline's. The i18n card check (src/i18n of the
// web repo) stays there.
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;
using static MinikUsta.Core.Tests.Fixtures.ObstacleFixtures;

namespace MinikUsta.Core.Tests.Obstacles
{
    [TestFixture]
    public class Y3ChainTests
    {
        /// <summary>A chained B1 W at (0,0); its only neighbour is the B1 W at (1,0); a free D2_0 W at (4,0). Plan 2 × 2 W.</summary>
        private static LevelSpec LONELY(P[]? pieces = null, GravitySpec? gravity = null) => new LevelSpec
        {
            Wall = Wall(2),
            Site = new SizeSpec(2, 2),
            Plan = new[] { "WW", "WW" },
            Pieces = pieces ?? Ps(Pc("B1_0", "W", 0, 0, new[] { "chained" }), Pc("B1_0", "W", 1, 0), Pc("D2_0", "W", 4, 0)),
            Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "chain", "count", 1) },
            Gravity = gravity,
        };

        [Test(Description = "Y3 applies to levels with a chained block; it owns the `chained` flag and names its info card obs.y3.desc")]
        public void Y3_applies_to_levels_with_a_chained_block()
        {
            var lvl = Builders.Compiled(LONELY());
            Assert.IsTrue(Y3_Chain.Rule.AppliesTo(lvl));
            Assert.AreEqual(PieceFlags.Chained, Y3_Chain.Rule.Owns!.PieceFlag);
            CollectionAssert.AreEqual(new[] { "Y3" }, Registry.ActiveRuleIds(lvl));
            CollectionAssert.AreEqual(new[] { "obs.y3.desc" }, Registry.InfoKeysFor(lvl));
            CollectionAssert.AreEqual(new[] { "afterNeighbors", "drag", "hammer", "onNeighborMoved", "onTruckHelp" }, Registry.LevelHooks(lvl).DefinedKeys());
        }

        [Test(Description = "Y3 K-09 (c) a chained block cannot be held; E-13 a neighbour move frees it in step 5 and counts clear: chain")]
        public void Y3_K09_c_a_chained_block_cannot_be_held_E13_a_neighbour_move_frees_it()
        {
            var s = Init(LONELY());
            var hooks = Registry.LevelHooks(s.Lvl);
            AssertPickFails(Movement.TryBeginDrag(s, 0, hooks.Drag), PickFailures.Rule);
            var r = Run(s, DragTo(1, 6, 8)); // the neighbour leaves (1,0)
            var released = Find<ChainReleasedEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 5, 0 }, new[] { released.Step, released.PieceId });
            var goal = Find<GoalProgressEvent>(r.Ev);
            CollectionAssert.AreEqual(new[] { 7, 1, 1 }, new[] { goal.Step, goal.Goal, goal.Value });
            Assert.IsFalse(HasFlag(s, 0, FlagBit.Chained));
            Assert.IsTrue(Movement.TryBeginDrag(s, 0, hooks.Drag).Ok);
            ExpectConsistent(s);
        }

        [Test(Description = "E-53 a chain with no block, Ağır Yük, crate or bag left around it is released at the end of step 5 (no neighbour effect needed)")]
        public void E53_a_chain_with_no_neighbour_left_is_released_at_the_end_of_step_5()
        {
            // the free D2_0 is not a neighbour: its move does not touch (0,0); the chained block has its B1 neighbour …
            var s = Init(LONELY(Ps(Pc("B1_0", "W", 0, 0, new[] { "chained" }), Pc("D2_0", "W", 0, 1), Pc("B1_0", "W", 4, 0))));
            var first = Run(s, DragTo(2, 6, 8)); // far away: (0,0) keeps its neighbour (0,1)
            CollectionAssert.DoesNotContain(Types(first.Ev), "chainReleased");
            // … the D2_0 above leaves: a neighbour move (step 5 effect) frees it
            var second = Run(s, DragTo(1, 7, 8));
            var released = Find<ChainReleasedEvent>(second.Ev);
            CollectionAssert.AreEqual(new[] { 5, 0 }, new[] { released.Step, released.PieceId });

            // a chained block that loses its last neighbour through the hammer (Ağır Yük smashed, mini pipeline has no step-5
            // effect): the next drag's step 5 releases it (E-53)
            var t = Init(LONELY(Ps(Pc("B1_0", "W", 0, 0, new[] { "chained" }), Pc("I5_0", "W", 1, 0), Pc("D2_0", "W", 0, 2), Pc("B1_0", "W", 5, 7))));
            Run(t, new HammerMove(HammerTarget.Piece(1)));
            Assert.IsTrue(HasFlag(t, 0, FlagBit.Chained));
            var next = Run(t, DragTo(3, 6, 8)); // far from (0,0); (0,1) is empty, (1,0) is empty now
            var swept = Find<ChainReleasedEvent>(next.Ev);
            CollectionAssert.AreEqual(new[] { 5, 0 }, new[] { swept.Step, swept.PieceId });
            Assert.AreEqual(1, GoalValue(t, 1));
        }

        [Test(Description = "Y3 chained blocks still fall with yard gravity (Y6) after the release check")]
        public void Y3_chained_blocks_still_fall_with_yard_gravity_after_the_release_check()
        {
            var s = Init(LONELY(Ps(Pc("B1_0", "W", 0, 1, new[] { "chained" }), Pc("B1_0", "W", 0, 0), Pc("D2_0", "W", 1, 1)), new GravitySpec { Yard = true }));
            Run(s, DragTo(1, 6, 8)); // the block under it leaves: the chain goes (neighbour), the block falls
            CollectionAssert.AreEqual(new object[] { false, 0 }, new object[] { HasFlag(s, 0, FlagBit.Chained), PieceY(s, 0) });
        }
    }
}
