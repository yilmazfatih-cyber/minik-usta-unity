#nullable enable
// The sound player of the game without an engine (paket V2; web AudioService + SoundGate, JUICE §0 rule 6): the policy in front
// of the engine's output. Sounds are the pre-rendered buffers of `SoundBank`; a pitched play (combo / coin steps, JUICE #12,
// #15, #56) is the same buffer resampled by `rate` and cached per (sound, rate) — one AudioSource, one AudioClip per variant
// (Unity), no per-source pitch that would bend sounds already playing.
//
// Policy: a name starts at most once per `SameSoundCooldownMs`, at most `MaxVoices` sounds sound at once; a request while the
// output is LOCKED (WebGL: before the first user input), muted ("Ses" switch) or not rendered yet is DROPPED, never queued.
// The clock is the sum of `Tick` frame times, so the policy is deterministic (tests drive it with fixed frames).
using System;
using System.Collections.Generic;

namespace MinikUsta.Presentation.Audio
{
    /// <summary>The engine edge: Unity creates one AudioClip per key and plays it on one AudioSource.</summary>
    public interface IAudioOutput
    {
        /// <summary>A buffer is final: create the clip now (a frame of its own, never inside an input handler).</summary>
        void Prepare(string clipKey, float[] samples, int sampleRate);

        /// <summary>Plays the clip once at linear `volume` (the clip was prepared; an unknown key is ignored).</summary>
        void Play(string clipKey, float volume);
    }

    /// <summary>Sound maths of the juice rows (web catalog.ts `semitoneRate`, `dbGain`, `comboRate`, `landGain`, `coinRate`).</summary>
    public static class SoundFeel
    {
        /// <summary>JUICE #10: `sfx_fall` from this many rows.</summary>
        public const int FallSoundMinRows = 3;
        /// <summary>JUICE #11: `sfx_land` gets +0 … +4 dB over the fall distance (0 … 8 rows).</summary>
        public const double LandMaxDb = 4;
        public const double LandRowsForMaxDb = 8;
        /// <summary>JUICE #19: `sfx_land` of a delivered block, dB.</summary>
        public const double TruckLandDb = -6;
        /// <summary>JUICE #44: `sfx_land` of a yard cascade fall, dB.</summary>
        public const double CascadeLandDb = -8;
        /// <summary>JUICE #84: the two missing-support ticks, dB.</summary>
        public const double SupportTickDb = -8;
        /// <summary>JUICE #56: +1 semitone every 3 coins, at most +12.</summary>
        public const int BonusPitchEvery = 3;
        public const int BonusPitchMax = 12;

        /// <summary>Playback rate of a pitch step of `semitones` (TECH §11.6: pitch steps are played with a rate, not re-rendered).</summary>
        public static double SemitoneRate(double semitones) => Math.Pow(2, semitones / 12);

        /// <summary>Linear gain of `db` decibels.</summary>
        public static double DbGain(double db) => Math.Pow(10, db / 20);

        /// <summary>JUICE #12 / #15: the streak value a placement reached (1 … 4) → `comboSemitones` step.</summary>
        public static double ComboRate(SoundSet set, int reached)
        {
            var steps = set.ComboSemitones;
            int i = Math.Max(0, Math.Min(steps.Length - 1, reached - 1));
            return SemitoneRate(steps[i]);
        }

        /// <summary>JUICE #11: gain of `sfx_land` over the fall distance.</summary>
        public static double LandGain(int rows)
        {
            double k = Math.Max(0, Math.Min(1, rows / LandRowsForMaxDb));
            return DbGain(k * LandMaxDb);
        }

        /// <summary>JUICE #56: pitch of the n-th coin (0-based).</summary>
        public static double CoinRate(int n) => SemitoneRate(Math.Min(BonusPitchMax, n / BonusPitchEvery));

        /// <summary>Linear-interpolation resample by `rate` (rate 2 = an octave up, half as long). Pure; rate 1 returns the input.</summary>
        public static float[] Resample(float[] src, double rate)
        {
            if (rate == 1 || src.Length == 0 || !(rate > 0)) return src;
            int n = Math.Max(1, (int)(src.Length / rate));
            var o = new float[n];
            for (int j = 0; j < n; j++)
            {
                double pos = j * rate;
                int a = (int)pos;
                double frac = pos - a;
                float x = src[Math.Min(a, src.Length - 1)];
                float y = src[Math.Min(a + 1, src.Length - 1)];
                o[j] = (float)(x + (y - x) * frac);
            }
            return o;
        }
    }

    /// <summary>One `Play` call as the player saw it (tests, the debug overlay): what was asked and whether it sounded.</summary>
    public readonly struct SoundRequest
    {
        public readonly string Name;
        public readonly double Rate;
        public readonly double Gain;
        public readonly float At;
        public readonly bool Played;

        public SoundRequest(string name, double rate, double gain, float at, bool played)
        {
            Name = name;
            Rate = rate;
            Gain = gain;
            At = at;
            Played = played;
        }

        public override string ToString() => Name + (Rate != 1 ? " ×" + Rate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "") + (Played ? "" : " (dropped)");
    }

