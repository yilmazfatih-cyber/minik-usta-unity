#nullable enable
// Port of tests/core/hash.test.ts (TECH §2.6, D-052) plus node-generated cross-language vectors.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class HashTests
    {
        private static void Move(GameState s, int id, int x, int y)
        {
            Grid.VacatePiece(s, id);
            SetPieceField(s, id, PF.X, x);
            SetPieceField(s, id, PF.Y, y);
            Grid.OccupyPiece(s, id);
        }

        private static LevelSpec Spec3() => new LevelSpec { Id = 3, Pieces = new[] { new P("B1_0", "W", 0, 0), new P("O4_0", "Y", 2, 0) } };

        [Test]
        public void Is_deterministic_two_compiles_of_the_same_level_give_the_same_hash()
        {
            Assert.AreEqual(Hash.HashHex(Builders.InitialState(Spec3())), Hash.HashHex(Builders.InitialState(Spec3())));
            Assert.IsTrue(Regex.IsMatch(Hash.HashHex(Builders.InitialState(Spec3())), "^[0-9a-f]{16}$"));
        }

        [Test]
        public void Initial_hashes_equal_the_TypeScript_values_bit_for_bit()
        {
            var v = TestPaths.Vectors["hashSpec"]!;
            Assert.AreEqual(v["id3"]!.AsString, Hash.HashHex(Builders.InitialState(Spec3())));
            var appendix = Builders.InitialState(new LevelSpec
            {
                Id = 4,
                Wall = new WallSpec { Height = 6, Gaps = new[] { J.O("type", "static", "y", 3, "size", 1) } },
                Plan = new[] { "YY", "WW", "W.", "WW", "YY" },
                Pieces = new[]
                {
                    new P("D2_90", "Y", 0, 7), new P("D2_0", "W", 2, 6), new P("O4_0", "G", 3, 4), new P("B1_0", "W", 5, 3),
                    new P("D2_90", "Y", 0, 0), new P("D2_90", "W", 2, 0), new P("D2_0", "W", 4, 0),
                },
                Obstacles = new[]
                {
                    J.O("type", "crate", "x", 5, "y", 4, "hp", 2), J.O("type", "cement_bag", "x", 0, "y", 2),
                    J.O("type", "screw", "x", 3, "y", 4), J.O("type", "key", "x", 5, "y", 3, "id", "a"),
                },
            });
            Assert.AreEqual(v["appendix"]!.AsString, Hash.HashHex(appendix));
        }

        [Test]
        public void Uses_the_piece_class_not_the_piece_id_swapping_identical_blocks_keeps_the_hash()
        {
            var s = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "W", 1, 0), new P("B1_0", "Y", 2, 0) } });
            string h = Hash.HashHex(s);
            var swapped = CloneState(s);
            Move(swapped, 0, 4, 0);
            Move(swapped, 1, 0, 0);
            Move(swapped, 0, 1, 0);
            Assert.AreEqual(h, Hash.HashHex(swapped));
            var colourSwap = CloneState(s);
            Move(colourSwap, 1, 4, 0);
            Move(colourSwap, 2, 1, 0);
            Move(colourSwap, 1, 2, 0);
            Assert.AreNotEqual(h, Hash.HashHex(colourSwap));
        }

        [Test]
        public void Symmetric_orientations_hash_the_same_canonical_shape()
        {
            var a = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("O4_0", "W", 0, 0) } });
            var b = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("O4_90", "W", 0, 0) } });
            Assert.AreEqual(Hash.HashHex(a), Hash.HashHex(b));
        }

        [Test]
        public void Position_zone_flags_gaps_filled_cursor_and_turn_mod_L_change_the_hash_counters_do_not()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Id = 13,
                Wall = new WallSpec { Height = 4, Gaps = new[] { J.O("type", "shutter", "y", 0, "size", 1, "period", 2) } },
                Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "Y", 1, 0) },
            });
            Assert.AreEqual(4, s.Lvl.Cycle);
            string h = Hash.HashHex(s);
            var variants = new Action<GameState>[]
            {
                t => Move(t, 0, 3, 3),
                t => SetFlag(t, 0, FlagBit.Glass, true),
                t => SetGapField(t, 0, GF.Open, 0),
                t => SetFilledMask(t, 0, 0, 1),
                t => SetHdr(t, H.DeliveryCursor, 1),
                t => SetHdr(t, H.Turn, 1),
                t => SetHdr(t, H.OpenShutterUntil, 5),
            };
            foreach (var change in variants)
            {
                var t = CloneState(s);
                change(t);
                Assert.AreNotEqual(h, Hash.HashHex(t));
            }
            var same = new Action<GameState>[]
            {
                t => SetHdr(t, H.Turn, 4), // turn mod L
                t => SetHdr(t, H.MovesLeft, 1),
                t => SetHdr(t, H.Combo, 3),
                t => SetHdr(t, H.WrongCount, 2),
                t => SetPieceField(t, 0, PF.ArrivedTurn, 9),
            };
            foreach (var change in same)
            {
                var t = CloneState(s);
                change(t);
                Assert.AreEqual(h, Hash.HashHex(t));
            }
        }

        [Test(Description = "K-26 queue order is part of the hash; pending and gone pieces are not")]
        public void K26_queue_order_is_part_of_the_hash_pending_and_gone_pieces_are_not()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Plans = new[] { new[] { "WW" }, new[] { "WW" } },
                Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "Y", 1, 0) },
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = new[] { new P("B1_0", "Y", 0, 8) } } },
            });
            string ToQueue(int[] order)
            {
                var t = CloneState(s);
                foreach (int id in order)
                {
                    Grid.VacatePiece(t, id);
                    EnqueuePiece(t, id);
                }
                return Hash.HashHex(t);
            }
            Assert.AreNotEqual(ToQueue(new[] { 1, 0 }), ToQueue(new[] { 0, 1 }));
            var gone = CloneState(s);
            SetPieceField(gone, 2, PF.Zone, Zone.Gone);
            Assert.AreEqual(Hash.HashHex(s), Hash.HashHex(gone));
        }

        [Test]
        public void No_collisions_over_5000_random_yard_configurations()
        {
            var b = Builders.InitialState(new LevelSpec
            {
                Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "Y", 1, 0), new P("D2_0", "W", 2, 0), new P("O4_0", "Y", 3, 0) },
            });
            var rng = new Mulberry32(2026);
            var seen = new Dictionary<string, string>();
            for (int n = 0; n < 5000; n++)
            {
                var t = CloneState(b);
                for (int id = 0; id < 4; id++) Grid.VacatePiece(t, id);
                var placed = new List<string>();
                for (int id = 0; id < 4; id++)
                {
                    int x = rng.NextInt(5);
                    int y = rng.NextInt(7);
                    SetPieceField(t, id, PF.X, x);
                    SetPieceField(t, id, PF.Y, y);
                    placed.Add(id + "@" + x + "," + y);
                }
                string key = string.Join(" ", placed);
                string h = Hash.HashHex(t);
                if (seen.TryGetValue(h, out var prev)) Assert.AreEqual(key, prev);
                seen[h] = key;
            }
            Assert.Greater(seen.Count, 4000);
        }

        [Test]
        public void HashState_writes_into_a_caller_buffer_and_tables_are_fixed_seed()
        {
            var s = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("B1_0", "W", 0, 0) } });
            var o = new uint[2];
            Assert.AreSame(o, Hash.HashState(s, o));
            var z1 = Hash.BuildZobrist(s.Lvl.Layout, 1);
            var z2 = Hash.BuildZobrist(Builders.Compiled(new LevelSpec { Pieces = new[] { new P("B1_0", "W", 0, 0) } }).Layout, 1);
            CollectionAssert.AreEqual(z1.Pos0, z2.Pos0);
            Assert.AreEqual(12, Hash.Lcm(new[] { 4, 6, 4 }));
            Assert.AreEqual(1, Hash.Lcm(new int[0]));
        }

        [Test]
        public void Zobrist_tables_equal_the_TypeScript_tables_splitmix32_heads()
        {
            var v = TestPaths.Vectors["zobristP1"]!;
            var z = Hash.BuildZobrist(ComputeLayout(new StateCounts(1, 1, 0, 0, 0, 1)), 1);
            Assert.AreEqual(v["lengths"]![0].AsInt, z.Pos0.Length);
            Assert.AreEqual(v["lengths"]![1].AsInt, z.Field0.Length);
            for (int i = 0; i < v["pos0"]!.Count; i++) Assert.AreEqual(v["pos0"]![i].AsString, "0x" + z.Pos0[i].ToString("x8"));
            Assert.AreEqual(v["pos0At240"]!.AsString, "0x" + z.Pos0[240].ToString("x8"));
            Assert.AreEqual(v["pos1First"]!.AsString, "0x" + z.Pos1[0].ToString("x8"));
            for (int i = 0; i < v["field0"]!.Count; i++) Assert.AreEqual(v["field0"]![i].AsString, "0x" + z.Field0[i].ToString("x8"));
            Assert.AreEqual(v["field1First"]!.AsString, "0x" + z.Field1[0].ToString("x8"));
            Assert.AreEqual(80, Hash.PosSite);
            Assert.AreEqual(240, Hash.PosQueue);
        }
    }
}
