#nullable enable
// D-106 (web docs/DECISIONS.md, GDD K-58 item 10, RulesVersion 6): the C# twin of tests/core/palletD106.test.ts (web 98a01c0). A held
// pallet layer does not collide with the remaining layers of its own pallet (it rises out of the stack whatever its shape), while every
// other block, the wall and the frame still collide and the pallet stays solid for every other block. pallet_stuck is retired. Every
// test name carries "K-58 item 10"; DiffFuzzTests prove bit equality with the TypeScript core on the same boards.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class PalletD106Tests
    {
        private static LevelData Parse(LevelSpec spec)
        {
            spec.SchemaVersion = 3;
            var res = Logic.ValidateLevelJson(Builders.LevelJson(spec), new LogicContext(only: new string[0]));
            Assert.IsNotNull(res.Level, string.Join("\n", res.Issues.Select(i => i.ToString())));
            return res.Level!;
        }

        private static GameState V3(LevelSpec spec) => CreateInitialState(Compiler.Compile(Parse(spec)));

        private static string[] Codes(LevelSpec spec)
        {
            spec.SchemaVersion = 3;
            return Logic.ValidateLevelJson(Builders.LevelJson(spec), new LogicContext()).Issues.Select(i => i.Code).ToArray();
        }

        private static MinikUsta.Core.Json.JsonValue StackOf(int id, int z) => J.O("stack", J.O("id", id, "z", z));

        /// <summary>`layers` identical W layers of `shape` anchored at (0, 0) = piece ids 0 … layers − 1 (top = layers − 1).</summary>
        private static P[] PalletPieces(string shape, int layers) => Enumerable.Range(0, layers).Select(_ => Pc(shape, "W", 0, 0)).ToArray();

        private static Dictionary<int, MinikUsta.Core.Json.JsonValue> PalletTraits(int layers) =>
            Enumerable.Range(0, layers).ToDictionary(z => z, z => StackOf(0, z));

        private static readonly string[] FullPlan = { "WWW", "WWW", "WWW", "WWW", "WWW" };

        /// <summary>
        /// The top layer of a ×3 pallet of `shape` at (0, 0) of a 6 × 4 yard, wall height 2, 3 × 5 site; `plan` makes the shape at site
        /// anchor (6, 0) a correct placement (`.` under a T overhang). No full cover (15 or 14 cells against 9 or 12): D2 / D3a stay off.
        /// </summary>
        private static LevelSpec PalletLevel(string shape, string[] plan, params P[] extra) => new LevelSpec
        {
            Id = 8, Yard = new SizeSpec(6, 4), Site = new SizeSpec(3, 5), Wall = Wall(2), Plan = plan,
            Pieces = PalletPieces(shape, 3).Concat(extra).ToArray(), Traits = PalletTraits(3),
        };

        private static IEnumerable<TestCaseData> Cases()
        {
            yield return new TestCaseData("C3_0", FullPlan);
            yield return new TestCaseData("L4_0", FullPlan);
            yield return new TestCaseData("T4_0", new[] { "WWW", "WWW", "WWW", "WWW", "W.W" });
            yield return new TestCaseData("J4_0", FullPlan);
            yield return new TestCaseData("O4_0", FullPlan);
        }

        [Test(Description = "K-58 item 10 RULES_VERSION 6 (D-106 changes the drag R of a pallet layer and the replay of saved logs)")]
        public void K58_item_10_rules_version_6() => Assert.AreEqual(6, GameSession.RulesVersion);

        [TestCaseSource(nameof(Cases))]
        [Test(Description = "K-58 item 10 pallet x3 of C3/L4/T4/J4/O4: the top layer is held, rises through its own footprint and is placed on the site")]
        public void K58_item_10_the_top_layer_rises_through_its_own_footprint_and_is_placed(string shape, string[] plan)
        {
            var s = V3(PalletLevel(shape, plan));
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
            CollectionAssert.AreEqual(new[] { true, true, false }, new[] { Grid.IsCovered(s, 0), Grid.IsCovered(s, 1), Grid.IsCovered(s, 2) });
            // the held top layer's collision rows do not contain its own pallet's cells (the layers below)
            Assert.AreEqual(0, Grid.CollisionMasks(s, 2)[0], "row 0");
            var attempt = Movement.TryBeginDrag(s, 2);
            Assert.AreEqual("ok", attempt.Ok ? "ok" : attempt.Reason);
            var d = Movement.BeginDrag(s, 2)!;
            Assert.IsNotNull(d);
            // one row up: the new cells overlap the pallet's own footprint (D-106: no collision with its own layers)
            Assert.IsTrue(d.IsReachable(new DragNode(0, 1, 0)));
            // the start is still the K-07 row 1 cancel and the yard releases still cancel (K-58 item 9 c)
            var start = d.Classify(new DragNode(0, 0, 0));
            Assert.AreEqual((DropKinds.Cancel, CancelReasons.SameSpot), (start.Kind, start.Reason));
            var up = d.Classify(new DragNode(0, 1, 0));
            Assert.AreEqual((DropKinds.Cancel, CancelReasons.PalletSiteOnly), (up.Kind, up.Reason));
            Assert.IsTrue(d.ReachableNodes().Any(n => n.Ix >= 6 && d.Classify(n).Kind == DropKinds.SiteFree));
            var r = Run(s, DragTo(2, 6, 3));
            CollectionAssert.DoesNotContain(Types(r.Ev), "teardown");
            var ok = Find<PlacementCorrectEvent>(r.Ev);
            Assert.AreEqual(2, ok.PieceId);
            Assert.IsTrue(ok.OverWall);
            Assert.AreEqual(Zone.Site, PieceZone(s, 2));
            // the next layer is the top now and owns the pallet's cells; it can be held too
            Assert.AreEqual(2, YardOcc(s, 0, 0));
            Assert.IsFalse(Grid.IsCovered(s, 1));
            Assert.IsTrue(Grid.IsCovered(s, 0));
            Assert.IsTrue(Movement.TryBeginDrag(s, 1).Ok);
            CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s));
        }

        [Test(Description = "K-58 item 10 the crane still lifts the top layer of a C3_0 pallet to a correct site spot only (K-58 item 9 e)")]
        public void K58_item_10_the_crane_lifts_the_top_C3_layer_to_the_site_only()
        {
            var s = V3(PalletLevel("C3_0", FullPlan));
            var hooks = MinikUsta.Core.Obstacles.Registry.LevelHooks(s.Lvl);
            Assert.IsNull(Boosters.CraneRejection(s, new CraneMove(2, "site", 6, 0, 0), hooks));
            Assert.AreEqual(BoosterRejects.PalletSiteOnly, Boosters.CraneRejection(s, new CraneMove(2, "yard", 3, 0, 0), hooks));
            // a covered layer is no target (K-66 item 1)
            Assert.AreEqual(BoosterRejects.NoTarget, Boosters.CraneRejection(s, new CraneMove(1, "site", 6, 0, 0), hooks));
        }

        // C3_0 pallet x3 at (0, 0): cells (0,0) (1,0) (0,1). A B1 G at (1,1) caps the up and the right shift; the frame closes the rest.
        private static LevelSpec Capped() => PalletLevel("C3_0", FullPlan, Pc("B1_0", "G", 1, 1));

        [Test(Description = "K-58 item 10 a held layer still collides with another block: capped by a B1 it cannot move (immovable)")]
        public void K58_item_10_a_held_layer_still_collides_with_another_block()
        {
            var s = V3(Capped());
            var attempt = Movement.TryBeginDrag(s, 2);
            Assert.AreEqual(PickFailures.Immovable, attempt.Ok ? "ok" : attempt.Reason);
            var covered = Movement.TryBeginDrag(s, 1);
            Assert.AreEqual(PickFailures.Covered, covered.Ok ? "ok" : covered.Reason);
        }

        [Test(Description = "K-58 item 10 the pallet stays solid for every other block: the B1 cannot step onto the pallet cells")]
        public void K58_item_10_the_pallet_stays_solid_for_every_other_block()
        {
            var s = V3(Capped());
            var d = Movement.BeginDrag(s, 3);
            Assert.IsNotNull(d);
            Assert.IsFalse(d!.IsReachable(new DragNode(1, 0, 0)), "(1,0) is a pallet cell");
            Assert.IsFalse(d.IsReachable(new DragNode(0, 1, 0)), "(0,1) is a pallet cell");
            Assert.IsTrue(d.IsReachable(new DragNode(2, 1, 0)));
            // once the B1 parks elsewhere the layer rises (the cap was another block, never its own pallet)
            Assert.AreEqual(MoveStatuses.Applied, Run(s, DragTo(3, 4, 0)).Res.Status);
            Assert.IsTrue(Movement.BeginDrag(s, 2)!.IsReachable(new DragNode(0, 1, 0)));
        }

        [Test(Description = "K-58 item 10 the wall and the frame still collide: a layer under a wall higher than the board has only cancels")]
        public void K58_item_10_the_wall_and_the_frame_still_collide()
        {
            var spec = PalletLevel("C3_0", FullPlan);
            spec.Wall = Wall(6);
            var s = V3(spec);
            var d = Movement.BeginDrag(s, 2);
            Assert.IsNotNull(d);
            Assert.IsTrue(d!.ReachableNodes().All(n => n.Ix + 1 <= s.Lvl.Geo.BoundaryX));
            Assert.IsTrue(d.ReachableNodes().All(n => d.Classify(n).Kind == DropKinds.Cancel));
        }

        [Test(Description = "K-58 item 10 the D2_90 example of K-58 item 4: the top layer now slides sideways over its own cells when the side is free")]
        public void K58_item_10_a_D2_90_top_layer_slides_sideways_over_its_own_cells()
        {
            var s = V3(new LevelSpec
            {
                Id = 8, Yard = new SizeSpec(6, 4), Site = new SizeSpec(3, 4), Plan = new[] { "WWW", "WWW", "WWW", "WWW" },
                Pieces = PalletPieces("D2_90", 2), Traits = PalletTraits(2),
            });
            var d = Movement.BeginDrag(s, 1)!;
            Assert.IsTrue(d.IsReachable(new DragNode(1, 0, 0)));
            var c = d.Classify(new DragNode(1, 0, 0));
            Assert.AreEqual((DropKinds.Cancel, CancelReasons.PalletSiteOnly), (c.Kind, c.Reason));
        }

        /// <summary>One pallet layer left (the last layer of a pallet) and a wall of `height`.</summary>
        private static LevelSpec LastLayer(int height) => new LevelSpec
        {
            Id = 8, Yard = new SizeSpec(6, 4), Site = new SizeSpec(3, 4), Wall = Wall(height), Plan = new[] { "WWW", "WWW", "WWW", "WWW" },
            Pieces = Ps(Pc("C3_0", "W", 0, 0)), Traits = new Dictionary<int, MinikUsta.Core.Json.JsonValue> { { 0, StackOf(0, 0) } },
        };

        [Test(Description = "K-58 item 10 K-30 the last pallet layer behind an uncrossable wall is no move (the quick test defers to the BFS)")]
        public void K58_item_10_K30_the_last_layer_behind_an_uncrossable_wall_is_no_move()
        {
            var s = V3(LastLayer(6));
            var hooks = MinikUsta.Core.Obstacles.Registry.LevelHooks(s.Lvl);
            Assert.IsFalse(Deadlock.QuickHoldable(s, 0, hooks.Drag));
            Assert.IsNotNull(Movement.BeginDrag(s, 0, hooks.Drag), "it can be held …");
            Assert.IsTrue(Deadlock.NoMoves(s, hooks.Drag), "… but has no release other than a cancel");
        }

        [Test(Description = "K-58 item 10 K-30 the same layer with a crossable wall is a move (a site release exists)")]
        public void K58_item_10_K30_the_same_layer_with_a_crossable_wall_is_a_move()
        {
            var s = V3(LastLayer(2));
            Assert.IsFalse(Deadlock.NoMoves(s, MinikUsta.Core.Obstacles.Registry.LevelHooks(s.Lvl).Drag));
        }

        [TestCase("C3_0")]
        [TestCase("O4_0")]
        [TestCase("L4_0")]
        [TestCase("J4_0")]
        [TestCase("T4_0")]
        [TestCase("D2_90")]
        [Test(Description = "K-58 item 10 pallet_stuck is retired: a pallet x2 of any shape is not reported")]
        public void K58_item_10_pallet_stuck_is_retired(string shape)
        {
            var spec = new LevelSpec
            {
                Id = 8, Yard = new SizeSpec(6, 4), Site = new SizeSpec(3, 4), Plan = new[] { "GGG", "GGG", "WWW", "WWW" },
                Pieces = PalletPieces(shape, 2).Concat(Ps(Pc("D2_90", "G", 3, 0), Pc("D2_90", "G", 3, 2))).ToArray(),
                Traits = PalletTraits(2),
            };
            CollectionAssert.DoesNotContain(Codes(spec), "pallet_stuck");
        }
    }
}
