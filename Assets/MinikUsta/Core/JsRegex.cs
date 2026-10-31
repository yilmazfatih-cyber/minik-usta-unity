#nullable enable
// JavaScript regular expressions on .NET (REVIEW_PHASE2 R-01, R-02, R-12). The web core validates strings with JS
// literals such as `/^[WYGROCBP.?]{1,4}$/` (zod schema, logic.ts, ascii.ts). The same text means something else in
// .NET: `$` also matches before a final "\n"; `\d` is any Unicode Nd digit; `\s` has \x85 and lacks \ufeff; `.` does not
// exclude \r, \u2028 and \u2029. `RegexOptions.ECMAScript` fixes none of the first one. So every core pattern is written
// once as its TS source text and `JsRegex` translates it to the .NET dialect with the JS meaning:
//   `$` (outside a class) -> `\z`        `\d` -> `[0-9]`        `\s` -> the 25 JS whitespace/line terminator code points
//   `\w` -> `[A-Za-z0-9_]`               `.`  -> `[^\n\r\u2028\u2029]`
// Flags: none (the `m`/`i`/`s`/`u` flags are not used by the core; a flag-free `^` is already start-of-input in .NET).
// The TS source stays available as `Source` / `Literal` so schema messages equal zod's en text (`must match pattern /…/`).
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MinikUsta.Core
{
    /// <summary>A .NET <see cref="Regex"/> built from a JS regular expression source with JS semantics (no flags).</summary>
    public sealed class JsRegex : Regex
    {
        /// <summary>JS `\s` without the brackets (ECMA-262 WhiteSpace + LineTerminator).</summary>
        public const string SpaceSet = "\\t\\n\\v\\f\\r \\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000\\ufeff";

        /// <summary>The JS source text, exactly as written in the TS file (without the slashes).</summary>
        public readonly string Source;

        /// <summary>`/source/`, what `String(regex)` prints in JS and zod puts into "Invalid string: must match pattern …".</summary>
        public string Literal => "/" + Source + "/";

        public JsRegex(string source)
            : base(Translate(source), RegexOptions.CultureInvariant)
        {
            Source = source;
        }

        /// <summary>True for a JS `\s` character (ECMA-262 WhiteSpace and LineTerminator).</summary>
        public static bool IsJsSpace(char c)
        {
            switch (c)
            {
                case '\t': case '\n': case '\v': case '\f': case '\r': case ' ':
                case '\u00a0': case '\u1680': case '\u2028': case '\u2029':
                case '\u202f': case '\u205f': case '\u3000': case '\ufeff':
                    return true;
                default:
                    return c >= '\u2000' && c <= '\u200a';
            }
        }

        /// <summary>JS `String.prototype.trim`: removes JS whitespace and line terminators on both sides (not \x85, which .NET trims).</summary>
        public static string Trim(string s)
        {
            int start = 0;
            int end = s.Length;
            while (start < end && IsJsSpace(s[start])) start++;
            while (end > start && IsJsSpace(s[end - 1])) end--;
            return start == 0 && end == s.Length ? s : s.Substring(start, end - start);
        }

        /// <summary>Translates a flag-free JS pattern into .NET syntax with the JS meaning (see the file header).</summary>
        public static string Translate(string source)
        {
            var sb = new StringBuilder(source.Length + 16);
            bool inClass = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '\\')
                {
                    if (i + 1 >= source.Length) throw new ArgumentException("JsRegex: dangling backslash in /" + source + "/");
                    char e = source[++i];
                    switch (e)
                    {
                        case 'd':
                            sb.Append(inClass ? "0-9" : "[0-9]");
                            break;
                        case 's':
                            sb.Append(inClass ? SpaceSet : "[" + SpaceSet + "]");
                            break;
                        case 'w':
                            sb.Append(inClass ? "A-Za-z0-9_" : "[A-Za-z0-9_]");
                            break;
                        case 'D':
                        case 'S':
                        case 'W':
                            if (inClass) throw new NotSupportedException("JsRegex: \\" + e + " inside a class is not supported: /" + source + "/");
                            sb.Append(e == 'D' ? "[^0-9]" : e == 'S' ? "[^" + SpaceSet + "]" : "[^A-Za-z0-9_]");
                            break;
                        case 'b':
                        case 'B':
                        case 'p':
                        case 'P':
                        case 'k':
                        case 'u':
                        case 'x':
                        case 'c':
                            // \b/\B differ (Unicode \w in .NET), the others have another syntax or meaning: add them with a test.
                            throw new NotSupportedException("JsRegex: \\" + e + " is not supported: /" + source + "/");
                        default:
                            sb.Append('\\').Append(e);
                            break;
                    }
                    continue;
                }
                if (inClass)
                {
                    if (c == ']') inClass = false;
                    sb.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '[':
                        inClass = true;
                        sb.Append(c);
                        break;
                    case '$':
                        sb.Append("\\z");
                        break;
                    case '.':
                        sb.Append("[^\\n\\r\\u2028\\u2029]");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// JS `Number(s)` for a string already known to be `[0-9]+` (checked by the caller's regex): the int value, saturating at
        /// <see cref="int.MaxValue"/> (JS would give a huge double; every caller only compares it with a small count).
        /// </summary>
        public static int DigitsToInt(string digits)
        {
            long v = 0;
            foreach (char ch in digits)
            {
                if (ch < '0' || ch > '9') throw new FormatException("not an ASCII digit string: " + digits);
                v = v * 10 + (ch - '0');
                if (v >= int.MaxValue) return int.MaxValue;
            }
            return (int)v;
        }
    }
}
