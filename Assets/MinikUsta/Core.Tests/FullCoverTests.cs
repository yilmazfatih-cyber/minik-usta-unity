#nullable enable
// Port of tests/core/fullcover.test.ts (web repo): K-47 full cover (compile totals, Ağır Yük as cargo), K-48 win by full
// use, K-41 S4 debris as a coloured material block, and the K-47 conservation property under seeded random play
// (drags, hammer, crane, paint brush, Golden Trowel, Undo, +5 offers). `K47_LEVELS` / `K47_SEEDS` environment variables
// scale the property test like the TS file (defaults 20 × 500).
using System;
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
    public class FullCoverTests
    {
        private static int Sum(IEnumerable<int> a) => a.Sum();

        // --- K-47 full cover: compile totals -------------------------------------------------------------------------------------

        [Test(Description = "K-47 compile: supply and demand per colour, per segment and per batch (debris in its segment, Ağır Yük excluded), N")]
        public void K47_compile_supply_and_demand_per_colour_segment_and_batch()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Plans = new[] { new[] { "WY", "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("O4_0", "W", 0, 0), Pc("B1_0", "Y", 2, 0), Pc("Q9_0", "R", 3, 0)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "Y", 0, 8)) } },
                Debris = new[] { new D("B1_0", "W", 7, 0, 1) },
            });
            // plan: segment 0 W 3 + Y 1, segment 1 Y 2
            CollectionAssert.AreEqual(new[] { new[] { 3, 1 }, new[] { 0, 2 } }, lvl.DemandBySegment.Select(r => new[] { r[0], r[1] }).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 3 }, new[] { lvl.Demand[0], lvl.Demand[1] });
            // supply: O4 W 4, B1 Y, truck B1 Y, debris B1 W (segment 1 → batch row 1); Q9 is cargo
            CollectionAssert.AreEqual(new[] { 5, 2, 0 }, new[] { lvl.Supply[0], lvl.Supply[1], lvl.Supply[3] });
            CollectionAssert.AreEqual(new[] { new[] { 4, 1 }, new[] { 1, 1 } }, lvl.SupplyByBatch.Select(r => new[] { r[0], r[1] }).ToArray());
            Assert.AreEqual(4, lvl.MaterialCount);
            Assert.IsFalse(Deadlock.IsFullCover(lvl)); // W 5 ≠ 3, Y 2 ≠ 3
        }

        [Test(Description = "K-44 an Ağır Yük (I5, Q9) is cargo: colourless (−1 whatever the data colour), never supply, demand or blocks left")]
        public void K44_an_Agir_Yuk_is_cargo_colourless_never_supply_demand_or_blocks_left()
        {
            var spec = new LevelSpec
            {
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("I5_0", "Y", 0, 1), Pc("Q9_0", "G", 0, 2)),
            };
            var lvl = Builders.Compiled(spec);
            CollectionAssert.AreEqual(
                new[] { "material/0", "cargo/-1", "cargo/-1" },
                lvl.Pieces.Select(p => p.Cls + "/" + p.ColorIndex).ToArray());
            Assert.IsTrue(Deadlock.IsFullCover(lvl));
            var s = Init(spec);
            CollectionAssert.AreEqual(new[] { true, false, false }, new[] { Goals.IsMaterial(s, 0), Goals.IsMaterial(s, 1), Goals.IsMaterial(s, 2) });
            Assert.AreEqual(1, Goals.BlocksLeft(s));
            Assert.AreEqual(2, Sum(Goals.RemainingSupplyByColor(s)));
        }

        // --- K-48 win: full use --------------------------------------------------------------------------------------------------

        [Test(Description = "K-48 GDD test fixture (bozuk veri): the plan is complete but a B1 is left in the yard → not won")]
        public void K48_GDD_test_fixture_the_plan_is_complete_but_a_B1_is_left_in_the_yard()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 3, 0)),
            });
            var r = Run(s, DragTo(0, 6, 8));
            Assert.AreEqual(0, Find<SegmentCompletedEvent>(r.Ev).Seg);
            Assert.IsFalse(r.Res.Won);
            CollectionAssert.AreEqual(new[] { true, false }, new[] { Goals.MaterialLeft(s), Goals.LevelGoalsMet(s) });
        }

        [Test(Description = "E-49 the last segment completes with an Ağır Yük and two crates left in the yard: won (K-28, K-48)")]
        public void E49_the_last_segment_completes_with_an_Agir_Yuk_and_two_crates_left_won()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("Q9_0", "W", 2, 0)),
                Obstacles = new[] { Crate(5, 0, 1), Crate(5, 1, 2) },
            });
            var r = Run(s, DragTo(0, 6, 8));
            AssertResult(r.Res, MoveStatuses.Applied, null, true, false);
            Assert.AreEqual(11, Find<LevelWonEvent>(r.Ev).Step);
            Assert.AreEqual(Zone.Yard, PieceZone(s, 1));
        }

        [Test(Description = "K-48 blocks-left chip equals N minus correct: −1 per correct placement, unchanged by yard moves and wrong drops, 0 ⇔ every segment complete")]
        public void K48_blocks_left_chip_equals_N_minus_correct()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "YY", "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "Y", 2, 0), Pc("B1_0", "Y", 3, 0)),
            });
            Assert.AreEqual(3, Goals.BlocksLeft(s));
            Run(s, DragTo(1, 1, 3)); // yard move
            Assert.AreEqual(3, Goals.BlocksLeft(s));
            Run(s, DragTo(1, 6, 8)); // Y on W: wrong, bounces
            Assert.AreEqual(3, Goals.BlocksLeft(s));
            Run(s, DragTo(0, 6, 8));
            Assert.AreEqual(2, Goals.BlocksLeft(s));
            Run(s, DragTo(1, 6, 8));
            Run(s, DragTo(2, 7, 8));
            CollectionAssert.AreEqual(new object[] { 0, true }, new object[] { Goals.BlocksLeft(s), Goals.LevelGoalsMet(s) });
        }

        // --- K-41 S4 debris (Faz 2R: a coloured material block) -----------------------------------------------------------------------

        private static LevelSpec DEBRIS(P[]? pieces = null) => new LevelSpec
        {
            Wall = Wall(2),
            Plan = new[] { "WR", "RR" },
            Pieces = pieces ?? Ps(Pc("D2_90", "R", 0, 0), Pc("B1_0", "W", 2, 0)),
            Debris = new[] { new D("B1_0", "R", 6, 1) },
            Goals = new[] { J.O("type", "build"), J.O("type", "clear", "target", "debris", "count", 1) },
        };

        [Test(Description = "E-44 K-47 debris is supply; once it leaves its start spot it counts once (K-41), loses the debris flag and is placed like any block")]
        public void E44_K47_debris_is_supply_counts_once_and_is_placed_like_any_block()
        {
            var s = Init(DEBRIS());
            const int debris = 2;
            Assert.IsTrue(Deadlock.IsFullCover(s.Lvl));
            CollectionAssert.AreEqual(Goals.RemainingDemandByColor(s), Goals.RemainingSupplyByColor(s));
            var lift = Run(s, DragTo(debris, 3, 0)); // to the yard
            var goal = Find<GoalProgressEvent>(lift.Ev);
            CollectionAssert.AreEqual(new[] { 1, 1, 7 }, new[] { goal.Goal, goal.Value, goal.Step });
            CollectionAssert.AreEqual(new object[] { Zone.Yard, false }, new object[] { PieceZone(s, debris), HasFlag(s, debris, FlagBit.Debris) });
            Run(s, DragTo(0, 6, 8)); // R R on row 0
            var placed = Run(s, DragTo(debris, 7, 8)); // R on (7,1): correct
            CollectionAssert.Contains(Types(placed.Ev), "placementCorrect");
            Assert.AreEqual(1, GoalValue(s, 1)); // counted once
            Run(s, DragTo(1, 6, 8));
            Assert.IsTrue(Goals.LevelGoalsMet(s));
        }

        [Test(Description = "K-41 a debris placed correctly elsewhere on the site counts in that move and locks (no yard trip)")]
        public void K41_a_debris_placed_correctly_elsewhere_on_the_site_counts_in_that_move_and_locks()
        {
            var s = Init(DEBRIS(Ps(Pc("B1_0", "R", 0, 0), Pc("B1_0", "R", 1, 0), Pc("B1_0", "W", 2, 0))));
            const int debris = 3;
            Assert.IsTrue(Deadlock.IsFullCover(s.Lvl));
            Run(s, DragTo(0, 7, 8)); // R on (7,0)
            var r = Run(s, DragTo(debris, 7, 8)); // the debris itself, from (6,1) over to (7,1): correct
            Assert.AreEqual(debris, Find<PlacementCorrectEvent>(r.Ev).PieceId);
            Assert.AreEqual(1, GoalValue(s, 1));
            Assert.AreEqual(FlagBit.Locked, PieceFlagBits(s, debris) & (FlagBit.Debris | FlagBit.Locked));
            Run(s, DragTo(1, 6, 8));
            Assert.IsTrue(Run(s, DragTo(2, 6, 8)).Res.Won);
            ExpectConsistent(s);
        }

        // --- K-47 conservation: seeded random play -------------------------------------------------------------------------------

        private sealed class ActionTaken
        {
            public readonly string Kind;
            public readonly bool Applied;
            public readonly bool Teardown;

            public ActionTaken(string kind, bool applied, bool teardown)
            {
                Kind = kind;
                Applied = applied;
                Teardown = teardown;
            }
        }

        /// <summary>One random action of the session (TS `randomAction`); `none` when nothing could be done.</summary>
        private static ActionTaken RandomAction(GameSession game, IRng rng)
        {
            var s = game.State;
            var hooks = game.Hooks;
            double roll = rng.Next();
            if (roll < 0.06 && game.CanUndo()) return new ActionTaken("undo", game.Undo(), false);
            Move? move = null;
            if (roll < 0.14)
            {
                var targets = Boosters.HammerTargets(s, hooks);
                int ti = rng.NextInt(Math.Max(1, targets.Length));
                if (ti < targets.Length) move = new HammerMove(targets[ti]);
            }
            else if (roll < 0.22)
            {
                int P = s.Lvl.Layout.Counts.Pieces;
                var ids = Enumerable.Range(0, P).Where(id => Boosters.CraneSelectable(s, id, hooks)).ToArray();
                int ii = rng.NextInt(Math.Max(1, ids.Length));
                if (ii < ids.Length)
                {
                    int id = ids[ii];
                    var spots = Boosters.CraneSiteSpots(s, id);
                    int si = rng.NextInt(Math.Max(1, spots.Length));
                    if (si < spots.Length && rng.Next() < 0.6)
                    {
                        var spot = spots[si];
                        move = new CraneMove(id, At.ZoneSite, spot.At.Ix, spot.At.Iy, spot.Rotation);
                    }
                    else
                    {
                        var shape = Shapes.ShapeByIndex(PieceShape(s, id));
                        move = new CraneMove(id, At.ZoneYard, rng.NextInt(s.Lvl.Geo.Wy - shape.W + 1), rng.NextInt(s.Lvl.Geo.Hy - shape.H + 1), shape.Rotation);
                    }
                }
            }
            else if (roll < 0.28)
            {
                int P = s.Lvl.Layout.Counts.Pieces;
                int a = rng.NextInt(P);
                var bs = Boosters.PaintPartners(s, a);
                int bi = rng.NextInt(Math.Max(1, bs.Length));
                if (bi < bs.Length) move = new PaintMove(a, bs[bi]);
            }
            else if (roll < 0.36)
            {
                var ids = Boosters.TrowelPieces(s, hooks);
                int ii = rng.NextInt(Math.Max(1, ids.Length));
                if (ii < ids.Length)
                {
                    int id = ids[ii];
                    var spots = Boosters.TrowelSpots(s, id);
                    int si = rng.NextInt(Math.Max(1, spots.Length));
                    if (si < spots.Length) move = new GoldTrowelMove(id, spots[si].Ix, spots[si].Iy);
                }
            }
            if (move == null)
            {
                int P = s.Lvl.Layout.Counts.Pieces;
                int start = rng.NextInt(P);
                for (int k = 0; k < P && move == null; k++)
                {
                    int id = (start + k) % P;
                    int z = PieceZone(s, id);
                    if (z != Zone.Yard && z != Zone.Site) continue;
                    var a = Movement.TryBeginDrag(s, id, hooks.Drag);
                    if (!a.Ok) continue;
                    var nodes = a.Session!.ReachableNodes();
                    // favour site releases so the plan fills
                    var site = nodes.Where(n => n.Ix >= s.Lvl.Geo.SiteX).ToArray();
                    var pool = site.Length > 0 && rng.Next() < 0.6 ? site : nodes;
                    if (pool.Length > 0) move = new DragMove(id, pool[rng.NextInt(pool.Length)]);
                }
            }
            if (move == null) return new ActionTaken("none", false, false);
            var res = game.Commit(move, new ArraySink());
            return new ActionTaken(move.Kind, res.Status == MoveStatuses.Applied, res.TeardownCause != null);
        }

        private static void ExpectConserved(GameState s, string where)
        {
            var sup = Goals.RemainingSupplyByColor(s);
            var dem = Goals.RemainingDemandByColor(s);
            Assert.AreEqual(Sum(dem), Sum(sup), where + ": Σ supply = Σ demand");
            CollectionAssert.AreEqual(dem, sup, where + ": per colour");
        }

        private static int EnvInt(string name, int fallback)
        {
            string? v = Environment.GetEnvironmentVariable(name);
            return v != null && int.TryParse(v, out int n) ? n : fallback;
        }

        private static readonly int LEVELS = EnvInt("K47_LEVELS", 20);
        private static readonly int SEEDS = EnvInt("K47_SEEDS", 500);

        [Test(Description = "K-47 the generated levels are full covers with a tiling at the start")]
        public void K47_the_generated_levels_are_full_covers_with_a_tiling_at_the_start()
        {
            for (int n = 1; n <= LEVELS; n++)
            {
                var s = Init(Cover.CoverLevel(n));
                Assert.IsTrue(Deadlock.IsFullCover(s.Lvl), "level " + n);
                Assert.AreEqual(TileResults.Ok, Deadlock.TileRemaining(s), "level " + n);
                CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s), "level " + n);
            }
        }

        [Test(Description = "K-47 conservation: LEVELS levels × SEEDS seeded action sequences (drags, hammer, crane, paint brush, Golden Trowel, Undo, +5): remaining supply = remaining demand per colour at every step; K-47 boosters never create a D3a dead end")]
        public void K47_conservation_under_seeded_random_play()
        {
            int actions = 0;
            int boosters = 0;
            int wins = 0;
            for (int n = 1; n <= LEVELS; n++)
            {
                var lvl = Builders.Compiled(Cover.CoverLevel(n));
                for (int seed = 1; seed <= SEEDS; seed++)
                {
                    var rng = new Mulberry32(n * 1000 + seed);
                    var game = GameSession.Start(lvl, new[] { PreBoosters.TrowelStart, PreBoosters.Thermos });
                    for (int step = 0; step < 24 && game.Outcome != BoosterOutcomes.Won && game.Outcome != BoosterOutcomes.Lost; step++)
                    {
                        if (game.Outcome == BoosterOutcomes.OutOfMoves)
                        {
                            if (game.AcceptOffer(AddMovesSource.OfferCoins).Status != MoveStatuses.Applied) break;
                            continue;
                        }
                        var a = RandomAction(game, rng);
                        if (a.Kind == "none") break;
                        actions++;
                        string where = "level " + n + " seed " + seed + " step " + step + " (" + a.Kind + ")";
                        ExpectConserved(game.State, where);
                        if (a.Applied && !a.Teardown && (a.Kind == "crane" || a.Kind == "paint" || a.Kind == "goldTrowel"))
                        {
                            boosters++;
                            Assert.AreNotEqual(TileResults.Dead, Deadlock.TileRemaining(game.State), where + ": K-47 booster dead end");
                        }
                    }
                    if (game.Outcome == BoosterOutcomes.Won) wins++;
                    CollectionAssert.IsEmpty(Grid.StateInvariantErrors(game.State), "level " + n + " seed " + seed);
                    Assert.IsTrue(Goals.BlocksLeft(game.State) >= 0);
                }
            }
            Assert.Greater(actions, LEVELS * SEEDS * 4);
            Assert.Greater(boosters, 0);
            Assert.GreaterOrEqual(wins, 0);
        }
    }
}
