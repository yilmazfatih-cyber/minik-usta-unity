#nullable enable
// When the 3D clip pages are decoded (P4B-05). A page decode (ImageConversion.LoadImage) is synchronous: king_0 (1964x2012) takes
// 160 ms and fx_0 (1920x752) 47 ms on a desktop CPU in the CI log, a few times that on a phone. Decoded lazily on first use, the fx
// page was decoded exactly at the player's first correct placement (the lock flash) and the king page on the first home frame. The
// warm-up moves both to moments where a longer frame cannot be seen:
//   - the king pages (`KingLibrary`) and the crew page (`CrewLibrary`, Tuna and Kepçe: art turn 1, 2036x1032, both are on the first home
//     frame and in the level corner) at boot, before the first frame is shown (the Unity loader bar is still up);
//   - the fx pages (`FxLibrary`) as soon as the player leaves the home screen (the level-start window, 'Oyna' was pressed) or, if the
//     player does not, after `IdleMs` of a calm home screen. Never during play.
// Engine-independent: the Unity layer passes `ClipSprites.Warm` as the callback; tests pass a recorder.
using System;
using System.Collections.Generic;
using MinikUsta.Presentation.Flow;

namespace MinikUsta.Presentation.Anim
{
    public sealed class ArtWarmup
    {
        public const string KingLibrary = "king";
        public const string FxLibrary = "fx";
        public const string CrewLibrary = "crew";

        /// <summary>Milliseconds of a calm home screen after which the fx pages are decoded anyway.</summary>
        public const double DefaultIdleMs = 2500;

        private readonly Action<string> warm;
        private readonly double idleMs;
        private double idle;
        private bool fxDone;

        public ArtWarmup(Action<string> warm, double idleMs = DefaultIdleMs)
        {
            this.warm = warm ?? throw new ArgumentNullException(nameof(warm));
            this.idleMs = idleMs;
        }

        /// <summary>Libraries warmed so far, in order (tests, logs).</summary>
        public List<string> Warmed { get; } = new List<string>();

        public bool KingDone { get; private set; }
        public bool FxDone => fxDone;

        /// <summary>Decodes the character pages now, the king's then the crew's (called once, in `Awake`, before the first frame).</summary>
        public void WarmKing()
        {
            if (KingDone) return;
            KingDone = true;
            Warmed.Add(KingLibrary);
            warm(KingLibrary);
            Warmed.Add(CrewLibrary);
            warm(CrewLibrary);
        }

        /// <summary>
        /// Every frame: the fx pages are decoded when the app is on any screen but boot/home (the level-start window, the level), or
        /// after `idleMs` on the home screen. One decode per frame at most (the call is synchronous).
        /// </summary>
        public void Tick(double dtMs, AppScreen screen)
        {
            if (fxDone) return;
            if (screen == AppScreen.Boot) return;
            if (screen != AppScreen.Home)
            {
                WarmFx();
                return;
            }
            idle += dtMs;
            if (idle >= idleMs) WarmFx();
        }

        private void WarmFx()
        {
            fxDone = true;
            Warmed.Add(FxLibrary);
            warm(FxLibrary);
        }
    }
}
