#nullable enable
// The stuck pulse ("takılma nabzı"; D-091: the match-3 "hint" counterpart). Port of web src/scenes/level/stuckPulse.ts.
// GDD K-34 hook 5: after 6000 ms (easy) / 12000 ms (normal) without a touch the `neededNow` blocks (TurnSummary.NeededNow)
// pulse twice — once per idle period; any touch starts a new period. Hard and super hard levels have no pulse; nor does a
// level while a tutorial step is active (K-53 item 4), a window is open or the board is busy (`quiet`). These idle times
// are RULE values (product-lead), not feel settings; the pulse look is `FeelSettings.HintPulseMs` / `HintPulsePeak`.
// Real time lives in the caller: it passes its clock (ms).
namespace MinikUsta.Presentation.Feel
{
    public sealed class StuckPulse
    {
        /// <summary>GDD K-34 hook 5 idle time on an easy level (ms).</summary>
        public const float EasyIdleMs = 6000;
        /// <summary>GDD K-34 hook 5 idle time on a normal level (ms).</summary>
        public const float NormalIdleMs = 12000;
        /// <summary>GDD K-34 hook 5: "2 kez nabız atar".</summary>
        public const int Pulses = 2;

        private float? idleMs;
        private float since;
        private bool fired;

        /// <summary>Idle time before the pulse on a level of `difficulty` (`CompiledLevel.Difficulty`); null = no pulse.</summary>
        public static float? IdleMsFor(string? difficulty)
        {
            if (difficulty == "easy") return EasyIdleMs;
            if (difficulty == "normal") return NormalIdleMs;
            return null;
        }

        /// <summary>The idle time of the current level; null = no pulse.</summary>
        public float? IdleMs => idleMs;

        /// <summary>A new level (or attempt): the period starts now.</summary>
        public void Level(string? difficulty, float now)
        {
            idleMs = IdleMsFor(difficulty);
            since = now;
            fired = false;
        }

        /// <summary>Any touch: a new idle period.</summary>
        public void Touched(float now)
        {
            since = now;
            fired = false;
        }

        /// <summary>
        /// True once per idle period when the pulse is due now. `quiet` = the pulse may not show (tutorial step active, window
        /// open, drag or animations running): the period's clock keeps running and the pulse comes when it is allowed again.
        /// </summary>
        public bool Due(float now, bool quiet)
        {
            if (!idleMs.HasValue || fired || quiet) return false;
            if (now - since < idleMs.Value) return false;
            fired = true;
            return true;
        }
    }
}
