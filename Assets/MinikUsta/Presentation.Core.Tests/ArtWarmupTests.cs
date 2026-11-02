#nullable enable
// Paket F1 (P4B-05): the 3D clip pages are decoded where a longer frame cannot be seen. The king pages at boot, the fx pages when the
// player leaves the home screen (or after a calm 2,5 s on it), never during play; one decode per library.
using MinikUsta.Presentation.Anim;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class ArtWarmupTests
    {
        [Test]
        public void P4B05_the_king_pages_are_warmed_once_at_boot_and_the_fx_pages_wait_for_the_player()
        {
            var w = new ArtWarmup(_ => { });
            w.WarmKing();
            w.WarmKing();
            CollectionAssert.AreEqual(new[] { ArtWarmup.KingLibrary }, w.Warmed, "king once, however often it is asked");
            w.Tick(16.7, AppScreen.Boot);
            w.Tick(16.7, AppScreen.Home);
            Assert.IsFalse(w.FxDone, "a home screen that has been up for 33 ms does not decode the fx pages");
        }

        [Test]
        public void P4B05_the_fx_pages_are_warmed_when_the_player_leaves_the_home_screen()
        {
            var called = new System.Collections.Generic.List<string>();
            var w = new ArtWarmup(called.Add);
            w.WarmKing();
            w.Tick(16.7, AppScreen.Home);
            w.Tick(16.7, AppScreen.LevelStart); // 'Oyna' pressed: the level-start window is up, the first placement is seconds away
            CollectionAssert.AreEqual(new[] { "king", "fx" }, called);
            w.Tick(16.7, AppScreen.Level);
            w.Tick(16.7, AppScreen.Home);
            Assert.AreEqual(2, called.Count, "fx is decoded once; later frames and screens never call the decoder again");
        }

        [Test]
        public void P4B05_the_fx_pages_are_warmed_after_a_calm_home_screen_and_never_before_the_idle_time()
        {
            var called = new System.Collections.Generic.List<string>();
            var w = new ArtWarmup(called.Add, 1000);
            w.WarmKing();
            for (int i = 0; i < 59; i++) w.Tick(16.7, AppScreen.Home); // 985 ms
            Assert.IsFalse(w.FxDone);
            w.Tick(16.7, AppScreen.Home); // 1001.9 ms
            Assert.IsTrue(w.FxDone);
            CollectionAssert.AreEqual(new[] { "king", "fx" }, called);
        }

        [Test]
        public void P4B05_a_resumed_level_decodes_the_fx_pages_on_its_first_frame_and_the_boot_screen_decodes_nothing()
        {
            var called = new System.Collections.Generic.List<string>();
            var w = new ArtWarmup(called.Add);
            w.Tick(16.7, AppScreen.Boot);
            CollectionAssert.IsEmpty(called);
            w.Tick(16.7, AppScreen.Level); // K-43 resume goes straight to the board
            CollectionAssert.AreEqual(new[] { "fx" }, called);
        }

        [Test]
        public void P4B05_the_unity_layer_warms_the_king_in_Awake_before_the_app_boots_and_ticks_the_warm_up_every_frame()
        {
            // the Unity layer is not compiled here (dotnet/UnityStubs gate); the call order is part of the contract, so it is pinned in text
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(PresentationPaths.RepoRoot, "Assets", "MinikUsta", "Unity", "GameRunner.cs"));
            int load = src.IndexOf("ClipSprites.Load()", System.StringComparison.Ordinal);
            int warm = src.IndexOf("warmup.WarmKing()", System.StringComparison.Ordinal);
            int boot = src.IndexOf("app.Boot()", System.StringComparison.Ordinal);
            Assert.That(load, Is.GreaterThan(0));
            Assert.That(warm, Is.GreaterThan(load), "the king is warmed after the libraries are read");
            Assert.That(boot, Is.GreaterThan(warm), "…and before the home screen is built");
            StringAssert.Contains("warmup?.Tick(dtMs, a.Flow.Current)", src);
            StringAssert.Contains("new ArtWarmup(ClipSprites.Warm)", src);
        }
    }
}
