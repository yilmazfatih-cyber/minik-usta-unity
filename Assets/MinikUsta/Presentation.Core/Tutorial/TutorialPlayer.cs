#nullable enable
// The light tutorial of a level screen (paket C1; K-53): glue between `LevelPlayer` and the pieces of this folder. `LevelPlayer` reports what
// happens on the board (a touch, a lifted block, a drag signal, a move that finished playing, a window opening, a new layout) through the
// small hook methods below; this class runs the `TutorialController` (which step is active), the `TutorialPresence` (is it shown now) and
// the `TutorialView` (the glow, the glove, the bubble on `UiLayer.Tutorial`). Nothing here blocks input: the layer has no tap and the
// player does not know the tutorial (K-53/2).
//
// Data driven (A2 changes the levels): everything comes from the level's `tutorial[]`; there is no level-specific code.
//
// Unity-less: `LevelPlayer.Tutorial` is null in tests that do not exercise it and in a build without the tutorial.
using System;
using System.Collections.Generic;
using MinikUsta.Core;
using MinikUsta.Core.Level;
using MinikUsta.Presentation.Game;
using MinikUsta.Presentation.Layout;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Tutorial
{
    public sealed class TutorialPlayer : ITutorialHost
    {
        private readonly LevelPlayer player;
        private readonly IUiSurface surface;
        private TutorialController? controller;
        private TutorialPresence? presence;
        private TutorialContent? content;
        private int seenVersion = -1;
        private float now;
        private bool glovePlays;
        private int windows;
        private float heldMs;
        private GameState? replayState;
        private readonly List<PxRect> litScratch = new List<PxRect>();

        public TutorialPlayer(LevelPlayer player, IUiSurface ui, TutorialSettings? settings = null)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            surface = ui ?? throw new ArgumentNullException(nameof(ui));
            Settings = settings ?? new TutorialSettings();
            View = new TutorialView(ui, Settings);
        }

        public TutorialSettings Settings { get; }
        public TutorialView View { get; }

        /// <summary>The controller of the level on screen (tests, debug); null when the level has no steps or was won before (K-53/6).</summary>
        public TutorialController? Controller => controller;

        public TutorialPresence? Presence => presence;

        /// <summary>The glove of the active step may play now (DL-2R-20).</summary>
        public bool GlovePlays => glovePlays;

        /// <summary>GDD §14.1/2: a `tut.ctx.&lt;topic&gt;` step started (the host stores `seenContextTips`).</summary>
        public Action<string>? ContextTipSeen { get; set; }

        /// <summary>Analytics `tutorial_step`: step number, shows (≥ 1), ms from the first show to done.</summary>
        public Action<int, int, int>? StepReported { get; set; }

        /// <summary>Tutorial clock (ms): the sum of the frame times.</summary>
        public float Now => now;

        public GameState? CurrentState => replayState ?? player.Session?.State;

        void ITutorialHost.MarkContextTip(string topic) => ContextTipSeen?.Invoke(topic);

        void ITutorialHost.StepEnded(int step, float t)
        {
            var p = presence;
            if (p == null) return;
            p.Handle(PresenceInput.StepDone, t);
            p.Report(t, out int shows, out int ms);
            StepReported?.Invoke(step, shows, ms);
        }

        void ITutorialHost.Progressed(int step, float t) => presence?.Handle(PresenceInput.CorrectAction, t);

        // --- hooks of the level screen ---------------------------------------------------------------------------------------------

        /// <summary>A level attempt started (or resumed: the log has more than the start action). `won` = the save marks the level won (K-53/6).</summary>
        public void LevelStarted(GameSession session, bool won)
        {
            Reset();
            controller = TutorialFactory.Create(session.Lvl, this, won);
            if (controller == null) return;
            controller.Start(now);
            if (session.Log.Count > 1)
            {
                // K-43: the Unity save keeps no tutorial position, so the step is rebuilt from the log (TECH §8.2)
                try
                {
                    TutorialReplay.Rebuild(session.Lvl, session.Log, controller, now, st => replayState = st);
                }
                catch (Exception)
                {
                    // a log the replay cannot rebuild was voided by the app already; the tutorial then simply starts over
                }
                replayState = null;
            }
            windows = player.Panel != PanelKind.None ? 1 : 0;
        }

        /// <summary>The attempt is decided (won, out of moves, lost) or left: nothing more is shown (GDD §14.1/5).</summary>
        public void Stop()
        {
            controller?.Stop();
            Sync();
        }

        /// <summary>Back to nothing (a new level, the home screen).</summary>
        public void Reset()
        {
            controller = null;
            presence = null;
            content = null;
            seenVersion = -1;
            windows = 0;
            heldMs = 0;
            replayState = null;
            View.Clear();
        }

        public void TouchDown() => presence?.Handle(PresenceInput.TouchDown, now);

        public void TouchUp() => presence?.Handle(PresenceInput.TouchUp, now);

        /// <summary>A block was lifted (the K-07 threshold passed): a new drag; lifting a highlighted block is the right action (the glow steps back).</summary>
        public void DragLifted(int pieceId)
        {
            var c = controller;
            if (c == null) return;
            c.DragStarted();
            heldMs = 0;
            if (c.Highlights(pieceId)) presence?.Handle(PresenceInput.CorrectAction, now);
        }

        public void DragSignal(string kind) => controller?.DragSignal(kind, now);

        public void Tapped(int pieceId) => controller?.TappedPiece(pieceId, now);

        /// <summary>A booster slot was tapped (`booster:hammer` …).</summary>
        public void TappedTarget(string target) => controller?.TappedTarget(target, now);

        /// <summary>One committed move finished playing: a move-end event counts once; the glove condition and the rectangles are read again.</summary>
        public void MoveEnded(IReadOnlyList<GameEvent> events)
        {
            var c = controller;
            if (c == null) return;
            c.MoveEnded(events, now);
            Sync();
            if (!c.Finished) Rebuild(false);
        }

        /// <summary>A window opened or closed (`LevelPlayer.Panel`): every timer stops while one is open (K-53/3).</summary>
        public void PanelChanged(bool open)
        {
            int was = windows;
            windows = open ? 1 : 0;
            if (presence == null) return;
            if (open && was == 0) presence.Handle(PresenceInput.WindowOpen, now);
            else if (!open && was > 0) presence.Handle(PresenceInput.WindowClose, now);
        }

        /// <summary>The screen size or the layout changed: the rectangles are read again.</summary>
        public void LayoutChanged()
        {
            if (controller == null) return;
            Rebuild(true);
        }

        // --- frame -----------------------------------------------------------------------------------------------------------------

        public void Tick(float dtMs)
        {
            if (!(dtMs > 0)) return;
            now += dtMs;
            var c = controller;
            if (c == null) return;
            if (!player.View.WorldVisible)
            {
                Reset();
                return;
            }
            Sync();
            var p = presence;
            if (p == null) return;
            HoldWatch(c, dtMs);
            p.Handle(PresenceInput.Tick, now);
            var look = p.Look();
            var step = c.Current;
            IReadOnlyList<PxRect> lit = content != null ? content.Lit : litScratch;
            if (step != null && player.Mapper.Active && look.Highlight)
            {
                litScratch.Clear();
                litScratch.AddRange(LitRects(step));
                if (litScratch.Count == lit.Count) lit = litScratch;
            }
            View.Frame(now, look, glovePlays, lit, player.Reduced);
        }

        /// <summary>`holdOverBuild`: the finger keeps a block over the site for `minMs` (the scene's hold timer, TECH §8.2).</summary>
        private void HoldWatch(TutorialController c, float dtMs)
        {
            int? min = c.HoldMinMs();
            var drag = player.Mapper.Session;
            if (min == null || !player.Mapper.Active || drag == null)
            {
                heldMs = 0;
                return;
            }
            var drop = drag.Classify();
            bool over = drop.Kind == DropKinds.SiteFree || drop.Kind == DropKinds.SiteRail;
            heldMs = over ? heldMs + dtMs : 0f;
            if (over && heldMs >= min.Value) c.DragSignal(TutorialEvents.HoldOverBuild, now, heldMs);
        }

        /// <summary>A new active step (or none) since the last frame: a new presence and a new tree.</summary>
        private void Sync()
        {
            var c = controller;
            if (c == null || c.Version == seenVersion) return;
            seenVersion = c.Version;
            var step = c.Current;
            if (step == null)
            {
                presence = null;
                content = null;
                View.Clear();
                return;
            }
            float delay = Settings.StartDelayMs + (step.Index > 0 ? Settings.NextStepDelayMs : 0f);
            presence = new TutorialPresence(Settings, now, delay);
            if (windows > 0) presence.Handle(PresenceInput.WindowOpen, now);
            Rebuild(true);
        }

        /// <summary>Reads the glove condition and the rectangles of the active step again; `force` rebuilds the tree even if nothing changed.</summary>
        private void Rebuild(bool force)
        {
            var c = controller;
            var step = c?.Current;
            var session = player.Session;
            var L = player.Layout;
            if (c == null || step == null || session == null || L == null) return;
            bool plays = TutorialGlove.Plays(session.State, step.Data, step.Pieces, session.Hooks.Drag);
            var next = BuildContent(step, plays, session, L);
            bool same = !force && content != null && plays == glovePlays && ContentEquals(content, next);
            glovePlays = plays;
            if (same) return;
            content = next;
            View.Build(next);
        }

        private TutorialContent BuildContent(ActiveStep step, bool plays, GameSession session, ScreenLayout L)
        {
            var data = step.Data;
            var lit = LitRects(step);
            string text = player.Text.Get(data.TextKey);
            var path = new Vec2[0];
            string? kind = null;
            PxRect? handBox = null;
            if (data.Hand != null && plays)
            {
                kind = data.Hand.Kind;
                if (kind == "tap")
                {
                    if (lit.Count > 0) path = new[] { new Vec2((float)lit[0].CenterX, (float)lit[0].CenterY) };
                }
                else if (data.Hand.Path != null && data.Hand.Path.Length > 0)
                {
                    path = TutorialGlove.RoundedPath(TutorialGlove.FingerPoints(L.Grid, player.Feel.FingerOffsetCells, data.Hand.Path), Settings.CornerRadiusPx);
                }
                if (path.Length == 0) kind = null;
                else
                {
                    double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                    foreach (var p in path)
                    {
                        x0 = Math.Min(x0, p.X);
                        y0 = Math.Min(y0, p.Y);
                        x1 = Math.Max(x1, p.X);
                        y1 = Math.Max(y1, p.Y);
                    }
                    handBox = new PxRect(x0 - Settings.GloveW * 0.36, y0 - 8, x1 - x0 + Settings.GloveW, y1 - y0 + Settings.GloveH);
                }
            }
            var size = View.BubbleSize(text);
            var place = TutorialLayout.PlaceBubble(L, size.X, size.Y, data.Highlight, lit, handBox, Settings.MarginPx, Settings.DockGapPx);
            return new TutorialContent
            {
                Text = text,
                Dock = new DockPlace(place.Dock, new PxRect(place.Rect.X, place.Rect.Y, size.X, size.Y), place.TailUp),
                Lit = lit,
                HandKind = kind,
                Path = path,
                CanvasW = player.Viewport.W,
                CanvasH = player.Viewport.H,
            };
        }

        private static bool ContentEquals(TutorialContent a, TutorialContent b)
        {
            if (a.Text != b.Text || a.HandKind != b.HandKind || a.Path.Length != b.Path.Length || a.Lit.Count != b.Lit.Count) return false;
            if (a.CanvasW != b.CanvasW || a.CanvasH != b.CanvasH) return false;
            for (int i = 0; i < a.Lit.Count; i++) if (!a.Lit[i].Equals(b.Lit[i])) return false;
            for (int i = 0; i < a.Path.Length; i++) if (!a.Path[i].Equals(b.Path[i])) return false;
            return a.Dock.Rect.Equals(b.Dock.Rect) && a.Dock.TailUp == b.Dock.TailUp;
        }

        // --- rectangles --------------------------------------------------------------------------------------------------------------

        private List<PxRect> LitRects(ActiveStep step)
        {
            var session = player.Session;
            var L = player.Layout;
            if (session == null || L == null) return new List<PxRect>();
            var summary = player.Summary;
            bool truck = summary != null && summary.Queue.Length + summary.PendingBlocks > 0;
            var ctx = new HighlightContext(L, session.State, session.Lvl, truck, PieceRect);
            return TutorialLayout.All(step.Data.Highlight, ctx);
        }

        /// <summary>The block as the screen shows it: the lifted block follows the finger, others sit at their rest pose; null = not on the board.</summary>
        private PxRect? PieceRect(int pieceId)
        {
            var session = player.Session;
            var L = player.Layout;
            if (session == null || L == null) return null;
            var s = session.State;
            if (Grid.PieceBoardCells(s, pieceId).Length == 0) return null;
            var shape = BoardPose.ShapeOf(s, pieceId);
            var g = L.Grid;
            double w = shape.W * g.CellPx;
            double h = shape.H * g.CellPx;
            if (player.Mapper.Active && player.Mapper.PieceId == pieceId)
            {
                var v = player.Pieces.Get(pieceId);
                if (v != null) return new PxRect(v.Position.X - w / 2, v.Position.Y - v.Hop - h / 2, w, h);
            }
            if (!BoardPose.RestAnchor(s, pieceId, out int ax, out int ay)) return null;
            return g.PieceRect(ax, ay, shape.W, shape.H);
        }
    }
}
