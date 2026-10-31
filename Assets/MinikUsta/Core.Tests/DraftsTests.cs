#nullable enable
// Port of tests/level/drafts.test.ts (web repo): levels 1–10 in the Faz 2R format validate with their mechanic history and the
// GDD counter-examples are caught. The two TS tests that read docs/LEVELS.md (summary rows, text keys) stay in the web repo.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class DraftsTests
    {
        private static readonly int[] Ids = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        /// <summary>levels/level_NNN.json (a fresh copy: the counter-example tests change it).</summary>
        private static JsonValue Draft(int n) => TestPaths.LoadLevelJson(n);

        private static string[] Codes(IEnumerable<Issue> issues) => issues.Select(i => i.Code).Distinct().OrderBy(c => c, System.StringComparer.Ordinal).ToArray();

        /// <summary>Validates draft n with the mechanic history of drafts 1 … n − 1 (as `levels:validate` does).</summary>
        private static Issue[] ValidateDraft(int n, JsonValue? json = null)
        {
            var previous = new HashSet<string>();
            for (int k = 1; k < System.Math.Min(n, 11); k++)
                foreach (var m in Mechanics.DeriveMechanics(LevelJson.ParseOrThrow(Draft(k)))) previous.Add(m);
            return Logic.ValidateLevelJson(json ?? Draft(n), new LogicContext(fileId: n, previousMechanics: previous, boosterUnlock: TestPaths.LoadBoosterUnlock())).Issues;
        }

        [Test(Description = "K-45 every level passes the schema and every validate check (mechanic history, booster unlocks)")]
        public void K45_every_level_passes_the_schema_and_every_validate_check()
        {
            foreach (int n in Ids)
            {
                var issues = ValidateDraft(n);
                CollectionAssert.IsEmpty(issues, "level " + n + ": " + string.Join("\n", issues.Select(i => i.ToString())));
            }
        }

        [Test(Description = "K-45/10 the derived new mechanics are 4 → W1, 5 → S1, 6 → W2, 8 → Y5, 9 → W3 (OBSTACLES \"Veri imzası\" Faz 2R)")]
        public void K45_10_the_derived_new_mechanics_are_4_W1_5_S1_6_W2_8_Y5_9_W3()
        {
            var seen = new HashSet<string>();
            var fresh = new Dictionary<int, string[]>();
            foreach (int n in Ids)
            {
                var lvl = LevelJson.ParseOrThrow(Draft(n));
                var derived = Mechanics.DeriveMechanics(lvl);
                fresh[n] = derived.Where(m => !seen.Contains(m)).ToArray();
                foreach (var m in derived) seen.Add(m);
                Assert.AreEqual(fresh[n].Length > 0 ? fresh[n][0] : null, lvl.Teaches, "level " + n + " teaches");
            }
            var expected = new Dictionary<int, string[]>
            {
                { 1, new string[0] }, { 2, new string[0] }, { 3, new string[0] }, { 4, new[] { "W1" } }, { 5, new[] { "S1" } },
                { 6, new[] { "W2" } }, { 7, new string[0] }, { 8, new[] { "Y5" } }, { 9, new[] { "W3" } }, { 10, new string[0] },
            };
            foreach (int n in Ids) CollectionAssert.AreEqual(expected[n], fresh[n], "level " + n);
        }

        [Test(Description = "K-49 K-02 K-47 each level's sizes, material count and E are consistent with its data (the LEVELS 2 summary-row comparison stays in the web repo)")]
        public void K49_K02_K47_each_level_is_consistent_with_its_data()
        {
            foreach (int n in Ids)
            {
                var lvl = LevelJson.ParseOrThrow(Draft(n));
                var geo = Geometry.GeoFromLevel(lvl);
                var compiled = Compiler.Compile(lvl);
                int filled = lvl.Yard.Batches[0].Pieces.Sum(p => Shapes.ShapeById(p.Shape).CellCount);
                int e = geo.Wy * geo.Hy - filled;
                Assert.GreaterOrEqual(e, Logic.YardEmptyMin, "level " + n + " E");
                Assert.LessOrEqual(e, Logic.YardEmptyMax(geo.Wy * geo.Hy), "level " + n + " E");
                int material = lvl.Yard.Batches.SelectMany(b => b.Pieces).Count(p => !Mechanics.IsCargo(Shapes.ShapeById(p.Shape)));
                Assert.AreEqual(material, compiled.MaterialCount, "level " + n + " N");
                CollectionAssert.AreEqual(compiled.Demand, compiled.Supply, "level " + n + " K-47 full cover");
            }
        }

        // --- K-45 GDD counter-examples on levels 1–10 -------------------------------------------------------------------------------

        [Test(Description = "K-47 GDD example level 3: one more D2_90 Y makes the Y supply 6 > 4: cover_mismatch")]
        public void K47_GDD_example_level_3_one_more_D2_90_Y_is_cover_mismatch()
        {
            var json = Draft(3);
            json["yard"]!["batches"]![0]["pieces"]!.Add(J.O("shape", "D2_90", "color", "Y", "x", 2, "y", 1));
            CollectionAssert.AreEqual(new[] { "cover_mismatch" }, Codes(ValidateDraft(3, json)));
        }

        [Test(Description = "K-45 GDD example level 2: one B1 more keeps E = 5 in the band but supply 11 > 10: cover_mismatch")]
        public void K45_GDD_example_level_2_one_B1_more_is_cover_mismatch()
        {
            var json = Draft(2);
            json["yard"]!["batches"]![0]["pieces"]!.Add(J.O("shape", "B1_0", "color", "W", "x", 0, "y", 2));
            CollectionAssert.AreEqual(new[] { "cover_mismatch" }, Codes(ValidateDraft(2, json)));
        }

        [Test(Description = "K-45/3 GDD example level 9: the narrow gap moved to y = 3 is gap_touches_top (3 + 1 > height − 1)")]
        public void K45_3_GDD_example_level_9_the_narrow_gap_moved_to_y_3_is_gap_touches_top()
        {
            var json = Draft(9);
            json["wall"]!.Set("gaps", J.A(J.O("type", "static", "y", 3, "size", 1)));
            CollectionAssert.AreEqual(new[] { "gap_touches_top" }, Codes(ValidateDraft(9, json)));
        }

        [Test(Description = "K-45/10 GDD example level 4: a size-1 gap derives {W1, W3}: too_many_new_mechanics")]
        public void K45_10_GDD_example_level_4_a_size_1_gap_is_too_many_new_mechanics()
        {
            var json = Draft(4);
            json["wall"]!.Set("gaps", J.A(J.O("type", "static", "y", 0, "size", 1)));
            CollectionAssert.AreEqual(new[] { "too_many_new_mechanics" }, Codes(ValidateDraft(4, json)));
        }

        [Test(Description = "K-49 GDD example: Wy = 6, Ws = 3 is board_too_wide")]
        public void K49_GDD_example_Wy_6_Ws_3_is_board_too_wide()
        {
            var json = Draft(1);
            json["yard"]!.Set("cols", JsonValue.Of(6));
            json.Set("site", J.O("cols", 3, "rows", 5));
            CollectionAssert.AreEqual(new[] { "board_too_wide" }, Codes(ValidateDraft(1, json)));
        }

        [Test(Description = "K-44 GDD example: an L4_90 W on a 2-column site is piece_too_wide")]
        public void K44_GDD_example_an_L4_90_W_on_a_2_column_site_is_piece_too_wide()
        {
            var json = Draft(1);
            json.Set("id", JsonValue.Of(12)).Set("chapter", JsonValue.Of(2)).Set("difficulty", JsonValue.Of("normal"));
            var first = json["yard"]!["batches"]![0]["pieces"]![0];
            first.Set("shape", JsonValue.Of("L4_90")).Set("color", JsonValue.Of("W")).Set("x", JsonValue.Of(0)).Set("y", JsonValue.Of(2));
            CollectionAssert.Contains(ValidateDraft(12, json).Select(i => i.Code).ToArray(), "piece_too_wide");
        }

        [Test(Description = "K-15 K-45/4 GDD example [\"YY\",\"W.\"] is plan_has_window and (Hs = 5) plan_size")]
        public void K15_K45_4_GDD_example_YY_W_dot_is_plan_has_window_and_plan_size()
        {
            var json = Draft(2);
            json["build"]!["segments"]![0].Set("rows", J.V(new[] { "YY", "W." }));
            CollectionAssert.AreEqual(new[] { "plan_has_window", "plan_size" }, Codes(ValidateDraft(2, json)));
        }

        [Test(Description = "K-44 GDD example level 8: Q9_0 is Ağır Yük even with a colour written; it stays out of the colour set")]
        public void K44_GDD_example_level_8_Q9_0_is_Agir_Yuk_even_with_a_colour_written()
        {
            var json = Draft(8);
            json["yard"]!["batches"]![0]["pieces"]![5].Set("color", JsonValue.Of("G"));
            CollectionAssert.IsEmpty(ValidateDraft(8, json));
        }
    }
}
