#nullable enable
// The whole game (`GameApp`) on fakes, driven like the Unity layer drives it: a FakeView whose HUD is the real `LevelHud` on a
// `FakeSurface`, the repo's levels / i18n / economy.json, an in-memory save store, fixed frames. Taps go through the surface
// (only enabled, visible buttons fire), so a test sees exactly what a finger can reach.
using System;
using MinikUsta.Core;
using MinikUsta.Presentation.Flow;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.I18n;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Meta;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Ui;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public sealed class AppRig
    {
        public readonly FakeView View = new FakeView();
        public readonly FakeSurface Ui = new FakeSurface();
        public readonly MemorySaveStore Store;
        public readonly GameApp App;
        public readonly Localizer I18n;

        public AppRig(MemorySaveStore? store = null, double w = 1080, double h = 1920)
        {
            Store = store ?? new MemorySaveStore();
            I18n = new Localizer(PresentationPaths.ResourceText);
            var layout = new LayoutSettings();
            GameApp? app = null;
            View.Hud = new LevelHud(Ui, I18n.Text, layout, () => app!.Player.PausePressed());
            app = new GameApp(View, Ui, new LevelCatalog(Rig.ReadLevel), I18n, Economy(), Store, null, layout);
            App = app;
            App.SetScreen(w, h);
        }

        public static EconomyConfig Economy() => EconomyConfig.FromText(PresentationPaths.ResourceText("config/economy"));

        public LevelPlayer Player => App.Player;
        public GameSession Session => App.Player.Session!;
        public AppScreen Screen => App.Flow.Current;

        public void Tick(float ms)
        {
            while (ms > 1e-4f)
            {
                float d = Math.Min(Rig.FrameMs, ms);
                App.Tick(d);
                ms -= d;
            }
        }

        /// <summary>Frames until no step is pending and no tween runs.</summary>
        public void Settle(float maxMs = 30000)
        {
            float t = 0;
            while ((Player.Queue.Busy || Player.Clock.Count > 0 || Player.CoinsInFlight > 0) && t < maxMs)
            {
                App.Tick(Rig.FrameMs);
                t += Rig.FrameMs;
            }
            App.Tick(Rig.FrameMs);
        }

        public void Tap(string id) => Assert.IsTrue(Ui.Tap(id), "tap " + id + ": not a visible, enabled button");

        /// <summary>The window option firing `action` (fails when the open window has none).</summary>
        public UiNode Option(PanelAction action)
        {
            var w = Ui.Root(UiLayer.Window);
            Assert.IsNotNull(w, "a window is open");
            foreach (var n in w!.All())
                if (n.Action == WindowViews.ActionOf(action)) return n;
            throw new AssertionException("the window has no " + action + " option");
        }

        public void TapOption(PanelAction action) => Tap(Option(action).Id);

        /// <summary>Commits the drags of the level's golden solution (each plan settles first).</summary>
        public void PlayGolden(int level, int count = int.MaxValue)
        {
            int i = 0;
            foreach (var m in Play.SolverGolden(level).Drags)
            {
                if (i++ >= count) break;
                Settle();
                var r = Player.Commit(m);
                Assert.AreEqual(MoveStatuses.Applied, r.Status, "golden move " + i + " of level " + level);
            }
            Settle();
        }
    }
}
