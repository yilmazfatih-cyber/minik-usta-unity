#nullable enable
// Port of tests/obstacles/W1_staticGap.test.ts (web repo). Drags go through `Boards.Drop` / `Boards.Play`; the i18n card text
// check (src/i18n of the web repo) and the K-46 YAO counters (`measureYao`, pipeline step 3) come with the K-35 package.
using System;
using System.Linq;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests.Obstacles
{
    [TestFixture]
    public class W1StaticGapTests
    {
        /// <summary>Session of a pick with the level's registry hooks (what the scene does); throws when the block cannot be held.</summary>
        private static DragSession Session(GameState s, int id) => Drag(s, id, Registry.LevelHooks(s.Lvl).Drag);

        /// <summary>Wall 4, static gap row 2; plan (bottom → top) `..`, `..`, `RR`: only the gap reaches row 2 without falling.</summary>
        private static LevelSpec LINTEL() => new LevelSpec
        {
            Wall = Wall(4, Gap(2, 1)),
            Plan = new[] { "RR", "..", ".." },
            Pieces = Ps(Pc("D2_90", "R", 4, 2)),
        };

        [Test(Description = "W1 applies to levels with a static gap (levels 4 and 9) and carries the obs.w1.desc card (the TR/EN text lives in the web repo)")]
        public void W1_applies_to_levels_with_a_static_gap_and_carries_the_obs_w1_desc_card()
        {
            Assert.IsTrue(W1_StaticGap.Rule.AppliesTo(LevelFile(4)));
            Assert.IsTrue(W1_StaticGap.Rule.AppliesTo(LevelFile(9))); // W3 = a static gap of size 1
            Assert.IsFalse(W1_StaticGap.Rule.AppliesTo(LevelFile(3))); // wall without gaps
            Assert.IsFalse(W1_StaticGap.Rule.AppliesTo(LevelFile(5)));
            Assert.AreEqual("static", W1_StaticGap.Rule.Owns!.GapType);
            CollectionAssert.AreEqual(new[] { "W1" }, Registry.ActiveRuleIds(LevelFile(4)));
            CollectionAssert.AreEqual(new[] { "obs.w1.desc" }, Registry.InfoKeysFor(LevelFile(4)));
        }

        [Test(Description = "W1 K-04 the static gap is open from the level start and never closes (level 4 canonical solution)")]
        public void W1_K04_the_static_gap_is_open_from_the_level_start_and_never_closes()
        {
            var s = LevelState(4); // wall 4, gap y 0 size 2
            string[] Rows() => new[] { Ascii.WallChar(s, 0), Ascii.WallChar(s, 1) };
            Assert.AreEqual(1, GapField(s, 0, GF.Open));
            CollectionAssert.AreEqual(new[] { "=", "=" }, Rows());
            CollectionAssert.AreEqual(new[] { "#", "#" }, new[] { Ascii.WallChar(s, 2), Ascii.WallChar(s, 3) });
            foreach (var m in HandMoves(4))
            {
                Play(s, m);
                Assert.AreEqual(1, GapField(s, 0, GF.Open));
                CollectionAssert.AreEqual(new[] { "=", "=" }, Rows());
            }
        }

        [Test(Description = "W1 K-12 level 4 move 3: the buried O4 slides through the gap onto the rail and is placed without falling")]
        public void W1_K12_level_4_move_3_the_buried_O4_slides_through_the_gap_onto_the_rail()
        {
            var s = LevelState(4);
            Play(s, 4, 0, 2); // e: shift · c: shift, the corridor opens
            var r = Drop(s, 0, 4, 0, 0);
            Assert.AreEqual(DropKinds.SiteRail, r.Drop.Kind);
            Assert.AreEqual(0, r.Drop.Gap);
            Assert.IsTrue(r.Correct);
            CollectionAssert.AreEqual(new[] { Zone.Site, 4, 0 }, new[] { PieceZone(s, 0), PieceX(s, 0), PieceY(s, 0) });
        }

        [Test(Description = "W1 K-12 a rail block released over empty cells is held by the scaffold; over the wall it would fall (K-11)")]
        public void W1_K12_a_rail_block_released_over_empty_cells_is_held_by_the_scaffold()
        {
            var rail = Init(LINTEL());
            var held = Drop(rail, 0, 6, 2, 0);
            Assert.IsTrue(held.Correct);
            CollectionAssert.AreEqual(new[] { Site(6, 2), Site(7, 2) }, held.Outcome!.Cells);
            Assert.IsTrue(Goals.LevelGoalsMet(rail)); // the two `.` rows stay empty (S2)

            var free = Init(LINTEL());
            var fell = Drop(free, 0, 6, 8);
            Assert.AreEqual(PlacementOutcome.KindBounced, fell.Outcome!.Kind);
            Assert.AreEqual(A(6, 0), fell.Outcome.Landing);
            Assert.AreEqual(VerdictReasons.Window, fell.Outcome.Reason);
            Assert.AreEqual(YardP(4, 2), fell.Outcome.Target!.To);
        }

        [Test(Description = "W1 K-12 only a block whose rows all lie in the gap rows can enter the rail")]
        public void W1_K12_only_a_block_whose_rows_all_lie_in_the_gap_rows_can_enter_the_rail()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(6, Gap(2, 1)),
                Plan = WW(2),
                Pieces = Ps(
                    Pc("D2_0", "W", 0, 4), // vertical: two rows, the gap is row 2 only
                    Pc("D2_90", "W", 2, 0)), // horizontal: climbs to row 2 in the yard, then enters
            });
            bool HasRail(int id) => Session(s, id).ReachableNodes().Any(n => n.Mode == Movement.RailMode(0));
            Assert.IsFalse(HasRail(0));
            Assert.IsTrue(HasRail(1));
            Assert.IsTrue(Session(s, 1).ReachableNodes().Where(n => n.Mode == Movement.RailMode(0)).All(n => n.Iy == 2));
        }

        [Test(Description = "W1 K-12 the rail is entered rightwards from a position fully in the yard and left back into the yard")]
        public void W1_K12_the_rail_is_entered_rightwards_from_a_position_fully_in_the_yard()
        {
            var s = Init(LINTEL());
            var drag = Session(s, 0);
            Assert.AreEqual(N(4, 2), drag.Current);
            CollectionAssert.AreEqual(new[] { R(0, 5, 2), R(0, 6, 2) }, drag.PathTo(R(0, 6, 2)));
            // RAIL: horizontal only; the way out is a step left onto a node fully in the yard (FREE again)
            Assert.IsTrue(drag.Neighbours(R(0, 6, 2)).All(n => n.Iy == 2));
            CollectionAssert.Contains(drag.Neighbours(R(0, 5, 2)), N(4, 2));
            // no rail entry from the site side: a FREE node over the site never steps into the rail
            Assert.IsFalse(drag.Neighbours(N(6, 4)).Any(n => n.Mode != Movement.Free));
        }

        [Test(Description = "W1 K-07 a block released while straddling the gap is cancelled and burns no move")]
        public void W1_K07_a_block_released_while_straddling_the_gap_is_cancelled_and_burns_no_move()
        {
            var s = Init(LINTEL());
            int before = Hdr(s, H.MovesLeft);
            AssertCancel(Session(s, 0).Classify(R(0, 5, 2)), CancelReasons.Straddle);
            Assert.Throws<InvalidOperationException>(() => Drop(s, 0, 5, 2, 0)); // the strict TS `run` refuses a cancelled release too
            Assert.AreEqual(before, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(new[] { 4, 2 }, new[] { PieceX(s, 0), PieceY(s, 0) });
        }

        [Test(Description = "W1 K-45 a gap must leave a closed wall row above it: y + size ≤ height − 1 (gap_touches_top)")]
        public void W1_K45_a_gap_must_leave_a_closed_wall_row_above_it()
        {
            LevelSpec WithGap(int size)
            {
                var spec = LINTEL();
                spec.Wall = Wall(4, Gap(2, size));
                return spec;
            }
            var bad = Logic.ValidateLevelJson(Builders.LevelJson(WithGap(2)));
            CollectionAssert.Contains(bad.Issues.Select(i => i.Code).ToArray(), "gap_touches_top");
            var ok = Logic.ValidateLevelJson(Builders.LevelJson(WithGap(1)));
            CollectionAssert.DoesNotContain(ok.Issues.Select(i => i.Code).ToArray(), "gap_touches_top");
            var gap = Builders.Compiled(LINTEL()).Gaps[0];
            CollectionAssert.AreEqual(new object[] { "static", 2, 1 }, new object[] { gap.Type, gap.Y, gap.Size });
        }
    }
}
