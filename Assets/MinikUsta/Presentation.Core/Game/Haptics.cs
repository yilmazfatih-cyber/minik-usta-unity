#nullable enable
// Haptics (Faz 3 Q2 item 12; JUICE §0 rule 7): the juice rows name a strength (light / medium / strong / double light /
// success / victory pattern); the engine edge maps it to what the device can do. Unity's `Handheld.Vibrate()` buzzes one
// fixed pulse and only Android is used (the brief: "yalnız Android'de"), so `FixedPulseHaptics` turns the medium and
// stronger kinds into that one pulse and drops the light ones — a 400 ms buzz for every placement would be noise.
// `HapticRouter` is the policy in front of any sink: the player's vibration switch ("Titreşim", rule 7: reduced motion does
// NOT turn haptics off) and a minimum gap so one beat buzzes once.
using System;

namespace MinikUsta.Presentation.Game
{
    /// <summary>JUICE §0 rule 7 strengths, weakest first.</summary>
    public enum HapticKind
    {
        Light,
        DoubleLight,
        Medium,
        Heavy,
        Success,
        Win,
    }

    public interface IHapticSink
    {
        void Vibrate(HapticKind kind);
    }

    public sealed class HapticRouter
    {
        private readonly IHapticSink? sink;
        private float lastAt = float.NegativeInfinity;

        public HapticRouter(IHapticSink? sink)
        {
            this.sink = sink;
        }

        /// <summary>The "Titreşim" switch (default on).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Buzzes sent to the sink since the start (tests).</summary>
        public int Sent { get; private set; }

        /// <summary>Buzzes dropped by the switch or the gap (tests).</summary>
        public int Dropped { get; private set; }

        /// <summary>Asks for a buzz at clock time `nowMs`; false when it was dropped (off, no sink, or `minGapMs` after the last one).</summary>
        public bool Pulse(HapticKind kind, float nowMs, float minGapMs)
        {
            if (sink == null || !Enabled || nowMs - lastAt < minGapMs)
            {
                Dropped++;
                return false;
            }
            lastAt = nowMs;
            Sent++;
            sink.Vibrate(kind);
            return true;
        }

        /// <summary>A new attempt: the gap clock restarts.</summary>
        public void Reset() => lastAt = float.NegativeInfinity;
    }

    /// <summary>
    /// A device that can only buzz one fixed pulse (Unity `Handheld.Vibrate`, Android): kinds from `minimum` up buzz, weaker ones
    /// are dropped. `supported` false (every other platform) = never.
    /// </summary>
    public sealed class FixedPulseHaptics : IHapticSink
    {
        private readonly bool supported;
        private readonly Action vibrate;
        private readonly HapticKind minimum;

        public FixedPulseHaptics(bool supported, Action vibrate, HapticKind minimum = HapticKind.Medium)
        {
            this.supported = supported;
            this.vibrate = vibrate ?? throw new ArgumentNullException(nameof(vibrate));
            this.minimum = minimum;
        }

        public void Vibrate(HapticKind kind)
        {
            if (supported && kind >= minimum) vibrate();
        }
    }
}
