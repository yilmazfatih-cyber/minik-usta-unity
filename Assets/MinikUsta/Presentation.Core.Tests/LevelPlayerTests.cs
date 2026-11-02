#nullable enable
// The engine-independent level player (Faz 3 Q1) end to end: finger → DragMapper → GameSession → AnimationPlanner →
// AnimationQueue → StepRunner → views (FakeView = the Unity layer's stand-in). What the Unity layer adds on top is only
// GameObjects, sprites and uGUI.
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Art;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Text;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public sealed class LevelPlayerTests
    {
        // --- the golden solutions, by finger, through the whole player ------------------------------------------------

        [Test]
        public void K07_K08_K28_K35_every_golden_solution_played_by_finger_wins_with_the_typescript_event_log([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var rig = new Rig();
            rig.Load(level);
            var golden = Play.SolverGolden(level);
            rig.AssertBoardMatchesState("B" + level + " start");
            int i = 0;
            foreach (var m in golden.Drags)
            {
                var res = rig.DragTo(m);
                Assert.AreEqual(MoveStatuses.Applied, res.Status, "B" + level + " move " + i);
                rig.Settle();
                rig.AssertBoardMatchesState("B" + level + " after move " + i);
                Assert.AreEqual(rig.Player.Summary!.BlocksLeft, rig.View.Blocks, "B" + level + " move " + i + ": K-48 blocks-left chip = the summary");
                i++;
            }
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome, "B" + level + " won");
            Assert.AreEqual(golden.EventLogHash, Moves.EventLogHash(rig.Events), "B" + level + ": the finger-played packages give the TypeScript event log");
            Assert.AreEqual(PanelKind.Win, rig.Player.Panel, "K-28: the win window opens after the plan");
            Assert.AreEqual(Rig.Tr().Get("win.title"), rig.View.Panel!.Title);
            Assert.AreEqual(0, rig.View.Moves, "#56 Bonus İnşaat counted the moves down");
            Assert.AreEqual(0, rig.View.Blocks, "K-48: no block left");
            Assert.AreEqual(0, rig.View.FlyersOut.Count, "every chip flyer went back to the pool");
            Assert.Less(rig.Player.MaxEndSnapPx, 0.5f, "B" + level + ": the steps brought every block to its state pose before End (piece " + rig.Player.MaxEndSnapPiece + ")");
        }

        [Test]
        public void K10_K11_K35_crane_area_releases_fall_and_land_and_still_win([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var rig = new Rig(390, 844);
            rig.Load(level);
            foreach (var m in Play.CraneMoves(level)) rig.DragTo(m);
            rig.Settle();
            rig.AssertBoardMatchesState("B" + level + " end");
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome);
            Assert.AreEqual(PanelKind.Win, rig.Player.Panel);
            Assert.Less(rig.Player.MaxEndSnapPx, 0.5f, "B" + level + ": falls (#10), landings (#11), bounces and deliveries end on the state pose (piece " + rig.Player.MaxEndSnapPiece + ", snaps " + string.Join(",", rig.Player.EndSnaps) + ")");
        }

        [Test]
        public void R12_K08_pressing_right_after_a_release_finishes_the_board_motions_and_still_wins([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var rig = new Rig();
            rig.Load(level);
            var golden = Play.SolverGolden(level);
            bool first = true;
            foreach (var m in Play.CraneMoves(level))
            {
                rig.DragTo(m, settleFirst: first);
                first = false;
            }
            rig.Settle();
            rig.AssertBoardMatchesState("B" + level + " end");
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome);
            Assert.Greater(rig.FastForwardedPresses, 0, "B" + level + ": at least one press fast-forwarded pending steps");
        }

        [Test]
        public void R12_a_grab_puts_the_previous_block_at_its_rest_pose_at_once()
        {
            var rig = new Rig();
            rig.Load(1);
            var moves = Play.CraneMoves(1);
            rig.DragTo(moves[0]);
            Assert.IsTrue(rig.Player.Queue.Busy, "the fall of move 0 is playing");
            rig.PressPoint(moves[1].PieceId, out double x, out double y);
            rig.Player.PointerDown(9, x, y);
            Assert.IsTrue(rig.Player.Mapper.Active, "the second block is held");
            Assert.IsFalse(rig.Player.Queue.Busy, "no lock window: every pending step ran");
            var s = rig.Session.State;
            Assert.IsTrue(BoardPose.RestAnchor(s, moves[0].PieceId, out int ax, out int ay));
            var want = BoardPose.Center(rig.Player.Layout!.Grid, ax, ay, BoardPose.ShapeOf(s, moves[0].PieceId));
            var v = rig.View.Live[moves[0].PieceId];
            Assert.AreEqual(want.X, v.Position.X, 1e-3);
            Assert.AreEqual(want.Y, v.Position.Y, 1e-3);
            Assert.AreEqual(1f, v.Scale.Y, 1e-4, "the landing squash jumped to its end");
        }

        // --- the drag look ---------------------------------------------------------------------------------------------

        [Test]
        public void K18_the_shadow_shows_the_core_landing_with_the_verdict_and_goes_away_on_release()
        {
            var rig = new Rig();
            rig.Load(1);
            var m = Play.CraneMoves(1)[0];
            rig.PressPoint(m.PieceId, out double x, out double y);
            rig.Player.PointerDown(1, x, y);
            var drag = rig.Player.Mapper.Session!;
            var g = rig.Player.Layout!.Grid;
            var grab = Input.DragMath.GrabAt(g, x, y, drag.Start.Ix, drag.Start.Iy, drag.Shape.Cells);
            rig.Player.PointerMove(1, x, y - 10);
            rig.Tick(120);
            var v = rig.View.Live[m.PieceId];
            Assert.IsTrue(v.Lifted && v.Raised, "JUICE #1: lifted and above the board");
            Assert.AreEqual(rig.Player.Feel.LiftScale, v.Scale.X, 1e-4, "JUICE #1: 1,08");
            foreach (var node in drag.PathTo(m.To)!)
            {
                Input.DragMath.FingerFor(g, grab, node.Ix, node.Iy, rig.Player.Feel.FingerOffsetCells, out double fx, out double fy);
                rig.Player.PointerMove(1, fx, fy);
                rig.Tick(Rig.FrameMs);
            }
            var shadow = rig.View.Shadow;
            Assert.IsNotNull(shadow, "over the site the shadow shows");
            var fall = Gravity.ComputeFall(rig.Session.State, m.PieceId, m.To, new FallOptions(null, rig.Session.Hooks.Fall));
            Assert.AreEqual(fall.Landing.Ix, shadow!.LandX, "K-18: the core's landing");
            Assert.AreEqual(fall.Landing.Iy, shadow.LandY);
            Assert.AreEqual(ShadowOutline.Valid, shadow.Look.Outline, "K-18 easy level, correct landing: valid outline");
            Assert.AreEqual(ShadowBadge.Ok, shadow.Look.Badge);
            Assert.AreEqual(shadow.Shape.CellCount, shadow.CellCenters().Count());
            rig.Player.PointerUp(1);
            Assert.IsNull(rig.View.Shadow, "the release hides the shadow");
            Assert.IsFalse(v.Lifted, "the lifted silhouette goes away on release");
        }

        [Test]
        public void K18_hard_level_shadow_is_neutral()
        {
            var rig = new Rig();
            rig.Load(10);
            Assert.AreEqual("hard", rig.Session.Lvl.Difficulty);
            var golden = Play.SolverGolden(10).Drags;
            var m = golden.First(d => d.To.Ix >= rig.Session.Lvl.Geo.SiteX);
            foreach (var d in golden)
            {
                if (ReferenceEquals(d, m)) break;
                rig.DragTo(d);
            }
            rig.Settle();
            rig.PressPoint(m.PieceId, out double x, out double y);
            rig.Player.PointerDown(1, x, y);
            var drag = rig.Player.Mapper.Session!;
            var g = rig.Player.Layout!.Grid;
            var grab = Input.DragMath.GrabAt(g, x, y, drag.Start.Ix, drag.Start.Iy, drag.Shape.Cells);
            rig.Player.PointerMove(1, x, y - 10);
            rig.Tick(120);
            foreach (var node in drag.PathTo(m.To)!)
            {
                Input.DragMath.FingerFor(g, grab, node.Ix, node.Iy, rig.Player.Feel.FingerOffsetCells, out double fx, out double fy);
                rig.Player.PointerMove(1, fx, fy);
                rig.Tick(Rig.FrameMs);
            }
            Assert.IsNotNull(rig.View.Shadow);
            Assert.AreEqual(ShadowOutline.Neutral, rig.View.Shadow!.Look.Outline, "K-18: hard levels show the position only");
            Assert.AreEqual(ShadowBadge.None, rig.View.Shadow.Look.Badge);
        }

        [Test]
        public void K09_a_blocked_pick_shakes_the_block_and_starts_no_drag()
        {
            var rig = new Rig();
            rig.Load(1);
            var s = rig.Session.State;
            int blocked = -1;
            for (int id = 0; id < s.Lvl.Layout.Counts.Pieces; id++)
            {
                var a = Movement.TryBeginDrag(s, id, rig.Session.Hooks.Drag);
                if (!a.Ok && (a.Reason == PickFailures.Immovable || a.Reason == PickFailures.Rule) && State.PieceZone(s, id) == Zone.Yard)
                {
                    blocked = id;
                    break;
                }
            }
            Assert.GreaterOrEqual(blocked, 0, "level 1 has a covered block");
            var v = rig.View.Live[blocked];
            var home = v.Position;
            rig.PressPoint(blocked, out double x, out double y);
            rig.Player.PointerDown(1, x, y);
            Assert.IsFalse(rig.Player.Mapper.Active, "K-09: no drag");
            rig.Tick(rig.Player.Feel.BlockedShakeMs / 4);
            Assert.AreNotEqual(home.X, v.Position.X, "JUICE #2: the block shakes sideways");
            rig.Player.PointerUp(1);
            rig.Settle();
            Assert.AreEqual(home.X, v.Position.X, 1e-4, "the shake ends on the rest position");
            Assert.AreEqual(0, rig.Player.Packages, "nothing was committed");
        }

        [Test]
        public void K07_a_release_on_the_start_cells_cancels_and_the_block_goes_home_for_free()
        {
            var rig = new Rig();
            rig.Load(1);
            int moves = rig.Session.MovesLeft;
            var m = Play.SolverGolden(1).Drags[0];
            var start = Movement.BeginDrag(rig.Session.State, m.PieceId, rig.Session.Hooks.Drag)!.Start;
            var res = rig.DragTo(new DragMove(m.PieceId, start));
            Assert.AreEqual(MoveStatuses.Cancelled, res.Status);
            Assert.AreEqual(StepKind.Cancel, AnimationPlanner.Plan(res, PlanContext.Capture(rig.Session), rig.Player.Feel).Steps[0].Kind);
            rig.Settle();
            rig.AssertBoardMatchesState("after the cancel");
            Assert.AreEqual(moves, rig.Session.MovesLeft, "K-07: 0 moves");
            Assert.AreEqual(moves, rig.View.Moves);
        }

        [Test]
        public void UX53_a_tap_hops_the_block_and_commits_nothing()
        {
            var rig = new Rig();
            rig.Load(1);
            var m = Play.SolverGolden(1).Drags[0];
            rig.PressPoint(m.PieceId, out double x, out double y);
            rig.Player.PointerDown(1, x, y);
            rig.Player.PointerUp(1);
            var v = rig.View.Live[m.PieceId];
            rig.Tick(rig.Player.Feel.TapHopMs / 2);
            Assert.Greater(v.Hop, 0f, "UX §5.3: one-cell hop");
            rig.Settle();
            Assert.AreEqual(0f, v.Hop, 1e-4);
            Assert.AreEqual(0, rig.Player.Packages);
        }

        // --- level flow: segments, truck, out of moves, windows ----------------------------------------------------------

        [Test]
        public void K22_K25_the_segment_slide_and_the_truck_lock_the_board_and_a_touch_plays_them_3x_faster()
        {
            var rig = new Rig();
            rig.Load(5);
            Assert.AreEqual(2, rig.Session.Lvl.Segments.Length);
            bool sawLock = false;
            foreach (var m in Play.SolverGolden(5).Drags)
            {
                var res = rig.DragTo(m);
                var segDone = res.Events.OfType<SegmentCompletedEvent>().Any();
                if (!segDone || res.Won) continue;
                // the segment slide (#18) starts after the placement beats: run into it
                float guard = 0;
                while (!rig.Player.Queue.Locked(rig.Player.Clock.Now) && guard < 5000)
                {
                    rig.Tick(Rig.FrameMs);
                    guard += Rig.FrameMs;
                }
                Assert.IsTrue(rig.Player.Queue.Locked(rig.Player.Clock.Now), "K-22: the segment slide locks the board");
                int next = Enumerable.Range(0, rig.Session.Lvl.Layout.Counts.Pieces)
                    .First(id => State.PieceZone(rig.Session.State, id) == Zone.Yard);
                rig.PressPoint(next, out double x, out double y);
                rig.Player.PointerDown(4, x, y);
                Assert.IsFalse(rig.Player.Mapper.Active, "JUICE §0 rule 3: nothing is picked during a locked window");
                Assert.AreEqual(rig.Player.Feel.LockSpeedUp, rig.Player.Queue.Rate(rig.Player.Clock.Now), "a touch plays it 3× faster");
                rig.Player.PointerUp(4);
                sawLock = true;
                rig.Settle();
                Assert.AreEqual(1f, rig.Player.Queue.Rate(rig.Player.Clock.Now), "the rate falls back after the window");
                Assert.AreEqual(1, rig.View.Board!.Segment, "the board shows segment 2");
                rig.AssertBoardMatchesState("after the segment slide");
            }
            Assert.IsTrue(sawLock, "level 5 completes a segment before the last move");
            rig.Settle();
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome);
        }

        [Test]
        public void K29_out_of_moves_opens_the_window_with_only_retry_and_retry_restarts_the_attempt()
        {
            string text = Rig.ReadLevel(1)!.Replace("\"moves\": 10", "\"moves\": 1"); // D-099 level 1: 10 moves
            var rig = new Rig(read: n => n == 1 ? text : Rig.ReadLevel(n));
            rig.Load(1);
            Assert.AreEqual(1, rig.Session.MovesLeft);
            rig.DragTo(Play.SolverGolden(1).Drags[0]);
            rig.Settle();
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, rig.Session.Outcome);
            Assert.AreEqual(PanelKind.Lose, rig.Player.Panel);
            var panel = rig.View.Panel!;
            Assert.AreEqual(Rig.Tr().Get("lose.title"), panel.Title);
            Assert.AreEqual(Rig.Tr().Format("lose.blocksLeft", rig.Player.BlocksLeft), panel.Body);
            Assert.AreEqual(1, panel.Buttons.Length, "Faz 3 Q1: no +5 offer, only retry");
            Assert.AreEqual(PanelAction.Retry, panel.Buttons[0].Action);
            Assert.AreEqual(Rig.Tr().Get("lose.retry"), panel.Buttons[0].Label);
            // the board is closed while the window is open
            rig.PressPoint(Play.SolverGolden(1).Drags[1].PieceId, out double x, out double y);
            rig.Player.PointerDown(1, x, y);
            Assert.IsFalse(rig.Player.Mapper.Active);
            rig.View.Press(PanelAction.Retry);
            Assert.AreEqual(PanelKind.None, rig.Player.Panel);
            Assert.IsNull(rig.View.Panel);
            Assert.AreEqual(1, rig.Session.MovesLeft, "a new attempt");
            Assert.AreEqual(BoosterOutcomes.Playing, rig.Session.Outcome);
            rig.AssertBoardMatchesState("after retry");
        }

        /// <summary>GDD K-30 D1 board (web deadlock.test.ts JAM; yard 5 × 4, four blocks under an I5 Ağır Yük, crates on top), 2 moves.</summary>
        private const string JamJson = @"{""schemaVersion"":1,""id"":1,""chapter"":1,""name"":{""tr"":""Test"",""en"":""Test""},""difficulty"":""easy"",""moves"":2,
          ""goals"":[{""type"":""build""}],""gravity"":{""build"":""normal"",""yard"":false},""wall"":{""height"":2,""gaps"":[]},""site"":{""cols"":2,""rows"":4},
          ""build"":{""mode"":""segments"",""segments"":[{""name"":{""tr"":""D0"",""en"":""S0""},""rows"":[""WW"",""WW"",""WW"",""WW""]}]},
          ""yard"":{""cols"":5,""rows"":4,""batches"":[{""forSegment"":0,""pieces"":[{""shape"":""D2_90"",""color"":""W"",""x"":0,""y"":0},{""shape"":""B1_0"",""color"":""W"",""x"":2,""y"":0},
            {""shape"":""B1_0"",""color"":""W"",""x"":3,""y"":0},{""shape"":""B1_0"",""color"":""W"",""x"":4,""y"":0},{""shape"":""I5_0"",""color"":""W"",""x"":0,""y"":1},
            {""shape"":""D2_90"",""color"":""W"",""x"":0,""y"":3},{""shape"":""B1_0"",""color"":""W"",""x"":2,""y"":3}]}]},
          ""obstacles"":[{""type"":""crate"",""x"":0,""y"":2,""hp"":1},{""type"":""crate"",""x"":1,""y"":2,""hp"":1},{""type"":""crate"",""x"":2,""y"":2,""hp"":1},{""type"":""crate"",""x"":3,""y"":2,""hp"":1},{""type"":""crate"",""x"":4,""y"":2,""hp"":1}]}";

        [Test]
        public void K30_K55_E64_a_D1_after_the_plus_5_offer_tears_the_last_drag_down_on_screen_and_the_board_matches_the_state()
        {
            // the last drag leaves no holdable block (the counter is 0, so D1 does not run); the accepted +5 offer runs D1 once: a Söküm
            var rig = new Rig();
            rig.Player.Start(Play.FromJson(JamJson), 99);
            Assert.AreEqual(MoveStatuses.Applied, rig.Player.Commit(new DragMove(5, new DragNode(5, 4, Movement.Free))).Status);
            rig.Settle();
            var last = rig.Player.Commit(new DragMove(6, new DragNode(5, 4, Movement.Free)));
            rig.Settle();
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, rig.Session.Outcome);
            Assert.IsFalse(last.Events.Any(e => e is DeadlockDetectedEvent), "K-30: D1 does not run at 0 moves");
            var before = (int[])rig.Session.State.Buf.Clone();
            var offer = rig.Player.Commit(new AddMovesMove(5, AddMovesSource.OfferCoins));
            Assert.AreEqual(MoveStatuses.Applied, offer.Status);
            CollectionAssert.AreEqual(new[] { "movesChanged", "deadlockDetected", "teardown" }, offer.Events.Select(e => e.TypeName).ToArray());
            Assert.AreEqual(TeardownCauses.Access, offer.Teardown!.Cause, "RULES_VERSION 3: a D1 the rules cannot lift is a Söküm, never a reshuffle");
            var plan = AnimationPlanner.Plan(offer, PlanContext.Capture(GameSession.Replay(rig.Session.Lvl, rig.Session.Log.Take(rig.Session.Log.Count - 1).ToArray())), rig.Player.Feel);
            Assert.IsTrue(plan.Steps.Any(st => st.Kind == StepKind.Teardown), "the Söküm is a planned step of the package");
            rig.Settle();
            Assert.AreEqual(BoosterOutcomes.Playing, rig.Session.Outcome);
            Assert.AreEqual(5, rig.Session.MovesLeft, "the counter keeps the +5; the drag is taken back");
            Assert.IsFalse(State.PieceZone(rig.Session.State, 6) != Zone.Yard);
            rig.AssertBoardMatchesState("after the D1 Söküm");
            CollectionAssert.AreNotEqual(before, rig.Session.State.Buf);
        }

        [Test]
        public void K28_next_level_cycles_1_to_10_and_back_to_1()
        {
            Assert.AreEqual(2, LevelCatalog.Next(1));
            Assert.AreEqual(10, LevelCatalog.Next(9));
            Assert.AreEqual(1, LevelCatalog.Next(10));
            var rig = new Rig();
            rig.Load(10);
            foreach (var m in Play.SolverGolden(10).Drags) rig.DragTo(m);
            rig.Settle();
            Assert.AreEqual(PanelKind.Win, rig.Player.Panel);
            rig.View.Press(PanelAction.Next);
            Assert.AreEqual(1, rig.Player.LevelNumber);
            Assert.AreEqual(rig.Session.Lvl.Moves, rig.View.Moves);
            Assert.AreEqual(Rig.Tr().Format("home.play", 1), rig.View.LevelLabel);
            Assert.AreEqual("Ağaç Basamakları", rig.View.LevelName);
            rig.AssertBoardMatchesState("level 1 after next");
        }

        [Test]
        public void Pause_window_restart_and_continue()
        {
            var rig = new Rig();
            rig.Load(2);
            var m = Play.SolverGolden(2).Drags[0];
            rig.DragTo(m);
            rig.Settle();
            int left = rig.Session.MovesLeft;
            rig.Player.PausePressed();
            Assert.AreEqual(PanelKind.Pause, rig.Player.Panel);
            Assert.AreEqual(Rig.Tr().Get("pause.title"), rig.View.Panel!.Title);
            rig.View.Press(PanelAction.Resume);
            Assert.AreEqual(PanelKind.None, rig.Player.Panel);
            Assert.AreEqual(left, rig.Session.MovesLeft, "continue keeps the attempt");
            rig.Player.PausePressed();
            rig.View.Press(PanelAction.Restart);
            Assert.AreEqual(rig.Session.Lvl.Moves, rig.Session.MovesLeft, "restart = a new attempt");
            Assert.AreEqual(0, rig.Session.MovesMade);
            rig.AssertBoardMatchesState("after restart");
        }

        [Test]
        public void D005_an_invalid_level_file_is_not_played()
        {
            var rig = new Rig(read: n => n == 3 ? "{\"id\": 3}" : Rig.ReadLevel(n));
            rig.Load(2);
            Assert.IsFalse(rig.Player.Load(3), "the invalid level does not load");
            StringAssert.Contains("invalid", rig.Player.LoadError);
            Assert.AreEqual(2, rig.Player.LevelNumber, "the previous attempt stays");
            Assert.IsFalse(new Rig(read: n => null).Player.Load(1));
        }

        [Test]
        public void K34_hook5_the_stuck_pulse_beats_the_needed_blocks_after_6_s_on_an_easy_level()
        {
            var rig = new Rig();
            rig.Load(1);
            var needed = rig.Session.Summary().NeededNow;
            Assert.Greater(needed.Length, 0);
            var v = rig.View.Live[needed[0]];
            rig.Tick(StuckPulse6sMinus());
            Assert.AreEqual(1f, v.Scale.X, 1e-5, "no pulse before 6000 ms");
            rig.Tick(100 + rig.Player.Feel.HintPulseMs / 4);
            Assert.Greater(v.Scale.X, 1f, "K-34 hook 5: the block pulses");
            rig.Settle();
            Assert.AreEqual(1f, v.Scale.X, 1e-5);
        }

        private static float StuckPulse6sMinus() => Feel.StuckPulse.EasyIdleMs - 200;

        [Test]
        public void K49_UX58_the_board_and_hud_rectangles_of_every_level_fit_the_canvas([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            foreach (var screen in new[] { (390.0, 844.0), (360.0, 800.0), (1080.0, 1920.0), (768.0, 1024.0) })
            {
                var rig = new Rig(screen.Item1, screen.Item2);
                rig.Load(level);
                var L = rig.Player.Layout!;
                foreach (var item in rig.View.Board!.Items)
                {
                    if (item.Kind == BoardItemKind.Background) continue;
                    Assert.GreaterOrEqual(item.Rect.X, 0, item + " inside the canvas");
                    Assert.LessOrEqual(item.Rect.Right, L.W + 1e-6, item.ToString());
                    Assert.GreaterOrEqual(item.Rect.Y, L.TopGroupBottomY, item + " below the HUD");
                    // V1 (ART_UNITY §4.3–§4.4): the tray's bottom frame reaches into the status strip by design (the truck chip rides it)
                    if (item.Kind == BoardItemKind.Tray) Assert.LessOrEqual(item.Rect.Bottom, L.Board.Status.Bottom + 1e-6, item + " tray ends in the status strip");
                    else Assert.LessOrEqual(item.Rect.Bottom, L.Board.Status.Y + 1e-6, item + " above the status strip");
                }
                foreach (var r in new[] { HudLayout.Capsule(L), HudLayout.BlocksChip(L), HudLayout.LevelName(L), HudLayout.Moves(L), HudLayout.Pause(L), HudLayout.Segments(L) })
                {
                    Assert.GreaterOrEqual(r.X, 0);
                    Assert.LessOrEqual(r.Right, L.W);
                    Assert.LessOrEqual(r.Bottom, L.TopGroupBottomY + 1e-6, "HUD rect " + r + " in the top group");
                }
                Assert.GreaterOrEqual(HudLayout.Pause(L).W, L.MinTargetPx, "UX §0.3: the pause tool is a ≥ 128 px target");
            }
        }

        [Test]
        public void Determinism_the_same_input_gives_the_same_frames()
        {
            string Run()
            {
                var rig = new Rig(390, 844);
                rig.Load(5);
                var log = new System.Text.StringBuilder();
                foreach (var m in Play.CraneMoves(5))
                {
                    rig.DragTo(m, settleFirst: false);
                    for (int f = 0; f < 20; f++)
                    {
                        rig.Tick(Rig.FrameMs);
                        foreach (var kv in rig.View.Live.OrderBy(k => k.Key))
                            log.Append(kv.Key).Append(':').Append(kv.Value.Position).Append(kv.Value.Scale).Append(';');
                    }
                }
                rig.Settle();
                return log.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void Pooling_visuals_are_reused_across_attempts()
        {
            var rig = new Rig();
            rig.Load(1);
            int created = rig.View.PiecesCreated;
            for (int i = 0; i < 5; i++) rig.Player.Restart();
            rig.Player.Load(2);
            rig.Player.Load(1);
            Assert.AreEqual(created, rig.View.PiecesCreated, "TECH §10.6: no new block visual after the first attempt");
        }

        [Test]
        public void K48_UX59_hud_text_comes_from_tr_json()
        {
            var rig = new Rig();
            rig.Load(1);
            Assert.AreEqual("Hamle", rig.View.MovesLabel);
            Assert.AreEqual("blok", rig.View.BlocksLabel);
            Assert.AreEqual("Bölüm 1", rig.View.LevelLabel);
            Assert.AreEqual("0/2", rig.View.Segments, "D-099 level 1: two floors");
            Assert.AreEqual(rig.Session.Summary().BlocksLeft, rig.View.Blocks);
            Assert.AreEqual(10, rig.View.Moves);
        }
    }
}

namespace MinikUsta.Presentation.Tests
{
    public sealed class DebugSolverTests
    {
        [Test]
        public void K28_debug_play_the_solution_from_resources_wins_every_level([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var rig = new Rig();
            rig.Load(level);
            var solution = DebugSolver.SolutionLog(PresentationPaths.ResourceText("solutions/level_" + level.ToString("000") + ".solver"));
            Assert.IsNotNull(solution, "Resources/solutions has level " + level);
            for (int step = 0; step < 60 && rig.Session.Outcome == BoosterOutcomes.Playing; step++)
            {
                var a = DebugSolver.Next(rig.Session, solution);
                Assert.IsNotNull(a, "B" + level + " step " + step);
                Assert.AreEqual(MoveStatuses.Applied, rig.Player.Commit(a!).Status);
                rig.Settle();
            }
            Assert.AreEqual(BoosterOutcomes.Won, rig.Session.Outcome);
            Assert.IsNull(DebugSolver.Next(rig.Session, solution), "nothing after the win");
        }

        [Test]
        public void K16_K34_debug_greedy_hints_are_correct_placements([ValueSource(typeof(Play), nameof(Play.Levels))] int level)
        {
            var rig = new Rig();
            rig.Load(level);
            var solution = DebugSolver.SolutionLog(PresentationPaths.ResourceText("solutions/level_" + level.ToString("000") + ".solver"))!;
            // walk half of the solution, then ask the greedy helper (off the solution path the game falls back to it)
            for (int i = 1; i < solution.Length / 2; i++)
            {
                rig.Player.Commit(solution[i]);
                rig.Settle();
            }
            int hints = 0;
            for (int step = 0; step < 40 && rig.Session.Outcome == BoosterOutcomes.Playing; step++)
            {
                var m = DebugSolver.NextCorrectMove(rig.Session);
                if (m == null) break;
                var res = rig.Player.Commit(m);
                Assert.AreEqual(MoveStatuses.Applied, res.Status, "B" + level + " hint " + step);
                Assert.IsTrue(res.Events.OfType<PlacementCorrectEvent>().Any(), "B" + level + " hint " + step + " is a correct placement");
                hints++;
                rig.Settle();
                rig.AssertBoardMatchesState("B" + level + " after hint " + step);
            }
            if (level <= 2) Assert.Greater(hints, 0, "B" + level + ": every block is reachable from the start");
        }
    }
}
