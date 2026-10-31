#nullable enable
// REVIEW_PHASE2 R-06: the test helpers build comparison strings with `"" + int` / `string.Join(",", int[])`, which write a
// negative number with the minus sign of the current culture (U+2212 in fi-FI / sv-SE / nb-NO, U+061C + '-' in ar-SA). The
// TS core prints plain '-', so the expected hashes would differ on such a machine (599 of 1 420 tests red under fi-FI).
// Every test therefore runs under the invariant culture. The core does not depend on the culture (K35_K43_K45_core_outputs_do_not_depend_on_CurrentCulture);
// this attribute only keeps the *test harness* independent of the developer's regional settings (also in the Unity Editor).
// NUnit 3.5 (Unity's bundled version) declares SetCulture with AttributeTargets.Assembly; CultureGuardTests is the canary
// that names the cause if a runner ever ignores it.
[assembly: NUnit.Framework.SetCulture("")]
