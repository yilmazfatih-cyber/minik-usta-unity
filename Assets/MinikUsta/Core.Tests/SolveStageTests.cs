#nullable enable
// Solve-stage requirements of the shipped levels (GDD K-51, K-52, K-46, E-54; TECH §2R.3 L-19, L-32, L-34) without the
// solver: tools/solver is a web tool (tests/tools/solver/checks.test.ts, level10.test.ts). What the Unity port can check
// on its own data: `min` and the canonical solution come from the solver goldens (Golden/level_NNN.solver.json, written
// by the web solver); the K-52 band is a port of tools/solver/metrics.ts `movesBand`; K-51 item 1 (`firstNeedDepth ≥ 1`
// = no correct placement is possible at the start) is checked by brute force over every release of every pickable block
// through the C# pipeline; K-51 item 4 / K-46 and item 5 / E-54 by playing the canonical solution through `GameSession`.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class SolveStageTests
    {
        // --- K-52 move budget: tools/solver/metrics.ts `movesBand` ---------------------------------------------------------

        public readonly struct MovesBand
        {
            /// <summary>The K-52 row used (an intro level takes the easier row, DL-2R-05).</summary>
            public readonly string Row;
            public readonly int T;
            public readonly int Floor;
            /// <summary>A = max(1, ⌊0.1 · min⌋).</summary>
            public readonly int A;
            /// <summary>min + T (a = 0).</summary>
            public readonly int Nominal;
            /// <summary>Lowest / highest valid `moves`: [max(min + T − A, min + floor), min + T + A].</summary>
            public readonly int Lo;
            public readonly int Hi;

            public MovesBand(string row, int t, int floor, int a, int nominal, int lo, int hi)
            {
                Row = row;
                T = t;
                Floor = floor;
                A = a;
                Nominal = nominal;
                Lo = lo;
                Hi = hi;
            }
        }

        private static readonly Dictionary<string, (int Pct, int MinT, int Floor)> Rows = new Dictionary<string, (int, int, int)>
        {
            { "easy", (50, 6, 4) }, { "normal", (35, 4, 3) }, { "hard", (20, 3, 2) }, { "superhard", (12, 2, 1) },
        };

        private static readonly Dictionary<string, string> Easier = new Dictionary<string, string>
        {
            { "easy", "easy" }, { "normal", "easy" }, { "hard", "normal" }, { "superhard", "hard" },
        };

        /// <summary>GDD K-52: `moves = min + T + a`, |a| ≤ max(1, ⌊0.1·min⌋), `moves − min ≥ taban`; intro levels one row easier.</summary>
        public static MovesBand Band(int min, string difficulty, bool intro)
        {
            string row = intro ? Easier[difficulty] : difficulty;
            var r = Rows[row];
            int t = Math.Max(r.MinT, (int)Math.Ceiling(r.Pct * min / 100.0));
            int a = Math.Max(1, min / 10);
            return new MovesBand(row, t, r.Floor, a, min + t, Math.Max(min + t - a, min + r.Floor), min + t + a);
        }

        private static JsonValue SolverGolden(int n) => TestPaths.LoadGolden("level_" + n.ToString("000") + ".solver.json");

        public static IEnumerable<int> LevelIds() => Enumerable.Range(1, 10);

        [Test(Description = "K-52 the GDD examples: level 3 min 5 easy → 11, level 10 min 13 hard → 16 (A 1, floor 2)")]
        public void K52_the_GDD_examples_level_3_min_5_easy_11_level_10_min_13_hard_16()
        {
            var b3 = Band(5, "easy", false);
            CollectionAssert.AreEqual(new[] { 6, 11, 10, 12 }, new[] { b3.T, b3.Nominal, b3.Lo, b3.Hi });
            var b10 = Band(13, "hard", false);
            CollectionAssert.AreEqual(new[] { 3, 1, 2, 16, 15, 17 }, new[] { b10.T, b10.A, b10.Floor, b10.Nominal, b10.Lo, b10.Hi });
            // floor wins over T − A: min 30 superhard → T max(2, ⌈3.6⌉) = 4, A 3: [min + 1, min + 7]
            var b30 = Band(30, "superhard", false);
            CollectionAssert.AreEqual(new[] { 4, 3, 31, 37 }, new[] { b30.T, b30.A, b30.Lo, b30.Hi });
        }

        [Test(Description = "K-52 intro level uses the easier row (DL-2R-05): level 5 normal + teaches S1, min 11 → T 6 → 17")]
        public void K52_an_intro_level_uses_the_easier_row_level_5_normal_teaches_S1_min_11_T_6_17()
        {
            var intro = Band(11, "normal", true);
            Assert.AreEqual("easy", intro.Row);
            CollectionAssert.AreEqual(new[] { 6, 4, 17 }, new[] { intro.T, intro.Floor, intro.Nominal });
            var plain = Band(11, "normal", false);
            Assert.AreEqual("normal", plain.Row);
            CollectionAssert.AreEqual(new[] { 4, 3, 15 }, new[] { plain.T, plain.Floor, plain.Nominal });
            Assert.AreEqual("normal", Band(13, "hard", true).Row);
            Assert.AreEqual("hard", Band(13, "superhard", true).Row);
            Assert.AreEqual("easy", Band(13, "easy", true).Row);
        }

        [Test(Description = "K-52 moves_budget: moves outside [min + T − A, min + T + A] or below min + floor is an error (level 3: band 10–12)")]
        public void K52_moves_budget_outside_the_band_is_an_error_level_3_band_10_to_12()
        {
            var lvl = Boards.LevelFile(3).Data;
            int min = SolverGolden(3)["min"]!.AsInt;
            Assert.AreEqual(5, min, "solver min of level 3");
            var band = Band(min, lvl.Difficulty, lvl.Teaches != null);
            foreach (var (moves, bad) in new[] { (9, true), (10, false), (11, false), (12, false), (13, true) })
                Assert.AreEqual(bad, moves < band.Lo || moves > band.Hi, "moves " + moves);
        }

        [Test(Description = "K-52 K-51 levels 1–10: `moves` lies in the K-52 band of the solver's min (intro levels one row easier)")]
        public void K52_K51_levels_1_to_10_spend_a_move_budget_inside_the_band_of_the_solver_min()
        {
            foreach (int n in LevelIds())
            {
                var data = Boards.LevelFile(n).Data;
                var golden = SolverGolden(n);
                int min = golden["min"]!.AsInt;
                Assert.AreEqual(min + 1, golden["log"]!.Items.Count, "level " + n + ": the canonical log is the start action + min drags");
                var band = Band(min, data.Difficulty, data.Teaches != null);
                Assert.That(data.Moves, Is.InRange(band.Lo, band.Hi), "level " + n + ": moves " + data.Moves + " outside [" + band.Lo + ", " + band.Hi + "] (min " + min + ", " + band.Row + " row)");
            }
        }

        // --- K-51 item 1: firstNeedDepth ≥ 1 --------------------------------------------------------------------------------

        /// <summary>
        /// K-50 item 5 depth 0: some release of some pickable block (a node of R that is no start node and no K-07 cancel)
        /// is a correct placement now. Every candidate runs through the C# pipeline on a copy of the state.
        /// </summary>
        private static bool CorrectPlacementAtStart(CompiledLevel lvl, out string witness)
        {
            var game = GameSession.Start(lvl);
            var s = game.State;
            var hooks = game.Hooks;
            int pieces = lvl.Layout.Counts.Pieces;
            for (int id = 0; id < pieces; id++)
            {
                int z = State.PieceZone(s, id);
                if (z != Zone.Yard && z != Zone.Site) continue;
                var attempt = Movement.TryBeginDrag(s, id, hooks.Drag);
                if (!attempt.Ok) continue;
                var d = attempt.Session!;
                foreach (var node in d.ReachableNodes())
                {
                    if (d.StartNodes.Contains(node) || Movement.IsCancelled(d.Classify(node))) continue;
                    var copy = State.CloneState(s);
                    var sink = new ArraySink();
                    Moves.ApplyMove(copy, new DragMove(id, node), sink, new ApplyOptions(hooks: hooks, noTruckHelp: true));
                    if (sink.Events.Any(e => e is PlacementCorrectEvent))
                    {
                        witness = "piece " + id + " → (" + node.Ix + "," + node.Iy + ", mode " + node.Mode + ")";
                        return true;
                    }
                }
            }
            witness = "";
            return false;
        }

        [Test(Description = "K-51 item 1 (L-32 puzzle_first_reachable): levels ≥ 3 allow no correct placement at the start (firstNeedDepth ≥ 1, so minShifts ≥ 1); levels 1–2 are tutorials")]
        public void K51_item_1_levels_3_to_10_allow_no_correct_placement_at_the_start_firstNeedDepth_at_least_1()
        {
            foreach (int n in LevelIds())
            {
                bool now = CorrectPlacementAtStart(Boards.LevelFile(n), out string witness);
                if (n >= 3) Assert.IsFalse(now, "level " + n + ": a correct placement is possible at the start (" + witness + ")");
            }
            // the tutorials start with an open block (0 is allowed there)
            Assert.IsTrue(CorrectPlacementAtStart(Boards.LevelFile(1), out _), "level 1 is a tutorial: its first block is open");
        }

        [Test(Description = "K-51 puzzle_first_reachable: level 3 with O4 G bottom right and the first blocks open (GDD K-51 example) has firstNeedDepth 0")]
        public void K51_GDD_example_level_3_with_O4_G_bottom_right_and_the_first_blocks_open_has_firstNeedDepth_0()
        {
            var json = TestPaths.LoadLevelJson(3);
            var pieces = J.A(
                J.O("shape", "D2_0", "color", "Y", "x", 0, "y", 0),
                J.O("shape", "D2_0", "color", "W", "x", 1, "y", 0),
                J.O("shape", "O4_0", "color", "G", "x", 2, "y", 0),
                J.O("shape", "D2_90", "color", "Y", "x", 2, "y", 2));
            json["yard"]!.Set("batches", J.A(J.O("forSegment", 0, "pieces", pieces)));
            var lvl = Compiler.Compile(LevelJson.ParseOrThrow(json));
            Assert.IsTrue(CorrectPlacementAtStart(lvl, out string witness), "the open D2_0 Y / D2_0 W reach their plan cells at once");
            StringAssert.Contains("→", witness);
            // the shipped level 3 hides them (LEVELS §2.0 "tuzak kaynağı" fixed): depth ≥ 1
            Assert.IsFalse(CorrectPlacementAtStart(Boards.LevelFile(3), out _));
        }

        // --- K-51 item 4 (K-46) and item 5, E-54: the canonical solutions ---------------------------------------------------

        private static (GameSession Game, List<ActionResult> Steps) PlayCanonical(int n)
        {
            var lvl = Boards.LevelFile(n);
            var log = SessionActionJson.LogFromJson(SolverGolden(n)["log"]!);
            var start = (StartAction)log[0];
            var game = GameSession.Start(lvl, start.PreBoosters, start.StreakTier, new SessionOptions(strict: true));
            var steps = new List<ActionResult>();
            for (int i = 1; i < log.Length; i++) steps.Add(game.Apply(log[i]));
            return (game, steps);
        }

        [Test(Description = "K-51 item 4 K-46: every canonical solution of levels 1–10 wins with YAO ≥ 0.60 (L-19 yao_low), equal to the solver golden")]
        public void K51_item_4_K46_every_canonical_solution_wins_with_YAO_at_least_0_60()
        {
            foreach (int n in LevelIds())
            {
                var (game, steps) = PlayCanonical(n);
                Assert.AreEqual(BoosterOutcomes.Won, game.Outcome, "level " + n);
                Assert.IsTrue(steps.All(r => r.Status == MoveStatuses.Applied), "level " + n + ": every canonical drag is applied");
                var yao = game.Yao();
                var golden = SolverGolden(n)["yao"]!;
                CollectionAssert.AreEqual(new[] { golden["overWall"]!.AsInt, golden["rail"]!.AsInt }, new[] { yao.OverWall, yao.Rail }, "level " + n);
                Assert.IsTrue(yao.Yao.HasValue && yao.Yao.Value >= 0.6, "level " + n + ": YAO " + yao.Yao);
            }
        }

        [Test(Description = "K-45/9 L-34 level 10: no truck block waits in the canonical solution (E-54 queue removed, LEVELS §2.0 3f)")]
        public void K45_9_L34_E54_level_10_no_truck_block_waits_in_the_canonical_solution()
        {
            var (game, steps) = PlayCanonical(10);
            for (int i = 0; i < steps.Count; i++)
            {
                CollectionAssert.IsEmpty(steps[i].Summary.Queue, "after canonical move " + (i + 1));
                CollectionAssert.DoesNotContain(steps[i].Events.Select(e => e.TypeName).ToArray(), "deliveryQueued", "move " + (i + 1));
            }
            Assert.AreEqual(BoosterOutcomes.Won, game.Outcome);
        }

        [Test(Description = "K-51 item 5 level 10: the truck of segment 2 comes on canonical move 8 and 7 moves remain to the win (D 8, g 0)")]
        public void K51_item_5_level_10_the_truck_of_segment_2_comes_on_canonical_move_8_and_7_moves_remain()
        {
            var (game, steps) = PlayCanonical(10);
            int arrival = steps.FindIndex(r => r.Events.Any(e => e.TypeName == "deliveryArrived")) + 1;
            Assert.AreEqual(8, arrival, "the delivery of batch 1 (segment 0 completed)");
            Assert.AreEqual(7, steps.Count - arrival, "moves from the delivery to the win (dist 7, min 15 = 8 + 7: g = 0)");
            Assert.AreEqual(15, SolverGolden(10)["min"]!.AsInt);
            Assert.AreEqual(BoosterOutcomes.Won, game.Outcome);
        }
    }
}
