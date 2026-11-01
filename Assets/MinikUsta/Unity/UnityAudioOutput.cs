#nullable enable
// The engine edge of the sounds (paket V2): one AudioSource on the game object, one AudioClip per prepared buffer
// (`AudioClip.Create` + `SetData`, mono at the bank's sample rate), played with `PlayOneShot(clip, volume)`. Pitched variants are
// separate clips (`AudioPlayer` resamples them), so no per-source pitch bends sounds that are still playing. All policy — mute,
// locked WebGL output, cooldown, voice cap — is the engine-independent `AudioPlayer`; this class only owns Unity objects.
using System.Collections.Generic;
using MinikUsta.Presentation.Audio;
using UnityEngine;

namespace MinikUsta.Unity
{
    public sealed class UnityAudioOutput : IAudioOutput
    {
        private readonly AudioSource source;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public UnityAudioOutput(GameObject host, Camera? listenerOn)
        {
            // Level.unity carries the AudioListener on its Main Camera; a camera made in code (Awake) gets one here
            if (listenerOn != null && listenerOn.GetComponent<AudioListener>() == null) listenerOn.gameObject.AddComponent<AudioListener>();
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        public void Prepare(string clipKey, float[] samples, int sampleRate)
        {
            if (samples.Length == 0 || clips.ContainsKey(clipKey)) return;
            var clip = AudioClip.Create(clipKey, samples.Length, 1, sampleRate, false);
            if (clip == null) return;
            clip.SetData(samples, 0);
            clips[clipKey] = clip;
        }

        public void Play(string clipKey, float volume)
        {
            // PlayOneShot's volume scale is a 0 … 1 factor: the +4 dB landing accent (> 1) is capped, never distorted
            if (clips.TryGetValue(clipKey, out var clip)) source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
