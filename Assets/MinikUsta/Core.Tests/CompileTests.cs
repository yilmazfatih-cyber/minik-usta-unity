#nullable enable
// Port of tests/core/level/compile.test.ts (TECH §2.3). `loadLevel` (schema + runtime logic subset) follows with the logic.ts port.
using System.Linq;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class CompileTests
    {
        private static readonly P[] B1 = { new P("B1_0", "W", 0, 0) };

        [Test(Description = "K-45/1 seed defaults to id × 1000 + id")]
        public void K45_1_seed_defaults_to_id_x_1000_plus_id()
        {
            Assert.AreEqual(4004, Builders.Compiled(new LevelSpec { Id = 4, Pieces = B1 }).Seed);
            Assert.AreEqual(7, Builders.Compiled(new LevelSpec { Id = 4, Seed = 7, Pieces = B1 }).Seed);
        }

        [Test(Description = "K-44 symmetric shape ids are reduced to the canonical shape")]
        public void K44_symmetric_shape_ids_are_reduced_to_the_canonical_shape()
        {
            var lvl = Builders.Compiled(new LevelSpec { Pieces = new[] { new P("O4_270", "W", 0, 0), new P("D2_180", "Y", 2, 0) } });
            Assert.AreEqual(Shapes.ShapeById("O4_0").Index, lvl.Pieces[0].ShapeIndex);
            Assert.AreEqual(Shapes.ShapeById("D2_0").Index, lvl.Pieces[1].ShapeIndex);
            Assert.AreEqual("O4_270", lvl.Pieces[0].DataShape);
        }

        [Test(Description = "K-15 K-32 plan colours by local index with `.`, outside and resolved `?`")]
        public void K15_K32_plan_colours_by_local_index_with_dot_outside_and_resolved_hidden()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Plans = new[] { new[] { "WY", "W." }, new[] { "??", "YW" } },
                Hidden = new[] { null, J.O("kind", "mirrorOf", "segment", 0) },
                Pieces = B1,
            });
            var s0 = lvl.Segments[0];
            // rows top → bottom: "WY" is plan row 1, "W." row 0
            CollectionAssert.AreEqual(new sbyte[] { 0, (sbyte)Compiler.PlanDot, 0, 1 }, s0.PlanColors.Take(4).ToArray());
            Assert.AreEqual(Compiler.PlanOutside, s0.PlanColors[4]);
            CollectionAssert.AreEqual(new[] { 0b11, 0b10 }, s0.PlanMask);
            CollectionAssert.AreEqual(new[] { 0, 0b01 }, s0.DotMask);
            // mirrorOf 0: (c, r) ← segment 0 (1 − c, r); row 1 of segment 0 is W Y → ? ? = Y W
            var s1 = lvl.Segments[1];
            CollectionAssert.AreEqual(new sbyte[] { 1, 0 }, s1.PlanColors.Skip(2).Take(2).ToArray());
            Assert.AreEqual(0b1100, s1.HiddenMask);
        }

        [Test(Description = "K-25 piece ids: batch 0, then truck batches, then debris (Faz 2R: no D2 help slots, TECH §2R.1)")]
        public void K25_piece_ids_batch_0_then_truck_batches_then_debris()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Id = 17,
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "Y", 1, 0) },
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = new[] { new P("D2_90", "Y", 2, 8) } } },
                Debris = new[] { new D("B1_0", "W", 7, 0, 1) },
            });
            CollectionAssert.AreEqual(new[] { "yard", "yard", "truck", "debris" }, lvl.Pieces.Select(p => p.Origin).ToArray());
            CollectionAssert.AreEqual(new[] { Zone.Yard, Zone.Yard, Zone.Pending, Zone.Site }, lvl.Pieces.Select(p => p.StartZone).ToArray());
            Assert.AreEqual(4, lvl.StaticPieceCount);
            Assert.AreEqual(0, lvl.HelpPieceCount);
            CollectionAssert.AreEqual(new[] { 0, 1 }, lvl.Batches[0].PieceIds);
            CollectionAssert.AreEqual(new[] { 2 }, lvl.Batches[1].PieceIds);
            Assert.AreEqual(1, lvl.Pieces[3].Segment);
        }

        [Test(Description = "GDD 14.1 tutorial highlight table maps piece:, piece:k and debris: to PieceIds")]
        public void GDD_14_1_tutorial_highlight_table_maps_piece_piece_k_and_debris_to_PieceIds()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Id = 17,
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = B1,
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = new[] { new P("B1_0", "Y", 0, 8), new P("B1_0", "Y", 1, 8) } } },
                Debris = new[] { new D("B1_0", "W", 7, 0) },
            });
            Assert.AreEqual(4, lvl.TutorialPieceIds.Count);
            Assert.AreEqual(0, lvl.TutorialPieceIds["piece:0"]);
            Assert.AreEqual(1, lvl.TutorialPieceIds["piece:k1_0"]);
            Assert.AreEqual(2, lvl.TutorialPieceIds["piece:k1_1"]);
            Assert.AreEqual(3, lvl.TutorialPieceIds["debris:0"]);
        }

        [Test(Description = "K-19 gravity profiles: glass thresholds 4/3/2, G-H hold 700 (1400 reduced), only G-L steers")]
        public void K19_gravity_profiles_glass_thresholds_4_3_2_GH_hold_700_1400_reduced_only_GL_steers()
        {
            var g = Compiler.GravityProfiles;
            Assert.AreEqual(4, g["low"].GlassThreshold);
            Assert.AreEqual(3, g["normal"].GlassThreshold);
            Assert.AreEqual(2, g["high"].GlassThreshold);
            Assert.AreEqual(700, g["high"].HoldMs);
            Assert.AreEqual(1400, g["high"].HoldMsReduced);
            Assert.IsNull(g["normal"].HoldMs);
            CollectionAssert.AreEqual(new[] { true, false, false }, new[] { g["low"].Steerable, g["normal"].Steerable, g["high"].Steerable });
        }

        [Test(Description = "K-19 normal gravity has no hold limit and no steering")]
        public void K19_normal_gravity_has_no_hold_limit_and_no_steering()
        {
            var lvl = Builders.Compiled(new LevelSpec { Pieces = B1 });
            Assert.AreEqual("normal", lvl.Gravity.Build);
            Assert.IsFalse(lvl.Gravity.Yard);
            Assert.IsNull(lvl.Gravity.HoldMs);
            Assert.IsNull(lvl.Gravity.HoldMsReduced);
            Assert.AreEqual(3, lvl.Gravity.GlassThreshold);
            Assert.IsFalse(lvl.Gravity.Steerable);
        }

        [Test(Description = "K-35 step 10 timer order is W4, W5, S5, S6, Y4, K-40 and independent of rule order")]
        public void K35_step_10_timer_order_is_W4_W5_S5_S6_Y4_K40_and_independent_of_rule_order()
        {
            CollectionAssert.AreEqual(new[] { "W4", "W5", "S5", "S6", "Y4", "K-40" }, Compiler.Step10Timers.Select(t => t.Id).ToArray());
            var all = Builders.Level(new LevelSpec
            {
                Id = 40,
                Mode = "carousel",
                CarouselEvery = 3,
                Wall = new WallSpec
                {
                    Height = 8,
                    Gaps = new[] { J.O("type", "slider", "y", 4, "size", 1, "range", new[] { 4, 5 }), J.O("type", "shutter", "y", 0, "size", 1, "period", 2) },
                },
                Elevator = J.O("range", new[] { 0, 1 }, "start", 0, "dir", 1),
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = new[] { new P("B1_0", "W", 0, 0, new[] { "wet" }, 2) },
            });
            CollectionAssert.AreEqual(new[] { "W4", "W5", "S5", "S6", "Y4", "K-40" }, Compiler.ActiveStep10Timers(all).Select(t => t.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, Compiler.Compile(all).Step10.Select(t => t.Order).ToArray());
            Assert.AreEqual(0, Compiler.ActiveStep10Timers(Builders.Level(new LevelSpec { Pieces = B1 })).Length);
            var locked = Builders.Level(new LevelSpec
            {
                Id = 26, Wall = new WallSpec { Height = 6, Gaps = new[] { J.O("type", "locked", "y", 0, "size", 2, "keyId", "a") } }, Pieces = B1,
            });
            CollectionAssert.AreEqual(new[] { "K-40" }, Compiler.ActiveStep10Timers(locked).Select(t => t.Id).ToArray());
        }

        [Test(Description = "K-23 K-24 cycle length L is the LCM of the timed mechanics")]
        public void K23_K24_cycle_length_L_is_the_LCM_of_the_timed_mechanics()
        {
            Assert.AreEqual(1, Compiler.CycleLength(Builders.Level(new LevelSpec { Pieces = B1 })));
            var lvl = Builders.Level(new LevelSpec
            {
                Id = 40,
                Mode = "carousel",
                CarouselEvery = 2,
                Plans = new[] { new[] { "WW" }, new[] { "WW" }, new[] { "WW" } },
                Wall = new WallSpec { Height = 8, Gaps = new[] { J.O("type", "shutter", "y", 0, "size", 1, "period", 2) } },
                Elevator = J.O("range", new[] { 0, 2 }, "start", 0, "dir", 1),
                Pieces = B1,
            });
            // shutter 2·2 = 4, carousel 2·3 = 6, elevator 2·2 = 4 → 12
            Assert.AreEqual(12, Compiler.CycleLength(lvl));
        }

        [Test]
        public void Buffer_layout_matches_TECH_2_4_sections()
        {
            var lvl = Compiler.Compile(Builders.Level(new LevelSpec { Id = 4, Plan = new[] { "WW", "WW" }, Pieces = new[] { new P("O4_0", "W", 0, 0) } }));
            var L = lvl.Layout;
            Assert.AreEqual(18, L.YardOcc);
            Assert.AreEqual(60, L.SiteOcc - L.YardOcc);
            Assert.AreEqual(16, L.Filled - L.SiteOcc);
            Assert.AreEqual(2, L.Pieces - L.WrongOcc);
            Assert.AreEqual(L.Revealed + 1, L.Size);
        }

        [Test(Description = "K-44 K-47 cargo pieces (I5/Q9) are colourless and leave supply; mechanics derive Y5")]
        public void K44_K47_cargo_pieces_are_colourless_and_leave_supply_mechanics_derive_Y5()
        {
            var lvl = Builders.Compiled(new LevelSpec
            {
                Id = 8, Yard = new SizeSpec(6, 5), Site = new SizeSpec(2, 6), Plan = new[] { "WW", "WW", "WW", "WW", "WW", "WW" },
                Pieces = new[] { new P("I5_0", "W", 0, 0), new P("O4_0", "W", 0, 1), new P("O4_0", "W", 2, 1), new P("O4_0", "W", 4, 1) },
            });
            Assert.AreEqual(PieceClasses.Cargo, lvl.Pieces[0].Cls);
            Assert.AreEqual(-1, lvl.Pieces[0].ColorIndex);
            Assert.AreEqual(12, lvl.Supply[0]);
            Assert.AreEqual(12, lvl.Demand[0]);
            Assert.AreEqual(3, lvl.MaterialCount);
            CollectionAssert.Contains(lvl.Mechanics, "Y5");
            Assert.AreEqual(PieceClasses.Material, Compiler.PieceClassOf("O4_90"));
            Assert.AreEqual(PieceClasses.Cargo, Compiler.PieceClassOf("Q9_90"));
        }
    }
}
