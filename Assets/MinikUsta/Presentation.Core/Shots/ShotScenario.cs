#nullable enable
// The CI screenshot driver (paket C1; docs/CI.md "Ekran görüntüleri"): a scripted playthrough of the game that stops at fixed
// moments and tells the outside world "take the picture now". It is opened ONLY by the URL parameter `?shots=1` (`ShotOptions`),
// so a normal visit never sees it; the Unity layer then keeps its save in memory, so the autoplay cannot touch a real save.
//
// The scenario drives `GameApp` through the same calls a finger ends in (UI taps, the pointer, the committed golden move) and
// reports each moment as one line `[minikShot] shot:<name>`; the Unity layer writes the line with `Debug.Log` (the browser console)
// and the Playwright script of tools/ci-shots waits for it. After a signal the scenario HOLDS for `HoldMs` game milliseconds
// without touching anything, so a slow software-GL browser (a few frames per second) still shows the frame it was asked about.
//
//   home → settings → (close) → play → level_start (0,5 s after the start, before the tutorial appears) → tutorial (3 s after the start: glow,
//   bubble and the glove on its path) → [golden moves up to the first one that goes to the site] → drag_mid (a real pointer drag of that
//   block to its golden release node, held and then cancelled: the lifted block and its green K-18 shadow) → landed (that move committed and settled) → … golden solution …
//   → win (the win window and its rewards settled)
//   U2 (UX_UNITY P0-11): → Devam → home → Bölüm 2 (direct) → pause (the pause window) → exit_confirm (Ana sayfa → the exit confirmation)
//   → Kal → Devam → the golden solution of Bölüm 2 → Devam → home → level_start_window (Bölüm 3: the first level start window) → Oyna →
//   yard moves only until the moves run out → lose_offer (the +5 offer window) → done
//
// Motion windows (P4B-08; only with `?shots=1&motion=<scale>`, CI uses 0.0125): two stretches of game time are played in slow motion so that a software-GL
// browser, which draws a few frames per second, can still photograph every frame of the animation: `seq:king_wave` (the home king's
// arrival wave, the first `MotionGameMs` after boot, before the `home` shot) and `seq:king_big_cheer` (the win window's king, from the
// moment the win window opens, before the `win` shot). Inside a window `TimeScale` is the scale (the Unity layer multiplies the app's frame
// time by it) and the scenario reports `seq:<name>:begin`, `seq:<name>:t=<game ms>` (every frame) and `seq:<name>:end`; the Playwright
// script (tools/ci-shots) photographs the page between begin and end, tags each picture with the last `t`, resamples the pictures to
// 60 and 30 frames per game second, counts how many differ from the previous one and writes a strip PNG.
//
// Pure: no engine, no wall clock — the clock is the sum of `Tick` frame times, so a test drives it with fixed frames.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Shots
{
    /// <summary>What the URL asked for (`?shots=1`, optionally `&amp;shotsHold=2500`).</summary>
    public sealed class ShotOptions
    {
        /// <summary>Game milliseconds the scenario stands still after a shot signal.</summary>
        public float HoldMs = 2500f;

        /// <summary>P4B-08: `&amp;motion=0.1` plays the two motion windows at that fraction of real time (0 = no windows).</summary>
        public float MotionScale;

        /// <summary>P4B-08: game milliseconds of one motion window (`&amp;motionMs=1800`).</summary>
        public float MotionGameMs = 1800f;

        /// <summary>`url` is `Application.absoluteURL` (empty outside WebGL); null when `shots=1` is not in the query.</summary>
        public static ShotOptions? FromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            int q = url!.IndexOf('?');
            if (q < 0) return null;
            int hash = url.IndexOf('#', q);
            string query = hash < 0 ? url.Substring(q + 1) : url.Substring(q + 1, hash - q - 1);
            var o = new ShotOptions();
            bool on = false;
            foreach (var pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                string key = eq < 0 ? pair : pair.Substring(0, eq);
                string val = eq < 0 ? "" : pair.Substring(eq + 1);
                switch (key)
                {
                    case "shots":
                        on = val == "1";
                        break;
                    case "motion":
                        if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ms) && ms >= 0.005f && ms <= 1f) o.MotionScale = ms;
                        break;
                    case "motionMs":
                        if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float mg) && mg >= 500f && mg <= 6000f) o.MotionGameMs = mg;
                        break;
                    case "shotsHold":
                        if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h) && h >= 200f && h <= 20000f) o.HoldMs = h;
                        break;
                }
            }
            return on ? o : null;
        }
    }

    public sealed class ShotScenario
    {
        /// <summary>Prefix of every line the scenario reports; the Playwright script matches it.</summary>
        public const string Tag = "[minikShot]";

        /// <summary>The shots in the order they are signalled (`shot:&lt;name&gt;`); the CI manifest lists the same names.</summary>
        public static readonly string[] ShotNames =
        {
            "home", "settings", "level_start", "tutorial", "drag_mid", "landed", "win", "pause", "exit_confirm", "level_start_window",
            // E3 (GDD §16 v3 block traits): level 5 two-tone, level 7 wrap + scissors, level 9 bonus star, level 99 the showcase
            "v3_twotone", "v3_wrap", "v3_bonus", "v3_showcase",
            "lose_offer",
        };

        /// <summary>The motion windows (`seq:&lt;name&gt;`) and the shot each one comes right before; tools/ci-shots/shots.json lists the same.</summary>
        public static readonly (string Name, string Before)[] SequenceNames = { ("king_wave", "home"), ("king_big_cheer", "win") };

        /// <summary>U2: game ms the scenario holds after a shot that follows a motion window (the script's picture analysis comes first).</summary>
        public const float AfterMotionHoldMs = 45000f;

        /// <summary>Game milliseconds from the start after which the scenario gives up (`error:timeout`).</summary>
        public const float TimeoutMs = 420000f;

        private readonly GameApp app;
        private readonly Func<int, SessionAction[]?> solutions;
        private readonly Action<string> signal;
        private readonly ShotOptions options;
        private readonly List<Step> steps = new List<Step>();
        private int index;
        private float waited;
        private float total;
        private bool announced;

        // drag preview
        private DragMove? preview;
        private float dragT;
        private double startX, startY, endX, endY;

        // golden play
        private SessionAction[]? solution;
        private int solutionLevel;
        private float sinceCommit;

        /// <summary>The golden solution of the level being played (reloaded when the level changes).</summary>
        private SessionAction[]? Sol()
        {
            int n = app.Player.LevelNumber;
            if (solution == null || solutionLevel != n)
            {
                solution = solutions(n);
                solutionLevel = n;
            }
            return solution;
        }

        private delegate bool Run(float dtMs);

        private readonly struct Step
        {
            public readonly string Name;
            public readonly float WaitMs;
            public readonly Run Run;

            public Step(string name, float waitMs, Run run)
            {
                Name = name;
                WaitMs = waitMs;
                Run = run;
            }
        }

        public ShotScenario(GameApp app, Func<int, SessionAction[]?> solutions, Action<string> signal, ShotOptions? options = null)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            this.solutions = solutions ?? throw new ArgumentNullException(nameof(solutions));
            this.signal = signal ?? throw new ArgumentNullException(nameof(signal));
            this.options = options ?? new ShotOptions();
            Build();
        }

        public bool Done { get; private set; }
        public bool Failed { get; private set; }

        /// <summary>P4B-08: factor the Unity layer applies to the app's frame time (1 outside a motion window).</summary>
        public float TimeScale { get; private set; } = 1f;
        private bool motionOpen;
        private float motionGameMs;
        private float motionRealMs;
        /// <summary>Name of the step being waited for / run (tests, logs).</summary>
        public string Current => index < steps.Count ? steps[index].Name : "done";
        /// <summary>Shots signalled so far, in order.</summary>
        public List<string> Signalled { get; } = new List<string>();

        // --- script ------------------------------------------------------------------------------------------------------------

        private void Build()
        {
            float hold = options.HoldMs;
            Add("home_motion", 0f, Motion("king_wave", () => app.Flow.Current == AppScreen.Home));
            Add("boot", 2500f, Once(() => RequireScreen(AppScreen.Home) ? Shot("home") : false));
            Add("open_settings", hold, Once(() => TapHome(HomeView.SettingsAction)));
            Add("settings", 1000f, Once(() => Shot("settings")));
            Add("close_settings", hold, Once(CloseSettings));
            Add("play", 900f, Once(() => TapHome(HomeView.PlayAction)));
            Add("level_start_window", 600f, Once(AcceptLevelStart));
            Add("level_start", 500f, Once(() => RequireScreen(AppScreen.Level) ? Shot("level_start") : false));
            Add("tutorial", hold, Once(() => Shot("tutorial")));
            Add("approach", hold, Approach);
            Add("drag_down", 0f, Once(DragDown));
            Add("drag_move", 0f, DragStep);
            Add("drag_mid", 600f, Once(() => Shot("drag_mid")));
            Add("drag_cancel", hold, Once(() =>
            {
                app.PointerUp(0, true);
                return true;
            }));
            Add("first_move", 1000f, Once(CommitNext));
            Add("landed", 0f, WaitIdleThen(450f, () => Shot("landed")));
            Add("solve", hold, Solve);
            Add("win_motion", 0f, Motion("king_big_cheer", () => app.Player.Panel == PanelKind.Win));
            Add("win", 0f, WaitWin);
            // U2 (P0-11): the windows the audit could not see — pause, exit confirmation, the Bölüm 3 level start window, the +5 offer
            // the Playwright script analyses the king_big_cheer pictures (tens of seconds on software GL) BEFORE it photographs "win", while
            // the game keeps running: after a motion window the scenario stands still much longer, or "win" would show the home screen
            Add("win_continue", options.MotionScale > 0f ? Math.Max(hold, AfterMotionHoldMs) : hold, Once(() => Panel(AppScreen.Win, PanelAction.Continue)));
            Add("play_2", 900f, Once(() => TapHome(HomeView.PlayAction)));
            Add("pause_open", 1500f, WaitIdleThen(300f, () =>
            {
                app.Player.PausePressed();
                return RequireScreen(AppScreen.Paused);
            }));
            Add("pause", 700f, Once(() => Shot("pause")));
            Add("exit_open", hold, Once(() => Panel(AppScreen.Paused, PanelAction.Exit)));
            Add("exit_confirm", 700f, Once(() => Shot("exit_confirm")));
            Add("exit_stay", hold, Once(() => Panel(AppScreen.ExitConfirm, PanelAction.Stay) && Panel(AppScreen.Paused, PanelAction.Resume)));
            Add("solve_2", 600f, Solve);
            Add("win_2", 0f, WaitWin2);
            Add("win_2_continue", 300f, Once(() => Panel(AppScreen.Win, PanelAction.Continue)));
            Add("play_3", 900f, Once(() => TapHome(HomeView.PlayAction) && RequireScreen(AppScreen.LevelStart)));
            Add("level_start_window", 900f, Once(() => Shot("level_start_window")));
            Add("start_3", hold, Once(() => Panel(AppScreen.LevelStart, PanelAction.Play)));
            // E3: the v3 block traits on their levels, then back to level 3 for the +5 offer
            Add("v3_start_5", hold, Once(() => DebugLevel(5)));
            Add("v3_twotone", 1500f, Once(() => RequireScreen(AppScreen.Level) && Shot("v3_twotone")));
            Add("v3_start_7", hold, Once(() => DebugLevel(7)));
            Add("v3_wrap", 1500f, Once(() => RequireScreen(AppScreen.Level) && Shot("v3_wrap")));
            Add("v3_start_9", hold, Once(() => DebugLevel(9)));
            Add("v3_bonus", 1500f, Once(() => RequireScreen(AppScreen.Level) && Shot("v3_bonus")));
            Add("v3_start_99", hold, Once(() => DebugLevel(LevelCatalog.Showcase)));
            Add("v3_showcase", 1500f, Once(() => RequireScreen(AppScreen.Level) && Shot("v3_showcase")));
            Add("v3_back_3", hold, Once(() => DebugLevel(3)));
            Add("burn", 1200f, BurnMoves);
            Add("lose_offer", 0f, WaitIdleThen(900f, () => RequireScreen(AppScreen.LoseOffer) && Shot("lose_offer")));
            Add("done", hold, Once(() =>
            {
                Say("done");
                Done = true;
                return true;
            }));
        }

        private void Add(string name, float waitMs, Run run) => steps.Add(new Step(name, waitMs, run));

        /// <summary>
        /// P4B-08: a motion window. Waits (up to the scenario's own timeout) until `ready`, then plays `MotionGameMs` game milliseconds at
        /// `MotionScale` of real time, reporting `seq:name:begin`, one `seq:name:t=` line per frame and `seq:name:end`. No window without a scale.
        /// </summary>
        private Run Motion(string name, Func<bool> ready) => dtMs =>
        {
            if (options.MotionScale <= 0f) return true;
            if (!motionOpen)
            {
                if (!ready()) return false;
                motionOpen = true;
                motionGameMs = 0f;
                TimeScale = options.MotionScale;
                Say("seq:" + name + ":begin");
                return false;
            }
            motionGameMs += dtMs * TimeScale;
            motionRealMs += dtMs;
            Say("seq:" + name + ":t=" + ((int)motionGameMs).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (motionGameMs < options.MotionGameMs) return false;
            motionOpen = false;
            TimeScale = 1f;
            Say("seq:" + name + ":end");
            return true;
        };

        /// <summary>A step that does its work once: `act` returns false to fail the scenario.</summary>
        private Run Once(Func<bool> act) => _ =>
        {
            if (!act()) Fail(Current);
            return true;
        };

        private void Say(string text) => signal(Tag + " " + text);

        private bool Shot(string name)
        {
            Signalled.Add(name);
            Say("shot:" + name);
            return true;
        }

        private void Fail(string why)
        {
            if (Failed) return;
            Failed = true;
            Done = true;
            Say("error:" + why);
        }

        // --- frame -------------------------------------------------------------------------------------------------------------

        public void Tick(float dtMs)
        {
            if (Done) return;
            if (!announced)
            {
                announced = true;
                Say("ready " + (int)app.Player.Viewport.W + "x" + (int)app.Player.Viewport.H);
            }
            if (!(dtMs > 0f)) return;
            total += dtMs;
            // the slow-motion windows take MotionGameMs / MotionScale real milliseconds each (a few minutes at 0.0125): they do not count
            if (total - motionRealMs > TimeoutMs)
            {
                Fail("timeout:" + Current);
                return;
            }
            if (index >= steps.Count)
            {
                Done = true;
                return;
            }
            waited += dtMs;
            sinceCommit += dtMs;
            var step = steps[index];
            if (waited < step.WaitMs) return;
            if (!step.Run(dtMs)) return;
            index++;
            waited = 0f;
            if (index >= steps.Count) Done = true;
        }

        // --- actions -----------------------------------------------------------------------------------------------------------

        private bool RequireScreen(AppScreen screen)
        {
            if (app.Flow.Current == screen) return true;
            Say("note:screen=" + app.Flow.Current + " wanted=" + screen);
            return false;
        }

        private bool TapHome(string action)
        {
            var root = app.Home;
            if (root == null) return false;
            foreach (var n in root.All())
            {
                if (n.Action != action) continue;
                app.OnUiTap(n);
                return true;
            }
            return false;
        }

        private bool CloseSettings()
        {
            var root = app.Settings;
            if (root == null) return false;
            foreach (var n in root.All())
            {
                if (n.Action != SettingsView.Close) continue;
                app.OnUiTap(n);
                return true;
            }
            return false;
        }

        /// <summary>A window button of the open window when the app is on `screen` (false otherwise: the scenario fails).</summary>
        private bool Panel(AppScreen screen, PanelAction action)
        {
            if (!RequireScreen(screen)) return false;
            app.PanelAction(app.Player, action);
            return true;
        }

        /// <summary>The second win window (Bölüm 2): no shot, only settled.</summary>
        private bool WaitWin2(float dtMs)
        {
            var p = app.Player;
            if (p.Session != null && p.Session.Outcome != BoosterOutcomes.Won)
                return p.Session.Outcome == BoosterOutcomes.Playing ? Fail2("level 2 still playing") : Fail2("level 2 outcome " + p.Session.Outcome);
            if (p.Panel != PanelKind.Win || !Idle())
            {
                sinceCommit = 0f;
                return false;
            }
            return sinceCommit >= 600f;
        }

        /// <summary>
        /// U2: spends the moves of the level with drags that stay in the yard (each costs one move, K-07 step 4) until the +5 offer opens
        /// (K-29). Deterministic: the first holdable block (id order) and its first reachable yard node that the core accepts.
        /// </summary>
        private bool BurnMoves(float dtMs)
        {
            var p = app.Player;
            var session = p.Session;
            if (app.Flow.Current == AppScreen.LoseOffer) return true;
            if (session == null) return Fail2("no session");
            if (session.Outcome != BoosterOutcomes.Playing) return session.Outcome == BoosterOutcomes.OutOfMoves || Fail2("outcome " + session.Outcome);
            if (!Idle() || sinceCommit < 250f || p.Panel != PanelKind.None) return false;
            var s = session.State;
            foreach (int id in Summary.HoldableIds(s, session.Hooks))
            {
                var drag = Movement.BeginDrag(s, id, session.Hooks.Drag);
                if (drag == null) continue;
                foreach (var node in drag.ReachableNodes())
                {
                    if (drag.Classify(node).Kind != DropKinds.Yard) continue;
                    if (p.Commit(new DragMove(id, node)).Status != MoveStatuses.Applied) continue;
                    sinceCommit = 0f;
                    return false;
                }
            }
            return Fail2("no yard move left");
        }

        /// <summary>Level 3 and later open the level-start window first (UX §4); levels 1 and 2 start at once.</summary>
        private bool AcceptLevelStart()
        {
            if (app.Flow.Current == AppScreen.LevelStart) app.PanelAction(app.Player, PanelAction.Play);
            return true;
        }

        /// <summary>Plays the golden moves that stay in the yard (digging) until the next one is a drag to the site: that one is previewed and shown.</summary>
        private bool Approach(float dtMs)
        {
            var p = app.Player;
            var session = p.Session;
            if (session == null) return Fail2("no session");
            if (!Idle() || sinceCommit < 450f || p.Panel != PanelKind.None) return false;
            var next = DebugSolver.NextSolutionMove(session, Sol());
            if (next == null) return true; // off the solution: preview whatever comes
            if (next is DragMove dm && GoesToSite(session, dm)) return true;
            p.Commit(next);
            sinceCommit = 0f;
            return false;
        }

        private static bool GoesToSite(GameSession session, DragMove move)
        {
            var drag = Movement.BeginDrag(session.State, move.PieceId, session.Hooks.Drag);
            if (drag == null) return false;
            string kind = drag.Classify(move.To).Kind;
            return kind == DropKinds.SiteFree || kind == DropKinds.SiteRail;
        }

        private bool DragDown()
        {
            var p = app.Player;
            var session = p.Session;
            var L = p.Layout;
            if (session == null || L == null) return false;
            preview = DebugSolver.NextSolutionMove(session, Sol()) as DragMove;
            if (preview == null)
            {
                Say("note:no golden drag to preview");
                dragT = 1f; // the preview steps pass at once
                return true;
            }
            var s = session.State;
            if (!BoardPose.RestAnchor(s, preview.PieceId, out int ax, out int ay)) return false;
            var shape = BoardPose.ShapeOf(s, preview.PieceId);
            var g = L.Grid;
            var cell = shape.Cells[0];
            var rect = g.CellRect(ax + cell.X, ay + cell.Y);
            startX = rect.CenterX;
            startY = rect.CenterY;
            var from = BoardPose.Center(g, ax, ay, shape);
            var to = BoardPose.Center(g, preview.To.Ix, preview.To.Iy, shape);
            // all the way to the golden release node, the finger 1,2 cells BELOW the block (K-08: the block rides above the finger), but not
            // released: the block is lifted over the wall and its K-18 shadow shows the correct spot
            endX = startX + (to.X - from.X);
            endY = startY + (to.Y - from.Y) + p.Feel.FingerOffsetCells * g.CellPx;
            dragT = 0f;
            app.PointerDown(0, startX, startY);
            return true;
        }

        private bool DragStep(float dtMs)
        {
            if (preview == null) return true;
            dragT = Math.Min(1f, dragT + dtMs / 800f);
            double x = startX + (endX - startX) * dragT;
            double y = startY + (endY - startY) * dragT;
            app.PointerMove(0, x, y);
            return dragT >= 1f;
        }

        /// <summary>Commits the next move of the golden solution (the greedy correct drag when the attempt left it).</summary>
        private bool CommitNext()
        {
            var p = app.Player;
            var session = p.Session;
            if (session == null) return false;
            var move = DebugSolver.Next(session, Sol());
            if (move == null) return false;
            p.Commit(move);
            sinceCommit = 0f;
            return true;
        }

        /// <summary>E3: restarts the level screen on level `n` (the debug panel's level jump).</summary>
        private bool DebugLevel(int n)
        {
            if (!RequireScreen(AppScreen.Level)) return false;
            app.DebugStartLevel(n);
            return app.Player.LevelNumber == n;
        }

        private bool Idle() => !app.Player.Queue.Busy && app.Player.Clock.Count == 0 && app.Player.CoinsInFlight == 0;

        /// <summary>Waits until the board is idle for `settleMs`, then runs `then` once.</summary>
        private Run WaitIdleThen(float settleMs, Func<bool> then) => _ =>
        {
            if (!Idle())
            {
                sinceCommit = 0f;
                return false;
            }
            if (sinceCommit < settleMs) return false;
            if (!then()) Fail(Current);
            return true;
        };

        /// <summary>Plays the rest of the golden solution, one move per settled board, until the level is decided.</summary>
        private bool Solve(float dtMs)
        {
            var p = app.Player;
            var session = p.Session;
            if (session == null) return Fail2("no session");
            if (session.Outcome != BoosterOutcomes.Playing) return true;
            if (!Idle() || sinceCommit < 350f || p.Panel != PanelKind.None) return false;
            var move = DebugSolver.Next(session, Sol());
            if (move == null) return Fail2("no move left");
            p.Commit(move);
            sinceCommit = 0f;
            return false;
        }

        private bool Fail2(string why)
        {
            Fail(why);
            return true;
        }

        /// <summary>The win window is open and its rewards (coins, stars) have settled.</summary>
        private bool WaitWin(float dtMs)
        {
            var p = app.Player;
            if (p.Session != null && p.Session.Outcome != BoosterOutcomes.Won)
            {
                if (p.Session.Outcome == BoosterOutcomes.Playing) return Fail2("level still playing");
                return Fail2("outcome " + p.Session.Outcome);
            }
            if (p.Panel != PanelKind.Win)
            {
                sinceCommit = 0f;
                return false;
            }
            if (!Idle())
            {
                sinceCommit = 0f;
                return false;
            }
            if (sinceCommit < 1800f) return false;
            Shot("win");
            return true;
        }
    }
}