    public sealed class AudioPlayer
    {
        /// <summary>Samples rendered per frame while sounds are pending (~4 ms of the slowest recipes at 44.1 kHz).</summary>
        public const int PumpBudgetSamples = 16384;
        /// <summary>Finished buffers handed to the output per frame.</summary>
        public const int PreparePerFrame = 2;

        private readonly SoundBank bank;
        private readonly IAudioOutput? output;
        private readonly bool needsUnlock;
        private readonly Dictionary<string, float> lastStart = new Dictionary<string, float>();
        private readonly List<float> voiceEnds = new List<float>();
        private readonly Dictionary<string, float[]> variants = new Dictionary<string, float[]>();
        private readonly HashSet<string> prepared = new HashSet<string>();
        private float now;
        private bool unlocked;

        public AudioPlayer(SoundBank bank, IAudioOutput? output, bool needsUnlock = false)
        {
            this.bank = bank ?? throw new ArgumentNullException(nameof(bank));
            this.output = output;
            this.needsUnlock = needsUnlock;
            unlocked = !needsUnlock;
        }

        public SoundBank Bank => bank;

        /// <summary>The "Ses" switch (settings): off silences every sound, music included.</summary>
        public bool SoundOn { get; set; } = true;

        /// <summary>Music sounds (`music_*`) follow this switch and `SoundOn` (the Unity settings have no separate music row yet).</summary>
        public bool MusicOn { get; set; } = true;

        /// <summary>The output accepts sounds (always on Android; WebGL after the first user input).</summary>
        public bool Unlocked => unlocked;

        /// <summary>Clock of the player (ms): the sum of the frame times `Tick` got.</summary>
        public float Now => now;

        /// <summary>Keeps every `Play` call in `Requests` (tests, the dev overlay); off in the game.</summary>
        public bool RecordRequests { get; set; }

        public List<SoundRequest> Requests { get; } = new List<SoundRequest>();

        public int Started { get; private set; }
        public int DroppedLocked { get; private set; }
        public int DroppedMuted { get; private set; }
        public int DroppedNotReady { get; private set; }
        public int DroppedCooldown { get; private set; }
        public int DroppedVoices { get; private set; }

        /// <summary>Sounds sounding now (voices not yet ended).</summary>
        public int Voices => voiceEnds.Count;

        public static bool IsMusic(string name) => name.StartsWith("music_", StringComparison.Ordinal);

        /// <summary>Call from the first user input (WebGL: the browser keeps audio locked until then).</summary>
        public void Unlock() => unlocked = true;

        /// <summary>One frame: advance the clock, free ended voices, render pending sounds within the sample budget, hand finished ones to the output.</summary>
        public void Tick(float dtMs)
        {
            if (dtMs > 0) now += dtMs;
            for (int i = voiceEnds.Count - 1; i >= 0; i--)
                if (voiceEnds[i] <= now) voiceEnds.RemoveAt(i);
            if (bank.Pending > 0) bank.Pump(PumpBudgetSamples);
            for (int k = 0; k < PreparePerFrame; k++)
            {
                string? name = bank.TakeFinished();
                if (name == null) break;
                var samples = bank.Get(name);
                if (samples != null && output != null && prepared.Add(name)) output.Prepare(name, samples, bank.SampleRate);
            }
        }

        /// <summary>
        /// Plays `name` at playback `rate` (pitch step) and linear `gain`. False when dropped (locked, muted, not rendered, cooldown,
        /// voice cap) — the call is cheap and never throws for an unknown name (a missing sound must not stop the game).
        /// </summary>
        public bool Play(string name, double rate = 1, double gain = 1)
        {
            bool ok = PlayCore(name, rate, gain);
            if (RecordRequests) Requests.Add(new SoundRequest(name, rate, gain, now, ok));
            return ok;
        }

        private bool PlayCore(string name, double rate, double gain)
        {
            if (!unlocked)
            {
                DroppedLocked++;
                return false;
            }
            if (!SoundOn || (IsMusic(name) && !MusicOn))
            {
                DroppedMuted++;
                return false;
            }
            var samples = bank.Get(name);
            if (samples == null)
            {
                DroppedNotReady++;
                return false;
            }
            if (lastStart.TryGetValue(name, out float last) && now - last < bank.Set.SameSoundCooldownMs)
            {
                DroppedCooldown++;
                return false;
            }
            if (voiceEnds.Count >= bank.Set.MaxVoices)
            {
                DroppedVoices++;
                return false;
            }
            string key = name;
            float[] clip = samples;
            if (rate != 1 && rate > 0)
            {
                key = name + "@" + ((int)Math.Round(rate * 1000)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!variants.TryGetValue(key, out var v))
                {
                    v = SoundFeel.Resample(samples, rate);
                    variants[key] = v;
                }
                clip = v;
            }
            lastStart[name] = now;
            voiceEnds.Add(now + clip.Length * 1000f / bank.SampleRate);
            Started++;
            if (output != null)
            {
                if (prepared.Add(key)) output.Prepare(key, clip, bank.SampleRate);
                output.Play(key, (float)gain);
            }
            return true;
        }
    }
}
