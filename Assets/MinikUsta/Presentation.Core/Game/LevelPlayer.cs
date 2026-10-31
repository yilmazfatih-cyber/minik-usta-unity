#nullable enable
// The playable level screen without an engine (Faz 3 Q1; D-093, D-096): web LevelScene.ts + DragController host +
// EventPlayer, reduced to what the placeholder board needs. It owns the `GameSession`, the animation clock
// (`TweenClock`), the step queue (`AnimationQueue`), the pointer mapper (`DragMapper`) and the stuck pulse; the engine
// feeds it design-px pointer events and frame times, and implements `ILevelView`.
//
// Flow of a drag (TECH §6.1, §6.3; UX §5.3): press → K-09 gate + one BFS (`DragMapper.Down`) → lift after 8 px / 100 ms
// (JUICE #1) → K-08 sticky follow every pointer event and frame, the K-18 shadow on every node change → release → the
// core applies the move AT ONCE (`GameSession.Apply`; logic never waits for animation) → `AnimationPlanner.Plan` →
// `AnimationQueue.Schedule` → the step runner plays the JUICE rows → `End` re-reads the state, the HUD and the outcome.
//
// Input during animations (JUICE §0 rule 3, R-12, D-021): a press on the board first finishes the running board motions
// and fast-forwards the pending unlocked steps (`FastForward`); a touch during a locked window (segment slide, truck,
// level end) plays it 3× faster (`SpeedUp` → `Rate`) and picks nothing.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Animation;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Input;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Text;
using MinikUsta.Presentation.Tweening;
using static MinikUsta.Core.State;

namespace MinikUsta.Presentation.Game
{
    public sealed class LevelPlayer
    {
        /// <summary>Tween tag of board motions (a grab completes them, R-12).</summary>
        public const int BoardTag = 1;
        /// <summary>Tween tag of HUD / window motions (they keep their pace).</summary>
        public const int HudTag = 2;

        private readonly LevelCatalog catalog;
        private readonly StepRunner runner;
        private readonly HashSet<IFlyer> flyers = new HashSet<IFlyer>();
        private readonly List<IFlyer> flyerScratch = new List<IFlyer>();
        private double screenW = 1080;
        private double screenH = 1920;
        private int lastShadowPiece = -1;
        private string? lastShadowKey;

        public LevelPlayer(ILevelView view, LevelCatalog catalog, Strings strings, FeelSettings? feel = null, LayoutSettings? layout = null)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Text = strings ?? Strings.Empty;
            Feel = feel ?? new FeelSettings();
            LayoutSettings = layout ?? new LayoutSettings();
            runner = new StepRunner(this);
            Queue = new AnimationQueue((step, time, instant) => runner.Run(step, time, instant), Feel.LockSpeedUp);
            Mapper = new DragMapper(Feel);
            Pieces = new PieceViews(view);
            Viewport = ScreenFit.Viewport(screenW, screenH, LayoutSettings);
        }

        public ILevelView View { get; }
        public Strings Text { get; }
        public FeelSettings Feel { get; }
        public LayoutSettings LayoutSettings { get; }
        public TweenClock Clock { get; } = new TweenClock();
        public AnimationQueue Queue { get; }
        public DragMapper Mapper { get; }
        public StuckPulse Stuck { get; } = new StuckPulse();
        public PieceViews Pieces { get; }
        public DesignViewport Viewport { get; private set; }
        public ScreenLayout? Layout { get; private set; }
        public GameSession? Session { get; private set; }
        public CompiledLevel? Level => Session?.Lvl;
        /// <summary>The level number being played (1 … 10).</summary>
        public int LevelNumber { get; private set; }
        public PanelKind Panel { get; private set; } = PanelKind.None;
        /// <summary>Why the last `Load` failed (invalid or missing level file); null after a successful load.</summary>
        public string? LoadError { get; private set; }
        /// <summary>"Animasyonları azalt" (JUICE §0 rule 8). Off in Faz 3 Q1 (no settings screen yet).</summary>
        public bool Reduced { get; set; }
        /// <summary>The last `TurnSummary` (after the level start or the last package).</summary>
        public TurnSummary? Summary { get; private set; }
        /// <summary>Blocks left (K-48) of the last summary.</summary>
        public int BlocksLeft => Summary?.BlocksLeft ?? 0;
        /// <summary>Steps the runner has played (tests, debug overlay).</summary>
        public int StepsRun { get; internal set; }
        /// <summary>Packages committed since the level started (tests, debug).</summary>
        public int Packages { get; private set; }
        /// <summary>`PieceViews.LastSnapPx` at every plan `End` since the level started (tests, debug overlay).</summary>
        public List<float> EndSnaps { get; } = new List<float>();
        public float MaxEndSnapPx { get; private set; }
        public int MaxEndSnapPiece { get; private set; } = -1;
        /// <summary>The last applied action's result (tests, debug).</summary>
        public ActionResult? LastResult { get; private set; }

