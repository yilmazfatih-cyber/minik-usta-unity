#nullable enable
// Port of tests/fixtures/builders.ts (web repo): `Level(spec)` returns a real, schema-parsed LevelData (the spec is written
// as level JSON and run through LevelJson, so the schema defaults apply exactly as in TS); `Compiled` / `InitialState` go
// one step further; `ExpectAscii` compares a state with Appendix A text. Builders only run the schema, not the logic checks.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;
using MinikUsta.Core.Level;
using NUnit.Framework;

namespace MinikUsta.Core.Tests.Fixtures
{
    /// <summary>`[shape, colour, x, y, flags?, wetMoves?]` — anchor = bottom-left of the bounding box.</summary>
    public sealed class P
    {
        public readonly string Shape;
        public readonly string Color;
        public readonly int X;
        public readonly int Y;
        public readonly string[]? Flags;
        public readonly int? WetMoves;

        public P(string shape, string color, int x, int y, string[]? flags = null, int? wetMoves = null)
        {
            Shape = shape;
            Color = color;
            X = x;
            Y = y;
            Flags = flags;
            WetMoves = wetMoves;
        }
    }

    /// <summary>`[shape, colour, x (global site column, default 6–7), plan row, segment?]`.</summary>
    public sealed class D
    {
        public readonly string Shape;
        public readonly string Color;
        public readonly int X;
        public readonly int Y;
        public readonly int? Segment;

        public D(string shape, string color, int x, int y, int? segment = null)
        {
            Shape = shape;
            Color = color;
            X = x;
            Y = y;
            Segment = segment;
        }
    }

    public sealed class SizeSpec
    {
        public int? Cols;
        public int? Rows;

        public SizeSpec(int? cols = null, int? rows = null)
        {
            Cols = cols;
            Rows = rows;
        }
    }

    public sealed class WallSpec
    {
        public int? Height;
        public JsonValue[]? Gaps;
        public string? Fan;
    }

    public sealed class GravitySpec
    {
        public string? Build;
        public bool? Yard;
    }

    public sealed class BatchSpec
    {
        public int ForSegment;
        public P[] Pieces = new P[0];
        public int[]? DropColumns;
    }

    public sealed class LevelSpec
    {
        public int? Id;
        /// <summary>GDD §14 `schemaVersion` (default 1; 2 = K-55 fixed yard, D-099).</summary>
        public int? SchemaVersion;
        /// <summary>K-49 yard size Wy × Hy (default 6 × 8).</summary>
        public SizeSpec? Yard;
        /// <summary>K-49 site size Ws × Hs (default 2 × 8).</summary>
        public SizeSpec? Site;
        public int? Chapter;
        public int? Moves;
        public string? Difficulty;
        public long? Seed;
        public string? Teaches;
        public WallSpec? Wall;
        /// <summary>Rows of one segment (top → bottom) …</summary>
        public string[]? Plan;
        /// <summary>… or one row list per segment.</summary>
        public string[][]? Plans;
        public JsonValue?[]? Hidden;
        /// <summary>Batch 0 (start yard).</summary>
        public P[]? Pieces;
        /// <summary>Truck batches k ≥ 1 (y is written as Hy).</summary>
        public BatchSpec[]? Batches;
        public D[]? Debris;
        public JsonValue[]? Obstacles;
        public GravitySpec? Gravity;
        public string? Mode;
        public int? CarouselEvery;
        public JsonValue? Elevator;
        public JsonValue[]? Goals;
        public JsonValue[]? Tutorial;
        /// <summary>v3 fields of start-batch piece `i` (GDD §16): Y9 `colors`, Y10 `stack`, Y12 `bonusMoves` (merged into the piece JSON).</summary>
        public Dictionary<int, JsonValue>? Traits;
    }

    public static class Builders
    {
        private static JsonValue Piece(P p, int? yOverride = null)
        {
            var o = J.O("shape", p.Shape, "color", p.Color, "x", p.X, "y", yOverride ?? p.Y);
            if (p.Flags != null && p.Flags.Length > 0) o.Set("flags", J.V(p.Flags));
            if (p.WetMoves.HasValue) o.Set("wetMoves", JsonValue.Of(p.WetMoves.Value));
            return o;
        }

