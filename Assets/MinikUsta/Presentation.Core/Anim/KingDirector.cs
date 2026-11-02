#nullable enable
// Usta Kral on screen (paket B2; D-101; ART_3D §7.2; JUICE #12/#15/#16, #18, #21, #52/#57, #55/#95, #81/#103/#104, #99): which clip
// the king plays where. Three slots, each a `ClipActor` on a UI node:
//   home    the main symbol of the home screen: waves twice on arrival, then the stage idle; a tap → a cheer (stage scale)
//   corner  the level screen's corner portrait (in place of Tuna): idle; a correct placement → a short cheer; a Golden Trowel, a
//           finished segment, the win → a big cheer; 5 s without a touch or a stuck pulse (K-34 hook 5) → think until the next
//           touch; the tutorial bubble → talk, the tutorial glove → point (Usta Dede's bubbles become the king's); out of moves →
//           encourage (never mocking, never shaming)
//   window  the king in the open window: level start → point; win → big cheer (loops); +5 offer / life lost → encourage, then
//           idle; exit confirmation → think
// One state machine per slot: a ONE-SHOT (cheer, big cheer, encourage, wave) plays to its end and then the slot's LOOP (idle,
// think, talk, point) comes back; a higher-priority one-shot replaces a lower one, an equal or lower one is dropped (no
// jitter). Every clip starts and ends on K0, loops with `out` frames leave through them, so changes are cut-free.
// Budget (ART_3D §7.5, B2 brief): at most two KING clips advance at once — the corner freezes while a window shows the
// king, home and corner never run together. Tuna and Kepçe (`Crew`, art turn 1) are idle loops of the same screens, counted apart. Reduced motion: still poses, short clips, no breathing (`AnimPlayer.Reduced`).
// It also plays the window FX that must sit INSIDE the window, between the panel and its content: the JUICE #96 star burst
// (`fx_star_burst` behind the earned star; the Fx layer would draw it over the star) and, art turn 1, the dust puff under the win
// window's level piece when it lands (`fx_dust_puff`, tint #E9D2A8).
using System;
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Anim
{
    public enum KingCue
    {
        Idle,
        Cheer,
        BigCheer,
        Encourage,
        Wave,
        Think,
        Talk,
        Point,
    }

    public sealed class KingDirector
    {
        public const string IdleClip = "king_idle";
        public const string HomeIdleClip = "king_home_idle";
        public const string CheerClip = "king_cheer";
        public const string BigCheerClip = "king_big_cheer";
        public const string ThinkClip = "king_think";
        public const string TalkClip = "king_talk";
        public const string PointClip = "king_point";
        public const string EncourageClip = "king_encourage";
        public const string WaveClip = "king_wave";
        /// <summary>The v4 atlas still the nodes carry until the actor draws them (B1: `chr_dede_idle` holds Usta Kral pixels).</summary>
        public const string FallbackFrame = "chr_dede_idle";

        /// <summary>One slot: an actor and its state machine.</summary>
        public sealed class Slot
        {
            private readonly KingDirector d;
            private AnimClip? pending;
            private int pendingPasses;
            private AnimClip? loop;

            internal Slot(KingDirector director, string name, string baseClip)
            {
                d = director;
                Name = name;
                Actor = new ClipActor(director.Library);
                var b = director.Library?.Clip(baseClip);
                if (b != null) Actor.BaseScale = b.Scale;
                Actor.Player.OnEvent = e => director.OnEvent?.Invoke(name, e);
            }

            public string Name { get; }
            public ClipActor Actor { get; }
            public bool Bound => Actor.Bound;
            /// <summary>The slot's loop (what it returns to after a one-shot).</summary>
            public string? LoopClip => loop?.Name;
            /// <summary>The clip playing now.</summary>
            public string? Playing => Actor.Player.Clip?.Name;

            internal void SetLoop(string clip)
            {
                var c = d.Library?.Clip(clip);
                if (c != null) loop = c;
            }

            /// <summary>Plays a one-shot unless one of equal or higher priority is playing.</summary>
            internal void Shot(string clip, int passes = 1)
            {
                var c = d.Library?.Clip(clip);
                if (c == null) return;
                var cur = OneShotNow();
                if (pending != null && Priority(pending.Name) >= Priority(clip)) return;
                if (cur != null && Priority(cur.Name) >= Priority(clip)) return;
                pending = c;
                pendingPasses = passes;
                if (cur != null) Start(); // a lower one-shot is replaced at once (both are on K0 only at their ends: a hard cut, softened)
            }

            internal void Reset()
            {
                pending = null;
                Actor.Player.Stop();
            }

            private AnimClip? OneShotNow()
            {
                var p = Actor.Player;
                return p.Clip != null && p.Playing && IsOneShot(p.Clip.Name) ? p.Clip : null;
            }

            private void Start()
            {
                var p = Actor.Player;
                bool cut = p.Clip != null && p.Playing && p.Frame != p.Clip.First;
                p.Play(pending!, pendingPasses);
                if (cut) p.Cut = true;
                pending = null;
            }

            /// <summary>One frame of the state machine, then the actor.</summary>
            internal void Advance(float dtMs)
            {
                var p = Actor.Player;
                if (pending != null)
                {
                    if (p.Clip != null && p.Playing && p.Clip.Loops && p.Clip.Out.Length > 0)
                    {
                        if (!p.Ending) p.Release(); // think / point / wave leave through their out frames first
                    }
                    else Start();
                }
                else if (OneShotNow() == null && loop != null)
                {
                    if (p.Clip == null || !p.Playing)
                    {
                        // a big cheer ends on its loop drawing, not on K0: the cut back to the loop settles with a squash
                        bool cut = p.Clip != null && p.Frame != loop.First;
                        p.Play(loop);
                        if (cut) p.Cut = true;
                    }
                    else if (p.Clip != loop && !p.Ending)
                    {
                        // think / point / wave leave through their out frames, talk finishes its sentence (it ends on K0); idle cuts
                        if (p.Clip.Out.Length > 0 || (p.Clip.Loops && p.Clip.EndsOnFirst)) p.Release();
                        else p.Play(loop);
                    }
                }
                Actor.Advance(dtMs);
            }
        }

        public KingDirector(ArtSet? art)
        {
            Library = art?.King;
            fx = art?.Fx;
            Crew = new CrewDirector(art);
            burst = fx != null ? new ClipActor(fx) : null;
            puff = fx != null ? new ClipActor(fx) : null;
            Home = new Slot(this, "home", HomeIdleClip);
            Corner = new Slot(this, "corner", IdleClip);
            Window = new Slot(this, "window", HomeIdleClip);
            Home.SetLoop(HomeIdleClip);
            Corner.SetLoop(IdleClip);
        }

        public ClipLibrary? Library { get; }
        public bool Enabled => Library != null;

        /// <summary>Art turn 1: Tuna and Kepçe (crew.json) ride on the same screens, visibility flags and reduced-motion switch as the king.</summary>
        public CrewDirector Crew { get; }

        public Slot Home { get; }
        public Slot Corner { get; }
        public Slot Window { get; }

        /// <summary>Event frames of every slot (`land`, `hat_land`, `pump` …): the host plays sounds and haptics.</summary>
        public Action<string, AnimEvent>? OnEvent { get; set; }

        /// <summary>The home screen is on top (its slot advances).</summary>
        public bool HomeVisible { get; set; }

        /// <summary>The level screen is on top (the corner slot advances unless a window shows the king).</summary>
        public bool CornerVisible { get; set; }

        /// <summary>After this long without a touch the corner king thinks (B2 brief: 5 s).</summary>
        public float ThinkAfterMs { get; set; } = 5000f;

        /// <summary>Slots that advanced in the last frame (≤ 2: ART_3D §7.5 budget).</summary>
        public int ActiveClips { get; private set; }
        public int PeakActiveClips { get; private set; }

        private readonly ClipLibrary? fx;
        private readonly ClipActor? burst;
        private readonly ClipActor? puff;
        private bool burstOn;
        private bool puffOn;
        private bool reduced;
        private float quietMs;
        private bool thinking;
        private bool talk;
        private bool point;
        private bool cornerWasVisible;

        public bool Reduced
        {
            get => reduced;
            set
            {
                reduced = value;
                Home.Actor.Player.Reduced = value;
                Corner.Actor.Player.Reduced = value;
                Window.Actor.Player.Reduced = value;
                Crew.Reduced = value;
            }
        }

        public static bool IsOneShot(string clip) => clip == CheerClip || clip == BigCheerClip || clip == EncourageClip || clip == WaveClip;

        /// <summary>One-shot priority: encourage &gt; big cheer &gt; cheer &gt; wave (a tap on the waving home king cheers at once).</summary>
        public static int Priority(string clip)
        {
            switch (clip)
            {
                case EncourageClip: return 4;
                case BigCheerClip: return 3;
                case CheerClip: return 2;
                case WaveClip: return 1;
                default: return 0;
            }
        }

        /// <summary>The size factor that fits the king of a slot into a box `heightPx` tall (the clip canvas height = the box).</summary>
        public double SizeFor(Slot slot, double heightPx)
        {
            var c = Library?.Clip(slot == Corner ? IdleClip : HomeIdleClip);
            return c != null ? ClipPlacement.FitHeight(c, heightPx) : 1;
        }

        // --- home ------------------------------------------------------------------------------------------------------------

        /// <summary>The home tree was (re)built: the king stands with his feet at `feet`; `greet` = the home screen was just entered (a wave).</summary>
        public void BindHome(IUiSurface surface, UiNode node, Vec2 feet, double size, bool greet)
        {
            if (!Enabled) return;
            Home.Actor.Bind(surface, node, feet, size);
            if (greet)
            {
                Home.Reset();
                Home.Shot(WaveClip, 2);
            }
        }

        public void UnbindHome() => Home.Actor.Unbind();

        /// <summary>A tap on the home king: a cheer (unless he is already celebrating).</summary>
        public void HomeTapped() => Home.Shot(CheerClip);

        // --- corner ----------------------------------------------------------------------------------------------------------

        public void BindCorner(IUiSurface surface, UiNode node, Vec2 feet, double size)
        {
            if (!Enabled) return;
            Corner.Actor.Bind(surface, node, feet, size);
        }

        /// <summary>A new level attempt: the corner starts calm (idle), no pending reaction, the quiet timer restarts.</summary>
        public void LevelStarted()
        {
            Corner.Reset();
            quietMs = 0;
            thinking = false;
            talk = false;
            point = false;
            Corner.SetLoop(IdleClip);
        }

        /// <summary>A game event for the corner king (cheer, big cheer, encourage).</summary>
        public void Cue(KingCue cue)
        {
            switch (cue)
            {
                case KingCue.Cheer:
                    Corner.Shot(CheerClip);
                    break;
                case KingCue.BigCheer:
                    Corner.Shot(BigCheerClip, 1);
                    break;
                case KingCue.Encourage:
                    Corner.Shot(EncourageClip);
                    break;
                case KingCue.Think:
                    Stuck();
                    break;
            }
        }

        /// <summary>A touch on the level screen: thinking stops, the quiet timer restarts.</summary>
        public void Touched()
        {
            quietMs = 0;
            thinking = false;
        }

        /// <summary>K-34 hook 5 (the stuck pulse) or 5 s without a touch: the king thinks until the next touch.</summary>
        public void Stuck() => thinking = true;

        /// <summary>The tutorial's look (every frame): bubble on → talk, glove playing → point.</summary>
        public void Tutorial(bool bubble, bool glove)
        {
            talk = bubble;
            point = glove;
        }

        private string CornerLoop() => point ? PointClip : talk ? TalkClip : thinking ? ThinkClip : IdleClip;

        // --- window ----------------------------------------------------------------------------------------------------------

        /// <summary>A window shows the king: `cue` picks his clip (point, big cheer, encourage, think).</summary>
        public void BindWindow(IUiSurface surface, UiNode node, Vec2 feet, double size, KingCue cue)
        {
            if (!Enabled) return;
            Window.Reset();
            Window.Actor.Bind(surface, node, feet, size);
            switch (cue)
            {
                case KingCue.Point:
                    Window.SetLoop(PointClip);
                    break;
                case KingCue.BigCheer:
                    Window.SetLoop(BigCheerClip);
                    break;
                case KingCue.Think:
                    Window.SetLoop(ThinkClip);
                    break;
                case KingCue.Encourage:
                    Window.SetLoop(HomeIdleClip);
                    Window.Shot(EncourageClip);
                    break;
                default:
                    Window.SetLoop(HomeIdleClip);
                    break;
            }
        }

        public void UnbindWindow()
        {
            Window.Actor.Unbind();
            Window.Reset();
            burst?.Unbind();
            burstOn = false;
            puff?.Unbind();
            puffOn = false;
        }

        /// <summary>The open window's star-burst node (hidden until `PlayWindowBurst`): the burst is drawn by its centre at `center`.</summary>
        public void BindWindowBurst(IUiSurface surface, UiNode node, Vec2 center)
        {
            if (burst == null) return;
            burstOn = false;
            burst.Player.Stop();
            burst.Bind(surface, node, center, 1);
        }

        /// <summary>JUICE #96: plays `clip` once behind the window's star; false = not played (no clip, no node, reduced motion).</summary>
        public bool PlayWindowBurst(string clip, double size)
        {
            var c = fx?.Clip(clip);
            var n = burst?.Node;
            if (burst == null || c == null || n == null || !burst.Bound || reduced) return false;
            burst.Size = size;
            burst.Player.Play(c);
            n.Visible = true;
            burstOn = true;
            burst.Advance(0f);
            return true;
        }

        /// <summary>The star burst is playing (tests).</summary>
        public bool BurstPlaying => burstOn;

        /// <summary>Art turn 1: the open win window's dust-puff node (hidden until `PlayWindowPuff`): the puff stands on `ground` (the clip's bottom-centre pivot).</summary>
        public void BindWindowPuff(IUiSurface surface, UiNode node, Vec2 ground)
        {
            if (puff == null) return;
            puffOn = false;
            puff.Player.Stop();
            puff.Bind(surface, node, ground, 1);
        }

        /// <summary>Art turn 1 (ART_3D §14.3 item 4): plays `clip` once under the window's piece; false = not played (no clip, no node, reduced motion).</summary>
        public bool PlayWindowPuff(string clip, double size)
        {
            var c = fx?.Clip(clip);
            var n = puff?.Node;
            if (puff == null || c == null || n == null || !puff.Bound || reduced) return false;
            puff.Size = size;
            puff.Player.Play(c);
            n.Visible = true;
            puffOn = true;
            puff.Advance(0f);
            return true;
        }

        /// <summary>The window puff is playing (tests).</summary>
        public bool PuffPlaying => puffOn;

        // --- frame -----------------------------------------------------------------------------------------------------------

        /// <summary>One frame (`dtMs` of real time; the king keeps his pace when the board plays 3× faster).</summary>
        public void Advance(float dtMs)
        {
            if (!(dtMs > 0)) dtMs = 0;
            // the crew: the home pair on the home screen, Kepçe's corner copy while no window shows the king (they do not need the king's library)
            Crew.Advance(dtMs, HomeVisible, CornerVisible && !Window.Bound);
            if (!Enabled) return;
            int active = 0;
            if (Home.Bound && HomeVisible)
            {
                Home.Advance(dtMs);
                active++;
            }
            bool corner = Corner.Bound && CornerVisible && !Window.Bound;
            if (corner)
            {
                if (!cornerWasVisible) quietMs = 0;
                quietMs += dtMs;
                if (!thinking && quietMs >= ThinkAfterMs) thinking = true;
                Corner.SetLoop(CornerLoop());
                Corner.Advance(dtMs);
                active++;
            }
            cornerWasVisible = corner;
            if (Window.Bound)
            {
                Window.Advance(dtMs);
                active++;
            }
            if (burstOn && burst != null)
            {
                burst.Advance(dtMs);
                if (!burst.Player.Playing)
                {
                    burstOn = false;
                    var n = burst.Node;
                    if (n != null)
                    {
                        n.Visible = false;
                        burst.Refresh();
                    }
                }
            }
            if (puffOn && puff != null)
            {
                puff.Advance(dtMs);
                if (!puff.Player.Playing)
                {
                    puffOn = false;
                    var n = puff.Node;
                    if (n != null)
                    {
                        n.Visible = false;
                        puff.Refresh();
                    }
                }
            }
            ActiveClips = active;
            if (active > PeakActiveClips) PeakActiveClips = active;
        }
    }
}
