#nullable enable
// Port of the zod `LevelSchema` of src/core/level/schema.ts (web repo, source of truth) as a hand-written structural parser:
// every strictObject / Int(min, max) / enum / regex / tuple / discriminated union of the TS schema is checked here, issues
// are collected with zod-4 codes and paths (`invalid_type`, `too_small`, `too_big`, `invalid_value`, `invalid_format`,
// `unrecognized_keys`, `invalid_union`, `custom`) so the validator port (logic.ts `issuesFromSchema`) can map them to K-45
// codes. Defaults of the TS schema are applied (shutter `phase` 0, slider `dir` 1, cargo colour placeholder "W").
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;

namespace MinikUsta.Core.Level
{
    /// <summary>One schema problem (zod issue): path segments (string keys and int indices), code and message.</summary>
    public sealed class SchemaIssue
    {
        public readonly object[] Path;
        public readonly string Code;
        public readonly string Message;
        /// <summary>`unrecognized_keys` only.</summary>
        public readonly string[]? Keys;

        public SchemaIssue(object[] path, string code, string message, string[]? keys)
        {
            Path = path;
            Code = code;
            Message = message;
            Keys = keys;
        }

        /// <summary>`wall.gaps[0].size` style path (logic.ts `formatPath`).</summary>
        public string PathString
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var p in Path)
                {
                    if (p is int i) sb.Append('[').Append(i.ToString(CultureInfo.InvariantCulture)).Append(']');
                    else
                    {
                        if (sb.Length > 0) sb.Append('.');
                        sb.Append((string)p);
                    }
                }
                return sb.ToString();
            }
        }

        public override string ToString() => Code + " at " + (PathString.Length == 0 ? "<root>" : PathString) + ": " + Message;
    }

    public sealed class LevelSchemaException : Exception
    {
        public readonly SchemaIssue[] Issues;

        public LevelSchemaException(SchemaIssue[] issues) : base(Describe(issues))
        {
            Issues = issues;
        }

        private static string Describe(SchemaIssue[] issues)
        {
            var sb = new StringBuilder("level breaks the schema:");
            foreach (var i in issues) sb.Append('\n').Append(i.ToString());
            return sb.ToString();
        }
    }

    public static class LevelJson
    {
        /// <summary>`LevelSchema.safeParse(json)`: the parsed level, or null with the issues appended to `issues`.</summary>
        public static LevelData? Parse(JsonValue json, List<SchemaIssue> issues)
        {
            var p = new Parser(issues);
            var level = p.Level(json);
            return issues.Count > 0 ? null : level;
        }

        /// <summary>`LevelSchema.parse(json)`: throws `LevelSchemaException` with every issue.</summary>
        public static LevelData ParseOrThrow(JsonValue json)
        {
            var issues = new List<SchemaIssue>();
            var level = Parse(json, issues);
            if (level == null) throw new LevelSchemaException(issues.ToArray());
            return level;
        }

        public static LevelData ParseOrThrow(string text) => ParseOrThrow(JsonReader.Parse(text));

        /// <summary>
        /// K-43 item 4 `levelHash` (TECH §11.1; TS session.ts): FNV-1a 64 of the canonical JSON of the level data, 16 hex
        /// digits. Hashes the level as loaded (the parsed `LevelData`, defaults applied), exactly like the TS `levelHash(lvl.data)`.
        /// </summary>
        public static string LevelHash(LevelData data) => LevelHash(data.ToJson());

        /// <summary>`levelHash` of any JSON value (tools and tests hash raw data too).</summary>
        public static string LevelHash(JsonValue any) => Fnv1a.Hash64Hex(JsonWriter.Canonical(any));

        private static readonly string[] Difficulties = { "easy", "normal", "hard", "superhard" };
        private static readonly string[] GravityBuilds = { "low", "normal", "high" };
        private static readonly string[] FanDirs = { "left", "right" };
        private static readonly string[] BuildModes = { "segments", "carousel" };
        private static readonly string[] GapTypes = { "static", "shutter", "slider", "paint", "locked" };
        private static readonly string[] HiddenKinds = { "repeat", "mirrorOf" };
        private static readonly string[] GoalTypes = { "build", "clear", "collect" };
        private static readonly string[] ClearTargets = { "crate", "chain", "debris" };
        private static readonly string[] ObstacleTypes = { "crate", "cement_bag", "screw", "key" };
        private static readonly string[] HandKinds = { "tap", "drag", "hold" };
        private static readonly string[] FlagFilters = { "glass", "balloon", "mortar" };
        private static readonly string[] HitTypes = { "crate", "cement_bag", "chain" };
        private static readonly string[] ItemTypes = { "screw", "key" };
        private static readonly string[] Teaches = Concat(MechanicIds.All, MechanicIds.Legacy);

        private static string[] Concat(string[] a, string[] b)
        {
            var o = new string[a.Length + b.Length];
            a.CopyTo(o, 0);
            b.CopyTo(o, a.Length);
            return o;
        }

        private sealed class Parser
        {
            private readonly List<SchemaIssue> issues;
            private static readonly object[] Root = new object[0];

            public Parser(List<SchemaIssue> issues)
            {
                this.issues = issues;
            }

            // --- primitives --------------------------------------------------------------------------------------------

            private void Issue(object[] path, string code, string message, string[]? keys = null)
            {
                issues.Add(new SchemaIssue(path, code, message, keys));
            }

            private static object[] At(object[] path, object key)
            {
                var a = new object[path.Length + 1];
                Array.Copy(path, a, path.Length);
                a[path.Length] = key;
                return a;
            }

            private static string TypeName(JsonValue v)
            {
                switch (v.Kind)
                {
                    case JsonKind.Null: return "null";
                    case JsonKind.Bool: return "boolean";
                    case JsonKind.Number: return "number";
                    case JsonKind.String: return "string";
                    case JsonKind.Array: return "array";
                    default: return "object";
                }
            }

            private bool Missing(JsonValue? v, object[] path, bool required, string expected)
            {
                if (v != null) return false;
                if (required) Issue(path, "invalid_type", "Invalid input: expected " + expected + ", received undefined");
                return true;
            }

            /// <summary>`z.strictObject`: an object whose keys are all in `allowed`.</summary>
            private bool Strict(JsonValue? v, object[] path, string[] allowed, bool required = true)
            {
                if (Missing(v, path, required, "object")) return false;
                if (!v!.IsObject)
                {
                    Issue(path, "invalid_type", "Invalid input: expected object, received " + TypeName(v));
                    return false;
                }
                List<string>? extra = null;
                foreach (var k in v.Keys)
                    if (Array.IndexOf(allowed, k) < 0) (extra ??= new List<string>()).Add(k);
                if (extra != null)
                    Issue(path, "unrecognized_keys", "Unrecognized key(s) in object: " + string.Join(", ", extra), extra.ToArray());
                return true;
            }

            /// <summary>`Int(min, max)`: `z.int().check(z.minimum(min), z.maximum(max))`.</summary>
            private int? Int(JsonValue? v, object[] path, int min, int max, bool required = true)
            {
                var n = IntAny(v, path, required);
                if (n == null) return null;
                if (n.Value < min)
                {
                    Issue(path, "too_small", "Too small: expected number to be >=" + min);
                    return null;
                }
                if (n.Value > max)
                {
                    Issue(path, "too_big", "Too big: expected number to be <=" + max);
                    return null;
                }
                return (int)n.Value;
            }

            /// <summary>`z.int()` without bounds (safe integers).</summary>
            private long? IntAny(JsonValue? v, object[] path, bool required = true)
            {
                if (Missing(v, path, required, "number")) return null;
                if (!v!.IsNumber)
                {
                    Issue(path, "invalid_type", "Invalid input: expected number, received " + TypeName(v));
                    return null;
                }
                if (!v.IsInteger)
                {
                    Issue(path, "invalid_type", "Invalid input: expected int, received number");
                    return null;
                }
                return v.AsLong;
            }

            private double? Num(JsonValue? v, object[] path, double min, double max, bool required = true)
            {
                if (Missing(v, path, required, "number")) return null;
                if (!v!.IsNumber)
                {
                    Issue(path, "invalid_type", "Invalid input: expected number, received " + TypeName(v));
                    return null;
                }
                double d = v.AsDouble;
                if (d < min)
                {
                    Issue(path, "too_small", "Too small: expected number to be >=" + min.ToString(CultureInfo.InvariantCulture));
                    return null;
                }
                if (d > max)
                {
                    Issue(path, "too_big", "Too big: expected number to be <=" + max.ToString(CultureInfo.InvariantCulture));
                    return null;
                }
                return d;
            }

            private string? Str(JsonValue? v, object[] path, bool required = true, int minLength = 0)
            {
                if (Missing(v, path, required, "string")) return null;
                if (!v!.IsString)
                {
                    Issue(path, "invalid_type", "Invalid input: expected string, received " + TypeName(v));
                    return null;
                }
                if (v.AsString.Length < minLength)
                {
                    Issue(path, "too_small", "Too small: expected string to have >=" + minLength + " characters");
                    return null;
                }
                return v.AsString;
            }

            private string? Pattern(JsonValue? v, object[] path, Regex re, bool required = true)
            {
                var s = Str(v, path, required);
                if (s == null) return null;
                if (!re.IsMatch(s))
                {
                    Issue(path, "invalid_format", "Invalid string: must match pattern " + re);
                    return null;
                }
                return s;
            }

            private string? Enum(JsonValue? v, object[] path, string[] options, bool required = true)
            {
                if (Missing(v, path, required, "string")) return null;
                if (!v!.IsString || Array.IndexOf(options, v.AsString) < 0)
                {
                    Issue(path, "invalid_value", "Invalid option: expected one of " + string.Join("|", options));
                    return null;
                }
                return v.AsString;
            }

            private bool? Bool(JsonValue? v, object[] path, bool required = true)
            {
                if (Missing(v, path, required, "boolean")) return null;
                if (!v!.IsBool)
                {
                    Issue(path, "invalid_type", "Invalid input: expected boolean, received " + TypeName(v));
                    return null;
                }
                return v.AsBool;
            }

            /// <summary>`Yes = z.optional(z.literal(true))`.</summary>
            private bool? LiteralTrue(JsonValue? v, object[] path)
            {
                if (v == null) return null;
                if (!v.IsBool || !v.AsBool)
                {
                    Issue(path, "invalid_value", "Invalid input: expected true");
                    return null;
                }
                return true;
            }

            /// <summary>`Dir = z.union([z.literal(1), z.literal(-1)])`.</summary>
            private int? Dir(JsonValue? v, object[] path, bool required = true)
            {
                if (Missing(v, path, required, "number")) return null;
                if (v!.IsNumber && (v.AsDouble == 1 || v.AsDouble == -1)) return (int)v.AsDouble;
                Issue(path, "invalid_union", "Invalid input: expected 1 or -1");
                return null;
            }

            private JsonValue? Arr(JsonValue? v, object[] path, bool required = true, int min = -1, int max = -1)
            {
                if (Missing(v, path, required, "array")) return null;
                if (!v!.IsArray)
                {
                    Issue(path, "invalid_type", "Invalid input: expected array, received " + TypeName(v));
                    return null;
                }
                if (min >= 0 && v.Count < min)
                {
                    Issue(path, "too_small", "Too small: expected array to have >=" + min + " items");
                    return null;
                }
                if (max >= 0 && v.Count > max)
                {
                    Issue(path, "too_big", "Too big: expected array to have <=" + max + " items");
                    return null;
                }
                return v;
            }

            /// <summary>`z.tuple([Int(min, max), Int(min2, max2)])`.</summary>
            private int[]? Tuple(JsonValue? v, object[] path, int min0, int max0, int min1, int max1, bool required = true)
            {
                var a = Arr(v, path, required);
                if (a == null) return null;
                if (a.Count != 2)
                {
                    Issue(path, a.Count < 2 ? "too_small" : "too_big", "Invalid input: expected a tuple of 2 items");
                    return null;
                }
                var x = Int(a[0], At(path, 0), min0, max0);
                var y = Int(a[1], At(path, 1), min1, max1);
                if (x == null || y == null) return null;
                return new[] { x.Value, y.Value };
            }

            private double[]? RateTuple(JsonValue? v, object[] path)
            {
                var a = Arr(v, path);
                if (a == null) return null;
                if (a.Count != 2)
                {
                    Issue(path, a.Count < 2 ? "too_small" : "too_big", "Invalid input: expected a tuple of 2 items");
                    return null;
                }
                var x = Num(a[0], At(path, 0), 0, 1);
                var y = Num(a[1], At(path, 1), 0, 1);
                if (x == null || y == null) return null;
                return new[] { x.Value, y.Value };
            }

            // --- schema nodes --------------------------------------------------------------------------------------------

            private static readonly string[] LevelKeys =
            {
                "schemaVersion", "id", "chapter", "name", "difficulty", "moves", "teaches", "seed", "goals", "gravity", "wall",
                "site", "build", "yard", "obstacles", "tutorial", "targets",
            };

            public LevelData? Level(JsonValue v)
            {
                var path = Root;
                if (!Strict(v, path, LevelKeys)) return null;
                int? schemaVersion = null;
                var sv = v["schemaVersion"];
                if (sv != null)
                {
                    if (sv.IsNumber && sv.AsDouble == 1) schemaVersion = 1;
                    else Issue(At(path, "schemaVersion"), "invalid_value", "Invalid input: expected 1");
                }
                var id = Int(v["id"], At(path, "id"), 1, 999);
                var chapter = Int(v["chapter"], At(path, "chapter"), 1, 5);
                var name = I18n(v["name"], At(path, "name"));
                var difficulty = Enum(v["difficulty"], At(path, "difficulty"), Difficulties);
                var moves = Int(v["moves"], At(path, "moves"), 1, 99);
                var teaches = Enum(v["teaches"], At(path, "teaches"), Teaches, false);
                var seed = IntAny(v["seed"], At(path, "seed"), false);
                var goals = Goals(v["goals"], At(path, "goals"));
                var gravity = Gravity(v["gravity"], At(path, "gravity"));
                var wall = Wall(v["wall"], At(path, "wall"));
                var site = Size(v["site"], At(path, "site"));
                var build = Build(v["build"], At(path, "build"));
                var yard = Yard(v["yard"], At(path, "yard"));
                var obstacles = Obstacles(v["obstacles"], At(path, "obstacles"));
                var tutorial = Tutorial(v["tutorial"], At(path, "tutorial"));
                var targets = Targets(v["targets"], At(path, "targets"));
                if (issues.Count > 0 || id == null || chapter == null || name == null || difficulty == null || moves == null
                    || goals == null || gravity == null || wall == null || build == null || yard == null || obstacles == null)
                    return null;
                return new LevelData(schemaVersion, id.Value, chapter.Value, name, difficulty, moves.Value, teaches, seed, goals, gravity,
                    wall, site, build, yard, obstacles, tutorial, targets);
            }

            private static readonly string[] I18nKeys = { "tr", "en" };

            private I18nText? I18n(JsonValue? v, object[] path)
            {
                if (!Strict(v, path, I18nKeys)) return null;
                var tr = Str(v!["tr"], At(path, "tr"), true, 1);
                var en = Str(v["en"], At(path, "en"), true, 1);
                return tr == null || en == null ? null : new I18nText(tr, en);
            }

            private static readonly string[] GoalBuildKeys = { "type" };
            private static readonly string[] GoalClearKeys = { "type", "target", "count" };
            private static readonly string[] GoalCollectKeys = { "type", "item", "count" };

            private GoalData[]? Goals(JsonValue? v, object[] path)
            {
                var a = Arr(v, path, true, 1, 3);
                if (a == null) return null;
                var o = new GoalData[a.Count];
                bool ok = true;
                for (int i = 0; i < a.Count; i++)
                {
                    var g = Goal(a[i], At(path, i));
                    if (g == null) ok = false;
                    else o[i] = g;
                }
                return ok ? o : null;
            }

            private GoalData? Goal(JsonValue v, object[] path)
            {
                if (!v.IsObject)
                {
                    Issue(path, "invalid_type", "Invalid input: expected object, received " + TypeName(v));
                    return null;
                }
                var tv = v["type"];
                if (tv == null || !tv.IsString || Array.IndexOf(GoalTypes, tv.AsString) < 0)
                {
                    Issue(At(path, "type"), "invalid_union", "Invalid input: `type` must be one of " + string.Join("|", GoalTypes)); // zod 4: the issue sits on the discriminator
                    return null;
                }
                string type = tv.AsString;
                switch (type)
                {
                    case "build":
                        return Strict(v, path, GoalBuildKeys) ? new GoalData(type, null, null, null) : null;
                    case "clear":
                    {
                        if (!Strict(v, path, GoalClearKeys)) return null;
                        var target = Enum(v["target"], At(path, "target"), ClearTargets);
                        var count = Int(v["count"], At(path, "count"), 1, 99);
                        return target == null || count == null ? null : new GoalData(type, target, count, null);
                    }
                    default:
                    {
                        if (!Strict(v, path, GoalCollectKeys)) return null;
                        var item = v["item"];
                        string? itemName = null;
                        if (item == null || !item.IsString || item.AsString != "screw") Issue(At(path, "item"), "invalid_value", "Invalid input: expected \"screw\"");
                        else itemName = "screw";
                        var count = Int(v["count"], At(path, "count"), 1, 99);
                        return itemName == null || count == null ? null : new GoalData(type, null, count, itemName);
                    }
                }
            }

            private static readonly string[] GravityKeys = { "build", "yard" };

            private GravityData? Gravity(JsonValue? v, object[] path)
            {
                if (!Strict(v, path, GravityKeys)) return null;
                var build = Enum(v!["build"], At(path, "build"), GravityBuilds);
                var yard = Bool(v["yard"], At(path, "yard"));
                return build == null || yard == null ? null : new GravityData(build, yard.Value);
            }

            private static readonly string[] WallKeys = { "height", "gaps", "fan" };
            private static readonly string[] FanKeys = { "dir" };

            private WallData? Wall(JsonValue? v, object[] path)
            {
                if (!Strict(v, path, WallKeys)) return null;
                var height = Int(v!["height"], At(path, "height"), 0, 8);
                var gapsArr = Arr(v["gaps"], At(path, "gaps"), true, -1, 3);
                GapData[]? gaps = null;
                if (gapsArr != null)
                {
                    gaps = new GapData[gapsArr.Count];
                    for (int i = 0; i < gapsArr.Count; i++)
                    {
                        var g = Gap(gapsArr[i], At(At(path, "gaps"), i));
                        if (g == null) gaps = null;
                        else if (gaps != null) gaps[i] = g;
                    }
                }
                FanData? fan = null;
                var fv = v["fan"];
                if (fv != null)
                {
                    if (Strict(fv, At(path, "fan"), FanKeys))
                    {
                        var dir = Enum(fv["dir"], At(At(path, "fan"), "dir"), FanDirs);
                        if (dir != null) fan = new FanData(dir);
                    }
                }
                return height == null || gaps == null ? null : new WallData(height.Value, gaps, fan);
            }

            private static readonly string[] GapStaticKeys = { "type", "y", "size" };
            private static readonly string[] GapShutterKeys = { "type", "y", "size", "period", "phase" };
            private static readonly string[] GapSliderKeys = { "type", "y", "size", "range", "dir" };
            private static readonly string[] GapPaintKeys = { "type", "y", "size", "color" };
            private static readonly string[] GapLockedKeys = { "type", "y", "size", "keyId" };

            private GapData? Gap(JsonValue v, object[] path)
            {
                if (!v.IsObject)
                {
                    Issue(path, "invalid_type", "Invalid input: expected object, received " + TypeName(v));
                    return null;
                }
                var tv = v["type"];
                if (tv == null || !tv.IsString || Array.IndexOf(GapTypes, tv.AsString) < 0)
                {
                    Issue(At(path, "type"), "invalid_union", "Invalid input: `type` must be one of " + string.Join("|", GapTypes)); // zod 4: the issue sits on the discriminator
                    return null;
                }
                string type = tv.AsString;
                string[] keys = type == "static" ? GapStaticKeys
                    : type == "shutter" ? GapShutterKeys
                    : type == "slider" ? GapSliderKeys
                    : type == "paint" ? GapPaintKeys
                    : GapLockedKeys;
                if (!Strict(v, path, keys)) return null;
                var y = Int(v["y"], At(path, "y"), 0, 7);
                var size = Int(v["size"], At(path, "size"), 1, 7);
                int period = 0;
                int phase = 0;
                int[]? range = null;
                int dir = 1;
                string? color = null;
                string? keyId = null;
                bool ok = y != null && size != null;
                switch (type)
                {
                    case "shutter":
                    {
                        var p = Int(v["period"], At(path, "period"), 1, 4);
                        /* `phase` default 0 = starts open (GDD S-24); < 2·period (L-09). */
                        var ph = v.Has("phase") ? Int(v["phase"], At(path, "phase"), 0, 7) : 0;
                        if (p == null || ph == null) ok = false;
                        else
                        {
                            period = p.Value;
                            phase = ph.Value;
                        }
                        break;
                    }
                    case "slider":
                    {
                        range = Tuple(v["range"], At(path, "range"), 0, 7, 0, 7);
                        /* `dir` default 1 (GDD §14, OBSTACLES W5). */
                        var d = v.Has("dir") ? Dir(v["dir"], At(path, "dir")) : 1;
                        if (range == null || d == null) ok = false;
                        else dir = d.Value;
                        break;
                    }
                    case "paint":
                        color = Enum(v["color"], At(path, "color"), ColorCodes.All);
                        if (color == null) ok = false;
                        break;
                    case "locked":
                        keyId = Str(v["keyId"], At(path, "keyId"), true, 1);
                        if (keyId == null) ok = false;
                        break;
                }
                return ok ? new GapData(type, y!.Value, size!.Value, period, phase, range, dir, color, keyId) : null;
            }

            private static readonly string[] SizeKeys = { "cols", "rows" };

            private SizeData? Size(JsonValue? v, object[] path)
            {
                if (v == null) return null;
                if (!Strict(v, path, SizeKeys)) return null;
                var cols = Int(v["cols"], At(path, "cols"), 1, 16);
                var rows = Int(v["rows"], At(path, "rows"), 1, 16);
                return cols == null || rows == null ? null : new SizeData(cols.Value, rows.Value);
            }

            private static readonly string[] BuildKeys = { "mode", "carouselEvery", "elevator", "segments", "debris" };
            private static readonly string[] ElevatorKeys = { "range", "start", "dir" };

            private BuildData? Build(JsonValue? v, object[] path)
            {
                if (!Strict(v, path, BuildKeys)) return null;
                var mode = Enum(v!["mode"], At(path, "mode"), BuildModes);
                var carouselEvery = Int(v["carouselEvery"], At(path, "carouselEvery"), 2, 6, false);
                ElevatorData? elevator = null;
                var ev = v["elevator"];
                bool ok = mode != null;
                if (ev != null)
                {
                    var ep = At(path, "elevator");
                    if (Strict(ev, ep, ElevatorKeys))
                    {
                        var range = Tuple(ev["range"], At(ep, "range"), 0, 3, 0, 3);
                        var start = Int(ev["start"], At(ep, "start"), 0, 3);
                        var dir = Dir(ev["dir"], At(ep, "dir"));
                        if (range != null && start != null && dir != null) elevator = new ElevatorData(range, start.Value, dir.Value);
                        else ok = false;
                    }
                    else ok = false;
                }
                var segArr = Arr(v["segments"], At(path, "segments"), true, 1, 5);
                SegmentData[]? segments = null;
                if (segArr != null)
                {
                    segments = new SegmentData[segArr.Count];
                    for (int i = 0; i < segArr.Count; i++)
                    {
                        var s = Segment(segArr[i], At(At(path, "segments"), i));
                        if (s == null) segments = null;
                        else if (segments != null) segments[i] = s;
                    }
                }
                DebrisData[]? debris = null;
                var dv = v["debris"];
                if (dv != null)
                {
                    var da = Arr(dv, At(path, "debris"));
                    if (da == null) ok = false;
                    else
                    {
                        debris = new DebrisData[da.Count];
                        for (int i = 0; i < da.Count; i++)
                        {
                            var d = Debris(da[i], At(At(path, "debris"), i));
                            if (d == null) ok = false;
                            else debris[i] = d;
                        }
                    }
                }
                return ok && segments != null ? new BuildData(mode!, carouselEvery, elevator, segments, debris) : null;
            }

            private static readonly string[] SegmentKeys = { "name", "rows", "hidden" };
            private static readonly string[] HiddenRepeatKeys = { "kind", "period" };
            private static readonly string[] HiddenMirrorKeys = { "kind", "segment" };

            private SegmentData? Segment(JsonValue v, object[] path)
            {
                if (!Strict(v, path, SegmentKeys)) return null;
                var name = I18n(v["name"], At(path, "name"));
                var rowsArr = Arr(v["rows"], At(path, "rows"), true, 1, 8);
                string[]? rows = null;
                if (rowsArr != null)
                {
                    rows = new string[rowsArr.Count];
                    for (int i = 0; i < rowsArr.Count; i++)
                    {
                        var r = Pattern(rowsArr[i], At(At(path, "rows"), i), LevelSchemaPatterns.PlanRow);
                        if (r == null) rows = null;
                        else if (rows != null) rows[i] = r;
                    }
                }
                HiddenRule? hidden = null;
                bool ok = true;
                var hv = v["hidden"];
                if (hv != null)
                {
                    var hp = At(path, "hidden");
                    if (!hv.IsObject)
                    {
                        Issue(hp, "invalid_type", "Invalid input: expected object, received " + TypeName(hv));
                        ok = false;
                    }
                    else
                    {
                        var kv = hv["kind"];
                        if (kv == null || !kv.IsString || Array.IndexOf(HiddenKinds, kv.AsString) < 0)
                        {
                            Issue(At(hp, "kind"), "invalid_union", "Invalid input: `kind` must be one of " + string.Join("|", HiddenKinds)); // zod 4: the issue sits on the discriminator
                            ok = false;
                        }
                        else if (kv.AsString == "repeat")
                        {
                            if (!Strict(hv, hp, HiddenRepeatKeys)) ok = false;
                            var period = Int(hv["period"], At(hp, "period"), 1, 4);
                            if (period == null) ok = false;
                            else if (ok) hidden = new HiddenRule("repeat", period.Value, 0);
                        }
                        else
                        {
                            if (!Strict(hv, hp, HiddenMirrorKeys)) ok = false;
                            var segment = Int(hv["segment"], At(hp, "segment"), 0, 4);
                            if (segment == null) ok = false;
                            else if (ok) hidden = new HiddenRule("mirrorOf", 0, segment.Value);
                        }
                    }
                }
                return ok && name != null && rows != null ? new SegmentData(name, rows, hidden) : null;
            }

            private static readonly string[] DebrisKeys = { "shape", "color", "x", "y", "segment" };

            private DebrisData? Debris(JsonValue v, object[] path)
            {
                if (!Strict(v, path, DebrisKeys)) return null;
                var shape = Pattern(v["shape"], At(path, "shape"), LevelSchemaPatterns.ShapeId);
                var color = Enum(v["color"], At(path, "color"), ColorCodes.All);
                var x = Int(v["x"], At(path, "x"), 0, 7);
                var y = Int(v["y"], At(path, "y"), 0, 7);
                var segment = Int(v["segment"], At(path, "segment"), 0, 4, false);
                if (shape == null || color == null || x == null || y == null) return null;
                return new DebrisData(shape, color, x.Value, y.Value, segment);
            }

            private static readonly string[] YardKeys = { "cols", "rows", "batches" };
            private static readonly string[] BatchKeys = { "forSegment", "dropColumns", "pieces" };
            private static readonly string[] PieceKeys = { "shape", "color", "x", "y", "flags", "wetMoves" };

            private YardData? Yard(JsonValue? v, object[] path)
            {
                if (!Strict(v, path, YardKeys)) return null;
                var cols = Int(v!["cols"], At(path, "cols"), 1, 16, false);
                var rows = Int(v["rows"], At(path, "rows"), 1, 16, false);
                var ba = Arr(v["batches"], At(path, "batches"), true, 1);
                if (ba == null) return null;
                var batches = new BatchData[ba.Count];
                bool ok = true;
                for (int i = 0; i < ba.Count; i++)
                {
                    var bp = At(At(path, "batches"), i);
                    var b = ba[i];
                    if (!Strict(b, bp, BatchKeys))
                    {
                        ok = false;
                        continue;
                    }
                    var forSegment = Int(b["forSegment"], At(bp, "forSegment"), 0, 5);
                    int[]? dropColumns = null;
                    var dc = b["dropColumns"];
                    if (dc != null)
                    {
                        var dca = Arr(dc, At(bp, "dropColumns"));
                        if (dca == null) ok = false;
                        else
                        {
                            dropColumns = new int[dca.Count];
                            for (int k = 0; k < dca.Count; k++)
                            {
                                var c = Int(dca[k], At(At(bp, "dropColumns"), k), 0, 5);
                                if (c == null) ok = false;
                                else dropColumns[k] = c.Value;
                            }
                        }
                    }
                    var pa = Arr(b["pieces"], At(bp, "pieces"), true, 1);
                    PieceData[]? pieces = null;
                    if (pa != null)
                    {
                        pieces = new PieceData[pa.Count];
                        for (int k = 0; k < pa.Count; k++)
                        {
                            var p = Piece(pa[k], At(At(bp, "pieces"), k));
                            if (p == null) pieces = null;
                            else if (pieces != null) pieces[k] = p;
                        }
                    }
                    if (forSegment == null || pieces == null) ok = false;
                    else batches[i] = new BatchData(forSegment.Value, dropColumns, pieces);
                }
                return ok ? new YardData(cols, rows, batches) : null;
            }

            private PieceData? Piece(JsonValue v, object[] path)
            {
                if (!Strict(v, path, PieceKeys)) return null;
                var shape = Pattern(v["shape"], At(path, "shape"), LevelSchemaPatterns.ShapeId);
                var color = Enum(v["color"], At(path, "color"), ColorCodes.All, false);
                var x = Int(v["x"], At(path, "x"), 0, 7);
                var y = Int(v["y"], At(path, "y"), 0, 9);
                string[]? flags = null;
                bool ok = true;
                var fv = v["flags"];
                if (fv != null)
                {
                    var fa = Arr(fv, At(path, "flags"));
                    if (fa == null) ok = false;
                    else
                    {
                        flags = new string[fa.Count];
                        for (int i = 0; i < fa.Count; i++)
                        {
                            var f = Enum(fa[i], At(At(path, "flags"), i), PieceFlags.DataFlags);
                            if (f == null) ok = false;
                            else flags[i] = f;
                        }
                    }
                }
                var wetMoves = Int(v["wetMoves"], At(path, "wetMoves"), 1, 5, false);
                // superRefine: a material block needs a colour (only I5/Q9 Ağır Yük has none, K-44)
                if (shape != null && !v.Has("color") && !LevelSchemaPatterns.CargoShape.IsMatch(shape))
                {
                    Issue(At(path, "color"), "custom", shape + " is a material block: color is required (only I5/Q9 Ağır Yük has no colour, K-44)");
                    ok = false;
                }
                if (v.Has("color") && color == null) ok = false;
                if (!ok || shape == null || x == null || y == null) return null;
                // transform: color ?? CARGO_PLACEHOLDER_COLOR
                return new PieceData(shape, color ?? LevelSchemaPatterns.CargoPlaceholderColor, x.Value, y.Value, flags, wetMoves);
            }

            private static readonly string[] ObstacleKeys = { "type", "x", "y", "hp", "id" };

            private ObstacleData[]? Obstacles(JsonValue? v, object[] path)
            {
                var a = Arr(v, path);
                if (a == null) return null;
                var o = new ObstacleData[a.Count];
                bool ok = true;
                for (int i = 0; i < a.Count; i++)
                {
                    var p = At(path, i);
                    var ov = a[i];
                    if (!Strict(ov, p, ObstacleKeys))
                    {
                        ok = false;
                        continue;
                    }
                    var type = Enum(ov["type"], At(p, "type"), ObstacleTypes);
                    var x = Int(ov["x"], At(p, "x"), 0, 5);
                    var y = Int(ov["y"], At(p, "y"), 0, 7);
                    var hp = Int(ov["hp"], At(p, "hp"), 1, 3, false);
                    var id = Str(ov["id"], At(p, "id"), false);
                    if (type == null || x == null || y == null) ok = false;
                    else o[i] = new ObstacleData(type, x.Value, y.Value, hp, id);
                }
                return ok ? o : null;
            }

            private static readonly string[] TutorialStepKeys = { "step", "mode", "highlight", "hand", "textKey", "startOn", "done" };
            private static readonly string[] HandKeys = { "kind", "path" };

            private TutorialStepData[]? Tutorial(JsonValue? v, object[] path)
            {
                if (v == null) return null;
                var a = Arr(v, path);
                if (a == null) return null;
                var o = new TutorialStepData[a.Count];
                bool ok = true;
                for (int i = 0; i < a.Count; i++)
                {
                    var s = TutorialStep(a[i], At(path, i));
                    if (s == null) ok = false;
                    else o[i] = s;
                }
                return ok ? o : null;
            }

            private TutorialStepData? TutorialStep(JsonValue v, object[] path)
            {
                if (!Strict(v, path, TutorialStepKeys)) return null;
                var step = Int(v["step"], At(path, "step"), 1, 20);
                var mode = Str(v["mode"], At(path, "mode"), false);
                var ha = Arr(v["highlight"], At(path, "highlight"));
                string[]? highlight = null;
                bool ok = true;
                if (ha != null)
                {
                    highlight = new string[ha.Count];
                    for (int i = 0; i < ha.Count; i++)
                    {
                        var h = Pattern(ha[i], At(At(path, "highlight"), i), LevelSchemaPatterns.Highlight);
                        if (h == null) highlight = null;
                        else if (highlight != null) highlight[i] = h;
                    }
                }
                HandData? hand = null;
                var hv = v["hand"];
                if (hv != null)
                {
                    var hp = At(path, "hand");
                    if (!Strict(hv, hp, HandKeys)) ok = false;
                    else
                    {
                        var kind = Enum(hv["kind"], At(hp, "kind"), HandKinds);
                        int[][]? hpath = null;
                        var pv = hv["path"];
                        if (pv != null)
                        {
                            var pa = Arr(pv, At(hp, "path"));
                            if (pa == null) ok = false;
                            else
                            {
                                hpath = new int[pa.Count][];
                                for (int i = 0; i < pa.Count; i++)
                                {
                                    var t = Tuple(pa[i], At(At(hp, "path"), i), 0, 7, 0, 9);
                                    if (t == null) ok = false;
                                    else hpath[i] = t;
                                }
                            }
                        }
                        if (kind == null) ok = false;
                        else if (ok) hand = new HandData(kind, hpath);
                    }
                }
                var textKey = Pattern(v["textKey"], At(path, "textKey"), LevelSchemaPatterns.TutTextKey);
                TutCondition? startOn = null;
                var sv = v["startOn"];
                if (sv != null)
                {
                    startOn = TutCond(sv, At(path, "startOn"));
                    if (startOn == null) ok = false;
                }
                var dv = v["done"];
                TutCondition? done = null;
                if (dv == null) Issue(At(path, "done"), "invalid_type", "Invalid input: expected object, received undefined");
                else done = TutCond(dv, At(path, "done"));
                if (!ok || step == null || highlight == null || textKey == null || done == null) return null;
                return new TutorialStepData(step.Value, mode, highlight, hand, textKey, startOn, done);
            }

            private static readonly string[] CondCountKeys = { "event", "count" };
            private static readonly string[] CondHoldKeys = { "event", "count", "minMs" };
            private static readonly string[] CondPlacementKeys = { "event", "count", "at", "piece", "hidden" };
            private static readonly string[] CondYardMoveKeys = { "event", "count", "at", "piece", "painted" };
            private static readonly string[] CondLandedKeys = { "event", "count", "flag", "wind" };
            private static readonly string[] CondTypeKeys = { "event", "count", "type" };
            private static readonly string[] CondDeliveryKeys = { "event", "count", "flag" };

            /// <summary>GDD §14.1/3 closed `done` vocabulary: filters may only be written on the event GDD allows.</summary>
            private TutCondition? TutCond(JsonValue v, object[] path)
            {
                if (!v.IsObject)
                {
                    Issue(path, "invalid_type", "Invalid input: expected object, received " + TypeName(v));
                    return null;
                }
                var ev = v["event"];
                if (ev == null || !ev.IsString || Array.IndexOf(TutEvents.All, ev.AsString) < 0)
                {
                    Issue(At(path, "event"), "invalid_union", "Invalid input: `event` must be one of " + string.Join("|", TutEvents.All)); // zod 4: the issue sits on the discriminator
                    return null;
                }
                string e = ev.AsString;
                string[] keys;
                switch (e)
                {
                    case "holdOverBuild": keys = CondHoldKeys; break;
                    case "placementCorrect": keys = CondPlacementKeys; break;
                    case "yardMove": keys = CondYardMoveKeys; break;
                    case "landed": keys = CondLandedKeys; break;
                    case "obstacleHit":
                    case "itemCollected": keys = CondTypeKeys; break;
                    case "deliveryDone": keys = CondDeliveryKeys; break;
                    default: keys = CondCountKeys; break;
                }
                if (!Strict(v, path, keys)) return null;
                bool ok = true;
                var count = Int(v["count"], At(path, "count"), 1, 9, false);
                if (v.Has("count") && count == null) ok = false;
                int? minMs = null;
                int[]? at = null;
                string? piece = null;
                bool? hidden = null;
                bool? painted = null;
                string? flag = null;
                bool? wind = null;
                string? type = null;
                if (e == "holdOverBuild")
                {
                    minMs = Int(v["minMs"], At(path, "minMs"), 100, 5000);
                    if (minMs == null) ok = false;
                }
                if (e == "placementCorrect" || e == "yardMove")
                {
                    if (v.Has("at"))
                    {
                        at = Tuple(v["at"], At(path, "at"), 0, 7, 0, 7);
                        if (at == null) ok = false;
                    }
                    if (v.Has("piece"))
                    {
                        piece = Pattern(v["piece"], At(path, "piece"), LevelSchemaPatterns.PieceRef);
                        if (piece == null) ok = false;
                    }
                }
                if (e == "placementCorrect" && v.Has("hidden"))
                {
                    hidden = LiteralTrue(v["hidden"], At(path, "hidden"));
                    if (hidden == null) ok = false;
                }
                if (e == "yardMove" && v.Has("painted"))
                {
                    painted = LiteralTrue(v["painted"], At(path, "painted"));
                    if (painted == null) ok = false;
                }
                if ((e == "landed" || e == "deliveryDone") && v.Has("flag"))
                {
                    flag = Enum(v["flag"], At(path, "flag"), FlagFilters);
                    if (flag == null) ok = false;
                }
                if (e == "landed" && v.Has("wind"))
                {
                    wind = LiteralTrue(v["wind"], At(path, "wind"));
                    if (wind == null) ok = false;
                }
                if (e == "obstacleHit" || e == "itemCollected")
                {
                    type = Enum(v["type"], At(path, "type"), e == "obstacleHit" ? HitTypes : ItemTypes);
                    if (type == null) ok = false;
                }
                return ok ? new TutCondition(e, count, minMs, at, piece, hidden, painted, flag, wind, type) : null;
            }

            private static readonly string[] TargetsKeys = { "minShifts", "firstNeedDepth", "choices0", "deadRate" };

            private TargetsData? Targets(JsonValue? v, object[] path)
            {
                if (v == null) return null;
                if (!Strict(v, path, TargetsKeys)) return null;
                bool ok = true;
                int[]? minShifts = null;
                int[]? firstNeedDepth = null;
                int[]? choices0 = null;
                double[]? deadRate = null;
                if (v.Has("minShifts"))
                {
                    minShifts = Tuple(v["minShifts"], At(path, "minShifts"), 0, 99, 0, 99);
                    if (minShifts == null) ok = false;
                }
                if (v.Has("firstNeedDepth"))
                {
                    firstNeedDepth = Tuple(v["firstNeedDepth"], At(path, "firstNeedDepth"), 0, 99, 0, 99);
                    if (firstNeedDepth == null) ok = false;
                }
                if (v.Has("choices0"))
                {
                    choices0 = Tuple(v["choices0"], At(path, "choices0"), 0, 99, 0, 99);
                    if (choices0 == null) ok = false;
                }
                if (v.Has("deadRate"))
                {
                    deadRate = RateTuple(v["deadRate"], At(path, "deadRate"));
                    if (deadRate == null) ok = false;
                }
                return ok ? new TargetsData(minShifts, firstNeedDepth, choices0, deadRate) : null;
            }
        }
    }
}
