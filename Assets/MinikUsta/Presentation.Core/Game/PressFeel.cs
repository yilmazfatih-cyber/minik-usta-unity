#nullable enable
// The press feel of a button (Faz 3 Q2 item 11; JUICE #69): pressed it sinks to 0,94 in 60 ms (Quad.easeOut); released it
// pops to 1,04 and settles back to 1,0 in 120 ms (Back.easeOut). A pointer that slides off the button cancels the press
// (no click, as uGUI): it just returns to 1,0. Engine-independent: it drives an `ITweenTarget` on its own clock; the Unity
// layer feeds it the pointer events of a uGUI button and the frame time.
using System;
using MinikUsta.Presentation.Feel;
using MinikUsta.Presentation.Tweening;

namespace MinikUsta.Presentation.Game
{
    public sealed class PressFeel
    {
        private readonly ITweenTarget target;
        private readonly FeelSettings feel;
        private readonly TweenClock clock = new TweenClock();
        private Tween? running;

        public PressFeel(ITweenTarget target, FeelSettings feel)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.feel = feel ?? throw new ArgumentNullException(nameof(feel));
        }

        /// <summary>True between `Press` and `Release` / `Cancel`.</summary>
        public bool Held { get; private set; }

        /// <summary>The pointer went down on the button: sink to `ButtonPressScale`.</summary>
        public void Press()
        {
            if (Held) return;
            Held = true;
            Start(Tweens.Scale(target, feel.ButtonPressScale, feel.ButtonPressMs, feel.ButtonPressEase));
        }

        /// <summary>The pointer was released on the button (the click): pop to `ButtonReleasePeak`, settle to 1 with `ButtonReleaseEase`.</summary>
        public void Release()
        {
            if (!Held) return;
            Held = false;
            float peak = feel.ButtonReleasePeak;
            Start(new Tween(feel.ButtonReleaseMs, (u, k) =>
            {
                float s = u >= 1f ? 1f : peak + (1f - peak) * k;
                target.Scale = new Vec2(s, s);
            }, () => target.Scale = new Vec2(peak, peak), feel.ButtonReleaseEase));
        }

        /// <summary>The pointer left the button (or the press was cancelled): back to 1, no pop.</summary>
        public void Cancel()
        {
            if (!Held) return;
            Held = false;
            Start(Tweens.Scale(target, 1f, feel.ButtonReleaseMs, feel.ButtonReleaseEase));
        }

        /// <summary>Straight to rest (the button is hidden or re-shown).</summary>
        public void Reset()
        {
            Held = false;
            clock.Kill();
            running = null;
            target.Scale = Vec2.One;
        }

        public void Tick(float dtMs)
        {
            if (running == null) return;
            clock.Advance(dtMs);
            if (!running.IsActive) running = null;
        }

        /// <summary>A tween is still moving the button.</summary>
        public bool Busy => running != null && running.IsActive;

        private void Start(Tween t)
        {
            clock.Kill();
            running = clock.Play(t);
        }
    }
}
