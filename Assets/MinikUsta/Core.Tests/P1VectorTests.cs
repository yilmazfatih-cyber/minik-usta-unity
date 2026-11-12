#nullable enable
// TS ↔ C# equivalence of the P1 modules (movement, gravity, placement, delivery) on the 10 shipped levels and on 45
// seeded random boards of the 10 geometries (TECH §2R.1): the lines written by tools/gen-p1-vectors.ts (TypeScript core =
// source of truth, D-091) are rebuilt here from the C# core and compared hash for hash (FNV-1a 64). Covers K-04, K-05,
// K-07 (release table), K-08 (BFS distances, sticky follow with hysteresis — floating point included), K-09 (pick
// outcomes), K-11/K-12/K-13 (R), K-16/K-34 (verdicts of every site landing), K-17 (return target and yard drop rows),
// K-18 (shadow tones), K-19 normal gravity (computeFall path, direction, distance), yard gravity (settleYard motions and
// the state hash after the settle), K-33 (build front), K-44 (cargo flag).
// Regenerate: `node tools/gen-p1-vectors.ts /path/to/minik-usta` (and `--verbose` for every line of every piece).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;
using static MinikUsta.Core.State;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class P1VectorTests
    {
        private static JsonValue Vectors => TestPaths.P1Vectors;

        // --- serialisation: byte for byte the strings of gen-p1-vectors.ts ---------------------------------------------------

        private static string Key(DragNode n) => n.Mode + ":" + n.Ix + "," + n.Iy;
        private static string Anc(Anchor a) => a.Ix + "," + a.Iy;
        private static string AtS(At a) => a.Zone + ":" + a.X + "," + a.Y + "," + (a.Seg.HasValue ? a.Seg.Value.ToString() : "-");
        private static string CellS(BoardCell c) => c.X + "," + c.Y;
        private static string VerdictS(Verdict v) => (v.Ok ? "1" : "0") + "[" + string.Join(",", v.Reasons) + "]{" + string.Join(";", v.MissingSupport.Select(AtS)) + "}";
        private static string PlaceS(PiecePlace p) => p.Zone + "," + p.X + "," + p.Y + "," + p.Seg;
        private static string Half(int n) => (n / 2.0).ToString(CultureInfo.InvariantCulture);
        private static string FlagsS(FollowResult f) =>
            (f.Changed ? "1" : "0") + (f.CrossedWall ? "W" : "") + (f.EnteredRail ? "R" : "") + (f.BlockedByWallHeight ? "H" : "") + (f.BlockedCargo ? "C" : "") + ":" + f.Path.Length;

        private sealed class PieceLines
        {
            public string Pick = "";
            public DragSession? Session;
            public readonly List<string> Lines = new List<string>();
            public readonly List<string> Follow = new List<string>();
        }

        private static PieceLines PieceVector(GameState s, int id)
        {
            var o = new PieceLines();
            var a = Movement.TryBeginDrag(s, id);
            if (!a.Ok)
            {
                o.Pick = a.Reason!;
                return o;
            }
            o.Pick = "ok";
            var d = a.Session!;
            o.Session = d;
            var lvl = s.Lvl;
            var geo = lvl.Geo;
            foreach (var n in d.ReachableNodes())
            {
                var cls = d.Classify(n);
                string line = Key(n) + ":" + d.DistanceFromStart(n) + ":" + cls.Kind + ":" + (cls.Reason ?? "-") + ":" + cls.Row + ":" + (cls.Gap >= 0 ? cls.Gap.ToString() : "-");
                if (cls.Kind == DropKinds.SiteFree || cls.Kind == DropKinds.SiteRail)
                {
                    var f = Gravity.ComputeFall(s, id, n);
                    string effect = f.Effect.Kind == LandingEffect.KindBreak ? "break" + f.Effect.Penalty : f.Effect.Kind;
                    line += "|" + f.Mode + "|" + Anc(f.Landing) + "|" + f.Dir + "|" + f.Distance + "|" + f.Drift + "|" + string.Join(">", f.Path.Select(Anc))
                        + "|" + string.Join(">", f.Cells.Select(CellS)) + "|" + VerdictS(f.Verdict) + "|" + (f.TouchesHidden ? "1" : "0") + "|" + effect
                        + "|" + Gravity.ShadowInfo(f, lvl.Difficulty).Tone;
                }
                o.Lines.Add(line);
            }
            var drops = new List<int>();
            for (int ix = 0; ix + d.Shape.W <= geo.Wy; ix++) drops.Add(Placement.DropIntoYard(s, d.Shape, ix));
            o.Lines.Add("yard:" + Placement.YardDropRow(geo, d.Shape) + ":" + string.Join(",", drops));
            int zone = PieceZone(s, id);
            if (zone == Zone.Yard || zone == Zone.Site)
            {
                var start = PiecePlace.Site(geo.SiteX, 0, Grid.VisibleSegment(s));
                var rt = Placement.ReturnTarget(s, id, new ReturnOptions(true, start));
                o.Lines.Add("rt:" + rt.Step + ":" + PlaceS(rt.To) + ":" + (rt.DropFrom.HasValue ? Anc(rt.DropFrom.Value) : "-"));
                var rt0 = Placement.ReturnTarget(s, id);
                o.Lines.Add("rt0:" + rt0.Step + ":" + PlaceS(rt0.To) + ":" + (rt0.DropFrom.HasValue ? Anc(rt0.DropFrom.Value) : "-"));
            }
            // K-08 sticky follow sweep on a fresh session: anchor points on a 0.5-cell grid, row-major, bottom row first
            var f2 = Movement.TryBeginDrag(s, id).Session!;
            for (int py2 = 0; py2 < 2 * geo.Rows; py2++)
            {
                for (int px2 = 0; px2 < 2 * geo.Cols; px2++)
                {
                    var r = f2.Follow(px2 / 2.0, py2 / 2.0);
                    o.Follow.Add(Half(px2) + "," + Half(py2) + ">" + Key(r.Node) + ":" + FlagsS(r));
                }
            }
            o.Follow.Add("end:" + Key(f2.Current));
            return o;
        }

        private static string[] Strings(JsonValue? arr) => arr == null ? new string[0] : arr.Items.Select(v => v.AsString).ToArray();
        private static int[] Ints(JsonValue? arr) => arr == null ? new int[0] : arr.Items.Select(v => v.AsInt).ToArray();

        /// <summary>Compares one board (fresh initial state → settle → every piece) with its vector.</summary>
        private static void CheckBoard(JsonValue b, CompiledLevel lvl)
        {
            string name = b["name"]!.AsString;
            Assert.AreEqual(b["geo"]!.AsString, lvl.Geo.Wy + "x" + lvl.Geo.Hy + "|" + lvl.Geo.Ws + "x" + lvl.Geo.Hs, name + " geo");
            Assert.AreEqual(b["yardGravity"]!.AsBool, lvl.Gravity.Yard, name + " yard gravity");
            Assert.AreEqual(b["wallHeight"]!.AsInt, lvl.WallHeight, name + " wall height");
            CollectionAssert.AreEqual(Strings(b["gaps"]), lvl.Gaps.Select(g => g.Type + ":" + g.Y + ":" + g.Size).ToArray(), name + " gaps");
            var s = CreateInitialState(lvl);
            var motions = lvl.Gravity.Yard
                ? Gravity.SettleYard(s).Select(m => m.Kind + ":" + m.Id + ":" + Anc(m.From) + ">" + Anc(m.To) + ":" + m.Dy).ToArray()
                : new string[0];
            CollectionAssert.AreEqual(Strings(b["motions"]), motions, name + " settleYard motions");
            Assert.AreEqual(b["hashAfterSettle"]!.AsString, Hash.HashHex(s), name + " state hash after the settle");
            Assert.AreEqual(b["movesLeft"]!.AsInt, Hdr(s, H.MovesLeft), name + " movesLeft");
            CollectionAssert.AreEqual(Strings(b["front"]), Placement.BuildFront(s).Select(AtS).ToArray(), name + " build front");
            CollectionAssert.AreEqual(Ints(b["masks"]), Gravity.SiteColumnMasks(s), name + " site column masks");
            var pieces = b["pieces"]!.Items.ToArray();
            Assert.AreEqual(pieces.Length, lvl.Layout.Counts.Pieces, name + " piece count");
            for (int id = 0; id < pieces.Length; id++)
            {
                var p = pieces[id];
                string where = name + " piece " + id;
                Assert.AreEqual(id, p["id"]!.AsInt, where);
                var got = PieceVector(s, id);
                Assert.AreEqual(p["pick"]!.AsString, got.Pick, where + " pick");
                if (got.Session == null) continue;
                var d = got.Session;
                Assert.AreEqual(p["cargo"]!.AsBool, d.Cargo, where + " cargo");
                Assert.AreEqual(p["canCrossWall"]!.AsBool, d.CanCrossWall, where + " canCrossWall");
                Assert.AreEqual(p["canEnterRail"]!.AsBool, d.CanEnterRail, where + " canEnterRail");
                Assert.AreEqual(p["reachableCount"]!.AsInt, d.ReachableCount, where + " reachableCount");
                CollectionAssert.AreEqual(Strings(p["starts"]), d.StartNodes.Select(Key).ToArray(), where + " start nodes");
                Assert.AreEqual(p["nodes"]!.AsInt, got.Lines.Count, where + " line count");
                if (p["lines"] != null) CollectionAssert.AreEqual(Strings(p["lines"]), got.Lines, where + " lines");
                if (p["follow"] != null) CollectionAssert.AreEqual(Strings(p["follow"]), got.Follow, where + " follow");
                Assert.AreEqual(p["hash"]!.AsString, Fnv1a.Hash64Hex(string.Join("\n", got.Lines)), where + " hash of R / classes / falls / verdicts / drops / return targets");
                Assert.AreEqual(p["followHash"]!.AsString, Fnv1a.Hash64Hex(string.Join("\n", got.Follow)), where + " hash of the K-08 follow sweep");
            }
        }

        [Test(Description = "P1 vectors: the file comes from the same web commit as the P0 vectors and covers 10 shipped levels + 10 geometries × 5 random boards + 10 Faz 2R archive levels")]
        public void P1_vectors_file_is_consistent()
        {
            Assert.AreEqual(TestPaths.Vectors["generatedFrom"]!["webCommit"]!.AsString, Vectors["generatedFrom"]!["webCommit"]!.AsString, "p0 and p1 vectors from different web commits");
            Assert.AreEqual("tools/gen-p1-vectors.ts", Vectors["generatedFrom"]!["generator"]!.AsString);
            Assert.AreEqual(10 + GeometryTests.G.Length * Vectors["random"]!["perGeometry"]!.AsInt + 10, Vectors["boards"]!.Items.Count());
            Assert.IsNotNull(Vectors["boards"]![0]["pieces"]![0]["lines"], "level 1 keeps the full lines as the format anchor");
        }

        [TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6), TestCase(7), TestCase(8), TestCase(9), TestCase(10)]
        [Test(Description = "K-04 K-05 K-07 K-08 K-09 K-11 K-12 K-13 K-16 K-17 K-18 K-19 K-33 K-34 K-44 every pick, node, class, fall, verdict, shadow, follow step, drop row and return target of level N equals the TypeScript core")]
        public void Kxx_level_N_movement_gravity_placement_results_equal_the_TypeScript_core(int n)
        {
            var b = Vectors["boards"]!.Items.ElementAt(n - 1);
            Assert.AreEqual("level_" + n.ToString("000"), b["name"]!.AsString);
            CheckBoard(b, Compiler.Compile(LevelJson.ParseOrThrow(TestPaths.LoadCurrentLevelJson(n))));
        }

        [TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6), TestCase(7), TestCase(8), TestCase(9), TestCase(10)]
        [Test(Description = "K-25 K-26 K-55 item 9 the same on the Faz 2R archive level N (schemaVersion 1, truck batches): every pick, node, class, fall, verdict, shadow, follow step, drop row and return target equals the TypeScript core")]
        public void K25_K55_archive_level_N_movement_gravity_placement_results_equal_the_TypeScript_core(int n)
        {
            int index = 10 + GeometryTests.G.Length * Vectors["random"]!["perGeometry"]!.AsInt + (n - 1);
            var b = Vectors["boards"]!.Items.ElementAt(index);
            Assert.AreEqual("archive_level_" + n.ToString("000"), b["name"]!.AsString);
            CheckBoard(b, Compiler.Compile(LevelJson.ParseOrThrow(TestPaths.LoadLevelJson(n))));
        }

        [Test(Description = "K-49 K-08 K-17 K-19 the same on 50 seeded random boards of the 10 geometries (yard gravity settle included): GeometryTests.RandomLevel draws the TS randomLevel sequence")]
        public void K49_random_boards_of_the_10_geometries_equal_the_TypeScript_core()
        {
            var random = Vectors["random"]!;
            var rng = new Mulberry32(random["seed"]!.AsInt);
            int per = random["perGeometry"]!.AsInt;
            int levelId = random["levelId"]!.AsInt;
            var boards = Vectors["boards"]!.Items.Skip(10).Take(GeometryTests.G.Length * per).ToArray();
            int i = 0;
            foreach (var c in GeometryTests.G)
            {
                for (int k = 0; k < per; k++)
                {
                    var b = boards[i++];
                    Assert.AreEqual("random " + c.Label + " #" + k, b["name"]!.AsString);
                    CheckBoard(b, GeometryTests.RandomLevel(c, rng, levelId));
                }
            }
            Assert.AreEqual(boards.Length, i);
        }
    }
}