        // --- screen and level ------------------------------------------------------------------------------------------

        /// <summary>The screen size changed (px): new viewport; the board is rebuilt for the new design height (EXPAND).</summary>
        public void SetScreen(double width, double height)
        {
            if (!(width > 0) || !(height > 0)) return;
            screenW = width;
            screenH = height;
            Viewport = ScreenFit.Viewport(width, height, LayoutSettings);
            if (Session == null) return;
            Mapper.Abort();
            Queue.Flush(Clock.Now);
            Clock.Complete();
            Layout = ScreenLayout.Create(LayoutSettings, Viewport.H, Session.Lvl.Geo);
            View.SetViewport(Viewport, Layout);
            Pieces.CellPx = Layout.Grid.CellPx;
            SyncAll();
        }

        /// <summary>Starts a new attempt of level `n` (an invalid / missing file is not played: false + `LoadError`).</summary>
        public bool Load(int n)
        {
            var lvl = catalog.TryLoad(n, out string? error);
            if (lvl == null)
            {
                LoadError = error;
                return false;
            }
            LoadError = null;
            Start(lvl, n);
            return true;
        }

        /// <summary>A new attempt of a compiled level (tests, debug boards).</summary>
        public void Start(CompiledLevel lvl, int number)
        {
            ResetPlayback();
            LevelNumber = number;
            Session = GameSession.Start(lvl);
            Packages = 0;
            EndSnaps.Clear();
            MaxEndSnapPx = 0;
            MaxEndSnapPiece = -1;
            LastResult = null;
            Layout = ScreenLayout.Create(LayoutSettings, Viewport.H, lvl.Geo);
            View.SetViewport(Viewport, Layout);
            Pieces.Clear();
            Pieces.CellPx = Layout.Grid.CellPx;
            Panel = PanelKind.None;
            View.ShowPanel(null, OnPanelAction);
            View.SetLevel(Text.Format(Strings.Keys.LevelLabel, number), lvl.Data.Name.Tr);
            Summary = Session.Summary();
            SyncAll();
            Stuck.Level(lvl.Difficulty, Clock.Now);
        }

        public void Restart()
        {
            if (Session == null) return;
            Start(Session.Lvl, LevelNumber);
        }

        /// <summary>"Sonraki bölüm": the next level (10 → 1).</summary>
        public bool Next() => Load(LevelCatalog.Next(LevelNumber));

        private void ResetPlayback()
        {
            Mapper.Abort();
            Queue.Clear();
            Clock.Kill();
            Clock.TimeScale = 1;
            View.ShowShadow(null);
            lastShadowKey = null;
            lastShadowPiece = -1;
            flyerScratch.Clear();
            flyerScratch.AddRange(flyers);
            foreach (var f in flyerScratch) ReleaseFlyer(f);
        }

        // --- frame -----------------------------------------------------------------------------------------------------

        /// <summary>One frame of `dtMs` real milliseconds: animation clock (× the lock speed-up), due steps, drag glide, stuck pulse.</summary>
        public void Tick(float dtMs)
        {
            if (!(dtMs >= 0)) dtMs = 0;
            float rate = Queue.Rate(Clock.Now);
            Clock.TimeScale = rate;
            Clock.Advance(dtMs);
            Queue.Advance(Clock.Now);
            if (Layout != null)
            {
                var f = Mapper.Update(Clock.Now, Layout);
                if (f != null) DragFrame(f);
            }
            StuckPulseCheck();
        }

        // --- pointer (design px, y down) ---------------------------------------------------------------------------------

