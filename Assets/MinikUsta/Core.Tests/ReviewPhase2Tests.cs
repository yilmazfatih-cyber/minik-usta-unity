#nullable enable
// Skeptic review of the phase 2 port (docs/REVIEW_PHASE2.md). Each test pins what the TypeScript core (web repo, source of
// truth, D-091) does in a corner the P0–P4 vectors and the differential fuzz do not reach. Expected values were produced by
// the TS runtime (node 22, web repo b31c4e8): inline constants carry the probe in their comment, larger sets come from
// tools/review/gen-*-vectors.ts (Golden/review_*.json).
//
// A test that is red on the current C# core is marked [Ignore("REVIEW_PHASE2 R-xx …")]: the suite stays green, the finding
// stays executable. Whoever fixes the core removes the Ignore; the test must then pass unchanged.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class ReviewPhase2Tests
    {
        // --- helpers ----------------------------------------------------------------------------------------------------

        private static string Level1Text() => System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.LevelsDir, "level_001.json"));

        /// <summary>level_001.json with the first occurrence of `from` replaced (the TS probe applied the same edit to the parsed object).</summary>
        private static JsonValue Level1With(string from, string to)
        {
            string text = Level1Text();
            int at = text.IndexOf(from, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "level_001.json contains " + from);
            return JsonReader.Parse(text.Substring(0, at) + to + text.Substring(at + from.Length));
        }

        private static string LoadSummary(JsonValue json)
        {
            var r = Compiler.LoadLevel(json);
            if (r.Ok) return "ok";
            return r.Stage + " " + string.Join(",", r.Issues.Select(i => i.Check + "/" + i.Code + "@" + i.Path));
        }

        private static string ValidateSummary(JsonValue json)
        {
            var v = Logic.ValidateLevelJson(json, new LogicContext(fileId: 1));
            return (v.Level != null ? "level" : "none") + " " + string.Join(",", v.Issues.Select(i => i.Check + "/" + i.Code + "@" + i.Path));
        }

        /// <summary>Schema issues as `code@path:message`; the path in logic.ts `formatPath` form (`a.b[0].c`), as TS reports it.</summary>
        private static string SchemaSummary(JsonValue json)
        {
            var issues = new List<SchemaIssue>();
            LevelJson.Parse(json, issues);
            return string.Join(" ; ", issues.Select(i => i.Code + "@" + i.PathString + ":" + i.Message));
        }

        // --- R-01 / R-02: .NET Regex `$` and `\d` are not the JS ones ---------------------------------------------------

        // TS probe: loadLevel(level_001 with the edit) → { ok: false, stage, issues: check/code@path }.
        private static readonly object[][] TrailingNewlineCases =
        {
            new object[] { "plan row", "\"rows\": [\"WW\",", "\"rows\": [\"WW\\n\",", "schema L-01/schema_invalid@build.segments[0].rows[0]" },
            new object[] { "shape id", "\"shape\": \"D2_0\"", "\"shape\": \"D2_0\\n\"", "schema L-01/schema_invalid@yard.batches[0].pieces[0].shape" },
            new object[] { "highlight", "\"highlight\": [\"piece:0\"", "\"highlight\": [\"piece:0\\n\"", "schema L-01/schema_invalid@tutorial[0].highlight[0]" },
            new object[] { "textKey", "\"textKey\": \"tut.m.lift\"", "\"textKey\": \"tut.m.lift\\n\"", "schema L-17/tut_key_missing@tutorial[0].textKey" },
        };

        [Test(Description = "K-45/1 a schema string with a trailing newline is refused at the schema stage like zod (JS `$` = end of input)")]
        [TestCaseSource(nameof(TrailingNewlineCases))]
        public void K45_a_trailing_newline_fails_the_schema_like_zod(string what, string from, string to, string ts)
        {
            string cs;
            try
            {
                cs = LoadSummary(Level1With(from, to));
            }
            catch (Exception e)
            {
                cs = "THROW " + e.GetType().Name + ": " + e.Message;
            }
            Assert.AreEqual(ts, cs, what);
        }

        // TS probe: validateLevelJson(level_001 with the edit, { fileId: 1 }) → { level: null, issues }.
        private static readonly object[][] NonAsciiDigitCases =
        {
            new object[] { "highlight piece:\u0660", "\"highlight\": [\"piece:0\"", "\"highlight\": [\"piece:\\u0660\"", "none L-01/schema_invalid@tutorial[0].highlight[0]" },
            new object[] { "highlight debris:\u0660", "\"highlight\": [\"piece:0\"", "\"highlight\": [\"debris:\\u0660\"", "none L-01/schema_invalid@tutorial[0].highlight[0]" },
            new object[] { "done piece:\u0660", "\"done\": { \"event\": \"placementCorrect\", \"count\": 1 }", "\"done\": { \"event\": \"placementCorrect\", \"count\": 1, \"piece\": \"piece:\\u0660\" }", "none L-01/schema_invalid@tutorial[0].done.piece" },
            new object[] { "done piece:k1_\u0660", "\"done\": { \"event\": \"placementCorrect\", \"count\": 1 }", "\"done\": { \"event\": \"placementCorrect\", \"count\": 1, \"piece\": \"piece:k1_\\u0660\" }", "none L-01/schema_invalid@tutorial[0].done.piece" },
            new object[] { "textKey tut.l\u0661", "\"textKey\": \"tut.m.lift\"", "\"textKey\": \"tut.l\\u0661.lift\"", "none L-17/tut_key_missing@tutorial[0].textKey" },
        };

        [Test(Description = "K-45 the validator refuses non-ASCII digits at the schema stage (JS `\\d` = [0-9]) and never throws")]
        [TestCaseSource(nameof(NonAsciiDigitCases))]
        public void K45_non_ASCII_digits_fail_the_schema_without_throwing(string what, string from, string to, string ts)
        {
            string cs;
            try
            {
                cs = ValidateSummary(Level1With(from, to));
            }
            catch (Exception e)
            {
                cs = "THROW " + e.GetType().Name + ": " + e.Message;
            }
            Assert.AreEqual(ts, cs, what);
        }

        [Test(Description = "K-45 guard: the schema regexes keep JS semantics on plain ASCII input (the R-01/R-02 fix must not change these)")]
        public void K45_schema_regexes_accept_and_refuse_plain_ASCII_like_zod()
        {
            // TS probe: LevelSchema.safeParse(level_001 with the edit).success
            Assert.AreEqual("ok", LoadSummary(TestPaths.LoadLevelJson(1)));
            Assert.AreEqual("", SchemaSummary(Level1With("\"highlight\": [\"piece:0\"", "\"highlight\": [\"piece:k1_12\"")));
            Assert.AreEqual("", SchemaSummary(Level1With("\"textKey\": \"tut.m.lift\"", "\"textKey\": \"tut.l12.liftAll9\"")));
            StringAssert.StartsWith("invalid_format@build.segments[0].rows[0]:", SchemaSummary(Level1With("\"rows\": [\"WW\",", "\"rows\": [\"WX\",")));
            StringAssert.StartsWith("invalid_format@tutorial[0].highlight[0]:", SchemaSummary(Level1With("\"highlight\": [\"piece:0\"", "\"highlight\": [\"piece:123\"")));
        }

        [Test(Description = "K-45 schema message of a failed regex equals zod's en locale text (`/^…$/`)")]
        public void K45_regex_schema_message_equals_zod_en_locale()
        {
            // TS probe (z.config(en()), path through logic.ts formatPath): invalid_format@build.segments[0].rows[0]:Invalid string: must match pattern /^[WYGROCBP.?]{1,4}$/
            Assert.AreEqual("invalid_format@build.segments[0].rows[0]:Invalid string: must match pattern /^[WYGROCBP.?]{1,4}$/",
                SchemaSummary(Level1With("\"rows\": [\"WW\",", "\"rows\": [\"WX\",")));
        }

        // --- R-03: unsafe integers (zod 4 `z.int()` = safe integer range) ------------------------------------------------

        [Ignore("REVIEW_PHASE2 R-03 (Öneri): C# reports |n| > 2^53 − 1 as invalid_type; zod 4 reports too_big / too_small and still runs the bounds check")]
        [Test(Description = "K-45/1 an integer outside ±(2^53 − 1) gets zod's too_big / too_small issues (code, message, count)")]
        public void K45_unsafe_integers_report_too_big_like_zod_4()
        {
            // TS probe (z.config(en())), LevelSchema.safeParse:
            //   yard.cols = 1e20  → too_big "Too big: expected int to be <=9007199254740991" ; too_big "Too big: expected number to be <=16"
            //   seed = 2^53       → too_big "Too big: expected int to be <=9007199254740991"
            //   seed = −2^53      → too_small "Too small: expected int to be >=-9007199254740991"
            Assert.AreEqual("too_big@yard.cols:Too big: expected int to be <=9007199254740991 ; too_big@yard.cols:Too big: expected number to be <=16",
                SchemaSummary(Level1With("\"cols\": 4,", "\"cols\": 1e20,")));
            Assert.AreEqual("too_big@seed:Too big: expected int to be <=9007199254740991",
                SchemaSummary(Level1With("\"moves\": 11,", "\"moves\": 11, \"seed\": 9007199254740992,")));
            Assert.AreEqual("too_small@seed:Too small: expected int to be >=-9007199254740991",
                SchemaSummary(Level1With("\"moves\": 11,", "\"moves\": 11, \"seed\": -9007199254740992,")));
        }

        [Test(Description = "K-45/1 guard: fractions, −0 and large safe seeds behave like zod (−0 is an int, seed −2^40 − 7 loads, `seed | 0` = low 32 bits)")]
        public void K45_fractions_negative_zero_and_large_safe_seeds_behave_like_zod()
        {
            // TS probe: x = 1.5 → invalid_type "Invalid input: expected int, received number"; x = -0 → ok; seed = −2^40 − 7 → ok
            Assert.AreEqual("invalid_type@yard.batches[0].pieces[0].x:Invalid input: expected int, received number",
                SchemaSummary(Level1With("\"x\": 0, \"y\": 2", "\"x\": 1.5, \"y\": 2")));
            Assert.AreEqual("ok", LoadSummary(Level1With("\"x\": 0, \"y\": 2", "\"x\": -0, \"y\": 2")));
            var lvl = Compiler.LoadLevel(Level1With("\"moves\": 11,", "\"moves\": 11, \"seed\": -1099511627783,")).LevelOrThrow();
            // TS: (−2^40 − 7) | 0 = −7
            Assert.AreEqual(-7, State.Hdr(State.CreateInitialState(lvl), H.Rng));
        }

        // --- R-04: canonicalJson key order of array-index keys ------------------------------------------------------------

        [Ignore("REVIEW_PHASE2 R-04 (Öneri): JS objects enumerate array-index keys (\"0\" … \"4294967294\") first, ascending; C# sorts every key by code units")]
        [Test(Description = "K-43/4 K-35 canonicalJson / JSON.stringify order array-index keys like a JS object")]
        public void K43_canonical_json_orders_array_index_keys_like_a_JS_object()
        {
            // TS probe: canonicalJson({ "10": 1, "2": 2, b: 3, a: 4 })
            Assert.AreEqual("{\"2\":2,\"10\":1,\"a\":4,\"b\":3}", JsonWriter.Canonical(JsonReader.Parse("{\"10\":1,\"2\":2,\"b\":3,\"a\":4}")));
            // TS probe: canonicalJson(JSON.parse(…)) — "4294967295" and "01" / "-1" are not array indices
            Assert.AreEqual("{\"2\":3,\"10\":2,\"4294967294\":8,\"-1\":6,\"01\":5,\"4294967295\":7,\"a\":4,\"b\":1}",
                JsonWriter.Canonical(JsonReader.Parse("{\"b\":1,\"10\":2,\"2\":3,\"a\":4,\"01\":5,\"-1\":6,\"4294967295\":7,\"4294967294\":8}")));
            // TS probe: JSON.stringify(JSON.parse('{"b":1,"1":2}'))
            Assert.AreEqual("{\"1\":2,\"b\":1}", JsonWriter.Write(JsonReader.Parse("{\"b\":1,\"1\":2}"), false));
        }

        [Test(Description = "K-43/4 guard: duplicate keys keep the first position and the last value (JSON.parse), escapes and surrogates round-trip")]
        public void K43_duplicate_keys_and_unicode_escapes_round_trip_like_JSON_parse()
        {
            // TS probes: canonicalJson(JSON.parse('{"b":1,"a":2,"b":3}')), canonicalJson(JSON.parse('["\ud83d\ude00","\uD83D","\u00e7","\/"]'))
            Assert.AreEqual("{\"a\":2,\"b\":3}", JsonWriter.Canonical(JsonReader.Parse("{\"b\":1,\"a\":2,\"b\":3}")));
            Assert.AreEqual("{\"b\":3,\"a\":2}", JsonWriter.Write(JsonReader.Parse("{\"b\":1,\"a\":2,\"b\":3}"), false));
            Assert.AreEqual("[\"\ud83d\ude00\",\"\\ud83d\",\"\u00e7\",\"/\"]", JsonWriter.Canonical(JsonReader.Parse("[\"\\ud83d\\ude00\",\"\\uD83D\",\"\\u00e7\",\"\\/\"]")));
            // TS probes: canonicalJson(JSON.parse(t)) for 1E2, -0, 1e400, -1e400, 9007199254740993, 12345678901234567890
            string[] input = { "1E2", "-0", "1e400", "-1e400", "9007199254740993", "12345678901234567890" };
            string[] ts = { "100", "0", "null", "null", "9007199254740992", "12345678901234567000" };
            for (int i = 0; i < input.Length; i++) Assert.AreEqual(ts[i], JsonWriter.Canonical(JsonReader.Parse(input[i])), input[i]);
        }

        [Ignore("REVIEW_PHASE2 R-11 (Öneri): JsonReader stops at depth 256; JSON.parse reads far deeper (300 levels parse in node 22)")]
        [Test(Description = "K-43 a 300-level nested array parses like JSON.parse")]
        public void K43_deep_nesting_parses_like_JSON_parse()
        {
            string deep = "0";
            for (int i = 0; i < 300; i++) deep = "[" + deep + "]";
            // TS probe: JSON.stringify(JSON.parse(deep)).length === 601
            Assert.AreEqual(601, JsonWriter.Write(JsonReader.Parse(deep), false).Length);
        }

        // --- R-05: number and string vectors (Unity Mono / IL2CPP guard) ------------------------------------------------

        private static JsonValue? jsonVectors;
        private static JsonValue JsonVectors => jsonVectors ??= TestPaths.LoadGolden("review_json_vectors.json");

        private static double FromBits(string hex16) => BitConverter.Int64BitsToDouble(unchecked((long)Convert.ToUInt64(hex16, 16)));

        [Test(Description = "K-35 K-43/4 JsNumber.Format equals JSON.stringify on 400 doubles (edge cases, random bit patterns, ratios) and parses back bit for bit")]
        public void K35_K43_JsNumber_format_equals_JSON_stringify_and_round_trips()
        {
            var bad = new List<string>();
            foreach (var d in JsonVectors["doubles"]!.Items)
            {
                double x = FromBits(d["bits"]!.AsString);
                string ts = d["text"]!.AsString;
                string cs = JsNumber.Format(x);
                if (cs != ts) bad.Add(d["bits"]!.AsString + ": TS " + ts + " C# " + cs);
                else if (BitConverter.DoubleToInt64Bits(JsonReader.Parse(ts).AsDouble) != BitConverter.DoubleToInt64Bits(x == 0 ? 0.0 : x))
                    bad.Add(d["bits"]!.AsString + ": " + ts + " does not parse back to the same bits");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad.Take(20)));
        }

        [Test(Description = "K-35 K-43/4 JsonReader + JsNumber equal JSON.stringify(JSON.parse(text)) on 300 decimal texts")]
        public void K35_K43_JsonReader_parses_decimal_texts_like_JSON_parse()
        {
            var bad = new List<string>();
            foreach (var t in JsonVectors["texts"]!.Items)
            {
                string cs = JsonWriter.Canonical(JsonReader.Parse(t["text"]!.AsString));
                if (cs != t["out"]!.AsString) bad.Add(t["text"]!.AsString + ": TS " + t["out"]!.AsString + " C# " + cs);
            }
            Assert.IsEmpty(bad, string.Join("\n", bad.Take(20)));
        }

        [Test(Description = "K-35 K-43/4 canonicalJson quoting and fnv1a64 (UTF-8 by hand, lone surrogates) equal TS on 200 strings")]
        public void K35_K43_string_quoting_and_fnv1a64_equal_TS()
        {
            var bad = new List<string>();
            foreach (var t in JsonVectors["strings"]!.Items)
            {
                string s = JsonReader.Parse(t["s"]!.AsString).AsString;
                string json = JsonWriter.Canonical(JsonValue.Of(s));
                string fnv = Moves.Fnv1a64(s);
                if (json != t["json"]!.AsString || fnv != t["fnv"]!.AsString) bad.Add(t["s"]!.AsString + ": TS " + t["json"]!.AsString + "/" + t["fnv"]!.AsString + " C# " + json + "/" + fnv);
            }
            Assert.IsEmpty(bad, string.Join("\n", bad.Take(20)));
        }

        // --- R-06: CurrentCulture (Unity follows the device locale) -------------------------------------------------------

        /// <summary>Core outputs that reach hashes, saves, golden files or validator messages; only strings made by the core are joined here.</summary>
        private static List<string> CoreOutputs()
        {
            var o = new List<string>();
            foreach (int id in HandGoldenTests.GoldenLevels)
            {
                var lvl = Boards.LevelFile(id);
                var log = SessionActionJson.LogFromJson(TestPaths.LoadGolden("level_" + id.ToString("000", CultureInfo.InvariantCulture) + ".hand.json")["log"]!);
                var sink = new ArraySink();
                var game = GameSession.Replay(lvl, log, null, sink);
                o.Add(Moves.EventLogHash(sink.Events) + "\n" + Hash.HashHex(game.State) + "\n" + Ascii.ToAscii(game.State, true) + "\n" + game.Outcome
                    + "\n" + JsonWriter.Canonical(EventJson.ToJson(sink.Events)) + "\n" + JsonWriter.Canonical(SessionActionJson.ToJson(game.Log)));
            }
            foreach (string group in new[] { "valid", "invalid" })
            {
                foreach (string name in TestPaths.FixtureNames(group))
                {
                    var v = Logic.ValidateLevelJson(TestPaths.LoadFixture(group, name), new LogicContext());
                    o.Add(group + "/" + name + ": " + string.Join("\n", v.Issues.Select(i => i.ToString())));
                    var r = Compiler.LoadLevel(TestPaths.LoadFixture(group, name));
                    o.Add(string.Join("\n", r.Issues.Concat(r.Warnings).Select(i => i.ToString())));
                }
            }
            foreach (var d in JsonVectors["doubles"]!.Items) o.Add(JsNumber.Format(FromBits(d["bits"]!.AsString)));
            o.Add(new Mulberry32(-12345).NextU32().ToString(CultureInfo.InvariantCulture));
            return o;
        }

        [Test(Description = "K-35 K-43 K-45 the core is culture invariant: replays, hashes, ASCII, event / log JSON and validator messages under fi-FI, ar-SA, tr-TR, sv-SE equal the invariant culture")]
        public void K35_K43_K45_core_outputs_do_not_depend_on_CurrentCulture()
        {
            var saved = CultureInfo.CurrentCulture;
            var savedUi = CultureInfo.CurrentUICulture;
            List<string> baseline;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                baseline = CoreOutputs();
                int tried = 0;
                foreach (string name in new[] { "fi-FI", "ar-SA", "tr-TR", "sv-SE" })
                {
                    CultureInfo c;
                    try
                    {
                        c = new CultureInfo(name);
                    }
                    catch (CultureNotFoundException)
                    {
                        continue; // invariant globalization mode: nothing to compare
                    }
                    tried++;
                    CultureInfo.CurrentCulture = c;
                    CultureInfo.CurrentUICulture = c;
                    var got = CoreOutputs();
                    Assert.AreEqual(baseline.Count, got.Count, name);
                    for (int i = 0; i < got.Count; i++) Assert.AreEqual(baseline[i], got[i], name + " output " + i);
                }
                if (tried == 0) Assert.Ignore("no culture data (invariant globalization)");
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
                CultureInfo.CurrentUICulture = savedUi;
            }
        }

        // --- R-07: the D3a memo is process-global ---------------------------------------------------------------------------

        private static List<GameState> D3aStates()
        {
            var o = new List<GameState>();
            foreach (int id in HandGoldenTests.GoldenLevels)
            {
                var lvl = Boards.LevelFile(id);
                var s0 = State.CreateInitialState(lvl);
                o.Add(s0);
                for (int p = 0; p < lvl.Layout.Counts.Pieces; p++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        var s = State.CloneState(s0);
                        State.SetPieceField(s, p, PF.Color, c); // recoloured supply: many dead D3a searches that fill the memo
                        o.Add(s);
                    }
                }
            }
            return o;
        }

        [Test(Description = "K-30 guard: the D3a states of R-07 give the same results when run twice in a row (memo generations reset per call)")]
        public void K30_D3a_results_repeat_sequentially()
        {
            var states = D3aStates();
            var first = states.Select(s => Deadlock.TileRemaining(s)).ToArray();
            var second = states.AsEnumerable().Reverse().Select(s => Deadlock.TileRemaining(s)).Reverse().ToArray();
            CollectionAssert.AreEqual(first, second);
            Assert.Greater(first.Count(r => r == TileResults.Dead), 50);
            Assert.Greater(first.Count(r => r == TileResults.Ok), 20);
        }

        [Test(Description = "K-30 D3a TileRemaining gives the sequential result when called from several threads at once")]
        public void K30_D3a_tile_remaining_is_thread_safe()
        {
            var states = D3aStates();
            states.AddRange(D3aBoards().Select(kv => kv.Value));
            var expected = states.Select(s => Deadlock.TileRemaining(s)).ToArray();
            int bad = 0;
            for (int round = 0; round < 3; round++)
            {
                Parallel.For(0, states.Count * 4, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
                {
                    int k = (i * 7919) % states.Count;
                    if (Deadlock.TileRemaining(states[k]) != expected[k]) Interlocked.Increment(ref bad);
                });
            }
            Assert.AreEqual(0, bad, "parallel TileRemaining results that differ from the sequential ones");
        }

        [Test(Description = "K-30 D3aStats.Expansions is per thread: a search on another thread never changes the counter of this one (R-07)")]
        public void K30_D3aStats_expansions_are_per_thread()
        {
            var boards = D3aBoards().Select(kv => kv.Value).ToList();
            // the board with the largest counter, and a counter this thread leaves behind
            GameState big = boards[0];
            int bigCount = -1;
            foreach (var b in boards)
            {
                Deadlock.TileRemaining(b);
                if (D3aStats.Expansions > bigCount)
                {
                    bigCount = D3aStats.Expansions;
                    big = b;
                }
            }
            Assert.Greater(bigCount, 1);
            Deadlock.TileRemaining(big);
            Assert.AreEqual(bigCount, D3aStats.Expansions);
            int otherThread = -1;
            var t = new Thread(() =>
            {
                Assert.AreEqual(0, D3aStats.Expansions, "a new thread starts at 0");
                Deadlock.TileRemaining(big, 1);
                otherThread = D3aStats.Expansions;
            });
            t.Start();
            t.Join();
            Assert.AreEqual(1, otherThread, "budget 1 -> one expansion on the other thread");
            Assert.AreEqual(bigCount, D3aStats.Expansions, "this thread's counter is untouched by the other thread");
        }

        // --- R-13: D3a expansion counter on large searches -----------------------------------------------------------------

        private static JsonValue? d3aVectors;
        private static JsonValue D3aVectors => d3aVectors ??= TestPaths.LoadGolden("review_d3a_vectors.json");

        private static readonly System.Text.RegularExpressions.Regex VariantName = new System.Text.RegularExpressions.Regex("^p([0-9]+)c([0-9]+)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>The R-13 boards: (board index, variant name, state) — the initial state or one block recoloured.</summary>
        private static List<KeyValuePair<string, GameState>> D3aBoards()
        {
            var o = new List<KeyValuePair<string, GameState>>();
            var boards = D3aVectors["boards"]!.Items;
            for (int n = 0; n < boards.Count; n++)
            {
                var lvl = Compiler.Compile(LevelJson.ParseOrThrow(boards[n]["level"]!.AsString));
                var s0 = State.CreateInitialState(lvl);
                foreach (var row in boards[n]["rows"]!.Items)
                {
                    string name = row.AsString.Substring(0, row.AsString.IndexOf('|'));
                    var s = State.CloneState(s0);
                    var m = VariantName.Match(name);
                    if (m.Success) State.SetPieceField(s, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), PF.Color, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
                    else Assert.AreEqual("init", name);
                    o.Add(new KeyValuePair<string, GameState>("board " + n + " " + row.AsString, s));
                }
            }
            return o;
        }

        [Test(Description = "K-30 D3a on 24 random 4 × 8 two-segment sites (120 states): result and expansion counter equal TS for budgets 1, 37, 500, 5 000, 20 000 (ok, dead and unknown)")]
        public void K30_D3a_result_and_expansions_equal_TS_on_large_searches()
        {
            var budgets = D3aVectors["budgets"]!.Items.Select(b => b.AsInt).ToArray();
            int n = 0;
            foreach (var kv in D3aBoards())
            {
                string expected = kv.Key.Substring(kv.Key.IndexOf('|') + 1);
                var got = budgets.Select(b => Deadlock.TileRemaining(kv.Value, b) + ":" + D3aStats.Expansions.ToString(CultureInfo.InvariantCulture));
                Assert.AreEqual(expected, string.Join(" ", got), kv.Key);
                n++;
            }
            Assert.AreEqual(120, n);
        }

        // --- T-01: D1 reshuffle attempts repeat the same shuffle (a TS bug the C# port keeps, D-091) ------------------------

        /// <summary>Wraps the buffer RNG and records (state before the draw, n, value) of every `NextInt`.</summary>
        private sealed class RecordingRng : IRng
        {
            private readonly IRng inner;
            private readonly Func<int> state;
            public readonly List<string> Draws = new List<string>();

            public RecordingRng(IRng inner, Func<int> state)
            {
                this.inner = inner;
                this.state = state;
            }

            public uint NextU32() => inner.NextU32();

            public double Next() => inner.Next();

            public int NextInt(int n)
            {
                int before = state();
                int v = inner.NextInt(n);
                Draws.Add(before.ToString(CultureInfo.InvariantCulture) + "," + n.ToString(CultureInfo.InvariantCulture) + "," + v.ToString(CultureInfo.InvariantCulture));
                return v;
            }
        }

        // TS probe (web b31c4e8, node 22): createInitialState(level n); reshuffleYard(s, recording bufferRng(s.buf, H.rng), [1]) →
        // attempts 1..7 each draw the same (state, n, value) list because `s.buf.set(start)` also restores the rng header (T-01).
        [TestCase(4, 7, "4004,4,3;1831569817,3,1;-631831666,2,0")]
        [TestCase(6, 7, "6006,4,1;1831571819,3,2;-631829664,2,0")]
        [TestCase(8, 7, "8008,5,3;1831573821,4,1;-631827662,3,2;1199738151,2,0")]
        [Test(Description = "K-30 D1 reshuffle with the buffer RNG: attempts 1-7 repeat one shuffle (TS bug T-01, kept bit for bit; fix in the web repo first, then RULES_VERSION and the vectors)")]
        public void K30_D1_reshuffle_buffer_RNG_attempts_repeat_the_same_draws_like_TS_T01(int levelId, int attempts, string firstAttempt)
        {
            var lvl = Compiler.LoadLevel(TestPaths.LoadLevelJson(levelId)).LevelOrThrow();
            var s = State.CreateInitialState(lvl);
            var rec = new RecordingRng(new BufferRng(s.Buf, H.Rng), () => s.Buf[H.Rng]);
            Deadlock.ReshuffleYard(s, rec, new[] { 1 });
            Assert.Greater(rec.Draws.Count, 0);
            // an attempt starts whenever the rng state is the first one again (the restore of `start`)
            string startState = rec.Draws[0].Substring(0, rec.Draws[0].IndexOf(','));
            var runs = new List<List<string>>();
            foreach (var d in rec.Draws)
            {
                if (d.StartsWith(startState + ",", StringComparison.Ordinal) && (runs.Count == 0 || runs[runs.Count - 1].Count > 0)) runs.Add(new List<string>());
                runs[runs.Count - 1].Add(d);
            }
            Assert.AreEqual(attempts, runs.Count, "attempts that drew random numbers");
            Assert.AreEqual(firstAttempt, string.Join(";", runs[0]));
            foreach (var r in runs) CollectionAssert.AreEqual(runs[0], r, "every attempt repeats the first one");
        }

        // --- R-08: K-08 sticky follow under a continuous pointer -----------------------------------------------------------

        private static JsonValue? followVectors;
        private static JsonValue FollowVectors => followVectors ??= TestPaths.LoadGolden("review_follow_vectors.json");

        private static string Node(DragNode n) => n.Ix.ToString(CultureInfo.InvariantCulture) + "," + n.Iy.ToString(CultureInfo.InvariantCulture) + "," + n.Mode.ToString(CultureInfo.InvariantCulture);

        private static string Drop(DropClass d) => d.Kind + ":" + (d.Reason ?? "") + ":" + d.Row.ToString(CultureInfo.InvariantCulture) + ":" + d.Gap.ToString(CultureInfo.InvariantCulture);

        [Test(Description = "K-08 K-05 K-44 DragSession.follow under 114 seeded pointer walks (levels 1–10, states along the golden logs): nearest node, hysteresis, tie order, path, signals and K-07 class equal TS")]
        public void K08_K05_K44_follow_under_a_continuous_pointer_equals_TS()
        {
            var drags = FollowVectors["drags"]!.Items;
            Assert.Greater(drags.Count, 100);
            var levels = new Dictionary<int, CompiledLevel>();
            var logs = new Dictionary<int, SessionAction[]>();
            int steps = 0;
            foreach (var d in drags)
            {
                int level = d["level"]!.AsInt;
                if (!levels.ContainsKey(level))
                {
                    levels[level] = Boards.LevelFile(level);
                    logs[level] = SessionActionJson.LogFromJson(TestPaths.LoadGolden("level_" + level.ToString("000", CultureInfo.InvariantCulture) + ".hand.json")["log"]!);
                }
                var log = logs[level];
                var start = (StartAction)log[0];
                var game = GameSession.Start(levels[level], start.PreBoosters, start.StreakTier);
                int prefix = d["prefix"]!.AsInt;
                for (int i = 1; i <= prefix; i++) game.Apply(log[i]);
                var s = game.State;
                string where = "level " + level + " prefix " + prefix + " piece " + d["pieceId"]!.AsInt;
                Assert.AreEqual(d["z"]!.AsString, Hash.HashHex(s), where + ": state hash");
                var attempt = Movement.TryBeginDrag(s, d["pieceId"]!.AsInt, game.Hooks.Drag);
                Assert.IsTrue(attempt.Ok, where + ": pickable");
                var ses = attempt.Session!;
                var pointer = d["pointer"]!.Items;
                var lines = d["lines"]!.Items;
                for (int t = 0; t < lines.Count; t++)
                {
                    double px = pointer[2 * t].AsDouble;
                    double py = pointer[2 * t + 1].AsDouble;
                    var nearest = ses.Nearest(px, py);
                    var f = ses.Follow(px, py);
                    string flags = (f.Changed ? "1" : "0") + (f.CrossedWall ? "1" : "0") + (f.EnteredRail ? "1" : "0") + (f.BlockedByWallHeight ? "1" : "0") + (f.BlockedCargo ? "1" : "0");
                    string line = Node(nearest) + ">" + Node(f.Node) + "|" + flags + "|" + string.Join(" ", f.Path.Select(Node)) + "|" + Drop(ses.Classify());
                    Assert.AreEqual(lines[t].AsString, line, where + " step " + t + " p = (" + JsNumber.Format(px) + ", " + JsNumber.Format(py) + ")");
                    steps++;
                }
                Assert.AreEqual(d["cancelled"]!.AsBool, Movement.IsCancelled(ses.Classify()), where + ": K-07 cancel at release");
            }
            Assert.Greater(steps, 3000);
        }

        // --- R-09: negative inputs of the frame coordinate helpers ----------------------------------------------------------

        [Ignore("REVIEW_PHASE2 R-09 (Öneri): TS uses Math.floor, C# truncating integer division; they differ for negative cells / codes")]
        [Test(Description = "K-01 cellY / dragNodeFromCode use floor division like TS for negative inputs")]
        public void K01_cell_and_node_decoding_floor_negative_inputs_like_TS()
        {
            // TS probe: [cellY(-1), cellX(-1), cellY(-9), dragNodeFromCode(-1), dragNodeFromCode(-81)]
            //         = [-1, -1, -2, {ix:-1, iy:-1, mode:-1}, {ix:-1, iy:-1, mode:-2}]
            Assert.AreEqual(-1, Coords.CellY(-1));
            Assert.AreEqual(-1, Coords.CellX(-1));
            Assert.AreEqual(-2, Coords.CellY(-9));
            var a = Coords.DragNodeFromCode(-1);
            Assert.AreEqual("-1,-1,-1", Node(a));
            var b = Coords.DragNodeFromCode(-81);
            Assert.AreEqual("-1,-1,-2", Node(b));
        }

        // --- R-10: public mutable rule tables -------------------------------------------------------------------------------

        [Ignore("REVIEW_PHASE2 R-10 (Öneri): rule tables are public mutable HashSets (TS: frozen / ReadonlySet); any caller can change K-44 / K-02 data at run time")]
        [Test(Description = "K-44 K-02 rule tables (AlwaysHeavy, ForbiddenShapes, HardLevels, SuperhardLevels) are read-only")]
        public void K44_K02_rule_tables_are_read_only()
        {
            Assert.IsTrue(((ICollection<string>)Shapes.AlwaysHeavy).IsReadOnly, "Shapes.AlwaysHeavy");
            Assert.IsTrue(((ICollection<string>)Shapes.ForbiddenShapes).IsReadOnly, "Shapes.ForbiddenShapes");
            Assert.IsTrue(((ICollection<int>)Logic.HardLevels).IsReadOnly, "Logic.HardLevels");
            Assert.IsTrue(((ICollection<int>)Logic.SuperhardLevels).IsReadOnly, "Logic.SuperhardLevels");
        }
    }
}
