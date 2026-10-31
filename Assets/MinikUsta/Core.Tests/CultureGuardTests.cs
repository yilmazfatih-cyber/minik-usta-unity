#nullable enable
// Canary for AssemblyInfo.cs (REVIEW_PHASE2 R-06): if a test runner does not apply the assembly-level SetCulture, this test
// fails first with the real cause instead of hundreds of hash mismatches.
using System.Globalization;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class CultureGuardTests
    {
        [Test(Description = "K-43 test harness: tests run under the invariant culture (AssemblyInfo.cs SetCulture(\"\")), so '-' prints as '-'")]
        public void K43_test_harness_runs_under_the_invariant_culture()
        {
            Assert.AreEqual("-", NumberFormatInfo.CurrentInfo.NegativeSign,
                "the assembly-level [SetCulture(\"\")] was not applied by this runner (current culture '" + CultureInfo.CurrentCulture.Name
                + "'): hash strings built with int.ToString() would differ from the TypeScript ones");
            Assert.AreEqual("-12", (-12).ToString());
            Assert.AreEqual("-1", ("" + -1));
        }
    }
}
