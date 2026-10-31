#nullable enable
// C# port of the web repo's coverage gate (tools/rule-coverage.ts `names`, `missingIds`; TECH_DESIGN §12.2, §14.1,
// CLAUDE.md "Her K-xx kuralının en az bir testi vardır"). The rule ids and phases come from Golden/rule_ids.json
// (tools/gen-rule-ids.ts extracts them from the web documents with the same `ruleIds`); the test names are read by
// reflection from this assembly: "Fixture.Method" of every [Test] / [TestCase] / [TestCaseSource] method (the fixture
// name plays the role of the TS describe title) plus every [TestCase(TestName = …)].
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;
using NUnit.Framework;

namespace MinikUsta.Core.Tests.Fixtures
{
    public sealed class RuleId
    {
        public readonly string Id;
        public readonly string Kind;
        public readonly double Phase;

        public RuleId(string id, string kind, double phase)
        {
            Id = id;
            Kind = kind;
            Phase = phase;
        }

        public override string ToString() => Id;
    }

    public static class RuleCoverage
    {
        /// <summary>TS `PHASE_2R`: the Faz 2R phase, between 2 and 3.</summary>
        public const double Phase2R = 2.5;

        /// <summary>Every id of Golden/rule_ids.json (all phases).</summary>
        public static List<RuleId> LoadIds()
        {
            var j = TestPaths.LoadGolden("rule_ids.json");
            var o = new List<RuleId>();
            foreach (var r in j["ids"]!.Items) o.Add(new RuleId(r["id"]!.AsString, r["kind"]!.AsString, r["phase"]!.AsDouble));
            return o;
        }

        /// <summary>The C# identifier form of an id: `K-49` → `K49`, `E-03` → `E03`, `W1` / `N6` unchanged.</summary>
        public static string Token(string id) => id.Replace("-", "");

        /// <summary>
        /// TS `names(title, id)`: the title names the id, not as a prefix of a longer id (K-1 ≠ K-10, S1 ≠ S10). C# names
        /// carry the identifier form (`K49_…`, `…_E42_…`, `W1StaticGapTests`); a free-text test name may carry `K-49`.
        /// </summary>
        public static bool Names(string title, string id)
        {
            if (Regex.IsMatch(title, "(^|[^\\w-])" + Regex.Escape(id) + "(?!\\d)")) return true;
            string token = Token(id);
            return Regex.IsMatch(title, "(^|[^A-Za-z0-9])" + Regex.Escape(token) + "(?![0-9])");
        }

        /// <summary>TS `missingIds`: required ids of `phase` (null = all) that no title names.</summary>
        public static List<RuleId> MissingIds(IEnumerable<RuleId> ids, IReadOnlyList<string> titles, double? phase) =>
            ids.Where(r => (phase == null || r.Phase <= phase.Value) && !titles.Any(t => Names(t, r.Id))).ToList();

        private static bool IsTestMethod(MethodInfo m) =>
            m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0
            || m.GetCustomAttributes(typeof(TestCaseAttribute), false).Length > 0
            || m.GetCustomAttributes(typeof(TestCaseSourceAttribute), false).Length > 0;

        /// <summary>The test names of an assembly: "Fixture.Method" per test method, plus explicit [TestCase] names.</summary>
        public static List<string> TestTitles(Assembly assembly, out int methods)
        {
            var titles = new List<string>();
            methods = 0;
            foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (!IsTestMethod(m)) continue;
                    methods++;
                    titles.Add(type.Name + "." + m.Name);
                    foreach (TestCaseAttribute tc in m.GetCustomAttributes(typeof(TestCaseAttribute), false))
                        if (!string.IsNullOrEmpty(tc.TestName)) titles.Add(type.Name + "." + tc.TestName);
                }
            }
            return titles;
        }
    }
}
