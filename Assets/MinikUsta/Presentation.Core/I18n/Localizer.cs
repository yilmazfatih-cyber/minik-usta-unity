#nullable enable
// Languages of the Unity build (paket V1; CLAUDE.md "TR/EN eksiksiz"): the web repo's tr.json / en.json (Resources/i18n,
// copied by tools/sync-from-web.sh, never edited here) + the Unity-only overlay (Resources/i18n/unity_tr.json, unity_en.json:
// keys the web build has no screen for — the settings language row, "Hareketi azalt", "Oyna · Bölüm {n}"). One live
// `Strings` table: `SetLang` swaps its content, so the HUD, the windows and the level player switch at once. A missing key
// shows the key and is reported once (`Missing`, Unity: Debug.LogWarning). `I18nTests` scans the sources: every key the
// code uses exists in tr and en.
using System;
using MinikUsta.Presentation.Text;

namespace MinikUsta.Presentation.I18n
{
    public sealed class Localizer
    {
        public const string Tr = "tr";
        public const string En = "en";
        public static readonly string[] Langs = { Tr, En };

        private readonly Func<string, string?> read;

        /// <summary>`read(resource)` = the JSON text of `i18n/tr`, `i18n/unity_tr` … or null when missing.</summary>
        public Localizer(Func<string, string?> read, string lang = Tr)
        {
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            SetLang(lang);
        }

        /// <summary>The live table every screen reads.</summary>
        public Strings Text { get; } = new Strings();

        public string Lang { get; private set; } = Tr;

        public static bool IsLang(string? lang) => lang == Tr || lang == En;

        /// <summary>The table of `lang` (base + overlay), not the live one (tests, the key scan).</summary>
        public Strings Load(string lang)
        {
            var s = Strings.FromText(read("i18n/" + lang));
            s.Merge(read("i18n/unity_" + lang));
            s.Lang = lang;
            return s;
        }

        /// <summary>Switches the live table to `lang` (unknown → tr).</summary>
        public void SetLang(string lang)
        {
            if (!IsLang(lang)) lang = Tr;
            Lang = lang;
            Text.ReplaceWith(Load(lang));
        }
    }
}
