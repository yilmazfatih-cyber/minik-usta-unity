#nullable enable
// Port of the core halves of tests/review/data.review.test.ts (web repo): the describes "review K-31 colour set and
// unlocks" and "review K-27 material and K-25 truck batches" (validator L-06, L-07, L-10, L-12, L-30 on builder levels
// and level files). The rest of data.review compares the data with docs/LEVELS.md and GDD text and stays in the web repo.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class DataReviewTests
    {
        // --- helpers (data.review.test.ts) ----------------------------------------------------------------------------

        private static P Pc(string shape, string color, int x, int y) => new P(shape, color, x, y);
        private static P[] Ps(params P[] ps) => ps;

        /// <summary>TS `codes`: the distinct issue codes, sorted.</summary>
        private static string[] Codes(IEnumerable<Issue> list) => list.Select(i => i.Code).Distinct().OrderBy(c => c, System.StringComparer.Ordinal).ToArray();

        /// <summary>TS `run(spec, only)`: logic checks `only` on a schema-valid builder level.</summary>
        private static Issue[] Run(LevelSpec spec, params string[] only) => Logic.CheckLevel(Builders.Level(spec), new LogicContext(only: only));

        /// <summary>TS `runJson(json, only?)`: schema + logic checks on raw JSON.</summary>
        private static Issue[] RunJson(JsonValue json, params string[] only) =>
            Logic.ValidateLevelJson(json, only.Length > 0 ? new LogicContext(only: only) : new LogicContext()).Issues;

        private static JsonValue BatchOf(JsonValue json, int k) => json["yard"]!["batches"]!.Items[k];

        private static JsonValue Gap(string type, int y, int size, string? color = null)
        {
            var g = J.O("type", type, "y", y, "size", size);
            if (color != null) g.Set("color", JsonValue.Of(color));
            return g;
        }

        // --- K-31 colour set (L-06, L-07) ----------------------------------------------------------------------------------

        [Test(Description = "K-31 GDD example: level 12 (story chapter 2) with W, Y, O, C, R in the plan is 5 > 4 colours → too_many_colors")]
        public void K31_GDD_example_level_12_with_5_plan_colours_in_story_chapter_2_is_too_many_colors()
        {
            var issues = Run(new LevelSpec { Id = 12, Plan = new[] { "WY", "OC", "RR" }, Pieces = Ps(Pc("D2_90", "R", 0, 0)) }, "L-06", "L-07");
            CollectionAssert.AreEqual(new[] { "too_many_colors|K-45/4|error" }, issues.Select(i => i.Code + "|" + i.Rule + "|" + i.Severity).ToArray());
        }

        [Test(Description = "K-31 GDD example on level_005.json (limit 3): plan R/W/Y and blocks R/W/Y are valid; one G block in the yard makes 4 colours → too_many_colors (and the full cover breaks → cover_mismatch)")]
        public void K31_GDD_example_on_level_5_a_G_block_makes_4_colours_too_many_colors_and_cover_mismatch()
        {
            var json = TestPaths.LoadLevelJson(5);
            CollectionAssert.IsEmpty(RunJson(json));
            var parsed = Logic.ValidateLevelJson(json).Level;
            Assert.IsNotNull(parsed, "level 5 schema");
            CollectionAssert.AreEqual(new[] { "R", "W", "Y" }, Logic.LevelColorSet(parsed!).OrderBy(c => c, System.StringComparer.Ordinal).ToArray());
            BatchOf(json, 0)["pieces"]!.Add(J.O("shape", "B1_0", "color", "G", "x", 2, "y", 2));
            CollectionAssert.AreEqual(new[] { "cover_mismatch", "too_many_colors" }, Codes(RunJson(json)));
        }

        [Test(Description = "K-31 GDD example: in level 22 the paint gate colour P joins the set (B, P, Y, R → 4 ≤ 5, nothing locked)")]
        public void K31_GDD_example_level_22_the_paint_gate_colour_joins_the_set_4_colours_nothing_locked()
        {
            var spec = new LevelSpec
            {
                Id = 22,
                Wall = new WallSpec { Height = 6, Gaps = new[] { Gap("paint", 1, 2, "P") } },
                Plan = new[] { "BB", "YR" },
                Pieces = Ps(Pc("D2_90", "B", 0, 0), Pc("B1_0", "Y", 2, 0), Pc("B1_0", "R", 3, 0)),
            };
            CollectionAssert.AreEqual(new[] { "Y", "R", "B", "P" }, Logic.LevelColorSet(Builders.Level(spec)));
            CollectionAssert.IsEmpty(Run(spec, "L-06", "L-07"));
        }

        [Test(Description = "K-31 colours unlock at their first level (W,Y 1; G 2; R 4; O,C 11; B,P 21) for every source: decoy, truck block, debris, paint gate")]
        public void K31_colours_unlock_at_their_first_level_for_every_source_decoy_truck_debris_paint_gate()
        {
            string[] LockedAt(LevelSpec spec) => Run(spec, "L-07").Select(i => i.Code).ToArray();
            // G decoy in level 1, fine in level 2
            LevelSpec GDecoy(int id) => new LevelSpec { Id = id, Plan = new[] { "WW", "YY" }, Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "Y", 2, 0), Pc("B1_0", "G", 4, 0)) };
            CollectionAssert.AreEqual(new[] { "color_locked" }, LockedAt(GDecoy(1)));
            CollectionAssert.IsEmpty(LockedAt(GDecoy(2)));
            // R only in a truck batch: locked in level 3, fine in level 4
            LevelSpec RTruck(int id) => new LevelSpec
            {
                Id = id,
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("D2_90", "Y", 0, 8), Pc("B1_0", "R", 2, 8)) } },
            };
            CollectionAssert.AreEqual(new[] { "color_locked" }, LockedAt(RTruck(3)));
            CollectionAssert.IsEmpty(LockedAt(RTruck(4)));
            // C only as debris: locked in level 10, fine in 11
            LevelSpec CDebris(int id) => new LevelSpec
            {
                Id = id,
                Plan = new[] { "WW", "YY" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "Y", 2, 0)),
                Debris = new[] { new D("B1_0", "C", 6, 0) },
            };
            CollectionAssert.AreEqual(new[] { "color_locked" }, LockedAt(CDebris(10)));
            CollectionAssert.IsEmpty(LockedAt(CDebris(11)));
            // B only as a paint gate colour: locked in level 20, fine in 21
            LevelSpec BGate(int id) => new LevelSpec
            {
                Id = id,
                Wall = new WallSpec { Height = 6, Gaps = new[] { Gap("paint", 1, 2, "B") } },
                Plan = new[] { "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0)),
            };
            CollectionAssert.AreEqual(new[] { "color_locked" }, LockedAt(BGate(20)));
            CollectionAssert.IsEmpty(LockedAt(BGate(21)));
        }

        [Test(Description = "K-31 colour limit per story chapter: 4 colours fail in level 10 and pass in 11; 6 colours fail in 21 and 41, 5 pass")]
        public void K31_colour_limit_per_story_chapter_4_colours_fail_in_level_10_pass_in_11_6_fail_in_21_and_41()
        {
            string[] TooMany(int id, params string[] plan) => Run(new LevelSpec { Id = id, Plan = plan, Pieces = Ps(Pc("B1_0", "W", 0, 0)) }, "L-06").Select(i => i.Code).ToArray();
            CollectionAssert.AreEqual(new[] { "too_many_colors" }, TooMany(10, "WY", "GR"));
            CollectionAssert.IsEmpty(TooMany(11, "WY", "GR"));
            CollectionAssert.IsEmpty(TooMany(21, "WY", "GR", "OO"));
            CollectionAssert.AreEqual(new[] { "too_many_colors" }, TooMany(21, "WY", "GR", "OC"));
            CollectionAssert.AreEqual(new[] { "too_many_colors" }, TooMany(41, "WY", "GR", "OC"));
        }

        [Test(Description = "K-31 resolved `?` colours count, `.` is not a colour: repeat `?` takes the colour p rows below")]
        public void K31_resolved_hidden_colours_count_a_window_is_not_a_colour_repeat_takes_the_colour_p_rows_below()
        {
            var spec = new LevelSpec
            {
                Id = 27,
                Plan = new[] { "?.", "RW", "YW" },
                Hidden = new JsonValue?[] { J.O("kind", "repeat", "period", 2) },
                Pieces = Ps(Pc("B1_0", "W", 0, 0)),
            };
            // (6,2) = `?` → (6,0) = Y (period 2); the set is W, Y, R from the plan and W from the block.
            CollectionAssert.AreEqual(new[] { "W", "Y", "R" }, Logic.LevelColorSet(Builders.Level(spec)));
            var seg = Builders.Compiled(spec).Segments[0];
            Assert.AreEqual(System.Array.IndexOf(ColorCodes.All, "Y"), (int)seg.PlanColors[2 * 2 + 0]);
        }

        // --- K-27 material (L-10), K-25 batches (L-12) ------------------------------------------------------------------

        [Test(Description = "K-27 GDD example (Faz 2R, K-47): level 5 segment 2 has R 4, W 2, Y 4 and batch 1 brings O4 Y, D2_0 R × 2, D2_90 W → valid; a C3 W added to the batch is cover_mismatch (no decoys)")]
        public void K27_K47_GDD_example_level_5_batch_1_covers_segment_2_a_C3_W_decoy_is_cover_mismatch()
        {
            var json = TestPaths.LoadLevelJson(5);
            CollectionAssert.IsEmpty(RunJson(json, "L-10", "L-30"));
            CollectionAssert.AreEqual(new[] { "O4_0 Y", "D2_0 R", "D2_0 R", "D2_90 W" },
                BatchOf(json, 1)["pieces"]!.Items.Select(p => p["shape"]!.AsString + " " + p["color"]!.AsString).ToArray());
            BatchOf(json, 1)["pieces"]!.Add(J.O("shape", "C3_0", "color", "W", "x", 0, "y", 4));
            CollectionAssert.AreEqual(new[] { "cover_mismatch|K-45/8" }, RunJson(json, "L-10", "L-30").Select(i => i.Code + "|" + i.Rule).ToArray());
        }

        [Test(Description = "K-27 K-47 Ağır Yük is not supply: a Q9 \"W\" next to one O4 W cannot cover 8 W cells (cover_mismatch); two O4 W can")]
        public void K27_K47_Agir_Yuk_is_not_supply_a_Q9_next_to_one_O4_cannot_cover_8_cells_two_O4_can()
        {
            string[] Supply(params P[] pieces) =>
                Run(new LevelSpec { Id = 8, Site = new SizeSpec(2, 4), Plan = new[] { "WW", "WW", "WW", "WW" }, Pieces = pieces }, "L-10", "L-30").Select(i => i.Code).ToArray();
            CollectionAssert.AreEqual(new[] { "cover_mismatch" }, Supply(Pc("Q9_0", "W", 0, 0), Pc("O4_0", "W", 3, 0)));
            CollectionAssert.IsEmpty(Supply(Pc("Q9_0", "W", 0, 0), Pc("O4_0", "W", 3, 0), Pc("O4_0", "W", 3, 2)));
        }

        [Test(Description = "K-47 item 1 with a W6 paint gate the cover equality is on the total cell count (the per-colour balance is the solver's); without the gate the same data is cover_mismatch")]
        public void K47_K27_with_a_paint_gate_the_cover_equality_is_on_the_total_cell_count_without_it_cover_mismatch()
        {
            LevelSpec Spec(params JsonValue[] gaps) => new LevelSpec
            {
                Id = 22,
                Site = new SizeSpec(2, 4),
                Wall = new WallSpec { Height = 6, Gaps = gaps },
                Plan = new[] { "PP", "PP", "YY", "YY" },
                Pieces = Ps(Pc("O4_0", "Y", 0, 0), Pc("O4_0", "Y", 2, 0)),
            };
            CollectionAssert.IsEmpty(Run(Spec(Gap("paint", 1, 2, "P")), "L-10", "L-30"));
            CollectionAssert.AreEqual(new[] { "cover_mismatch" }, Codes(Run(Spec(), "L-10", "L-30")));
            // the material condition still counts every block once, for one colour: 4 cells as Y or as P, never 2 + 2
            var block = new SupplyBlock(0, "Y", 4, new[] { "P" });
            Assert.Greater(Logic.MaterialShortfall(new Dictionary<string, int> { { "Y", 2 }, { "P", 2 } }, new[] { block }).Length, 0);
        }

        [Test(Description = "K-25 truck blocks: y written as 8, 0 ≤ x ≤ 6 − w, forSegment 1…S−1, dropColumns fit the block")]
        public void K25_K27_truck_blocks_y_written_as_8_x_fits_forSegment_1_to_S_minus_1_dropColumns_fit()
        {
            string[] Truck(P[] pieces, int forSegment = 1, int[]? dropColumns = null) =>
                Run(new LevelSpec
                {
                    Id = 5,
                    Plans = new[] { new[] { "WW" }, new[] { "WW" }, new[] { "WW" } },
                    Pieces = Ps(Pc("D2_90", "W", 0, 0)),
                    Batches = new[] { new BatchSpec { ForSegment = forSegment, Pieces = pieces, DropColumns = dropColumns } },
                }, "L-12").Select(i => i.Code).ToArray();
            CollectionAssert.IsEmpty(Truck(Ps(Pc("O4_0", "W", 4, 8))));
            CollectionAssert.AreEqual(new[] { "batch_invalid" }, Truck(Ps(Pc("O4_0", "W", 5, 8))));
            CollectionAssert.IsEmpty(Truck(Ps(Pc("B1_0", "W", 5, 8))));
            CollectionAssert.AreEqual(new[] { "batch_invalid" }, Truck(Ps(Pc("B1_0", "W", 6, 8))));
            CollectionAssert.IsEmpty(Truck(Ps(Pc("B1_0", "W", 0, 8)), forSegment: 2));
            CollectionAssert.AreEqual(new[] { "batch_invalid" }, Truck(Ps(Pc("B1_0", "W", 0, 8)), forSegment: 3));
            CollectionAssert.AreEqual(new[] { "batch_invalid" }, Truck(Ps(Pc("B1_0", "W", 0, 8)), forSegment: 0));
            CollectionAssert.IsEmpty(Truck(Ps(Pc("D2_90", "W", 0, 8)), dropColumns: new[] { 4, 0 }));
            CollectionAssert.AreEqual(new[] { "batch_invalid" }, Truck(Ps(Pc("D2_90", "W", 0, 8)), dropColumns: new[] { 5 }));
            // y of a truck block is written as 8 (GDD §14)
            var json = Builders.LevelJson(new LevelSpec
            {
                Id = 5,
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0)),
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("B1_0", "W", 0, 8)) } },
            });
            BatchOf(json, 1)["pieces"]!.Items[0].Set("y", JsonValue.Of(7));
            CollectionAssert.AreEqual(new[] { "batch_invalid" }, RunJson(json, "L-12").Select(i => i.Code).ToArray());
        }
    }
}