        public void PointerDown(int pointer, double x, double y)
        {
            var session = Session;
            var L = Layout;
            if (session == null || L == null) return;
            float now = Clock.Now;
            Stuck.Touched(now);
            if (Panel != PanelKind.None) return;
            if (Mapper.Active) return; // second finger (TECH §4.6)
            bool onBoard = L.OnBoard(x, y);
            if (onBoard)
            {
                // R-12: running board motions jump to their end, pending unlocked steps run now (a locked one may start now)
                Clock.Complete(BoardTag);
                Queue.FastForward(now);
                Clock.Complete(BoardTag);
            }
            Queue.SpeedUp(now); // JUICE §0 rule 3
            var state = session.Outcome == BoosterOutcomes.Playing && !Queue.Locked(now) ? session.State : null;
            var press = Mapper.Down(state, L, session.Hooks.Drag, pointer, x, y, now);
            if (press.Outcome == PressOutcome.PickFailed && (press.Reason == PickFailures.Immovable || press.Reason == PickFailures.Rule))
                PickBlocked(press.PieceId);
        }

        public void PointerMove(int pointer, double x, double y)
        {
            var L = Layout;
            if (L == null) return;
            var f = Mapper.Move(pointer, x, y, Clock.Now, L);
            if (f != null) DragFrame(f);
        }

        public void PointerUp(int pointer, bool canceled = false)
        {
            var session = Session;
            var L = Layout;
            if (session == null || L == null) return;
            int id = Mapper.PieceId;
            var r = Mapper.Up(pointer, canceled);
            if (r.Kind == ReleaseKind.None) return;
            View.ShowShadow(null);
            lastShadowKey = null;
            lastShadowPiece = -1;
            var v = Pieces.Get(r.PieceId >= 0 ? r.PieceId : id);
            switch (r.Kind)
            {
                case ReleaseKind.Tap:
                    if (v != null) Clock.Play(Hop(v, Feel.TapHopCells * (float)L.Grid.CellPx, Feel.TapHopMs));
                    return;
                case ReleaseKind.Aborted:
                    if (v != null) ReturnHome(v, r.PieceId);
                    return;
                case ReleaseKind.Drag:
                    if (v != null)
                    {
                        v.Lifted = false;
                        v.Alpha = 1;
                    }
                    Commit(r.Move!);
                    return;
            }
        }

        /// <summary>Commits an action and schedules its plan (drag releases; tests and the debug solver use it directly).</summary>
        public ActionResult Commit(SessionAction action)
        {
            var session = Session ?? throw new InvalidOperationException("LevelPlayer.Commit: no level");
            var ctx = PlanContext.Capture(session, Reduced);
            var res = session.Apply(action);
            Packages++;
            LastResult = res;
            Summary = res.Summary;
            var plan = AnimationPlanner.Plan(res, ctx, Feel);
            Queue.Schedule(plan, Clock.Now);
            Queue.Advance(Clock.Now); // steps at 0 ms start in this frame (one-frame response, TECH §4.6)
            return res;
        }

        // --- windows ---------------------------------------------------------------------------------------------------

        /// <summary>The pause tool (HUD v3): opens "Mola" with restart / continue.</summary>
        public void PausePressed()
        {
            if (Session == null || Panel != PanelKind.None) return;
            if (Session.Outcome != BoosterOutcomes.Playing) return;
            Mapper.Abort();
            View.ShowShadow(null);
            ShowPanel(new PanelModel(PanelKind.Pause, Text.Get(Strings.Keys.PauseTitle), "", new[]
            {
                new PanelButton(Text.Get(Strings.Keys.Continue), PanelAction.Resume, true),
                new PanelButton(Text.Get(Strings.Keys.Restart), PanelAction.Restart, false),
            }));
        }

        public void OnPanelAction(PanelAction action)
        {
            switch (action)
            {
                case PanelAction.Resume:
                    HidePanel();
                    return;
                case PanelAction.Restart:
                case PanelAction.Retry:
                    HidePanel();
                    Restart();
                    return;
                case PanelAction.Next:
                    HidePanel();
                    if (!Next()) Restart();
                    return;
            }
        }

        private void ShowPanel(PanelModel model)
        {
            Panel = model.Kind;
            View.ShowPanel(model, OnPanelAction);
            var t = View.PanelTarget;
            // JUICE #70 window open: 0,85 → 1,03 → 1 (Back.easeOut)
            var pop = Tweens.Pop(t, Reduced ? 1f : Feel.PopupFrom, Reduced ? 1f : Feel.PopupPeak, 1f, Feel.PopupOpenMs, 0.6f);
            pop.Tag = HudTag;
            Clock.Play(pop);
        }

