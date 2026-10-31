#nullable enable
// Port of tests/review/movement.review.test.ts — the tests that go through the K-35 pipeline (`Moves.ApplyMove` with
// `noTruckHelp`, TS `NO_HELP`): K-07 costs and cancels, E-30, K-10 / K-11 / K-12 / K-13 pipeline examples, the round-2
// crane transit, later-segment debris after a slide, N14 pull-out, the Bölüm 1/4/9 drag gloves after canonical moves and
// S2 `.` through a placement (P3). Same partial class as MovementReviewTests.cs.
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    public partial class MovementReviewTests
    {
        private static DragNode F(int ix, int iy) => N(ix, iy);
        private static DragNode RL(int gap, int ix, int iy) => R(gap, ix, iy);

        /// <summary>TS `NO_HELP` + `applyMove(s, drop(id, to), undefined, NO_HELP)`.</summary>
        private static MoveResult Drop_(GameState s, int id, DragNode to) => Moves.ApplyMove(s, new DragMove(id, to), null, new ApplyOptions(noTruckHelp: true));

        private static void AssertCancelled(MoveResult r, string reason, string? label = null)
        {
            Assert.AreEqual(MoveStatuses.Cancelled, r.Status, label);
            Assert.AreEqual(reason, r.Reason, label);
        }

        private static int[] Spot(GameState s, int id) => new[] { PieceZone(s, id), PieceX(s, id), PieceY(s, id) };

        // --- K-07 release table ------------------------------------------------------------------------------------------

        [Test(Description = "K-07 GDD example: B1 from (2,6) released in the crane area at (2,8) cancels (12 → 12, state unchanged); released at the empty (4,7) it is a yard move (12 → 11)")]
        public void K07_GDD_example_B1_released_in_the_crane_area_cancels_released_at_the_empty_4_7_it_is_a_yard_move()
        {
            var s = Init(new LevelSpec { Moves = 12, Wall = Wall(6), Pieces = FullYard(Ps(Pc("B1_0", "Y", 2, 6)), (2, 7), (4, 7)) });
            var d = Drag(s, 0);
            AssertCancel(d.Classify(F(2, 8)), CancelReasons.CraneOverYard, 3);
            AssertDrop(d.Classify(F(4, 7)), DropKinds.Yard, null, 2);

            var before = Snapshot(s);
            AssertCancelled(Drop_(s, 0, F(2, 8)), CancelReasons.CraneOverYard);
            Assert.AreEqual(12, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(before, s.Buf); // "İptal edilen bırakmada hiçbir durum değişmez"

            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, F(4, 7)).Status);
            Assert.AreEqual(11, Hdr(s, H.MovesLeft));
            CollectionAssert.AreEqual(new[] { 4, 7, Zone.Yard }, new[] { PieceX(s, 0), PieceY(s, 0), PieceZone(s, 0) });
        }

        [Test(Description = "K-07 row 5 / E-27: build complete but a clear goal open → every site release is cancelled at no cost; yard moves still cost 1")]
        public void K07_row_5_E27_build_complete_but_a_clear_goal_open_every_site_release_is_cancelled()
        {
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Wall = Wall(2),
                Plan = new[] { "WW" },
                Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "crate", "count", 1) },
                Obstacles = new[] { Crate(0, 0) },
                Pieces = Ps(Pc("D2_90", "W", 2, 0), Pc("B1_0", "Y", 4, 0), Pc("D2_90", "Y", 2, 3)),
            });
            var first = Drop_(s, 0, F(6, 2));
            Assert.AreEqual(MoveStatuses.Applied, first.Status);
            Assert.IsFalse(first.Won);
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            Assert.AreEqual(9, Hdr(s, H.MovesLeft));

            var d = Drag(s, 1);
            AssertCancel(d.Classify(F(6, 8)), CancelReasons.SiteClosed, 5);
            AssertCancel(d.Classify(F(7, 1)), CancelReasons.SiteClosed, 5);
            // rows 2–4 are tried before row 5
            AssertDrop(d.Classify(F(4, 1)), DropKinds.Yard, null, 2);
            AssertDrop(Drag(s, 2).Classify(F(5, 8)), null, CancelReasons.Straddle, 4);
            var before = Snapshot(s);
            AssertCancelled(Drop_(s, 1, F(7, 8)), CancelReasons.SiteClosed);
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 1, F(4, 1)).Status);
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "K-07 E-06 E-28 a release straddling the boundary (half-way through a gap, or over the wall in the crane rows) is cancelled and changes nothing")]
        public void K07_E06_E28_a_release_straddling_the_boundary_is_cancelled_and_changes_nothing()
        {
            var s = Init(new LevelSpec { Moves = 7, Wall = Wall(6, Gap(2, 1)), Pieces = Ps(Pc("D2_90", "W", 3, 2)) });
            var d = Drag(s, 0);
            Assert.IsTrue(d.IsReachable(RL(0, 5, 2)));
            Assert.IsTrue(d.IsReachable(F(5, 8)));
            Assert.IsTrue(d.IsReachable(F(5, 6))); // above the wall top: also a straddle
            var before = Snapshot(s);
            foreach (var node in new[] { RL(0, 5, 2), F(5, 8), F(5, 6) })
            {
                AssertCancelled(Drop_(s, 0, node), CancelReasons.Straddle, Key(node));
                CollectionAssert.AreEqual(before, s.Buf, Key(node));
            }
        }

        [Test(Description = "E-30 after the last move the counter is 0 and no block can be picked")]
        public void E30_after_the_last_move_the_counter_is_0_and_no_block_can_be_picked()
        {
            var s = Init(new LevelSpec { Moves = 1, Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 3, 0)) });
            var r = Drop_(s, 0, F(1, 0));
            Assert.AreEqual(MoveStatuses.Applied, r.Status);
            Assert.IsTrue(r.OutOfMoves);
            Assert.AreEqual(0, Hdr(s, H.MovesLeft));
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.NoMoves);
            AssertPickFails(Movement.TryBeginDrag(s, 1), PickFailures.NoMoves);
        }

        // --- K-10 / K-11 --------------------------------------------------------------------------------------------------

        [Test(Description = "K-10 GDD example: O4 (2,6) → (4,6) is one move and, with yard gravity off, stays hanging over the empty (4,4)–(5,5)")]
        public void K10_GDD_example_O4_is_one_move_and_with_yard_gravity_off_stays_hanging()
        {
            var s = Init(new LevelSpec
            {
                Moves = 20,
                Gravity = new GravitySpec { Yard = false },
                Pieces = FullYard(Ps(Pc("O4_0", "Y", 2, 6)), (4, 6), (5, 6), (4, 7), (5, 7), (4, 5), (5, 5), (4, 4), (5, 4)),
            });
            var d = Drag(s, 0);
            AssertDrop(d.Classify(F(4, 6)), DropKinds.Yard, null, 2);
            AssertDrop(d.Classify(F(4, 4)), DropKinds.Yard, null, 2);
            Assert.IsFalse(d.IsReachable(F(4, 3)));
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, F(4, 6)).Status);
            CollectionAssert.AreEqual(new[] { 4, 6 }, new[] { PieceX(s, 0), PieceY(s, 0) });
            Assert.AreEqual(19, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "K-11 K-22 after the S1 shift the drag sees the new, empty segment: the next block goes down to (6,0) again")]
        public void K11_K22_after_the_S1_shift_the_drag_sees_the_new_empty_segment()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "Y", 2, 0)),
            });
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, F(6, 2)).Status);
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            Assert.AreEqual(1, Hdr(s, H.ActiveSeg));
            var d = Drag(s, 1);
            Assert.IsTrue(d.IsReachable(F(6, 0)));
            AssertDrop(d.Classify(F(6, 0)), DropKinds.SiteFree, null, 6);
            Assert.IsFalse(Movement.TryBeginDrag(s, 0).Ok); // the completed segment is in the panorama now (K-06, K-14)
        }

        // --- K-12 / K-13 through the pipeline -------------------------------------------------------------------------------

        [Test(Description = "K-12 GDD debris example through the move pipeline: pulled left out of the rail to (4,3)–(5,3) costs 1 move; released back at its start in either mode it is a free cancel")]
        public void K12_GDD_debris_example_through_the_move_pipeline()
        {
            var s = Init(new LevelSpec
            {
                Moves = 9,
                Wall = Wall(5, Gap(3, 1)),
                Plan = new[] { "WW", "WW", "WW", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("D2_90", "R", 6, 3) },
            });
            var before = Snapshot(s);
            foreach (var start in new[] { RL(0, 6, 3), F(6, 3) })
            {
                AssertCancelled(Drop_(s, 1, start), CancelReasons.SameSpot, Key(start));
                CollectionAssert.AreEqual(before, s.Buf, Key(start));
            }
            Assert.AreEqual(2, Drag(s, 1).DistanceFromStart(F(4, 3))); // R(6,3) → R(5,3) → F(4,3)
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 1, F(4, 3)).Status);
            CollectionAssert.AreEqual(new[] { Zone.Yard, 4, 3 }, Spot(s, 1));
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "K-12 GDD example through the move pipeline: D2_90 (4,3)–(5,3) enters the y=3 gap, is released at (6,3)–(7,3) and stays in row 3 over empty cells")]
        public void K12_GDD_example_through_the_move_pipeline_D2_90_enters_the_gap_and_stays_in_row_3()
        {
            var s = Init(new LevelSpec
            {
                Moves = 6,
                Wall = Wall(5, Gap(3, 1)),
                Plan = new[] { "WW", "..", "..", ".." },
                Pieces = Ps(Pc("D2_90", "W", 4, 3), Pc("D2_0", "W", 0, 3)),
            });
            var d = Drag(s, 0);
            CollectionAssert.AreEqual(new[] { "R0(5,3)", "R0(6,3)" }, Keys(d.PathTo(RL(0, 6, 3)) ?? new DragNode[0]));
            AssertFall(Gravity.ComputeFall(s, 0, RL(0, 6, 3)), FallResult.ModeRail, A(6, 3), null, 0);
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, RL(0, 6, 3)).Status);
            CollectionAssert.AreEqual(new[] { Zone.Site, 6, 3 }, Spot(s, 0));
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked));
            Assert.AreEqual(5, Hdr(s, H.MovesLeft));
            // "Aynı geçide D2_0 (1×2, satır 3–4) giremez"
            Assert.IsFalse(Drag(s, 1).CanEnterRail);
        }

        [Test(Description = "K-13 GDD example built with real moves: B1 placed by rail at (7,3) over empty `.` cells locks; a FREE B1 at (6,1) cannot step right to (7,1)")]
        public void K13_GDD_example_built_with_real_moves()
        {
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Wall = Wall(5, Gap(3, 1)),
                Plan = new[] { "WW", "W.", "W.", "W." },
                Pieces = Ps(Pc("B1_0", "W", 5, 3), Pc("B1_0", "W", 0, 0)),
            });
            var placed = Drop_(s, 0, RL(0, 7, 3));
            Assert.AreEqual(MoveStatuses.Applied, placed.Status);
            Assert.AreEqual(Zone.Site, PieceZone(s, 0));
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Locked)); // (7,0)–(7,2) are `.`: K-34 counts them as filled
            AssertPickFails(Movement.TryBeginDrag(s, 0), PickFailures.Locked); // K-14: locked blocks never move

            // silhouette: top(6) = 0, top(7) = 4 (siteColumnTops holds top − 1, the dragged piece excluded)
            CollectionAssert.AreEqual(new sbyte[] { -1, 3 }, Grid.SiteColumnTops(s, 1));
            CollectionAssert.AreEqual(new sbyte[] { -1, -1 }, Grid.SiteColumnTops(s, 0));
            var d = Drag(s, 1);
            Assert.IsTrue(d.IsReachable(F(6, 1)));
            CollectionAssert.DoesNotContain(Keys(d.Neighbours(F(6, 1))), "F(7,1)"); // top(7) = 4 > 1
            for (int y = 0; y <= 3; y++) Assert.IsFalse(d.IsReachable(F(7, y)), "F(7," + y + ")");
            Assert.IsTrue(d.IsReachable(F(7, 4)));
            // the other entry of K-13: the rail (here only up to (6,3); (7,3) is taken)
            Assert.IsTrue(d.IsReachable(RL(0, 6, 3)));
            Assert.IsFalse(d.IsReachable(RL(0, 7, 3)));
        }

        // --- round 2 ----------------------------------------------------------------------------------------------------------

        [Test(Description = "K-09 (a) a block whose only translation is half-way into a gap (the site cell beyond is taken) can be picked, and every release of it is a free cancel")]
        public void K09_a_a_block_whose_only_translation_is_half_way_into_a_gap_can_be_picked()
        {
            var s = Init(new LevelSpec
            {
                Moves = 8,
                Wall = Wall(5, Gap(3)),
                Plan = new[] { "WW", "WW", "WW", "WW" },
                Pieces = FullYard(Ps(Pc("D2_90", "Y", 4, 3))),
                Debris = new[] { new D("B1_0", "R", 7, 3) },
            });
            var d = Drag(s, 0); // K-09 (a): the step right to R0(5,3) is a valid K-08 position
            CollectionAssert.AreEqual(new[] { "F(4,3)", "R0(5,3)" }, Keys(d.ReachableNodes()));
            AssertCancel(d.Classify(RL(0, 5, 3)), CancelReasons.Straddle, 4);
            var before = Snapshot(s);
            AssertCancelled(Drop_(s, 0, RL(0, 5, 3)), CancelReasons.Straddle);
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "K-10 K-05 over a full yard a block travels through the crane area to the only empty cell: path up, along row 8, down; release there is a yard move")]
        public void K10_K05_over_a_full_yard_a_block_travels_through_the_crane_area()
        {
            var s = Init(new LevelSpec { Moves = 15, Wall = Wall(6), Pieces = FullYard(Ps(Pc("B1_0", "Y", 0, 7)), (5, 7)) });
            var d = Drag(s, 0);
            Assert.AreEqual(7, d.DistanceFromStart(F(5, 7)));
            var go = d.MoveTo(F(5, 7));
            CollectionAssert.AreEqual(new[] { "F(0,8)", "F(1,8)", "F(2,8)", "F(3,8)", "F(4,8)", "F(5,8)", "F(5,7)" }, Keys(go.Path));
            foreach (var n in go.Path.Take(go.Path.Length - 1)) AssertCancel(d.Classify(n), CancelReasons.CraneOverYard, 3, Key(n));
            AssertDrop(d.Classify(), DropKinds.Yard, null, 2);
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, F(5, 7)).Status);
            CollectionAssert.AreEqual(new[] { Zone.Yard, 5, 7 }, Spot(s, 0));
            Assert.AreEqual(14, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "K-05 E-28 W2 (height 8): O4 crosses only in the crane rows — (4,8) cancels over the yard, the straddle (5,8) cancels, (6,8) falls 8 rows to (6,0)")]
        public void K05_E28_W2_height_8_O4_crosses_only_in_the_crane_rows()
        {
            var s = Init(new LevelSpec { Moves = 5, Wall = Wall(8), Plan = new[] { "WW", "WW" }, Pieces = Ps(Pc("O4_0", "W", 0, 0)) });
            var d = Drag(s, 0);
            Assert.IsFalse(d.IsReachable(F(5, 7))); // straddle with row 7 closed
            Assert.IsTrue(d.IsReachable(F(4, 7)));
            AssertCancel(d.Classify(F(4, 8)), CancelReasons.CraneOverYard, 3);
            AssertCancel(d.Classify(F(5, 8)), CancelReasons.Straddle, 4);
            AssertDrop(d.Classify(F(6, 8)), DropKinds.SiteFree, null, 6);
            AssertFall(Gravity.ComputeFall(s, 0, F(6, 8)), FallResult.ModeFree, A(6, 0), null, 8);
            var before = Snapshot(s);
            foreach (var (node, reason) in new[] { (F(4, 8), CancelReasons.CraneOverYard), (F(5, 8), CancelReasons.Straddle) })
            {
                AssertCancelled(Drop_(s, 0, node), reason, Key(node));
                CollectionAssert.AreEqual(before, s.Buf, Key(node));
            }
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, F(6, 8)).Status);
            CollectionAssert.AreEqual(new[] { Zone.Site, 6, 0 }, Spot(s, 0));
            Assert.AreEqual(4, Hdr(s, H.MovesLeft));
        }

        [Test(Description = "K-07 row 1 is decided by the release node, not by the landing: debris lifted one row and released is row 6 (a move); K-17 returns it to its start")]
        public void K07_row_1_is_decided_by_the_release_node_not_by_the_landing()
        {
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Wall = Wall(2),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Debris = new[] { new D("B1_0", "R", 6, 0) },
            });
            var d = Drag(s, 1);
            AssertCancel(d.Classify(F(6, 0)), CancelReasons.SameSpot, 1);
            AssertDrop(d.Classify(F(6, 1)), DropKinds.SiteFree, null, 6);
            Assert.AreEqual(A(6, 0), Gravity.ComputeFall(s, 1, F(6, 1)).Landing);
            var r = Drop_(s, 1, F(6, 1));
            Assert.AreEqual(MoveStatuses.Applied, r.Status);
            Assert.AreEqual(9, Hdr(s, H.MovesLeft)); // K-17: "hamle yanar (maliyet 1)"
            // K-16 (2): debris is never correct → K-17 step 1: back to its (empty) start cells, still debris, not locked
            CollectionAssert.AreEqual(new[] { Zone.Site, 6, 0 }, Spot(s, 1));
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Debris));
            Assert.IsFalse(HasFlag(s, 1, FlagBit.Locked));
        }

        [Test(Description = "K-22 K-09 K-11 debris of a later segment is no obstacle and cannot be picked until the site shifts to its segment; then it blocks and is pickable")]
        public void K22_K09_K11_debris_of_a_later_segment_is_no_obstacle_until_the_site_shifts()
        {
            var s = Init(new LevelSpec
            {
                Moves = 10,
                Wall = Wall(2),
                Plans = new[] { new[] { "WW" }, new[] { "YY", "YY" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "Y", 3, 0)),
                Debris = new[] { new D("B1_0", "R", 7, 1, 1) },
            });
            const int debris = 2; // PieceId order: batch 0, truck batches, then debris
            AssertPickFails(Movement.TryBeginDrag(s, debris), PickFailures.HiddenSegment);
            Assert.AreEqual(0, Grid.CollisionMasks(s, 1)[1] & (1 << 7));
            CollectionAssert.AreEqual(new sbyte[] { -1, -1 }, Grid.SiteColumnTops(s, 1));
            Assert.IsTrue(Drag(s, 1).IsReachable(F(7, 0)));

            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, F(6, 2)).Status);
            Assert.AreEqual(1, Hdr(s, H.ActiveSeg));

            Assert.IsTrue(Movement.TryBeginDrag(s, debris).Ok);
            Assert.AreEqual(F(7, 1), Drag(s, debris).Start);
            Assert.AreNotEqual(0, Grid.CollisionMasks(s, 1)[1] & (1 << 7));
            CollectionAssert.AreEqual(new sbyte[] { -1, 1 }, Grid.SiteColumnTops(s, 1));
            var d = Drag(s, 1);
            Assert.IsFalse(d.IsReachable(F(7, 0))); // under the debris: no open sky
            Assert.IsTrue(d.IsReachable(F(7, 2)));
            Assert.IsTrue(d.IsReachable(F(6, 0))); // the new segment's column 6 is empty
        }

        // --- round 3 ----------------------------------------------------------------------------------------------------------

        [Test(Description = "K-09 (a) K-12 K-13 debris in an open gap row whose only translation is right along its rail (under an overhang) is pickable; that release is a site move (row 7) and S4 returns it to its start for 1 move")]
        public void K09_a_K12_K13_debris_in_an_open_gap_row_whose_only_translation_is_right_along_its_rail_is_pickable()
        {
            var s = Init(new LevelSpec
            {
                Moves = 9,
                Wall = Wall(5, Gap(3)),
                Plan = new[] { "WW", "WW", "WW", "WW", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 5, 3)), // closes the way out to the yard
                Debris = new[] { new D("B1_0", "R", 6, 3), new D("B1_0", "R", 6, 4), new D("B1_0", "R", 7, 4) }, // overhang over (7,3)
            });
            var d = Drag(s, 1); // "4 birim ötelemesinden en az biri": right, in rail mode
            CollectionAssert.AreEqual(new[] { "F(6,3)", "R0(6,3)", "R0(7,3)" }, Keys(d.ReachableNodes()));
            Assert.IsFalse(d.IsReachable(F(7, 3))); // K-13: FREE cannot slide under (7,4)
            AssertDrop(d.Classify(RL(0, 7, 3)), DropKinds.SiteRail, null, 7, 0);
            var r = Drop_(s, 1, RL(0, 7, 3));
            Assert.AreEqual(MoveStatuses.Applied, r.Status);
            Assert.AreEqual(8, Hdr(s, H.MovesLeft));
            // OBSTACLES S4: "Şantiyede başka yere bırakılırsa hatalı → başlangıcına döner"
            CollectionAssert.AreEqual(new[] { Zone.Site, 6, 3 }, Spot(s, 1));
            Assert.IsTrue(HasFlag(s, 1, FlagBit.Debris));
        }

        [Test(Description = "K-12 N14 debris in a gap row blocks the rail for every other block (the site cell beyond is reachable only over the wall); once pulled out, the rail is open")]
        public void K12_N14_debris_in_a_gap_row_blocks_the_rail_for_every_other_block()
        {
            var s = Init(new LevelSpec
            {
                Moves = 9,
                Wall = Wall(5, Gap(3)),
                Plan = new[] { "WW", "WW", "WW", "WW" },
                Pieces = Ps(Pc("B1_0", "W", 4, 3)),
                Debris = new[] { new D("B1_0", "R", 6, 3) },
            });
            var blocked = Drag(s, 0);
            Assert.IsFalse(blocked.CanEnterRail);
            CollectionAssert.IsEmpty(blocked.ReachableNodes().Where(n => n.Mode != Movement.Free).ToArray());
            Assert.IsFalse(blocked.IsReachable(RL(0, 7, 3)));
            Assert.IsTrue(blocked.IsReachable(F(7, 3)));
            Assert.Greater(blocked.DistanceFromStart(F(7, 3)), 4); // up to row 5, over, down
            Assert.IsTrue(blocked.IsReachable(F(6, 4))); // on top of the debris (open sky)
            // the debris itself leaves through the gap and is parked above the gap row
            var debris = Drag(s, 1);
            Assert.AreEqual(2, debris.DistanceFromStart(F(5, 4)));
            var path = Keys(debris.PathTo(F(5, 4)) ?? new DragNode[0]);
            CollectionAssert.AreEqual(new[] { "F(5,3)", "F(5,4)" }, path.Skip(path.Length - 2).ToArray());
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 1, F(5, 4)).Status);
            CollectionAssert.AreEqual(new[] { Zone.Yard, 5, 4 }, Spot(s, 1));
            var open = Drag(s, 0);
            Assert.IsTrue(open.CanEnterRail);
            CollectionAssert.AreEqual(new[] { "F(5,3)", "R0(6,3)", "R0(7,3)" }, Keys(open.PathTo(RL(0, 7, 3)) ?? new DragNode[0]));
        }

        [Test(Description = "K-11 K-12 GDD §14 Bölüm 1, 4 and 9 drag gloves: the hand path on the board where the step starts emits the step signal (overWall / gapPass) and ends where the step points")]
        public void K11_K12_GDD_14_Bolum_1_4_and_9_drag_gloves()
        {
            // LEVELS §2: Bölüm 1 step 1 on the start board; Bölüm 4 step 2 after canonical moves 1–2 (startOn: the plug
            // shifted); Bölüm 9 step 2 after canonical move 1 (step 1 done). Anchor paths of the gloves (cell − grabbed offset).
            var cases = new[]
            {
                (level: 1, before: 0, piece: 0, path: new[] { (0, 4), (4, 4) }, signal: "overWall", end: F(4, 4)),
                (level: 4, before: 2, piece: 0, path: new[] { (4, 0) }, signal: "gapPass", end: RL(0, 4, 0)),
                (level: 9, before: 1, piece: 0, path: new[] { (4, 0) }, signal: "gapPass", end: RL(0, 4, 0)),
            };
            foreach (var c in cases)
            {
                string label = "Bölüm " + c.level;
                var s = LevelState(c.level);
                var log = TestPaths.LoadGolden("level_" + c.level.ToString("000") + ".hand.json")["log"]!;
                for (int i = 1; i < 1 + c.before; i++)
                    Assert.AreEqual(MoveStatuses.Applied, Moves.ApplyMove(s, (Move)SessionActionJson.FromJson(log[i]), null, new ApplyOptions(noTruckHelp: true)).Status, label);
                var d = Drag(s, c.piece);
                // the event is reachable from the pick
                if (c.signal == "overWall") Assert.IsTrue(d.CanCrossWall, label);
                else Assert.IsTrue(d.CanEnterRail, label);
                bool crossed = false;
                bool entered = false;
                foreach (var (x, y) in c.path)
                {
                    var r = d.Follow(x, y);
                    crossed |= r.CrossedWall;
                    entered |= r.EnteredRail;
                }
                Assert.AreEqual(c.end, d.Current, label);
                Assert.IsTrue(c.signal == "overWall" ? crossed : entered, label);
                Assert.IsFalse(c.signal == "overWall" ? entered : crossed, label);
            }
        }

        [Test(Description = "K-11 S2 a plan `.` is no obstacle for the drag: over a correct (7,0) a B1 is lowered onto the `.` at (7,1); the release there is a site move (row 6) landing on the `.`")]
        public void K11_S2_a_plan_dot_is_no_obstacle_for_the_drag()
        {
            var s = Init(new LevelSpec { Wall = Wall(2), Plan = new[] { "WW", "W.", "WW" }, Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "W", 2, 0)) });
            Assert.AreEqual(MoveStatuses.Applied, Drop_(s, 0, F(7, 8)).Status);
            CollectionAssert.AreEqual(new object[] { Zone.Site, 7, 0, true }, new object[] { PieceZone(s, 0), PieceX(s, 0), PieceY(s, 0), HasFlag(s, 0, FlagBit.Locked) });
            var d = Drag(s, 1);
            Assert.IsFalse(d.IsReachable(F(7, 0)));
            Assert.IsTrue(d.IsReachable(F(7, 1)));
            AssertDrop(d.Classify(F(7, 1)), DropKinds.SiteFree, null, 6);
            AssertDrop(d.Classify(F(7, 8)), DropKinds.SiteFree, null, 6);
            AssertFall(Gravity.ComputeFall(s, 1, F(7, 8)), null, A(7, 1), null, 7);
        }
    }
}
