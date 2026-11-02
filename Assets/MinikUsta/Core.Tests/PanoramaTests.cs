#nullable enable
// Port of tests/core/panorama.test.ts: segments are completed through `Moves.ApplyMove` (`Run`, P3), as in the TS file; the
// level-5 test replays the canonical moves 1–5.
using System.Linq;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class PanoramaTests
    {
        private static string[] Statuses(GameState s) => Panorama.PanoramaView(s).Select(p => p.Status).ToArray();

        /// <summary>TS `run(s, dragTo(id, ix, iy))`: the full K-35 pipeline.</summary>
        private static void Place(GameState s, int id, int ix, int iy) => Run(s, DragTo(id, ix, iy));

        private static string RowsText(PanoramaSegment p) => string.Join("/", p.Rows.Select(r => string.Join("", r)));

        [Test(Description = "K-06 GDD example: 3 segments, after segment 1 the panorama reads [done][active][future]")]
        public void K06_GDD_example_3_segments_after_segment_1_the_panorama_reads_done_active_future()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plans = new[] { new[] { "WW" }, new[] { "YY" }, new[] { "WW" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "Y", 2, 0), Pc("D2_90", "W", 4, 0)),
            });
            CollectionAssert.AreEqual(new[] { "active", "future", "future" }, Statuses(s));
            Place(s, 0, 6, 8);
            Assert.AreEqual(1, Hdr(s, H.ActiveSeg));
            CollectionAssert.AreEqual(new[] { "done", "active", "future" }, Statuses(s));
            var rows = Panorama.PanoramaView(s).Select(p => p.Rows).ToArray();
            CollectionAssert.AreEqual(new[] { "W", "W" }, rows[0][0]);
            CollectionAssert.AreEqual(new[] { "Y", "Y" }, rows[1][0]);
            CollectionAssert.AreEqual(new[] { "W", "W" }, rows[2][0]);
            Assert.IsTrue(rows.All(r => r.Length == 1));
        }

        [Test(Description = "K-06 hidden `?` cells show `?` in the panorama until they are revealed (K-32)")]
        public void K06_hidden_cells_show_question_mark_in_the_panorama_until_they_are_revealed()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "??", "WY" },
                Hidden = Repeat1,
                // a fourth block stays in the yard: a holdable block keeps K-30 D1 (RulesVersion 3: Söküm) away
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "W", 2, 0), Pc("B1_0", "Y", 3, 0)),
            });
            var before = Panorama.PanoramaView(s)[0].Rows;
            CollectionAssert.AreEqual(new[] { "?", "?" }, before[0]);
            CollectionAssert.AreEqual(new[] { "W", "Y" }, before[1]);
            Place(s, 0, 6, 8);
            Place(s, 1, 7, 8);
            Place(s, 2, 6, 8); // W on the hidden (6,1): correct, opens it
            var after = Panorama.PanoramaView(s)[0].Rows;
            CollectionAssert.AreEqual(new[] { "W", "?" }, after[0]);
            CollectionAssert.AreEqual(new[] { "W", "Y" }, after[1]);
        }

        [Test(Description = "K-06 reading the panorama never changes the game state (bit-identical buffer, same m)")]
        public void K06_reading_the_panorama_never_changes_the_game_state()
        {
            var s = LevelState(5);
            foreach (var m in HandMoves(5).Take(5)) Run(s, new DragMove(m.PieceId, m.To)); // Sol Oda complete (LEVELS §2 Bölüm 5 move 5)
            var before = Snapshot(s);
            var views = new[] { Panorama.PanoramaView(s), Panorama.PanoramaView(s), Panorama.PanoramaView(s) };
            CollectionAssert.AreEqual(before, s.Buf);
            Assert.AreEqual(5, Hdr(s, H.Turn));
            CollectionAssert.AreEqual(views[0].Select(v => v.Index + ":" + v.Status + ":" + RowsText(v)).ToArray(), views[1].Select(v => v.Index + ":" + v.Status + ":" + RowsText(v)).ToArray());
            CollectionAssert.AreEqual(
                new[] { "0:done:WW/WY/WY/RR/RR", "1:active:RR/RR/WW/YY/YY" },
                views[0].Select(v => v.Index + ":" + v.Status + ":" + RowsText(v)).ToArray());
        }

        [Test(Description = "K-06 an S2 window cell stays `.` in the panorama (builder board: S2 is outside the Faz 2R MVP, CL-2R-12)")]
        public void K06_an_S2_window_cell_stays_dot_in_the_panorama()
        {
            var view = Panorama.PanoramaView(Init(new LevelSpec
            {
                Wall = Wall(2),
                Plan = new[] { "WW", "W.", "WW" },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("B1_0", "W", 2, 0), Pc("D2_90", "W", 3, 0)),
            }));
            Assert.AreEqual(1, view.Length);
            Assert.AreEqual(0, view[0].Index);
            Assert.AreEqual("active", view[0].Status);
            Assert.AreEqual(3, view[0].Rows.Length);
            CollectionAssert.AreEqual(new[] { "W", "W" }, view[0].Rows[0]);
            CollectionAssert.AreEqual(new[] { "W", "." }, view[0].Rows[1]);
            CollectionAssert.AreEqual(new[] { "W", "W" }, view[0].Rows[2]);
        }
    }
}
