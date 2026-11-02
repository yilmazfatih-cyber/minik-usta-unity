#nullable enable
// Port of tests/level/validator.test.ts (web repo): the validate stage (TECH §8.3 + Faz 2R §2R.3; GDD K-45 items 1–8 and 10,
// K-02, K-44, K-47, K-49, K-53). One invalid fixture per code in Fixtures/Levels/invalid (copies of tests/level/fixtures/invalid,
// each otherwise clean, so the case lists its exact code set); the valid fixtures pass every check with the history their
// `teaches` needs.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class LogicTests
    {
        public sealed class Case
        {
            public readonly string Name;
            public readonly string File;
            public readonly string Code;
            public readonly string Rule;
            public readonly string Severity;
            public readonly LogicContext? Ctx;

            public Case(string name, string file, string code, string rule, string severity = Severities.Error, LogicContext? ctx = null)
            {
                Name = name;
                File = file;
                Code = code;
                Rule = rule;
                Severity = severity;
                Ctx = ctx;
            }

            public override string ToString() => Name;
        }

        private static readonly I18nKeySets I18N = new I18nKeySets(new HashSet<string> { "tut.m.dig", "tut.m.free" }, new HashSet<string> { "tut.m.dig", "tut.m.free" });
        private static ISet<string> Prev(params string[] ids) => new HashSet<string>(ids);

        private static readonly Case[] Cases =
        {
            // K-45/1 schema and identity
            new Case("K-45/1 schema_invalid unknown key", "schema_invalid", "schema_invalid", "K-45/1"),
            new Case("K-45/1 K-44 schema_invalid a material block without color", "schema_invalid_color_missing", "schema_invalid", "K-45/1"),
            new Case("K-45/1 K-53/3 schema_invalid timeoutMs is no done condition (CL-2R-10)", "schema_invalid_timeoutms", "schema_invalid", "K-45/1"),
            new Case("K-45/1 chapter_mismatch chapter ≠ ceil(id/10)", "chapter_mismatch", "chapter_mismatch", "K-45/1"),
            // K-45/2 sizes and yard
            new Case("K-45/2 K-49 size_out_of_range Ws 1 (Ws 2–3 in story chapters 1–3)", "size_out_of_range", "size_out_of_range", "K-45/2"),
            new Case("K-45/2 K-49 board_too_wide Wy 6 + Ws 3 > 8", "board_too_wide", "board_too_wide", "K-45/2"),
            new Case("K-45/2 overlap two blocks on one cell", "overlap", "overlap", "K-45/2"),
            new Case("K-45/2 out_of_yard a block crosses x = Wy − 1", "out_of_yard", "out_of_yard", "K-45/2"),
            new Case("K-45/2 out_of_yard an obstacle outside Wy × Hy", "out_of_yard_obstacle", "out_of_yard", "K-45/2"),
            new Case("K-45/2 K-02 yard_fill_low E = 12 > ⌊0,4·24⌋", "yard_fill_low", "yard_fill_low", "K-45/2"),
            new Case("K-45/2 K-02 yard_fill_high E = 1 < 2", "yard_fill_high", "yard_fill_high", "K-45/2"),
            // K-45/3 wall
            new Case("K-45/3 gap_touches_top y + size > height − 1", "gap_touches_top", "gap_touches_top", "K-45/3"),
            new Case("K-45/3 gap_overlap across slider range", "gap_overlap_slider_range", "gap_overlap", "K-45/3"),
            new Case("K-45/3 shutter_phase phase ≥ 2·period", "shutter_phase", "shutter_phase", "K-45/3"),
            new Case("K-45/3 slider_range a == b", "slider_range_a_eq_b", "slider_range", "K-45/3"),
            new Case("K-45/3 key_missing locked gap without key", "key_missing", "key_missing", "K-45/3"),
            new Case("K-45/3 fan_unused warns without 1-wide blocks", "fan_unused", "fan_unused", "K-45/3", Severities.Warn),
            new Case("K-45/3 K-04 a wall above the board height H is size_out_of_range on wall.height", "wall_height_over_h", "size_out_of_range", "K-45/3"),
            // K-45/4 plan
            new Case("K-45/4 K-15 plan_size rows ≠ site.rows", "plan_size_rows", "plan_size", "K-45/4"),
            new Case("K-45/4 K-15 plan_size a row of 3 characters on a 2-column site", "plan_size_width", "plan_size", "K-45/4"),
            new Case("K-45/4 K-15 plan_size a row of 5 characters (schema stage, not schema_invalid)", "plan_size_schema", "plan_size", "K-45/4"),
            new Case("K-45/4 K-15 plan_has_window a `.` cell (R2-01)", "plan_has_window", "plan_has_window", "K-45/4"),
            new Case("K-45/4 K-24 elevator_overflow Hs + b > 8", "elevator_overflow", "elevator_overflow", "K-45/4"),
            new Case("K-45/4 too_many_colors 4 colours in story chapter 1", "too_many_colors", "too_many_colors", "K-45/4"),
            new Case("K-45/4 color_locked R before level 4", "color_locked", "color_locked", "K-45/4"),
            new Case("K-45/4 hidden_invalid ? without hidden rule", "hidden_invalid", "hidden_invalid", "K-45/4"),
            // K-45/5 shapes
            new Case("K-45/5 shape_forbidden I5_90 (K-44)", "shape_forbidden", "shape_forbidden", "K-45/5"),
            new Case("K-45/5 K-44 shape_locked I3 in story chapter 1", "shape_locked_i3_chapter1", "shape_locked", "K-45/5"),
            new Case("K-45/5 K-44 shape_locked Ağır Yük Q9 in level 7", "shape_locked_q9_level7", "shape_locked", "K-45/5"),
            new Case("K-45/5 K-44 piece_too_wide a 3-wide I3_90 on a 2-column site", "piece_too_wide", "piece_too_wide", "K-45/5"),
            new Case("K-45/5 K-21 flag_combo_forbidden glass + balloon", "flag_combo_forbidden", "flag_combo_forbidden", "K-45/5"),
            new Case("K-45/5 K-44 flag_combo_forbidden Ağır Yük cannot be glass", "flag_combo_cargo_glass", "flag_combo_forbidden", "K-45/5"),
            // K-45/6 hidden items and goals
            new Case("K-45/6 hidden_item_exposed screw not covered", "hidden_item_exposed", "hidden_item_exposed", "K-45/6"),
            new Case("K-45/6 hidden_item_stacked two screws in one cell", "hidden_item_stacked", "hidden_item_stacked", "K-45/6"),
            new Case("K-45/6 goal_count_too_high clear crate 2 with 1 crate", "goal_count_too_high", "goal_count_too_high", "K-45/6"),
            new Case("K-45/6 goal_build_missing", "goal_build_missing", "goal_build_missing", "K-45/6"),
            // K-45/7 debris
            new Case("K-45/7 debris_misplaced debris in the yard (x < Wy)", "debris_misplaced", "debris_misplaced", "K-45/7"),
            new Case("K-45/7 S4 debris_correct_at_start debris already on its colour with support", "debris_correct_at_start", "debris_correct_at_start", "K-45/7"),
            // K-45/8 full cover
            new Case("K-45/8 K-47 cover_mismatch supply ≠ demand per colour", "cover_mismatch", "cover_mismatch", "K-45/8"),
            new Case("K-45/8 K-47 cover_prefix_short segment 0 needs a colour that only batch 1 brings", "cover_prefix_short", "cover_prefix_short", "K-45/8"),
            new Case("K-45/8 K-30 untileable D3a finds no exact bottom-up cover", "untileable", "untileable", "K-45/8"),
            // K-25 batches
            new Case("K-25 batch_invalid truck y must be Hy", "batch_invalid", "batch_invalid", "K-25"),
            // K-45/10 mechanics and tutorial
            new Case("K-45/10 teaches_mismatch teaches must equal the derived new mechanic", "teaches_mismatch", "teaches_mismatch", "K-45/10", Severities.Error, new LogicContext(previousMechanics: Prev())),
            new Case("K-45/10 too_many_new_mechanics a size-1 static gap is W1 + W3", "too_many_new_mechanics_size1_gap_level4", "too_many_new_mechanics", "K-45/10", Severities.Error, new LogicContext(previousMechanics: Prev())),
            new Case("K-45/10 K-53/1 tut_too_many_steps 3 steps", "tut_too_many_steps", "tut_too_many_steps", "K-45/10"),
            new Case("K-45/10 K-53/2 tut_blocking mode \"required\"", "tut_blocking_mode", "tut_blocking", "K-45/10"),
            new Case("K-45/10 K-53/2 tut_blocking a spotlight field", "tut_blocking_spot", "tut_blocking", "K-45/10"),
            new Case("GDD 14.1 tut_key_missing text key not in tr/en", "tut_key_missing", "tut_key_missing", "GDD 14.1", Severities.Error, new LogicContext(i18nKeys: I18N)),
            new Case("GDD 14.1/1 tut_key_missing textKey outside tut.l<n> | tut.m | tut.ctx (schema stage)", "tut_key_format", "tut_key_missing", "GDD 14.1"),
            new Case("GDD 14.1/5 tut_highlight_invalid a truck block before its batch is delivered", "tut_highlight_batch_piece_without_startOn", "tut_highlight_invalid", "GDD 14.1"),
            new Case("GDD 14.1/5 tut_highlight_invalid startOn mortar delivery highlights a block without mortar", "tut_highlight_startOn_flag_mismatch", "tut_highlight_invalid", "GDD 14.1"),
            new Case("GDD 14 tut_highlight_invalid debris index out of range", "tut_highlight_debris_out_of_range", "tut_highlight_invalid", "GDD 14.1"),
            new Case("GDD 14 K-49 tut_highlight_invalid cell:7,0 is off a 6-column board", "tut_highlight_cell_off_board", "tut_highlight_invalid", "GDD 14.1"),
            new Case("GDD 14.1 tut_done_invalid obstacleHit crate without crate", "tut_done_obstaclehit_without_crate", "tut_done_invalid", "GDD 14.1"),
            new Case("GDD 14.1 tut_done_invalid steered without low gravity", "tut_done_steered_without_low_gravity", "tut_done_invalid", "GDD 14.1"),
            new Case("GDD 14.1 tut_done_invalid startOn deliveryDone flag without such block", "tut_done_startOn_flag_without_block", "tut_done_invalid", "GDD 14.1"),
            new Case("GDD 14.1/3 tut_done_invalid piece filter names no block", "tut_done_piece_unknown", "tut_done_invalid", "GDD 14.1"),
            new Case("GDD 14.1/3 K-44 tut_done_invalid placementCorrect of an Ağır Yük", "tut_done_piece_cargo_placement", "tut_done_invalid", "GDD 14.1"),
            // others
            new Case("LEVELS 0 difficulty_sawtooth warns for level 10 not hard", "difficulty_sawtooth", "difficulty_sawtooth", "LEVELS 0", Severities.Warn),
            new Case("K-24 elevator_range a ≥ b", "elevator_range", "elevator_range", "K-24"),
            new Case("K-23 carousel_every_missing", "carousel_every_missing", "carousel_every_missing", "K-23"),
            new Case("Y4 wet_moves_missing", "wet_moves_missing", "wet_moves_missing", "Y4"),
        };

        private static string[] Codes(IEnumerable<Issue> issues) => issues.Select(i => i.Code).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToArray();
        private static string[] CodesOf(ValidationResult r) => Codes(r.Issues);
        private static JsonValue Fixture(string group, string name) => TestPaths.LoadFixture(group, name);

        public static IEnumerable<TestCaseData> InvalidCases()
        {
            foreach (var c in Cases) yield return new TestCaseData(c).SetName("K45_invalid_fixture_" + c.File).SetDescription(c.Name);
        }

        [TestCaseSource(nameof(InvalidCases))]
        public void K45_invalid_fixture(Case c)
        {
            var issues = Logic.ValidateLevelJson(Fixture("invalid", c.File), c.Ctx ?? new LogicContext()).Issues;
            var hit = issues.Where(i => i.Code == c.Code).ToArray();
            Assert.Greater(hit.Length, 0, string.Join("\n", issues.Select(i => i.ToString())));
            foreach (var i in hit)
            {
                Assert.AreEqual(c.Rule, i.Rule, i.ToString());
                Assert.AreEqual(c.Severity, i.Severity, i.ToString());
            }
            CollectionAssert.AreEqual(new[] { c.Code }, Codes(issues), string.Join("\n", issues.Select(i => i.ToString())));
        }

        [Test(Description = "K-45 every invalid fixture file has a case")]
        public void K45_every_invalid_fixture_file_has_a_case()
        {
            CollectionAssert.AreEqual(Cases.Select(c => c.File).OrderBy(f => f, StringComparer.Ordinal).ToArray(), TestPaths.FixtureNames("invalid"));
        }

        [Test(Description = "K-45 every Faz 2R validate code of TECH §2R.3 has an invalid fixture (L-35 tut_hand_invalid is levels:solve)")]
        public void K45_every_Faz_2R_validate_code_has_an_invalid_fixture()
        {
            var faz2r = new[]
            {
                "yard_fill_high", "yard_fill_low", "shape_forbidden", "shape_locked", "piece_too_wide", "plan_size", "plan_has_window",
                "elevator_overflow", "cover_prefix_short", "untileable", "debris_misplaced", "too_many_new_mechanics", "teaches_mismatch",
                "size_out_of_range", "board_too_wide", "debris_correct_at_start", "cover_mismatch", "tut_too_many_steps", "tut_blocking",
            };
            var covered = new HashSet<string>(Cases.Select(c => c.Code));
            CollectionAssert.IsEmpty(faz2r.Where(code => !covered.Contains(code)).ToArray());
        }

        [Test(Description = "K-45 every issue carries code, rule, check and path")]
        public void K45_every_issue_carries_code_rule_check_and_path()
        {
            var codeFormat = new System.Text.RegularExpressions.Regex("^[a-z_]+$");
            foreach (var c in Cases)
            {
                foreach (var i in Logic.ValidateLevelJson(Fixture("invalid", c.File), c.Ctx ?? new LogicContext()).Issues)
                {
                    CollectionAssert.Contains(Logic.CheckIds, i.Check);
                    Assert.IsTrue(codeFormat.IsMatch(i.Code), i.Code);
                    Assert.Greater(i.Path.Length, 0);
                    Assert.Greater(i.Message.Length, 0);
                }
            }
        }

        // --- valid fixtures --------------------------------------------------------------------------------------------------

        private static readonly (string name, string[] history)[] Valid =
        {
            ("base", new string[0]),
            ("cargo_level8", new[] { "W1", "S1", "W2" }),
            ("debris", new[] { "W1", "S1", "W2", "Y5", "W3", "Y1", "S9" }),
            ("segments_truck", new[] { "W1" }),
            ("paint_gate_totals", new[] { "W1", "S1", "W2", "Y5", "W3", "S4" }),
            ("screw_under_block", new[] { "W1", "S1", "W2", "Y5", "W3" }),
            ("wide_site_s9", new[] { "W1", "S1", "W2", "Y5", "W3" }),
            ("startOn_first_mortar_batch", new[] { "W1", "S1", "W2", "Y5", "W3" }),
        };

        public static IEnumerable<TestCaseData> ValidCases()
        {
            foreach (var v in Valid) yield return new TestCaseData(v.name, v.history).SetName("K45_valid_fixture_" + v.name + "_passes_every_validate_check");
        }

        [TestCaseSource(nameof(ValidCases))]
        public void K45_valid_fixture_passes_every_validate_check(string name, string[] history)
        {
            var r = Logic.ValidateLevelJson(Fixture("valid", name), new LogicContext(previousMechanics: Prev(history)));
            Assert.IsNotNull(r.Level);
            CollectionAssert.IsEmpty(r.Issues, string.Join("\n", r.Issues.Select(i => i.ToString())));
        }

        [Test(Description = "K-45/1 id_mismatch when the file name number differs")]
        public void K45_1_id_mismatch_when_the_file_name_number_differs()
        {
            var r = Logic.ValidateLevelJson(Fixture("valid", "base"), new LogicContext(fileId: 5));
            CollectionAssert.AreEqual(new[] { "id_mismatch" }, CodesOf(r));
            Assert.AreEqual("K-45/1", r.Issues[0].Rule);
        }

        [Test(Description = "K-45/1 id_mismatch when id is outside 1–50")]
        public void K45_1_id_mismatch_when_id_is_outside_1_50()
        {
            var json = Fixture("valid", "base").Set("id", JsonValue.Of(51)).Set("chapter", JsonValue.Of(5));
            CollectionAssert.Contains(Logic.ValidateLevelJson(json).Issues.Select(i => i.Code).ToArray(), "id_mismatch");
        }

        [Test(Description = "K-45/1 seed is optional (compile default, no issue)")]
        public void K45_1_seed_is_optional()
        {
            var json = Fixture("valid", "base").Remove("seed");
            CollectionAssert.IsEmpty(Logic.ValidateLevelJson(json).Issues);
        }

        [Test(Description = "K-45/1 targets bands need low ≤ high (schema_invalid on the band)")]
        public void K45_1_targets_bands_need_low_le_high()
        {
            var json = Fixture("valid", "base").Set("targets", J.O("minShifts", new[] { 3, 2 }));
            var issues = Logic.ValidateLevelJson(json).Issues;
            CollectionAssert.AreEqual(new[] { "schema_invalid|targets.minShifts" }, issues.Select(i => i.Code + "|" + i.Path).ToArray());
        }

        // --- logic details --------------------------------------------------------------------------------------------------

        /// <summary>A sized builder level (default site 2 × 6, yard 4 × 4) for single checks.</summary>
        private static LevelData Sized(LevelSpec spec)
        {
            spec.Yard ??= new SizeSpec(4, 4);
            spec.Site ??= new SizeSpec(2, 6);
            if (spec.Plan == null && spec.Plans == null) spec.Plan = new[] { "WW", "YY", "GG", "GG", "YW", "YW" };
            return Builders.Level(spec);
        }

        private static string[] Only(LevelData lvl, params string[] checks) => Logic.CheckLevel(lvl, new LogicContext(only: checks)).Select(i => i.Code).ToArray();

        private static P[] Cells(int n)
        {
            var o = new P[n];
            for (int i = 0; i < n; i++) o[i] = Pc("B1_0", "W", i % 4, i / 4);
            return o;
        }

        [Test(Description = "K-02 K-45/2 the yard band is 2 ≤ E ≤ ⌊0,4·C⌋, inclusive (C = 16: E 2…6 pass, 1 high, 7 low)")]
        public void K02_K45_2_the_yard_band_is_inclusive()
        {
            string[] Run(int filled) => Only(Sized(new LevelSpec { Pieces = Cells(filled) }), "L-03");
            CollectionAssert.AreEqual(new[] { "yard_fill_low" }, Run(9)); // E = 7
            CollectionAssert.IsEmpty(Run(10)); // E = 6 = ⌊6,4⌋ (GDD K-02 example Bölüm 3)
            CollectionAssert.IsEmpty(Run(14)); // E = 2
            CollectionAssert.AreEqual(new[] { "yard_fill_high" }, Run(15)); // E = 1
        }

        [Test(Description = "K-02 crates and cement bags fill yard cells, Ağır Yük too; screws and keys do not")]
        public void K02_crates_and_cement_bags_fill_yard_cells_Agir_Yuk_too_screws_and_keys_do_not()
        {
            var pieces = Cells(8);
            LevelData Lvl(JsonValue[] obstacles) => Sized(new LevelSpec { Pieces = pieces, Obstacles = obstacles });
            CollectionAssert.AreEqual(new[] { "yard_fill_low" }, Only(Lvl(new JsonValue[0]), "L-03")); // E = 8
            CollectionAssert.IsEmpty(Only(Lvl(new[] { Crate(0, 2, 1), Bag(1, 2) }), "L-03"));
            CollectionAssert.AreEqual(new[] { "yard_fill_low" }, Only(Lvl(new[] { Screw(0, 0), KeyItem(1, 0, "k") }), "L-03"));
            var cargo = Builders.Level(new LevelSpec
            {
                Id = 8, Yard = new SizeSpec(6, 4), Site = new SizeSpec(2, 6), Plan = WW(6),
                Pieces = Ps(Pc("Q9_0", "W", 0, 0), Pc("O4_0", "W", 3, 0), Pc("O4_0", "W", 3, 2), Pc("D2_0", "W", 5, 0), Pc("D2_0", "W", 5, 2)),
            });
            CollectionAssert.IsEmpty(Only(cargo, "L-03")); // F = 9 + 12, E = 3
        }

        [Test(Description = "K-49 K-45/2 size ranges: Wy 3–6, Hy 4–8, Hs 4–8, Ws 2–3 in story chapters 1–3 and 2–4 in 4–5")]
        public void K49_K45_2_size_ranges()
        {
            string[] Sizes(int id, int wy, int hy, int ws, int hs) =>
                Logic.CheckLevel(Builders.Level(new LevelSpec { Id = id, Yard = new SizeSpec(wy, hy), Site = new SizeSpec(ws, hs), Pieces = Ps(Pc("B1_0", "W", 0, 0)) }),
                    new LogicContext(only: new[] { "L-28" })).Select(i => i.Code + "@" + i.Path).ToArray();
            CollectionAssert.IsEmpty(Sizes(1, 4, 4, 2, 5));
            CollectionAssert.IsEmpty(Sizes(1, 3, 8, 3, 4));
            CollectionAssert.AreEqual(new[] { "size_out_of_range@yard.cols" }, Sizes(1, 2, 4, 2, 5));
            CollectionAssert.AreEqual(new[] { "size_out_of_range@yard.rows" }, Sizes(1, 4, 3, 2, 5));
            CollectionAssert.AreEqual(new[] { "size_out_of_range@site.rows" }, Sizes(1, 4, 4, 2, 3));
            CollectionAssert.AreEqual(new[] { "size_out_of_range@site.cols" }, Sizes(30, 4, 4, 4, 5));
            CollectionAssert.IsEmpty(Sizes(31, 4, 4, 4, 5));
            CollectionAssert.AreEqual(new[] { "board_too_wide@site.cols" }, Sizes(41, 5, 4, 4, 5));
            // K-49 example: Wy = 6, Ws = 3 → board_too_wide
            CollectionAssert.AreEqual(new[] { "board_too_wide@site.cols" }, Sizes(12, 6, 5, 3, 5));
        }

        [Test(Description = "K-49 a level outside the 8 × 10 frame stops after L-28: no geometry, no other check")]
        public void K49_a_level_outside_the_8x10_frame_stops_after_L28()
        {
            var wide = Fixture("valid", "base");
            wide["yard"]!.Set("cols", JsonValue.Of(7));
            wide.Set("site", J.O("cols", 3, "rows", 6));
            CollectionAssert.AreEqual(new[] { "board_too_wide", "size_out_of_range" }, CodesOf(Logic.ValidateLevelJson(wide)));
            var tall = Fixture("valid", "base").Set("site", J.O("cols", 2, "rows", 9));
            CollectionAssert.AreEqual(new[] { "size_out_of_range@site.rows" }, Logic.ValidateLevelJson(tall).Issues.Select(i => i.Code + "@" + i.Path).ToArray());
        }

        [Test(Description = "K-45/4 K-15 GDD example [\"YY\",\"W.\"] on Hs 5: plan_has_window and plan_size; no cover verdict on a broken plan")]
        public void K45_4_K15_GDD_example_YY_W_dot_on_Hs_5()
        {
            var bad = Fixture("valid", "base").Set("site", J.O("cols", 2, "rows", 5));
            bad["build"]!.Set("segments", J.A(J.O("name", J.O("tr", "A", "en", "A"), "rows", new[] { "YY", "W." })));
            CollectionAssert.AreEqual(new[] { "plan_has_window", "plan_size" }, CodesOf(Logic.ValidateLevelJson(bad)));
        }

        [Test(Description = "K-47/1 Ağır Yük is no supply and has no colour: colour set and cover ignore a colour written on it")]
        public void K47_1_Agir_Yuk_is_no_supply_and_has_no_colour()
        {
            var json = Fixture("valid", "cargo_level8");
            json["yard"]!["batches"]![0]["pieces"]![5].Set("color", JsonValue.Of("R"));
            var r = Logic.ValidateLevelJson(json);
            CollectionAssert.IsEmpty(r.Issues);
            Assert.IsNotNull(r.Level);
            CollectionAssert.AreEqual(new[] { "W", "Y", "G" }, Logic.LevelColorSet(r.Level!));
        }

        [Test(Description = "K-47/1 W6: only the totals have to match (the paint gate turns Y into P); one cell more is cover_mismatch")]
        public void K47_1_W6_only_the_totals_have_to_match()
        {
            CollectionAssert.IsEmpty(Logic.ValidateLevelJson(Fixture("valid", "paint_gate_totals")).Issues);
            var json = Fixture("valid", "paint_gate_totals");
            json["yard"]!["batches"]![0]["pieces"]!.Add(J.O("shape", "B1_0", "color", "P", "x", 2, "y", 2));
            var issues = Logic.ValidateLevelJson(json).Issues;
            CollectionAssert.AreEqual(new[] { "cover_mismatch" }, issues.Select(i => i.Code).ToArray());
            StringAssert.Contains("13 ≠ plan cells 12", issues[0].Message);
        }

        [Test(Description = "K-47/2 the last prefix is the whole level: a total shortage is cover_mismatch, not cover_prefix_short")]
        public void K47_2_the_last_prefix_is_the_whole_level()
        {
            var json = Fixture("valid", "segments_truck");
            var batches = json["yard"]!["batches"]!;
            var replaced = JsonValue.Array();
            replaced.Add(batches[0]);
            replaced.Add(J.O("forSegment", 1, "pieces", J.A(J.O("shape", "D2_0", "color", "W", "x", 0, "y", 4))));
            json["yard"]!.Set("batches", replaced);
            CollectionAssert.AreEqual(new[] { "cover_mismatch" }, CodesOf(Logic.ValidateLevelJson(json)));
        }

        private static JsonValue AsCarousel(JsonValue json)
        {
            json["build"]!.Set("mode", JsonValue.Of("carousel")).Set("carouselEvery", JsonValue.Of(3));
            return json;
        }

        [Test(Description = "K-47/2 cover_prefix_short is skipped in carousel mode and with a W6 gate")]
        public void K47_2_cover_prefix_short_is_skipped_in_carousel_mode()
        {
            CollectionAssert.IsEmpty(CodesOf(Logic.ValidateLevelJson(AsCarousel(Fixture("invalid", "cover_prefix_short")))));
        }

        [Test(Description = "K-30 D3a respects batch availability: a truck block never fills an earlier segment (carousel ignores it)")]
        public void K30_D3a_respects_batch_availability()
        {
            var parsed = LevelJson.ParseOrThrow(Fixture("invalid", "cover_prefix_short"));
            Assert.AreEqual(TileResults.Dead, Logic.TileLevel(parsed, Plan.BuildPlans(parsed).Plans));
            var carousel = LevelJson.ParseOrThrow(AsCarousel(Fixture("invalid", "cover_prefix_short")));
            Assert.AreEqual(TileResults.Ok, Logic.TileLevel(carousel, Plan.BuildPlans(carousel).Plans));
        }

        [Test(Description = "K-30 D3a paint joker: a block that fits a paint gate may take its colour")]
        public void K30_D3a_paint_joker()
        {
            var parsed = LevelJson.ParseOrThrow(Fixture("valid", "paint_gate_totals"));
            Assert.AreEqual(TileResults.Ok, Logic.TileLevel(parsed, Plan.BuildPlans(parsed).Plans));
            var noGateJson = Fixture("valid", "paint_gate_totals");
            noGateJson["wall"]!.Set("gaps", JsonValue.Array());
            var noGate = LevelJson.ParseOrThrow(noGateJson);
            Assert.AreEqual(TileResults.Dead, Logic.TileLevel(noGate, Plan.BuildPlans(noGate).Plans));
            // the joker needs the box height to fit the gate: a 1-row gate paints the 1-high D2_90 only (still enough here)
            var lowJson = Fixture("valid", "paint_gate_totals");
            lowJson["wall"]!.Set("gaps", J.A(J.O("type", "paint", "y", 0, "size", 1, "color", "P")));
            var low = LevelJson.ParseOrThrow(lowJson);
            Assert.AreEqual(TileResults.Ok, Logic.TileLevel(low, Plan.BuildPlans(low).Plans));
        }

        [Test(Description = "K-30 D3a budget exhaustion is unknown, not dead, and L-11 then reports nothing (K-45/8)")]
        public void K30_D3a_budget_exhaustion_is_unknown_not_dead()
        {
            var parsed = LevelJson.ParseOrThrow(Fixture("invalid", "untileable"));
            var plans = Plan.BuildPlans(parsed).Plans;
            Assert.AreEqual(TileResults.Dead, Logic.TileLevel(parsed, plans));
            Assert.AreEqual(TileResults.Unknown, Logic.TileLevel(parsed, plans, null, 0));
            CollectionAssert.IsEmpty(Logic.CheckLevel(parsed, new LogicContext(only: new[] { "L-11" }, tileBudget: 0)));
            Assert.AreEqual(20_000, Logic.TileBudget);
        }

        [Test(Description = "K-30 D3a solves the LEVELS levels 1–10 drafts within 200 expansions each (TECH §2R.4)")]
        public void K30_D3a_solves_levels_1_10_within_200_expansions()
        {
            for (int n = 1; n <= 10; n++)
            {
                var parsed = LevelJson.ParseOrThrow(TestPaths.LoadLevelJson(n));
                Assert.AreEqual(TileResults.Ok, Logic.TileLevel(parsed, Plan.BuildPlans(parsed).Plans, null, 200), "level " + n);
            }
        }

        [Test(Description = "K-30 D3a: every shape has contiguous columns (the profile update relies on it)")]
        public void K30_D3a_every_shape_has_contiguous_columns()
        {
            foreach (var s in Shapes.All)
            {
                for (int c = 0; c < s.W; c++)
                {
                    int rows = s.Cells.Count(p => p.X == c);
                    Assert.AreEqual(s.ColTop[c] - s.ColBottom[c] + 1, rows, s.Id);
                }
            }
        }

        [Test(Description = "K-44 isCargo (level modules) equals movement.ts isCargoShape on every shape: I5 and Q9 only")]
        public void K44_IsCargo_equals_IsCargoShape_on_every_shape()
        {
            foreach (var s in Shapes.All) Assert.AreEqual(Movement.IsCargoShape(s), Mechanics.IsCargo(s), s.Id);
            CollectionAssert.AreEqual(new[] { "I5", "I5", "I5", "I5", "Q9", "Q9", "Q9", "Q9" }, Shapes.All.Where(Mechanics.IsCargo).Select(s => s.Kind).ToArray());
        }

        [Test(Description = "K-44 K-05 K-12 canReachSite: over the wall by (H + 2) − height, or a gap; never Ağır Yük; a w ≥ 3 block is material")]
        public void K44_K05_K12_CanReachSite()
        {
            LevelData Lvl(int height, params JsonValue[] gaps) => Sized(new LevelSpec { Id = 12, Wall = Wall(height, gaps), Pieces = Ps(Pc("B1_0", "W", 0, 0)) });
            // H = 6: height 6 leaves 2 open rows
            Assert.IsTrue(Logic.CanReachSite(Shapes.ShapeById("D2_0"), Lvl(6)));
            Assert.IsFalse(Logic.CanReachSite(Shapes.ShapeById("I3_0"), Lvl(6)));
            Assert.IsTrue(Logic.CanReachSite(Shapes.ShapeById("I3_0"), Lvl(5)));
            Assert.IsTrue(Logic.CanReachSite(Shapes.ShapeById("I3_0"), Lvl(6, Gap(0, 3))));
            Assert.IsTrue(Logic.CanReachSite(Shapes.ShapeById("I3_90"), Lvl(6)));
            Assert.IsFalse(Logic.CanReachSite(Shapes.ShapeById("Q9_0"), Lvl(0)));
            Assert.IsFalse(Logic.CanReachSite(Shapes.ShapeById("I5_0"), Lvl(0)));
        }

        [Test(Description = "K-45/7 S4 debris_correct_at_start follows K-16 with K-34 support: a W debris on a W cell above empty cells is wrong")]
        public void K45_7_S4_debris_correct_at_start_follows_K16_with_K34_support()
        {
            string[] Debris(params D[] spec) => Only(Sized(new LevelSpec { Id = 17, Chapter = 2, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Debris = spec }), "L-29");
            CollectionAssert.IsEmpty(Debris(new D("B1_0", "W", 5, 5))); // top W cell, nothing below: no support
            CollectionAssert.AreEqual(new[] { "debris_correct_at_start" }, Debris(new D("D2_0", "W", 5, 0)));
            CollectionAssert.IsEmpty(Debris(new D("D2_0", "Y", 5, 0))); // wrong colour
            // the lower debris is correct; the upper one stays wrong: rows 2–3 under it are not filled (K-34)
            CollectionAssert.AreEqual(new[] { "debris_correct_at_start" }, Debris(new D("D2_0", "Y", 4, 0), new D("D2_90", "Y", 4, 4)));
            var stacked = Logic.CheckLevel(
                Sized(new LevelSpec
                {
                    Id = 17, Chapter = 2, Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                    // stacked: (5,1) is supported by the correct debris (5,0) below it
                    Debris = new[] { new D("D2_0", "Y", 4, 0), new D("B1_0", "W", 5, 0), new D("B1_0", "W", 5, 1) },
                }),
                new LogicContext(only: new[] { "L-29" }));
            CollectionAssert.AreEqual(new[] { "build.debris[0]", "build.debris[1]", "build.debris[2]" }, stacked.Select(i => i.Path).ToArray());
        }

        [Test(Description = "K-45 runtime subset: L-02, L-04, L-05, L-08, L-09, L-24, L-25, L-26, L-28, L-36 (WP-M put L-05 back after the Faz 2R levels; K-55 adds L-36)")]
        public void K45_runtime_subset()
        {
            CollectionAssert.AreEqual(new[] { "L-02", "L-04", "L-05", "L-08", "L-09", "L-24", "L-25", "L-26", "L-28", "L-36" }, Logic.RuntimeChecks);
            string[] Run(string file) => Logic.ValidateLevelJson(Fixture("invalid", file), new LogicContext(only: Logic.RuntimeChecks)).Issues.Select(i => i.Code).ToArray();
            CollectionAssert.IsEmpty(Run("yard_fill_low"));
            CollectionAssert.AreEqual(new[] { "plan_size" }, Run("plan_size_rows"));
            CollectionAssert.AreEqual(new[] { "plan_has_window" }, Run("plan_has_window"));
            Assert.IsFalse(Compiler.LoadLevel(Fixture("invalid", "plan_size_rows")).Ok);
            CollectionAssert.AreEqual(new[] { "gap_touches_top" }, Run("gap_touches_top"));
            CollectionAssert.AreEqual(new[] { "board_too_wide" }, Run("board_too_wide"));
            CollectionAssert.AreEqual(new[] { "piece_too_wide" }, Run("piece_too_wide"));
            Assert.IsFalse(Compiler.LoadLevel(Fixture("invalid", "board_too_wide")).Ok);
            Assert.IsTrue(Compiler.LoadLevel(Fixture("valid", "base")).Ok);
        }

        [Test(Description = "K-45/1 crate without hp is schema_invalid")]
        public void K45_1_crate_without_hp_is_schema_invalid()
        {
            var lvl = Sized(new LevelSpec { Id = 11, Pieces = Ps(Pc("B1_0", "W", 0, 0)), Obstacles = new[] { Crate(1, 0) } });
            CollectionAssert.AreEqual(new[] { "schema_invalid" }, Only(lvl, "L-14"));
        }

        // --- GDD 14.1 L-17 tutorial checks (Faz 2R) ------------------------------------------------------------------------------

        private static JsonValue Step(int step, string[] highlight, string textKey, JsonValue done, JsonValue? startOn = null, JsonValue? hand = null, string? mode = null)
        {
            var o = J.O("step", step);
            if (mode != null) o.Set("mode", JsonValue.Of(mode));
            o.Set("highlight", J.V(highlight));
            if (hand != null) o.Set("hand", hand);
            o.Set("textKey", JsonValue.Of(textKey));
            if (startOn != null) o.Set("startOn", startOn);
            o.Set("done", done);
            return o;
        }

        [Test(Description = "GDD 14.1 tutorial booster and pre highlights need the unlock level (economy.json)")]
        public void GDD_14_1_tutorial_booster_and_pre_highlights_need_the_unlock_level()
        {
            var lvl = Sized(new LevelSpec
            {
                Id = 9, Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Tutorial = new[]
                {
                    Step(1, new[] { "booster:hammer" }, "tut.l9.a", J.O("event", "tap")),
                    Step(2, new[] { "pre:thermos" }, "tut.l9.b", J.O("event", "tap")),
                },
            });
            var issues = Logic.CheckLevel(lvl, new LogicContext(only: new[] { "L-17" }, boosterUnlock: new Dictionary<string, int> { { "hammer", 8 }, { "thermos", 12 } }));
            CollectionAssert.AreEqual(new[] { "tutorial[1].highlight[0]" }, issues.Select(i => i.Path).ToArray());
            Assert.AreEqual("tut_highlight_invalid", issues[0].Code);
        }

        [Test(Description = "L-17 a drag / hold glove starts on a cell of a highlighted block at its start (LEVELS 5; GDD 14 hand.path (1))")]
        public void L17_a_drag_hold_glove_starts_on_a_cell_of_a_highlighted_block()
        {
            LevelData Glove(int[][] path, string kind = "hold") => Sized(new LevelSpec
            {
                Id = 2, Pieces = Ps(Pc("C3_180", "Y", 0, 2), Pc("B1_0", "G", 2, 0)),
                Tutorial = new[]
                {
                    Step(1, new[] { "piece:0", "build" }, "tut.m.shadow", J.O("event", "placementCorrect", "count", 1), null,
                        J.O("kind", kind, "path", J.V(path.Select(p => (object)J.V(p)).ToArray()))),
                },
            });
            // C3_180 anchored (0,2) covers (1,2), (0,3), (1,3): (0,2) is the anchor, not a block cell (LEVELS Bölüm 2 adım 2)
            var bad = Logic.CheckLevel(Glove(new[] { new[] { 0, 2 }, new[] { 0, 4 }, new[] { 5, 4 } }), new LogicContext(only: new[] { "L-17" }));
            CollectionAssert.AreEqual(new[] { "tut_highlight_invalid|tutorial[0].hand.path[0]" }, bad.Select(i => i.Code + "|" + i.Path).ToArray());
            CollectionAssert.IsEmpty(Logic.CheckLevel(Glove(new[] { new[] { 1, 3 }, new[] { 1, 5 }, new[] { 5, 5 } }), new LogicContext(only: new[] { "L-17" })));
            CollectionAssert.IsEmpty(Logic.CheckLevel(Glove(new[] { new[] { 0, 2 } }, "tap"), new LogicContext(only: new[] { "L-17" }))); // a tap may press any cell
        }

        [Test(Description = "L-17 tut_hold_done: a holdOverBuild done / startOn is a warning (LEVELS 5)")]
        public void L17_tut_hold_done_is_a_warning()
        {
            var lvl = Sized(new LevelSpec
            {
                Id = 2, Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Tutorial = new[]
                {
                    Step(1, new[] { "build" }, "tut.m.shadow", J.O("event", "holdOverBuild", "count", 1, "minMs", 500)),
                    Step(2, new[] { "build" }, "tut.m.free", J.O("event", "placementCorrect", "count", 1), J.O("event", "holdOverBuild", "count", 1, "minMs", 500)),
                },
            });
            CollectionAssert.AreEqual(
                new[] { "tut_hold_done|tutorial[0].done|warn", "tut_hold_done|tutorial[1].startOn|warn" },
                Logic.CheckLevel(lvl, new LogicContext(only: new[] { "L-17" })).Select(i => i.Code + "|" + i.Path + "|" + i.Severity).ToArray());
        }

        [Test(Description = "GDD 14.1 tutorial steps are numbered 1, 2, … and done.at regions follow the level geometry (K-49)")]
        public void GDD_14_1_tutorial_steps_are_numbered_and_done_at_regions_follow_the_geometry()
        {
            string[] At_(string ev, int x, int y, int step = 1) => Only(Sized(new LevelSpec
            {
                Id = 7, Pieces = Ps(Pc("B1_0", "W", 0, 0)),
                Tutorial = new[] { Step(step, new[] { "build" }, "tut.m.free", J.O("event", ev, "at", new[] { x, y })) },
            }), "L-17");
            CollectionAssert.IsEmpty(At_("yardMove", 3, 3));
            CollectionAssert.AreEqual(new[] { "tut_done_invalid" }, At_("yardMove", 4, 0)); // x = Wy is the site
            CollectionAssert.AreEqual(new[] { "tut_done_invalid" }, At_("yardMove", 0, 4)); // y = Hy is yard air
            CollectionAssert.IsEmpty(At_("placementCorrect", 4, 5));
            CollectionAssert.AreEqual(new[] { "tut_done_invalid" }, At_("placementCorrect", 3, 0));
            CollectionAssert.AreEqual(new[] { "tut_done_invalid" }, At_("placementCorrect", 4, 6)); // y = H
            CollectionAssert.AreEqual(new[] { "tut_done_invalid" }, At_("yardMove", 3, 3, 2)); // numbered 2 as the first step
        }

        [Test(Description = "GDD 14.1/5 a truck block may be highlighted once its batch is on the board: startOn deliveryDone, or after its segmentDone (LEVELS Bölüm 5 adım 2)")]
        public void GDD_14_1_5_a_truck_block_may_be_highlighted_once_its_batch_is_on_the_board()
        {
            CollectionAssert.IsEmpty(Logic.ValidateLevelJson(Fixture("valid", "segments_truck"), new LogicContext(only: new[] { "L-17" })).Issues);
            string[] Steps(params JsonValue[] tutorial) =>
                Logic.ValidateLevelJson(Fixture("valid", "segments_truck").Set("tutorial", J.V(tutorial)), new LogicContext(only: new[] { "L-17" })).Issues.Select(i => i.Code).ToArray();
            // the step itself starts on the segmentDone (Bölüm 7/10 style)
            CollectionAssert.IsEmpty(Steps(Step(1, new[] { "piece:k1_0" }, "tut.m.truck", J.O("event", "tap"), J.O("event", "segmentDone"))));
            // an earlier step's done that is not a segmentDone does not deliver the batch
            CollectionAssert.AreEqual(new[] { "tut_highlight_invalid" }, Steps(
                Step(1, new[] { "panorama" }, "tut.m.segments", J.O("event", "placementCorrect")),
                Step(2, new[] { "piece:k1_0" }, "tut.m.truck", J.O("event", "tap"))));
        }

        [Test(Description = "K-53/1 K-45/10 two steps are allowed, the third is tut_too_many_steps")]
        public void K53_1_K45_10_two_steps_are_allowed_the_third_is_tut_too_many_steps()
        {
            var json = Fixture("valid", "base");
            Assert.AreEqual(2, json["tutorial"]!.Count);
            CollectionAssert.IsEmpty(Logic.ValidateLevelJson(json, new LogicContext(only: new[] { "L-31" })).Issues);
            var four = Fixture("valid", "base");
            var steps = four["tutorial"]!;
            var doubled = JsonValue.Array();
            foreach (var st in steps.Items) doubled.Add(st.Clone());
            foreach (var st in steps.Items) doubled.Add(st.Clone());
            four.Set("tutorial", doubled);
            CollectionAssert.AreEqual(new[] { "4 steps; a level has at most 2 (K-53/1)" },
                Logic.ValidateLevelJson(four, new LogicContext(only: new[] { "L-31" })).Issues.Select(i => i.Message).ToArray());
        }

        [Test(Description = "L-17 every level 1–10 glove starts on its highlighted block; none ends a step on holdOverBuild")]
        public void L17_every_level_1_10_glove_starts_on_its_highlighted_block()
        {
            for (int id = 1; id <= 10; id++)
            {
                var data = LevelJson.ParseOrThrow(TestPaths.LoadLevelJson(id));
                var issues = Logic.CheckLevel(data, new LogicContext(only: new[] { "L-17" }));
                CollectionAssert.IsEmpty(issues.Where(i => i.Path.EndsWith("hand.path[0]", StringComparison.Ordinal)).ToArray(), "level " + id);
                CollectionAssert.IsEmpty(issues.Where(i => i.Code == "tut_hold_done").ToArray(), "level " + id);
            }
        }
    }
}
