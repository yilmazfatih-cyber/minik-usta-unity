#nullable enable
// Paket F (CL-V3C-03/05) C# mirrors of tests/tools/packageF.test.ts (web): wide-site requirement, pallet_stuck (retired by D-106;
// PalletD106Tests), Y12 first level.
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

        [Test(Description = "K-58 item 10 a C3 pallet (once pallet_stuck) and a D2 pallet both validate without pallet_stuck (D-106)")]
        public void K58_item_10_C3_and_D2_pallets_validate_without_pallet_stuck()
        {
            foreach (var shape in new[] { "C3_0", "D2_90" })
            {
                var spec = new Fixtures.LevelSpec
                {
                    Id = 8, SchemaVersion = 3, Yard = new Fixtures.SizeSpec(6, 4), Site = new Fixtures.SizeSpec(3, 4), Plan = new[] { "GGG", "GGG", "WWW", "WWW" },
                    Pieces = Fixtures.Boards.Ps(Fixtures.Boards.Pc(shape, "W", 0, 0), Fixtures.Boards.Pc(shape, "W", 0, 0), Fixtures.Boards.Pc("D2_90", "G", 3, 0), Fixtures.Boards.Pc("D2_90", "G", 3, 2)),
                    Traits = new System.Collections.Generic.Dictionary<int, Json.JsonValue>
                    {
                        { 0, Fixtures.J.O("stack", Fixtures.J.O("id", 0, "z", 0)) },
                        { 1, Fixtures.J.O("stack", Fixtures.J.O("id", 0, "z", 1)) },
                    },
                };
                var codes = Logic.ValidateLevelJson(Fixtures.Builders.LevelJson(spec), new LogicContext()).Issues.Select(i => i.Code).ToArray();
                CollectionAssert.DoesNotContain(codes, "pallet_stuck", shape);
            }
        }

        private static string[] ShippedCodes(int n) =>
            Logic.ValidateLevelJson(Fixtures.TestPaths.LoadCurrentLevelJson(n), new LogicContext()).Issues.Select(i => i.Code).ToArray();

        [Test(Description = "K-63 C2 NarrowPlanTrapOpen is empty (level 4 repainted trap-free): no shipped 2-column level carries plan_trap")]
        public void K63_C2_narrow_plan_trap_open_is_empty_and_levels_1_to_5_have_no_plan_trap()
        {
            Assert.AreEqual(0, Logic.NarrowPlanTrapOpen.Count);
            for (int n = 1; n <= 5; n++) CollectionAssert.DoesNotContain(ShippedCodes(n), "plan_trap", "B" + n);
        }

        [Test(Description = "K-63 C2 tile_family_mix is silent when the plan trap scan proves 0 traps (level 5)")]
        public void K63_C2_tile_family_mix_is_silent_on_a_proven_trap_free_plan()
        {
            var codes = ShippedCodes(5);
            CollectionAssert.DoesNotContain(codes, "tile_family_mix");
            CollectionAssert.DoesNotContain(codes, "plan_trap");
        }

        [Test(Description = "K-65 K-53 CL-V3C-15 C2 the tutorial hand path and done.at accept x = 8 on the 9-column board, x = 9 is a schema error")]
        public void K65_K53_CL_V3C_15_hand_path_and_done_at_accept_column_8()
        {
            Json.JsonValue Level7(int x)
            {
                var raw = Fixtures.TestPaths.LoadCurrentLevelJson(7);
                var step = raw["tutorial"]![0];
                step.Set("hand", Fixtures.J.O("kind", "tap", "path", Fixtures.J.A(Fixtures.J.A(x, 0))));
                return raw;
            }
            CollectionAssert.DoesNotContain(Logic.ValidateLevelJson(Level7(8), new LogicContext()).Issues.Select(i => i.Code).ToArray(), "schema");
            Assert.IsNull(Logic.ValidateLevelJson(Level7(9), new LogicContext()).Level, "x 9 never parses");
        }

        [Test(Description = "K-60 item 6 Y12 unlocks at level 7")]
        public void K60_item_6_Y12_first_level_is_7()
        {
            Assert.AreEqual(7, Mechanics.TraitTable.First(m => m.Id == "Y12").FirstLevel);
        }
    }
}
