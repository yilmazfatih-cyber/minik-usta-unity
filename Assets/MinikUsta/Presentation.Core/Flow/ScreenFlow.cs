#nullable enable
// Screen flow of the Unity build (paket V1; UX_FLOWS §12 reduced to the Faz 2R slice: no intro story, chest, shop, league).
// A pure state machine: `Fire(event)` moves to the next screen or throws on a move the flow does not have (a test can never
// reach a screen the UX does not draw). The settings panel is an overlay over Home or Paused (`SettingsOpen`); it does not
// change the screen.
//
//   Boot ──Booted──▶ Home ──PlayDirect (Bölüm 1–2, UX §2)──▶ Level
//                    Home ──PlayWindow (Bölüm ≥ 3, UX §4)──▶ LevelStart ──StartLevel──▶ Level ; LevelStart ──Close──▶ Home
//   Boot ──Resume (K-43)──▶ Level ; Boot ──ResumeOffer (K-43/3: the +5 window was open)──▶ LoseOffer
//   Level ──Won──▶ Win ──Continue──▶ Home            (UX §6: "Devam" → home; the home button is "Oyna · Bölüm N+1")
//   Level ──OutOfMoves (K-29, offer left)──▶ LoseOffer ──Accept──▶ Level ; ──Decline──▶ LoseFinal
//   Level ──Lost (no offer left)──▶ LoseFinal ──RetryDirect / RetryWindow──▶ Level / LevelStart ; ──Home──▶ Home
//   Level ──Pause──▶ Paused ──Resume──▶ Level ; ──Exit──▶ ExitConfirm ──Stay──▶ Paused
//   ExitConfirm ──LeaveFree (movesSpent 0, K-43/2)──▶ Home ; ──LeaveLoss (movesSpent ≥ 1)──▶ LoseFinal
//   ExitConfirm ──Restart (the pause "restart" intent: a new attempt at once)──▶ Level
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Flow
{
    public enum AppScreen
    {
        Boot,
        Home,
        LevelStart,
        Level,
        Paused,
        ExitConfirm,
        Win,
        LoseOffer,
        LoseFinal,
    }

    public enum FlowEvent
    {
        Booted,
        Resume,
        ResumeOffer,
        PlayDirect,
        PlayWindow,
        StartLevel,
        Close,
        Won,
        OutOfMoves,
        Lost,
        Pause,
        Continue,
        Exit,
        Stay,
        LeaveFree,
        LeaveLoss,
        Restart,
        Accept,
        Decline,
        RetryDirect,
        RetryWindow,
        Home,
    }

    public sealed class ScreenFlow
    {
        private static readonly Dictionary<(AppScreen, FlowEvent), AppScreen> Table = new Dictionary<(AppScreen, FlowEvent), AppScreen>
        {
            { (AppScreen.Boot, FlowEvent.Booted), AppScreen.Home },
            { (AppScreen.Boot, FlowEvent.Resume), AppScreen.Level },
            { (AppScreen.Boot, FlowEvent.ResumeOffer), AppScreen.LoseOffer },
            { (AppScreen.Home, FlowEvent.PlayDirect), AppScreen.Level },
            { (AppScreen.Home, FlowEvent.PlayWindow), AppScreen.LevelStart },
            { (AppScreen.LevelStart, FlowEvent.StartLevel), AppScreen.Level },
            { (AppScreen.LevelStart, FlowEvent.Close), AppScreen.Home },
            { (AppScreen.Level, FlowEvent.Won), AppScreen.Win },
            { (AppScreen.Level, FlowEvent.OutOfMoves), AppScreen.LoseOffer },
            { (AppScreen.Level, FlowEvent.Lost), AppScreen.LoseFinal },
            { (AppScreen.Level, FlowEvent.Pause), AppScreen.Paused },
            { (AppScreen.Paused, FlowEvent.Continue), AppScreen.Level },
            { (AppScreen.Paused, FlowEvent.Exit), AppScreen.ExitConfirm },
            { (AppScreen.ExitConfirm, FlowEvent.Stay), AppScreen.Paused },
            { (AppScreen.ExitConfirm, FlowEvent.LeaveFree), AppScreen.Home },
            { (AppScreen.ExitConfirm, FlowEvent.LeaveLoss), AppScreen.LoseFinal },
            { (AppScreen.ExitConfirm, FlowEvent.Restart), AppScreen.Level },
            { (AppScreen.Win, FlowEvent.Continue), AppScreen.Home },
            { (AppScreen.LoseOffer, FlowEvent.Accept), AppScreen.Level },
            { (AppScreen.LoseOffer, FlowEvent.Decline), AppScreen.LoseFinal },
            { (AppScreen.LoseFinal, FlowEvent.RetryDirect), AppScreen.Level },
            { (AppScreen.LoseFinal, FlowEvent.RetryWindow), AppScreen.LevelStart },
            { (AppScreen.LoseFinal, FlowEvent.Home), AppScreen.Home },
        };

        public AppScreen Current { get; private set; } = AppScreen.Boot;

        /// <summary>The settings overlay is open (over Home or Paused).</summary>
        public bool SettingsOpen { get; private set; }

        /// <summary>Screens visited, in order (tests, debug).</summary>
        public List<AppScreen> History { get; } = new List<AppScreen> { AppScreen.Boot };

        public event Action<AppScreen, AppScreen>? Changed;

        /// <summary>UX §2 / §4: levels 1–2 start without the level start window.</summary>
        public static bool SkipsLevelStart(int level) => level <= 2;

        public bool Can(FlowEvent e) => Table.ContainsKey((Current, e));

        public AppScreen Fire(FlowEvent e)
        {
            if (!Table.TryGetValue((Current, e), out var next))
                throw new InvalidOperationException("ScreenFlow: no " + e + " from " + Current);
            var prev = Current;
            Current = next;
            SettingsOpen = false;
            History.Add(next);
            Changed?.Invoke(prev, next);
            return next;
        }

        /// <summary>The board is on screen (the level runs or a level window is over it).</summary>
        public bool BoardShown => Current == AppScreen.Level || Current == AppScreen.Paused || Current == AppScreen.ExitConfirm || Current == AppScreen.Win
            || Current == AppScreen.LoseOffer || Current == AppScreen.LoseFinal;

        public bool CanOpenSettings => Current == AppScreen.Home || Current == AppScreen.Paused;

        public void OpenSettings()
        {
            if (!CanOpenSettings) throw new InvalidOperationException("ScreenFlow: no settings over " + Current);
            SettingsOpen = true;
        }

        public void CloseSettings() => SettingsOpen = false;
    }
}
