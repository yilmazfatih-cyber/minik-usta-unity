#nullable enable
// Port of tests/core/state.test.ts (TECH §2.4, D-052)
using System;
using System.Text.RegularExpressions;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class StateTests
    {
        [Test]
        public void Layout_follows_the_TECH_2_4_table_header_18_yardOcc_60_siteOcc_Sx16_filled_wrongOcc_Sx2_pieces_Px9()
        {
            var L = ComputeLayout(new StateCounts(5, 40, 3, 8, 4, 3));
            Assert.AreEqual(18, HeaderSize);
            Assert.AreEqual(9, PieceStride);
            Assert.AreEqual(18, L.YardOcc);
            Assert.AreEqual(18 + 60, L.SiteOcc);
            Assert.AreEqual(L.SiteOcc + 80, L.Filled);
            Assert.AreEqual(L.Filled + 10, L.WrongOcc);
            Assert.AreEqual(L.WrongOcc + 10, L.Pieces);
            // the fixed part of the typical level: 18 + 60 + 80 + 10 + 10 + 360 = 538 (TECH §2.4)
            Assert.AreEqual(538, L.Pieces + 40 * PieceStride);
            Assert.AreEqual(538 + 9 + 16 + 4 + 3 + 40 + 5, L.Size);
        }

        [Test(Description = "K-25 initial state: batch 0 in the yard, trucks pending, help slots gone, header start values")]
        public void K25_initial_state_batch_0_in_the_yard_trucks_pending_help_slots_gone_header_start_values()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Id = 17,
                Moves = 13,
                Seed = 99,
                Plans = new[] { new[] { "WW", "WW" }, new[] { "YY" } },
                Pieces = new[] { new P("O4_0", "W", 0, 0), new P("D2_90", "Y", 2, 7) },
                Batches = new[] { new BatchSpec { ForSegment = 1, Pieces = new[] { new P("B1_0", "Y", 0, 8) } } },
                Debris = new[] { new D("B1_0", "W", 7, 1) },
                Obstacles = new[]
                {
                    J.O("type", "crate", "x", 4, "y", 0, "hp", 2), J.O("type", "cement_bag", "x", 5, "y", 0), J.O("type", "screw", "x", 0, "y", 0),
                },
            });
            Assert.AreEqual(13, Hdr(s, H.MovesLeft));
            Assert.AreEqual(99, Hdr(s, H.Rng));
            Assert.AreEqual(0, Hdr(s, H.Turn));
            Assert.AreEqual(1, YardOcc(s, 1, 1));
            Assert.AreEqual(2, YardOcc(s, 3, 7));
            Assert.AreEqual(-1, YardOcc(s, 4, 0));
            Assert.AreEqual(-2, YardOcc(s, 5, 0));
            Assert.AreEqual(2, ObstacleField(s, 0, OF.Hp));
            Assert.AreEqual("pending", ReadPiece(s, 2).Zone);
            var debris = ReadPiece(s, 3);
            Assert.AreEqual("site", debris.Zone);
            Assert.AreEqual(7, debris.X);
            Assert.AreEqual(1, debris.Y);
            Assert.AreEqual(0, debris.Seg);
            Assert.IsTrue(debris.Debris);
            Assert.AreEqual("B1_0", debris.Shape);
            Assert.AreEqual("W", debris.Color);
            Assert.AreEqual(4, SiteOcc(s, 0, 1, 1));
            Assert.AreEqual(0b10, WrongOccMask(s, 0, 1));
            // Faz 2R (TECH §2R.1): no D2 help slots; the piece table holds the static pieces only
            CollectionAssert.AreEqual(new[] { 0, 4, 4 }, new[] { s.Lvl.HelpPieceCount, s.Lvl.HelpPieceBase, s.Lvl.Layout.Counts.Pieces });
            Assert.AreEqual(-1, PieceField(s, 0, PF.ArrivedTurn));
            CollectionAssert.AreEqual(new string[0], Grid.StateInvariantErrors(s));
        }

        [Test(Description = "K-24 W4 W5 W7 start values: elevator offset/direction, shutter phase, slider direction, locked closed")]
        public void K24_W4_W5_W7_start_values_elevator_offset_direction_shutter_phase_slider_direction_locked_closed()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Id = 40,
                Wall = new WallSpec
                {
                    Height = 8,
                    Gaps = new[]
                    {
                        J.O("type", "shutter", "y", 0, "size", 1, "period", 2, "phase", 2),
                        J.O("type", "slider", "y", 3, "size", 1, "range", new[] { 2, 4 }, "dir", -1),
                        J.O("type", "locked", "y", 6, "size", 1, "keyId", "a"),
                    },
                },
                Elevator = J.O("range", new[] { 0, 2 }, "start", 2, "dir", 1),
                Pieces = new[] { new P("B1_0", "W", 0, 0) },
            });
            CollectionAssert.AreEqual(new[] { 2, 1 }, new[] { Hdr(s, H.Elev), Hdr(s, H.ElevDir) });
            // OBSTACLES W4: open ⇔ floor((m + phase) / period) even → m 0, phase 2, period 2 → 1 → closed
            Assert.AreEqual(0, GapField(s, 0, GF.Open));
            Assert.AreEqual(2, GapField(s, 0, GF.Phase));
            CollectionAssert.AreEqual(new[] { 1, 3, -1 }, new[] { GapField(s, 1, GF.Open), GapField(s, 1, GF.Y), GapField(s, 1, GF.Phase) });
            Assert.AreEqual(0, GapField(s, 2, GF.Open));
        }

        [Test]
        public void CloneState_changes_to_the_copy_never_leak_into_the_original()
        {
            var s = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("B1_0", "W", 0, 0) } });
            var before = (int[])s.Buf.Clone();
            var c = CloneState(s);
            SetHdr(c, H.Turn, 5);
            SetPieceX(c, 0, 3);
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreSame(s.Lvl, c.Lvl);
        }

        [Test]
        public void EncodeState_DecodeState_round_trip_and_length_check()
        {
            var s = Builders.InitialState(new LevelSpec { Seed = -123456, Pieces = new[] { new P("B1_0", "W", 0, 0), new P("O4_0", "Y", 2, 0) } });
            SetHdr(s, H.Combo, 7);
            string text = EncodeState(s);
            Assert.IsTrue(Regex.IsMatch(text, "^[A-Za-z0-9_-]+$"));
            var back = DecodeState(s.Lvl, text);
            CollectionAssert.AreEqual(s.Buf, back.Buf);
            var other = Builders.Compiled(new LevelSpec { Plans = new[] { new[] { "WW" }, new[] { "WW" } }, Pieces = new[] { new P("B1_0", "W", 0, 0) } });
            Assert.Throws<ArgumentException>(() => DecodeState(other, text));
            Assert.Throws<ArgumentException>(() => DecodeState(s.Lvl, text.Substring(0, text.Length - 1) + "!"));
        }

        [Test(Description = "K-26 queue is FIFO: append at the end, removal keeps the order of the others")]
        public void K26_queue_is_FIFO_append_at_the_end_removal_keeps_the_order_of_the_others()
        {
            var s = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("B1_0", "W", 0, 0), new P("B1_0", "W", 1, 0), new P("B1_0", "W", 2, 0) } });
            foreach (int id in new[] { 2, 0, 1 })
            {
                s.Buf[s.Lvl.Layout.Pieces + id * PieceStride + PF.Zone] = Zone.Gone;
                EnqueuePiece(s, id);
            }
            CollectionAssert.AreEqual(new[] { 2, 0, 1 }, QueueIds(s));
            Assert.AreEqual("queue", ReadPiece(s, 0).Zone);
            Assert.AreEqual(0, RemoveQueueAt(s, 1));
            CollectionAssert.AreEqual(new[] { 2, 1 }, QueueIds(s));
            Assert.AreEqual(2, Hdr(s, H.QueueLen));
        }

        [Test]
        public void Piece_flag_bits_round_trip_and_setFlag_toggles_one_bit()
        {
            CollectionAssert.AreEqual(new[] { "glass", "locked", "stuck" }, BitsToFlags(FlagsToBits(new[] { "glass", "stuck", "locked" })));
            var s = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("B1_0", "W", 0, 0, new[] { "glass" }) } });
            Assert.IsTrue(HasFlag(s, 0, FlagBit.Glass));
            SetFlag(s, 0, FlagBit.Locked, true);
            SetFlag(s, 0, FlagBit.Glass, false);
            Assert.AreEqual(FlagBit.Locked, PieceField(s, 0, PF.Flags));
            Assert.AreEqual(FlagBit.Locked, FlagBit.Of("locked"));
            Assert.Throws<ArgumentOutOfRangeException>(() => FlagBit.Of("shiny"));
        }
    }
}
