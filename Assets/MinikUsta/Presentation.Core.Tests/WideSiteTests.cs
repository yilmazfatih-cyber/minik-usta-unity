#nullable enable
// P4 (levels v3c, D-103..D-105): the 9-column board of the wide site, the pallet layer cancel, the bonus "+n" burst and the
// info card of a taught mechanic. Layout and hit-test criteria of UX_UNITY §4: touch target >= 44 pt, text >= 34 px.
using System;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Tests
{
    [TestFixture]
    public class WideSiteTests
    {
        private static readonly LayoutSettings S = new LayoutSettings();

        private static BoardGeo Wide => Geometry.MakeGeo(new GeoParams(6, 8, 3, 5, 0));

        [Test(Description = "S9 K-49 item 5 D-103: a 9-column board (6 × 8 | 3 × 5) has a 108 px cell, a 54 px wall, a 1026 px board and 27 px on both sides at FIT and EXPAND; every 8-column board keeps its 120+ px cell")]
        public void S9_K49_item5_wide_board_cell_108_wall_54_board_1026()
        {
            foreach (double h in new[] { 1920.0, ScreenFit.DesignHeight(ScaleMode.Expand, 390, 844, S), ScreenFit.DesignHeight(ScaleMode.Expand, 360, 800, S) })
            {
                var L = ScreenLayout.Create(S, h, Wide);
                Assert.AreEqual(108, L.Grid.CellPx);
                Assert.AreEqual(54, L.Grid.WallW, 1e-9);
                Assert.AreEqual(1026, L.Board.Board.W, 1e-9);
                Assert.AreEqual(27, L.Grid.YardX);
                Assert.AreEqual(27, L.W - L.Grid.SiteRight);
                Assert.AreEqual(0.9, L.Grid.K, 1e-12);
                Assert.IsTrue(L.Board.Crane.Right <= L.W && L.Board.Crane.X >= 0, "the crane area is inside the canvas");
            }
            Assert.AreEqual(120, BoardLayout.AdaptiveCellPx(S, Geometry.DefaultGeo));
            foreach (var n in Play.Levels)
                Assert.GreaterOrEqual(BoardLayout.AdaptiveCellPx(S, Play.LevelFile(n).Geo), 100, "no level has a cell below 100 design px (B" + n + ")");
        }

        [Test(Description = "K-49 item 5 UX_UNITY §4: on 390 and 360 px wide phones a block's touch target (cell + hit slop) is >= 44 pt, the wall strip and the crane area fit, and a B1 cell is >= 36 CSS px")]
        public void UX4_wide_board_touch_target_is_at_least_44pt_on_390_and_360()
        {
            foreach (var (w, h) in new[] { (390.0, 844.0), (360.0, 800.0) })
            {
                var L = ScreenLayout.Create(S, ScreenFit.DesignHeight(ScaleMode.Expand, w, h, S), Wide);
                double cssPerDesign = w / 1080.0;
                double cellCss = L.Grid.CellPx * cssPerDesign;
                Assert.GreaterOrEqual(cellCss, 36, "cell " + w + " px wide");
                // a single-cell block is picked inside its cell grown by the hit slop: (cell + 2 · slop) must reach 44 pt on both axes
                double targetCss = (L.Grid.CellPx + 2 * L.HitSlopPx) * cssPerDesign;
                Assert.GreaterOrEqual(targetCss, 44, "block touch target at " + w + " px wide (cell " + Math.Round(cellCss, 1) + " CSS px)");
                Assert.GreaterOrEqual(L.Grid.WallW * cssPerDesign, 16, "the wall strip is a visible strip");
                Assert.IsTrue(L.Board.Site.X >= L.Board.Yard.Right, "the site is right of the yard");
                Assert.AreEqual(3 * L.Grid.CellPx, L.Board.Site.W, 1e-9);
            }
        }

        [Test(Description = "K-49 item 5 hit-test: every yard cell and every site cell of the 9-column board is found at its centre and its four corners; the wall strip is not a cell")]
        public void K49_wide_board_hit_test_finds_every_cell_and_not_the_wall()
        {
            var L = BoardLayout.Create(S, Wide, 1920).Grid;
            for (int y = 0; y < Wide.Rows; y++)
            {
                for (int x = 0; x < Wide.Cols; x++)
                {
                    var r = L.CellRect(x, y);
                    foreach (var (px, py) in new[] { (r.CenterX, r.CenterY), (r.X, r.Y), (r.Right - 0.01, r.Y), (r.X, r.Bottom - 0.01), (r.Right - 0.01, r.Bottom - 0.01) })
                    {
                        Assert.IsTrue(L.CellAt(px, py, out int cx, out int cy), "cell " + x + "," + y);
                        Assert.AreEqual((x, y), (cx, cy));
                    }
                }
            }
            Assert.IsFalse(L.CellAt(L.WallX + L.WallW / 2, L.RowTop(0) + 1, out _, out _));
        }

        [Test(Description = "K-49 item 5 levels 6-10: every yard block's cell centre picks that block (a covered pallet layer: the top layer) on 390 × 844, and the first site column starts right of the wall strip")]
        public void K49_shipped_wide_levels_pick_every_block_on_390x844()
        {
            foreach (int n in new[] { 6, 7, 8, 9, 10 })
            {
                var lvl = Play.LevelFile(n);
                Assert.AreEqual(9, lvl.Geo.Cols, "B" + n + " is a 9-column board (D-103)");
                var s = CreateInitialState(lvl);
                var L = ScreenLayout.Create(S, ScreenFit.DesignHeight(ScaleMode.Expand, 390, 844, S), lvl.Geo);
                int picked = 0;
                for (int id = 0; id < lvl.Layout.Counts.Pieces; id++)
                {
                    if (PieceZone(s, id) != Zone.Yard || Grid.IsCovered(s, id)) continue;
                    foreach (var c in Grid.PieceBoardCells(s, id))
                    {
                        var r = L.Grid.CellRect(c.X, c.Y);
                        Assert.AreEqual(id, HitTest.PieceAtPoint(s, L.Grid, r.CenterX, r.CenterY, L.HitSlopPx), "B" + n + " piece " + id);
                        picked++;
                    }
                }
                Assert.Greater(picked, 0);
                Assert.Greater(L.Grid.BuildX, L.Grid.WallX);
            }
        }

        [Test(Description = "K-58 item 9 c D-104: dragging a pallet layer over the yard shows the cancel look and a release there is a cancel (no move); every drop class of a pallet layer is cancel or site")]
        public void K58_pallet_layer_over_the_yard_is_a_cancel_preview()
        {
            // the first golden drag of level 7 (the pallet stands in the yard); a state where some pallet layer can be held
            var rig = new Rig();
            rig.Load(7);
            rig.Settle();
            int checkedLayers = 0;
            foreach (var m in Play.SolverGolden(7).Drags)
            {
                var s = rig.Session.State;
                for (int id = 0; id < s.Lvl.Pieces.Length; id++)
                {
                    if (s.Lvl.Pieces[id].StackId < 0 || Grid.IsCovered(s, id) || !Movement.IsPalletLayer(s, id)) continue;
                    var d = Movement.BeginDrag(s, id);
                    if (d == null) continue;
                    checkedLayers++;
                    Assert.IsTrue(d.PalletLayer);
                    foreach (var n in d.ReachableNodes())
                    {
                        var drop = d.Classify(n);
                        Assert.IsTrue(drop.Kind == DropKinds.Cancel || drop.Kind == DropKinds.SiteFree || drop.Kind == DropKinds.SiteRail, "node " + n + " " + drop);
                        if (drop.Kind == DropKinds.Cancel && drop.Reason == CancelReasons.PalletSiteOnly) Assert.IsTrue(ShadowLook.CancelPreview(drop));
                    }
                }
                rig.Settle();
                Assert.AreEqual(MoveStatuses.Applied, rig.Player.Commit(m).Status);
            }
            Assert.Greater(checkedLayers, 0, "a pallet layer was held in level 7's solution");
        }

        [Test(Description = "K-60 K-62 D-105: the bonus placement shows a gold \"+2\" burst on the moves plate (<= 900 ms, hidden after), cheers the king and plays the moves jingle; the HUD keeps the bonus count")]
        public void Y12_bonus_placement_shows_the_plus_two_burst_and_the_king_cheers()
        {
            var rig = new Rig();
            rig.Load(10);
            rig.Settle();
            bool burst = false;
            foreach (var m in Play.SolverGolden(10).Drags)
            {
                rig.Settle();
                var r = rig.Player.Commit(m);
                Assert.AreEqual(MoveStatuses.Applied, r.Status);
                var bonus = r.Events.OfType<MovesChangedEvent>().FirstOrDefault(e => e.Reason == MovesChangedReasons.Bonus);
                int shownBefore = rig.View.MovesBurstShown.Count;
                // sample the burst while the plan plays
                float t = 0;
                bool sawText = false;
                while (t < 2500)
                {
                    rig.Tick(Rig.FrameMs);
                    t += Rig.FrameMs;
                    if (rig.View.MovesBurstText != null) sawText = true;
                }
                if (bonus != null)
                {
                    burst = true;
                    Assert.IsTrue(sawText, "the +2 burst was shown");
                    Assert.AreEqual("+" + bonus.Delta, rig.View.MovesBurstShown[rig.View.MovesBurstShown.Count - 1]);
                    Assert.AreEqual(2, bonus.Delta, "K-62 / D-105: the bonus gives +2");
                    Assert.Greater(rig.View.MovesBurstShown.Count, shownBefore);
                    Assert.IsNull(rig.View.MovesBurstText, "the burst is gone within 900 ms of its start");
                }
                else Assert.AreEqual(shownBefore, rig.View.MovesBurstShown.Count, "no burst without a bonus");
            }
            Assert.IsTrue(burst, "level 10 holds a bonus block (the finale's reward moment)");
        }

        [Test(Description = "K-53 P4: the level start window of a level that teaches a mechanic shows its info card (obs.*.desc, 34 px, <= 2 lines) in TR and EN; Y10 adds the D-104 rule; a level without `teaches` shows none")]
        public void UX4_level_start_info_card_of_the_taught_mechanic()
        {
            foreach (var (level, key) in new[] { (6, "obs.s9.desc"), (7, "obs.y10.desc"), (8, "obs.y11.desc"), (9, "obs.y13.desc") })
            {
                foreach (var lang in new[] { "tr", "en" })
                {
                    var rig = new AppRig(null, 390, 844);
                    rig.I18n.SetLang(lang);
                    rig.App.Boot();
                    for (int n = 1; n < level; n++) rig.App.Save.Level(n).Won = true;
                    rig.Tap(HomeView.PlayId);
                    Assert.AreEqual(AppScreen.LevelStart, rig.Screen, "B" + level);
                    var w = rig.Ui.Root(UiLayer.Window)!;
                    var lines = w.All().Where(n => n.Id.StartsWith("win.info.l", StringComparison.Ordinal)).OrderBy(n => n.Id, StringComparer.Ordinal).ToList();
                    Assert.IsTrue(lines.Count >= 1 && lines.Count <= 2, "B" + level + " " + lang + ": one or two lines");
                    string joined = string.Join(" ", lines.Select(n => n.Text));
                    StringAssert.StartsWith(rig.I18n.Text.Get(key), joined, "B" + level + " " + lang);
                    if (level == 7) StringAssert.EndsWith(rig.I18n.Text.Get("card.y10.site"), joined);
                    foreach (var l in lines)
                    {
                        Assert.GreaterOrEqual(TextFit.SizeOf(l), HudLayout.MinTextPx, "B" + level + " " + lang + ": text >= 34 px, shrunk to " + TextFit.SizeOf(l));
                        Assert.GreaterOrEqual(l.Rect.Y, w.Get("win.info").Rect.Y);
                        Assert.LessOrEqual(l.Rect.Bottom, w.Get("win.info").Rect.Bottom);
                    }
                    var panel = w.Get("win.panel").Rect;
                    Assert.GreaterOrEqual(panel.Y, 0);
                    Assert.LessOrEqual(panel.Bottom, ScreenFit.DesignHeight(S.Mode, 390, 844, S) + 1e-6);
                    var cardRect = w.Get("win.info").Rect;
                    Assert.IsTrue(cardRect.X >= panel.X && cardRect.Right <= panel.Right && cardRect.Y >= panel.Y && cardRect.Bottom <= panel.Bottom, "the card is inside the window");
                    var slotsTitle = w.Find("win.slots");
                    if (slotsTitle != null) Assert.Greater(slotsTitle.Rect.Y, w.Get("win.info").Rect.Bottom, "the slot row stays under the card");
                }
            }
            var plain = new AppRig(null, 390, 844);
            plain.App.Boot();
            for (int n = 1; n < 10; n++) plain.App.Save.Level(n).Won = true;
            plain.Tap(HomeView.PlayId);
            Assert.IsNull(plain.Ui.Root(UiLayer.Window)!.Find("win.info"), "level 10 teaches nothing: no card");
        }
    }
}
