#nullable enable
// Paket V1: languages (CLAUDE.md "Oyun içi tüm metinler i18n anahtarıyla, TR/EN eksiksiz"; "Türkçe karakterler fontta doğru").
// The live table switches in place, `{name}` fields fill in, a missing key shows itself and is reported once; a scan of the
// sources proves every key the code asks for exists in tr AND en; the Baloo 2 TTF draws every character of both tables.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MinikUsta.Presentation.I18n;
using MinikUsta.Presentation.Meta;
using MinikUsta.Presentation.Tests.Fixtures;
using MinikUsta.Presentation.Text;
using NUnit.Framework;

namespace MinikUsta.Presentation.Tests
{
    public class I18nTests
    {
        private static Localizer New(string lang = "tr") => new Localizer(PresentationPaths.ResourceText, lang);

        [Test]
        public void I18N_tr_en_switch_in_place_with_placeholders_and_the_unity_overlay()
        {
            var l = New();
            var table = l.Text;
            Assert.AreEqual("Bölüm 3", table.Format("home.play", 3));
            Assert.AreEqual("Oyna · Bölüm 1", table.Format("home.playLevel", 1), "Unity overlay key");
            Assert.AreEqual("Hareketi azalt", table.Get("settings.reduceMotion"));
            l.SetLang("en");
            Assert.AreSame(table, l.Text, "one live table: holders switch at once");
            Assert.AreEqual("Play · Level 1", table.Format("home.playLevel", 1));
            Assert.AreEqual("Level 3", table.Format("home.play", 3));
            Assert.AreEqual("Offer 2/3", table.Format("lose.offer.count", ("n", 2), ("max", 3)));
            l.SetLang("xx");
            Assert.AreEqual("tr", l.Lang, "an unknown language is Turkish");
            Assert.IsFalse(table.Has("_doc"), "overlay notes are not keys");
        }

        [Test]
        public void I18N_a_missing_key_shows_the_key_and_is_reported_once()
        {
            var l = New();
            var seen = new List<string>();
            l.Text.Missing = k => seen.Add(k);
            Assert.AreEqual("no.such.key", l.Text.Get("no.such.key"));
            Assert.AreEqual("no.such.key", l.Text.Get("no.such.key"));
            Assert.AreEqual(new[] { "no.such.key" }, seen.ToArray());
        }

        [Test]
        public void I18N_TR_upper_case_i_dotted()
        {
            Assert.AreEqual("HAMLE", TrText.Upper("Hamle"));
            Assert.AreEqual("İPUCU", TrText.Upper("ipucu"));
            Assert.AreEqual("IŞIK", TrText.Upper("ışık"));
        }

        private static IEnumerable<string> SourceFiles()
        {
            foreach (var dir in new[] { "Presentation.Core", "Unity" })
                foreach (var f in Directory.EnumerateFiles(Path.Combine(PresentationPaths.RepoRoot, "Assets", "MinikUsta", dir), "*.cs", SearchOption.AllDirectories))
                    yield return f;
        }

        /// <summary>Every key the code asks for: `Get("k")` / `Format("k"…)` literals, `Strings.Keys` constants and the keys meta returns.</summary>
        public static SortedSet<string> UsedKeys()
        {
            var keys = new SortedSet<string>();
            var call = new Regex("\\b(?:Get|Format)\\(\\s*\"([a-zA-Z][a-zA-Z0-9]*(?:\\.[a-zA-Z0-9_]+)+)\"");
            foreach (var f in SourceFiles())
                foreach (Match m in call.Matches(File.ReadAllText(f))) keys.Add(m.Groups[1].Value);
            foreach (var fi in typeof(Strings.Keys).GetFields()) keys.Add((string)fi.GetRawConstantValue()!);
            // keys handed out as values (offer counter / ad line, difficulty pill)
            keys.Add(Offers.CountKey);
            keys.Add(Offers.LastKey);
            foreach (var k in new[] { "lose.adToday", "ads.none", "ads.tomorrow" }) keys.Add(k);
            foreach (var d in new[] { "hard", "superhard" }) keys.Add("difficulty." + d);
            return keys;
        }

