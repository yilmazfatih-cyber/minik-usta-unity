#nullable enable
// Dependency-free JSON document model for the core (no System.Text.Json / Newtonsoft: the core assembly has no engine or
// package references). Mirrors what `JSON.parse` produces: null, bool, number (double), string, array, object (ordered keys,
// a repeated key replaces the earlier value in place).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MinikUsta.Core.Json
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object,
    }

    public sealed class JsonValue
    {
        public readonly JsonKind Kind;
        private readonly bool boolValue;
        private readonly double numberValue;
        private readonly string? stringValue;
        private readonly List<JsonValue>? items;
        private readonly List<string>? keys;
        private readonly List<JsonValue>? values;

        private JsonValue(JsonKind kind, bool b, double n, string? s, List<JsonValue>? items, List<string>? keys, List<JsonValue>? values)
        {
            Kind = kind;
            boolValue = b;
            numberValue = n;
            stringValue = s;
            this.items = items;
            this.keys = keys;
            this.values = values;
        }

        public static readonly JsonValue Null = new JsonValue(JsonKind.Null, false, 0, null, null, null, null);
        public static readonly JsonValue True = new JsonValue(JsonKind.Bool, true, 0, null, null, null, null);
        public static readonly JsonValue False = new JsonValue(JsonKind.Bool, false, 0, null, null, null, null);

        public static JsonValue Of(bool b) => b ? True : False;
        public static JsonValue Of(double n) => new JsonValue(JsonKind.Number, false, n, null, null, null, null);
        public static JsonValue Of(int n) => Of((double)n);
        public static JsonValue Of(long n) => Of((double)n);
        public static JsonValue Of(string s) => new JsonValue(JsonKind.String, false, 0, s, null, null, null);
        public static JsonValue Array() => new JsonValue(JsonKind.Array, false, 0, null, new List<JsonValue>(), null, null);
        public static JsonValue Array(IEnumerable<JsonValue> values)
        {
            var a = Array();
            foreach (var v in values) a.Add(v);
            return a;
        }
        public static JsonValue Object() => new JsonValue(JsonKind.Object, false, 0, null, null, new List<string>(), new List<JsonValue>());

        public bool IsNull => Kind == JsonKind.Null;
        public bool IsBool => Kind == JsonKind.Bool;
        public bool IsNumber => Kind == JsonKind.Number;
        public bool IsString => Kind == JsonKind.String;
        public bool IsArray => Kind == JsonKind.Array;
        public bool IsObject => Kind == JsonKind.Object;

        public bool AsBool => Kind == JsonKind.Bool ? boolValue : throw new InvalidOperationException("not a bool");
        public double AsDouble => Kind == JsonKind.Number ? numberValue : throw new InvalidOperationException("not a number");
        public string AsString => Kind == JsonKind.String ? stringValue! : throw new InvalidOperationException("not a string");

        /// <summary>`Number.isInteger` and inside the safe range (zod `z.int()`).</summary>
        public bool IsInteger => Kind == JsonKind.Number && !double.IsNaN(numberValue) && !double.IsInfinity(numberValue)
            && Math.Floor(numberValue) == numberValue && Math.Abs(numberValue) <= 9007199254740991.0;

        public int AsInt
        {
            get
            {
                if (!IsInteger || numberValue < int.MinValue || numberValue > int.MaxValue) throw new InvalidOperationException("not an int32");
                return (int)numberValue;
            }
        }

        public long AsLong
        {
            get
            {
                if (!IsInteger) throw new InvalidOperationException("not an integer");
                return (long)numberValue;
            }
        }

        /// <summary>Array length or object member count.</summary>
        public int Count => Kind == JsonKind.Array ? items!.Count : Kind == JsonKind.Object ? keys!.Count : throw new InvalidOperationException("not a container");

        public JsonValue this[int index] => Kind == JsonKind.Array ? items![index] : throw new InvalidOperationException("not an array");

        /// <summary>Object member, or null when absent (TS `obj[key] === undefined`).</summary>
        public JsonValue? this[string key]
        {
            get
            {
                if (Kind != JsonKind.Object) throw new InvalidOperationException("not an object");
                int i = keys!.IndexOf(key);
                return i < 0 ? null : values![i];
            }
        }

        public bool Has(string key) => Kind == JsonKind.Object && keys!.IndexOf(key) >= 0;

        public IReadOnlyList<JsonValue> Items => Kind == JsonKind.Array ? items! : throw new InvalidOperationException("not an array");
        public IReadOnlyList<string> Keys => Kind == JsonKind.Object ? keys! : throw new InvalidOperationException("not an object");

        public IEnumerable<KeyValuePair<string, JsonValue>> Members
        {
            get
            {
                if (Kind != JsonKind.Object) throw new InvalidOperationException("not an object");
                for (int i = 0; i < keys!.Count; i++) yield return new KeyValuePair<string, JsonValue>(keys[i], values![i]);
            }
        }

        public JsonValue Add(JsonValue v)
        {
            if (Kind != JsonKind.Array) throw new InvalidOperationException("not an array");
            items!.Add(v);
            return this;
        }

        /// <summary>Sets a member; an existing key keeps its position and takes the new value (`JSON.parse` semantics).</summary>
        public JsonValue Set(string key, JsonValue value)
        {
            if (Kind != JsonKind.Object) throw new InvalidOperationException("not an object");
            int i = keys!.IndexOf(key);
            if (i < 0)
            {
                keys.Add(key);
                values!.Add(value);
            }
            else values![i] = value;
            return this;
        }

        /// <summary>Sets a member when `value` is not null (TS: an `undefined` field is dropped by canonical JSON anyway).</summary>
        public JsonValue SetOpt(string key, JsonValue? value) => value == null ? this : Set(key, value);

        public override string ToString() => JsonWriter.Write(this, false);

        /// <summary>Deep structural equality (`canonicalJson(a) === canonicalJson(b)`).</summary>
        public bool DeepEquals(JsonValue o)
        {
            if (Kind != o.Kind) return false;
            switch (Kind)
            {
                case JsonKind.Null: return true;
                case JsonKind.Bool: return boolValue == o.boolValue;
                case JsonKind.Number: return numberValue.Equals(o.numberValue) || numberValue == o.numberValue;
                case JsonKind.String: return stringValue == o.stringValue;
                case JsonKind.Array:
                    if (items!.Count != o.items!.Count) return false;
                    for (int i = 0; i < items.Count; i++) if (!items[i].DeepEquals(o.items[i])) return false;
                    return true;
                default:
                    if (keys!.Count != o.keys!.Count) return false;
                    for (int i = 0; i < keys.Count; i++)
                    {
                        var ov = o[keys[i]];
                        if (ov == null || !values![i].DeepEquals(ov)) return false;
                    }
                    return true;
            }
        }

        internal static string DebugNumber(double n) => n.ToString("R", CultureInfo.InvariantCulture);
    }
}
