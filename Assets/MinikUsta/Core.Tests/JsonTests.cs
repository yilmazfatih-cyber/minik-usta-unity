#nullable enable
// Core/Json reader + writer: JSON.parse / JSON.stringify compatibility, canonical form and FNV-1a 64 (moves.ts vectors).
using System;
using MinikUsta.Core.Json;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class JsonTests
    {
        [Test]
        public void Reader_parses_the_six_value_kinds_and_keeps_object_key_order()
        {
            var v = JsonReader.Parse(" {\"b\": [1, 2.5, -3e2, true, false, null], \"a\": {\"x\": \"s\\n\\u00e9\\ud83d\\ude00\"}, \"b\": 7} ");
            Assert.IsTrue(v.IsObject);
            CollectionAssert.AreEqual(new[] { "b", "a" }, v.Keys); // repeated key keeps its position, takes the last value
            Assert.AreEqual(7, v["b"]!.AsInt);
            var inner = v["a"]!["x"]!.AsString;
            Assert.AreEqual("s\n\u00e9\U0001F600", inner);
            Assert.IsNull(v["missing"]);
            var arr = JsonReader.Parse("[1, 2.5, -3e2, true, false, null]");
            Assert.AreEqual(6, arr.Count);
            Assert.AreEqual(1, arr[0].AsInt);
            Assert.AreEqual(2.5, arr[1].AsDouble);
            Assert.AreEqual(-300, arr[2].AsDouble);
            Assert.IsTrue(arr[2].IsInteger);
            Assert.IsTrue(arr[3].AsBool);
            Assert.IsFalse(arr[4].AsBool);
            Assert.IsTrue(arr[5].IsNull);
            Assert.IsFalse(arr[1].IsInteger);
        }

        [TestCase("{\"a\": 1,}")]
        [TestCase("[1, 2,]")]
        [TestCase("{a: 1}")]
        [TestCase("// c\n1")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("01")]
        [TestCase("1.")]
        [TestCase(".5")]
        [TestCase("\"tab\there\"")]
        [TestCase("\"open")]
        [TestCase("1 2")]
        [TestCase("")]
        [TestCase("'single'")]
        [TestCase("tru")]
        public void Reader_rejects_what_JSON_parse_rejects(string text)
        {
            Assert.Throws<JsonParseException>(() => JsonReader.Parse(text));
        }

        [TestCase(0.35, "0.35")]
        [TestCase(1e21, "1e+21")]
        [TestCase(1.5e-7, "1.5e-7")]
        [TestCase(-0.0, "0")]
        [TestCase(123456789012.0, "123456789012")]
        [TestCase(0.000001, "0.000001")]
        [TestCase(1e-7, "1e-7")]
        [TestCase(100.0, "100")]
        [TestCase(1.5, "1.5")]
        [TestCase(-2.5e-8, "-2.5e-8")]
        [TestCase(1e20, "100000000000000000000")]
        [TestCase(123456.789, "123456.789")]
        [TestCase(0.1, "0.1")]
        [TestCase(5e-324, "5e-324")]
        [TestCase(1.7976931348623157e308, "1.7976931348623157e+308")]
        public void Writer_formats_numbers_like_ECMAScript_Number_toString(double value, string expected)
        {
            Assert.AreEqual(expected, JsNumber.Format(value));
        }

        [Test]
        public void Canonical_sorts_keys_recursively_and_escapes_like_JSON_stringify()
        {
            var v = JsonReader.Parse("{\"b\":1,\"a\":{\"d\":[2,{\"z\":1,\"y\":2}]},\"n\":0.35,\"m\":-0,\"k\":1e21,\"j\":1.5e-7,\"s\":\"x\\\"y\\\\z\\n\\u0001é\"}");
            string expected = TestPaths.Vectors["fnv"]!["canonicalSample"]!.AsString;
            Assert.AreEqual(expected, JsonWriter.Canonical(v));
            Assert.AreEqual(TestPaths.Vectors["fnv"]!["canonicalSampleHash"]!.AsString, Fnv1a.Hash64Hex(JsonWriter.Canonical(v)));
            // non-canonical write keeps the source order
            Assert.AreEqual("{\"b\":1,\"a\":2}", JsonWriter.Write(JsonReader.Parse("{ \"b\" : 1 , \"a\" : 2 }"), false));
            // a lone surrogate is escaped (well-formed JSON.stringify)
            Assert.AreEqual("\"\\ud800\"", JsonWriter.Write(JsonValue.Of("\ud800"), false));
        }

        [Test]
        public void Fnv1a64_matches_the_TypeScript_reference_vectors()
        {
            var v = TestPaths.Vectors["fnv"]!;
            Assert.AreEqual("cbf29ce484222325", Fnv1a.Hash64Hex(""));
            Assert.AreEqual("af63dc4c8601ec8c", Fnv1a.Hash64Hex("a"));
            Assert.AreEqual("85944171f73967e8", Fnv1a.Hash64Hex("foobar"));
            Assert.AreEqual(v["empty"]!.AsString, Fnv1a.Hash64Hex(""));
            Assert.AreEqual(v["dotlessI"]!.AsString, Fnv1a.Hash64Hex("ı"));
            Assert.AreNotEqual(Fnv1a.Hash64Hex("i"), Fnv1a.Hash64Hex("ı"));
            Assert.AreEqual(v["turkish"]!.AsString, Fnv1a.Hash64Hex("Ağaç Basamakları"));
            Assert.AreEqual(v["emoji"]!.AsString, Fnv1a.Hash64Hex("a\U0001F600b"));
        }

        [Test]
        public void Round_trip_parse_write_parse_is_stable_for_every_shipped_level()
        {
            for (int n = 1; n <= 10; n++)
            {
                var raw = TestPaths.LoadLevelJson(n);
                var again = JsonReader.Parse(JsonWriter.Write(raw, false));
                Assert.IsTrue(raw.DeepEquals(again), "level " + n);
                Assert.AreEqual(JsonWriter.Canonical(raw), JsonWriter.Canonical(again));
            }
        }
    }
}
