#nullable enable
// Paket C1: the CI screenshot driver (`?shots=1`). The scenario plays the real game on fakes: home → settings → Bölüm 1 → a cancelled
// pointer drag → the golden solution → the win window, signalling each moment; the tests check the signals, their order, that the
// scenario ends without an error, that it really won the level, and that the URL parser opens it only for `shots=1`.
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Shots;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class ShotScenarioTests
    {
        private static SessionAction[]? Solution(int level) =>
            DebugSolver.SolutionLog(PresentationPaths.ResourceText("solutions/level_" + level.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".solver"));

        private static (AppRig rig, ShotScenario scenario, List<string> lines) Start(ShotOptions? o = null)
        {
            var rig = new AppRig();
            rig.App.Boot();
            var lines = new List<string>();
            var sc = new ShotScenario(rig.App, Solution, lines.Add, o);
            return (rig, sc, lines);
        }

        private static void Run(AppRig rig, ShotScenario sc, float maxMs = ShotScenario.TimeoutMs)
        {
            float t = 0;
            while (!sc.Done && t < maxMs)
            {
                rig.App.Tick(Fixtures.Rig.FrameMs);
                sc.Tick(Fixtures.Rig.FrameMs);
                t += Fixtures.Rig.FrameMs;
            }
        }

        [Test]
        public void C1_shots_scenario_signals_every_shot_in_order_and_wins_level_1()
        {
            var (rig, sc, lines) = Start();
            Run(rig, sc);
            Assert.IsTrue(sc.Done, "the scenario finished within its own timeout; at " + sc.Current);
            Assert.IsFalse(sc.Failed, "no error line: " + string.Join(" | ", lines));
            CollectionAssert.AreEqual(ShotScenario.ShotNames, sc.Signalled.ToArray());
            foreach (var name in ShotScenario.ShotNames) CollectionAssert.Contains(lines, ShotScenario.Tag + " shot:" + name);
            Assert.AreEqual(ShotScenario.Tag + " done", lines[lines.Count - 1]);
            Assert.IsTrue(rig.App.Save.Won(1), "the golden solution wins Bölüm 1");
            Assert.IsTrue(rig.App.Save.Won(2), "U2: …and Bölüm 2, so Bölüm 3 opens its level start window");
            Assert.AreEqual(3, rig.Player.LevelNumber);
            Assert.AreEqual(BoosterOutcomes.OutOfMoves, rig.Session.Outcome, "U2: Bölüm 3 ends out of moves");
            Assert.AreEqual(PanelKind.Lose, rig.Player.Panel, "the scenario ends with the +5 offer window open");
            Assert.AreEqual(AppScreen.LoseOffer, rig.Screen);
        }

        [Test]
        public void U2_P0_11_shots_scenario_shows_pause_exit_confirm_level_3_start_window_and_the_offer()
        {
            var (rig, sc, lines) = Start();
            var seen = new Dictionary<string, (AppScreen Screen, PanelKind Panel, int Level)>();
            float t = 0;
            while (!sc.Done && t < ShotScenario.TimeoutMs)
            {
                int before = sc.Signalled.Count;
                rig.App.Tick(Fixtures.Rig.FrameMs);
                sc.Tick(Fixtures.Rig.FrameMs);
                t += Fixtures.Rig.FrameMs;
                if (sc.Signalled.Count > before) seen[sc.Signalled[sc.Signalled.Count - 1]] = (rig.Screen, rig.Player.Panel, rig.Player.LevelNumber);
            }
            Assert.IsFalse(sc.Failed, string.Join(" | ", lines));
            Assert.AreEqual((AppScreen.Paused, PanelKind.Pause, 2), seen["pause"]);
            Assert.AreEqual((AppScreen.ExitConfirm, PanelKind.ExitConfirm, 2), seen["exit_confirm"]);
            Assert.AreEqual((AppScreen.LevelStart, PanelKind.LevelStart, 2), seen["level_start_window"], "Bölüm 3's window opens over the last board (Bölüm 2)");
            Assert.AreEqual((AppScreen.LoseOffer, PanelKind.Lose, 3), seen["lose_offer"]);
            Assert.Less(t, ShotScenario.TimeoutMs * 0.6f, "the longer scenario keeps a wide margin under its own timeout");
        }

        [Test]
        public void C1_shots_scenario_signals_the_tutorial_shot_while_the_step_is_on_screen()
        {
            var (rig, sc, _) = Start();
            float t = 0;
            while (!sc.Signalled.Contains("tutorial") && !sc.Done && t < ShotScenario.TimeoutMs)
            {
                rig.App.Tick(Fixtures.Rig.FrameMs);
                sc.Tick(Fixtures.Rig.FrameMs);
                t += Fixtures.Rig.FrameMs;
            }
            Assert.IsTrue(sc.Signalled.Contains("tutorial"));
            var tp = rig.Player.Tutorial!;
            Assert.AreEqual(Tutorial.PresencePhase.Shown, tp.Presence!.Phase, "the tutorial shot is taken with the step shown");
            Assert.IsTrue(tp.View.Bubble!.Visible);
            Assert.IsTrue(tp.View.Glove!.Visible, "…and the glove on its path");
            Assert.IsFalse(sc.Signalled.Contains("level_start") && tp.Presence.Shows == 0);
        }

        [Test]
        public void C1_shots_scenario_is_deterministic()
        {
            var (rigA, a, linesA) = Start();
            var (rigB, b, linesB) = Start();
            Run(rigA, a);
            Run(rigB, b);
            CollectionAssert.AreEqual(linesA, linesB);
            Assert.AreEqual(rigA.Session.Log.Count, rigB.Session.Log.Count);
        }

        [Test]
        public void C1_shots_scenario_holds_still_after_each_signal()
        {
            // after a shot signal nothing is driven for HoldMs: the committed action count does not change during the hold
            var rig = new AppRig();
            rig.App.Boot();
            var lines = new List<string>();
            var sc = new ShotScenario(rig.App, Solution, lines.Add, new ShotOptions { HoldMs = 1500f });
            int logAtShot = -1;
            float sinceShot = 0;
            int shots = 0;
            float t = 0;
            while (!sc.Done && t < ShotScenario.TimeoutMs)
            {
                rig.App.Tick(Fixtures.Rig.FrameMs);
                sc.Tick(Fixtures.Rig.FrameMs);
                t += Fixtures.Rig.FrameMs;
                if (sc.Signalled.Count > shots)
                {
                    shots = sc.Signalled.Count;
                    logAtShot = rig.Player.Session?.Log.Count ?? -1;
                    sinceShot = 0;
                }
                else
                {
                    sinceShot += Fixtures.Rig.FrameMs;
                    if (shots > 0 && sinceShot < 1400f && !sc.Done && shots < ShotScenario.ShotNames.Length)
                        Assert.AreEqual(logAtShot, rig.Player.Session?.Log.Count ?? -1, "no move is committed in the hold after shot " + shots);
                }
            }
            Assert.IsFalse(sc.Failed);
        }

        [Test]
        public void C1_shots_scenario_drag_preview_lifts_a_block_over_the_wall_with_its_shadow_and_commits_nothing()
        {
            var (rig, sc, _) = Start();
            float t = 0;
            // run until the drag_mid shot is signalled
            while (!sc.Signalled.Contains("drag_mid") && !sc.Done && t < ShotScenario.TimeoutMs)
            {
                rig.App.Tick(Fixtures.Rig.FrameMs);
                sc.Tick(Fixtures.Rig.FrameMs);
                t += Fixtures.Rig.FrameMs;
            }
            Assert.IsTrue(sc.Signalled.Contains("drag_mid"));
            Assert.IsTrue(rig.Player.Mapper.Active, "the finger is down: a drag is in progress at the drag_mid shot");
            var golden = DebugSolver.SolutionLog(PresentationPaths.ResourceText("solutions/level_001.solver"))!;
            // every move committed so far is the golden one (the approach digs in the yard); the previewed move is the next golden one
            for (int i = 1; i < rig.Session.Log.Count; i++) Assert.IsTrue(SessionActionJson.SameAs(rig.Session.Log[i], golden[i]), "log " + i + " follows the golden solution");
            var next = (DragMove)golden[rig.Session.Log.Count];
            Assert.AreEqual(next.PieceId, rig.Player.Mapper.PieceId, "the lifted block is the one the golden solution moves next");
            var drop = rig.Player.Mapper.Session!.Classify();
            Assert.IsTrue(drop.Kind == DropKinds.SiteFree || drop.Kind == DropKinds.SiteRail, "the preview is over the wall: " + drop.Kind);
            Assert.IsNotNull(rig.View.Shadow, "the K-18 shadow of the lifted block is on the site");
            Assert.AreEqual(ShadowBadge.Ok, rig.View.Shadow!.Look.Badge, "…and it is the green correct-spot look (the preview rides to the golden release node)");
            Assert.AreEqual(next.To.Ix, rig.Player.Mapper.Session!.Current.Ix, "the block is at the golden release node");
        }

        [Test]
        public void C1_ci_shots_json_lists_the_same_shots_and_tag_as_the_scenario()
        {
            // tools/ci-shots/shots.json is what the Playwright script waits for; the scenario is what signals: they must name the same shots
            string path = System.IO.Path.Combine(PresentationPaths.RepoRoot, "tools", "ci-shots", "shots.json");
            var json = MinikUsta.Core.Json.JsonReader.Parse(System.IO.File.ReadAllText(path));
            var names = new List<string>();
            foreach (var n in json["shots"]!.Items) names.Add(n.AsString);
            CollectionAssert.AreEqual(ShotScenario.ShotNames, names.ToArray());
            Assert.AreEqual(ShotScenario.Tag, json["tag"]!.AsString);
            var vps = new List<string>();
            foreach (var v in json["viewports"]!.Items) vps.Add(v["name"]!.AsString);
            CollectionAssert.AreEqual(new[] { "390x844", "360x800" }, vps.ToArray());
        }

        [TestCase("https://x.github.io/minik-usta-unity/?shots=1", true)]
        [TestCase("https://x.github.io/minik-usta-unity/index.html?a=2&shots=1#top", true)]
        [TestCase("https://x.github.io/minik-usta-unity/?shots=0", false)]
        [TestCase("https://x.github.io/minik-usta-unity/", false)]
        [TestCase("https://x.github.io/minik-usta-unity/?shotsX=1", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void C1_shot_options_open_only_for_shots_1(string? url, bool opened)
        {
            Assert.AreEqual(opened, ShotOptions.FromUrl(url) != null);
        }

        [Test]
        public void C1_shot_options_read_hold_within_bounds()
        {
            var o = ShotOptions.FromUrl("http://h/?shots=1&shotsHold=4000")!;
            Assert.AreEqual(4000f, o.HoldMs);
            var bad = ShotOptions.FromUrl("http://h/?shots=1&shotsHold=5")!;
            Assert.AreEqual(2500f, bad.HoldMs, "a hold under 200 ms is ignored");
        }
    }
}
