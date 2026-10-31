#nullable enable
// REVIEW_PHASE2 R-01, R-02, R-12: the core's regular expressions have the JavaScript meaning (JsRegex). Expected values come
// from node 22 (`/\s/.test(c)`, `/./.test(c)`, `/\d/.test(c)` over all 65 536 UTF-16 code units; parseAscii of the web core).
using System;
using System.Linq;
using System.Text.RegularExpressions;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class JsRegexTests
    {
        /// <summary>TS probe: every c in 0..0xFFFF with /\s/.test(String.fromCharCode(c)) (25 code units).</summary>
        private static readonly int[] JsSpace =
        {
            0x9, 0xa, 0xb, 0xc, 0xd, 0x20, 0xa0, 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008,
            0x2009, 0x200a, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000, 0xfeff,
        };

        [Test(Description = "K-45 `$` matches only at the end of the input (JS, no m flag): a final newline is not accepted")]
        public void K45_JsRegex_dollar_is_the_end_of_input_only()
        {
            var re = new JsRegex("^[WY]{1,4}$");
            Assert.IsTrue(re.IsMatch("WY"));
            Assert.IsFalse(re.IsMatch("WY\n"));
            Assert.IsFalse(re.IsMatch("WY\r"));
            Assert.IsFalse(re.IsMatch("\nWY"));
            // the .NET behaviour this class replaces (documents why `\z` is needed)
            Assert.IsTrue(new Regex("^[WY]{1,4}$").IsMatch("WY\n"));
        }

        [Test(Description = "K-45 `\\d` is [0-9] (JS), also inside a character class")]
        public void K45_JsRegex_digit_is_ASCII_only()
        {
            var re = new JsRegex("^(\\d+)$");
            Assert.IsTrue(re.IsMatch("0123456789"));
            int digits = 0;
            for (int c = 0; c <= 0xFFFF; c++)
                if (re.IsMatch(((char)c).ToString())) digits++;
            Assert.AreEqual(10, digits, "TS: 10 code units match /\\d/");
            Assert.IsFalse(re.IsMatch("\u0660"));
            Assert.IsFalse(re.IsMatch("\u0966"));
            Assert.IsFalse(re.IsMatch("\uff10"));
            var inClass = new JsRegex("^[\\d.]+$");
            Assert.IsTrue(inClass.IsMatch("12.5"));
            Assert.IsFalse(inClass.IsMatch("1\u0661"));
        }

        [Test(Description = "K-45 `\\s` is the 25 JS white-space / line-terminator code units (not \\x85, with \\uFEFF)")]
        public void K45_JsRegex_space_class_equals_the_JS_set()
        {
            var re = new JsRegex("^\\s$");
            var notRe = new JsRegex("^\\S$");
            var inClass = new JsRegex("^[\\s_]$");
            for (int c = 0; c <= 0xFFFF; c++)
            {
                string s = ((char)c).ToString();
                bool expected = Array.IndexOf(JsSpace, c) >= 0;
                Assert.AreEqual(expected, re.IsMatch(s), "\\s on U+" + c.ToString("X4"));
                Assert.AreEqual(!expected, notRe.IsMatch(s), "\\S on U+" + c.ToString("X4"));
                Assert.AreEqual(expected || c == '_', inClass.IsMatch(s), "[\\s_] on U+" + c.ToString("X4"));
                Assert.AreEqual(expected, JsRegex.IsJsSpace((char)c), "IsJsSpace U+" + c.ToString("X4"));
            }
            Assert.AreEqual(25, Enumerable.Range(0, 0x10000).Count(c => JsRegex.IsJsSpace((char)c)));
            Assert.IsFalse(new JsRegex("^\\s$").IsMatch("\u0085"), ".NET `\\s` matches U+0085, JS does not");
            Assert.IsTrue(new Regex("^\\s$").IsMatch("\u0085"));
            Assert.IsTrue(new JsRegex("^\\s$").IsMatch("\ufeff"), "JS `\\s` matches U+FEFF, .NET does not");
            Assert.IsFalse(new Regex("^\\s$").IsMatch("\ufeff"));
        }

        [Test(Description = "K-45 a bare `.` excludes the four JS line terminators (\\n \\r U+2028 U+2029)")]
        public void K45_JsRegex_dot_excludes_the_JS_line_terminators()
        {
            var re = new JsRegex("^.$");
            var excluded = Enumerable.Range(0, 0x10000).Where(c => !re.IsMatch(((char)c).ToString())).ToArray();
            CollectionAssert.AreEqual(new[] { 0x0a, 0x0d, 0x2028, 0x2029 }, excluded, "TS probe: /./.test over all code units");
            Assert.IsFalse(new JsRegex("^.$").IsMatch("\n"));
            Assert.IsTrue(new JsRegex("^[.]$").IsMatch("."), "a dot inside a class stays a literal dot");
            Assert.IsFalse(new JsRegex("^[.]$").IsMatch("x"));
        }

        [Test(Description = "K-45 `\\w` is [A-Za-z0-9_] (63 code units); escapes without a safe translation are refused")]
        public void K45_JsRegex_word_class_and_unsupported_escapes()
        {
            var w = new JsRegex("^\\w$");
            Assert.AreEqual(63, Enumerable.Range(0, 0x10000).Count(c => w.IsMatch(((char)c).ToString())));
            Assert.Throws<NotSupportedException>(() => new JsRegex("\\bword"));
            Assert.Throws<NotSupportedException>(() => new JsRegex("\\p{L}"));
            Assert.Throws<NotSupportedException>(() => new JsRegex("[\\S]"));
            Assert.Throws<ArgumentException>(() => new JsRegex("abc\\"));
        }

        [Test(Description = "K-45 R-12 the JS source text is kept verbatim: Literal = `/source/`, Source = the TS literal")]
        public void K45_JsRegex_keeps_the_TS_source_for_schema_messages()
        {
            Assert.AreEqual("^[WYGROCBP.?]{1,4}$", LevelSchemaPatterns.PlanRow.Source);
            Assert.AreEqual("/^[WYGROCBP.?]{1,4}$/", LevelSchemaPatterns.PlanRow.Literal);
            Assert.AreEqual("/^piece:(\\d{1,2}|k[1-9]_\\d{1,2})$/", LevelSchemaPatterns.PieceRef.Literal);
            Assert.AreEqual("/^tut\\.(l\\d{1,2}|m|ctx)\\.[a-z][A-Za-z0-9]*$/", LevelSchemaPatterns.TutTextKey.Literal);
            Assert.AreEqual("/^(B1|D2|I3|I4|O4|C3|L4|J4|T4|S4|Z4|I5|Q9)_(0|90|180|270)$/", LevelSchemaPatterns.ShapeId.Literal);
            StringAssert.StartsWith("/^(piece:(\\d{1,2}|k[1-9]_\\d{1,2})|cell:[0-7],[0-9]|", LevelSchemaPatterns.Highlight.Literal);
            StringAssert.EndsWith("|truck|streak|fan)$/", LevelSchemaPatterns.Highlight.Literal);
        }

        [Test(Description = "K-45 JS trim: removes the 25 JS space code units, keeps U+0085 (which .NET Trim removes)")]
        public void K45_JsRegex_trim_equals_String_prototype_trim()
        {
            foreach (int c in JsSpace) Assert.AreEqual("a b", JsRegex.Trim(((char)c) + "a b" + (char)c), "U+" + c.ToString("X4"));
            Assert.AreEqual("\u0085x", JsRegex.Trim("\u0085x"));
            Assert.AreEqual("", JsRegex.Trim(" \t\r\n\ufeff"));
            Assert.AreEqual("x\u0085", JsRegex.Trim(" x\u0085 "));
            Assert.AreEqual("same", JsRegex.Trim("same"));
        }

        [Test(Description = "K-45 digit strings convert like JS Number for the values compared with a count; they saturate instead of throwing")]
        public void K45_JsRegex_DigitsToInt_saturates_and_refuses_non_digits()
        {
            Assert.AreEqual(0, JsRegex.DigitsToInt("0"));
            Assert.AreEqual(12, JsRegex.DigitsToInt("012"));
            Assert.AreEqual(2147483646, JsRegex.DigitsToInt("2147483646"));
            Assert.AreEqual(int.MaxValue, JsRegex.DigitsToInt("2147483647"));
            Assert.AreEqual(int.MaxValue, JsRegex.DigitsToInt("99999999999999999999999999"));
            Assert.Throws<FormatException>(() => JsRegex.DigitsToInt("\u0660"));
            Assert.Throws<FormatException>(() => JsRegex.DigitsToInt("1 "));
        }

        // --- Ascii.ParseAscii (ascii.ts parseAscii): JS whitespace in `trim` and `\s+$` -------------------------------------

        // TS probe: toAscii(createInitialState(level_001)) + the edit → parseAscii: "ok" or "THROW SyntaxError: <message start>".
        private static string BaseAscii()
        {
            var s = State.CreateInitialState(Compiler.LoadLevel(TestPaths.LoadLevelJson(1)).LevelOrThrow());
            return Ascii.ToAscii(s);
        }

        private static string Parse(string text)
        {
            try
            {
                var b = Ascii.ParseAscii(text);
                return "ok plan=" + string.Join("|", b.Plan);
            }
            catch (FormatException e)
            {
                if (e.Message.StartsWith("ascii: bad header", StringComparison.Ordinal)) return "THROW bad header";
                if (e.Message.StartsWith("ascii: unexpected line", StringComparison.Ordinal)) return "THROW unexpected line";
                return "THROW " + e.Message;
            }
        }

        [Test(Description = "K-45 ParseAscii strips trailing JS whitespace (U+FEFF, U+00A0, CR) but not U+0085, like parseAscii in TS")]
        public void K45_ParseAscii_whitespace_follows_JS()
        {
            string a = BaseAscii();
            const string ok = "ok plan=WW|YY|WW|YW|YW";
            Assert.AreEqual(ok, Parse(a), "plain");
            Assert.AreEqual(ok, Parse(a + "\n\ufeff"), "blank line of U+FEFF");
            Assert.AreEqual("THROW unexpected line", Parse(a + "\n\u0085"), "blank line of U+0085");
            Assert.AreEqual(ok, Parse(a + "\n\u00a0"), "blank line of U+00A0");
            Assert.AreEqual(ok, Parse(a.Replace("\n", "\ufeff\n")), "trailing U+FEFF on every line");
            Assert.AreEqual("THROW bad header", Parse(a.Replace("\n", "\u0085\n")), "trailing U+0085 on every line");
            Assert.AreEqual(ok, Parse(a.Replace("\n", "\r\n")), "CRLF");
            Assert.AreEqual("THROW bad header", Parse(a.Replace("L1 ", "L\u0661 ")), "Arabic-Indic digit in the header");
            Assert.AreEqual(ok, Parse(a + "\nhidden:\ufeff-"), "hidden: U+FEFF -");
        }
    }
}
