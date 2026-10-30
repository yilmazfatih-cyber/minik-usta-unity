#nullable enable
// Strict RFC 8259 JSON reader (dependency free). Rejects comments, trailing commas, NaN/Infinity, leading zeros and
// control characters inside strings, like `JSON.parse`. Numbers become doubles (the JS number model).
using System;
using System.Globalization;
using System.Text;

namespace MinikUsta.Core.Json
{
    public sealed class JsonParseException : Exception
    {
        public readonly int Line;
        public readonly int Column;

        public JsonParseException(string message, int line, int column)
            : base(message + " (line " + line + ", column " + column + ")")
        {
            Line = line;
            Column = column;
        }
    }

    public static class JsonReader
    {
        public static JsonValue Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWs();
            var v = p.ReadValue(0);
            p.SkipWs();
            if (!p.AtEnd) throw p.Error("unexpected trailing characters");
            return v;
        }

        private const int MaxDepth = 256;

        private sealed class Parser
        {
            private readonly string s;
            private int i;

            public Parser(string text)
            {
                s = text;
                i = 0;
            }

            public bool AtEnd => i >= s.Length;

            public JsonParseException Error(string message)
            {
                int line = 1;
                int col = 1;
                for (int k = 0; k < i && k < s.Length; k++)
                {
                    if (s[k] == '\n')
                    {
                        line++;
                        col = 1;
                    }
                    else col++;
                }
                return new JsonParseException(message, line, col);
            }

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                    else break;
                }
            }

            public JsonValue ReadValue(int depth)
            {
                if (depth > MaxDepth) throw Error("nesting too deep");
                if (AtEnd) throw Error("unexpected end of input");
                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return JsonValue.Of(ReadString());
                    case 't': Expect("true"); return JsonValue.True;
                    case 'f': Expect("false"); return JsonValue.False;
                    case 'n': Expect("null"); return JsonValue.Null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("unexpected character '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error("invalid literal");
                i += word.Length;
            }

            private JsonValue ReadObject(int depth)
            {
                var o = JsonValue.Object();
                i++; // {
                SkipWs();
                if (!AtEnd && s[i] == '}')
                {
                    i++;
                    return o;
                }
                while (true)
                {
                    SkipWs();
                    if (AtEnd || s[i] != '"') throw Error("expected a string key");
                    string key = ReadString();
                    SkipWs();
                    if (AtEnd || s[i] != ':') throw Error("expected ':'");
                    i++;
                    SkipWs();
                    var v = ReadValue(depth + 1);
                    o.Set(key, v);
                    SkipWs();
                    if (AtEnd) throw Error("unterminated object");
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    if (s[i] == '}')
                    {
                        i++;
                        return o;
                    }
                    throw Error("expected ',' or '}'");
                }
            }

            private JsonValue ReadArray(int depth)
            {
                var a = JsonValue.Array();
                i++; // [
                SkipWs();
                if (!AtEnd && s[i] == ']')
                {
                    i++;
                    return a;
                }
                while (true)
                {
                    SkipWs();
                    a.Add(ReadValue(depth + 1));
                    SkipWs();
                    if (AtEnd) throw Error("unterminated array");
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    if (s[i] == ']')
                    {
                        i++;
                        return a;
                    }
                    throw Error("expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw Error("control character in string");
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (AtEnd) throw Error("unterminated escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Error("bad unicode escape");
                            int code = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int d = HexDigit(s[i + k]);
                                if (d < 0) throw Error("bad unicode escape");
                                code = code * 16 + d;
                            }
                            i += 4;
                            sb.Append((char)code);
                            break;
                        default:
                            throw Error("bad escape '\\" + e + "'");
                    }
                }
            }

            private static int HexDigit(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            private JsonValue ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                if (AtEnd) throw Error("bad number");
                if (s[i] == '0')
                {
                    i++;
                    if (!AtEnd && s[i] >= '0' && s[i] <= '9') throw Error("leading zero");
                }
                else if (s[i] >= '1' && s[i] <= '9')
                {
                    while (!AtEnd && s[i] >= '0' && s[i] <= '9') i++;
                }
                else throw Error("bad number");
                if (!AtEnd && s[i] == '.')
                {
                    i++;
                    int digits = 0;
                    while (!AtEnd && s[i] >= '0' && s[i] <= '9')
                    {
                        i++;
                        digits++;
                    }
                    if (digits == 0) throw Error("bad fraction");
                }
                if (!AtEnd && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++;
                    if (!AtEnd && (s[i] == '+' || s[i] == '-')) i++;
                    int digits = 0;
                    while (!AtEnd && s[i] >= '0' && s[i] <= '9')
                    {
                        i++;
                        digits++;
                    }
                    if (digits == 0) throw Error("bad exponent");
                }
                string text = s.Substring(start, i - start);
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) throw Error("bad number");
                return JsonValue.Of(d);
            }
        }
    }
}