        private void HidePanel()
        {
            Panel = PanelKind.None;
            View.ShowPanel(null, OnPanelAction);
        }

        // --- called by the step runner ---------------------------------------------------------------------------------

        /// <summary>The plan's `End`: the board shows the state again; the level-end window opens (K-28 win, K-29 out of moves).</summary>
        internal void PlanEnded()
        {
            var session = Session;
            if (session == null) return;
            SyncAll();
            EndSnaps.Add(Pieces.LastSnapPx);
            if (Pieces.LastSnapPx > MaxEndSnapPx)
            {
                MaxEndSnapPx = Pieces.LastSnapPx;
                MaxEndSnapPiece = Pieces.LastSnapPiece;
            }
            string outcome = session.Outcome;
            if (outcome == BoosterOutcomes.Won)
            {
                int moves = session.MovesLeft;
                ShowPanel(new PanelModel(PanelKind.Win, Text.Get(Strings.Keys.WinTitle), Text.Format(Strings.Keys.WinBonus, moves), new[]
                {
                    new PanelButton(Text.Get(Strings.Keys.Continue), PanelAction.Next, true),
                }));
            }
            else if (outcome == BoosterOutcomes.OutOfMoves || outcome == BoosterOutcomes.Lost)
            {
                // Faz 3 Q1: no +5 offer (K-29 window) yet — only "Tekrar dene"
                ShowPanel(new PanelModel(PanelKind.Lose, Text.Get(Strings.Keys.LoseTitle), Text.Format(Strings.Keys.LoseBlocksLeft, BlocksLeft), new[]
                {
                    new PanelButton(Text.Get(Strings.Keys.LoseRetry), PanelAction.Retry, true),
                }));
            }
        }

        /// <summary>Board, blocks and HUD from the state (End, Resync, screen change).</summary>
        internal void SyncAll()
        {
            var session = Session;
            var L = Layout;
            if (session == null || L == null) return;
            Clock.Complete(BoardTag);
            RebuildBoard();
            Summary = session.Summary();
            Pieces.Sync(session.State, L, Summary.Holdable, Mapper.Active ? Mapper.PieceId : -1);
            // #56 counts the moves down on a won level: the plate keeps that count (web hudSync)
            if (session.Outcome != BoosterOutcomes.Won) ShowMoves(session.MovesLeft);
            ShowBlocks(Summary.BlocksLeft);
        }

        internal void RebuildBoard()
        {
            var session = Session;
            var L = Layout;
            if (session == null || L == null) return;
            View.BuildBoard(BoardScene.Build(session.State, L));
        }

        internal void ShowMoves(int n, bool? danger = null)
        {
            bool d = danger ?? (n > 0 && AnimationPlanner.LowMovesWarning(n, BlocksLeft));
            View.SetMoves(n, Text.Get(Strings.Keys.Moves), d);
        }

        internal void ShowBlocks(int n)
        {
            var session = Session;
            string segs = "";
            if (session != null)
            {
                int total = session.Lvl.Segments.Length;
                int done = Math.Min(total, Hdr(session.State, H.ActiveSeg));
                if (session.Outcome == BoosterOutcomes.Won) done = total;
                segs = Text.Format(Strings.Keys.Count, ("n", done), ("max", total));
            }
            View.SetBlocks(n, Text.Get(Strings.Keys.Blocks), segs);
        }

        internal IFlyer AcquireFlyer(int colorIndex, double sizePx)
        {
            var f = View.AcquireFlyer(colorIndex, sizePx);
            flyers.Add(f);
            return f;
        }

        internal void ReleaseFlyer(IFlyer f)
        {
            if (!flyers.Remove(f)) return;
            f.Visible = false;
            View.ReleaseFlyer(f);
        }

        // --- drag presentation -----------------------------------------------------------------------------------------

        private void DragFrame(DragFrame f)
        {
            var session = Session;
            var L = Layout;
            var drag = Mapper.Session;
            if (session == null || L == null || drag == null) return;
            var v = Pieces.Get(drag.PieceId);
            if (v == null) return;
            var shape = drag.Shape;
            if (f.Lifted)
            {
                // JUICE #1: 1,00 → 1,08 (80 ms Back.easeOut), 6 px hop, lifted silhouette, above every block
                v.Raised = true;
                v.Lifted = true;
                Clock.Play(Tweens.Scale(v, Reduced ? Feel.ReducedScaleMax : Feel.LiftScale, Feel.LiftMs, Feel.LiftEase));
                if (!Reduced) Clock.Play(Hop(v, Feel.LiftHopPx, Feel.LiftMs));
            }
            v.Position = BoardPose.Center(L.Grid, f.Ax, f.Ay, shape);
            if (f.NodeChanged && f.Drop != null) NodeChanged(session, drag, v, f);
        }

