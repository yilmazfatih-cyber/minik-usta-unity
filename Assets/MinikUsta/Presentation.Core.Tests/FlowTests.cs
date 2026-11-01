#nullable enable
// Paket V1: the screen flow state machine (UX_FLOWS §12 for the Faz 2R slice).
using System;
using MinikUsta.Presentation.Flow;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class FlowTests
    {
        [Test]
        public void UX12_boot_home_level_win_home()
        {
            var f = new ScreenFlow();
            Assert.AreEqual(AppScreen.Boot, f.Current);
            f.Fire(FlowEvent.Booted);
            f.Fire(FlowEvent.PlayDirect);
            Assert.AreEqual(AppScreen.Level, f.Current);
            f.Fire(FlowEvent.Won);
            Assert.AreEqual(AppScreen.Home, f.Fire(FlowEvent.Continue));
            Assert.AreEqual(new[] { AppScreen.Boot, AppScreen.Home, AppScreen.Level, AppScreen.Win, AppScreen.Home }, f.History.ToArray());
        }

        [Test]
        public void UX12_K29_K43_out_of_moves_offer_decline_loss_retry_and_exit_paths()
        {
            var f = new ScreenFlow();
            f.Fire(FlowEvent.Booted);
            f.Fire(FlowEvent.PlayWindow);
            Assert.AreEqual(AppScreen.LevelStart, f.Current);
            f.Fire(FlowEvent.StartLevel);
            f.Fire(FlowEvent.OutOfMoves);
            Assert.AreEqual(AppScreen.Level, f.Fire(FlowEvent.Accept));
            f.Fire(FlowEvent.OutOfMoves);
            Assert.AreEqual(AppScreen.LoseFinal, f.Fire(FlowEvent.Decline));
            Assert.AreEqual(AppScreen.LevelStart, f.Fire(FlowEvent.RetryWindow));
            f.Fire(FlowEvent.StartLevel);
            f.Fire(FlowEvent.Pause);
            f.Fire(FlowEvent.Exit);
            Assert.AreEqual(AppScreen.Paused, f.Fire(FlowEvent.Stay));
            f.Fire(FlowEvent.Exit);
            Assert.AreEqual(AppScreen.LoseFinal, f.Fire(FlowEvent.LeaveLoss), "K-43/2: movesSpent ≥ 1 → a loss");
            Assert.AreEqual(AppScreen.Home, f.Fire(FlowEvent.Home));
        }

        [Test]
        public void UX12_a_move_the_flow_does_not_have_throws_and_settings_open_only_over_home_or_pause()
        {
            var f = new ScreenFlow();
            Assert.Throws<InvalidOperationException>(() => f.Fire(FlowEvent.Won));
            Assert.IsFalse(f.Can(FlowEvent.Pause));
            f.Fire(FlowEvent.Booted);
            f.OpenSettings();
            Assert.IsTrue(f.SettingsOpen);
            f.Fire(FlowEvent.PlayDirect);
            Assert.IsFalse(f.SettingsOpen, "a screen change closes the settings");
            Assert.Throws<InvalidOperationException>(() => f.OpenSettings());
            Assert.IsTrue(f.BoardShown);
        }

        [Test]
        public void UX2_levels_1_and_2_skip_the_level_start_window()
        {
            Assert.IsTrue(ScreenFlow.SkipsLevelStart(1));
            Assert.IsTrue(ScreenFlow.SkipsLevelStart(2));
            Assert.IsFalse(ScreenFlow.SkipsLevelStart(3));
        }
    }
}
