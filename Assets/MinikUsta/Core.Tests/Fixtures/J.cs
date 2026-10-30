#nullable enable
// Tiny JSON literal builder for test specs: J.O("type", "static", "y", 3, "size", 1) → {"type":"static","y":3,"size":1}.
// A null value omits the key (TS: an undefined field).
using System.Collections.Generic;
using MinikUsta.Core.Json;

namespace MinikUsta.Core.Tests.Fixtures
{
    public static class J
    {
        public static JsonValue V(object? v)
        {
            switch (v)
            {
                case null: return JsonValue.Null;
                case JsonValue j: return j;
                case bool b: return JsonValue.Of(b);
                case int i: return JsonValue.Of(i);
                case long l: return JsonValue.Of(l);
                case double d: return JsonValue.Of(d);
                case string s: return JsonValue.Of(s);
                case int[] ia:
                {
                    var a = JsonValue.Array();
                    foreach (var x in ia) a.Add(JsonValue.Of(x));
                    return a;
                }
                case string[] sa:
                {
                    var a = JsonValue.Array();
                    foreach (var x in sa) a.Add(JsonValue.Of(x));
                    return a;
                }
                case JsonValue[] ja:
                {
                    var a = JsonValue.Array();
                    foreach (var x in ja) a.Add(x);
                    return a;
                }
                case object[] oa:
                {
                    var a = JsonValue.Array();
                    foreach (var x in oa) a.Add(V(x));
                    return a;
                }
                case IEnumerable<JsonValue> e:
                {
                    var a = JsonValue.Array();
                    foreach (var x in e) a.Add(x);
                    return a;
                }
                default:
                    throw new System.ArgumentException("J.V: unsupported value " + v.GetType().Name);
            }
        }

        /// <summary>Object from alternating key / value arguments; a null value omits the key.</summary>
        public static JsonValue O(params object?[] kv)
        {
            var o = JsonValue.Object();
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                if (kv[i + 1] == null) continue;
                o.Set((string)kv[i]!, V(kv[i + 1]));
            }
            return o;
        }

        public static JsonValue A(params object?[] items)
        {
            var a = JsonValue.Array();
            foreach (var x in items) a.Add(V(x));
            return a;
        }
    }
}