        private void NodeChanged(GameSession session, DragSession drag, IPieceVisual v, DragFrame f)
        {
            var drop = f.Drop!;
            v.Alpha = ShadowLook.CancelPreview(drop) ? Feel.CancelPreviewAlpha : 1f;
            if (!ShadowLook.ShowsShadow(drop))
            {
                if (lastShadowKey != null) View.ShowShadow(null);
                lastShadowKey = null;
                lastShadowPiece = -1;
                return;
            }
            var s = session.State;
            var fall = Gravity.ComputeFall(s, drag.PieceId, f.Node, new FallOptions(null, session.Hooks.Fall));
            var look = ShadowLook.Of(fall, session.Lvl.Difficulty, s, drag.PieceId);
            var g = Layout!.Grid;
            string key = fall.Landing.Ix + "," + fall.Landing.Iy + "|" + look.Key;
            if (key == lastShadowKey && lastShadowPiece == drag.PieceId) return;
            lastShadowKey = key;
            lastShadowPiece = drag.PieceId;
            View.ShowShadow(new ShadowModel(drag.PieceId, drag.Shape, PieceColor(s, drag.PieceId), fall.Landing.Ix, fall.Landing.Iy, look,
                BoardPose.Center(g, fall.Landing.Ix, fall.Landing.Iy, drag.Shape), g.CellPx));
        }

        /// <summary>JUICE #2 pick blocked (K-09): 2 px × 3 shake and a white flash.</summary>
        private void PickBlocked(int id)
        {
            var v = Pieces.Get(id);
            if (v == null) return;
            if (!Reduced)
            {
                var shake = Tweens.Shake(v, new Vec2(Feel.BlockedShakePx, 0), Feel.BlockedShakeCycles, Feel.BlockedShakeMs, Feel.BlockedShakeEase);
                shake.Tag = BoardTag;
                Clock.Play(shake);
            }
            var flash = StepRunner.FlashPulse(v, Feel.PlaceFlashPeak, Feel.BlockedFlashMs);
            flash.Tag = BoardTag;
            Clock.Play(flash);
        }

        private void ReturnHome(IPieceVisual v, int id)
        {
            var session = Session;
            var L = Layout;
            v.Lifted = false;
            v.Alpha = 1;
            if (session == null || L == null || !BoardPose.RestAnchor(session.State, id, out int ax, out int ay)) return;
            var home = BoardPose.Center(L.Grid, ax, ay, BoardPose.ShapeOf(session.State, id));
            var move = Tweens.Move(v, home, Feel.CancelMs, Feel.CancelEase, Feel.CancelArcCells * (float)L.Grid.CellPx);
            move.Tag = BoardTag;
            move.OnComplete(() => v.Raised = false);
            Clock.Play(move);
            var scale = Tweens.Scale(v, 1f, Feel.CancelMs);
            scale.Tag = BoardTag;
            Clock.Play(scale);
        }

        /// <summary>An upward hop of `px` and back (sin π·u): JUICE #1 lift hop, UX §5.3 tap hop.</summary>
        internal static Tween Hop(IPieceVisual v, float px, float ms) =>
            new Tween(ms, (u, k) => v.Hop = u >= 1 ? 0f : px * (float)Math.Sin(Math.PI * u), null, EaseType.Linear);

        // --- K-34 hook 5 stuck pulse -----------------------------------------------------------------------------------

        private void StuckPulseCheck()
        {
            var session = Session;
            if (session == null || session.Outcome != BoosterOutcomes.Playing) return;
            bool quiet = Mapper.Active || Queue.Busy || Panel != PanelKind.None;
            if (!Stuck.Due(Clock.Now, quiet)) return;
            var needed = Summary?.NeededNow ?? session.Summary().NeededNow;
            foreach (int id in needed)
            {
                var v = Pieces.Get(id);
                if (v == null) continue;
                var t = Tweens.Pulse(v, Feel.HintPulsePeak, Feel.HintPulseMs, StuckPulse.Pulses);
                t.Tag = BoardTag;
                Clock.Play(t);
            }
        }
    }
}
