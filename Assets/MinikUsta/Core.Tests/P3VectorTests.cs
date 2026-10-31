#nullable enable
// TS ↔ C# equivalence of the P3 modules (moves.ts `applyMove` = the K-35 pipeline for every move kind, session.ts
// `GameSession`): tools/gen-p3-vectors.ts (TypeScript core = source of truth, D-091) plays seeded attempts through
// `GameSession.apply` — drags (reachable, raw, start node), hammer / crane / paint brush / Golden Trowel (valid and random
// targets), the legacy cell trowel, Undo, +5 offers (coins / ad), decline and exit — on levels 1–10 (after a prefix of the
// canonical solution; "burn" sequences shift blocks in the yard until the out-of-moves window opens) and on 20 seeded
// full-cover boards. The inputs are stored; this test replays them through the C# `GameSession` and rebuilds, per input,
// the same text line: result (status, reason, won, outOfMoves, teardown cause), event count + `eventLogHash` of the
// package, Zobrist hash and FNV-1a of the whole buffer, session fields, canonical JSON of the `TurnSummary`, YAO, invariant
// count. Lines are compared hash for hash per sequence; then the log (canonical JSON hash), `GameSession.Replay` (buffer,
// replayed events, outcome), the final ASCII board and the event-type / result counts of all sequences.
// Covers K-07, K-17, K-28, K-29, K-30 (Söküm inside the pipeline), K-33, K-35, K-36…K-39, K-40, K-43, K-46, K-47, K-48, K-54.
// Regenerate: `node tools/gen-p3-vectors.ts /path/to/minik-usta` (`--verbose`: every line + the canonical events of every
// package in p3_vectors.verbose.json, git-ignored); a failing sequence writes its C# lines next to it for a diff.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class P3VectorTests
    {
        private static JsonValue Vectors => TestPaths.P3Vectors;

        /// <summary>The streak bonus gen-p3-vectors.ts passes (core cannot read economy.json): tier t → t moves, a trowel from tier 2.</summary>
        private static readonly SessionOptions Opts = new SessionOptions(streakBonus: t => new StreakBonus(t, t >= 2 ? 1 : 0));

        private static readonly Dictionary<string, CompiledLevel> LevelCache = new Dictionary<string, CompiledLevel>();

        private static CompiledLevel LevelOf(JsonValue source)
        {
            string kind = source["kind"]!.AsString;
            int n = source["n"]!.AsInt;
            string key = kind + n;
            if (!LevelCache.TryGetValue(key, out var lvl))
            {
                lvl = kind == "level" ? Boards.LevelFile(n) : Builders.Compiled(Cover.CoverLevel(n));
                LevelCache[key] = lvl;
            }
            return lvl;
        }

        // --- serialisation: byte for byte the strings of gen-p3-vectors.ts ---------------------------------------------------

        private static string B(bool b) => b ? "1" : "0";

        private static string BufHash(GameState s) => Moves.Fnv1a64(string.Join(",", s.Buf));

        private static JsonValue Ints(int[] xs) => EventJson.Ints(xs);

        /// <summary>TS `canonicalJson(turnSummary)`: the plain object of summary.ts.</summary>
        private static JsonValue SummaryJson(TurnSummary t)
        {
            var queue = JsonValue.Array();
            foreach (var q in t.Queue) queue.Add(JsonValue.Object().Set("pieceId", JsonValue.Of(q.PieceId)).Set("shape", JsonValue.Of(q.Shape)).Set("color", JsonValue.Of(q.Color)));
            var bt = t.BoosterTargets;
            return JsonValue.Object()
                .Set("holdable", Ints(t.Holdable))
                .Set("queue", queue)
                .Set("pendingBatches", JsonValue.Of(t.PendingBatches))
                .Set("pendingBlocks", JsonValue.Of(t.PendingBlocks))
                .Set("blocksLeft", JsonValue.Of(t.BlocksLeft))
                .Set("neededNow", Ints(t.NeededNow))
                .Set("carryIds", Ints(t.CarryIds))
                .Set("trowelPieces", Ints(t.TrowelPieces))
                .Set("boosterTargets", JsonValue.Object()
                    .Set("hammer", JsonValue.Of(bt.Hammer)).Set("crane", JsonValue.Of(bt.Crane)).Set("undo", JsonValue.Of(bt.Undo))
                    .Set("paintBrush", JsonValue.Of(bt.PaintBrush)).Set("trowel", JsonValue.Of(bt.Trowel)));
        }

        private static string SessionLine(GameSession game)
        {
            var s = game.State;
            var offer = game.NextOffer();
            var yao = game.Yao();
            return Hash.HashHex(s) + "|" + BufHash(s) + "|" + game.Outcome + "|" + game.OffersUsed + "|" + B(game.AdOfferUsed) + "|" + (game.UndoBlock() ?? "-")
                + "|" + (offer.HasValue ? offer.Value.N + (offer.Value.AdAllowed ? "a" : "") : "-") + "|" + game.MovesLeft + "|" + game.MovesMade + "|" + game.MovesSpent
                + "|yao:" + yao.OverWall + "," + yao.Rail + "," + (yao.Yao.HasValue ? JsNumber.Format(yao.Yao.Value) : "-")
                + "|inv:" + Grid.StateInvariantErrors(s).Count;
        }

        private static string ApplyLine(int i, ActionResult r) =>
            i + "|" + r.Status + "|" + (r.Reason ?? "-") + "|" + B(r.Won) + "|" + B(r.OutOfMoves) + "|" + (r.TeardownCause ?? "-")
            + "|ev:" + r.Events.Length + ":" + Moves.EventLogHash(r.Events) + "|td:" + (r.Teardown != null ? r.Teardown.Seq.ToString() : "-")
            + "|sum:" + JsonWriter.Canonical(SummaryJson(r.Summary));

        private static void Count(Dictionary<string, int> map, string key) => map[key] = (map.TryGetValue(key, out int n) ? n : 0) + 1;

        private sealed class Played
        {
            public readonly List<string> Lines = new List<string>();
            public GameSession Game = null!;
        }

        /// <summary>Replays one stored sequence through the C# session (TS `playSequence` without the generator).</summary>
        private static Played Play(JsonValue seq, Dictionary<string, int>? coverage, Dictionary<string, int>? results)
        {
            var lvl = LevelOf(seq["source"]!);
            var pre = seq["preBoosters"]!.Items.Select(x => x.AsString).ToArray();
            int tier = seq["streakTier"]!.AsInt;
            var startSink = new ArraySink();
            var game = GameSession.Start(lvl, pre, tier, Opts, startSink);
            var p = new Played { Game = game };
            p.Lines.Add("start|ev:" + startSink.Events.Count + ":" + Moves.EventLogHash(startSink.Events) + "|" + SessionLine(game));
            if (coverage != null) foreach (var e in startSink.Events) Count(coverage, e.TypeName);
            var inputs = seq["inputs"]!.Items;
            for (int i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                string kind = input["kind"]!.AsString;
                if (kind == "exit")
                {
                    var x = game.Exit();
                    p.Lines.Add(i + "|exit|" + x.Kind + "|" + x.MovesMade + "|" + x.MovesSpent + "|" + string.Join(",", x.RefundPreBoosters) + "|" + B(x.StreakBonusConsumed) + "|" + SessionLine(game));
                    if (results != null) Count(results, "exit:" + x.Kind);
                    continue;
                }
                if (kind == "decline")
                {
                    game.DeclineOffer();
                    p.Lines.Add(i + "|decline|" + SessionLine(game));
                    if (results != null) Count(results, "decline");
                    continue;
                }
                var r = game.Apply(SessionActionJson.FromJson(input));
                if (coverage != null) foreach (var e in r.Events) Count(coverage, e.TypeName);
                if (results != null) Count(results, kind + ":" + r.Status + ":" + (r.Reason ?? "-"));
                p.Lines.Add(ApplyLine(i, r) + "|" + SessionLine(game));
            }
            if (results != null) Count(results, "outcome:" + game.Outcome + (game.OffersUsed > 0 ? ":offers" + game.OffersUsed : ""));
            return p;
        }

        private static string Where(JsonValue seq) => seq["name"]!.AsString;

        /// <summary>On a mismatch the C# lines go next to the vectors (git-ignored) for a diff with the `--verbose` TS lines.</summary>
        private static string Dump(JsonValue seq, List<string> lines)
        {
            string file = Path.Combine(TestPaths.GoldenDir, "p3_csharp." + Where(seq).Replace(' ', '_') + ".txt");
            try
            {
                File.WriteAllText(file, string.Join("\n", lines) + "\n");
            }
            catch (IOException)
            {
                return "(could not write " + file + ")";
            }
            return file;
        }

        public static IEnumerable<string> SequenceNames() => TestPaths.P3Vectors["sequences"]!.Items.Select(s => s["name"]!.AsString);

        private static JsonValue Sequence(string name) => Vectors["sequences"]!.Items.First(s => s["name"]!.AsString == name);

        [Test(Description = "K-35 vectors: the stored sequences come from the current TS core and cover every move kind")]
        public void K35_vectors_cover_every_move_kind_and_both_level_sources()
        {
            var seqs = Vectors["sequences"]!.Items;
            Assert.Greater(seqs.Count, 100);
            Assert.IsTrue(seqs.Any(s => s["source"]!["kind"]!.AsString == "level"));
            Assert.IsTrue(seqs.Any(s => s["source"]!["kind"]!.AsString == "cover"));
            var results = Vectors["results"]!;
            foreach (string k in new[] { "drag:applied:-", "hammer:applied:-", "crane:applied:-", "paint:applied:-", "goldTrowel:applied:-", "undo:applied:-", "addMoves:applied:-", "trowel:rejected:legacyTrowel", "decline", "exit:free", "exit:loss" })
                Assert.IsTrue(results[k] != null && results[k]!.AsInt > 0, "vectors contain " + k);
            var coverage = Vectors["coverage"]!;
            foreach (string t in new[] { "teardown", "levelWon", "outOfMoves", "placementWrong", "pieceBounced", "boosterRejected", "cargoSmashed", "colorsSwapped", "pieceLifted", "deliveryArrived", "siteShifted", "trowelEarned" })
                Assert.IsTrue(coverage[t] != null && coverage[t]!.AsInt > 0, "vectors contain a " + t + " event");
        }

        [Test(Description = "K-35 K-43 sequence: every package (result, eventLogHash, state hash, buffer, session fields, TurnSummary, YAO) equals the TypeScript session")]
        [TestCaseSource(nameof(SequenceNames))]
        public void K35_K43_session_sequence_equals_the_TypeScript_session(string name)
        {
            var seq = Sequence(name);
            var lvl = LevelOf(seq["source"]!);
            Assert.AreEqual(seq["levelHash"]!.AsString, GameSession.LevelHash(lvl.Data), name + ": levelHash (K-43/4)");
            var p = Play(seq, null, null);
            var keep = seq["lines"];
            if (keep != null && keep.IsArray)
            {
                // format anchor: the first sequence keeps its TS lines
                var expected = keep.Items.Select(x => x.AsString).ToArray();
                for (int i = 0; i < Math.Min(expected.Length, p.Lines.Count); i++)
                    Assert.AreEqual(expected[i], p.Lines[i], name + " line " + i);
            }
            string hash = Moves.Fnv1a64(string.Join("\n", p.Lines));
            if (hash != seq["hash"]!.AsString || p.Lines.Count != seq["count"]!.AsInt)
                Assert.Fail(name + ": lines differ from the TS vectors (" + p.Lines.Count + " vs " + seq["count"]!.AsInt + " lines); C# lines in " + Dump(seq, p.Lines)
                    + "; rerun `node tools/gen-p3-vectors.ts … --verbose` and diff");

            var log = p.Game.Log;
            Assert.AreEqual(seq["logLength"]!.AsInt, log.Count, name + ": log length");
            Assert.AreEqual(seq["logHash"]!.AsString, Moves.Fnv1a64(JsonWriter.Canonical(SessionActionJson.ToJson(log))), name + ": log (canonical JSON)");
            Assert.AreEqual(seq["finalAscii"]!.AsString, Ascii.ToAscii(p.Game.State), name + ": final ASCII");

            var replaySink = new ArraySink();
            var replayed = GameSession.Replay(lvl, log, Opts, replaySink);
            Assert.IsTrue(replayed.Resumed);
            var rep = seq["replay"]!;
            Assert.AreEqual(rep["buf"]!.AsString, BufHash(replayed.State), name + ": replay buffer (K-43 E-38)");
            Assert.AreEqual(rep["events"]!.AsString, Moves.EventLogHash(replaySink.Events), name + ": replayed events");
            Assert.AreEqual(rep["outcome"]!.AsString, replayed.Outcome, name + ": replayed outcome");
            // the replay rebuilds the live attempt bit for bit (an exit / decline is not an action of the log)
            if (p.Game.Outcome != BoosterOutcomes.Exited && p.Game.Outcome != BoosterOutcomes.Lost)
                CollectionAssert.AreEqual(p.Game.State.Buf, replayed.State.Buf, name + ": replay = live");
        }

        [Test(Description = "K-35 vectors: event types and results over all sequences equal the TypeScript counts")]
        public void K35_event_type_and_result_counts_equal_the_TypeScript_counts()
        {
            var coverage = new Dictionary<string, int>();
            var results = new Dictionary<string, int>();
            foreach (var seq in Vectors["sequences"]!.Items) Play(seq, coverage, results);
            var tsCoverage = Vectors["coverage"]!.Members.ToDictionary(kv => kv.Key, kv => kv.Value.AsInt);
            var tsResults = Vectors["results"]!.Members.ToDictionary(kv => kv.Key, kv => kv.Value.AsInt);
            CollectionAssert.AreEquivalent(tsCoverage.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray(), coverage.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray(), "event types");
            CollectionAssert.AreEquivalent(tsResults.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray(), results.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToArray(), "results");
        }

        [Test(Description = "K-35 NULL_SINK: every sequence replayed without events reaches the same buffer")]
        public void K35_NullSink_sequences_reach_the_same_buffers()
        {
            foreach (var seq in Vectors["sequences"]!.Items)
            {
                var p = Play(seq, null, null);
                var silent = GameSession.Replay(LevelOf(seq["source"]!), p.Game.Log, Opts);
                Assert.AreEqual(seq["replay"]!["buf"]!.AsString, BufHash(silent.State), Where(seq));
            }
        }
    }
}
