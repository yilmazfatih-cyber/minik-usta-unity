#nullable enable
// Procedural sounds (paket V2; web src/services/audio.ts, TECH_DESIGN §11.6, decision D-057): recipes come only from the
// design tokens (`audio.sfx.<name>` = one ZzFX call, `audio.seq.<name>` = `[startMs, params]` steps mixed into one buffer),
// copied into Resources/audio/sounds.json by tools/audio/gen-sounds.mjs (tools/sync-from-web.sh). Rendering is pure
// (`RecipeRender`, `SoundBank`) at `sampleRateHz` mono and sliced over frames by a SAMPLE budget (not a clock: the schedule
// is deterministic and testable). Nothing here touches the engine; the Unity layer only turns a finished buffer into an
// AudioClip (`IAudioOutput`).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Audio
{
    /// <summary>One `[startMs, params]` step of a sequence.</summary>
    public sealed class SoundStep
    {
        public readonly double StartMs;
        public readonly double[] Params;

        public SoundStep(double startMs, double[] p)
        {
            StartMs = startMs;
            Params = p;
        }
    }

    /// <summary>A sound recipe: a single ZzFX call (`Steps` null) or a mixed sequence (`Params` null).</summary>
    public sealed class SoundRecipe
    {
        public readonly string Name;
        public readonly double[]? Params;
        public readonly SoundStep[]? Steps;

        public SoundRecipe(string name, double[]? p, SoundStep[]? steps)
        {
            Name = name;
            Params = p;
            Steps = steps;
        }

        public bool IsSequence => Steps != null;
    }

    /// <summary>Every sound of the game plus the playback policy numbers of the tokens (`audio.*`).</summary>
    public sealed class SoundSet
    {
        public int SampleRate = 44100;
        public double ComboRootHz = 660;
        public int[] ComboSemitones = { 0, 2, 4, 5, 7 };
        public float SameSoundCooldownMs = 60;
        public int MaxVoices = 4;
        public readonly List<SoundRecipe> Recipes = new List<SoundRecipe>();
        private readonly Dictionary<string, SoundRecipe> byName = new Dictionary<string, SoundRecipe>();

        public bool Has(string name) => byName.ContainsKey(name);

        public SoundRecipe? Find(string name) => byName.TryGetValue(name, out var r) ? r : null;

        public void Add(SoundRecipe r)
        {
            if (byName.ContainsKey(r.Name)) throw new InvalidOperationException("audio: sound " + r.Name + " is defined twice (audio.sfx and audio.seq must be disjoint)");
            byName[r.Name] = r;
            Recipes.Add(r);
        }

        /// <summary>Reads Resources/audio/sounds.json; a missing text gives an empty set (the game stays silent, never crashes).</summary>
        public static SoundSet FromText(string? json)
        {
            var set = new SoundSet();
            if (json == null) return set;
            var root = JsonReader.Parse(json);
            if (!root.IsObject) return set;
            set.SampleRate = Int(root, "sampleRateHz", set.SampleRate);
            set.ComboRootHz = Num(root, "comboRootHz", set.ComboRootHz);
            set.SameSoundCooldownMs = (float)Num(root, "sameSoundCooldownMs", set.SameSoundCooldownMs);
            set.MaxVoices = Int(root, "maxVoices", set.MaxVoices);
            var semis = root["comboSemitones"];
            if (semis != null && semis.IsArray && semis.Count > 0)
            {
                var l = new List<int>();
                foreach (var s in semis.Items) if (s.IsInteger) l.Add(s.AsInt);
                if (l.Count > 0) set.ComboSemitones = l.ToArray();
            }
            var sfx = root["sfx"];
            if (sfx != null && sfx.IsObject)
                foreach (var kv in sfx.Members)
                    if (!kv.Key.StartsWith("_doc", StringComparison.Ordinal)) set.Add(new SoundRecipe(kv.Key, Params(kv.Value), null));
            var seq = root["seq"];
            if (seq != null && seq.IsObject)
                foreach (var kv in seq.Members)
                {
                    if (kv.Key.StartsWith("_doc", StringComparison.Ordinal) || !kv.Value.IsArray) continue;
                    var steps = new List<SoundStep>();
                    foreach (var st in kv.Value.Items)
                        if (st.IsArray && st.Count == 2 && st[0].IsNumber) steps.Add(new SoundStep(st[0].AsDouble, Params(st[1])));
                    set.Add(new SoundRecipe(kv.Key, null, steps.ToArray()));
                }
            return set;
        }

        private static double[] Params(JsonValue v)
        {
            var l = new List<double>();
            if (v.IsArray) foreach (var x in v.Items) l.Add(x.IsNumber ? x.AsDouble : 0);
            return l.ToArray();
        }

        private static int Int(JsonValue o, string key, int fallback)
        {
            var v = o[key];
            return v != null && v.IsInteger ? v.AsInt : fallback;
        }

        private static double Num(JsonValue o, string key, double fallback)
        {
            var v = o[key];
            return v != null && v.IsNumber ? v.AsDouble : fallback;
        }
    }

    /// <summary>
    /// One recipe rendered in resumable slices (`Run(n)`: at most n sample units per call; `Done` → `Samples`).
    ///  - sfx: one `ZzFxRender`.
    ///  - seq: each step is rendered with its own `ZzFxRender` (created when the step starts) and added at the sample offset
    ///    `round(startMs · rate / 1000)`, in step order (the float32 sums equal mixing whole steps); the length is the latest
    ///    step end; then a peak pass, and if the summed peak exceeds 1 a scale pass so the peak is exactly 1 (no clipping).
    /// </summary>
    public sealed class RecipeRender
    {
        public readonly float[] Samples;
        private readonly int sampleRate;
        private readonly SoundStep[]? steps;
        private readonly int[] offsets;
        private readonly bool mix;
        private ZzFxRender? synth;
        private int step;
        private int peakAt;
        private double peak;
        private int scaleAt;

        public RecipeRender(SoundRecipe recipe, int sampleRate)
        {
            this.sampleRate = sampleRate;
            if (!recipe.IsSequence)
            {
                synth = new ZzFxRender(recipe.Params ?? new double[0], sampleRate);
                steps = null;
                offsets = new int[0];
                Samples = synth.Samples;
                mix = false;
                return;
            }
            steps = recipe.Steps!;
            offsets = new int[steps.Length];
            int length = 0;
            for (int k = 0; k < steps.Length; k++)
            {
                offsets[k] = (int)ZzFx.JsRound(steps[k].StartMs * sampleRate / 1000);
                length = Math.Max(length, offsets[k] + ZzFx.Length(steps[k].Params, sampleRate));
            }
            Samples = new float[length];
            mix = true;
        }

        public bool Done => !mix ? synth == null || synth.Done : step >= steps!.Length && scaleAt >= Samples.Length;

        /// <summary>Processes up to `maxUnits` samples (render + mix, then peak, then scale); returns the units used.</summary>
        public int Run(int maxUnits)
        {
            if (!mix) return synth?.Run(maxUnits) ?? 0;
            var o = Samples;
            var sts = steps!;
            long left = Math.Max(0, maxUnits);
            while (left > 0 && step < sts.Length)
            {
                var st = sts[step];
                var z = synth ??= new ZzFxRender(st.Params, sampleRate);
                int offset = offsets[step];
                int from = z.Rendered;
                int n = z.Run((int)Math.Min(left, int.MaxValue));
                var src = z.Samples;
                for (int i = from; i < from + n; i++) o[offset + i] = (float)((double)o[offset + i] + src[i]);
                left -= n;
                if (z.Done)
                {
                    step++;
                    synth = null;
                }
                else if (n == 0) break;
            }
            long used = maxUnits - left;
            if (step < sts.Length) return (int)used;
            if (peakAt < o.Length && left > 0)
            {
                int stop = (int)Math.Min(o.Length, peakAt + left);
                double pk = peak;
                for (int i = peakAt; i < stop; i++) pk = Math.Max(pk, Math.Abs((double)o[i]));
                peak = pk;
                left -= stop - peakAt;
                peakAt = stop;
            }
            if (peakAt < o.Length) return (int)(maxUnits - left);
            if (peak <= 1) scaleAt = o.Length;
            else if (scaleAt < o.Length && left > 0)
            {
                double k = 1 / peak;
                int stop = (int)Math.Min(o.Length, scaleAt + left);
                for (int i = scaleAt; i < stop; i++) o[i] = (float)((double)o[i] * k);
                left -= stop - scaleAt;
                scaleAt = stop;
            }
            return (int)(maxUnits - left);
        }

        /// <summary>One run to the end.</summary>
        public static float[] Render(SoundRecipe recipe, int sampleRate)
        {
            var r = new RecipeRender(recipe, sampleRate);
            r.Run(int.MaxValue);
            return r.Samples;
        }
    }

    /// <summary>
    /// Pre-rendered sample buffers, filled in budgeted slices: `Pump` renders chunks of the queued sounds (request order) until
    /// its sample budget is used (at least one chunk per call) and resumes the unfinished sound on the next call.
    /// </summary>
    public sealed class SoundBank
    {
        /// <summary>Samples per render slice (a `Pump` overshoots its budget by at most one chunk).</summary>
        public const int RenderChunkSamples = 2048;

        private readonly SoundSet set;
        private readonly Queue<string> queue = new Queue<string>();
        private readonly Dictionary<string, float[]> ready = new Dictionary<string, float[]>();
        private readonly Queue<string> finished = new Queue<string>();
        private string? jobName;
        private RecipeRender? job;

        public SoundBank(SoundSet set)
        {
            this.set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public SoundSet Set => set;

        public int SampleRate => set.SampleRate;

        /// <summary>Queues names for rendering (rendered, in-progress and queued names are skipped; an unknown name throws: a development error).</summary>
        public void Request(IEnumerable<string> names)
        {
            foreach (string n in names)
            {
                if (!set.Has(n)) throw new ArgumentException("audio: unknown sound \"" + n + "\"");
                if (ready.ContainsKey(n) || jobName == n || queue.Contains(n)) continue;
                queue.Enqueue(n);
            }
        }

        public void RequestAll()
        {
            var all = new List<string>();
            foreach (var r in set.Recipes) all.Add(r.Name);
            Request(all);
        }

        /// <summary>Sounds not rendered yet (queued + the one in progress).</summary>
        public int Pending => queue.Count + (job == null ? 0 : 1);

        /// <summary>Renders queued sounds until `budgetSamples` are used; returns the sounds still pending.</summary>
        public int Pump(int budgetSamples)
        {
            long used = 0;
            do
            {
                if (job == null)
                {
                    if (queue.Count == 0) break;
                    jobName = queue.Dequeue();
                    job = new RecipeRender(set.Find(jobName)!, set.SampleRate);
                }
                used += Math.Max(1, job.Run(RenderChunkSamples));
                if (job.Done)
                {
                    ready[jobName!] = job.Samples;
                    finished.Enqueue(jobName!);
                    job = null;
                    jobName = null;
                }
            } while (Pending > 0 && used < budgetSamples);
            return Pending;
        }

        public float[]? Get(string name) => ready.TryGetValue(name, out var s) ? s : null;

        public bool IsReady(string name) => ready.ContainsKey(name);

        /// <summary>The oldest sound rendered since the last call (null: none): the output turns it into a clip off the input path.</summary>
        public string? TakeFinished() => finished.Count > 0 ? finished.Dequeue() : null;
    }
}
