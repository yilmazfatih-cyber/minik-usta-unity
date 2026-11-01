#nullable enable
// The whole Unity game without an engine (paket V1): save, languages, screen flow, home screen, settings and the level
// player with its windows. The Unity layer only builds the views (`ILevelView`, `IUiSurface`), the store (PlayerPrefs) and
// pumps frames and pointers into it; the NUnit tests drive the very same object with fakes (`GameAppTests`).
//
// Rules carried here (web services/save.ts, meta/*, UX_FLOWS §2–§7, §11–§12; GDD K-28, K-29, K-43; META §1–§4):
// - Boot: save → settings (language, reduce motion, vibration) → K-43 resume of a running attempt (same level hash and rules
//   version → `GameSession.Replay`; else the attempt is voided penalty-free: reserved life and offer coins back) → home.
// - Home "Oyna · Bölüm N" (N = the first level not won; after all ten the 1–10 loop): levels 1–2 start at once, from 3 the
//   level start window (UX §2, §4). First launch → Bölüm 1 in ONE tap.
// - Level start: a life is reserved (META §2: back on a win or a free exit); booster free trials are granted at unlock (META §4).
// - Every applied action is saved at once (the log replays the attempt); a win or a loss is written in the same commit that
//   produced it, before its window opens (a kill during the celebration never loses the reward).
// - Win (K-28): base coins + Bonus İnşaat + leftover trowels (economy.json), 1 star on the first win; a won level played again
//   (the loop) pays the base coins only (UX §6). "Devam" → home.
// - Out of moves (K-29): the +5 offer window (offers.ts): lifetime-first gift, escalating prices, grey "Altın yetmiyor" when the
//   coins are short (no shop yet), the ad option grey ("Şu an reklam yok"); "Hayır, teşekkürler" / × → loss.
// - Pause: continue, restart, home (+ the settings gear in the corner); restart and home go through the K-43/2 exit
//   confirmation (movesSpent 0: free, the reserved life comes back; ≥ 1: a loss).
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Audio;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.I18n;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Meta;
using MinikUsta.Presentation.Save;
using MinikUsta.Presentation.Text;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Flow
{
    public sealed class GameApp : ILevelHost
    {
        private enum ExitIntent
        {
            Home,
            Restart,
        }

        private readonly ILevelView view;
        private readonly IUiSurface ui;
        private readonly LevelCatalog catalog;
        private readonly ISaveStore store;
        private readonly Action<string> log;
        private ExitIntent exitIntent;
        private WinRewards? lastWin;
        private bool lastWinFirst;
        private bool lastLossStreak;
        private int pendingOfferPaid;
        private int pendingStartLevel;
        private double canvasW = 1080;
        private double canvasH = 1920;

        public GameApp(ILevelView view, IUiSurface ui, LevelCatalog catalog, Localizer i18n, EconomyConfig economy, ISaveStore store,
            FeelSettings? feel = null, LayoutSettings? layout = null, IHapticSink? haptics = null, EconomyInfo? economyInfo = null, Action<string>? log = null,
            AudioPlayer? audio = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            I18n = i18n ?? throw new ArgumentNullException(nameof(i18n));
            Economy = economy ?? new EconomyConfig();
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.log = log ?? (_ => { });
            Save = SaveModel.Default(Economy.StartCoins, Economy.StartLives);
            var info = economyInfo ?? new EconomyInfo { BonusCoinsPerMoveLeft = Economy.BonusCoinsPerMoveLeft, BonusMaxMovesCounted = Economy.BonusMaxMovesCounted };
            foreach (var kv in Economy.Boosters)
                if (info.Unlock.ContainsKey(kv.Key)) info.Unlock[kv.Key] = kv.Value.UnlockLevel;
            Player = new LevelPlayer(view, catalog, i18n.Text, feel, layout, haptics, info)
            {
                Host = this,
            };
            Audio = audio;
            Player.Audio = audio;
            Player.Tutorial = new MinikUsta.Presentation.Tutorial.TutorialPlayer(Player, ui);
            Player.BoosterCount = id => Save.BoosterCount(id);
            Player.LevelWon = n => Save.Won(n);
        }

        public LevelPlayer Player { get; }
        /// <summary>V2: the sounds (null = silent). The "Ses" setting drives `SoundOn`; WebGL unlocks it at the first user input.</summary>
        public AudioPlayer? Audio { get; }
        public Localizer I18n { get; }
        public EconomyConfig Economy { get; }
        public ScreenFlow Flow { get; } = new ScreenFlow();
        public SaveModel Save { get; private set; }
        public SaveLoadStatus LoadStatus { get; private set; }
        /// <summary>The home tree on screen (tests, compose dumps).</summary>
        public UiNode? Home { get; private set; }
        public UiNode? Settings { get; private set; }
        /// <summary>Taps the player made on UI buttons since boot (UX §2.1 "≤ 3 dokunuş" check).</summary>
        public int Taps { get; private set; }
        /// <summary>The last attempt was voided at boot (K-43/4) or the save was repaired.</summary>
        public string? BootNote { get; private set; }

        private Strings T => I18n.Text;

        // --- boot ---------------------------------------------------------------------------------------------------------

        public void Boot()
        {
            var load = SaveCodec.Load(store, SaveModel.Default(Economy.StartCoins, Economy.StartLives));
            Save = load.Data;
            LoadStatus = load.Status;
            if (load.Status == SaveLoadStatus.Corrupt || load.Status == SaveLoadStatus.FromBackup) log("Minik Usta: save " + load.Status + " (" + load.Error + ")");
            ApplySettings();
            if (Save.InLevel != null && TryResume(Save.InLevel)) return;
            Flow.Fire(FlowEvent.Booted);
            ShowHome();
        }

        private void ApplySettings()
        {
            var s = Save.Settings;
            I18n.SetLang(s.Lang);
            Player.Reduced = s.ReduceMotion;
            Player.Haptics.Enabled = s.Haptics;
            if (Audio != null)
            {
                Audio.SoundOn = s.Sound;
                Audio.MusicOn = s.Music;
            }
        }

        /// <summary>K-43 resume; false when the record was voided (penalty-free) and the app goes home.</summary>
        private bool TryResume(InLevelRecord rec)
        {
            var lvl = catalog.TryLoad(rec.LevelId, out string? error);
            string? why = null;
            GameSession? session = null;
            if (lvl == null) why = error;
            else if (rec.RulesVersion != GameSession.RulesVersion) why = "rules version " + rec.RulesVersion;
            else if (rec.LevelHash != GameSession.LevelHash(lvl.Data)) why = "level hash";
            else
            {
                try
                {
                    session = GameSession.Replay(lvl, rec.Actions);
                }
                catch (Exception e)
                {
                    why = e.Message;
                }
            }
            if (session == null || why != null)
            {
                VoidAttempt(rec, why ?? "replay");
                return false;
            }
            Player.StartSession(session, rec.LevelId);
            ShowBoard();
            if (session.Outcome == BoosterOutcomes.OutOfMoves)
            {
                Flow.Fire(FlowEvent.ResumeOffer);
                OpenOffer();
            }
            else Flow.Fire(FlowEvent.Resume);
            return true;
        }

        /// <summary>K-43/4: the attempt cannot be replayed; the reserved life and the offer coins come back.</summary>
        private void VoidAttempt(InLevelRecord rec, string why)
        {
            if (rec.LifeReserved) Save.Lives = Math.Min(Economy.LivesMax, Save.Lives + 1);
            Save.Coins += rec.OfferSpendCoins;
            Save.InLevel = null;
            BootNote = "attempt voided: " + why;
            log("Minik Usta: K-43 attempt of level " + rec.LevelId + " voided (" + why + ")");
            Write();
        }

        private void Write() => SaveCodec.Write(store, Save);

        // --- frame, screen, input -------------------------------------------------------------------------------------------

        public void SetScreen(double w, double h)
        {
            Player.SetScreen(w, h);
            canvasW = Player.Viewport.W;
            canvasH = Player.Viewport.H;
            if (Flow.Current == AppScreen.Home) ShowHome();
            if (Flow.SettingsOpen) ShowSettings();
        }

        public void Tick(float dtMs)
        {
            Audio?.Tick(dtMs);
            Player.Tick(dtMs);
        }

        /// <summary>
        /// A UI button was PRESSED (uGUI pointer down; JUICE #69): the press sound at once — the tap itself fires on release. Also the
        /// first user input of a WebGL page, which unlocks the audio output.
        /// </summary>
        public void UiPressed()
        {
            Audio?.Unlock();
            Player.Sound(Sounds.Button);
        }

        public void PointerDown(int pointer, double x, double y)
        {
            Audio?.Unlock();
            if (Flow.Current == AppScreen.Level && !Flow.SettingsOpen) Player.PointerDown(pointer, x, y);
        }

        public void PointerMove(int pointer, double x, double y)
        {
            if (Flow.Current == AppScreen.Level) Player.PointerMove(pointer, x, y);
        }

        public void PointerUp(int pointer, bool canceled = false)
        {
            if (Flow.Current == AppScreen.Level) Player.PointerUp(pointer, canceled);
        }

        /// <summary>Development builds only (principle 8): a new attempt of level `n` from the level screen (saved like any start).</summary>
        public void DebugStartLevel(int n)
        {
            if (Flow.Current != AppScreen.Level) return;
            var rec = Save.InLevel;
            if (rec != null && rec.LifeReserved) Save.Lives = Math.Min(Economy.LivesMax, Save.Lives + 1);
            Save.InLevel = null;
            StartLevel(n);
        }

        // --- home -----------------------------------------------------------------------------------------------------------

        /// <summary>The level the home button plays: the first not won; once all are won the 1–10 loop after the last one played.</summary>
        public int NextLevel()
        {
            for (int n = LevelCatalog.First; n <= LevelCatalog.Last; n++)
                if (!Save.Won(n)) return n;
            return LevelCatalog.Next(Math.Max(LevelCatalog.First - 1, Save.LastLevel));
        }

        public HomeModel HomeModelOf() => new HomeModel
        {
            Lives = Save.Lives,
            LivesMax = Economy.LivesMax,
            Coins = Save.Coins,
            NextLevel = NextLevel(),
            Won = Math.Min(LevelCatalog.Last, Save.WonCount),
            Levels = LevelCatalog.Last,
            AllWon = Save.WonCount >= LevelCatalog.Last,
            Lang = I18n.Lang,
        };

        private void ShowHome()
        {
            view.WorldVisible = false;
            Player.ClosePanel();
            ui.SetLayerVisible(UiLayer.Hud, false);
            Home = HomeView.Build(HomeModelOf(), canvasW, canvasH, T);
            ui.Show(UiLayer.Home, Home, OnUiTap);
            ui.SetLayerVisible(UiLayer.Home, true);
        }

        private void ShowBoard()
        {
            ui.SetLayerVisible(UiLayer.Home, false);
            ui.SetLayerVisible(UiLayer.Hud, true);
            view.WorldVisible = true;
        }

        /// <summary>A tap on a home / settings button (the surface calls it; tests call it with the node).</summary>
        public void OnUiTap(UiNode n)
        {
            if (!n.Enabled || n.Action == null) return;
            Taps++;
            switch (n.Action)
            {
                case HomeView.PlayAction:
                    PlayFromHome();
                    return;
                case HomeView.SettingsAction:
                    Flow.OpenSettings();
                    ShowSettings();
                    return;
            }
            if (n.Action.StartsWith(SettingsView.Prefix, StringComparison.Ordinal)) SettingsTap(n.Action);
        }

        private void PlayFromHome()
        {
            int n = NextLevel();
            if (ScreenFlow.SkipsLevelStart(n))
            {
                if (!StartLevel(n)) return;
                Flow.Fire(FlowEvent.PlayDirect);
                return;
            }
            Flow.Fire(FlowEvent.PlayWindow);
            OpenLevelStart(n);
        }

        // --- level start ------------------------------------------------------------------------------------------------------

        private void OpenLevelStart(int n)
        {
            pendingStartLevel = n;
            var lvl = catalog.TryLoad(n, out _);
            var start = lvl != null ? MinikUsta.Core.State.CreateInitialState(lvl) : null;
            var d = new PanelDetail
            {
                LevelName = lvl != null ? (I18n.Lang == Localizer.En ? lvl.Data.Name.En : lvl.Data.Name.Tr) : "",
                DifficultyText = lvl != null && (lvl.Difficulty == "hard" || lvl.Difficulty == "superhard") ? TrText.Upper(T.Get("difficulty." + lvl.Difficulty)) : null,
                Blocks = start != null ? Goals.BlocksLeft(start) : -1,
                MovesText = lvl != null ? T.Format("levelStart.moves", lvl.Data.Moves) : null,
                Thumb = start != null ? PlanThumb.Whole(start) : null,
                SlotsTitle = T.Get("levelStart.boosters"),
                LockedSlots = PreLevelSlots(n),
                CloseAction = Game.PanelAction.Close,
            };
            Player.OpenPanel(new PanelModel(PanelKind.LevelStart, T.Format(Strings.Keys.LevelLabel, n), "", new[]
            {
                new PanelButton(T.Get("levelStart.play"), Game.PanelAction.Play, ButtonKind.Primary),
            }, false, d));
        }

        /// <summary>The three pre-level slots (Termos, Mala Başlangıcı, Açık Kepenk; UX §4): locked below their unlock level ("Bölüm N").</summary>
        private string[] PreLevelSlots(int level)
        {
            var o = new List<string>();
            foreach (var id in new[] { "thermos", "trowelStart", "openShutter" })
            {
                int u = Economy.UnlockLevel(id);
                if (level < u) o.Add(T.Format(Strings.Keys.LevelLabel, u));
            }
            return o.ToArray();
        }

        /// <summary>Starts a new attempt of level `n`: reserves a life, grants unlocked free trials, saves the in-level record.</summary>
        public bool StartLevel(int n)
        {
            var lvl = catalog.TryLoad(n, out string? error);
            if (lvl == null)
            {
                log("Minik Usta: " + error);
                return false;
            }
            foreach (var id in HudState.SlotOrder)
            {
                if (n < Economy.UnlockLevel(id) || Save.FreeTrialsGranted.Contains(id)) continue;
                Save.FreeTrialsGranted.Add(id);
                Save.Boosters[id] = Save.BoosterCount(id) + (Economy.Boosters.TryGetValue(id, out var b) ? b.FreeTrials : 0);
            }
            bool reserve = Save.Lives > 0; // V1: no regeneration yet (Faz 5) — at 0 lives the level still starts, nothing is reserved
            if (reserve) Save.Lives -= 1;
            Save.Level(n).Attempts += 1;
            Save.LastLevel = n;
            var session = GameSession.Start(lvl);
            Save.InLevel = new InLevelRecord
            {
                LevelId = n,
                LevelHash = GameSession.LevelHash(lvl.Data),
                RulesVersion = GameSession.RulesVersion,
                LifeReserved = reserve,
            };
            Save.InLevel.Actions.AddRange(session.Log);
            Write();
            Player.StartSession(session, n);
            ShowBoard();
            return true;
        }

        // --- ILevelHost -------------------------------------------------------------------------------------------------------

        public void Committed(LevelPlayer player, SessionAction action, ActionResult result)
        {
            var session = player.Session;
            var rec = Save.InLevel;
            if (session == null || rec == null || result.Status != MoveStatuses.Applied) return;
            rec.Actions.Clear();
            rec.Actions.AddRange(session.Log);
            rec.OffersUsed = session.OffersUsed;
            if (action is AddMovesMove)
            {
                rec.OfferSpendCoins += pendingOfferPaid;
                pendingOfferPaid = 0;
            }
            rec.OutcomeWindow = session.Outcome == BoosterOutcomes.OutOfMoves ? "outOfMoves" : "none";
            if (session.Outcome == BoosterOutcomes.Won) RecordWin(player, session);
            else if (session.Outcome == BoosterOutcomes.Lost) RecordLoss(rec);
            Write();
        }

        private void RecordWin(LevelPlayer player, GameSession session)
        {
            int n = player.LevelNumber;
            bool first = !Save.Won(n);
            var r = Rewards.Win(session.Lvl.Difficulty, session.MovesLeft, Combo.TrowelsOf(session.State), first, Economy);
            int coins = first ? r.TotalCoins : r.BaseCoins; // UX §6: a loop level pays the base coins only
            Save.Coins += coins;
            Save.Stars += r.Stars;
            var lr = Save.Level(n);
            lr.Won = true;
            Save.HighestLevel = Math.Max(Save.HighestLevel, n);
            Save.WinStreak += 1;
            if (Save.InLevel != null && Save.InLevel.LifeReserved) Save.Lives = Math.Min(Economy.LivesMax, Save.Lives + 1);
            Save.InLevel = null;
            lastWin = r;
            lastWinFirst = first;
        }

        private void RecordLoss(InLevelRecord rec)
        {
            lastLossStreak = Save.WinStreak > 0;
            Save.WinStreak = 0;
            Save.InLevel = null;
            // the reserved life is the life lost (META §2); nothing to give back
            _ = rec;
        }

        public void Ended(LevelPlayer player, string outcome)
        {
            if (outcome == BoosterOutcomes.Won)
            {
                Flow.Fire(FlowEvent.Won);
                OpenWin(player);
            }
            else if (outcome == BoosterOutcomes.OutOfMoves)
            {
                Flow.Fire(FlowEvent.OutOfMoves);
                OpenOffer();
            }
            else if (outcome == BoosterOutcomes.Lost)
            {
                Flow.Fire(FlowEvent.Lost);
                OpenLoseFinal();
            }
        }

        public void PauseRequested(LevelPlayer player)
        {
            Flow.Fire(FlowEvent.Pause);
            OpenPause();
        }

        public void PanelAction(LevelPlayer player, PanelAction action)
        {
            Taps++;
            switch (Flow.Current)
            {
                case AppScreen.LevelStart:
                    if (action == Game.PanelAction.Play)
                    {
                        if (StartLevel(pendingStartLevel)) Flow.Fire(FlowEvent.StartLevel);
                    }
                    else if (action == Game.PanelAction.Close)
                    {
                        Flow.Fire(FlowEvent.Close);
                        ShowHome();
                    }
                    return;
                case AppScreen.Paused:
                    if (action == Game.PanelAction.Resume)
                    {
                        Player.ClosePanel();
                        Flow.Fire(FlowEvent.Continue);
                    }
                    else if (action == Game.PanelAction.Settings)
                    {
                        Flow.OpenSettings();
                        ShowSettings();
                    }
                    else if (action == Game.PanelAction.Restart || action == Game.PanelAction.Exit)
                    {
                        exitIntent = action == Game.PanelAction.Restart ? ExitIntent.Restart : ExitIntent.Home;
                        Flow.Fire(FlowEvent.Exit);
                        OpenExitConfirm();
                    }
                    return;
                case AppScreen.ExitConfirm:
                    if (action == Game.PanelAction.Stay)
                    {
                        Flow.Fire(FlowEvent.Stay);
                        OpenPause();
                    }
                    else if (action == Game.PanelAction.Leave) Leave();
                    return;
                case AppScreen.LoseOffer:
                    if (action == Game.PanelAction.BuyMoves) AcceptOffer();
                    else if (action == Game.PanelAction.Decline || action == Game.PanelAction.Close) Decline();
                    return;
                case AppScreen.LoseFinal:
                    if (action == Game.PanelAction.Retry) Retry();
                    else if (action == Game.PanelAction.Home)
                    {
                        Flow.Fire(FlowEvent.Home);
                        ShowHome();
                    }
                    return;
                case AppScreen.Win:
                    if (action == Game.PanelAction.Continue || action == Game.PanelAction.Next)
                    {
                        Flow.Fire(FlowEvent.Continue);
                        ShowHome();
                    }
                    return;
            }
        }

        // --- pause and exit (K-43) ----------------------------------------------------------------------------------------

        private void OpenPause()
        {
            Player.OpenPanel(new PanelModel(PanelKind.Pause, T.Get(Strings.Keys.PauseTitle), "", new[]
            {
                new PanelButton(T.Get(Strings.Keys.Continue), Game.PanelAction.Resume, ButtonKind.Primary),
                new PanelButton(T.Get(Strings.Keys.Restart), Game.PanelAction.Restart, ButtonKind.Secondary),
                new PanelButton(T.Get("common.home"), Game.PanelAction.Exit, ButtonKind.Secondary),
            }, false, new PanelDetail { SettingsTool = true }));
        }

        private void OpenExitConfirm()
        {
            var session = Player.Session!;
            var lines = new List<string>();
            if (session.MovesSpent == 0) lines.Add(T.Get("exit.free"));
            else
            {
                lines.Add(T.Get("exit.cost"));
                if (Save.WinStreak > 0) lines.Add(T.Get("exit.streak"));
            }
            Player.OpenPanel(new PanelModel(PanelKind.ExitConfirm, T.Get("exit.title"), "", new[]
            {
                new PanelButton(T.Get("exit.stay"), Game.PanelAction.Stay, ButtonKind.Primary),
                new PanelButton(T.Get("exit.leave"), Game.PanelAction.Leave, ButtonKind.Danger),
            }, false, new PanelDetail { Lines = lines.ToArray(), Character = "chr_tuna_think" }));
        }

        private void Leave()
        {
            var session = Player.Session!;
            var res = session.Exit();
            var rec = Save.InLevel;
            bool free = res.Kind == ExitKinds.Free;
            if (free)
            {
                if (rec != null && rec.LifeReserved) Save.Lives = Math.Min(Economy.LivesMax, Save.Lives + 1);
                Save.InLevel = null;
            }
            else if (rec != null) RecordLoss(rec);
            else Save.InLevel = null;
            Write();
            int level = Player.LevelNumber;
            if (exitIntent == ExitIntent.Restart)
            {
                Flow.Fire(FlowEvent.Restart);
                StartLevel(level);
                return;
            }
            if (free)
            {
                Flow.Fire(FlowEvent.LeaveFree);
                ShowHome();
            }
            else
            {
                Flow.Fire(FlowEvent.LeaveLoss);
                OpenLoseFinal();
            }
        }

        // --- out of moves (K-29) ------------------------------------------------------------------------------------------

        public OfferModel? CurrentOffer()
        {
            var o = Player.Session?.NextOffer();
            if (!o.HasValue) return null;
            return Offers.Model(o.Value.N, o.Value.AdAllowed, !Save.FirstOfferGiftUsed, Save.Coins, AdAvailability.None, Economy);
        }

        private void OpenOffer()
        {
            var offer = CurrentOffer();
            if (offer == null)
            {
                OpenLoseFinal();
                return;
            }
            var buttons = new List<PanelButton>
            {
                new PanelButton(T.Get("lose.decline"), Game.PanelAction.Decline, ButtonKind.Secondary),
            };
            if (offer.Ad != null)
            {
                string line2 = offer.Ad.Line == AdLineKind.Today ? T.Format(offer.Ad.LineKey, ("n", offer.Ad.TodayN), ("max", offer.Ad.TodayMax)) : T.Get(offer.Ad.LineKey);
                buttons.Add(new PanelButton(T.Format("lose.ad", offer.Ad.Moves), Game.PanelAction.AdMoves, offer.Ad.Enabled ? ButtonKind.Secondary : ButtonKind.Disabled, line2));
            }
            if (offer.Coin == CoinOptionKind.Gift)
                buttons.Add(new PanelButton(T.Format("lose.offer.gift", offer.Moves), Game.PanelAction.BuyMoves, ButtonKind.Offer));
            else if (offer.Coin == CoinOptionKind.Buy)
                buttons.Add(new PanelButton(T.Format("lose.offer.moves", offer.Moves), Game.PanelAction.BuyMoves, ButtonKind.Offer,
                    Price.RealMoneyText(offer.Price, I18n.Lang, Economy.ReferencePack), offer.Price));
            else
                buttons.Add(new PanelButton(T.Get("common.notEnoughCoins"), Game.PanelAction.BuyMoves, ButtonKind.Disabled, T.Format("lose.offer.moves", offer.Moves), offer.Price));
            var session = Player.Session!;
            var d = new PanelDetail
            {
                Character = "chr_tuna_think",
                Thumb = PlanThumb.Visible(session.State),
                Counter = T.Format(offer.CounterKey, ("n", offer.N), ("max", offer.Max)),
                CloseAction = Game.PanelAction.Decline,
            };
            Player.OpenPanel(new PanelModel(PanelKind.Lose, T.Get(Strings.Keys.LoseTitle), T.Format(Strings.Keys.LoseBlocksLeft, Player.BlocksLeft), buttons.ToArray(), false, d));
        }

        private void AcceptOffer()
        {
            var offer = CurrentOffer();
            if (offer == null || !offer.CoinEnabled) return;
            if (offer.Coin == CoinOptionKind.Gift) Save.FirstOfferGiftUsed = true;
            Save.Coins -= offer.Paid;
            pendingOfferPaid = offer.Paid;
            Player.ClosePanel();
            Flow.Fire(FlowEvent.Accept);
            Player.Commit(new AddMovesMove(offer.Moves, AddMovesSource.OfferCoins));
        }

        private void Decline()
        {
            var session = Player.Session!;
            session.DeclineOffer();
            if (Save.InLevel != null) RecordLoss(Save.InLevel);
            Write();
            Flow.Fire(FlowEvent.Decline);
            OpenLoseFinal();
        }

        private void OpenLoseFinal()
        {
            var lines = new List<string>();
            if (lastLossStreak) lines.Add(T.Get("lose.streak"));
            Player.OpenPanel(new PanelModel(PanelKind.LoseFinal, T.Get("lose.life"), "", new[]
            {
                new PanelButton(T.Get(Strings.Keys.LoseRetry), Game.PanelAction.Retry, ButtonKind.Primary),
                new PanelButton(T.Get("common.home"), Game.PanelAction.Home, ButtonKind.Secondary),
            }, false, new PanelDetail { Lines = lines.ToArray(), Character = "chr_tuna_think" }));
        }

        private void Retry()
        {
            int n = Player.LevelNumber;
            if (ScreenFlow.SkipsLevelStart(n))
            {
                Flow.Fire(FlowEvent.RetryDirect);
                StartLevel(n);
            }
            else
            {
                Flow.Fire(FlowEvent.RetryWindow);
                OpenLevelStart(n);
            }
        }

        // --- win (K-28) ---------------------------------------------------------------------------------------------------

        private void OpenWin(LevelPlayer player)
        {
            var r = lastWin;
            var session = player.Session!;
            var d = new PanelDetail
            {
                Character = "chr_tuna_cheer",
                Thumb = PlanThumb.Whole(session.State),
                LevelName = player.LevelName(session.Lvl),
                CardCaption = T.Get("build.done"),
                Coins = r == null ? 0 : lastWinFirst ? r.TotalCoins : r.BaseCoins,
                Trowels = r?.Trowels ?? 0,
                TrowelCoins = r?.TrowelCoins ?? 0,
                BonusLine = r != null && lastWinFirst && r.BonusMoves > 0 ? T.Format(Strings.Keys.WinBonus, r.BonusMoves) : null,
                TrowelLine = r != null && lastWinFirst && r.Trowels > 0 ? T.Format("win.trowel", r.Trowels) : null,
            };
            player.OpenPanel(new PanelModel(PanelKind.Win, T.Get(Strings.Keys.WinTitle), T.Get(Strings.Keys.WinClear), new[]
            {
                new PanelButton(T.Get(Strings.Keys.Continue), Game.PanelAction.Continue, ButtonKind.Primary),
            }, lastWinFirst, d));
        }

        // --- settings --------------------------------------------------------------------------------------------------------

        private void ShowSettings()
        {
            var s = Save.Settings;
            Settings = SettingsView.Build(new SettingsModel { Sound = s.Sound, Haptics = s.Haptics, ReduceMotion = s.ReduceMotion, Lang = I18n.Lang }, canvasW, canvasH, T,
                WindowViews.PanelBottom(canvasH, Player.LayoutSettings));
            ui.Show(UiLayer.Overlay, Settings, OnUiTap);
        }

        private void SettingsTap(string action)
        {
            var s = Save.Settings;
            switch (action)
            {
                case SettingsView.Sound:
                    s.Sound = !s.Sound;
                    if (Audio != null) Audio.SoundOn = s.Sound;
                    break;
                case SettingsView.Haptics:
                    s.Haptics = !s.Haptics;
                    Player.Haptics.Enabled = s.Haptics;
                    break;
                case SettingsView.Motion:
                    s.ReduceMotion = !s.ReduceMotion;
                    Player.Reduced = s.ReduceMotion;
                    break;
                case SettingsView.LangTr:
                case SettingsView.LangEn:
                    {
                        string lang = action == SettingsView.LangTr ? Localizer.Tr : Localizer.En;
                        if (lang == I18n.Lang) return;
                        s.Lang = lang;
                        I18n.SetLang(lang);
                        Relabel();
                        break;
                    }
                case SettingsView.Close:
                    Write();
                    Flow.CloseSettings();
                    Settings = null;
                    ui.Show(UiLayer.Overlay, null, null);
                    return;
                default:
                    return;
            }
            Write();
            ShowSettings();
        }

        /// <summary>Language switch: every visible text again (home tree, HUD labels, the open pause window).</summary>
        private void Relabel()
        {
            if (Flow.Current == AppScreen.Home) ShowHome();
            if (Player.Session != null) Player.RefreshTexts();
            if (Flow.Current == AppScreen.Paused) OpenPause();
        }
    }
}
