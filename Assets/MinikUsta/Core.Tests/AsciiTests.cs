#nullable enable
// Port of tests/core/ascii.test.ts (TECH Appendix A)
using System;
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class AsciiTests
    {
        private static void ToSite(GameState s, int id, int x, int y, bool locked = true)
        {
            Grid.VacatePiece(s, id);
            SetPieceField(s, id, PF.Zone, Zone.Site);
            SetPieceField(s, id, PF.X, x);
            SetPieceField(s, id, PF.Y, y);
            SetPieceField(s, id, PF.Seg, 0);
            SetFlag(s, id, FlagBit.Locked, locked);
            Grid.OccupyPiece(s, id);
            Grid.RefreshSiteMasks(s, 0);
        }

        /// <summary>TECH Appendix A example board (BRIEF §12 level 4 draft: wall 6, gap y 3 size 1).</summary>
        private static GameState AppendixState()
        {
            var s = Builders.InitialState(new LevelSpec
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
            ToSite(s, 4, 6, 0);
            ToSite(s, 5, 6, 1);
            ToSite(s, 6, 6, 2);
            SetHdr(s, H.Turn, 3);
            SetHdr(s, H.MovesLeft, 11);
            return s;
        }

        [Test]
        public void Prints_the_Appendix_A_example_exactly()
        {
            Builders.ExpectAscii(AppendixState(), @"
                L4 turn 3 moves 11 seg 1/1 elev 0
                 y  0 1 2 3 4 5 | W | 6 7
                 9  . . . . . . | : | . .
                 8  . . . . . . | : | . .
                 7  y y w . . . | : | . .
                 6  . . w . . . | : | . .
                 5  . . . g g . | # | . .
                 4  . . . g g 2 | # | . .
                 3  . . . . . w | = | W .
                 2  % . . . . . | # | W .
                 1  . . . . . . | # | W W
                 0  . . . . . . | # | Y Y
                plan seg0 (top→bottom): YY WW W. WW YY
                hidden: screw@(3,4) key:a@(5,3)
                ");
        }

        [Test]
        public void Ascii_bag_and_blue_block_round_trip()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Id = 21, Plan = new[] { "BB" }, Pieces = new[] { new P("B1_0", "B", 1, 0) }, Obstacles = new[] { J.O("type", "cement_bag", "x", 0, "y", 0) },
            });
            var colour = Ascii.ParseAscii(Ascii.ToAscii(s));
            CollectionAssert.AreEqual(new[] { "%", "b" }, colour.Cells[0].Take(2).ToArray());
            string idsText = Ascii.ToAscii(s, true);
            StringAssert.Contains("obstacles: bag@(0,0)", idsText);
            CollectionAssert.AreEqual(new[] { ".", "0" }, Ascii.ParseAscii(idsText).Cells[0].Take(2).ToArray());
            var back = Ascii.FromAscii(s.Lvl, idsText);
            CollectionAssert.AreEqual(s.Buf, back.Buf);
            Assert.AreEqual(Ascii.ToAscii(s), Ascii.ToAscii(back));
        }

        [Test]
        public void Ids_mode_round_trips_locked_stuck_queue_trowel_destroyed_crates_and_collected_items()
        {
            var s = AppendixState();
            // stuck mortar on the window cell (7,2), one block in the truck queue, a trowel cell (7,3), crate gone, screw collected
            ToSite(s, 3, 7, 2, false);
            SetFlag(s, 3, FlagBit.Stuck, true);
            Grid.RefreshSiteMasks(s, 0);
            Grid.VacatePiece(s, 0);
            EnqueuePiece(s, 0);
            SetSiteOcc(s, 0, 1, 3, SiteTrowel);
            Grid.RefreshSiteMasks(s, 0);
            SetObstacleField(s, 0, OF.Hp, 0);
            SetYardOcc(s, 5, 4, 0);
            SetObstacleField(s, 2, OF.Hp, 0);
            SetHiddenCollected(s, 0, true);
            string text = Ascii.ToAscii(s, true);
            StringAssert.Contains("queue: 0", text);
            StringAssert.Contains("locked: 4 5 6", text);
            StringAssert.Contains("stuck: 3", text);
            StringAssert.Contains("hidden: key:a@(5,3)", text);
            var back = Ascii.FromAscii(s.Lvl, text);
            Assert.AreEqual(text, Ascii.ToAscii(back, true));
            CollectionAssert.AreEqual(s.Buf, back.Buf);
        }

        [Test(Description = "wall column: closed gap x, K-40 open shutter =, platform _ under the elevator offset")]
        public void K40_wall_column_closed_gap_x_open_shutter_equals_platform_under_the_elevator_offset()
        {
            var s = Builders.InitialState(new LevelSpec
            {
                Id = 37,
                Wall = new WallSpec { Height = 6, Gaps = new[] { J.O("type", "shutter", "y", 1, "size", 1, "period", 1, "phase", 1) } },
                Plan = new[] { "WW" },
                Elevator = J.O("range", new[] { 0, 1 }, "start", 1, "dir", 1),
                Pieces = new[] { new P("B1_0", "W", 0, 0) },
            });
            string[] Wall(GameState t) => Ascii.ParseAscii(Ascii.ToAscii(t)).Wall;
            CollectionAssert.AreEqual(new[] { "#", "x", "#", "#", "#", "#", ":", ":", ":", ":" }, Wall(s));
            SetHdr(s, H.OpenShutterUntil, 5);
            Assert.AreEqual("=", Wall(s)[1]);
            var board = Ascii.ParseAscii(Ascii.ToAscii(s));
            CollectionAssert.AreEqual(new[] { "_", "_", "." }, new[] { board.Cells[0][6], board.Cells[0][7], board.Cells[1][6] });
            Assert.AreEqual(1, board.Elev);
        }

        [Test]
        public void ParseAscii_rejects_malformed_text_and_fromAscii_needs_ids_mode()
        {
            Assert.Throws<FormatException>(() => Ascii.ParseAscii("nonsense"));
            var s = Builders.InitialState(new LevelSpec { Pieces = new[] { new P("B1_0", "W", 0, 0) } });
            string text = Ascii.ToAscii(s);
            Assert.Throws<FormatException>(() => Ascii.ParseAscii(string.Join("\n", text.Split('\n').Take(5))));
            var ex = Assert.Throws<InvalidOperationException>(() => Ascii.FromAscii(s.Lvl, text));
            StringAssert.Contains("ids mode", ex!.Message);
        }
    }
}
