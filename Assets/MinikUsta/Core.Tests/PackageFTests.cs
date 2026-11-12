#nullable enable
// Paket F (CL-V3C-03/05) C# mirrors of tests/tools/packageF.test.ts (web): wide-site requirement, pallet_stuck, Y12 first level.
using System.Linq;
using MinikUsta.Core.Level;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class PackageFTests
    {
        [Test(Description = "K-49 item 2 levels 6-10 require Ws = 3, levels 1-5 Ws = 2, later levels either (v2 archives keep 2-3)")]
        public void K49_item_2_levels_6_to_10_require_Ws_3()
        {
            CollectionAssert.AreEqual(new[] { 2, 2 }, Logic.SiteColsRange(5));
            CollectionAssert.AreEqual(new[] { 3, 3 }, Logic.SiteColsRange(6));
            CollectionAssert.AreEqual(new[] { 3, 3 }, Logic.SiteColsRange(10));
            CollectionAssert.AreEqual(new[] { 2, 3 }, Logic.SiteColsRange(11));
            CollectionAssert.AreEqual(new[] { 2, 3 }, Logic.SiteColsRange(7, 2));
        }

        [Test(Description = "K-58 item 10 a shape whose four one-unit shifts all overlap itself can never leave its pallet")]
        public void K58_item_10_pallet_stuck_shapes()
        {
            foreach (var id in new[] { "C3_0", "O4_0", "L4_0", "J4_0", "T4_0" }) Assert.IsTrue(Logic.PalletStuck(id), id);
            foreach (var id in new[] { "D2_0", "D2_90", "I3_0", "B1_0" }) Assert.IsFalse(Logic.PalletStuck(id), id);
        }

        [Test(Description = "K-60 item 6 Y12 unlocks at level 7")]
        public void K60_item_6_Y12_first_level_is_7()
        {
            Assert.AreEqual(7, Mechanics.TraitTable.First(m => m.Id == "Y12").FirstLevel);
        }
    }
}
