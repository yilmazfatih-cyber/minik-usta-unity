#nullable enable
// Port of tests/core/goals.test.ts (web repo). The level-5 test replays the canonical solution through `Boards.Play`
// (ComputeFall + SettlePlacement + segment completion + delivery) instead of `applyMove`.
using System;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class GoalsTests
    {
        private static LevelSpec GOALS() => new LevelSpec
        {
            Plans = new[] { new[] { "WW" }, new[] { "WW" } },
            Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "crate", "count", 2), J.O("type", "clear", "target", "debris", "count", 1) },
            Pieces = Ps(Pc("B1_0", "W", 0, 0)),
            Debris = new[] { new D("B1_0", "R", 7, 0) },
            Obstacles = new[] { Crate(4, 0, 1), Crate(5, 0, 1) },
        };

        [Test(Description = "K-41 goal kinds and targets: build counts segments, clear / collect their count")]
        public void K41_goal_kinds_and_targets()
        {
            var s = Init(GOALS());
            CollectionAssert.AreEqual(new[] { "build", "crate", "debris" }, Array.ConvertAll(s.Lvl.Goals, Goals.GoalKind));
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, new[] { Goals.GoalTarget(s.Lvl, 0), Goals.GoalTarget(s.Lvl, 1), Goals.GoalTarget(s.Lvl, 2) });
        }

        [Test(Description = "K-41 the build goal shows completed segments (level 5 after its first segment: 1/2)")]
        public void K41_the_build_goal_shows_completed_segments_level_5_after_its_first_segment()
        {
            var s = LevelState(5);
            Play(s, 5, 0, 5);
            CollectionAssert.AreEqual(new[] { new GoalView(0, "build", 1, 2, false) }, Goals.GoalViews(s));
        }

        [Test(Description = "K-41 a counter stops at its target; the surplus is not counted")]
        public void K41_a_counter_stops_at_its_target()
        {
            var s = Init(GOALS());
            var before = Goals.GoalSnapshot(s);
            CollectionAssert.AreEqual(new[] { 1 }, Goals.AddGoalCount(s, GoalKinds.Crate, 1));
            CollectionAssert.AreEqual(new[] { 1 }, Goals.AddGoalCount(s, GoalKinds.Crate, 5));
            CollectionAssert.IsEmpty(Goals.AddGoalCount(s, GoalKinds.Crate, 1));
            Assert.AreEqual(2, GoalValue(s, 1));
            CollectionAssert.IsEmpty(Goals.AddGoalCount(s, GoalKinds.Screw, 1));
            CollectionAssert.AreEqual(new[] { 1 }, Goals.ChangedCountGoals(s, before));
            Assert.Throws<ArgumentOutOfRangeException>(() => Goals.AddGoalCount(s, GoalKinds.Crate, -1));
        }

        [Test(Description = "K-41 debris counts once, when it leaves the site")]
        public void K41_debris_counts_once_when_it_leaves_the_site()
        {
            var s = Init(GOALS());
            const int debris = 1;
            Assert.IsFalse(Goals.CountDebrisLeftSite(s, debris, Zone.Site)); // still on the site
            Placement.MovePiece(s, debris, YardP(3, 0));
            Assert.IsTrue(Goals.CountDebrisLeftSite(s, debris, Zone.Site));
            Assert.AreEqual(1, GoalValue(s, 2));
            // moved again inside the yard (or broken later): its move started in the yard → no count
            Assert.IsFalse(Goals.CountDebrisLeftSite(s, debris, Zone.Yard));
            Assert.IsFalse(Goals.CountDebrisLeftSite(s, 0, Zone.Site)); // not debris
        }

        [Test(Description = "K-28 K-48 the level is won only with every segment complete, every extra goal met and no material block left")]
        public void K28_K48_the_level_is_won_only_with_every_segment_complete_every_extra_goal_met_and_no_material_block_left()
        {
            var s = Init(GOALS());
            CollectionAssert.AreEqual(new[] { false, false, false }, new[] { Goals.AllSegmentsComplete(s), Goals.ExtraGoalsMet(s), Goals.LevelGoalsMet(s) });
            SetHdr(s, H.DeliveryCursor, 2);
            CollectionAssert.AreEqual(new[] { 0 }, Goals.SetBuildProgress(s, 2));
            CollectionAssert.IsEmpty(Goals.SetBuildProgress(s, 2));
            Assert.IsFalse(Goals.LevelGoalsMet(s));
            Goals.AddGoalCount(s, GoalKinds.Crate, 2);
            Goals.AddGoalCount(s, GoalKinds.Debris, 1);
            Assert.IsTrue(Goals.ExtraGoalsMet(s));
            // K-48 (3): the yard block (material) still holds back the win
            CollectionAssert.AreEqual(new[] { true, false }, new[] { Goals.MaterialLeft(s), Goals.LevelGoalsMet(s) });
            Placement.MovePiece(s, 0, PiecePlace.Gone(0, 0));
            CollectionAssert.AreEqual(new[] { false, true }, new[] { Goals.MaterialLeft(s), Goals.LevelGoalsMet(s) });
            Assert.IsTrue(Array.TrueForAll(Goals.GoalViews(s), g => g.Done));
        }
    }
}
