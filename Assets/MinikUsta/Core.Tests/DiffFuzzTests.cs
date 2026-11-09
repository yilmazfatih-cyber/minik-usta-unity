#nullable enable
// Differential fuzz against the TypeScript core (P4). tools/diff/ts-oracle.mjs (node 22, imports the web repo's
// src/core/**: the source of truth, D-091) plays seeded random VALID action sequences through the TS `GameSession` —
// drags to reachable, non-cancelling nodes of the `DragSession`, hammer / crane / paint brush / Golden Trowel on valid
// targets, Undo (K-39), +5 offers (K-29), decline, exit (K-43), pre-level boosters (K-40) — on levels 1–10, the
// tests/fixtures/cover.ts boards and random levels in the 9 K-49 geometries, and writes after every action the Zobrist
// hash, the buffer hash, the event count + eventLogHash + canonical JSON of every event, the ASCII board and the session
// fields to tools/diff/out/*.json (git-ignored). This test replays the same actions through the C# `GameSession` and
// compares every step; the first divergence fails with the step, the action, the field, both ASCII boards side by side
// and the first differing event. Without the oracle output the cases are ignored (run `node tools/diff/ts-oracle.mjs`).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class DiffFuzzTests
    {
        private const string Missing = "(no oracle output)";

        public static string OutDir => Path.Combine(TestPaths.RepoRoot, "tools", "diff", "out");
        private static string ManifestPath => Path.Combine(OutDir, "manifest.json");

        /// <summary>The streak bonus ts-oracle.mjs passes (core cannot read economy.json): tier t → t moves, a trowel from tier 2.</summary>
        private static readonly SessionOptions Opts = new SessionOptions(streakBonus: t => new StreakBonus(t, t >= 2 ? 1 : 0));

        private static JsonValue? manifest;
        private static JsonValue Manifest => manifest ??= TestPaths.ReadJson(ManifestPath);

        private static readonly Dictionary<string, JsonValue> Files = new Dictionary<string, JsonValue>();
        private static readonly Dictionary<string, CompiledLevel> Levels = new Dictionary<string, CompiledLevel>();

        private static JsonValue SourceFile(string file)
        {
            if (!Files.TryGetValue(file, out var j))
            {
                j = TestPaths.ReadJson(Path.Combine(OutDir, file));
                Files[file] = j;
            }
            return j;
        }

        /// <summary>
        /// The level of a source file: the shipped levels 1–10 (`level`, D-099 schemaVersion 2) from StreamingAssets and the Faz 2R
        /// archive levels (`archive`, schemaVersion 1) from Core.Tests/Archive2R (both `Compiler.LoadLevel`), the others from the stored level data.
        /// </summary>
        private static CompiledLevel LevelOf(string file)
        {
            if (Levels.TryGetValue(file, out var lvl)) return lvl;
            var src = SourceFile(file);
            var source = src["source"]!;
            string kind = source["kind"]!.AsString;
            lvl = kind == "level" ? Boards.CurrentLevelFile(source["n"]!.AsInt)
                : kind == "archive" ? Boards.LevelFile(source["n"]!.AsInt)
                : Compiler.Compile(LevelJson.ParseOrThrow(src["level"]!));
            Levels[file] = lvl;
            return lvl;
        }

        public static IEnumerable<TestCaseData> Sequences()
        {
            if (!File.Exists(ManifestPath))
            {
                yield return new TestCaseData(Missing, 0).SetName("K35_K43_diff_fuzz_no_oracle_output");
                yield break;
            }
            foreach (var f in Manifest["files"]!.Items)
            {
                string file = f["file"]!.AsString;
                var names = f["sequences"]!.Items;
                for (int i = 0; i < names.Count; i++)
                    yield return new TestCaseData(file, i).SetName("K35_K43_diff_fuzz_" + names[i].AsString.Replace(' ', '_'));
            }
        }

        private static void RequireOracle()
        {
            if (!File.Exists(ManifestPath))
                Assert.Ignore("tools/diff/out/manifest.json is missing: run `node tools/diff/ts-oracle.mjs /path/to/minik-usta` first");
        }

        // --- the C# record of one step: the same fields the oracle writes ---------------------------------------------

        private sealed class Step
        {
            public string St = "";
            public string? Rs;
            public bool? W;
            public bool? Oom;
            public string? Td;
            public string Z = "";
            public string B = "";
            public int N;
            public string Ev = "";
            public string[] E = Array.Empty<string>();
            public string Ascii = "";
            public string Oc = "";
            public int Ml;
            public int Mm;
            public int Ms;
            public int Ou;
            public string? Ub;
            public ExitResult? X;
        }

        private static Step Snapshot(GameSession game, IReadOnlyList<GameEvent> events, string status)
        {
            var s = game.State;
            return new Step
            {
                St = status,
                Z = Hash.HashHex(s),
                B = Moves.Fnv1a64(string.Join(",", s.Buf)),
                N = events.Count,
                Ev = Moves.EventLogHash(events),
                E = events.Select(e => JsonWriter.Canonical(EventJson.ToJson(e))).ToArray(),
                Ascii = Ascii.ToAscii(s, ids: true),
                Oc = game.Outcome,
                Ml = game.MovesLeft,
                Mm = game.MovesMade,
                Ms = game.MovesSpent,
                Ou = game.OffersUsed,
                Ub = game.UndoBlock(),
            };
        }

        private static string? StrOrNull(JsonValue? j) => j == null || j.IsNull ? null : j.AsString;

        // --- divergence report -----------------------------------------------------------------------------------------

        private static string SideBySide(string ts, string cs)
        {
            var a = ts.Split('\n');
            var b = cs.Split('\n');
            int w = Math.Max(12, a.Max(l => l.Length));
            var sb = new StringBuilder();
            sb.Append("  ").Append("TS".PadRight(w)).Append("  |  C#\n");
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                string l = i < a.Length ? a[i] : "";
                string r = i < b.Length ? b[i] : "";
                sb.Append(l == r ? "  " : "* ").Append(l.PadRight(w)).Append("  |  ").Append(r).Append('\n');
            }
            return sb.ToString();
        }

        private static string EventDiff(string[] ts, string[] cs)
        {
            int n = Math.Max(ts.Length, cs.Length);
            for (int k = 0; k < n; k++)
            {
                string? a = k < ts.Length ? ts[k] : null;
                string? b = k < cs.Length ? cs[k] : null;
                if (a == b) continue;
                return "events: TS " + ts.Length + ", C# " + cs.Length + "; first difference at #" + k + "\n  TS: " + (a ?? "(none)") + "\n  C#: " + (b ?? "(none)");
            }
            return "events: identical (" + ts.Length + ")";
        }

        private static void Diverged(string seq, int step, JsonValue action, string field, string ts, string cs, JsonValue tsStep, Step mine)
        {
            var tsEvents = tsStep["e"]!.Items.Select(x => x.AsString).ToArray();
            Assert.Fail("diff fuzz: " + seq + " diverged at step " + step + " on `" + field + "`\n"
                + "action: " + JsonWriter.Canonical(action) + "\n"
                + "TS: " + ts + "\nC#: " + cs + "\n"
                + EventDiff(tsEvents, mine.E) + "\n"
                + "board after the step:\n" + SideBySide(tsStep["ascii"]!.AsString, mine.Ascii));
        }

        private static void Compare(string seq, int step, JsonValue action, JsonValue t, Step c)
        {
            void Eq(string field, string? ts, string? cs)
            {
                if (ts != cs) Diverged(seq, step, action, field, ts ?? "null", cs ?? "null", t, c);
            }
            string B(bool? b) => b.HasValue ? (b.Value ? "true" : "false") : "null";
            string? TsBool(string k) => t[k] == null || t[k]!.IsNull ? "null" : (t[k]!.AsBool ? "true" : "false");

            Eq("st (status)", t["st"]!.AsString, c.St);
            if (c.St != "start" && c.St != "exit" && c.St != "decline")
            {
                Eq("rs (reason)", StrOrNull(t["rs"]), c.Rs);
                Eq("w (won)", TsBool("w"), B(c.W));
                Eq("oom (outOfMoves)", TsBool("oom"), B(c.Oom));
                Eq("td (teardown cause)", StrOrNull(t["td"]), c.Td);
            }
            if (c.X != null)
            {
                var x = t["x"]!;
                Eq("x.kind", x["kind"]!.AsString, c.X.Kind);
                Eq("x.movesMade", x["movesMade"]!.AsInt.ToString(), c.X.MovesMade.ToString());
                Eq("x.movesSpent", x["movesSpent"]!.AsInt.ToString(), c.X.MovesSpent.ToString());
                Eq("x.refundPreBoosters", string.Join(",", x["refundPreBoosters"]!.Items.Select(i => i.AsString)), string.Join(",", c.X.RefundPreBoosters));
                Eq("x.streakBonusConsumed", x["streakBonusConsumed"]!.AsBool ? "true" : "false", c.X.StreakBonusConsumed ? "true" : "false");
            }
            Eq("n (event count)", t["n"]!.AsInt.ToString(), c.N.ToString());
            var te = t["e"]!.Items;
            for (int k = 0; k < Math.Min(te.Count, c.E.Length); k++) Eq("e[" + k + "] (event)", te[k].AsString, c.E[k]);
            Eq("ev (eventLogHash)", t["ev"]!.AsString, c.Ev);
            Eq("z (Zobrist hash)", t["z"]!.AsString, c.Z);
            Eq("b (buffer hash)", t["b"]!.AsString, c.B);
            Eq("ascii", t["ascii"]!.AsString, c.Ascii);
            Eq("oc (outcome)", t["oc"]!.AsString, c.Oc);
            Eq("ml (moves left)", t["ml"]!.AsInt.ToString(), c.Ml.ToString());
            Eq("mm (moves made)", t["mm"]!.AsInt.ToString(), c.Mm.ToString());
            Eq("ms (moves spent)", t["ms"]!.AsInt.ToString(), c.Ms.ToString());
            Eq("ou (offers used)", t["ou"]!.AsInt.ToString(), c.Ou.ToString());
            Eq("ub (undo block)", StrOrNull(t["ub"]), c.Ub);
        }

        // --- tests -------------------------------------------------------------------------------------------------------

        [Test(Description = "K-35 diff fuzz: the oracle output has at least 200 sequences of at most 40 actions with every action kind")]
        public void K35_K29_K39_diff_fuzz_oracle_output_has_200_sequences_and_every_action_kind()
        {
            RequireOracle();
            var m = Manifest;
            Assert.GreaterOrEqual(m["sequences"]!.AsInt, 200, "sequences");
            Assert.LessOrEqual(m["generatedFrom"]!["moves"]!.AsInt, 40, "move cap");
            int listed = m["files"]!.Items.Sum(f => f["sequences"]!.Items.Count);
            Assert.AreEqual(m["sequences"]!.AsInt, listed, "manifest file list");
            foreach (string k in new[] { "drag", "hammer", "crane", "paint", "goldTrowel", "undo", "addMoves", "decline", "exit" })
                Assert.IsTrue(m["actions"]![k] != null && m["actions"]![k]!.AsInt > 0, "the fuzz contains a " + k + " action");
            // every source kind: shipped levels, Faz 2R archive levels, cover boards, K-49 geometries, truck-queue boards, scripted scenarios (P4 item 3), v3 trait boards (pallet, two-tone, wrap, bonus, slippery)
            var kinds = m["files"]!.Items.Select(f => f["source"]!["kind"]!.AsString).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "archive", "cover", "geo", "level", "scenario", "truck", "v3" }, kinds);
            foreach (string t in new[] { "teardown", "levelWon", "outOfMoves", "deliveryArrived", "deliveryQueued", "siteShifted", "placementWrong", "trowelEarned", "deadlockDetected", "truckHelp", "chainReleased", "cargoSmashed", "colorsSwapped", "unwrapped" })
                Assert.IsTrue(m["events"]![t] != null && m["events"]![t]!.AsInt > 0, "the fuzz contains a " + t + " event");
        }

        [Test(Description = "K-35 K-43 diff fuzz: every step of a seeded TS sequence (Zobrist hash, buffer, events, ASCII, session) equals the C# session")]
        [TestCaseSource(nameof(Sequences))]
        public void K35_K43_diff_fuzz_sequence_equals_the_TypeScript_core(string file, int index)
        {
            if (file == Missing) RequireOracle();
            var src = SourceFile(file);
            var lvl = LevelOf(file);
            var seq = src["sequences"]!.Items[index];
            string name = seq["name"]!.AsString;
            Assert.AreEqual(src["levelHash"]!.AsString, GameSession.LevelHash(lvl.Data), name + ": levelHash (K-43/4) — C# compiled a different level");

            var steps = seq["steps"]!.Items;
            var startAction = steps[0]["a"]!;
            var pre = startAction["preBoosters"]!.Items.Select(x => x.AsString).ToArray();
            var startSink = new ArraySink();
            var game = GameSession.Start(lvl, pre, startAction["streakTier"]!.AsInt, Opts, startSink);
            Compare(name, 0, startAction, steps[0], Snapshot(game, startSink.Events, "start"));

            for (int i = 1; i < steps.Count; i++)
            {
                var t = steps[i];
                var a = t["a"]!;
                string kind = a["kind"]!.AsString;
                Step c;
                if (kind == "exit")
                {
                    var x = game.Exit();
                    c = Snapshot(game, Array.Empty<GameEvent>(), "exit");
                    c.X = x;
                }
                else if (kind == "decline")
                {
                    game.DeclineOffer();
                    c = Snapshot(game, Array.Empty<GameEvent>(), "decline");
                }
                else
                {
                    ActionResult r;
                    try
                    {
                        r = game.Apply(SessionActionJson.FromJson(a));
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail("diff fuzz: " + name + " step " + i + " throws in C# (" + ex.GetType().Name + ": " + ex.Message + ")\naction: " + JsonWriter.Canonical(a)
                            + "\nboard before the step (TS):\n" + steps[i - 1]["ascii"]!.AsString);
                        return;
                    }
                    c = Snapshot(game, r.Events, r.Status);
                    c.Rs = r.Reason;
                    c.W = r.Won;
                    c.Oom = r.OutOfMoves;
                    c.Td = r.TeardownCause;
                }
                Compare(name, i, a, t, c);
            }

            var log = game.Log;
            Assert.AreEqual(seq["log"]!["length"]!.AsInt, log.Count, name + ": log length");
            Assert.AreEqual(seq["log"]!["hash"]!.AsString, Moves.Fnv1a64(JsonWriter.Canonical(SessionActionJson.ToJson(log))), name + ": log (canonical JSON)");
            var replaySink = new ArraySink();
            var replayed = GameSession.Replay(lvl, log, Opts, replaySink);
            Assert.AreEqual(seq["replay"]!["b"]!.AsString, Moves.Fnv1a64(string.Join(",", replayed.State.Buf)), name + ": replayed buffer (K-43)");
            Assert.AreEqual(seq["replay"]!["ev"]!.AsString, Moves.EventLogHash(replaySink.Events), name + ": replayed events (K-43)");
            Assert.AreEqual(seq["replay"]!["oc"]!.AsString, replayed.Outcome, name + ": replayed outcome (K-43)");
        }
    }
}
