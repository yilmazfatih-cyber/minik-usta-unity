#nullable enable
// Port of tests/core/panorama.test.ts — the parts that need no move pipeline. The TS file completes segments through
// `applyMove`; here the same board states are reached with SettlePlacement + the site strategy (the K-35 package replays
// the originals). The level-5 HAND replay test comes with that package.
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

        private static void Place(GameState s, int id, int ix, int iy)
        {
            var fall = Gravity.ComputeFall(s, id, N(ix, iy));
            Assert.AreEqual(PlacementOutcome.KindCorrect, Placement.SettlePlacement(s, id, fall.Landing, fall.Verdict).Kind, "piece " + id);
            Site.SiteStrategy(s.Lvl).CompleteIfDone(s);
        }

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
                Pieces = Ps(Pc("B1_0", "W", 0, 0), Pc("B1_0", "Y", 1, 0), Pc("B1_0", "W", 2, 0)),
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

        [Test(Description = "K-06 reading the panorama never changes the game state (bit-identical buffer)")]
        public void K06_reading_the_panorama_never_changes_the_game_state()
        {
            var s = Init(new LevelSpec
            {
                Wall = Wall(2),
                Plans = new[] { new[] { "WW" }, new[] { "YY" } },
                Pieces = Ps(Pc("D2_90", "W", 0, 0), Pc("D2_90", "Y", 2, 0)),
            });
            Place(s, 0, 6, 8);
            var before = Snapshot(s);
            var views = new[] { Panorama.PanoramaView(s), Panorama.PanoramaView(s), Panorama.PanoramaView(s) };
            CollectionAssert.AreEqual(before, s.Buf);
            CollectionAssert.AreEqual(views[0].Select(v => v.Status).ToArray(), views[1].Select(v => v.Status).ToArray());
            CollectionAssert.AreEqual(new[] { "done", "active" }, Statuses(s));
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
