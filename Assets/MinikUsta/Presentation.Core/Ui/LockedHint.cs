#nullable enable
// LockedHint (paket U2; UX_UNITY §5.9, K6 "ölü dokunuş yok", P0-3): a tap on something that looks tappable but cannot act yet — a locked
// nav tab, the coin "+", the lives capsule, a locked or not-yet-playable booster slot — answers AT ONCE (the same frame, well inside the
// 100 ms of K6) with a small speech balloon 16 px above the target ("Yakında" / "{n}. bölümde açılır"), the lock icon shaking 3 times by
// ±6° (JUICE #73) and `sfx_locked` (Sounds.Locked; JUICE #8). The balloon stays 1200 ms; only one balloon at a time (a new tap replaces
// it). It lives on its own top layer (`UiLayer.Hint`), takes no tap and blocks nothing. Reduced motion: no shake.
//
// Pure: the clock is the sum of `Tick` frame times; the tests read the tree.
using System;
using MinikUsta.Presentation.Art;

namespace MinikUsta.Presentation.Ui
{
    public sealed class LockedHint
    {
        public const string RootId = "hint";
        public const string BalloonId = "hint.balloon";
        public const string TextId = "hint.text";
        public const string TailId = "hint.tail";

        /// <summary>UX_UNITY §5.9: balloon ≤ 520 × 104, 16 px from the target, shown 1200 ms; the lock shakes 3 × ±6°.</summary>
        public const double MaxW = 520;
        public const double BalloonH = 104;
        public const double GapPx = 16;
        public const double TextPx = 36;
        public const double PadX = 28;
        public const double EdgePx = 16;
        public const float ShowMs = 1200f;
        public const float ShakeMs = 360f;
        public const float ShakeDeg = 6f;
        public const int ShakeCount = 3;

        private readonly IUiSurface ui;
        private UiNode? lockNode;
        private float age;

        public LockedHint(IUiSurface ui)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        }

        /// <summary>Plays the hint sound (the app binds it to `sfx_locked`); null = silent.</summary>
        public Action? Sound { get; set; }

        /// <summary>Reduced motion (Settings): the lock does not shake.</summary>
        public bool Reduced { get; set; }

        /// <summary>The balloon on screen (null when hidden).</summary>
        public UiNode? Root { get; private set; }

        public bool Visible => Root != null;

        /// <summary>The text of the balloon on screen (null when hidden).</summary>
        public string? Text { get; private set; }

        /// <summary>The balloon rectangle of the last `Show` (design px).</summary>
        public PxRect Balloon { get; private set; }

        /// <summary>The target rectangle of the last `Show`.</summary>
        public PxRect Target { get; private set; }

        /// <summary>Hints shown since start (tests).</summary>
        public int Shows { get; private set; }

        /// <summary>The lock node's rotation now (tests; 0 when nothing shakes).</summary>
        public float LockRotation { get; private set; }

        /// <summary>
        /// Where the balloon goes for `target` on a W × H canvas: centred over it, its bottom `GapPx` above the target's top; below the target
        /// when there is no room above (the top bar); kept `EdgePx` inside the canvas sides.
        /// </summary>
        public static PxRect Place(PxRect target, double w, double canvasW, double canvasH)
        {
            double y = target.Y - GapPx - BalloonH;
            if (y < EdgePx) y = target.Bottom + GapPx;
            if (y + BalloonH > canvasH - EdgePx) y = canvasH - EdgePx - BalloonH;
            double x = target.CenterX - w / 2;
            x = Math.Max(EdgePx, Math.Min(canvasW - EdgePx - w, x));
            return new PxRect(x, y, w, BalloonH);
        }

        /// <summary>The balloon width for `text` (text + 2 × 28 px padding, ≤ 520).</summary>
        public static double WidthOf(string text) => Math.Min(MaxW, Math.Ceiling(FontMetrics.Width(text, TextPx)) + 2 * PadX + TextFit.Margin);

        /// <summary>Shows `text` over `target` (replacing a balloon on screen) and shakes `lockIcon` (the target's lock; null = none).</summary>
        public void Show(PxRect target, string text, UiNode? lockIcon, double canvasW, double canvasH)
        {
            StopShake();
            double w = WidthOf(text);
            var b = Place(target, w, canvasW, canvasH);
            bool below = b.Y > target.Y;
            var root = new UiNode(RootId, new PxRect(0, 0, canvasW, canvasH));
            root.Add(UiKit.Image(BalloonId, "bubble_speech", b, true));
            double tx = Math.Max(b.X + 24, Math.Min(b.Right - 88, target.CenterX - 32));
            var tail = UiKit.Image(TailId, "bubble_tail", below ? new PxRect(tx, b.Y - 40, 64, 56) : new PxRect(tx, b.Bottom - 16, 64, 56));
            if (below) tail.Rotation = 180f;
            root.Add(tail);
            root.Add(UiKit.Label(TextId, text, new PxRect(b.X + PadX - TextFit.Margin / 2, b.Y, b.W - 2 * PadX + TextFit.Margin, b.H), TextStyle.Card, TextPx));
            Root = root;
            Text = text;
            Balloon = b;
            Target = target;
            Shows++;
            age = 0f;
            lockNode = lockIcon;
            ui.Show(UiLayer.Hint, root, null);
            Sound?.Invoke();
            Tick(0f);
        }

        public void Tick(float dtMs)
        {
            if (Root == null) return;
            age += dtMs;
            if (lockNode != null)
            {
                float rot = 0f;
                if (!Reduced && age < ShakeMs) rot = ShakeDeg * (float)Math.Sin(2 * Math.PI * ShakeCount * age / ShakeMs);
                if (rot != LockRotation)
                {
                    LockRotation = rot;
                    ui.Target(lockNode).Rotation = rot;
                }
            }
            if (age >= ShowMs) Hide();
        }

        public void Hide()
        {
            if (Root == null) return;
            StopShake();
            Root = null;
            Text = null;
            ui.Show(UiLayer.Hint, null, null);
        }

        private void StopShake()
        {
            if (lockNode != null && LockRotation != 0f) ui.Target(lockNode).Rotation = 0f;
            LockRotation = 0f;
            lockNode = null;
        }
    }
}