        [Test]
        public void I18N_every_key_the_code_uses_exists_in_tr_and_en()
        {
            var tr = New("tr").Load("tr");
            var en = New("en").Load("en");
            var keys = UsedKeys();
            Assert.Greater(keys.Count, 40, "the scan found the keys");
            var missing = keys.Where(k => !tr.Has(k) || !en.Has(k)).ToArray();
            Assert.IsEmpty(missing, "keys missing in tr or en: " + string.Join(", ", missing));
            foreach (var k in tr.AllKeys) Assert.IsTrue(en.Has(k), "en lacks " + k);
            foreach (var k in en.AllKeys) Assert.IsTrue(tr.Has(k), "tr lacks " + k);
        }

        [Test]
        public void I18N_no_player_text_literal_in_the_screens()
        {
            // a quoted Turkish / English word passed as a UiNode text would bypass i18n: the screens only pass table values
            var suspicious = new Regex("UiKit\\.Label\\(\\s*\"[^\"]+\",\\s*\"[^\"]*[a-zA-ZçğıöşüÇĞİÖŞÜ]{3,}");
            foreach (var f in SourceFiles())
            {
                var m = suspicious.Match(File.ReadAllText(f));
                Assert.IsFalse(m.Success, Path.GetFileName(f) + ": " + m.Value);
            }
        }

        [Test]
        public void I18N_font_metrics_table_is_the_ttf_hmtx_and_text_fit_keeps_one_line()
        {
            string ttf = Path.Combine(PresentationPaths.ResourcesDir, "fonts", "Baloo2-ExtraBold.ttf");
            var adv = TrueTypeCmap.Advances(ttf);
            Assert.Greater(adv.Count, 400);
            foreach (var kv in adv) Assert.AreEqual(kv.Value, Ui.FontMetrics.Advance(kv.Key), "U+" + kv.Key.ToString("X4") + " (re-run tools/font/gen-metrics.py)");
            Assert.AreEqual(60, Ui.FontMetrics.FittedSize("Devam", 60, 1000), "fits: the designed size");
            int s = Ui.FontMetrics.FittedSize("Hayır, teşekkürler", 54, 300);
            Assert.Less(s, 54);
            Assert.LessOrEqual(Ui.FontMetrics.Width("Hayır, teşekkürler", s), 300);
            Assert.AreEqual(27, Ui.FontMetrics.FittedSize(new string('W', 200), 54, 100), "never below half the size");
        }

        [Test]
        public void I18N_baloo2_extrabold_draws_every_character_of_tr_and_en_including_s_g_dotless_i_dotted_I_o_u_c()
        {
            string ttf = Path.Combine(PresentationPaths.ResourcesDir, "fonts", "Baloo2-ExtraBold.ttf");
            Assert.IsTrue(File.Exists(ttf), ttf);
            Assert.IsTrue(File.Exists(Path.Combine(PresentationPaths.ResourcesDir, "fonts", "OFL.txt")), "the OFL ships with the font");
            var cmap = TrueTypeCmap.CodePoints(ttf);
            foreach (char c in "şğıİöüçŞĞÖÜÇ×·≈₺") Assert.IsTrue(cmap.Contains(c), "glyph " + c);
            var chars = new SortedSet<char>();
            foreach (var lang in Localizer.Langs)
            {
                var s = New(lang).Load(lang);
                foreach (var k in s.AllKeys) foreach (char c in s.Get(k)) if (!char.IsWhiteSpace(c) && !char.IsSurrogate(c)) chars.Add(c);
            }
            var missing = chars.Where(c => !cmap.Contains(c)).ToArray();
            Assert.IsEmpty(missing, "characters without a glyph: " + new string(missing));
        }
    }
}
