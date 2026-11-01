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
//   bubble and the glove on its path) → drag_mid (a real pointer drag, cancelled) → landed (first golden move committed and settled)
//   → … golden solution … → win (the win window and its rewards settled) → done
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
        public static readonly string[] ShotNames = { "home", "settings", "level_start", "tutorial", "drag_mid", "landed", "win" };

        /// <summary>Game milliseconds from the start after which the scenario gives up (`error:timeout`).</summary>
        public const float TimeoutMs = 240000f;

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
        private float sinceCommit;

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
        /// <summary>Name of the step being waited for / run (tests, logs).</summary>
        public string Current => index < steps.Count ? steps[index].Name : "done";
        /// <summary>Shots signalled so far, in order.</summary>
        public List<string> Signalled { get; } = new List<string>();

        // --- script ------------------------------------------------------------------------------------------------------------

        private void Build()
        {
            float hold = options.HoldMs;
            Add("boot", 2500f, Once(() => RequireScreen(AppScreen.Home) ? Shot("home") : false));
            Add("open_settings", hold, Once(() => TapHome(HomeView.SettingsAction)));
            Add("settings", 1000f, Once(() => Shot("settings")));
            Add("close_settings", hold, Once(CloseSettings));
            Add("play", 900f, Once(() => TapHome(HomeView.PlayAction)));
            Add("level_start_window", 600f, Once(AcceptLevelStart));
            Add("level_start", 500f, Once(() => RequireScreen(AppScreen.Level) ? Shot("level_start") : false));
            Add("tutorial", hold, Once(() => Shot("tutorial")));
            Add("drag_down", hold, Once(DragDown));
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
            Add("win", 0f, WaitWin);
            Add("done", hold, Once(() =>
            {
                Say("done");
                Done = true;
                return true;
            }));
        }

        private void Add(string name, float waitMs, Run run) => steps.Add(new Step(name, waitMs, run));

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
            if (total > TimeoutMs)
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

        /// <summary>Level 3 and later open the level-start window first (UX §4); levels 1 and 2 start at once.</summary>
        private bool AcceptLevelStart()
        {
            if (app.Flow.Current == AppScreen.LevelStart) app.PanelAction(app.Player, PanelAction.Play);
            return true;
        }

        private bool DragDown()
        {
            var p = app.Player;
            var session = p.Session;
            var L = p.Layout;
            if (session == null || L == null) return false;
            solution = solutions(p.LevelNumber);
            preview = DebugSolver.NextSolutionMove(session, solution) as DragMove;
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
            // 55 % of the way: far enough to lift and show the shadow, short of the release
            endX = startX + (to.X - from.X) * 0.55;
            endY = startY + (to.Y - from.Y) * 0.55;
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
            solution ??= solutions(p.LevelNumber);
            var move = DebugSolver.Next(session, solution);
            if (move == null) return false;
            p.Commit(move);
            sinceCommit = 0f;
            return true;
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
            var move = DebugSolver.Next(session, solution);
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
