#nullable enable
// `TutorialPresence` (paket C1; web TutorialPresence.ts; UX_FLOWS §13.1 "Zamanlama", GDD K-53/3): what ONE active tutorial step shows. It never
// ends a step (only the step's `done` event does, `TutorialController`); it decides whether the glove, the bubble and the highlight are on
// screen. No engine, no clock of its own: every input carries the animation time `t` (ms).
//
//   wait   nothing                              → `delayMs` after the start → shown
//   shown  bubble, glove (no touch held), glow  → `visibleMs` → hidden · a touch: glove off, bubble faded, its lift → hidden · `correctAction` → hidden
//   hidden nothing                              → `idleReshowMs` without any touch → shown (every touch restarts the count)
//   done   nothing (the view fades out)         —
//
// The step is ACTIVE in wait, shown and hidden: input is never blocked. `Shows` counts every entry into shown; after `ReshowsWithBubble`
// re-shows the bubble stays away (the text was read), the glove and the glow still come back. `WindowOpen` … `WindowClose` stop every
// timer and hide everything; on the last close the step goes on from the phase it was in.
using System;

namespace MinikUsta.Presentation.Tutorial
{
    /// <summary>`tokens.tutorial` of the design tokens (UX §13.1); a unit test compares every value with Golden/tokens.json.</summary>
    public sealed class TutorialSettings
    {
        public float StartDelayMs = 600;
        public float AppearMs = 220;
        public float HideMs = 200;
        public float VisibleMs = 4000;
        public float IdleReshowMs = 4000;
        public float NextStepDelayMs = 400;
        public int MaxStepsPerLevel = 2;
        public int MaxWordsPerBubble = 6;
        public int ReshowsWithBubble = 3;
        public float DragFadeAlpha = 0.35f;
        public float HandLoopMs = 1600;
        public float HandPauseMs = 400;
        public float GloveW = 140;
        public float GloveH = 160;
        public float GlovePressScale = 0.9f;
        public float TrailDotPx = 12;
        public float TrailGapPx = 22;
        public float TrailAlpha = 0.85f;
        public float HighlightStrokePx = 8;
        public float HighlightGlowPx = 18;
        public float HighlightPadPx = 4;
        public float HighlightPulseScale = 1.04f;
        public float HighlightPulseMs = 1200;
        public float BubbleMaxW = 640;
        public float BubbleMinH = 104;
        public float PortraitPx = 128;
        public float PortraitGapPx = 12;
        public float DockGapPx = 16;
        public float GhostAlpha = 0.55f;
        public float GhostLiftMs = 160;
        /// <summary>UX §13.1: corners of the glove path are rounded with a 24 px radius.</summary>
        public float CornerRadiusPx = 24;
        /// <summary>Screen margin of the bubble (UX §13.1 "Balon").</summary>
        public float MarginPx = 24;
        /// <summary>
        /// Atlas frame of the guide's portrait beside the bubble. D-101: Usta Kral replaces Usta Dede here — the king's bust frame is
        /// set by renaming this one string (the portrait is drawn aspect-preserved in a 128 px disc).
        /// </summary>
        public string PortraitFrame = "chr_dede_bust";
    }

    public enum PresencePhase
    {
        Wait,
        Shown,
        Hidden,
        Done,
    }

    public enum PresenceInput
    {
        Tick,
        TouchDown,
        TouchUp,
        CorrectAction,
        StepDone,
        WindowOpen,
        WindowClose,
    }

    /// <summary>What the view draws for a presence (UX §13.1 "Ne görünür").</summary>
    public readonly struct PresenceLook
    {
        /// <summary>The glow around the highlighted targets is on screen.</summary>
        public readonly bool Highlight;
        /// <summary>The bubble is on screen (shown and not past `ReshowsWithBubble` re-shows).</summary>
        public readonly bool Bubble;
        /// <summary>The bubble is at `DragFadeAlpha` (a touch is held).</summary>
        public readonly bool Faded;
        /// <summary>The glove is on screen (shown, no touch held; the view also needs its play condition).</summary>
        public readonly bool Glove;

        public PresenceLook(bool highlight, bool bubble, bool faded, bool glove)
        {
            Highlight = highlight;
            Bubble = bubble;
            Faded = faded;
            Glove = glove;
        }

        public bool Any => Highlight || Bubble || Glove;
    }

