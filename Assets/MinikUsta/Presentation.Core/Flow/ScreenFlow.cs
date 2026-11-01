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
    public enum Screen
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
        private static readonly Dictionary<(Screen, FlowEvent), Screen> Table = new Dictionary<(Screen, FlowEvent), Screen>
        {
            { (Screen.Boot, FlowEvent.Booted), Screen.Home },
            { (Screen.Boot, FlowEvent.Resume), Screen.Level },
            { (Screen.Boot, FlowEvent.ResumeOffer), Screen.LoseOffer },
            { (Screen.Home, FlowEvent.PlayDirect), Screen.Level },
            { (Screen.Home, FlowEvent.PlayWindow), Screen.LevelStart },
            { (Screen.LevelStart, FlowEvent.StartLevel), Screen.Level },
            { (Screen.LevelStart, FlowEvent.Close), Screen.Home },
            { (Screen.Level, FlowEvent.Won), Screen.Win },
            { (Screen.Level, FlowEvent.OutOfMoves), Screen.LoseOffer },
            { (Screen.Level, FlowEvent.Lost), Screen.LoseFinal },
            { (Screen.Level, FlowEvent.Pause), Screen.Paused },
            { (Screen.Paused, FlowEvent.Continue), Screen.Level },
            { (Screen.Paused, FlowEvent.Exit), Screen.ExitConfirm },
            { (Screen.ExitConfirm, FlowEvent.Stay), Screen.Paused },
            { (Screen.ExitConfirm, FlowEvent.LeaveFree), Screen.Home },
            { (Screen.ExitConfirm, FlowEvent.LeaveLoss), Screen.LoseFinal },
            { (Screen.ExitConfirm, FlowEvent.Restart), Screen.Level },
            { (Screen.Win, FlowEvent.Continue), Screen.Home },
            { (Screen.LoseOffer, FlowEvent.Accept), Screen.Level },
            { (Screen.LoseOffer, FlowEvent.Decline), Screen.LoseFinal },
            { (Screen.LoseFinal, FlowEvent.RetryDirect), Screen.Level },
            { (Screen.LoseFinal, FlowEvent.RetryWindow), Screen.LevelStart },
            { (Screen.LoseFinal, FlowEvent.Home), Screen.Home },
        };

        public Screen Current { get; private set; } = Screen.Boot;

        /// <summary>The settings overlay is open (over Home or Paused).</summary>
        public bool SettingsOpen { get; private set; }

        /// <summary>Screens visited, in order (tests, debug).</summary>
        public List<Screen> History { get; } = new List<Screen> { Screen.Boot };

        public event Action<Screen, Screen>? Changed;

        /// <summary>UX §2 / §4: levels 1–2 start without the level start window.</summary>
        public static bool SkipsLevelStart(int level) => level <= 2;

        public bool Can(FlowEvent e) => Table.ContainsKey((Current, e));

        public Screen Fire(FlowEvent e)
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
        public bool BoardShown => Current == Screen.Level || Current == Screen.Paused || Current == Screen.ExitConfirm || Current == Screen.Win
            || Current == Screen.LoseOffer || Current == Screen.LoseFinal;

        public bool CanOpenSettings => Current == Screen.Home || Current == Screen.Paused;

        public void OpenSettings()
        {
            if (!CanOpenSettings) throw new InvalidOperationException("ScreenFlow: no settings over " + Current);
            SettingsOpen = true;
        }

        public void CloseSettings() => SettingsOpen = false;
    }
}
