#nullable enable
// Port of tests/core/grid.test.ts plus node-generated cross-language vectors (the 1 000-step seeded walk).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class GridTests
    {
        /// <summary>Moves a piece onto the site (segment `seg`, global column x, plan row y) and refreshes the masks.</summary>
        private static void ToSite(GameState s, int id, int x, int y, int seg = 0, bool locked = true)
        {
            Grid.VacatePiece(s, id);
            SetPieceField(s, id, PF.Zone, Zone.Site);
            SetPieceField(s, id, PF.X, x);
            SetPieceField(s, id, PF.Y, y);
            SetPieceField(s, id, PF.Seg, seg);
            SetFlag(s, id, FlagBit.Locked, locked);
            Grid.OccupyPiece(s, id);
            Grid.RefreshSiteMasks(s, seg);
        }

        private static string[] Rows(ushort[] m) =>
            m.Select(r => string.Join("", Enumerable.Range(0, 8).Select(x => ((r >> x) & 1) != 0 ? "#" : "."))).Reverse().ToArray();

        [Test(Description = "K-08 collision rows: yard occupancy minus the dragged piece, crane rows empty")]
        public void K08_collision_rows_yard_occupancy_minus_the_dragged_piece_crane_rows_empty()
        {
            var s = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("O4_0", "W", 0, 0), new P("B1_0", "Y", 5, 7) } });
            CollectionAssert.AreEqual(new[]
            {
                "........", "........", ".....#..", "........", "........", "........", "........", "........", "........", "........",
            }, Rows(Grid.CollisionMasks(s, 0)));
            Assert.AreEqual(0b11, Grid.CollisionMasks(s, 1)[0] & 0b11);
        }

        [Test(Description = "K-24 site rows follow the elevator offset; rows below it are platform")]
        public void K24_site_rows_follow_the_elevator_offset_rows_below_it_are_platform()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Id = 37,
                Plan = new[] { "WW", "WW" },
                Elevator = J.O("range", new[] { 0, 2 }, "start", 2, "dir", 1),
                Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "W", 1, 0) },
            });
            ToSite(s, 0, 6, 0);
            var m = Grid.CollisionMasks(s, 1);
            CollectionAssert.AreEqual(new[] { 0b11, 0b11, 0b01, 0 }, new[] { m[0], m[1], m[2], m[3] }.Select(r => (r >> 6) & 0b11).ToArray());
            CollectionAssert.AreEqual(new sbyte[] { 2, 1 }, Grid.SiteColumnTops(s, -1));
            CollectionAssert.AreEqual(new[] { new BoardCell(6, 2) }, Grid.PieceBoardCells(s, 0));
        }

        [Test(Description = "K-11 site column tops: −1 for an empty column, excluding the dragged piece")]
        public void K11_site_column_tops_minus_1_for_an_empty_column_excluding_the_dragged_piece()
        {
            var s = Builders.InitialState(new LevelSpec { Plan = new[] { "WW", "WW", "WW" }, Pieces = new[] { new P("D2_0", "W", 0, 0), new P("B1_0", "W", 2, 0) } });
            CollectionAssert.AreEqual(new sbyte[] { -1, -1 }, Grid.SiteColumnTops(s, -1));
            ToSite(s, 0, 7, 0);
            CollectionAssert.AreEqual(new sbyte[] { -1, 1 }, Grid.SiteColumnTops(s, -1));
            CollectionAssert.AreEqual(new sbyte[] { -1, -1 }, Grid.SiteColumnTops(s, 0));
        }

        [Test(Description = "K-34 E-43 filled holds locked blocks and trowel cells; wrongOcc holds debris and stuck mortar (also on `.`)")]
        public void K34_E43_filled_holds_locked_blocks_and_trowel_cells_wrongOcc_holds_debris_and_stuck_mortar_also_on_dot()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Id = 35,
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "W", 1, 0, new[] { "mortar" }) },
                Debris = new[] { new D("B1_0", "W", 6, 2) },
            });
            ToSite(s, 0, 6, 0);
            ToSite(s, 1, 7, 1, 0, false);
            SetFlag(s, 1, FlagBit.Stuck, true);
            SetSiteOcc(s, 0, 1, 0, SiteTrowel);
            Grid.RefreshSiteMasks(s, 0);
            CollectionAssert.AreEqual(new[] { 0b001, 0b001 }, new[] { FilledMask(s, 0, 0), FilledMask(s, 0, 1) });
            // debris at (6,2) and stuck mortar on the `.` cell (7,1)
            CollectionAssert.AreEqual(new[] { 0b100, 0b010 }, new[] { WrongOccMask(s, 0, 0), WrongOccMask(s, 0, 1) });
            CollectionAssert.AreEqual(new string[0], Grid.StateInvariantErrors(s));
        }

        [Test(Description = "visible segment is the active one, or the front one on a carousel (K-22, K-23)")]
        public void K22_K23_visible_segment_is_the_active_one_or_the_front_one_on_a_carousel()
        {
            var seg = Builders.InitialState(new LevelSpec { Plans = new[] { new[] { "WW" }, new[] { "WW" } }, Pieces = new[] { new P("B1_0", "W", 0, 0) } });
            SetHdr(seg, H.ActiveSeg, 1);
            Assert.AreEqual(1, Grid.VisibleSegment(seg));
            var car = Builders.InitialState(new LevelSpec
            {
                Id = 31, Mode = "carousel", CarouselEvery = 2, Plans = new[] { new[] { "WW" }, new[] { "WW" } }, Pieces = new[] { new P("B1_0", "W", 0, 0) },
            });
            SetHdr(car, H.FrontSeg, 1);
            SetHdr(car, H.ActiveSeg, 0);
            Assert.AreEqual(1, Grid.VisibleSegment(car));
        }

        [Test]
        public void Invariants_catch_corrupted_occupancy_overlaps_stale_masks_queue_drift_and_K34_holes()
        {
            var s = Builders.InitialState(new LevelSpec { Plan = new[] { "WW", "WW" }, Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "W", 1, 0) } });
            var broken = new List<KeyValuePair<string, Action<GameState>>>
            {
                new KeyValuePair<string, Action<GameState>>("stale yard cell", t => SetYardOcc(t, 3, 3, 1)),
                new KeyValuePair<string, Action<GameState>>("overlap", t => SetPieceField(t, 1, PF.X, 0)),
                new KeyValuePair<string, Action<GameState>>("stale filled", t => SetSiteOcc(t, 0, 0, 0, 0)),
                new KeyValuePair<string, Action<GameState>>("queue drift", t => SetPieceField(t, 0, PF.Zone, Zone.Queue)),
                new KeyValuePair<string, Action<GameState>>("K-34 hole", t => ToSite(t, 0, 6, 1)),
            };
            foreach (var kv in broken)
            {
                var t = CloneState(s);
                if (kv.Key == "stale filled")
                {
                    ToSite(t, 0, 6, 0);
                    kv.Value(t);
                }
                else kv.Value(t);
                Assert.Greater(Grid.StateInvariantErrors(t).Count, 0, kv.Key);
            }
            CollectionAssert.AreEqual(new string[0], Grid.StateInvariantErrors(s));
        }

        private static List<string> RunWalk(int seed)
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Pieces = new[] { new P("O4_0", "W", 0, 0), new P("D2_0", "Y", 2, 0), new P("D2_90", "W", 3, 0), new P("C3_90", "Y", 0, 4), new P("B1_0", "W", 5, 7) },
                Obstacles = new[] { J.O("type", "crate", "x", 4, "y", 4, "hp", 2) },
            });
            var rng = new Mulberry32(seed);
            var hashes = new List<string>();
            int Cells()
            {
                int n = 0;
                for (int y = 0; y < 10; y++) for (int x = 0; x < 6; x++) if (YardOcc(s, x, y) > 0) n++;
                return n;
            }
            int total = Cells();
            for (int step = 0; step < 1000; step++)
            {
                int id = rng.NextInt(5);
                var shape = Shapes.ShapeByIndex(PieceField(s, id, PF.Shape));
                int x = rng.NextInt(6 - shape.W + 1);
                int y = rng.NextInt(8 - shape.H + 1);
                Grid.VacatePiece(s, id);
                bool free = shape.Cells.All(c => YardOcc(s, x + c.X, y + c.Y) == 0);
                if (free)
                {
                    SetPieceField(s, id, PF.X, x);
                    SetPieceField(s, id, PF.Y, y);
                }
                Grid.OccupyPiece(s, id);
                CollectionAssert.AreEqual(new string[0], Grid.StateInvariantErrors(s));
                Assert.AreEqual(total, Cells());
                hashes.Add(Hash.HashHex(s));
            }
            return hashes;
        }

        [Test]
        public void Invariants_hold_over_1000_seeded_random_yard_relocations_cell_count_kept_same_seed_same_hashes()
        {
            var a = RunWalk(7);
            CollectionAssert.AreEqual(a, RunWalk(7));
            CollectionAssert.AreNotEqual(a, RunWalk(8));
        }

        [Test]
        public void The_seeded_walk_hashes_equal_the_TypeScript_walk_bit_for_bit()
        {
            var v = TestPaths.Vectors["gridWalk"]!;
            var a = RunWalk(7);
            Assert.AreEqual(v["seed7"]!["first"]!.AsString, a[0]);
            Assert.AreEqual(v["seed7"]!["at500"]!.AsString, a[500]);
            Assert.AreEqual(v["seed7"]!["last"]!.AsString, a[999]);
            Assert.AreEqual(v["seed7"]!["fnvAll"]!.AsString, Fnv1a.Hash64Hex(string.Join(",", a)));
            Assert.AreEqual(v["seed8"]!["last"]!.AsString, RunWalk(8)[999]);
        }

        [Test]
        public void PlanColorAt_returns_colour_dot_or_outside()
        {
            var s = Builders.InitialState(new LevelSpec { Plan = new[] { "WY", "W." }, Pieces = new[] { new P("B1_0", "W", 0, 0) } });
            Assert.AreEqual(0, Grid.PlanColorAt(s, 0, 0, 0));
            Assert.AreEqual(Level.Compiler.PlanDot, Grid.PlanColorAt(s, 0, 1, 0));
            Assert.AreEqual(1, Grid.PlanColorAt(s, 0, 1, 1));
            Assert.AreEqual(Level.Compiler.PlanOutside, Grid.PlanColorAt(s, 0, 0, 2));
            Assert.AreEqual(Level.Compiler.PlanOutside, Grid.PlanColorAt(s, 0, 2, 0));
        }
    }
}