    public sealed class TutorialPresence
    {
        private readonly TutorialSettings s;
        private float since;
        private float idleSince;
        private float? pausedAt;

        /// <summary>A step started at `t`: it shows after `delayMs` (UX §13.1 "Bekleme").</summary>
        public TutorialPresence(TutorialSettings settings, float t, float delayMs)
        {
            s = settings ?? throw new ArgumentNullException(nameof(settings));
            Phase = PresencePhase.Wait;
            since = t;
            idleSince = t;
            DelayMs = delayMs;
        }

        public PresencePhase Phase { get; private set; }
        public float DelayMs { get; }
        public bool Touching { get; private set; }
        /// <summary>Open windows (and Söküm lines) now; &gt; 0 = paused.</summary>
        public int Pauses { get; private set; }
        /// <summary>Entries into shown so far (the first one included).</summary>
        public int Shows { get; private set; }
        public float? FirstShownAt { get; private set; }
        public float? DoneAt { get; private set; }
        /// <summary>When the phase was entered (shifted by the paused time).</summary>
        public float Since => since;

        public void Handle(PresenceInput kind, float t)
        {
            if (Phase == PresencePhase.Done) return;
            switch (kind)
            {
                case PresenceInput.StepDone:
                    Phase = PresencePhase.Done;
                    since = t;
                    DoneAt = t;
                    Touching = false;
                    return;
                case PresenceInput.WindowOpen:
                    // the input goes to the window: a touch held now never ends on the board
                    Pauses++;
                    pausedAt ??= t;
                    Touching = false;
                    return;
                case PresenceInput.WindowClose:
                    {
                        if (Pauses == 0) return;
                        if (Pauses > 1)
                        {
                            Pauses--;
                            return;
                        }
                        float shift = t - (pausedAt ?? t);
                        Pauses = 0;
                        pausedAt = null;
                        since += shift;
                        idleSince += shift;
                        return;
                    }
            }
            if (Pauses > 0) return; // timers stopped, nothing on screen
            switch (kind)
            {
                case PresenceInput.Tick:
                    if (Phase == PresencePhase.Wait)
                    {
                        if (t - since >= DelayMs) Show(t);
                    }
                    else if (Phase == PresencePhase.Shown)
                    {
                        if (!Touching && t - since >= s.VisibleMs) Hide(t);
                    }
                    else if (!Touching && t - idleSince >= s.IdleReshowMs) Show(t);
                    return;
                case PresenceInput.TouchDown:
                    Touching = true;
                    idleSince = t;
                    return;
                case PresenceInput.TouchUp:
                    Touching = false;
                    idleSince = t;
                    if (Phase == PresencePhase.Shown) Hide(t);
                    return;
                case PresenceInput.CorrectAction:
                    if (Phase == PresencePhase.Shown) Hide(t);
                    else idleSince = t;
                    return;
            }
        }

        private void Show(float t)
        {
            Phase = PresencePhase.Shown;
            since = t;
            Shows++;
            FirstShownAt ??= t;
        }

        private void Hide(float t)
        {
            Phase = PresencePhase.Hidden;
            since = t;
            idleSince = t;
        }

        public PresenceLook Look()
        {
            bool on = Phase == PresencePhase.Shown && Pauses == 0;
            return new PresenceLook(on, on && Shows - 1 <= s.ReshowsWithBubble, on && Touching, on && !Touching);
        }

        /// <summary>Analytics `tutorial_step` of an ended step: `shows` ≥ 1 (a step done before its first show counts one), ms from the first show to done.</summary>
        public void Report(float t, out int shows, out int msToDone)
        {
            float end = DoneAt ?? t;
            shows = Math.Max(1, Shows);
            msToDone = FirstShownAt == null ? 0 : (int)Math.Max(0, Math.Round(end - FirstShownAt.Value));
        }
    }
}
