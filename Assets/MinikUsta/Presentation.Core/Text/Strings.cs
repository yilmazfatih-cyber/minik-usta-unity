#nullable enable
// Player-facing text of the Unity port (CLAUDE.md "Kodda sabit metin yok"): the web repo's src/i18n/tr.json / en.json,
// copied to Assets/MinikUsta/Resources/i18n by tools/sync-from-web.sh, read with the core JSON reader. `{name}` fields are
// filled by `Format`. A missing key returns the key itself (visible in the build, caught by StringsTests for every key the
// HUD uses) and is reported once to `Missing` (Unity: Debug.LogWarning). Paket V1: the table is live — `Localizer.SetLang`
// replaces its content in place, so every holder (LevelPlayer, the screens) switches language at once; Unity-only keys come
// from an overlay file merged on top of the web copy (Resources/i18n/unity_tr.json, unity_en.json).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Text
{
    public sealed class Strings
    {
        private readonly Dictionary<string, string> map = new Dictionary<string, string>();
        private readonly HashSet<string> reported = new HashSet<string>();

        /// <summary>Called once per missing key (Unity: Debug.LogWarning); the key itself is shown.</summary>
        public Action<string>? Missing { get; set; }

        /// <summary>The language of the table ("tr" / "en"); informational.</summary>
        public string Lang { get; set; } = "tr";

        /// <summary>The keys the Unity HUD and windows use (StringsTests: each exists in tr.json and en.json).</summary>
        public static readonly string[] UsedKeys =
        {
            Keys.LevelLabel, Keys.Moves, Keys.Blocks, Keys.WinTitle, Keys.WinBonus, Keys.WinClear, Keys.LoseTitle, Keys.LoseBlocksLeft,
            Keys.LoseRetry, Keys.PauseTitle, Keys.Restart, Keys.Continue, Keys.Count, Keys.Streak, Keys.Times,
        };

        public static class Keys
        {
            /// <summary>"Bölüm {n}".</summary>
            public const string LevelLabel = "home.play";
            public const string Moves = "hud.moves";
            public const string Blocks = "hud.blocks";
            public const string WinTitle = "win.title";
            public const string WinBonus = "win.bonus";
            public const string WinClear = "win.clear";
            public const string LoseTitle = "lose.title";
            public const string LoseBlocksLeft = "lose.blocksLeft";
            public const string LoseRetry = "lose.retry";
            public const string PauseTitle = "pause.title";
            /// <summary>"Bölümü baştan başlat".</summary>
            public const string Restart = "error.restart";
            /// <summary>"Devam" (next level after a win; close the pause window).</summary>
            public const string Continue = "common.continue";
            /// <summary>"{n}/{max}" (segment counter).</summary>
            public const string Count = "common.count";
            /// <summary>"Usta Serisi" (streak capsule label, UX §5.1).</summary>
            public const string Streak = "hud.streak";
            /// <summary>"×{n}" (Golden Trowels held).</summary>
            public const string Times = "common.times";
        }

        public static Strings Empty => new Strings();

        /// <summary>Flattens a tr.json / en.json object into dotted keys.</summary>
        public static Strings FromJson(JsonValue root)
        {
            var o = new Strings();
            o.Add("", root);
            return o;
        }

        public static Strings FromText(string? json) => json == null ? Empty : FromJson(JsonReader.Parse(json));

        /// <summary>Adds (or overrides) the keys of a JSON object (the Unity overlay).</summary>
        public void Merge(JsonValue root) => Add("", root);

        public void Merge(string? json)
        {
            if (json != null) Merge(JsonReader.Parse(json));
        }

        /// <summary>Replaces the whole table with `other`'s keys (language switch in place).</summary>
        public void ReplaceWith(Strings other)
        {
            map.Clear();
            foreach (var kv in other.map) map[kv.Key] = kv.Value;
            reported.Clear();
            Lang = other.Lang;
        }

        public IEnumerable<string> AllKeys => map.Keys;

        private void Add(string prefix, JsonValue v)
        {
            if (v.IsObject)
            {
                foreach (var kv in v.Members)
                    if (!kv.Key.StartsWith("_", StringComparison.Ordinal)) Add(prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key, kv.Value); // "_doc" fields are notes
            }
            else if (v.IsString) map[prefix] = v.AsString;
        }

        public int Count => map.Count;

        public bool Has(string key) => map.ContainsKey(key);

        public string Get(string key)
        {
            if (map.TryGetValue(key, out var v)) return v;
            if (reported.Add(key)) Missing?.Invoke(key);
            return key;
        }

        /// <summary>`Get(key)` with `{name}` → value (invariant number text).</summary>
        public string Format(string key, params (string Name, object Value)[] args)
        {
            string s = Get(key);
            foreach (var a in args)
            {
                string val = a.Value is int i ? i.ToString(CultureInfo.InvariantCulture) : a.Value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : a.Value?.ToString() ?? "";
                s = s.Replace("{" + a.Name + "}", val);
            }
            return s;
        }

        public string Format(string key, int n) => Format(key, ("n", n));
    }

    /// <summary>Turkish casing without a culture (CLAUDE.md: i → İ, ı → I; WebGL builds may run with the invariant culture).</summary>
    public static class TrText
    {
        public static string Upper(string s)
        {
            var b = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (ch == 'i') b.Append('İ');
                else if (ch == 'ı') b.Append('I');
                else b.Append(char.ToUpperInvariant(ch));
            }
            return b.ToString();
        }
    }
}
