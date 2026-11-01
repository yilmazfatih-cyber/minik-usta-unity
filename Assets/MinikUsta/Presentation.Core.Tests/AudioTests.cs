#nullable enable
// Paket V2 sound: the ZzFX port against the web synthesizer (bit-parity vectors from tools/audio/gen-vectors.mjs), the
// resumable renderer, the sound bank and the playback policy (JUICE §0 rule 6: a name at most once per 60 ms, at most 4 voices,
// locked / muted requests dropped), the pitch maths of the juice rows and the sound set against the design tokens.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Presentation.Audio;
using MinikUsta.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class AudioTests
    {
        private static SoundSet? set;

        private static SoundSet Set => set ??= SoundSet.FromText(PresentationPaths.ResourceText("audio/sounds"));

        private static JsonValue Vectors => PresentationPaths.ReadJson(Path.Combine(PresentationPaths.GoldenDir, "audio_vectors.json"));

        public static IEnumerable<TestCaseData> VectorCases()
        {
            var v = Vectors["sounds"]!;
            for (int i = 0; i < v.Count; i++) yield return new TestCaseData(i).SetName("JUICE6_zzfx_matches_the_web_synthesizer_" + v[i]["name"]!.AsString + "_" + v[i]["rate"]!.AsInt);
        }

        [TestCaseSource(nameof(VectorCases))]
        public void JUICE6_zzfx_matches_the_web_synthesizer(int index)
        {
            var e = Vectors["sounds"]![index];
            string name = e["name"]!.AsString;
            int rate = e["rate"]!.AsInt;
            var recipe = Set.Find(name);
            Assert.IsNotNull(recipe, name);
            var s = RecipeRender.Render(recipe!, rate);
            Assert.AreEqual(e["length"]!.AsInt, s.Length, name + ": sample count");
            double peak = 0, sumAbs = 0;
            foreach (float x in s)
            {
                peak = Math.Max(peak, Math.Abs((double)x));
                sumAbs += Math.Abs((double)x);
            }
            bool chaotic = e["chaotic"]!.AsBool;
            // noise recipes (sin(t**3)): late samples may differ by an ulp of pow (measured worst: 7e-6 on sfx_fall, bit-identical
            // everywhere else); every other recipe is bit-identical to the web (hash below), points are checked at 1e-5
            double tolPoint = chaotic ? 5e-5 : 1e-5;
            Assert.AreEqual(e["peak"]!.AsDouble, peak, chaotic ? 5e-5 : 1e-5, name + ": peak");
            Assert.AreEqual(e["sumAbs"]!.AsDouble, sumAbs, Math.Max(1e-3, e["sumAbs"]!.AsDouble * (chaotic ? 1e-4 : 1e-6)), name + ": sum of |sample|");
            double worst = 0;
            foreach (var p in e["points"]!.Items)
            {
                int at = p[0].AsInt;
                double want = p[1].AsDouble;
                worst = Math.Max(worst, Math.Abs(want - s[at]));
                Assert.AreEqual(want, s[at], tolPoint, name + ": sample " + at);
            }
            TestContext.WriteLine(name + " @" + rate + " Hz: " + s.Length + " samples, worst sample-point deviation from the web " + worst.ToString("G3", System.Globalization.CultureInfo.InvariantCulture) + (chaotic ? " (noise recipe)" : ""));
            if (!chaotic) Assert.AreEqual(e["hash"]!.AsString, QuantisedHash(s), name + ": FNV-1a of the int16 buffer (worst point " + worst + ")");
        }

        private static string QuantisedHash(float[] samples)
        {
            uint h = 0x811c9dc5;
            foreach (float x in samples)
            {
                double c = Math.Max(-1, Math.Min(1, (double)x));
                int q = (int)Math.Floor(c * 32767 + 0.5); // JS Math.round: ties toward +infinity
                int v = q & 0xffff;
                h = unchecked((h ^ (uint)(v & 0xff)) * 0x01000193);
                h = unchecked((h ^ (uint)(v >> 8)) * 0x01000193);
            }
            return h.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void JUICE6_the_sound_set_is_the_design_tokens_audio_section_and_Sounds_names_all_of_it()
        {
            var tok = PresentationPaths.Token("audio");
            Assert.AreEqual(44100, Set.SampleRate, "the Unity AudioClip rate (the web build renders " + tok["sampleRateHz"]!.AsInt + ")");
            Assert.AreEqual(tok["comboRootHz"]!.AsDouble, Set.ComboRootHz);
            Assert.AreEqual(tok["maxVoices"]!.AsInt, Set.MaxVoices);
            Assert.AreEqual(tok["sameSoundCooldownMs"]!.AsDouble, Set.SameSoundCooldownMs, 1e-6);
            Assert.AreEqual(tok["comboSemitones"]!.Items.Select(x => x.AsInt).ToArray(), Set.ComboSemitones);
            int n = 0;
            foreach (var kv in tok["sfx"]!.Members)
            {
                if (kv.Key.StartsWith("_doc")) continue;
                var r = Set.Find(kv.Key);
                Assert.IsNotNull(r, kv.Key);
                Assert.IsFalse(r!.IsSequence, kv.Key);
                Assert.AreEqual(kv.Value.Items.Select(x => x.AsDouble).ToArray(), r.Params, kv.Key + " params");
                n++;
            }
            foreach (var kv in tok["seq"]!.Members)
            {
                if (kv.Key.StartsWith("_doc")) continue;
                var r = Set.Find(kv.Key);
                Assert.IsNotNull(r, kv.Key);
                Assert.IsTrue(r!.IsSequence, kv.Key);
                Assert.AreEqual(kv.Value.Count, r.Steps!.Length, kv.Key + " steps");
                for (int i = 0; i < r.Steps.Length; i++)
                {
                    Assert.AreEqual(kv.Value[i][0].AsDouble, r.Steps[i].StartMs, kv.Key + " step " + i + " start");
                    Assert.AreEqual(kv.Value[i][1].Items.Select(x => x.AsDouble).ToArray(), r.Steps[i].Params, kv.Key + " step " + i);
                }
                n++;
            }
            Assert.AreEqual(n, Set.Recipes.Count, "the file holds exactly the token sounds");
            CollectionAssert.AreEquivalent(Set.Recipes.Select(r => r.Name).ToArray(), Sounds.All, "Sounds.cs names the same sounds as the recipe file");
        }

        [Test]
        public void JUICE6_a_sequence_never_clips_and_every_sound_is_audible_and_finite()
        {
            foreach (var r in Set.Recipes)
            {
                var s = RecipeRender.Render(r, Set.SampleRate);
                Assert.Greater(s.Length, 0, r.Name);
                double peak = 0;
                foreach (float x in s)
                {
                    Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x), r.Name + ": finite");
                    peak = Math.Max(peak, Math.Abs((double)x));
                }
                Assert.Greater(peak, 0.01, r.Name + ": audible");
                if (r.IsSequence) Assert.LessOrEqual(peak, 1.0 + 1e-6, r.Name + ": mixed peak <= 1");
            }
        }

        [Test]
        public void JUICE6_rendering_in_slices_is_bit_identical_to_one_run()
        {
            foreach (string name in new[] { Sounds.Whoosh, Sounds.Blocked, Sounds.Coin, Sounds.PlaceBad, Sounds.Segment, Sounds.MusicWin, Sounds.Hammer })
            {
                var r = Set.Find(name)!;
                var whole = RecipeRender.Render(r, Set.SampleRate);
                var part = new RecipeRender(r, Set.SampleRate);
                int calls = 0;
                while (!part.Done && calls++ < 100000) part.Run(701);
                Assert.IsTrue(part.Done, name);
                CollectionAssert.AreEqual(whole, part.Samples, name + ": sliced == whole");
            }
        }

        [Test]
        public void JUICE6_zzfx_is_deterministic_and_the_delay_term_reads_float32_samples()
        {
            var p = new double[] { 0.5, 0, 440, 0.01, 0.05, 0.1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0.03 };
            var a = ZzFx.Build(p, 22050);
            var b = ZzFx.Build(p, 22050);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(ZzFx.Length(p, 22050), a.Length);
            Assert.AreEqual((int)(0.01 * 22050 + 0.0 + 0.05 * 22050 + 0.1 * 22050 + 0.03 * 22050), a.Length);
        }

        [Test]
        public void JUICE6_an_empty_or_zero_length_recipe_renders_nothing_and_does_not_throw()
        {
            Assert.AreEqual(9, ZzFx.Build(new double[] { 1, 0, 220, 0, 0, 0 }, 44100).Length, "attack 0 → the 9-sample anti-pop minimum, nothing else");
            var empty = SoundSet.FromText(null);
            Assert.AreEqual(0, empty.Recipes.Count);
            var player = new AudioPlayer(new SoundBank(empty), null);
            Assert.IsFalse(player.Play(Sounds.Pick), "an unknown / missing sound is dropped, never thrown");
            Assert.AreEqual(1, player.DroppedNotReady);
        }

        // --- bank and player --------------------------------------------------------------------------------------------------

        private sealed class Recorder : IAudioOutput
        {
            public readonly List<string> Prepared = new List<string>();
            public readonly List<(string Key, float Volume)> Played = new List<(string, float)>();
            public readonly Dictionary<string, int> Lengths = new Dictionary<string, int>();

            public void Prepare(string clipKey, float[] samples, int sampleRate)
            {
                Prepared.Add(clipKey);
                Lengths[clipKey] = samples.Length;
            }

            public void Play(string clipKey, float volume) => Played.Add((clipKey, volume));
        }

        private static (AudioPlayer Player, Recorder Out) Ready(bool locked = false)
        {
            var bank = new SoundBank(Set);
            bank.RequestAll();
            var rec = new Recorder();
            var player = new AudioPlayer(bank, rec, locked);
            for (int i = 0; i < 20000 && bank.Pending > 0; i++) player.Tick(16.7f);
            Assert.AreEqual(0, bank.Pending, "everything renders within the frame budget");
            for (int i = 0; i < 40; i++) player.Tick(16.7f);
            return (player, rec);
        }

        [Test]
        public void JUICE6_the_bank_renders_all_sounds_within_a_per_frame_sample_budget_and_hands_each_to_the_output_once()
        {
            var bank = new SoundBank(Set);
            bank.RequestAll();
            Assert.AreEqual(Sounds.All.Length, bank.Pending);
            var rec = new Recorder();
            var player = new AudioPlayer(bank, rec);
            player.Tick(16.7f);
            Assert.Less(bank.Pending, Sounds.All.Length, "the first frame already renders");
            Assert.Greater(bank.Pending, 0, "...but not everything: a frame has a sample budget");
            int frames = 1;
            while (bank.Pending > 0 && frames < 5000)
            {
                player.Tick(16.7f);
                frames++;
            }
            for (int i = 0; i < 40; i++) player.Tick(16.7f);
            Assert.Less(frames, 600, "all sounds are ready within ~10 s of frames");
            CollectionAssert.AreEquivalent(Sounds.All, rec.Prepared, "each sound is prepared exactly once");
            Assert.AreEqual(Sounds.All.Length, rec.Prepared.Distinct().Count());
        }

        [Test]
        public void JUICE6_a_locked_output_drops_requests_until_the_first_user_input_unlocks_it()
        {
            var (p, o) = Ready(locked: true);
            Assert.IsFalse(p.Unlocked);
            Assert.IsFalse(p.Play(Sounds.Pick));
            Assert.AreEqual(1, p.DroppedLocked);
            Assert.AreEqual(0, o.Played.Count, "never queued");
            p.Unlock();
            Assert.IsTrue(p.Play(Sounds.Pick));
            Assert.AreEqual(1, o.Played.Count);
            Assert.AreEqual(Sounds.Pick, o.Played[0].Key);
        }

        [Test]
        public void JUICE6_the_Ses_switch_silences_everything_music_included()
        {
            var (p, o) = Ready();
            p.SoundOn = false;
            Assert.IsFalse(p.Play(Sounds.Pick));
            Assert.IsFalse(p.Play(Sounds.MusicWin));
            Assert.AreEqual(2, p.DroppedMuted);
            p.SoundOn = true;
            p.MusicOn = false;
            Assert.IsFalse(p.Play(Sounds.MusicWin), "the music switch silences music_*");
            Assert.IsTrue(p.Play(Sounds.Pick), "...and only music");
            Assert.AreEqual(1, o.Played.Count);
        }

        [Test]
        public void JUICE6_the_same_sound_starts_at_most_once_per_60_ms()
        {
            var (p, o) = Ready();
            Assert.IsTrue(p.Play(Sounds.Tick));
            p.Tick(30);
            Assert.IsFalse(p.Play(Sounds.Tick), "30 ms later: still cooling down");
            Assert.AreEqual(1, p.DroppedCooldown);
            Assert.IsTrue(p.Play(Sounds.Pick), "another name is free");
            p.Tick(31);
            Assert.IsTrue(p.Play(Sounds.Tick), "61 ms after the first start");
            Assert.AreEqual(3, o.Played.Count);
        }

        [Test]
        public void JUICE6_at_most_4_sounds_sound_at_once_and_a_voice_frees_when_its_clip_ends()
        {
            var (p, o) = Ready();
            Assert.IsTrue(p.Play(Sounds.MusicWin));
            Assert.IsTrue(p.Play(Sounds.Segment));
            Assert.IsTrue(p.Play(Sounds.Fall));
            Assert.IsTrue(p.Play(Sounds.Offer));
            Assert.AreEqual(4, p.Voices);
            Assert.IsFalse(p.Play(Sounds.Pick), "the 5th is dropped");
            Assert.AreEqual(1, p.DroppedVoices);
            p.Tick(500); // sfx_offer (~120 ms) is over
            Assert.Less(p.Voices, 4);
            Assert.IsTrue(p.Play(Sounds.Pick));
            Assert.AreEqual(5, o.Played.Count);
        }

        [Test]
        public void JUICE6_a_pitched_play_is_a_resampled_clip_cached_per_rate_and_the_volume_is_the_gain()
        {
            var (p, o) = Ready();
            double up = SoundFeel.ComboRate(Set, 4);
            Assert.AreEqual(Math.Pow(2, 5 / 12.0), up, 1e-9, "the 4th streak step is +5 semitones (comboSemitones 0, 2, 4, 5, 7)");
            Assert.IsTrue(p.Play(Sounds.PlaceOk, up, 0.5));
            string key = o.Played[0].Key;
            StringAssert.StartsWith("sfx_place_ok@", key);
            Assert.AreEqual(0.5f, o.Played[0].Volume, 1e-6);
            int plain = o.Lengths[Sounds.PlaceOk];
            Assert.AreEqual((int)(plain / up), o.Lengths[key], "an octave-ish step is shorter by the rate");
            p.Tick(100);
            Assert.IsTrue(p.Play(Sounds.PlaceOk, up));
            Assert.AreEqual(1, o.Prepared.Count(k => k == key), "the variant is built once");
            Assert.IsTrue(p.Play(Sounds.StreakPip, 1));
            Assert.AreEqual(Sounds.StreakPip, o.Played.Last().Key, "rate 1 plays the plain clip");
        }

        [Test]
        public void JUICE6_pitch_and_gain_maths_of_the_juice_rows()
        {
            Assert.AreEqual(1.0, SoundFeel.ComboRate(Set, 1), 1e-12);
            Assert.AreEqual(Math.Pow(2, 2 / 12.0), SoundFeel.ComboRate(Set, 2), 1e-12);
            Assert.AreEqual(Math.Pow(2, 5 / 12.0), SoundFeel.ComboRate(Set, 4), 1e-12);
            Assert.AreEqual(Math.Pow(2, 7 / 12.0), SoundFeel.ComboRate(Set, 9), 1e-12, "the 5th step is the last one");
            Assert.AreEqual(1.0, SoundFeel.CoinRate(0), 1e-12);
            Assert.AreEqual(1.0, SoundFeel.CoinRate(2), 1e-12);
            Assert.AreEqual(Math.Pow(2, 1 / 12.0), SoundFeel.CoinRate(3), 1e-12, "+1 semitone every 3 coins");
            Assert.AreEqual(2.0, SoundFeel.CoinRate(99), 1e-12, "at most +12");
            Assert.AreEqual(1.0, SoundFeel.LandGain(0), 1e-12);
            Assert.AreEqual(Math.Pow(10, 4 / 20.0), SoundFeel.LandGain(8), 1e-12, "+4 dB from 8 rows");
            Assert.AreEqual(SoundFeel.LandGain(8), SoundFeel.LandGain(20), 1e-12);
            Assert.AreEqual(0.5011872336, SoundFeel.DbGain(-6), 1e-9);
            var s = new float[] { 0, 1, 0, -1, 0 };
            var o = SoundFeel.Resample(s, 2);
            Assert.AreEqual(2, o.Length);
            Assert.AreSame(s, SoundFeel.Resample(s, 1), "rate 1 is the buffer itself");
        }

        [Test]
        public void JUICE6_a_set_with_a_sound_in_both_sfx_and_seq_is_a_development_error()
        {
            const string both = "{\"sfx\":{\"a\":[1]},\"seq\":{\"a\":[[0,[1]]]}}";
            Assert.Throws<InvalidOperationException>(() => SoundSet.FromText(both));
        }
    }
}