        /// <summary>Level JSON as it would be written in levels/ (unparsed), with the K-49 size fields written in.</summary>
        public static JsonValue LevelJson(LevelSpec? spec = null)
        {
            spec ??= new LevelSpec();
            int id = spec.Id ?? 1;
            string[][] segments = spec.Plans ?? new[] { spec.Plan ?? new[] { "WW" } };
            var json = JsonValue.Object()
                .Set("schemaVersion", JsonValue.Of(spec.SchemaVersion ?? 1))
                .Set("id", JsonValue.Of(id))
                .Set("chapter", JsonValue.Of(spec.Chapter ?? (int)Math.Ceiling(id / 10.0)))
                .Set("name", J.O("tr", "Test", "en", "Test"))
                .Set("difficulty", JsonValue.Of(spec.Difficulty ?? "easy"))
                .Set("moves", JsonValue.Of(spec.Moves ?? 20));
            if (spec.Teaches != null) json.Set("teaches", JsonValue.Of(spec.Teaches));
            if (spec.Seed.HasValue) json.Set("seed", JsonValue.Of(spec.Seed.Value));
            json.Set("goals", spec.Goals != null ? J.V(spec.Goals) : J.A(J.O("type", "build")));
            json.Set("gravity", J.O("build", spec.Gravity?.Build ?? "normal", "yard", spec.Gravity?.Yard ?? false));
            var wall = J.O("height", spec.Wall?.Height ?? 0, "gaps", spec.Wall?.Gaps ?? new JsonValue[0]);
            if (spec.Wall?.Fan != null) wall.Set("fan", J.O("dir", spec.Wall.Fan));
            json.Set("wall", wall);
            if (spec.Site != null) json.Set("site", J.O("cols", spec.Site.Cols, "rows", spec.Site.Rows));
            var build = J.O("mode", spec.Mode ?? "segments");
            if (spec.CarouselEvery.HasValue) build.Set("carouselEvery", JsonValue.Of(spec.CarouselEvery.Value));
            if (spec.Elevator != null) build.Set("elevator", spec.Elevator);
            var segs = JsonValue.Array();
            for (int i = 0; i < segments.Length; i++)
            {
                var sg = J.O("name", J.O("tr", "D" + i, "en", "S" + i), "rows", segments[i]);
                var hidden = spec.Hidden != null && i < spec.Hidden.Length ? spec.Hidden[i] : null;
                if (hidden != null) sg.Set("hidden", hidden);
                segs.Add(sg);
            }
            build.Set("segments", segs);
            if (spec.Debris != null)
            {
                var d = JsonValue.Array();
                foreach (var x in spec.Debris) d.Add(J.O("shape", x.Shape, "color", x.Color, "x", x.X, "y", x.Y, "segment", x.Segment));
                build.Set("debris", d);
            }
            json.Set("build", build);
            var yard = JsonValue.Object();
            if (spec.Yard?.Cols != null) yard.Set("cols", JsonValue.Of(spec.Yard.Cols.Value));
            if (spec.Yard?.Rows != null) yard.Set("rows", JsonValue.Of(spec.Yard.Rows.Value));
            var batches = JsonValue.Array();
            var batch0 = JsonValue.Array();
            int pieceIndex = 0;
            foreach (var p in spec.Pieces ?? new P[0])
            {
                var pj = Piece(p);
                if (spec.Traits != null && spec.Traits.TryGetValue(pieceIndex, out var extra))
                    foreach (var k in extra.Keys.ToArray()) pj.Set(k, extra[k]!);
                batch0.Add(pj);
                pieceIndex++;
            }
            batches.Add(J.O("forSegment", 0, "pieces", batch0));
            foreach (var b in spec.Batches ?? new BatchSpec[0])
            {
                var pieces = JsonValue.Array();
                foreach (var p in b.Pieces) pieces.Add(Piece(p, spec.Yard?.Rows ?? 8));
                var bo = J.O("forSegment", b.ForSegment);
                if (b.DropColumns != null) bo.Set("dropColumns", J.V(b.DropColumns));
                bo.Set("pieces", pieces);
                batches.Add(bo);
            }
            yard.Set("batches", batches);
            json.Set("yard", yard);
            json.Set("obstacles", spec.Obstacles != null ? J.V(spec.Obstacles) : JsonValue.Array());
            if (spec.Tutorial != null) json.Set("tutorial", J.V(spec.Tutorial));
            return json;
        }

        /// <summary>Schema-parsed LevelData; throws with a readable message when the spec breaks the schema.</summary>
        public static LevelData Level(LevelSpec? spec = null) => Core.Level.LevelJson.ParseOrThrow(LevelJson(spec));

        public static CompiledLevel Compiled(LevelSpec? spec = null) => Compiler.Compile(Level(spec));

        public static GameState InitialState(LevelSpec? spec = null) => State.CreateInitialState(Compiled(spec));

        private static readonly Regex LeadingNewline = new Regex("^\\r?\\n");
        private static readonly Regex TrailingBlank = new Regex("\\n\\s*$");
        private static readonly Regex TrailingWs = new Regex("\\s+$");

        /// <summary>Removes the common indentation and blank first/last lines of a multi-line literal.</summary>
        public static string Dedent(string text)
        {
            var body = TrailingBlank.Replace(LeadingNewline.Replace(text.Replace("\r\n", "\n"), ""), "");
            var lines = body.Split('\n');
            int indent = int.MaxValue;
            foreach (var l in lines)
            {
                if (l.Trim().Length == 0) continue;
                int n = 0;
                while (n < l.Length && l[n] == ' ') n++;
                indent = Math.Min(indent, n);
            }
            if (indent == int.MaxValue) indent = 0;
            var o = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++) o[i] = lines[i].Length >= indent ? lines[i].Substring(indent) : lines[i].TrimStart(' ');
            return string.Join("\n", o);
        }

        private static string Norm(string t)
        {
            var lines = t.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = TrailingWs.Replace(lines[i], "");
            return string.Join("\n", lines);
        }

        /// <summary>Snapshot comparison against Appendix A text (indentation-insensitive, trailing spaces ignored).</summary>
        public static void ExpectAscii(GameState s, string expected, bool ids = false)
        {
            Assert.AreEqual(Norm(Dedent(expected)), Norm(Ascii.ToAscii(s, ids)));
        }

        /// <summary>`[...]` helper: pieces list.</summary>
        public static P[] Pieces(params P[] pieces) => pieces;

        public static List<string> Lines(string text) => new List<string>(text.Split('\n'));
    }
}
