#nullable enable
// Port of tests/level/schema.test.ts (web repo): the level schema (TECH §8.2, Faz 2R §2R.2; GDD §14, §14.1, K-44, K-45/1,
// K-49, K-53) as the hand-written LevelJson parser implements it. Three TS tests read docs/GDD.md, docs/UX_FLOWS.md and
// docs/LEVELS.md of the web repo (vocabulary ↔ document comparisons); their schema halves are kept here, the document halves
// stay in the web repo (`notVerified`).
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
    public class SchemaTests
    {
        private static bool Ok(JsonValue v) => LevelJson.Parse(v, new List<SchemaIssue>()) != null;
        private static JsonValue Base() => TestPaths.LoadFixture("valid", "base");
        private static readonly P[] B1 = { new P("B1_0", "W", 0, 0) };

        private static JsonValue WithTutorial(params JsonValue[] steps) => Builders.LevelJson(new LevelSpec { Pieces = B1 }).Set("tutorial", J.V(steps));

        private static JsonValue Step(JsonValue done, JsonValue? extra = null)
        {
            var o = J.O("step", 1, "highlight", new[] { "build" }, "textKey", "tut.m.lift", "done", done);
            if (extra != null) foreach (var kv in extra.Members) o.Set(kv.Key, kv.Value);
            return o;
        }

        /// <summary>`TutCond.safeParse(cond).success`: a step with that `done` parses.</summary>
        private static bool CondOk(JsonValue cond) => Ok(WithTutorial(Step(cond)));

        [Test(Description = "K-45/1 base fixture passes and unknown keys are rejected (strictObject)")]
        public void K45_1_base_fixture_passes_and_unknown_keys_are_rejected()
        {
            Assert.IsTrue(Ok(Base()));
            Assert.IsFalse(Ok(Base().Set("elevatorRange", J.V(new[] { 0, 1 }))));
        }

        [Test(Description = "K-45/1 defaults: shutter phase 0, slider dir 1; schemaVersion and seed optional")]
        public void K45_1_defaults_shutter_phase_0_slider_dir_1_schemaVersion_and_seed_optional()
        {
            var json = Builders.LevelJson(new LevelSpec
            {
                Id = 16,
                Wall = Wall(8, J.O("type", "shutter", "y", 0, "size", 1, "period", 2), J.O("type", "slider", "y", 3, "size", 1, "range", new[] { 2, 4 })),
                Pieces = B1,
            });
            var parsed = LevelJson.ParseOrThrow(json);
            Assert.AreEqual("shutter", parsed.Wall.Gaps[0].Type);
            Assert.AreEqual(0, parsed.Wall.Gaps[0].Phase);
            Assert.AreEqual("slider", parsed.Wall.Gaps[1].Type);
            Assert.AreEqual(1, parsed.Wall.Gaps[1].Dir);
            Assert.IsTrue(Ok(json.Clone().Remove("schemaVersion")));
            Assert.IsNull(parsed.Seed);
        }

        [Test(Description = "K-49 K-45/1 yard.cols/rows and site { cols, rows } are optional; a site needs both fields")]
        public void K49_K45_1_yard_and_site_sizes_are_optional_a_site_needs_both_fields()
        {
            var parsed = LevelJson.ParseOrThrow(Base());
            CollectionAssert.AreEqual(new int?[] { 4, 4, 2, 6 }, new[] { parsed.Yard.Cols, parsed.Yard.Rows, parsed.Site?.Cols, parsed.Site?.Rows });
            var json = Builders.LevelJson(new LevelSpec { Pieces = B1 });
            var def = LevelJson.ParseOrThrow(json);
            Assert.IsNull(def.Yard.Cols);
            Assert.IsNull(def.Yard.Rows);
            Assert.IsNull(def.Site);
            Assert.IsFalse(Ok(json.Clone().Set("site", J.O("cols", 2))));
            Assert.IsFalse(Ok(json.Clone().Set("site", J.O("cols", 2, "rows", 5, "depth", 1))));
            // The K-49 ranges are logic checks (size_out_of_range, board_too_wide): the schema only bounds the numbers.
            var wide = json.Clone().Set("site", J.O("cols", 4, "rows", 8));
            wide["yard"]!.Set("cols", JsonValue.Of(6));
            Assert.IsTrue(Ok(wide));
            Assert.IsFalse(Ok(json.Clone().Set("site", J.O("cols", 0, "rows", 8))));
        }

        [Test(Description = "K-24 elevator start and dir are required (GDD §14)")]
        public void K24_elevator_start_and_dir_are_required()
        {
            JsonValue WithElevator(JsonValue elevator)
            {
                var json = Builders.LevelJson(new LevelSpec { Pieces = B1 });
                json["build"]!.Set("elevator", elevator);
                return json;
            }
            Assert.IsTrue(Ok(WithElevator(J.O("range", new[] { 0, 2 }, "start", 0, "dir", 1))));
            Assert.IsFalse(Ok(WithElevator(J.O("range", new[] { 0, 2 }, "dir", 1))));
            Assert.IsFalse(Ok(WithElevator(J.O("range", new[] { 0, 2 }, "start", 0))));
        }

        [Test(Description = "K-45/1 ranges follow GDD/OBSTACLES (segments 1–5, carouselEvery 2–6, wetMoves 1–5, period 1–4)")]
        public void K45_1_ranges_follow_GDD_OBSTACLES()
        {
            var json = Builders.LevelJson(new LevelSpec { Pieces = B1 });
            var every = json.Clone();
            every["build"]!.Set("carouselEvery", JsonValue.Of(7));
            Assert.IsFalse(Ok(every));
            var noSeg = json.Clone();
            noSeg["build"]!.Set("segments", JsonValue.Array());
            Assert.IsFalse(Ok(noSeg));
            Assert.IsFalse(Ok(Builders.LevelJson(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "wet" }, 6)) })));
            Assert.IsTrue(Ok(Builders.LevelJson(new LevelSpec { Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "wet" }, 5)) })));
            Assert.IsFalse(Ok(Builders.LevelJson(new LevelSpec { Wall = Wall(4, J.O("type", "shutter", "y", 0, "size", 1, "period", 5)) })));
        }

        [Test(Description = "K-44 K-56 shape ids: all 56 accepted by the schema, others rejected")]
        public void K44_shape_ids_all_56_accepted_others_rejected()
        {
            Assert.IsTrue(Ok(Builders.LevelJson(new LevelSpec { Pieces = Ps(Pc("O4_270", "W", 0, 0)) })));
            foreach (var id in Shapes.Ids) Assert.IsTrue(LevelSchemaPatterns.ShapeId.IsMatch(id), id);
            var bad = Builders.LevelJson(new LevelSpec { Pieces = B1 });
            bad["yard"]!["batches"]![0]["pieces"]![0].Set("shape", JsonValue.Of("B1_45"));
            Assert.IsFalse(Ok(bad));
        }

        [Test(Description = "K-44 Ağır Yük (I5/Q9) may omit color, a material block may not; a missing cargo colour parses to the placeholder")]
        public void K44_Agir_Yuk_may_omit_color_a_material_block_may_not()
        {
            var cargo = TestPaths.LoadFixture("valid", "cargo_level8");
            var q9 = cargo["yard"]!["batches"]![0]["pieces"]![5];
            Assert.AreEqual("Q9_0", q9["shape"]!.AsString);
            Assert.IsFalse(q9.Has("color"));
            var parsed = LevelJson.ParseOrThrow(cargo);
            Assert.AreEqual(LevelSchemaPatterns.CargoPlaceholderColor, parsed.Yard.Batches[0].Pieces[5].Color);
            // A written cargo colour is kept (and ignored by every rule, K-44).
            Assert.AreEqual("R", LevelJson.ParseOrThrow(Builders.LevelJson(new LevelSpec { Id = 8, Pieces = Ps(Pc("I5_0", "R", 0, 0)) })).Yard.Batches[0].Pieces[0].Color);
            var issues = new List<SchemaIssue>();
            Assert.IsNull(LevelJson.Parse(TestPaths.LoadFixture("invalid", "schema_invalid_color_missing"), issues));
            CollectionAssert.AreEqual(new[] { "yard.batches[0].pieces[0].color" }, issues.Select(i => i.PathString).ToArray());
        }

        [Test(Description = "K-45/10 teaches accepts the 27 mechanic ids of the Faz 2R table (S9 in, S2 only as the legacy id)")]
        public void K45_10_teaches_accepts_the_27_mechanic_ids()
        {
            Assert.AreEqual(27, MechanicIds.All.Length);
            CollectionAssert.Contains(MechanicIds.All, "S9");
            CollectionAssert.DoesNotContain(MechanicIds.All, "S2");
            CollectionAssert.AreEqual(new[] { "S2" }, MechanicIds.Legacy);
            Assert.IsTrue(Ok(Builders.LevelJson(new LevelSpec { Teaches = "S7-R", Pieces = B1 })));
            Assert.IsTrue(Ok(Builders.LevelJson(new LevelSpec { Teaches = "S9", Pieces = B1 })));
            Assert.IsFalse(Ok(Builders.LevelJson(new LevelSpec { Pieces = B1 }).Set("teaches", JsonValue.Of("S7"))));
        }

        [Test(Description = "K-45/1 targets (LEVELS §0, P-2R-4): optional K-50 bands minShifts, firstNeedDepth, choices0, deadRate")]
        public void K45_1_targets_optional_K50_bands()
        {
            var t = LevelJson.ParseOrThrow(Base()).Targets!;
            CollectionAssert.AreEqual(new[] { 1, 1 }, t.MinShifts);
            CollectionAssert.AreEqual(new[] { 1, 1 }, t.FirstNeedDepth);
            Assert.IsNull(t.Choices0);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, t.DeadRate);
            var json = Builders.LevelJson(new LevelSpec { Pieces = B1 });
            Assert.IsTrue(Ok(json.Clone().Set("targets", J.O("choices0", new[] { 3, 12 }, "deadRate", J.A(0, 0.2)))));
            Assert.IsTrue(Ok(json.Clone().Set("targets", JsonValue.Object())));
            Assert.IsFalse(Ok(json.Clone().Set("targets", J.O("deadRate", J.A(0, 1.5)))));
            Assert.IsFalse(Ok(json.Clone().Set("targets", J.O("minShifts", new[] { 1 }))));
            Assert.IsFalse(Ok(json.Clone().Set("targets", J.O("yao", J.A(0.6, 1)))));
        }

        // --- GDD 14.1 tutorial schema (Faz 2R, K-53) ------------------------------------------------------------------------------

        [Test(Description = "tutorial done vocabulary: the 16 GDD 14.1 events parse with their minimal filters (the GDD.md comparison stays in the web repo)")]
        public void tutorial_done_vocabulary_16_events_parse()
        {
            Assert.AreEqual(16, TutEvents.All.Length);
            foreach (var ev in TutEvents.All)
            {
                var minimal = ev == "holdOverBuild" ? J.O("event", ev, "minMs", 500)
                    : ev == "obstacleHit" ? J.O("event", ev, "type", "crate")
                    : ev == "itemCollected" ? J.O("event", ev, "type", "screw")
                    : J.O("event", ev);
                Assert.IsTrue(CondOk(minimal), ev);
            }
            Assert.IsFalse(CondOk(J.O("event", "pieceMoved")));
        }

        [Test(Description = "tutorial highlight vocabulary: every UX 13.1 form has a sample accepted by the schema regex (the UX_FLOWS.md comparison stays in the web repo; Faz 2R: blocks, CL-2R-21)")]
        public void tutorial_highlight_vocabulary_samples_match()
        {
            var kinds = LevelSchemaPatterns.HighlightPatterns.Select(p => p.Split(':')[0]).ToArray();
            CollectionAssert.Contains(kinds, "blocks");
            var samples = new[]
            {
                "piece:0", "piece:k1_0", "cell:6,1", "gap:0", "obstacle:0", "debris:0", "booster:hammer", "booster:crane", "booster:brush", "booster:undo",
                "pre:thermos", "pre:trowel", "pre:shutter", "wall", "crane", "build", "front", "panorama", "goals", "blocks", "moves", "truck", "streak", "fan",
            };
            foreach (var sample in samples) Assert.IsTrue(LevelSchemaPatterns.Highlight.IsMatch(sample), sample);
            Assert.IsTrue(LevelSchemaPatterns.Highlight.IsMatch("piece:k1_0"));
            Assert.IsFalse(LevelSchemaPatterns.Highlight.IsMatch("booster:paintBrush"));
            Assert.IsFalse(LevelSchemaPatterns.Highlight.IsMatch("gap"));
        }

        [Test(Description = "K-53/3 done and startOn are events only: timeoutMs is schema_invalid (CL-2R-10)")]
        public void K53_3_done_and_startOn_are_events_only()
        {
            Assert.IsFalse(CondOk(J.O("timeoutMs", 2500)));
            Assert.IsTrue(Ok(WithTutorial(Step(J.O("event", "tap")))));
            Assert.IsFalse(Ok(WithTutorial(Step(J.O("timeoutMs", 2500)))));
            Assert.IsFalse(Ok(WithTutorial(Step(J.O("event", "tap"), J.O("startOn", J.O("timeoutMs", 1000))))));
        }

        [Test(Description = "K-53/2 mode is optional; any string passes the schema (L-31 names a non-soft value tut_blocking)")]
        public void K53_2_mode_is_optional_any_string_passes_the_schema()
        {
            Assert.IsTrue(Ok(WithTutorial(Step(J.O("event", "tap")))));
            Assert.IsTrue(Ok(WithTutorial(Step(J.O("event", "tap"), J.O("mode", "soft")))));
            Assert.IsTrue(Ok(WithTutorial(Step(J.O("event", "tap"), J.O("mode", "required")))));
            Assert.IsFalse(Ok(WithTutorial(Step(J.O("event", "tap"), J.O("mode", 3)))));
            Assert.IsFalse(Ok(WithTutorial(Step(J.O("event", "tap"), J.O("spotlight", true)))));
        }

        [Test(Description = "GDD 14.1/3 piece filter (DL-2R-02): only on yardMove and placementCorrect, a piece:<i> / piece:k<p>_<i> id, with at")]
        public void GDD_14_1_3_piece_filter()
        {
            Assert.IsTrue(CondOk(J.O("event", "yardMove", "piece", "piece:2")));
            Assert.IsTrue(CondOk(J.O("event", "placementCorrect", "count", 1, "piece", "piece:k1_0", "at", new[] { 6, 0 })));
            Assert.IsFalse(CondOk(J.O("event", "landed", "piece", "piece:2")));
            Assert.IsFalse(CondOk(J.O("event", "yardMove", "piece", "debris:0")));
            Assert.IsFalse(CondOk(J.O("event", "yardMove", "piece", "2")));
            Assert.IsTrue(LevelSchemaPatterns.PieceRef.IsMatch("piece:k1_12"));
        }

        [Test(Description = "GDD 14.1/3 boosterUsed is an event; startOn may be segmentDone (Faz 2R LEVELS 7, 10)")]
        public void GDD_14_1_3_boosterUsed_is_an_event_startOn_may_be_segmentDone()
        {
            Assert.IsTrue(Ok(WithTutorial(Step(J.O("event", "boosterUsed", "count", 1), J.O("startOn", J.O("event", "segmentDone"))))));
        }

        [Test(Description = "tutorial holdOverBuild needs minMs")]
        public void tutorial_holdOverBuild_needs_minMs()
        {
            Assert.IsFalse(CondOk(J.O("event", "holdOverBuild")));
            Assert.IsTrue(CondOk(J.O("event", "holdOverBuild", "minMs", 500)));
            Assert.IsFalse(CondOk(J.O("event", "overWall", "minMs", 500)));
        }

        [Test(Description = "tutorial at only on yardMove and placementCorrect")]
        public void tutorial_at_only_on_yardMove_and_placementCorrect()
        {
            Assert.IsTrue(CondOk(J.O("event", "yardMove", "count", 1, "at", new[] { 0, 6 })));
            Assert.IsTrue(CondOk(J.O("event", "placementCorrect", "at", new[] { 6, 0 })));
            Assert.IsFalse(CondOk(J.O("event", "landed", "at", new[] { 6, 0 })));
            Assert.IsFalse(CondOk(J.O("event", "gapPass", "at", new[] { 6, 0 })));
        }

        [Test(Description = "tutorial filter only on its event")]
        public void tutorial_filter_only_on_its_event()
        {
            Assert.IsTrue(CondOk(J.O("event", "landed", "flag", "glass", "wind", true)));
            Assert.IsTrue(CondOk(J.O("event", "deliveryDone", "flag", "mortar")));
            Assert.IsTrue(CondOk(J.O("event", "placementCorrect", "hidden", true)));
            Assert.IsTrue(CondOk(J.O("event", "yardMove", "painted", true)));
            Assert.IsFalse(CondOk(J.O("event", "landed", "type", "crate")));
            Assert.IsFalse(CondOk(J.O("event", "yardMove", "hidden", true)));
            Assert.IsFalse(CondOk(J.O("event", "deliveryDone", "wind", true)));
            Assert.IsFalse(CondOk(J.O("event", "landed", "flag", "chained")));
        }

        [Test(Description = "tutorial obstacleHit and itemCollected need type")]
        public void tutorial_obstacleHit_and_itemCollected_need_type()
        {
            Assert.IsFalse(CondOk(J.O("event", "obstacleHit")));
            Assert.IsFalse(CondOk(J.O("event", "itemCollected")));
            Assert.IsTrue(CondOk(J.O("event", "obstacleHit", "type", "chain")));
            Assert.IsTrue(CondOk(J.O("event", "itemCollected", "type", "key")));
            Assert.IsFalse(CondOk(J.O("event", "itemCollected", "type", "crate")));
        }

        [Test(Description = "GDD 14.1/1 textKey accepts tut.l<n>.<topic>, tut.m.<topic> (Faz 2R) and tut.ctx.<topic>")]
        public void GDD_14_1_1_textKey_formats()
        {
            foreach (var key in new[] { "tut.m.dig", "tut.m.carryNow", "tut.m.cranebooster", "tut.ctx.support", "tut.l12.clear" })
                Assert.IsTrue(LevelSchemaPatterns.TutTextKey.IsMatch(key), key);
            foreach (var key in new[] { "tut.meta.bridge", "tut.L1.lift", "tut.m.", "tut.m.dig.more", "tut.m.Dig", "tut.l123.x" })
                Assert.IsFalse(LevelSchemaPatterns.TutTextKey.IsMatch(key), key);
            Assert.IsTrue(Ok(WithTutorial(Step(J.O("event", "tap"), J.O("textKey", "tut.m.free")))));
            Assert.IsFalse(Ok(WithTutorial(Step(J.O("event", "tap"), J.O("textKey", "tut.meta.bridge")))));
        }
    }
}
