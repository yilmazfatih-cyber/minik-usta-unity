#nullable enable
// TS ↔ C# equivalence of the P2 modules (level/logic validator, goals, boosters, deadlock, summary, obstacle registry): the
// lines written by tools/gen-p2-vectors.ts (TypeScript core = source of truth, D-091) are rebuilt here from the C# core and
// compared hash for hash (FNV-1a 64) on levels 1–10 along their canonical solutions (states decoded from the TS buffers, so
// the K-35 pipeline is not needed yet) and on 40 seeded full-cover boards. Covers K-45 (every fixture's issue list, message
// for message), K-30 D1/D2/D3a/Söküm (expansion counts included), K-33/K-36/K-37/K-38 target sets and pre-checks,
// K-34 hook 5, K-41/K-47/K-48 goals, K-09/K-26 summary, K-35 §7 registry activation.
// Regenerate: `node tools/gen-p2-vectors.ts /path/to/minik-usta` (and `--verbose` for every line of every state).
using System;
using System.Collections.Generic;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Obstacles;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class P2VectorTests
    {
        private static JsonValue Vectors => TestPaths.P2Vectors;

        // --- serialisation: byte for byte the strings of gen-p2-vectors.ts ---------------------------------------------------

        private static string Anc(Anchor a) => a.Ix + "," + a.Iy;
        private static string AtS(At a) => a.Zone + ":" + a.X + "," + a.Y + "," + (a.Seg.HasValue ? a.Seg.Value.ToString() : "-");
        private static string B(bool b) => b ? "1" : "0";
        private static string List<T>(IEnumerable<T> xs) => string.Join(",", xs.Select(x => x!.ToString()));
        private static string TPlace(TeardownPlace p) => p.Off ?? AtS(p.At!.Value);

        private static string EventLine(GameEvent e)
        {
            switch (e)
            {
                case DeadlockDetectedEvent d: return "deadlockDetected:" + d.Reason;
                case TruckHelpEvent t:
                    return "truckHelp:" + t.HelpKind + (t.Moves != null ? ":" + string.Join(";", t.Moves.Select(m => m.PieceId + "@" + m.From.X + "," + m.From.Y + ">" + m.To.X + "," + m.To.Y)) : "");
                case ChainReleasedEvent c: return "chainReleased:" + c.PieceId;
                case TeardownEvent td:
                    return "teardown:" + td.ToTurn + ":" + td.Cause + ":" + string.Join(";", td.Pieces.Select(p => p.PieceId + ":" + TPlace(p.From) + ">" + TPlace(p.To)));
                default: return e.TypeName;
            }
        }

        private static List<string> StateLines(GameState s, int[]? prevBuf, int seed)
        {
            var lvl = s.Lvl;
            var hooks = Registry.LevelHooks(lvl);
            var lines = new List<string>();
            lines.Add("hash:" + Hash.HashHex(s));
            string tile = Deadlock.TileRemaining(s);
            lines.Add("tile:" + tile + ":" + D3aStats.Expansions);
            lines.Add("full:" + B(Deadlock.IsFullCover(lvl)) + ":imb:" + Deadlock.ColorImbalance(s) + ":detect:" + (Deadlock.DetectDeadlock(s, null) ?? "-") + ":precheck:" + B(Deadlock.PrecheckOk(s)));
            lines.Add("nomoves:" + B(Deadlock.NoMoves(s, hooks.Drag)));
            lines.Add("holdable:" + List(Summary.HoldableIds(s, hooks)));
            lines.Add("needed:" + List(Deadlock.NeededNow(s)));
            lines.Add("blocksLeft:" + Goals.BlocksLeft(s) + ":supply:" + List(Goals.RemainingSupplyByColor(s)) + ":demand:" + List(Goals.RemainingDemandByColor(s)));
            lines.Add("material:" + B(Goals.MaterialLeft(s)) + ":segs:" + B(Goals.AllSegmentsComplete(s)) + ":extra:" + B(Goals.ExtraGoalsMet(s)) + ":met:" + B(Goals.LevelGoalsMet(s)));
            lines.Add("goals:" + string.Join(";", Goals.GoalViews(s).Select(g => g.Index + ":" + g.Kind + ":" + g.Value + "/" + g.Target + ":" + B(g.Done))));
            lines.Add("queue:" + string.Join(";", Summary.QueueEntries(s).Select(q => q.PieceId + ":" + q.Shape + ":" + q.Color)));
            var pend = Summary.PendingCounts(s);
            lines.Add("pending:" + pend.Batches + "," + pend.Blocks + ":carry:" + List(Summary.CarryIds(s)));
            lines.Add("trowelPieces:" + List(Boosters.TrowelPieces(s, hooks)));
            var t = Boosters.BoosterTargets(s, hooks, false);
            lines.Add("targets:" + B(t.Hammer) + B(t.Crane) + B(t.Undo) + B(t.PaintBrush) + B(t.Trowel));
            lines.Add("hammerTargets:" + string.Join(",", Boosters.HammerTargets(s, hooks).Select(h => h.PieceId.HasValue ? "p" + h.PieceId.Value : "o" + h.Obstacle!.Value)));
            lines.Add("hasCrane:" + B(Boosters.HasCraneTarget(s, hooks)) + ":hasPaint:" + B(Boosters.HasPaintTarget(s)));
            lines.Add("front:" + string.Join(";", Placement.BuildFront(s).Select(AtS)));
            lines.Add("rules:" + List(Registry.ActiveRuleIds(lvl)) + ":info:" + List(Registry.InfoKeysFor(lvl)) + ":hooks:" + List(hooks.DefinedKeys()));
            int P = lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
            {
                var shape = Shapes.ShapeByIndex(PieceShape(s, id));
                int color = PieceColor(s, id);
                string spots = string.Join(";", Deadlock.CorrectSpots(s, shape, color).Select(Anc));
                string crane = string.Join(";", Boosters.CraneSiteSpots(s, id).Select(c => c.Rotation + "@" + Anc(c.At)));
                lines.Add(
                    "p" + id + ":z" + PieceZone(s, id) + ":quick:" + B(Deadlock.QuickHoldable(s, id, hooks.Drag)) + ":hold:" + B(Deadlock.HoldableIgnoringMoves(s, id, hooks.Drag))
                    + ":reach:" + B(Deadlock.HasReachableCorrect(s, id, hooks.Drag, hooks.Fall)) + ":spots:" + spots + ":crane:" + crane
                    + ":paint:" + List(Boosters.PaintPartners(s, id)) + ":trowel:" + string.Join(";", Boosters.TrowelSpots(s, id).Select(Anc))
                    + ":tp:" + string.Join(";", Boosters.TrowelSpots(s, id, true).Select(Anc))
                    + ":hk:" + (Boosters.HammerTargetKind(s, HammerTarget.Piece(id), hooks) ?? "-") + ":cs:" + B(Boosters.CraneSelectable(s, id, hooks))
                    + ":ts:" + B(Boosters.TrowelSelectable(s, id, hooks)) + ":ps:" + B(Boosters.PaintSelectable(s, id)));
            }
            // K-35 step 12 on a clone (the previous state is the Söküm target)
            {
                var copy = CloneState(s);
                var events = new List<GameEvent>();
                string? cause = Deadlock.RunStep12(RuleContext.Of(copy, e => events.Add(e)), hooks,
                    new Step12Options(prevBuf != null ? (int[])prevBuf.Clone() : null, true, Hdr(copy, H.MovesLeft) <= 0, null));
                lines.Add("step12:" + (cause ?? "-") + ":" + string.Join("|", events.Select(EventLine)) + ":" + Hash.HashHex(copy));
            }
            // a direct Söküm back to the previous state
            if (prevBuf != null)
            {
                var copy = CloneState(s);
                var ev = Deadlock.Teardown(copy, (int[])prevBuf.Clone(), TeardownCauses.Tiling);
                lines.Add("teardown:" + EventLine(ev) + ":" + Hash.HashHex(copy));
            }
            // K-29 (RulesVersion 3): step 12 after an accepted +5 offer: D1 only, the Söküm target is the start of the last action
            {
                var copy = CloneState(s);
                var events = new List<GameEvent>();
                string? cause = Deadlock.RunStep12(RuleContext.Of(copy, e => events.Add(e)), hooks,
                    new Step12Options(prevBuf != null ? (int[])prevBuf.Clone() : null, false, Hdr(copy, H.MovesLeft) <= 0, null, true));
                lines.Add("step12offer:" + (cause ?? "-") + ":" + string.Join("|", events.Select(EventLine)) + ":" + Hash.HashHex(copy));
            }
            return lines;
        }

        private static string[] Strings(JsonValue? arr) => arr == null ? new string[0] : arr.Items.Select(v => v.AsString).ToArray();

        private static void CheckState(JsonValue v, GameState s, int[]? prevBuf, int seed)
        {
            string name = v["name"]!.AsString;
            var got = StateLines(s, prevBuf, seed);
            Assert.AreEqual(v["count"]!.AsInt, got.Count, name + " line count");
            if (v["lines"] != null) CollectionAssert.AreEqual(Strings(v["lines"]), got, name + " lines");
            Assert.AreEqual(v["hash"]!.AsString, Fnv1a.Hash64Hex(string.Join("\n", got)), name + " hash of the P2 query lines");
        }

        // --- fixtures ---------------------------------------------------------------------------------------------------------

        /// <summary>Groups `levels` (the shipped levels, D-099) and `archive` (Faz 2R archive, schemaVersion 1) are levels 1–10; the rest are fixture folders.</summary>
        private static bool IsLevelGroup(string group) => group == "levels" || group == "archive";

        private static JsonValue FixtureJson(string group, string name) =>
            group == "levels" ? TestPaths.LoadCurrentLevelJson(int.Parse(name.Substring("level_".Length)))
            : group == "archive" ? TestPaths.LoadLevelJson(int.Parse(name.Substring("level_".Length)))
            : TestPaths.LoadFixture(group, name);

        private static string IssueLine(Issue i, bool schemaStage) =>
            i.Code + "|" + i.Rule + "|" + i.Check + "|" + i.Severity + "|" + i.Path + (schemaStage ? "" : "|" + i.Message);

        private static string IssueLine(JsonValue i, bool schemaStage) =>
            i["code"]!.AsString + "|" + i["rule"]!.AsString + "|" + i["check"]!.AsString + "|" + i["severity"]!.AsString + "|" + i["path"]!.AsString
            + (schemaStage ? "" : "|" + i["message"]!.AsString);

        [Test(Description = "K-45 every fixture and level: the C# validator reports the TS issue list (code, rule, check, severity, path, message) under both contexts, plus tileLevel and the colour set")]
        public void K45_fixtures_and_levels_issue_lists_equal_the_TS_validator()
        {
            var i18n = new HashSet<string>(Strings(Vectors["i18nKeys"]));
            var unlock = TestPaths.LoadBoosterUnlock();
            var histories = new Dictionary<string, HashSet<string>>();
            int checkedRuns = 0;
            foreach (var f in Vectors["fixtures"]!.Items)
            {
                string group = f["group"]!.AsString;
                if (!histories.TryGetValue(group, out var history)) histories[group] = history = new HashSet<string>();
                string name = f["name"]!.AsString;
                string ctxName = f["ctx"]!.AsString;
                var json = FixtureJson(group, name);
                LogicContext ctx;
                if (ctxName == "default") ctx = new LogicContext();
                else if (IsLevelGroup(group))
                    ctx = new LogicContext(fileId: int.Parse(name.Substring("level_".Length)), previousMechanics: new HashSet<string>(history), boosterUnlock: unlock, i18nKeys: new I18nKeySets(i18n, i18n));
                else ctx = new LogicContext(previousMechanics: new HashSet<string>(), i18nKeys: new I18nKeySets(i18n, i18n), boosterUnlock: unlock);
                var r = Logic.ValidateLevelJson(json, ctx);
                string where = group + "/" + name + " (" + ctxName + ")";
                bool schemaStage = f["schema"]!.AsBool;
                Assert.AreEqual(schemaStage, r.Level == null, where + " schema stage");
                // schema-stage issues: zod's own messages are not reproduced word for word; code / rule / check / severity / path are
                CollectionAssert.AreEqual(f["issues"]!.Items.Select(i => IssueLine(i, schemaStage)).ToArray(), r.Issues.Select(i => IssueLine(i, schemaStage)).ToArray(), where + " issues");
                if (r.Level != null && f["tile"] != null) // no `tile` = no geometry (board_too_wide stops after L-28)
                {
                    var plans = Plan.BuildPlans(r.Level).Plans;
                    Assert.AreEqual(f["tile"]!.AsString, Logic.TileLevel(r.Level, plans), where + " tileLevel");
                    CollectionAssert.AreEqual(Strings(f["colors"]), Logic.LevelColorSet(r.Level, plans), where + " colour set");
                }
                if (IsLevelGroup(group) && ctxName == "full") foreach (var m in Mechanics.DeriveMechanics(LevelJson.ParseOrThrow(json))) history.Add(m);
                checkedRuns++;
            }
            Assert.AreEqual(Vectors["fixtures"]!.Count, checkedRuns);
        }

        // --- levels 1–10 along the canonical solution -------------------------------------------------------------------------

        public static IEnumerable<TestCaseData> LevelCases()
        {
            for (int n = 1; n <= 10; n++) yield return new TestCaseData(n, false).SetName("K30_K33_K36_K37_K38_K41_K47_K48_level_" + n.ToString("000") + "_states_equal_the_TS_core");
            // D-099 / K-55 item 9: the Faz 2R archive (schemaVersion 1, truck batches) keeps its TS<->C# equivalence
            for (int n = 1; n <= 10; n++) yield return new TestCaseData(n, true).SetName("K25_K55_archive_level_" + n.ToString("000") + "_states_equal_the_TS_core");
        }

        [TestCaseSource(nameof(LevelCases))]
        public void Level_states_along_the_canonical_solution_equal_the_TS_core(int n, bool archive)
        {
            JsonValue? lv = null;
            foreach (var l in Vectors["levels"]!.Items) if (l["level"]!.AsInt == n && l["archive"]!.AsBool == archive) lv = l;
            Assert.IsNotNull(lv, "vector of level " + n);
            var lvl = Compiler.LoadLevel(archive ? TestPaths.LoadLevelJson(n) : TestPaths.LoadCurrentLevelJson(n)).LevelOrThrow();
            Assert.AreEqual(lv!["levelHash"]!.AsString, LevelJson.LevelHash(lvl.Data), "level " + n + " levelHash (K-43)");
            var states = lv["states"]!.Items.ToArray();
            int[]? prev = null;
            for (int i = 0; i < states.Length; i++)
            {
                var s = DecodeState(lvl, states[i]["buf"]!.AsString);
                CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s), states[i]["name"]!.AsString + " invariants");
                CheckState(states[i], s, prev, 1000 * n + i);
                prev = (int[])s.Buf.Clone();
            }
        }

        [Test(Description = "K-47 tests/fixtures/cover.ts coverLevel(1…40): the C# Cover port builds the same levels (levelHash) and the start states answer the same P2 queries")]
        public void K47_coverLevel_1_40_equal_the_TS_core()
        {
            foreach (var c in Vectors["cover"]!.Items)
            {
                int seed = c["seed"]!.AsInt;
                var lvl = Builders.Compiled(Cover.CoverLevel(seed));
                Assert.AreEqual(c["levelHash"]!.AsString, LevelJson.LevelHash(lvl.Data), "cover " + seed + " levelHash");
                var s = CreateInitialState(lvl);
                Assert.AreEqual(c["buf"]!.AsString, EncodeState(s), "cover " + seed + " start buffer");
                CheckState(c, s, null, 5000 + seed);
            }
        }
    }
}
