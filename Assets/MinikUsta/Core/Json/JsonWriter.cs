#nullable enable
// JSON writer that reproduces `JSON.stringify` byte for byte (string escapes, JS number formatting), plus the canonical
// form of src/core/moves.ts `canonicalJson`: object keys sorted recursively by UTF-16 code units (`Array.prototype.sort`
// default), no whitespace. `eventLogHash` and `levelHash` (FNV-1a 64) are computed over this text.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MinikUsta.Core.Json
{
    public static class JsonWriter
    {
        /// <summary>`JSON.stringify(sortKeys(value))`: one text per value.</summary>
        public static string Canonical(JsonValue v) => Write(v, true);

        public static string Write(JsonValue v, bool sortKeys)
        {
            var sb = new StringBuilder();
            WriteTo(sb, v, sortKeys);
            return sb.ToString();
        }

        private static void WriteTo(StringBuilder sb, JsonValue v, bool sortKeys)
        {
            switch (v.Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(v.AsBool ? "true" : "false");
                    break;
                case JsonKind.Number:
                    sb.Append(JsNumber.Format(v.AsDouble));
                    break;
                case JsonKind.String:
                    WriteString(sb, v.AsString);
                    break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < v.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteTo(sb, v[i], sortKeys);
                    }
                    sb.Append(']');
                    break;
                default:
                    sb.Append('{');
                    var keys = new List<string>(v.Keys);
                    if (sortKeys) keys.Sort(string.CompareOrdinal);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteString(sb, keys[i]);
                        sb.Append(':');
                        WriteTo(sb, v[keys[i]]!, sortKeys);
                    }
                    sb.Append('}');
                    break;
            }
        }

        /// <summary>`JSON.stringify` string quoting (well-formed: lone surrogates become `\udXXX`).</summary>
        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                        {
                            sb.Append(c).Append(s[i + 1]);
                            i++;
                        }
                        else if (char.IsSurrogate(c)) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>ECMAScript `Number::toString(10)` for finite doubles (JSON.stringify prints non-finite numbers as `null`).</summary>
    public static class JsNumber
    {
        public static string Format(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return "null";
            if (x == 0) return "0"; // −0 prints as 0
            bool negative = x < 0;
            if (negative) x = -x;
            // Shortest round-trip digits ("R" is shortest round-trippable on .NET Core 3.0+).
            string r = x.ToString("R", CultureInfo.InvariantCulture);
            int exp = 0;
            int ePos = r.IndexOfAny(new[] { 'E', 'e' });
            string mantissa = r;
            if (ePos >= 0)
            {
                exp = int.Parse(r.Substring(ePos + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                mantissa = r.Substring(0, ePos);
            }
            int dot = mantissa.IndexOf('.');
            string intPart = dot < 0 ? mantissa : mantissa.Substring(0, dot);
            string fracPart = dot < 0 ? "" : mantissa.Substring(dot + 1);
            string digits = intPart + fracPart;
            // n: position of the decimal point relative to the digit string (value = 0.digits × 10^n)
            int n = intPart.Length + exp;
            // strip leading zeros
            int lead = 0;
            while (lead < digits.Length - 1 && digits[lead] == '0')
            {
                lead++;
                n--;
            }
            digits = digits.Substring(lead);
            // strip trailing zeros
            int end = digits.Length;
            while (end > 1 && digits[end - 1] == '0') end--;
            digits = digits.Substring(0, end);
            int k = digits.Length;
            var sb = new StringBuilder();
            if (negative) sb.Append('-');
            if (k <= n && n <= 21)
            {
                sb.Append(digits);
                for (int i = 0; i < n - k; i++) sb.Append('0');
            }
            else if (0 < n && n <= 21)
            {
                sb.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
            }
            else if (-6 < n && n <= 0)
            {
                sb.Append("0.");
                for (int i = 0; i < -n; i++) sb.Append('0');
                sb.Append(digits);
            }
            else
            {
                int e = n - 1;
                sb.Append(digits[0]);
                if (k > 1) sb.Append('.').Append(digits, 1, k - 1);
                sb.Append('e').Append(e < 0 ? '-' : '+').Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
