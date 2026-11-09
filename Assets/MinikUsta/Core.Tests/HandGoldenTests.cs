#nullable enable
// Port of tests/golden/hand.test.ts (web repo): golden replays of the shipped levels 1–10 (D-099: schemaVersion 2, fixed
// yard, K-55) and of the Faz 2R archive levels 1–10 (schemaVersion 1, truck batches; Core.Tests/Archive2R, K-55 item 9)
// (docs/TECH_DESIGN.md §2R.10, §9.5; WP-M). `level_NNN.hand.json` is the solver's canonical solution (GDD K-50 item 4, LEVELS §2 "Kanonik çözüm") described
// move by move — block, start anchor, kind (over the wall, rail through a gap, yard shift), landing anchor, completed
// segment, truck blocks, queue — with the board geometry, the final ASCII board and the `eventLogHash`;
// `level_NNN.solver.json` is the solver's own golden (canonical log + hash). Both are written only by the web repo's
// `npm run levels:solve -- --update-golden` and copied here by tools/sync-from-web.sh.
//
// Every golden is played through `GameSession` (strict, step 12 on, as the game) and each step is checked against the
// events. The TS ↔ C# equivalence proof of the K-35 pipeline: `eventLogHash`, `finalAscii` and `levelHash` must equal
// the TypeScript values bit for bit (10/10). The independent check against the LEVELS §2 text lives in the web repo
// (tests/review/data.review.test.ts).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;
using static MinikUsta.Core.Tests.Fixtures.Boards;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class HandGoldenTests
    {
        /// <summary>Level numbers 1–10 (EN-2R-17).</summary>
        public static readonly int[] GoldenLevelIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        /// <summary>Levels 1–10 (EN-2R-17), twice: the shipped levels (D-099, `archive` false) and the Faz 2R archive (`archive` true).</summary>
        public static IEnumerable<TestCaseData> GoldenLevels()
        {
            for (int n = 1; n <= 10; n++) yield return new TestCaseData(n, false).SetName("{m}(" + n + ", shipped)");
            for (int n = 1; n <= 10; n++) yield return new TestCaseData(n, true).SetName("{m}(" + n + ", archive)");
        }

        private static CompiledLevel LevelOf(int n, bool archive) => archive ? LevelFile(n) : CurrentLevelFile(n);
        private static JsonValue LevelJsonOf(int n, bool archive) => archive ? TestPaths.LoadLevelJson(n) : TestPaths.LoadCurrentLevelJson(n);
        /// <summary>K-46: at least 60 % of the solution's correct site placements come over the wall.</summary>
        private const double YaoMin = 0.6;
        private static readonly Regex Hex16 = new Regex("^[0-9a-f]{16}$");

        /// <summary>Events that mean "not the documented solution": none may appear in a golden replay.</summary>
        private static readonly string[] Forbidden =
        {
            "moveCancelled", "placementWrong", "pieceBounced", "mortarStuck", "glassBroke", "pieceReturned", "outOfMoves",
            "deadlockDetected", "truckHelp", "teardown",
        };

        private static string Pad(int n) => n.ToString("000");

        /// <summary>One step of a golden (TS `StepSchema`).</summary>
        private sealed class GoldenStep
        {
            public string Piece = "";
            public string Ref = "";
            public string Shape = "";
            public string Color = "";
            public int[] From = new int[2];
            public string Kind = "";
            public int? Gap;
            public int[] To = new int[2];
            public int? SegmentCompleted;
            /// <summary>`[ref, x, y]` in FIFO order.</summary>
            public List<(string Ref, int X, int Y)> Delivered = new List<(string, int, int)>();
            public List<string> Queue = new List<string>();
        }

        /// <summary>The hand golden of a level (TS `GoldenSchema`), schema-checked by hand.</summary>
        private sealed class Golden
        {
            public int Level;
            public string LevelHash = "";
            public int RulesVersion;
            public int[] GeoYard = new int[2];
            public int[] GeoSite = new int[2];
            public int GeoH;
            public int GeoWall;
            public int Budget;
            public int MinMoves;
            public int MovesLeft;
            public int YaoOverWall;
            public int YaoRail;
            public List<GoldenStep> Steps = new List<GoldenStep>();
            public SessionAction[] Log = new SessionAction[0];
            public string EventLogHash = "";
            public string[] FinalAscii = new string[0];
        }

        private static int[] Xy(JsonValue v)
        {
            if (!v.IsArray || v.Count != 2) throw new FormatException("expected an [x, y] pair");
            return new[] { v[0].AsInt, v[1].AsInt };
        }

        private static Golden LoadGolden(int n, bool archive)
        {
            string file = "level_" + Pad(n) + ".hand.json";
            var j = archive ? TestPaths.LoadGolden(file) : TestPaths.LoadCurrentGolden(file);
            var g = new Golden
            {
                Level = j["level"]!.AsInt,
                LevelHash = j["levelHash"]!.AsString,
                RulesVersion = j["rulesVersion"]!.AsInt,
                GeoYard = Xy(j["geo"]!["yard"]!),
                GeoSite = Xy(j["geo"]!["site"]!),
                GeoH = j["geo"]!["H"]!.AsInt,
                GeoWall = j["geo"]!["wall"]!.AsInt,
                Budget = j["budget"]!.AsInt,
                MinMoves = j["minMoves"]!.AsInt,
                MovesLeft = j["movesLeft"]!.AsInt,
                YaoOverWall = j["yao"]!["overWall"]!.AsInt,
                YaoRail = j["yao"]!["rail"]!.AsInt,
                Log = SessionActionJson.LogFromJson(j["log"]!),
                EventLogHash = j["eventLogHash"]!.AsString,
                FinalAscii = j["finalAscii"]!.Items.Select(x => x.AsString).ToArray(),
            };
            Assert.IsTrue(Hex16.IsMatch(g.LevelHash), file + ": levelHash is 16 hex digits");
            Assert.IsTrue(Hex16.IsMatch(g.EventLogHash), file + ": eventLogHash is 16 hex digits");
            foreach (var st in j["steps"]!.Items)
            {
                var step = new GoldenStep
                {
                    Piece = st["piece"]!.AsString,
                    Ref = st["ref"]!.AsString,
                    Shape = st["shape"]!.AsString,
                    Color = st["color"]!.AsString,
                    From = Xy(st["from"]!),
                    Kind = st["kind"]!.AsString,
                    Gap = st["gap"] != null && st["gap"]!.IsNumber ? st["gap"]!.AsInt : (int?)null,
                    To = Xy(st["to"]!),
                    SegmentCompleted = st["segmentCompleted"] != null && st["segmentCompleted"]!.IsNumber ? st["segmentCompleted"]!.AsInt : (int?)null,
                };
                Assert.IsTrue(step.Kind == "overWall" || step.Kind == "rail" || step.Kind == "shift", file + ": step kind " + step.Kind);
                Assert.IsTrue(step.Color == "-" || ColorCodes.IndexOf(step.Color) >= 0, file + ": step colour " + step.Color);
                var delivered = st["delivered"];
                if (delivered != null && delivered.IsArray)
                    foreach (var d in delivered.Items) step.Delivered.Add((d[0].AsString, d[1].AsInt, d[2].AsInt));
                var queue = st["queue"];
                if (queue != null && queue.IsArray) foreach (var q in queue.Items) step.Queue.Add(q.AsString);
                g.Steps.Add(step);
            }
            return g;
        }

        /// <summary>`level_NNN.solver.json` (TS `SolverGoldenSchema`).</summary>
        private sealed class SolverGolden
        {
            public int Level;
            public string LevelHash = "";
            public int RulesVersion;
            public int SolverVersion;
            public int Min;
            public int YaoOverWall;
            public int YaoRail;
            public SessionAction[] Log = new SessionAction[0];
            public string EventLogHash = "";
        }

        private static SolverGolden LoadSolver(int n, bool archive)
        {
            string file = "level_" + Pad(n) + ".solver.json";
            var j = archive ? TestPaths.LoadGolden(file) : TestPaths.LoadCurrentGolden(file);
            return new SolverGolden
            {
                Level = j["level"]!.AsInt,
                LevelHash = j["levelHash"]!.AsString,
                RulesVersion = j["rulesVersion"]!.AsInt,
                SolverVersion = j["solverVersion"]!.AsInt,
                Min = j["min"]!.AsInt,
                YaoOverWall = j["yao"]!["overWall"]!.AsInt,
                YaoRail = j["yao"]!["rail"]!.AsInt,
                Log = SessionActionJson.LogFromJson(j["log"]!),
                EventLogHash = j["eventLogHash"]!.AsString,
            };
        }

        private static int IdOf(CompiledLevel lvl, string piece)
        {
            if (!lvl.TutorialPieceIds.TryGetValue(piece, out int id)) throw new InvalidOperationException("level " + lvl.Id + ": no piece " + piece);
            return id;
        }

        /// <summary>Bottom-left of the cells' bounding box = the piece anchor.</summary>
        private static int[] AnchorOf(At[] cells) => new[] { cells.Min(c => c.X), cells.Min(c => c.Y) };

        private static int[] YardSpot(GameState s, int id) => new[] { PieceZone(s, id), PieceX(s, id), PieceY(s, id) };

        private sealed class Played
        {
            public GameSession Session;
            public List<GameEvent> Events;

            public Played(GameSession session, List<GameEvent> events)
            {
                Session = session;
                Events = events;
            }
        }

        /// <summary>Plays the golden log move by move and checks every step (TS `playGolden`).</summary>
        private static Played PlayGolden(CompiledLevel lvl, Golden golden)
        {
            var sink = new ArraySink();
            var session = GameSession.Start(lvl, null, 0, new SessionOptions(strict: true), sink);
            var moves = golden.Log.Skip(1).ToArray();
            for (int i = 0; i < moves.Length; i++)
            {
                var step = golden.Steps[i];
                string where = "L" + golden.Level + " step " + (i + 1) + " (" + step.Piece + ")";
                if (!(moves[i] is DragMove move)) throw new InvalidOperationException(where + ": canonical solutions are drag moves only");
                var s = session.State;
                CollectionAssert.AreEqual(new[] { Zone.Yard, step.From[0], step.From[1] }, YardSpot(s, move.PieceId), where + ": start cell");

                int first = sink.Events.Count;
                var res = session.Commit(move, sink);
                var ev = sink.Events.Skip(first).ToList();
                bool last = i == moves.Length - 1;
                AssertResult(res, MoveStatuses.Applied, null, last, false, where + ": result");
                CollectionAssert.IsEmpty(ev.Where(e => Forbidden.Contains(e.TypeName)).Select(e => e.TypeName).ToArray(), where + ": no cancel / wrong placement / Söküm");

                var moved = ev.OfType<PieceMovedEvent>().Where(e => e.PieceId == move.PieceId).ToArray();
                string entry = step.Kind == "shift" ? EntryKinds.Yard : step.Kind == "rail" ? EntryKinds.Gap : EntryKinds.OverWall;
                Assert.AreEqual(1, moved.Length, where + ": one pieceMoved of the dragged block");
                Assert.AreEqual(entry, moved[0].Entry, where + ": entry");
                Assert.AreEqual(step.Kind == "rail" ? step.Gap : null, moved[0].Gap, where + ": gap");
                var correct = ev.OfType<PlacementCorrectEvent>().ToArray();
                if (step.Kind == "shift")
                {
                    Assert.AreEqual(0, correct.Length, where + ": a shift places nothing (K-10)");
                    CollectionAssert.AreEqual(new[] { Zone.Yard, step.To[0], step.To[1] }, YardSpot(s, move.PieceId), where + ": shift target");
                }
                else
                {
                    Assert.AreEqual(1, correct.Length, where + ": one correct placement");
                    var placed = correct[0];
                    Assert.AreEqual(move.PieceId, placed.PieceId, where + ": placed piece");
                    CollectionAssert.AreEqual(step.To, AnchorOf(placed.Cells), where + ": landing cell");
                    Assert.AreEqual(step.Kind == "overWall", placed.OverWall, where + ": over the wall (K-46)");
                }

                CollectionAssert.AreEqual(
                    step.SegmentCompleted.HasValue ? new[] { step.SegmentCompleted.Value } : new int[0],
                    ev.OfType<SegmentCompletedEvent>().Select(e => e.Seg).ToArray(),
                    where + ": segment completion (K-22)");

                var arrived = ev.OfType<DeliveryArrivedEvent>().SelectMany(e => e.Pieces).ToArray();
                CollectionAssert.AreEqual(step.Delivered.Select(d => IdOf(lvl, d.Ref)).ToArray(), arrived, where + ": FIFO delivery order (K-25, K-26)");
                foreach (var d in step.Delivered)
                    CollectionAssert.AreEqual(new[] { Zone.Yard, d.X, d.Y }, YardSpot(s, IdOf(lvl, d.Ref)), where + ": " + d.Ref + " delivered");
                CollectionAssert.AreEqual(step.Queue.Select(q => IdOf(lvl, q)).ToArray(), QueueIds(s), where + ": truck queue (K-26)");
                CollectionAssert.IsEmpty(Grid.StateInvariantErrors(s), where + ": state invariants");
            }
            return new Played(session, sink.Events);
        }

        [Test(Description = "golden level n transcription: piece refs, shapes, colours, geometry, budget and level hash match the level data")]
        [TestCaseSource(nameof(GoldenLevels))]
        public void Golden_level_transcription_matches_the_level_data(int n, bool archive)
        {
            var lvl = LevelOf(n, archive);
            var golden = LoadGolden(n, archive);
            var json = LevelJsonOf(n, archive);
            Assert.AreEqual(n, golden.Level);
            Assert.AreEqual(GameSession.LevelHash(LevelJson.ParseOrThrow(json)), golden.LevelHash, "levelHash of levels/level_NNN.json (K-43/4)");
            Assert.AreEqual(archive ? 3 : GameSession.RulesVersion, golden.RulesVersion); // the Faz 2R archive stays recorded at RULES_VERSION 3
            CollectionAssert.AreEqual(new[] { lvl.Geo.Wy, lvl.Geo.Hy }, golden.GeoYard, "geo.yard");
            CollectionAssert.AreEqual(new[] { lvl.Geo.Ws, lvl.Geo.Hs }, golden.GeoSite, "geo.site");
            Assert.AreEqual(lvl.Geo.H, golden.GeoH, "geo.H");
            Assert.AreEqual(lvl.WallHeight, golden.GeoWall, "geo.wall");
            Assert.AreEqual(golden.Budget, lvl.Moves, "LEVELS \"Hamle\"");
            Assert.AreEqual(golden.Budget - golden.MinMoves, golden.MovesLeft);
            Assert.IsTrue(SessionActionJson.SameAs(new StartAction(new string[0], 0), golden.Log[0]), "log[0] is the plain start action");
            Assert.IsTrue(golden.Log.Skip(1).All(a => a is DragMove), "every logged action after start is a drag");
            Assert.AreEqual(golden.MinMoves, golden.Steps.Count);
            Assert.AreEqual(golden.MinMoves + 1, golden.Log.Length);
            // K-47 / K-48: every material block is placed exactly once; a shift moves a block inside the yard
            var placed = golden.Steps.Where(st => st.Kind != "shift").Select(st => st.Ref).ToArray();
            Assert.AreEqual(placed.Length, placed.Distinct().Count(), "each block placed once");
            Assert.AreEqual(lvl.MaterialCount, placed.Length, "N = material blocks");
            Assert.AreEqual(placed.Length, golden.YaoOverWall + golden.YaoRail);
            for (int i = 0; i < golden.Steps.Count; i++)
            {
                var step = golden.Steps[i];
                string where = "L" + n + " step " + (i + 1) + " (" + step.Piece + ")";
                int id = IdOf(lvl, step.Ref);
                var action = golden.Log[i + 1] as DragMove;
                Assert.AreEqual(id, action != null ? action.PieceId : (int?)null, where + ": pieceId");
                var piece = lvl.Pieces[id];
                string color = piece.Cls == "cargo" ? "-" : ColorCodes.All[piece.ColorIndex];
                CollectionAssert.AreEqual(new[] { step.Shape, step.Color }, new[] { piece.DataShape, color }, where + ": shape, colour");
            }
        }

        [Test(Description = "K-50 K-46 level n: the hand golden is the solver golden (canonical log, min, YAO, eventLogHash)")]
        [TestCaseSource(nameof(GoldenLevels))]
        public void K50_K46_the_hand_golden_is_the_solver_golden(int n, bool archive)
        {
            var golden = LoadGolden(n, archive);
            var solver = LoadSolver(n, archive);
            Assert.AreEqual(n, solver.Level);
            Assert.AreEqual(golden.LevelHash, solver.LevelHash);
            Assert.AreEqual(archive ? 3 : GameSession.RulesVersion, solver.RulesVersion);
            Assert.IsTrue(SessionActionJson.SameLog(solver.Log, golden.Log), "solver log = hand log");
            Assert.AreEqual(golden.MinMoves, solver.Min);
            CollectionAssert.AreEqual(new[] { golden.YaoOverWall, golden.YaoRail }, new[] { solver.YaoOverWall, solver.YaoRail });
            Assert.AreEqual(golden.EventLogHash, solver.EventLogHash);
        }

        [Test(Description = "K-28 K-46 K-48 level n: the canonical solution wins in the minimum with the documented YAO, no wrong placement; eventLogHash and finalAscii equal the TypeScript golden")]
        [TestCaseSource(nameof(GoldenLevels))]
        public void K28_K46_K48_the_canonical_solution_wins_with_the_golden_event_log_hash(int n, bool archive)
        {
            var lvl = LevelOf(n, archive);
            var golden = LoadGolden(n, archive);
            var played = PlayGolden(lvl, golden);
            var session = played.Session;
            Assert.AreEqual(BoosterOutcomes.Won, session.Outcome);
            Assert.AreEqual(golden.MinMoves, session.MovesMade);
            Assert.AreEqual(golden.MovesLeft, session.MovesLeft);
            CollectionAssert.AreEqual(new[] { golden.MovesLeft }, played.Events.OfType<LevelWonEvent>().Select(e => e.MovesLeft).ToArray());
            Assert.AreEqual(0, Hdr(session.State, H.WrongCount));
            var yao = session.Yao();
            CollectionAssert.AreEqual(new[] { golden.YaoOverWall, golden.YaoRail }, new[] { yao.OverWall, yao.Rail });
            Assert.AreEqual((double)golden.YaoOverWall / (golden.YaoOverWall + golden.YaoRail), yao.Yao);
            Assert.GreaterOrEqual(yao.Yao ?? 0, YaoMin);
            Assert.AreEqual(string.Join("\n", golden.FinalAscii), Ascii.ToAscii(session.State), "L" + n + " finalAscii");
            Assert.AreEqual(
                golden.EventLogHash,
                Moves.EventLogHash(played.Events),
                "L" + n + " eventLogHash differs from the TypeScript golden (everything else green): compare the event JSON step by step with the web repo");
        }

        [Test(Description = "K-43 level n: the golden log replays bit for bit (replay = live state, same log)")]
        [TestCaseSource(nameof(GoldenLevels))]
        public void K43_the_golden_log_replays_bit_for_bit(int n, bool archive)
        {
            var lvl = LevelOf(n, archive);
            var golden = LoadGolden(n, archive);
            var played = PlayGolden(lvl, golden);
            var replayed = GameSession.Replay(lvl, golden.Log);
            Assert.IsTrue(replayed.Resumed);
            Assert.AreEqual(BoosterOutcomes.Won, replayed.Outcome);
            CollectionAssert.AreEqual(played.Session.State.Buf, replayed.State.Buf);
            Assert.IsTrue(SessionActionJson.SameLog(golden.Log, played.Session.Log), "live log = golden log");
            Assert.IsTrue(SessionActionJson.SameLog(golden.Log, replayed.Log), "replayed log = golden log");
        }
    }
}
