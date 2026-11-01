#nullable enable
// Paket C1: the light tutorial (K-53; GDD §14.1; UX_FLOWS §13.1). The controller, the presence, the events, the glove and the layout are pure
// and are tested on the real compiled levels 1-10; the integration tests run the whole game on fakes (`AppRig`) and read the tutorial layer
// the way a finger and an eye would. Every test name starts with the rule it covers.
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Tutorial;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class TutorialTests
    {
        private sealed class Host : ITutorialHost
        {
            public GameState? State;
            public readonly List<string> Tips = new List<string>();
            public readonly List<int> Ended = new List<int>();
            public readonly List<int> Progress = new List<int>();

            public GameState? CurrentState => State;
            public void MarkContextTip(string topic) => Tips.Add(topic);
            public void StepEnded(int step, float now) => Ended.Add(step);
            public void Progressed(int step, float now) => Progress.Add(step);
        }

        private static (TutorialController tut, Host host, CompiledLevel lvl) Make(int level)
        {
            var lvl = Play.LevelFile(level);
            var host = new Host { State = State.CreateInitialState(lvl) };
            var tut = TutorialFactory.Create(lvl, host, false)!;
            tut.Start(0);
            return (tut, host, lvl);
        }

        private static GameEvent Ev(GameEvent e, int step)
        {
            e.Step = step;
            return e;
        }

        private static AppRig Booted(MemorySaveStore? store = null)
        {
            var rig = new AppRig(store);
            rig.App.Boot();
            return rig;
        }

        // --- K-53/1-3 the controller -----------------------------------------------------------------------------------------------

        [Test]
        public void K53_at_most_two_steps_and_every_step_is_soft_in_the_shipped_levels()
        {
            foreach (int n in Play.Levels)
            {
                var steps = Play.LevelFile(n).Data.Tutorial ?? new TutorialStepData[0];
                Assert.LessOrEqual(steps.Length, 2, "level " + n);
                foreach (var st in steps) Assert.That(st.Mode, Is.Null.Or.EqualTo("soft"), "level " + n + " step " + st.Step);
            }
        }

        [Test]
        public void K53_the_first_step_starts_with_the_level_and_a_step_with_startOn_waits()
        {
            var (a, _, _) = Make(1);
            Assert.IsNotNull(a.Current, "level 1 has no startOn: step 1 is active at the start");
            Assert.AreEqual(1, a.Current!.Data.Step);
            var (b, _, _) = Make(4);
            Assert.IsNotNull(b.Current);
            b.MoveEnded(new GameEvent[] { Ev(new PieceMovedEvent(2, At.Yard(1, 1), At.Yard(2, 1), EntryKinds.Yard), 1) }, 10);
            // level 4: step 1 done = yardMove piece:4 (the filter names piece 4); a move of piece 2 leaves step 1 active
            Assert.AreEqual(1, b.Current!.Data.Step, "K-53 step done ignores other piece");
        }

        [Test]
        public void K53_step_done_ignores_other_piece_and_ends_on_the_named_piece()
        {
            var (tut, host, lvl) = Make(3);
            int named = lvl.TutorialPieceIds["piece:2"];
            int other = named == 0 ? 1 : 0;
            tut.MoveEnded(new GameEvent[] { Ev(new PieceMovedEvent(other, At.Yard(0, 0), At.Yard(1, 0), EntryKinds.Yard), 1) }, 100);
            Assert.AreEqual(1, tut.Current!.Data.Step, "a yard move of another block does not end step 1");
            Assert.IsEmpty(host.Ended);
            tut.MoveEnded(new GameEvent[] { Ev(new PieceMovedEvent(named, At.Yard(0, 3), At.Yard(1, 3), EntryKinds.Yard), 1) }, 200);
            Assert.AreEqual(new[] { 1 }, host.Ended.ToArray());
            Assert.AreEqual(2, tut.Current!.Data.Step, "the next step starts when the previous ended");
        }

        [Test]
        public void K53_a_step_ends_only_on_its_done_event_the_counter_starts_at_zero_and_count_defaults_to_one()
        {
            var (tut, host, _) = Make(1); // step 1: placementCorrect
            // earlier events never count: a tap or a drag signal does nothing for a placementCorrect step
            tut.DragSignal(TutorialEvents.OverWall, 5);
            tut.TappedPiece(0, 5);
            tut.MoveEnded(new GameEvent[] { Ev(new MovesChangedEvent(5, -1, "move", null), 12) }, 6);
            Assert.AreEqual(1, tut.Current!.Data.Step);
            tut.MoveEnded(new GameEvent[] { Ev(new PlacementCorrectEvent(0, new[] { At.Site(0, 0, 0) }, true), 3) }, 20);
            Assert.AreEqual(new[] { 1 }, host.Ended.ToArray());
        }

        [Test]
        public void K53_a_move_end_counts_at_most_once_per_move()
        {
            var (tut, host, _) = Make(1);
            var two = new GameEvent[]
            {
                Ev(new PlacementCorrectEvent(0, new[] { At.Site(0, 0, 0) }, true), 3),
                Ev(new PlacementCorrectEvent(1, new[] { At.Site(1, 0, 0) }, true), 3),
            };
            tut.MoveEnded(two, 1);
            Assert.AreEqual(new[] { 1 }, host.Ended.ToArray(), "two placements in one move are one count: step 1 (count 1) ended, step 2 (count 1) did not");
            Assert.AreEqual(2, tut.Current!.Data.Step);
        }

        [Test]
        public void K53_a_drag_signal_counts_once_per_drag_even_when_the_drag_is_cancelled_later()
        {
            var (tut, host, _) = Make(6); // step 2: overWall; step 1 first
            tut.MoveEnded(new GameEvent[] { Ev(new PieceMovedEvent(2, At.Yard(1, 4), At.Yard(2, 3), EntryKinds.Yard), 1) }, 1);
            Assert.AreEqual(2, tut.Current?.Data.Step, "step 1 ended on the yard move to (2,3)");
            tut.DragStarted();
            tut.DragSignal(TutorialEvents.OverWall, 10);
            Assert.AreEqual(new[] { 1, 2 }, host.Ended.ToArray());
            Assert.IsTrue(tut.Finished);
        }

        [Test]
        public void K53_a_startOn_that_never_happens_hides_its_step_and_Stop_ends_the_tutorial()
        {
            var (tut, host, _) = Make(7); // step 1 placementCorrect, step 2 startOn segmentDone
            Assert.AreEqual(1, tut.Current!.Data.Step);
            tut.MoveEnded(new GameEvent[] { Ev(new PlacementCorrectEvent(4, new[] { At.Site(0, 0, 0) }, true), 3) }, 5);
            Assert.IsNull(tut.Current, "step 2 waits for segmentDone: nothing is active");
            Assert.IsNotNull(tut.Waiting);
            Assert.AreEqual(2, tut.Waiting!.Step);
            tut.MoveEnded(new GameEvent[] { Ev(new SegmentCompletedEvent(0), 8) }, 9);
            Assert.AreEqual(2, tut.Current!.Data.Step, "the start event activates the step");
            tut.Stop();
            Assert.IsTrue(tut.Finished);
            Assert.IsNull(tut.Current);
            Assert.AreEqual(new[] { 1 }, host.Ended.ToArray());
        }

        [Test]
        public void K53_no_tutorial_on_replay_of_a_won_level_and_none_without_steps()
        {
            var lvl = Play.LevelFile(1);
            Assert.IsNull(TutorialFactory.Create(lvl, new Host(), true), "K-53/6: the save marks the level won");
            Assert.IsNotNull(TutorialFactory.Create(lvl, new Host(), false));
            var j = PresentationPaths.LoadLevelJson(1);
            j.Remove("tutorial");
            var noSteps = Play.FromJson(j.ToString());
            Assert.IsNull(TutorialFactory.Create(noSteps, new Host(), false), "a level without tutorial[] has no tutorial");
        }

        [Test]
        public void K53_a_context_text_marks_its_tip_when_the_step_starts()
        {
            var lvl = Play.LevelFile(1);
            var host = new Host { State = State.CreateInitialState(lvl) };
            var tut = TutorialFactory.Create(lvl, host, false)!;
            tut.Start(0);
            Assert.IsEmpty(host.Tips, "tut.m.* steps are not context tips");
        }

        // --- tutorialEvents --------------------------------------------------------------------------------------------------------

        [Test]
        public void K53_event_matching_covers_the_closed_vocabulary_with_its_filters()
        {
            var lvl = Play.LevelFile(1);
            var s = State.CreateInitialState(lvl);
            TutCondition C(string ev, int[]? at = null, string? piece = null, bool? hidden = null, bool? painted = null, string? flag = null, bool? wind = null, string? type = null) =>
                new TutCondition(ev, null, null, at, piece, hidden, painted, flag, wind, type);
            GameEvent[] none = new GameEvent[0];
            Assert.IsFalse(TutorialEvents.MoveMatches(C("placementCorrect"), none, s));
            var place = Ev(new PlacementCorrectEvent(0, new[] { At.Site(2, 1, 0), At.Site(3, 1, 0) }, true), 3);
            Assert.IsTrue(TutorialEvents.MoveMatches(C("placementCorrect"), new[] { place }, s));
            Assert.IsTrue(TutorialEvents.MoveMatches(C("placementCorrect", at: new[] { 2, 1 }), new[] { place }, s), "`at` is the anchor: min x, min y");
            Assert.IsFalse(TutorialEvents.MoveMatches(C("placementCorrect", at: new[] { 3, 1 }), new[] { place }, s));
            Assert.IsFalse(TutorialEvents.MoveMatches(C("placementCorrect", hidden: true), new[] { place }, s), "hidden needs a cellsRevealed event");
            var late = Ev(new PlacementCorrectEvent(0, new[] { At.Site(2, 1, 0) }, true), 5);
            Assert.IsFalse(TutorialEvents.MoveMatches(C("placementCorrect"), new[] { late }, s), "only pipeline step 3 counts");
            var moved = Ev(new PieceMovedEvent(1, At.Yard(0, 0), At.Yard(2, 3), EntryKinds.Yard), 1);
            Assert.IsTrue(TutorialEvents.MoveMatches(C("yardMove"), new[] { moved }, s));
            Assert.IsTrue(TutorialEvents.MoveMatches(C("yardMove", at: new[] { 2, 3 }), new[] { moved }, s));
            Assert.IsFalse(TutorialEvents.MoveMatches(C("yardMove", at: new[] { 0, 0 }), new[] { moved }, s));
            Assert.IsFalse(TutorialEvents.MoveMatches(C("yardMove", painted: true), new[] { moved }, s));
            var toSite = Ev(new PieceMovedEvent(1, At.Yard(0, 0), At.Site(2, 3, 0), EntryKinds.OverWall), 1);
            Assert.IsFalse(TutorialEvents.MoveMatches(C("yardMove"), new[] { toSite }, s), "a move into the site is not a yard move");
            Assert.IsTrue(TutorialEvents.MoveMatches(C("turnEnd"), new GameEvent[] { new MovesChangedEvent(4, -1, "move", null) }, s));
            Assert.IsFalse(TutorialEvents.MoveMatches(C("turnEnd"), new GameEvent[] { new MovesChangedEvent(4, 5, "offer", null) }, s));
            Assert.IsTrue(TutorialEvents.MoveMatches(C("boosterUsed"), new GameEvent[] { new BoosterAppliedEvent("hammer", null) }, s));
            Assert.IsTrue(TutorialEvents.MoveMatches(C("segmentDone"), new[] { Ev(new SegmentCompletedEvent(0), 8) }, s));
            Assert.IsFalse(TutorialEvents.MoveMatches(C("segmentDone"), new[] { Ev(new SegmentCompletedEvent(0), 3) }, s));
            Assert.IsTrue(TutorialEvents.MoveMatches(C("carouselTurn"), new[] { Ev(new CarouselRotatedEvent(1), 10) }, s));
            Assert.IsFalse(TutorialEvents.MoveMatches(C("overWall"), new[] { moved }, s), "drag signals never come from move events");
            Assert.IsFalse(TutorialEvents.MoveMatches(C("tap"), new[] { moved }, s));
            Assert.IsTrue(TutorialEvents.IsDragSignal("gapPass"));
            Assert.IsFalse(TutorialEvents.IsDragSignal("tap"));
        }

        // --- presence --------------------------------------------------------------------------------------------------------------

        [Test]
        public void K53_presence_waits_then_shows_then_hides_after_4_s_and_comes_back_after_4_s_without_a_touch()
        {
            var s = new TutorialSettings();
            var p = new TutorialPresence(s, 0, 600);
            Assert.AreEqual(PresencePhase.Wait, p.Phase);
            Assert.IsFalse(p.Look().Any, "nothing during the wait");
            p.Handle(PresenceInput.Tick, 599);
            Assert.AreEqual(PresencePhase.Wait, p.Phase);
            p.Handle(PresenceInput.Tick, 600);
            Assert.AreEqual(PresencePhase.Shown, p.Phase);
            Assert.IsTrue(p.Look().Highlight && p.Look().Bubble && p.Look().Glove);
            p.Handle(PresenceInput.Tick, 600 + 3999);
            Assert.AreEqual(PresencePhase.Shown, p.Phase);
            p.Handle(PresenceInput.Tick, 600 + 4000);
            Assert.AreEqual(PresencePhase.Hidden, p.Phase, "visibleMs 4000");
            Assert.IsFalse(p.Look().Any);
            p.Handle(PresenceInput.Tick, 4600 + 3999);
            Assert.AreEqual(PresencePhase.Hidden, p.Phase);
            p.Handle(PresenceInput.Tick, 4600 + 4000);
            Assert.AreEqual(PresencePhase.Shown, p.Phase, "idleReshowMs 4000");
            Assert.AreEqual(2, p.Shows);
        }

        [Test]
        public void K53_a_touch_takes_the_glove_away_fades_the_bubble_and_hides_the_step_on_release_but_never_ends_it()
        {
            var p = new TutorialPresence(new TutorialSettings(), 0, 0);
            p.Handle(PresenceInput.Tick, 0);
            p.Handle(PresenceInput.TouchDown, 100);
            var look = p.Look();
            Assert.IsFalse(look.Glove, "the glove steps back while a finger is down");
            Assert.IsTrue(look.Faded && look.Bubble && look.Highlight);
            p.Handle(PresenceInput.Tick, 100 + 9000);
            Assert.AreEqual(PresencePhase.Shown, p.Phase, "no timeout while the finger is held");
            p.Handle(PresenceInput.TouchUp, 9200);
            Assert.AreEqual(PresencePhase.Hidden, p.Phase);
            p.Handle(PresenceInput.TouchDown, 12000);
            p.Handle(PresenceInput.TouchUp, 12100);
            p.Handle(PresenceInput.Tick, 12100 + 3999);
            Assert.AreEqual(PresencePhase.Hidden, p.Phase, "every touch restarts the idle count");
            p.Handle(PresenceInput.Tick, 12100 + 4000);
            Assert.AreEqual(PresencePhase.Shown, p.Phase);
        }

        [Test]
        public void K53_the_bubble_stays_away_after_three_reshows_but_the_glow_and_glove_come_back()
        {
            var s = new TutorialSettings();
            var p = new TutorialPresence(s, 0, 0);
            float t = 0;
            for (int i = 0; i < 6; i++)
            {
                t += 0;
                p.Handle(PresenceInput.Tick, t);
                bool bubble = p.Look().Bubble;
                Assert.AreEqual(i <= s.ReshowsWithBubble, bubble, "show " + (i + 1));
                Assert.IsTrue(p.Look().Highlight && p.Look().Glove);
                t += 4000;
                p.Handle(PresenceInput.Tick, t); // hidden
                t += 4000;
            }
        }

        [Test]
        public void K53_a_window_stops_every_timer_and_hides_everything_and_the_step_goes_on_after_it()
        {
            var p = new TutorialPresence(new TutorialSettings(), 0, 0);
            p.Handle(PresenceInput.Tick, 0);
            p.Handle(PresenceInput.WindowOpen, 1000);
            Assert.IsFalse(p.Look().Any, "nothing under a window");
            p.Handle(PresenceInput.Tick, 1000 + 60000);
            Assert.AreEqual(PresencePhase.Shown, p.Phase, "timers are stopped");
            p.Handle(PresenceInput.WindowClose, 1000 + 60000);
            Assert.IsTrue(p.Look().Any);
            p.Handle(PresenceInput.Tick, 61000 + 2999);
            Assert.AreEqual(PresencePhase.Shown, p.Phase, "the 1000 ms shown before the window still count");
            p.Handle(PresenceInput.Tick, 61000 + 3000);
            Assert.AreEqual(PresencePhase.Hidden, p.Phase);
        }

        [Test]
        public void K53_presence_done_is_final_and_reports_shows_and_time_to_done()
        {
            var p = new TutorialPresence(new TutorialSettings(), 0, 500);
            p.Handle(PresenceInput.Tick, 500);
            p.Handle(PresenceInput.StepDone, 2500);
            p.Handle(PresenceInput.Tick, 99999);
            Assert.AreEqual(PresencePhase.Done, p.Phase);
            p.Report(2500, out int shows, out int ms);
            Assert.AreEqual(1, shows);
            Assert.AreEqual(2000, ms);
            var early = new TutorialPresence(new TutorialSettings(), 0, 500);
            early.Handle(PresenceInput.StepDone, 100);
            early.Report(100, out int s2, out int m2);
            Assert.AreEqual(1, s2, "a step done before its first show counts one");
            Assert.AreEqual(0, m2);
        }

        [Test]
        public void K53_settings_are_the_tokens_json_tutorial_values()
        {
            var s = new TutorialSettings();
            var t = PresentationPaths.Token("tutorial");
            var checks = new Dictionary<string, double>
            {
                ["startDelayMs"] = s.StartDelayMs, ["appearMs"] = s.AppearMs, ["hideMs"] = s.HideMs, ["visibleMs"] = s.VisibleMs,
                ["idleReshowMs"] = s.IdleReshowMs, ["nextStepDelayMs"] = s.NextStepDelayMs, ["maxStepsPerLevel"] = s.MaxStepsPerLevel,
                ["maxWordsPerBubble"] = s.MaxWordsPerBubble, ["reshowsWithBubble"] = s.ReshowsWithBubble, ["dragFadeAlpha"] = s.DragFadeAlpha,
                ["handLoopMs"] = s.HandLoopMs, ["handPauseMs"] = s.HandPauseMs, ["gloveW"] = s.GloveW, ["gloveH"] = s.GloveH,
                ["glovePressScale"] = s.GlovePressScale, ["trailDotPx"] = s.TrailDotPx, ["trailGapPx"] = s.TrailGapPx, ["trailAlpha"] = s.TrailAlpha,
                ["highlightStrokePx"] = s.HighlightStrokePx, ["highlightGlowPx"] = s.HighlightGlowPx, ["highlightPadPx"] = s.HighlightPadPx,
                ["highlightPulseScale"] = s.HighlightPulseScale, ["highlightPulseMs"] = s.HighlightPulseMs, ["bubbleMaxW"] = s.BubbleMaxW,
                ["bubbleMinH"] = s.BubbleMinH, ["portraitPx"] = s.PortraitPx, ["portraitGapPx"] = s.PortraitGapPx, ["dockGapPx"] = s.DockGapPx,
                ["ghostAlpha"] = s.GhostAlpha, ["ghostLiftMs"] = s.GhostLiftMs,
            };
            foreach (var kv in checks) Assert.AreEqual(t[kv.Key]!.AsDouble, kv.Value, 1e-6, "tokens.tutorial." + kv.Key);
            foreach (var key in t.Keys.Where(k => !k.StartsWith("_"))) Assert.IsTrue(checks.ContainsKey(key), "tokens.tutorial." + key + " has no TutorialSettings field");
        }

        // --- glove -----------------------------------------------------------------------------------------------------------------

        [Test]
        public void K53_the_glove_plays_for_the_first_step_of_levels_1_3_4_6_8_9_and_a_tap_glove_always()
        {
            foreach (int n in new[] { 1, 3, 4, 6, 8, 9 })
            {
                var (tut, _, lvl) = Make(n);
                var s = State.CreateInitialState(lvl);
                var step = tut.Current!;
                Assert.IsTrue(TutorialGlove.Plays(s, step.Data, step.Pieces), "level " + n + " step 1: its path is reachable and does not cancel");
            }
            var (t10, _, l10) = Make(10);
            Assert.IsNull(t10.Current, "level 10 step 1 waits for segmentDone");
            var tap = l10.Data.Tutorial![0];
            Assert.IsTrue(TutorialGlove.Plays(State.CreateInitialState(l10), tap, new int[0]), "a tap glove needs only its target");
        }

        [Test]
        public void K53_no_glove_when_the_block_left_the_start_cell_or_the_path_is_not_reachable()
        {
            var (tut, _, lvl) = Make(3);
            var s = State.CreateInitialState(lvl);
            var data = tut.Current!.Data;
            Assert.IsFalse(TutorialGlove.Plays(s, data, new int[0]), "no highlighted block: no glove");
            int other = tut.Current.Pieces[0] == 0 ? 1 : 0;
            Assert.IsFalse(TutorialGlove.Plays(s, data, new[] { other }), "path[0] is not a cell of that block");
            var bad = new TutorialStepData(1, null, data.Highlight, new HandData("drag", new[] { new[] { 0, 3 }, new[] { 7, 3 } }), data.TextKey, null, data.Done);
            Assert.IsFalse(TutorialGlove.Plays(s, bad, tut.Current.Pieces), "a path that leaves the reachable set");
            var diagonal = new TutorialStepData(1, null, data.Highlight, new HandData("drag", new[] { new[] { 0, 3 }, new[] { 1, 4 } }), data.TextKey, null, data.Done);
            Assert.IsFalse(TutorialGlove.Plays(s, diagonal, tut.Current.Pieces), "legs are straight (GDD §14)");
        }

        [Test]
        public void K53_glove_fingertip_path_rounds_corners_and_trail_dots_are_22_px_apart()
        {
            var lvl = Play.LevelFile(1);
            var L = ScreenLayout.Create(new LayoutSettings(), 1920, lvl.Geo);
            var step = lvl.Data.Tutorial![0];
            var pts = TutorialGlove.FingerPoints(L.Grid, 1.2f, step.Hand!.Path!);
            Assert.AreEqual(3, pts.Length);
            var c0 = L.Grid.CellRect(0, 3);
            Assert.AreEqual((float)c0.CenterX, pts[0].X, 0.01f, "the first point is the touched cell's centre");
            Assert.AreEqual((float)c0.CenterY, pts[0].Y, 0.01f);
            var c1 = L.Grid.CellRect(0, 5);
            Assert.AreEqual((float)(c1.CenterY + 1.2 * L.Grid.CellPx), pts[1].Y, 0.01f, "later points carry the finger offset (K-08)");
            var rounded = TutorialGlove.RoundedPath(pts, 24);
            Assert.Greater(rounded.Length, pts.Length, "the inner corner is an arc");
            Assert.AreEqual(pts[0], rounded[0]);
            Assert.AreEqual(pts[2], rounded[rounded.Length - 1]);
            Assert.AreEqual(TutorialGlove.Length(pts), TutorialGlove.Length(rounded), TutorialGlove.Length(pts) * 0.03f, "rounding shortens the path by a few percent at most");
            var dots = TutorialGlove.TrailDots(rounded, 22);
            for (int i = 1; i < dots.Length - 1; i++)
            {
                float dx = dots[i].X - dots[i - 1].X;
                float dy = dots[i].Y - dots[i - 1].Y;
                Assert.AreEqual(22f, System.Math.Sqrt(dx * dx + dy * dy), 2.5f, "dot " + i);
            }
            Assert.AreEqual(rounded[0], TutorialGlove.PathAt(rounded, 0));
            Assert.AreEqual(rounded[rounded.Length - 1].X, TutorialGlove.PathAt(rounded, 1).X, 0.01f);
            Assert.AreEqual(1, TutorialGlove.RoundedPath(new[] { new Vec2(1, 2) }, 24).Length);
            Assert.AreEqual(1, TutorialGlove.TrailDots(new[] { new Vec2(1, 2) }, 22).Length);
        }

        // --- layout ----------------------------------------------------------------------------------------------------------------

        [Test]
        public void K53_highlight_ids_resolve_to_rectangles_and_an_absent_target_gives_none()
        {
            foreach (int n in Play.Levels)
            {
                var lvl = Play.LevelFile(n);
                var s = State.CreateInitialState(lvl);
                var L = ScreenLayout.Create(new LayoutSettings(), 1920, lvl.Geo);
                var g = L.Grid;
                var ctx = new HighlightContext(L, s, lvl, false, id => BoardPose.RestAnchor(s, id, out int ax, out int ay) ? g.PieceRect(ax, ay, BoardPose.ShapeOf(s, id).W, BoardPose.ShapeOf(s, id).H) : (PxRect?)null);
                foreach (var st in lvl.Data.Tutorial ?? new TutorialStepData[0])
                    foreach (var id in st.Highlight)
                    {
                        var rects = TutorialLayout.Rects(id, ctx);
                        bool optional = id == "truck" || id.StartsWith("piece:k", System.StringComparison.Ordinal) || id.StartsWith("debris", System.StringComparison.Ordinal);
                        if (!optional) Assert.GreaterOrEqual(rects.Count, 1, "level " + n + " highlight " + id);
                        foreach (var r in rects) Assert.IsTrue(r.W > 0 && r.H > 0 && r.X > -200 && r.Right < L.W + 200, "level " + n + " " + id + " " + r);
                    }
            }
            var l1 = Play.LevelFile(1);
            var ctx1 = new HighlightContext(ScreenLayout.Create(new LayoutSettings(), 1920, l1.Geo), State.CreateInitialState(l1), l1, false, _ => null);
            Assert.IsEmpty(TutorialLayout.Rects("truck", ctx1), "the truck chip is not on screen while the queue is empty");
            Assert.IsEmpty(TutorialLayout.Rects("piece:0", ctx1), "a block that is not on the board has no glow");
            Assert.IsEmpty(TutorialLayout.Rects("pre:thermos", ctx1), "pre:* is not on this screen");
            Assert.AreEqual(1, TutorialLayout.Rects("cell:2,4", ctx1).Count);
            Assert.AreEqual(1, TutorialLayout.Rects("booster:hammer", ctx1).Count);
        }

        [TestCase(1920)]
        [TestCase(2337)]
        public void K53_the_bubble_never_covers_the_yard_the_site_the_status_strip_or_the_hud(double designH)
        {
            var layoutSettings = new LayoutSettings();
            var view = new TutorialView(new FakeSurface(), new TutorialSettings());
            foreach (int n in Play.Levels)
            {
                var lvl = Play.LevelFile(n);
                var L = ScreenLayout.Create(layoutSettings, designH, lvl.Geo);
                var hard = TutorialLayout.HardAreas(L);
                foreach (var st in lvl.Data.Tutorial ?? new TutorialStepData[0])
                {
                    var size = view.BubbleSize("Bu blok sonraki kat için.");
                    var place = TutorialLayout.PlaceBubble(L, size.X, size.Y, st.Highlight, new List<PxRect>(), null, 24, 16);
                    Assert.AreEqual(1080 > place.Rect.Right + 24, true, "inside the canvas width: level " + n);
                    if (TutorialLayout.IsHudHighlight(st.Highlight.FirstOrDefault(TutorialLayout.IsHudHighlight) ?? ""))
                    {
                        Assert.AreEqual(BubbleDock.Top, place.Dock);
                        Assert.IsTrue(place.TailUp);
                    }
                    else if (designH == 1920)
                        foreach (var h in hard) Assert.IsFalse(TutorialLayout.Pad(place.Rect, 16).Overlaps(h), "level " + n + " step " + st.Step + " vs " + h);
                }
            }
        }

        // --- the bubble text ---------------------------------------------------------------------------------------------------------

        [Test]
        public void K53_bubble_wraps_greedily_inside_its_width_and_the_check_icon_sits_in_the_line()
        {
            var lines = TutorialView.Wrap("Gölgede {ok} varsa yer doğru.", 44, 600);
            Assert.GreaterOrEqual(lines.Count, 1);
            var all = lines.SelectMany(l => l).ToList();
            Assert.AreEqual(5, all.Count, "five words, the icon is one of them");
            Assert.AreEqual(1, all.Count(w => w.Icon));
            foreach (var l in lines)
            {
                double w = l.Sum(x => x.Width) + FontMetrics.Width(" ", 44) * (l.Count - 1);
                Assert.LessOrEqual(w, 600 + 0.01, "a line fits its width");
            }
            var narrow = TutorialView.Wrap("Bu blok sonraki kat için.", 44, 300);
            Assert.Greater(narrow.Count, 1, "a narrow bubble wraps");
            Assert.AreEqual(5, narrow.SelectMany(l => l).Count());
        }

        // --- the highlight frame ---------------------------------------------------------------------------------------------------

        [Test]
        public void K53_the_highlight_tile_is_a_hollow_rounded_outline_with_a_soft_glow_and_the_frame_is_procedural_on_purpose()
        {
            var a = Atlas.Build();
            int mid = Atlas.Content / 2;
            Assert.AreEqual(0, a.At(Tile.Highlight, mid, mid).A, "hollow: the target shows through");
            Assert.Greater(a.At(Tile.Highlight, mid, 20).A, 250, "the stroke sits on the box edge, 20 px inside the tile");
            Assert.Greater(a.At(Tile.Highlight, 20, mid).A, 250);
            int glow = a.At(Tile.Highlight, mid, 12).A;
            Assert.That(glow, Is.InRange(30, 140), "a glow outside the stroke");
            Assert.Less(a.At(Tile.Highlight, mid, 1).A, 12, "…that has faded at the tile edge");
            Assert.AreEqual(0, a.At(Tile.Highlight, mid, mid + 1).A);
            // the whole corner arc fits the 40 px 9-slice border: the stretched middle row has the same alpha everywhere along an edge
            for (int u = Atlas.PanelBorder; u < Atlas.Content - Atlas.PanelBorder; u++)
                Assert.AreEqual(a.At(Tile.Highlight, mid, 20).A, a.At(Tile.Highlight, u, 20).A, "edge row at " + u);
            var fb = FallbackTiles.Of(FallbackTiles.TutorialHighlight)!.Value;
            Assert.AreEqual(Tile.Highlight, fb.Tile);
            Assert.IsTrue(fb.Sliced);
            Assert.IsTrue(FallbackTiles.IsProceduralOnly("tut_highlight"));
            Assert.IsFalse(FallbackTiles.IsProceduralOnly("bubble_speech"));
            Assert.AreEqual(16.67, TutorialView.HighlightInsetPx, 0.01);
        }

        // --- the whole game --------------------------------------------------------------------------------------------------------

        private static UiNode? TutorialRoot(AppRig rig) => rig.Ui.Root(UiLayer.Tutorial);

        [Test]
        public void K53_level_1_shows_the_glow_bubble_and_glove_after_the_start_delay_and_takes_no_tap()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            var tp = rig.Player.Tutorial!;
            Assert.IsNotNull(tp.Controller);
            rig.Tick(100);
            Assert.AreEqual(PresencePhase.Wait, tp.Presence!.Phase);
            var root = TutorialRoot(rig)!;
            Assert.IsFalse(root.Get(TutorialView.BubbleId).Visible, "nothing during the 600 ms wait");
            Assert.IsFalse(root.Get(TutorialView.GloveId).Visible);
            rig.Tick(800);
            Assert.AreEqual(PresencePhase.Shown, tp.Presence.Phase);
            root = TutorialRoot(rig)!;
            Assert.IsTrue(root.Get(TutorialView.BubbleId).Visible);
            Assert.IsTrue(root.Get(TutorialView.GloveId).Visible, "level 1 step 1 has a reachable drag path");
            Assert.IsTrue(root.Get(TutorialView.GlowPrefix + "0").Visible);
            Assert.AreEqual(2, tp.View.GlowCount, "piece:0 and crane");
            var frame = root.Get(TutorialView.GlowPrefix + "0").Get("tut.hl.frame");
            Assert.AreEqual("tut_highlight", frame.Frame);
            Assert.IsTrue(frame.Sliced);
            var words = root.All().Where(n => n.Id == "tut.word").Select(n => n.Text).ToArray();
            Assert.AreEqual(rig.I18n.Text.Get("tut.m.lift").Split(' '), words, "the bubble says the step's i18n text word by word");
            foreach (var n in root.All())
            {
                Assert.IsNull(n.Action, "K-53/2: no tutorial node is a button: " + n.Id);
                Assert.IsFalse(n.BlocksInput, "K-53/2: no tutorial node eats a tap: " + n.Id);
            }
            Assert.IsFalse(rig.Ui.Tap(TutorialView.GloveId), "a tap on the glove is not a button press");
        }

        [Test]
        public void K53_the_glove_walks_its_path_in_1200_ms_and_fades_in_the_last_400_ms_of_every_1600_ms_loop()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            var tp = rig.Player.Tutorial!;
            rig.Tick(700);
            Assert.AreEqual(PresencePhase.Shown, tp.Presence!.Phase);
            var glove = tp.View.Glove!;
            Assert.IsTrue(glove.Visible);
            var s = tp.Settings;
            // the fingertip of the glove node: (0.36 W, 8) inside its rectangle
            Vec2 Tip() => new Vec2((float)(glove.Rect.X + s.GloveW * 0.36), (float)(glove.Rect.Y + 8));
            var L = rig.Player.Layout!;
            var path = TutorialGlove.RoundedPath(TutorialGlove.FingerPoints(L.Grid, rig.Player.Feel.FingerOffsetCells, rig.Session.Lvl.Data.Tutorial![0].Hand!.Path!), s.CornerRadiusPx);
            float total = TutorialGlove.Length(path);
            float edge = tp.Now; // the glove edge is the tick that showed it; sample the next loop from there
            var samples = new List<(float t, Vec2 tip, float alpha)>();
            for (int i = 0; i < 28; i++)
            {
                rig.Tick(60);
                samples.Add((tp.Now - edge, Tip(), glove.Alpha));
            }
            var first = samples[3]; // after the 160 ms fade-in
            Assert.Less(Dist(first.tip, path[0]) , total * 0.45f, "the glove starts at the beginning of its path");
            float closestToEnd = samples.Min(x => Dist(x.tip, path[path.Length - 1]));
            Assert.Less(closestToEnd, total * 0.06f, "within a loop the fingertip reaches the end of the path");
            Assert.That(samples.Min(x => x.alpha), Is.LessThan(0.5f), "the glove fades during the pause at the end of the loop");
            Assert.That(samples.Any(x => x.alpha >= 0.99f), "and is fully visible while it walks");
            Assert.IsTrue(glove.Visible);
        }

        private static float Dist(Vec2 a, Vec2 b) => (float)System.Math.Sqrt((a.X - b.X) * (double)(a.X - b.X) + (a.Y - b.Y) * (double)(a.Y - b.Y));

        [Test]
        public void K53_tutorial_never_blocks_input_a_drag_works_while_the_step_is_shown_and_ends_the_step()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            rig.Tick(900);
            var tp = rig.Player.Tutorial!;
            Assert.AreEqual(PresencePhase.Shown, tp.Presence!.Phase);
            var first = Play.SolverGolden(1).Drags[0];
            var r = rig.Player.Commit(first);
            Assert.AreEqual(MoveStatuses.Applied, r.Status, "the board takes the move with the tutorial shown");
            rig.Settle();
            Assert.AreEqual(2, tp.Controller!.Current?.Data.Step, "placementCorrect ended step 1 once the move finished playing");
        }

        [Test]
        public void K53_hidden_step_still_completes_on_its_done_event()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            var tp = rig.Player.Tutorial!;
            rig.Tick(700);
            rig.Tick(4200); // shown 4000 ms → hidden
            Assert.AreEqual(PresencePhase.Hidden, tp.Presence!.Phase);
            var root = TutorialRoot(rig)!;
            Assert.IsFalse(root.Get(TutorialView.BubbleId).Visible);
            Assert.IsFalse(root.Get(TutorialView.GloveId).Visible);
            Assert.IsNotNull(tp.Controller!.Current, "hiding is not ending");
            rig.Player.Commit(Play.SolverGolden(1).Drags[0]);
            rig.Settle();
            Assert.AreEqual(2, tp.Controller.Current?.Data.Step);
        }

        [Test]
        public void K53_a_window_hides_the_tutorial_and_closing_it_brings_the_step_back()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            var tp = rig.Player.Tutorial!;
            rig.Tick(1000);
            Assert.AreEqual(PresencePhase.Shown, tp.Presence!.Phase);
            rig.Player.PausePressed();
            rig.Tick(100);
            Assert.IsTrue(tp.Presence.Pauses > 0);
            Assert.IsFalse(tp.Presence.Look().Any, "nothing under a window");
            Assert.IsFalse(TutorialRoot(rig)!.Get(TutorialView.BubbleId).Visible);
            rig.TapOption(PanelAction.Resume);
            rig.Tick(100);
            Assert.AreEqual(0, tp.Presence.Pauses);
            Assert.IsTrue(TutorialRoot(rig)!.Get(TutorialView.BubbleId).Visible);
        }

        [Test]
        public void K53_no_tutorial_on_replay_of_a_won_level_in_the_whole_game()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            rig.PlayGolden(1);
            Assert.IsTrue(rig.App.Save.Won(1));
            rig.Player.Load(1);
            Assert.IsNull(rig.Player.Tutorial!.Controller, "K-53/6: the save marks level 1 won");
            rig.Tick(2000);
            Assert.IsNull(TutorialRoot(rig), "nothing is drawn");
        }

        [Test]
        public void K53_the_tutorial_clears_when_the_attempt_is_decided()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            rig.Tick(1000);
            rig.PlayGolden(1);
            Assert.AreEqual(AppScreen.Win, rig.Screen);
            Assert.IsTrue(rig.Player.Tutorial!.Controller!.Finished);
            Assert.IsNull(TutorialRoot(rig), "a won attempt shows no step");
        }

        [Test]
        public void K43_K53_a_resumed_attempt_rebuilds_its_step_from_the_log()
        {
            var store = new MemorySaveStore();
            var rig = Booted(store);
            rig.Tap(HomeView.PlayId);
            rig.PlayGolden(1, 1); // step 1 (placementCorrect) is done by the first golden move
            Assert.AreEqual(2, rig.Player.Tutorial!.Controller!.Current?.Data.Step);
            var again = Booted(store);
            Assert.AreEqual(AppScreen.Level, again.Screen, "K-43: the attempt resumes");
            var c = again.Player.Tutorial!.Controller!;
            Assert.AreEqual(2, c.Current?.Data.Step, "the log was replayed through the tutorial: step 2 is the active one");
        }

        [Test]
        public void K53_the_home_screen_shows_no_tutorial_after_leaving_a_level()
        {
            var rig = Booted();
            rig.Tap(HomeView.PlayId);
            rig.Tick(1000);
            Assert.IsNotNull(TutorialRoot(rig));
            rig.Player.PausePressed();
            rig.TapOption(PanelAction.Exit);
            rig.TapOption(PanelAction.Leave);
            rig.Tick(100);
            Assert.AreEqual(AppScreen.Home, rig.Screen);
            Assert.IsNull(rig.Player.Tutorial!.Controller);
            Assert.IsNull(TutorialRoot(rig));
        }

        [Test]
        public void K53_every_shipped_tutorial_text_exists_in_both_languages_and_has_at_most_six_words()
        {
            foreach (var lang in new[] { "tr", "en" })
            {
                var i18n = new MinikUsta.Presentation.I18n.Localizer(PresentationPaths.ResourceText);
                i18n.SetLang(lang);
                foreach (int n in Play.Levels)
                    foreach (var st in Play.LevelFile(n).Data.Tutorial ?? new TutorialStepData[0])
                    {
                        string text = i18n.Text.Get(st.TextKey);
                        Assert.AreNotEqual(st.TextKey, text, lang + ": " + st.TextKey + " is missing");
                        int words = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length;
                        Assert.LessOrEqual(words, new TutorialSettings().MaxWordsPerBubble, lang + ": " + st.TextKey + " \"" + text + "\"");
                    }
            }
        }
    }
}
