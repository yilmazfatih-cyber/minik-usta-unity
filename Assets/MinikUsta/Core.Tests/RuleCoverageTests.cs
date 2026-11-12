#nullable enable
// The web repo's `npm run test:rules -- --phase 2R` gate (tools/rule-coverage.ts, tests/tools/ruleCoverage.test.ts) on
// the C# tests: every K-xx rule, obstacle, GDD §13 E row and OBSTACLES [kural] note up to Faz 2R (76 ids at web b31c4e8:
// K 46, obstacles 5, E 22, N-notes 3) has at least one test whose name names it. Ids and phases: Golden/rule_ids.json
// (`node tools/gen-rule-ids.ts`). The names of these tests deliberately carry no rule id.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class RuleCoverageTests
    {
        private static List<RuleId> Required() => RuleCoverage.LoadIds().Where(r => r.Phase <= RuleCoverage.Phase2R).ToList();

        [Test(Description = "test:rules id file: the phase 2R set of the web gate (K 46, obstacles 6 with S9 of level 6 (D-103), E 22, N-notes 3 = 77)")]
        public void Rule_coverage_id_file_holds_the_77_phase_2R_ids_of_the_web_gate()
        {
            var all = RuleCoverage.LoadIds();
            var req = Required();
            Assert.AreEqual(77, req.Count, "phase ≤ 2R ids");
            CollectionAssert.AreEqual(new[] { 46, 6, 22, 3 }, new[] { "K", "obstacle", "E", "N" }.Select(k => req.Count(r => r.Kind == k)).ToArray(), "K, obstacle, E, N");
            Assert.AreEqual(66, all.Count(r => r.Kind == "K"), "every GDD K-xx has an id (K-55 is the newest binding rule, phase 3; K-56…K-66 are the v3 ÖNERİ rules, phase v3)");
            CollectionAssert.AreEqual(new[] { "S1", "S9", "W1", "W2", "W3", "Y5" }, req.Where(r => r.Kind == "obstacle").Select(r => r.Id).OrderBy(x => x, System.StringComparer.Ordinal).ToArray(), "obstacles of levels 1–10");
            // later phases are not required yet (TS: K-20 and the bridge / league rows are phase 3–4)
            Assert.IsFalse(req.Any(r => r.Id == "K-20" || r.Id == "E-22" || r.Id == "E-45"), "phase 3–4 ids stay out of the 2R set");
            CollectionAssert.AllItemsAreUnique(all.Select(r => r.Id).ToArray());
        }

        [Test(Description = "test:rules id match: K-1 is not K-10, S1 is not S10, `K-17 …`, `(K-17)` and the C# form `K17_…` count")]
        public void Rule_coverage_id_match_rejects_prefixes_of_longer_ids_and_accepts_the_identifier_form()
        {
            Assert.IsTrue(RuleCoverage.Names("K-17 wrong placement bounces", "K-17"));
            Assert.IsTrue(RuleCoverage.Names("bounce (K-17)", "K-17"));
            Assert.IsFalse(RuleCoverage.Names("K-170 x", "K-17"));
            Assert.IsFalse(RuleCoverage.Names("S10 x", "S1"));
            Assert.IsTrue(RuleCoverage.Names("MovesTests.K17_a_wrong_drop_bounces", "K-17"));
            Assert.IsTrue(RuleCoverage.Names("SessionTests.K29_E42_an_accepted_offer", "E-42"));
            Assert.IsTrue(RuleCoverage.Names("W1StaticGapTests.Rail_example", "W1"));
            Assert.IsFalse(RuleCoverage.Names("MovesTests.K170_x", "K-17"));
            Assert.IsFalse(RuleCoverage.Names("MovesTests.K1_x", "K-10"));
            Assert.IsFalse(RuleCoverage.Names("MovesTests.XK17_x", "K-17"));
            Assert.IsFalse(RuleCoverage.Names("ShapesTests.S10_x", "S1"));
            Assert.Greater(RuleCoverage.MissingIds(Required(), new[] { "X.K01_only" }, RuleCoverage.Phase2R).Count, 0);
            Assert.AreEqual(0, RuleCoverage.MissingIds(Required(), Required().Select(r => r.Id + " covered").ToArray(), RuleCoverage.Phase2R).Count);
        }

        [Test(Description = "test:rules --phase v3 (CL-V3-11): every v3 ÖNERİ rule has a C# test named after it; the solver-measure rule is TS tool only, C# N/A")]
        public void Rule_coverage_phase_v3_every_proposal_rule_has_a_test_named_after_it_except_the_ts_only_one()
        {
            var v3 = RuleCoverage.LoadIds().Where(r => r.Phase == RuleCoverage.PhaseV3).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(56, 11).Select(n => "K-" + n).ToArray(), v3.Select(r => r.Id).OrderBy(x => x, System.StringComparer.Ordinal).ToArray(), "the v3 ids");
            var titles = RuleCoverage.TestTitles(typeof(RuleCoverageTests).Assembly, out _);
            var missing = RuleCoverage.MissingIds(v3.Where(r => !RuleCoverage.NotApplicableInCSharp.Contains(r.Id)), titles, RuleCoverage.PhaseV3);
            Assert.IsEmpty(missing, "rule-coverage (phase v3): " + string.Join(", ", missing));
        }

        [Test(Description = "test:rules --phase 2R on the C# tests: every required id has a test named after it")]
        public void Rule_coverage_phase_2R_every_required_id_has_a_test_named_after_it()
        {
            var titles = RuleCoverage.TestTitles(typeof(RuleCoverageTests).Assembly, out int methods);
            Assert.Greater(methods, 600, "test methods found by reflection");
            var missing = RuleCoverage.MissingIds(RuleCoverage.LoadIds(), titles, RuleCoverage.Phase2R);
            Assert.IsEmpty(missing, "rule-coverage (phase ≤ 2R): " + missing.Count + " rule id(s) without a C# test name: " + string.Join(", ", missing));
        }
    }
}
