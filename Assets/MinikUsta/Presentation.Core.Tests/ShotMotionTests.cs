#nullable enable
// Paket F1 (P4B-08): the motion windows of the CI screenshot driver. `?shots=1&motion=0.1` plays the home king's arrival wave and the win
// window's big cheer at a tenth of real time and reports `seq:<name>:begin`, one `seq:<name>:t=<game ms>` per frame and `seq:<name>:end`,
// so the Playwright script can photograph every frame of the animation on a browser that draws a few frames per second and measure how
// many frames differ from the previous one (tools/ci-shots/shoot.mjs).
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Shots;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class ShotMotionTests
    {
        private static SessionAction[]? Solution(int level) =>
            DebugSolver.SolutionLog(PresentationPaths.ResourceText("solutions/level_" + level.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".solver"));

        /// <summary>Plays the scenario like GameRunner does: the app gets the frame time times `TimeScale`, the scenario the real frame time.</summary>
        private static (AppRig rig, ShotScenario sc, List<string> lines, List<float> scales) Play(ShotOptions? o)
        {
            var rig = new AppRig();
            rig.App.Boot();
            var lines = new List<string>();
            var sc = new ShotScenario(rig.App, Solution, lines.Add, o);
            var scales = new List<float>();
            float t = 0;
            // the windows take MotionGameMs / MotionScale real ms each; the scenario's own timeout leaves them out
            float windows = o != null && o.MotionScale > 0f ? 2f * o.MotionGameMs / o.MotionScale : 0f;
            while (!sc.Done && t < ShotScenario.TimeoutMs + windows + 1000f)
            {
                rig.App.Tick(Rig.FrameMs * sc.TimeScale);
                sc.Tick(Rig.FrameMs);
                scales.Add(sc.TimeScale);
                t += Rig.FrameMs;
            }
            return (rig, sc, lines, scales);
        }

        [Test]
        public void P4B08_without_motion_the_scenario_has_no_windows_and_never_changes_the_time_scale()
        {
            var (_, sc, lines, scales) = Play(new ShotOptions());
            Assert.IsTrue(sc.Done && !sc.Failed);
            CollectionAssert.IsEmpty(lines.FindAll(l => l.Contains("seq:")));
            CollectionAssert.AreEqual(ShotScenario.ShotNames, sc.Signalled.ToArray());
            foreach (float s in scales) Assert.AreEqual(1f, s);
        }

        [Test]
        public void P4B08_the_two_windows_come_right_before_their_shots_with_one_t_line_per_frame_and_the_scale_inside()
        {
            var (_, sc, lines, scales) = Play(new ShotOptions { MotionScale = 0.1f });
            Assert.IsTrue(sc.Done && !sc.Failed, string.Join(" | ", lines));
            CollectionAssert.AreEqual(ShotScenario.ShotNames, sc.Signalled.ToArray(), "the shots are unchanged and in order");
            foreach (var (name, before) in ShotScenario.SequenceNames)
            {
                string tag = ShotScenario.Tag + " seq:" + name;
                int begin = lines.IndexOf(tag + ":begin");
                int end = lines.IndexOf(tag + ":end");
                int shot = lines.IndexOf(ShotScenario.Tag + " shot:" + before);
                Assert.That(begin, Is.GreaterThanOrEqualTo(0), name + " begins");
                Assert.That(end, Is.GreaterThan(begin), name + " ends after it began");
                Assert.That(shot, Is.GreaterThan(end), name + " is over before the shot " + before);
                var ts = new List<int>();
                for (int i = begin + 1; i < end; i++)
                {
                    Assert.IsTrue(lines[i].StartsWith(tag + ":t="), "only t lines inside the window: " + lines[i]);
                    ts.Add(int.Parse(lines[i].Substring((tag + ":t=").Length), System.Globalization.CultureInfo.InvariantCulture));
                }
                Assert.That(ts.Count, Is.GreaterThan(100), "a frame line every frame: " + ts.Count);
                for (int i = 1; i < ts.Count; i++) Assert.That(ts[i], Is.GreaterThanOrEqualTo(ts[i - 1]), "game time never goes back");
                Assert.That(ts[ts.Count - 1], Is.GreaterThanOrEqualTo(1700).And.LessThan(1900), name + " lasts about MotionGameMs (1800)");
            }
            // inside a window the scale is the asked one, outside it is 1
            int slow = scales.FindAll(s => s == 0.1f).Count;
            int normal = scales.FindAll(s => s == 1f).Count;
            Assert.AreEqual(scales.Count, slow + normal, "only the two values");
            Assert.That(slow, Is.GreaterThan(400), "two windows of 1800 game ms at 0.1 = about 216 frames at 16.7 ms: " + slow);
        }

        [Test]
        public void P4B08_the_home_window_plays_the_arrival_wave_and_the_win_window_the_big_cheer()
        {
            var rig = new AppRig();
            rig.App.Boot();
            var lines = new List<string>();
            var sc = new ShotScenario(rig.App, Solution, lines.Add, new ShotOptions { MotionScale = 0.1f });
            var homeClips = new HashSet<string>();
            var windowClips = new HashSet<string>();
            float t = 0;
            while (!sc.Done && t < ShotScenario.TimeoutMs)
            {
                rig.App.Tick(Rig.FrameMs * sc.TimeScale);
                sc.Tick(Rig.FrameMs);
                t += Rig.FrameMs;
                if (rig.App.King.Home.Playing != null) homeClips.Add(rig.App.King.Home.Playing);
                if (rig.App.King.Window.Playing != null) windowClips.Add(rig.App.King.Window.Playing);
            }
            Assert.IsTrue(sc.Done && !sc.Failed);
            CollectionAssert.Contains(homeClips, KingDirector.WaveClip, "the home king waves while the first window is open");
            CollectionAssert.Contains(windowClips, KingDirector.BigCheerClip, "the win window's king cheers while the second window is open");
        }

        [Test]
        public void P4B08_options_read_motion_and_motionMs_within_bounds_and_ignore_the_rest()
        {
            var o = ShotOptions.FromUrl("http://h/?shots=1&motion=0.1&motionMs=3000")!;
            Assert.AreEqual(0.1f, o.MotionScale);
            Assert.AreEqual(3000f, o.MotionGameMs);
            var off = ShotOptions.FromUrl("http://h/?shots=1")!;
            Assert.AreEqual(0f, off.MotionScale, "no motion parameter, no windows");
            Assert.AreEqual(1800f, off.MotionGameMs);
            var bad = ShotOptions.FromUrl("http://h/?shots=1&motion=5&motionMs=10")!;
            Assert.AreEqual(0f, bad.MotionScale, "a scale above 1 is ignored");
            Assert.AreEqual(1800f, bad.MotionGameMs, "a window under 500 ms is ignored");
            Assert.AreEqual(0f, ShotOptions.FromUrl("http://h/?shots=1&motion=0.004")!.MotionScale, "a scale under 0.005 is ignored");
            Assert.AreEqual(0.0125f, ShotOptions.FromUrl("http://h/?shots=1&motion=0.0125")!.MotionScale);
            Assert.IsNull(ShotOptions.FromUrl("http://h/?motion=0.1"), "motion alone does not open the driver");
        }

        [Test]
        public void P4B08_ci_shots_json_lists_the_same_sequences_as_the_scenario()
        {
            string path = System.IO.Path.Combine(PresentationPaths.RepoRoot, "tools", "ci-shots", "shots.json");
            var json = MinikUsta.Core.Json.JsonReader.Parse(System.IO.File.ReadAllText(path));
            var seqs = new List<(string, string)>();
            foreach (var s in json["sequences"]!.Items) seqs.Add((s["name"]!.AsString, s["before"]!.AsString));
            CollectionAssert.AreEqual(ShotScenario.SequenceNames, seqs.ToArray());
            Assert.AreEqual(0.0125, json["motionScale"]!.AsDouble, 1e-9);
            Assert.AreEqual(1800, json["motionMs"]!.AsDouble, 1e-9);
            foreach (var s in json["sequences"]!.Items)
            {
                var f = s["focus"]!;
                Assert.That(f["x"]!.AsDouble + f["w"]!.AsDouble, Is.LessThanOrEqualTo(1.0 + 1e-9), s["name"]!.AsString + " focus fits the picture");
                Assert.That(f["y"]!.AsDouble + f["h"]!.AsDouble, Is.LessThanOrEqualTo(1.0 + 1e-9), s["name"]!.AsString + " focus fits the picture");
            }
        }
    }
}
