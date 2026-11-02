#nullable enable
// Port of tests/level/mechanics.test.ts (web repo): mechanic data signatures (GDD K-45/10, R-21; OBSTACLES "Veri imzası" Faz 2R,
// CL-2R-12). The first TS test also compares the table with the docs/OBSTACLES.md rows (web repo only); its code half is kept.
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
    public class MechanicsTests
    {
        private static readonly P[] B1 = { new P("B1_0", "W", 0, 0) };

        private static string[] Derive(LevelSpec spec) => Mechanics.DeriveMechanics(Builders.Level(spec));

        [Test(Description = "K-45/10 signature table equals the MECHANIC_IDS order (27 rows, S2 out, S9 in; the OBSTACLES.md row comparison stays in the web repo)")]
        public void K45_10_signature_table_equals_the_mechanic_ids()
        {
            Assert.AreEqual(27, Mechanics.Table.Length);
            CollectionAssert.AreEqual(MechanicIds.All, Mechanics.Table.Select(m => m.Id).ToArray());
            // OBSTACLES "İlk bölüm" column (design data), as pinned by the TS test against the document
            var firstLevels = new Dictionary<string, int>
            {
                { "W1", 4 }, { "W2", 6 }, { "W3", 9 }, { "W4", 13 }, { "W5", 16 }, { "W6", 22 }, { "W7", 26 }, { "W8", 32 },
                { "Y1", 11 }, { "Y2", 18 }, { "Y3", 24 }, { "Y4", 28 }, { "Y5", 8 }, { "Y6", 14 }, { "Y7", 19 }, { "Y8", 35 },
                { "S1", 1 }, { "S3", 21 }, { "S4", 17 }, { "S5", 31 }, { "S6", 37 }, { "S7-R", 27 }, { "S7-M", 29 }, { "S8", 38 },
                { "G-H", 15 }, { "G-L", 23 }, { "S9", 12 },
            };
            foreach (var m in Mechanics.Table) Assert.AreEqual(firstLevels[m.Id], m.FirstLevel, m.Id);
        }

        [Test(Description = "K-45/10 W3 derived from gap size (any gap type, N2)")]
        public void K45_10_W3_derived_from_gap_size_any_gap_type_N2()
        {
            CollectionAssert.AreEqual(new[] { "W3", "W4" }, Derive(new LevelSpec { Id = 13, Wall = Wall(4, J.O("type", "shutter", "y", 0, "size", 1, "period", 2)), Pieces = B1 }));
            CollectionAssert.AreEqual(new[] { "W1" }, Derive(new LevelSpec { Id = 4, Wall = Wall(6, Gap(2, 2)), Pieces = B1 }));
        }

        [Test(Description = "K-45/10 K-49 W2 is height = H (H = max(Hy, Hs + eMax)); the default board keeps height = 8")]
        public void K45_10_K49_W2_is_height_equals_H()
        {
            string[] At_(int height, int cols, int rows, int yardRows = 4) =>
                Derive(new LevelSpec { Id = 6, Yard = new SizeSpec(4, yardRows), Site = new SizeSpec(cols, rows), Wall = Wall(height), Pieces = B1 });
            CollectionAssert.AreEqual(new[] { "W2" }, At_(7, 2, 7, 5)); // LEVELS Bölüm 6
            CollectionAssert.IsEmpty(At_(6, 2, 7, 5));
            CollectionAssert.AreEqual(new[] { "W2" }, At_(5, 2, 5, 5)); // Bölüm 7
            CollectionAssert.IsEmpty(At_(4, 2, 6)); // Bölüm 4: H = 6
            CollectionAssert.AreEqual(new[] { "W2" }, Derive(new LevelSpec { Id = 6, Wall = Wall(8), Pieces = B1 }));
            var elevator = Builders.Level(new LevelSpec
            {
                Id = 37, Yard = new SizeSpec(4, 4), Site = new SizeSpec(2, 5), Elevator = Elevator(0, 2, 0, 1), Wall = Wall(7), Pieces = B1,
            });
            Assert.AreEqual(7, Mechanics.BoardHeight(elevator));
            CollectionAssert.AreEqual(new[] { "W2", "S6" }, Mechanics.DeriveMechanics(elevator));
        }

        [Test(Description = "K-45/10 K-44 Y5 is I5/Q9 in any batch; a 3-wide material block (I3_90) is not Y5 any more")]
        public void K45_10_K44_Y5_is_I5_Q9_in_any_batch()
        {
            CollectionAssert.AreEqual(new[] { "Y5" }, Derive(new LevelSpec { Id = 8, Pieces = Ps(Pc("Q9_0", "W", 0, 0)) }));
            CollectionAssert.AreEqual(new[] { "Y5", "S1" }, Derive(new LevelSpec
            {
                Id = 8, Plans = new[] { new[] { "WW" }, new[] { "WW" } }, Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = Ps(Pc("I5_0", "W", 0, 0)) } }, Pieces = B1,
            }));
            CollectionAssert.IsEmpty(Derive(new LevelSpec { Id = 12, Pieces = Ps(Pc("I3_90", "W", 0, 0)) }));
        }

        [Test(Description = "K-45/10 K-49 S9 is site.cols ≥ 3")]
        public void K45_10_K49_S9_is_site_cols_ge_3()
        {
            string[] Site_(int cols) => Derive(new LevelSpec { Id = 12, Yard = new SizeSpec(4, 4), Site = new SizeSpec(cols, 4), Pieces = B1 });
            CollectionAssert.IsEmpty(Site_(2));
            CollectionAssert.AreEqual(new[] { "S9" }, Site_(3));
            CollectionAssert.AreEqual(new[] { "S9" }, Site_(4));
        }

        [Test(Description = "K-45/10 every signature is detected from data alone (S2 is not in the table)")]
        public void K45_10_every_signature_is_detected_from_data_alone()
        {
            var full = Builders.Level(new LevelSpec
            {
                Id = 40,
                Wall = new WallSpec
                {
                    Height = 8, Fan = "left",
                    Gaps = new[] { Gap(0, 1), J.O("type", "shutter", "y", 2, "size", 1, "period", 2), J.O("type", "slider", "y", 4, "size", 1, "range", new[] { 4, 5 }) },
                },
                Gravity = new GravitySpec { Build = "high", Yard = true },
                Plans = new[] { new[] { "W.", "WW" }, new[] { "??", "WW" }, new[] { "??", "WW" } },
                Hidden = new JsonValue?[] { null, J.O("kind", "repeat", "period", 1), J.O("kind", "mirrorOf", "segment", 0) },
                Pieces = Ps(Pc("B1_0", "W", 0, 0, new[] { "chained", "wet" }, 2), Pc("B1_0", "W", 1, 0, new[] { "glass", "mortar" }), Pc("B1_0", "W", 2, 0, new[] { "balloon" }), Pc("I5_0", "W", 0, 1)),
                Debris = new[] { new D("B1_0", "W", 6, 0) },
                Obstacles = new[] { Crate(3, 0, 1), Bag(4, 0), Screw(0, 0) },
                Elevator = Elevator(0, 1, 0, 1),
            });
            CollectionAssert.AreEqual(
                new[] { "W1", "W2", "W3", "W4", "W5", "W8", "Y1", "Y2", "Y3", "Y4", "Y5", "Y6", "Y7", "Y8", "S1", "S3", "S4", "S6", "S7-R", "S7-M", "S8", "G-H" },
                Mechanics.DeriveMechanics(full));
            var others = Builders.Level(new LevelSpec
            {
                Id = 31, Mode = "carousel", CarouselEvery = 3, Gravity = new GravitySpec { Build = "low" },
                Wall = Wall(6, J.O("type", "paint", "y", 0, "size", 2, "color", "W"), LockedGap(3, 2, "a")),
                Site = new SizeSpec(3, 8), Yard = new SizeSpec(5, 8), Pieces = B1,
            });
            CollectionAssert.AreEqual(new[] { "W6", "W7", "S5", "G-L", "S9" }, Mechanics.DeriveMechanics(others));
        }

        [Test(Description = "K-45/10 a single segment is not S1, teaches adds nothing to the derived set")]
        public void K45_10_a_single_segment_is_not_S1_teaches_adds_nothing()
        {
            CollectionAssert.IsEmpty(Derive(new LevelSpec { Id = 5, Teaches = "S1", Pieces = B1 }));
            CollectionAssert.AreEqual(new[] { "S1" }, Derive(new LevelSpec { Id = 5, Plans = new[] { new[] { "WW" }, new[] { "WW" } }, Pieces = B1 }));
        }

        [Test(Description = "K-45/10 newMechanics subtracts the earlier levels; a `.` plan derives no mechanic (S2 MVP dışı)")]
        public void K45_10_newMechanics_subtracts_the_earlier_levels()
        {
            var l4 = Builders.Level(new LevelSpec { Id = 4, Wall = Wall(6, Gap(3, 2)), Plan = new[] { "RR", "WW", "W.", "WW", "YY" }, Pieces = B1 });
            CollectionAssert.AreEqual(new[] { "W1" }, Mechanics.DeriveMechanics(l4));
            CollectionAssert.IsEmpty(Mechanics.NewMechanics(l4, new HashSet<string> { "W1" }));
            CollectionAssert.AreEqual(new[] { "W1" }, Mechanics.NewMechanics(l4, new HashSet<string>()));
        }
    }
}
