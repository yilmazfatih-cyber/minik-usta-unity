#nullable enable
// Tuna and Kepçe on screen (art turn 1; ART_3D §14.2, §14.3 item 2; art/3d/blender/crew/README.md): the second character library
// `Resources/art3d/crew.json` (same format as king.json, read by `ClipLibrary.Parse`). Three loops, each a `ClipActor` on a UI node:
//   home.tuna    → tuna_home_idle    (stage set, scale 0,84; 48 timeline frames of 24 drawings on twos, 24 fps)
//   home.kepce   → kepce_home_idle   (stage set, scale 0,84)
//   hud.kepce    → kepce_corner_idle (corner set, scale 1,05; the same drawings as kepce_home_idle, aliasOf)
// A crew member is placed by its ROOT PIVOT (the feet) at the bottom centre of the box the v4 still used, at the clip's own size —
// never fitted into the box (`ClipActor.Size` 1). Reduced motion: the clip's first drawing (`chr_tuna_home_idle_00`,
// `chr_kepce_idle_00`), no timeline. A member advances only while its screen is on top (the home pair on the home screen, Kepçe's corner
// copy on the level screen while no window shows the king), so the CPU cost of the idle loops is paid where they are seen.
// Without crew.json every member stays the v4 atlas still its node carries (`chr_tuna_cheer`, `chr_kepce_idle`).
using MinikUsta.Presentation.Ui;

namespace MinikUsta.Presentation.Anim
{
    public sealed class CrewDirector
    {
        public const string TunaHomeClip = "tuna_home_idle";
        public const string KepceHomeClip = "kepce_home_idle";
        public const string KepceCornerClip = "kepce_corner_idle";
        public const string TunaId = "home.tuna";
        public const string KepceId = "home.kepce";
        public const string HudKepceId = "hud.kepce";
        /// <summary>The v4 atlas stills the nodes carry until (or unless) the clips draw them.</summary>
        public const string TunaFallbackFrame = "chr_tuna_cheer";
        public const string KepceFallbackFrame = "chr_kepce_idle";

        private bool reduced;

        public CrewDirector(ArtSet? art)
        {
            Library = art?.Crew;
            Tuna = Make(TunaHomeClip);
            KepceHome = Make(KepceHomeClip);
            KepceCorner = Make(KepceCornerClip);
        }

        public ClipLibrary? Library { get; }
        public bool Enabled => Library != null;

        /// <summary>Tuna on the home screen.</summary>
        public ClipActor Tuna { get; }
        /// <summary>Kepçe on the home screen.</summary>
        public ClipActor KepceHome { get; }
        /// <summary>Kepçe at the king's feet in the level corner.</summary>
        public ClipActor KepceCorner { get; }

        /// <summary>Members that advanced in the last frame.</summary>
        public int ActiveClips { get; private set; }

        public bool Reduced
        {
            get => reduced;
            set
            {
                reduced = value;
                Tuna.Player.Reduced = value;
                KepceHome.Player.Reduced = value;
                KepceCorner.Player.Reduced = value;
            }
        }

        private ClipActor Make(string clip)
        {
            var a = new ClipActor(Library);
            var c = Library?.Clip(clip);
            if (c != null)
            {
                a.BaseScale = c.Scale; // one size per member: the stage or the corner set of its own clip
            }
            return a;
        }

        private void Start(ClipActor a, string clip)
        {
            var c = Library?.Clip(clip);
            if (c == null) return;
            if (a.Player.Clip != c) a.Player.Play(c);
        }

        /// <summary>The home tree was (re)built: Tuna and Kepçe stand with their feet at `tunaFeet` / `kepceFeet` (design px).</summary>
        public void BindHome(IUiSurface surface, UiNode tuna, Vec2 tunaFeet, UiNode kepce, Vec2 kepceFeet)
        {
            if (!Enabled) return;
            Start(Tuna, TunaHomeClip);
            Start(KepceHome, KepceHomeClip);
            Tuna.Bind(surface, tuna, tunaFeet, 1);
            KepceHome.Bind(surface, kepce, kepceFeet, 1);
        }

        public void UnbindHome()
        {
            Tuna.Unbind();
            KepceHome.Unbind();
        }

        /// <summary>The level HUD was (re)built: Kepçe stands with his feet at `feet`.</summary>
        public void BindCorner(IUiSurface surface, UiNode kepce, Vec2 feet)
        {
            if (!Enabled) return;
            Start(KepceCorner, KepceCornerClip);
            KepceCorner.Bind(surface, kepce, feet, 1);
        }

        /// <summary>One frame: the members whose screen is on top advance.</summary>
        public void Advance(float dtMs, bool homeVisible, bool cornerRuns)
        {
            if (!Enabled) return;
            if (!(dtMs > 0)) dtMs = 0;
            int active = 0;
            if (homeVisible)
            {
                if (Tuna.Bound)
                {
                    Tuna.Advance(dtMs);
                    active++;
                }
                if (KepceHome.Bound)
                {
                    KepceHome.Advance(dtMs);
                    active++;
                }
            }
            if (cornerRuns && KepceCorner.Bound)
            {
                KepceCorner.Advance(dtMs);
                active++;
            }
            ActiveClips = active;
        }
    }
}
