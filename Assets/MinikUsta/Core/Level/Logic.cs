#nullable enable
// Port of src/core/level/logic.ts (web repo, source of truth). Every message is the TS template string character for
// character (the P2 vectors compare them); numbers print as JS does (ints plain, doubles via JsNumber.Format).
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;

namespace MinikUsta.Core.Level
{
    public static class Severities
    {
        public const string Error = "error";
        public const string Warn = "warn";
    }

    /// <summary>One K-45 finding: `code` is the K-45 snake_case name, `rule` the K-45 item (or K id), `check` the internal L-xx number.</summary>
    public sealed class Issue : IEquatable<Issue>
    {
        /// <summary>K-45 snake_case code, e.g. `gap_touches_top`.</summary>
        public readonly string Code;
        /// <summary>`K-45/&lt;item&gt;` or the related rule id (`K-25`, `K-24`, `GDD 14.1` …).</summary>
        public readonly string Rule;
        /// <summary>Internal check number `L-xx`.</summary>
        public readonly string Check;
        public readonly string Severity;
        /// <summary>Data path, e.g. `wall.gaps[0]`.</summary>
        public readonly string Path;
        public readonly string Message;

        public Issue(string code, string rule, string check, string severity, string path, string message)
        {
            Code = code;
            Rule = rule;
            Check = check;
            Severity = severity;
            Path = path;
            Message = message;
        }

        public bool Equals(Issue? o) => o != null && Code == o.Code && Rule == o.Rule && Check == o.Check && Severity == o.Severity && Path == o.Path && Message == o.Message;
        public override bool Equals(object? obj) => obj is Issue i && Equals(i);
        public override int GetHashCode() => Code.GetHashCode() ^ Path.GetHashCode();
        public override string ToString() => Severity + " " + Rule + " " + Code + " " + Check + " " + Path + ": " + Message;
    }

    /// <summary>i18n keys of `src/i18n/tr.json` and `en.json` (L-17 `tut_key_missing`).</summary>
    public sealed class I18nKeySets
    {
        public readonly ISet<string> Tr;
        public readonly ISet<string> En;

        public I18nKeySets(ISet<string> tr, ISet<string> en)
        {
            Tr = tr;
            En = en;
        }
    }

    public sealed class LogicContext
    {
        /// <summary>Level number taken from the file name `level_NNN.json` (L-01 `id_mismatch`).</summary>
        public readonly int? FileId;
        /// <summary>Union of the mechanic sets of all earlier levels (L-16, L-22). Without it both checks are skipped.</summary>
        public readonly ISet<string>? PreviousMechanics;
        /// <summary>`config/economy.json → boosters.&lt;name&gt;.unlockLevel` (L-17 `booster:` / `pre:`). Without it the check is skipped.</summary>
        public readonly IReadOnlyDictionary<string, int>? BoosterUnlock;
        /// <summary>i18n keys (L-17 `tut_key_missing`). Without them the check is skipped.</summary>
        public readonly I18nKeySets? I18nKeys;
        /// <summary>Run only these checks (default: all of `Logic.CheckIds`).</summary>
        public readonly string[]? Only;
        /// <summary>Expansion budget of the L-11 tiling search (default `Logic.TileBudget` = D3a's 20 000).</summary>
        public readonly int? TileBudget;

        public LogicContext(int? fileId = null, ISet<string>? previousMechanics = null, IReadOnlyDictionary<string, int>? boosterUnlock = null,
            I18nKeySets? i18nKeys = null, string[]? only = null, int? tileBudget = null)
        {
            FileId = fileId;
            PreviousMechanics = previousMechanics;
            BoosterUnlock = boosterUnlock;
            I18nKeys = i18nKeys;
            Only = only;
            TileBudget = tileBudget;
        }

        /// <summary>The same context restricted to `only` (TS `{ ...ctx, only }`).</summary>
        public LogicContext WithOnly(string[] only) => new LogicContext(FileId, PreviousMechanics, BoosterUnlock, I18nKeys, only, TileBudget);
    }

    /// <summary>`validateLevelJson` result: the parsed level (null when the schema failed) and the issues.</summary>
    public sealed class ValidationResult
    {
        public readonly LevelData? Level;
        public readonly Issue[] Issues;

        public ValidationResult(LevelData? level, Issue[] issues)
        {
            Level = level;
            Issues = issues;
        }
    }

    /// <summary>K-49 sizes of a level as written (missing fields → GDD defaults 6, 8, 2, 8).</summary>
    public readonly struct LevelSizes
    {
        public readonly int Wy;
        public readonly int Hy;
        public readonly int Ws;
        public readonly int Hs;

        public LevelSizes(int wy, int hy, int ws, int hs)
        {
            Wy = wy;
            Hy = hy;
            Ws = ws;
            Hs = hs;
        }
    }

    /// <summary>A supply block of the Faz 2 whole-block assignment (`MaterialShortfall`, deprecated).</summary>
    public sealed class SupplyBlock
    {
        public readonly int ForSegment;
        public readonly string Color;
        public readonly int Cells;
        /// <summary>Paint gate colours this block can take on its way to the site (OBSTACLES W6).</summary>
        public readonly string[] Alts;

        public SupplyBlock(int forSegment, string color, int cells, string[]? alts = null)
        {
            ForSegment = forSegment;
            Color = color;
            Cells = cells;
            Alts = alts ?? new string[0];
        }
    }

    /// <summary>
    /// Level logic checks (docs/TECH_DESIGN.md §8.3 and the Faz 2R delta §2R.3; codes = GDD K-45 Faz 2R, D-005). Pure
    /// functions: the game runs the runtime subset at load time, `tools/validate-levels.ts` runs every validate-stage check
    /// (K-45 items 1–8 and 10). The solver stage (item 9: L-19, L-27, L-32…L-35, `tools/solve.ts`) and the bot (L-20) live
    /// in their own tools.
    ///
    /// Every finding is an `Issue { code, rule, check, severity, path, message }`. Any `error` means the level must not load.
    ///
    /// Order (Faz 2R): identity, then the K-49 sizes (L-28). Every later check reads the level geometry (`GeoFromLevel`),
    /// which only exists when the sizes fit the 8 × 10 frame; a level that does not fit stops after L-28. Checks whose
    /// answer is meaningless on a broken base are gated, not skipped silently: cover (L-10, L-30) and debris colours (L-29)
    /// need plans of exactly Hs × Ws without `.` and with every `?` resolved; the tiling (L-11) also needs valid shapes
    /// (L-04) and an exact cover (L-10, L-30), because D3a on a short or unplaceable supply only repeats those errors.
    /// </summary>
    public static class Logic
    {
        /// <summary>Validate-stage checks (TECH §8.3 + §2R.3). L-19, L-20, L-27 and L-32…L-35 are solver / bot stages.</summary>
        public static readonly string[] CheckIds =
        {
            "L-01", "L-02", "L-03", "L-04", "L-05", "L-06", "L-07", "L-08", "L-09", "L-10", "L-11", "L-12", "L-13", "L-14",
            "L-15", "L-16", "L-17", "L-18", "L-21", "L-22", "L-23", "L-24", "L-25", "L-26", "L-28", "L-29", "L-30", "L-31",
        };

        /// <summary>
        /// Checks the game runs when it loads a level (TECH §8.3, &lt; 1 ms): what the engine needs to build a sane board. The
        /// schema always runs. L-28 is first: `Compile` cannot build a geometry outside the frame. L-05 (`plan_size`,
        /// `plan_has_window`, `elevator_overflow`) is back in the runtime set since WP-M (TECH §2R.3 note 2).
        /// </summary>
        public static readonly string[] RuntimeChecks = { "L-02", "L-04", "L-05", "L-08", "L-09", "L-24", "L-25", "L-26", "L-28" };

        /// <summary>First level of each colour (BRIEF §6, GDD K-31; LEVELS §0: R "en erken" 4, first used in Faz 2R level 5).</summary>
        public static readonly IReadOnlyDictionary<string, int> ColorFirstLevel = new Dictionary<string, int>
        {
            { "W", 1 }, { "Y", 1 }, { "G", 2 }, { "R", 4 }, { "O", 11 }, { "C", 11 }, { "B", 21 }, { "P", 21 },
        };

        /// <summary>Colours per level by story chapter: 1 → 3, 2 → 4, 3–5 → 5 (GDD K-31).</summary>
        public static readonly IReadOnlyDictionary<int, int> ChapterColorLimit = new Dictionary<int, int> { { 1, 3 }, { 2, 4 }, { 3, 5 }, { 4, 5 }, { 5, 5 } };

        /// <summary>Sawtooth plan (LEVELS §0).</summary>
        public static readonly HashSet<int> HardLevels = new HashSet<int> { 10, 15, 25, 35, 45, 49 };
        public static readonly HashSet<int> SuperhardLevels = new HashSet<int> { 20, 30, 40, 50 };

        /// <summary>Highlight name → economy.json booster key (TECH §8.3 L-17).</summary>
        public static readonly IReadOnlyDictionary<string, string> HighlightBoosterKey = new Dictionary<string, string>
        {
            { "hammer", "hammer" }, { "crane", "crane" }, { "undo", "undo" }, { "brush", "paintBrush" }, { "thermos", "thermos" },
            { "trowel", "trowelStart" }, { "shutter", "openShutter" },
        };

        /// <summary>GDD K-49 / K-45/2 product ranges (product-lead), inside the code-lead frame of Geometry.cs: yard width Wy.</summary>
        public static readonly int[] YardColsRange = { 3, 6 };
        /// <summary>Yard height Hy.</summary>
        public static readonly int[] YardRowsRange = { 4, 8 };
        /// <summary>Site plan height Hs.</summary>
        public static readonly int[] SiteRowsRange = { 4, 8 };

        /// <summary>Site width Ws: 2–3 in story chapters 1–3 (levels 1–30), 2–4 in chapters 4–5 (K-49).</summary>
        public static int[] SiteColsRange(int chapter) => chapter <= 3 ? new[] { 2, 3 } : new[] { 2, Geometry.MaxSiteCols };

        /// <summary>Free yard cells at the start (K-02): `2 ≤ E ≤ ⌊0,4·C⌋`.</summary>
        public const int YardEmptyMin = 2;

        public static int YardEmptyMax(int cells) => (2 * cells) / 5;

        /// <summary>Expansion budget of the L-11 tiling search: D3a's `D3aMaxExpansions` (GDD K-30, TECH §2R.4).</summary>
        public const int TileBudget = 20_000;

        /// <summary>K-53/1: tutorial steps per level.</summary>
        public const int TutorialMaxSteps = 2;

        private static readonly Dictionary<string, string> CheckRule = new Dictionary<string, string>
        {
            { "L-01", "K-45/1" }, { "L-02", "K-45/2" }, { "L-03", "K-45/2" }, { "L-04", "K-45/5" }, { "L-05", "K-45/4" }, { "L-06", "K-45/4" },
            { "L-07", "K-45/4" }, { "L-08", "K-45/4" }, { "L-09", "K-45/3" }, { "L-10", "K-45/8" }, { "L-11", "K-45/8" }, { "L-12", "K-25" },
            { "L-13", "K-45/7" }, { "L-14", "K-45/6" }, { "L-15", "K-45/6" }, { "L-16", "K-45/10" }, { "L-17", "GDD 14.1" }, { "L-18", "LEVELS 0" },
            { "L-21", "K-45/5" }, { "L-22", "K-45/10" }, { "L-23", "K-45/6" }, { "L-24", "K-24" }, { "L-25", "K-23" }, { "L-26", "Y4" },
            { "L-28", "K-45/2" }, { "L-29", "K-45/7" }, { "L-30", "K-45/8" }, { "L-31", "K-45/10" },
        };

        /// <summary>`CHECK_RULE[check]`.</summary>
        public static string RuleOf(string check) => CheckRule[check];

        private delegate void Push(string check, string code, string path, string message, string severity = Severities.Error);

        /// <summary>Cells of a shape anchored at (x, y).</summary>
        public static BoardCell[] CellsAt(ShapeDef shape, int x, int y)
        {
            var o = new BoardCell[shape.Cells.Length];
            for (int i = 0; i < o.Length; i++) o[i] = new BoardCell(x + shape.Cells[i].X, y + shape.Cells[i].Y);
            return o;
        }

        public static LevelSizes LevelSizesOf(LevelData level) => new LevelSizes(
            level.Yard.Cols ?? Geometry.DefaultSizes.Wy,
            level.Yard.Rows ?? Geometry.DefaultSizes.Hy,
            level.Site?.Cols ?? Geometry.DefaultSizes.Ws,
            level.Site?.Rows ?? Geometry.DefaultSizes.Hs);

        /// <summary>The sizes fit the 8 × 10 frame, so `GeoFromLevel` can build the geometry (Geometry.cs `MakeGeo` limits).</summary>
        public static bool FitsFrame(LevelSizes z)
        {
            bool InRange(int v, int lo, int hi) => v >= lo && v <= hi;
            return InRange(z.Wy, 1, Geometry.MaxCols - 1)
                && InRange(z.Ws, 1, Geometry.MaxSiteCols)
                && z.Wy + z.Ws <= Geometry.MaxCols
                && InRange(z.Hy, 1, Geometry.MaxBoardRows)
                && InRange(z.Hs, 1, Geometry.MaxBoardRows);
        }

        /// <summary>
        /// A material block can reach the site over the wall (K-05: box height ≤ (H + 2) − height) or through a gap whose
        /// rows hold all of its rows (K-12). Ağır Yük (I5/Q9) never crosses the boundary (Y5, K-44).
        /// </summary>
        public static bool CanReachSite(ShapeDef shape, LevelData level)
        {
            if (Mechanics.IsCargo(shape)) return false;
            int open = Mechanics.BoardHeight(level) + 2 - level.Wall.Height;
            if (shape.H <= open) return true;
            foreach (var g in level.Wall.Gaps) if (shape.H <= g.Size) return true;
            return false;
        }

        private static void GapRows(GapData g, out int lo, out int hi)
        {
            if (g.Type == "slider")
            {
                lo = g.Range![0];
                hi = g.Range[1] + g.Size - 1;
            }
            else
            {
                lo = g.Y;
                hi = g.Y + g.Size - 1;
            }
        }

        private static string[] FlagsOf(PieceData p) => p.Flags ?? new string[0];

        private static bool HasFlagName(PieceData p, string flag) => Array.IndexOf(FlagsOf(p), flag) >= 0;

        /// <summary>One block of the level data (batch pieces and debris).</summary>
        private sealed class Placed
        {
            public readonly string Path;
            public readonly string Id;
            public readonly ShapeDef Shape;
            /// <summary>Ağır Yük (I5/Q9): no colour, not material (K-44).</summary>
            public readonly bool Cargo;
            public readonly string Color;
            /// <summary>Batch index (0 start yard, k ≥ 1 truck), −1 for debris.</summary>
            public readonly int Batch;
            /// <summary>Segment the block becomes available for: the batch's `forSegment`, the debris segment.</summary>
            public readonly int From;
            public readonly bool Debris;
            public readonly PieceData? Piece;

            public Placed(string path, string id, ShapeDef shape, bool cargo, string color, int batch, int from, bool debris, PieceData? piece)
            {
                Path = path;
                Id = id;
                Shape = shape;
                Cargo = cargo;
                Color = color;
                Batch = batch;
                From = from;
                Debris = debris;
                Piece = piece;
            }
        }

        private static List<Placed> AllPlaced(LevelData level)
        {
            var o = new List<Placed>();
            for (int bi = 0; bi < level.Yard.Batches.Length; bi++)
            {
                var b = level.Yard.Batches[bi];
                for (int i = 0; i < b.Pieces.Length; i++)
                {
                    var p = b.Pieces[i];
                    var shape = Shapes.ShapeById(p.Shape);
                    o.Add(new Placed("yard.batches[" + bi + "].pieces[" + i + "]", p.Shape, shape, Mechanics.IsCargo(shape), p.Color, bi, b.ForSegment, false, p));
                }
            }
            var debris = level.Build.Debris;
            if (debris != null)
            {
                for (int i = 0; i < debris.Length; i++)
                {
                    var d = debris[i];
                    var shape = Shapes.ShapeById(d.Shape);
                    o.Add(new Placed("build.debris[" + i + "]", d.Shape, shape, Mechanics.IsCargo(shape), d.Color, -1, d.Segment ?? 0, true, null));
                }
            }
            return o;
        }

        /// <summary>Material blocks (K-47 supply): every batch piece and debris except Ağır Yük.</summary>
        private static List<Placed> MaterialBlocks(LevelData level)
        {
            var o = new List<Placed>();
            foreach (var p in AllPlaced(level)) if (!p.Cargo) o.Add(p);
            return o;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Schema (L-01) and the codes the schema stage maps (L-05 plan_size, L-17 tut_key_missing, L-31 tut_blocking)
        // ---------------------------------------------------------------------------------------------------------------

        private static string FormatPath(object[] path)
        {
            var sb = new StringBuilder();
            foreach (var k in path)
            {
                if (k is int i) sb.Append('[').Append(i).Append(']');
                else
                {
                    if (sb.Length > 0) sb.Append('.');
                    sb.Append((string)k);
                }
            }
            return sb.Length == 0 ? "(root)" : sb.ToString();
        }

        /// <summary>A tutorial step field that would dim or lock the board (K-53/2: no spotlight, no dimming).</summary>
        // TS `/^(spot|dim)/i`: spelled with ASCII classes so no .NET case-folding rule (U+017F, U+212A, Turkish i) can join the match.
        private static readonly Regex BlockingField = new Regex("^(?:[sS][pP][oO][tT]|[dD][iI][mM])", RegexOptions.CultureInvariant);
        private static readonly JsRegex PlanRowChars = new JsRegex("^[WYGROCBP.?]*$");

        /// <summary>TS `valueAt(json, path)`: null when the path leaves the data (`undefined`).</summary>
        private static JsonValue? ValueAt(JsonValue json, object[] path)
        {
            JsonValue? acc = json;
            foreach (var k in path)
            {
                if (acc == null) return null;
                if (k is int i)
                {
                    if (!acc.IsArray || i < 0 || i >= acc.Count) return null;
                    acc = acc[i];
                }
                else
                {
                    if (!acc.IsObject) return null;
                    acc = acc[(string)k];
                }
            }
            return acc;
        }

        /// <summary>`JSON.stringify(value)` of a looked-up value; `undefined` for a missing one.</summary>
        private static string Stringify(JsonValue? v) => v == null ? "undefined" : JsonWriter.Write(v, false);

        /// <summary>
        /// Maps zod issues to K-45 issues. A plan of the wrong row count or a row of valid characters and the wrong width is
        /// `plan_size` (L-05); a tutorial `mode` the schema cannot read and a spotlight / dim field of a step are `tut_blocking`
        /// (L-31); a `textKey` outside the GDD §14.1/1 format is `tut_key_missing` (L-17, TECH §2R.2); everything else is
        /// `schema_invalid`.
        /// </summary>
        public static Issue[] IssuesFromSchema(IReadOnlyList<SchemaIssue> issues, JsonValue json)
        {
            Issue Make(string check, string code, string path, string message) => new Issue(code, CheckRule[check], check, Severities.Error, path, message);
            var o = new List<Issue>();
            foreach (var iss in issues)
            {
                var p = iss.Path;
                string path = FormatPath(p);
                if (p.Length >= 4 && Eq(p[0], "build") && Eq(p[1], "segments") && Eq(p[3], "rows"))
                {
                    if (p.Length == 4 && (iss.Code == "too_small" || iss.Code == "too_big"))
                    {
                        o.Add(Make("L-05", "plan_size", path, "a plan has 1–8 rows (exactly site.rows, K-15): " + iss.Message));
                        continue;
                    }
                    var row = ValueAt(json, p);
                    if (p.Length == 5 && row != null && row.IsString && PlanRowChars.IsMatch(row.AsString))
                    {
                        o.Add(Make("L-05", "plan_size", path, "plan row \"" + row.AsString + "\" must have exactly site.cols characters (K-15)"));
                        continue;
                    }
                }
                if (p.Length >= 2 && Eq(p[0], "tutorial") && p[1] is int)
                {
                    if (p.Length == 3 && Eq(p[2], "mode"))
                    {
                        o.Add(Make("L-31", "tut_blocking", path, "mode may only be \"soft\" (K-53/2): " + iss.Message));
                        continue;
                    }
                    if (p.Length == 3 && Eq(p[2], "textKey"))
                    {
                        o.Add(Make("L-17", "tut_key_missing", path,
                            "textKey " + Stringify(ValueAt(json, p)) + " is not tut.l<n>.<topic>, tut.m.<topic> or tut.ctx.<topic> (GDD 14.1/1)"));
                        continue;
                    }
                    if (p.Length == 2 && iss.Code == "unrecognized_keys")
                    {
                        var other = new List<string>();
                        foreach (var k in iss.Keys ?? new string[0])
                        {
                            if (BlockingField.IsMatch(k)) o.Add(Make("L-31", "tut_blocking", path + "." + k, "\"" + k + "\": a step never dims or locks the board (K-53/2)"));
                            else other.Add(k);
                        }
                        if (other.Count > 0)
                        {
                            var quoted = new string[other.Count];
                            for (int i = 0; i < other.Count; i++) quoted[i] = "\"" + other[i] + "\"";
                            o.Add(Make("L-01", "schema_invalid", path, "unrecognized key(s): " + string.Join(", ", quoted)));
                        }
                        continue;
                    }
                }
                o.Add(Make("L-01", "schema_invalid", path, iss.Message));
            }
            return o.ToArray();
        }

        private static bool Eq(object k, string s) => k is string str && str == s;

        /// <summary>Parses JSON with the schema and, when it passes, runs the logic checks.</summary>
        public static ValidationResult ValidateLevelJson(JsonValue json, LogicContext? ctx = null)
        {
            var schemaIssues = new List<SchemaIssue>();
            var level = LevelJson.Parse(json, schemaIssues);
            if (level == null) return new ValidationResult(null, IssuesFromSchema(schemaIssues, json));
            return new ValidationResult(level, CheckLevel(level, ctx));
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Logic checks
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>Runs the logic checks (L-01 data part … L-31) on a schema-valid level.</summary>
        public static Issue[] CheckLevel(LevelData level, LogicContext? ctx = null)
        {
            ctx ??= new LogicContext();
            var issues = new List<Issue>();
            var enabled = new HashSet<string>(ctx.Only ?? CheckIds);
            Push push = (check, code, path, message, severity) =>
            {
                if (enabled.Contains(check)) issues.Add(new Issue(code, CheckRule[check], check, severity, path, message));
            };

            if (enabled.Contains("L-01")) CheckIdentity(level, ctx, push);
            // K-49 first: the geometry of every later check exists only inside the frame.
            if (!CheckSizes(level, push)) return issues.ToArray();
            var geo = Geometry.GeoFromLevel(level);
            var built = Plan.BuildPlans(level, geo);
            var plans = built.Plans;
            var problems = built.Problems;
            // Gates are computed whatever `only` asks for; `push` drops the issues of disabled checks.
            bool plansShaped = CheckPlans(level, geo, push);
            bool plansUsable = plansShaped && problems.Length == 0;
            bool shapesOk = CheckShapes(level, geo, push);
            bool coverOk = plansUsable && CheckCover(level, plans, push);

            if (enabled.Contains("L-02") || enabled.Contains("L-03")) CheckYard(level, geo, push);
            if (enabled.Contains("L-06") || enabled.Contains("L-07")) CheckColors(level, plans, push);
            if (enabled.Contains("L-08"))
            {
                foreach (var p in problems) push("L-08", "hidden_invalid", "build.segments[" + p.Seg + "]", p.Message);
            }
            if (enabled.Contains("L-09")) CheckWall(level, geo, push);
            if (enabled.Contains("L-11") && coverOk && shapesOk) CheckTiling(level, plans, geo, ctx.TileBudget ?? TileBudget, push);
            if (enabled.Contains("L-12")) CheckBatches(level, geo, push);
            if (enabled.Contains("L-13") || enabled.Contains("L-29")) CheckDebris(level, plans, geo, plansUsable, push);
            if (enabled.Contains("L-14") || enabled.Contains("L-23")) CheckHiddenItems(level, push);
            if (enabled.Contains("L-15")) CheckGoals(level, push);
            if ((enabled.Contains("L-16") || enabled.Contains("L-22")) && ctx.PreviousMechanics != null) CheckMechanics(level, ctx.PreviousMechanics, push);
            if (enabled.Contains("L-17")) CheckTutorial(level, geo, ctx, push);
            if (enabled.Contains("L-18")) CheckSawtooth(level, push);
            if (enabled.Contains("L-21")) CheckFlagCombos(level, push);
            if (enabled.Contains("L-24")) CheckElevatorRange(level, push);
            if (enabled.Contains("L-25")) CheckCarousel(level, push);
            if (enabled.Contains("L-26")) CheckWet(level, push);
            if (enabled.Contains("L-31")) CheckTutorialLoad(level, push);
            return issues.ToArray();
        }

        /// <summary>L-01: id 1–50, chapter = ceil(id/10), file number; `targets` bands are `[low, high]` with low ≤ high.</summary>
        private static void CheckIdentity(LevelData level, LogicContext ctx, Push push)
        {
            if (level.Id < 1 || level.Id > 50) push("L-01", "id_mismatch", "id", "id " + level.Id + " is outside 1–50");
            if (ctx.FileId.HasValue && ctx.FileId.Value != level.Id)
                push("L-01", "id_mismatch", "id", "id " + level.Id + " does not match the file name (level " + ctx.FileId.Value + ")");
            int chapter = (level.Id + 9) / 10;
            if (level.Chapter != chapter) push("L-01", "chapter_mismatch", "chapter", "chapter " + level.Chapter + " ≠ ceil(id / 10) = " + chapter);
            var t = level.Targets;
            if (t != null)
            {
                void Band(string name, int[]? band)
                {
                    if (band != null && band[0] > band[1])
                        push("L-01", "schema_invalid", "targets." + name, "band [" + band[0] + ", " + band[1] + "] needs low ≤ high");
                }
                Band("minShifts", t.MinShifts);
                Band("firstNeedDepth", t.FirstNeedDepth);
                Band("choices0", t.Choices0);
                if (t.DeadRate != null && t.DeadRate[0] > t.DeadRate[1])
                    push("L-01", "schema_invalid", "targets.deadRate",
                        "band [" + JsNumber.Format(t.DeadRate[0]) + ", " + JsNumber.Format(t.DeadRate[1]) + "] needs low ≤ high");
            }
        }

        /// <summary>
        /// L-28 (K-49, K-45/2): `size_out_of_range` (Wy 3–6, Hy 4–8, Hs 4–8, Ws 2–3 in story chapters 1–3 and 2–4 in 4–5),
        /// `board_too_wide` (Wy + Ws &gt; 8). Returns whether the sizes fit the frame (false: the geometry cannot be built and the
        /// remaining checks are skipped).
        /// </summary>
        private static bool CheckSizes(LevelData level, Push push)
        {
            var z = LevelSizesOf(level);
            void Out(string path, string name, int v, int[] range, string why = "")
            {
                if (v < range[0] || v > range[1])
                    push("L-28", "size_out_of_range", path, name + " = " + v + " is outside " + range[0] + "–" + range[1] + why + " (K-49)");
            }
            Out("yard.cols", "Wy", z.Wy, YardColsRange);
            Out("yard.rows", "Hy", z.Hy, YardRowsRange);
            Out("site.cols", "Ws", z.Ws, SiteColsRange(level.Chapter), " in story chapter " + level.Chapter);
            Out("site.rows", "Hs", z.Hs, SiteRowsRange);
            if (z.Wy + z.Ws > Geometry.MaxCols)
                push("L-28", "board_too_wide", "site.cols", "Wy + Ws = " + z.Wy + " + " + z.Ws + " > " + Geometry.MaxCols + " (K-49, 120 px cells)");
            return FitsFrame(z);
        }

        /// <summary>
        /// L-05 (K-15, K-24, K-45/4): every segment is exactly Hs rows of exactly Ws characters (`plan_size`), no `.`
        /// (`plan_has_window`, R2-01: the plan covers the site); with an elevator `Hs + b ≤ 8` (`elevator_overflow`). Returns
        /// whether every plan is Hs × Ws without `.` (the cover and debris checks need that).
        /// </summary>
        private static bool CheckPlans(LevelData level, BoardGeo geo, Push push)
        {
            bool ok = true;
            for (int i = 0; i < level.Build.Segments.Length; i++)
            {
                var s = level.Build.Segments[i];
                string path = "build.segments[" + i + "].rows";
                if (s.Rows.Length != geo.Hs)
                {
                    ok = false;
                    push("L-05", "plan_size", path, s.Rows.Length + " rows; the site is Hs = " + geo.Hs + " rows (site.rows, K-15)");
                }
                for (int j = 0; j < s.Rows.Length; j++)
                {
                    string row = s.Rows[j];
                    if (row.Length != geo.Ws)
                    {
                        ok = false;
                        push("L-05", "plan_size", path + "[" + j + "]", "\"" + row + "\" has " + row.Length + " characters; the site is Ws = " + geo.Ws + " columns (site.cols, K-15)");
                    }
                    if (row.IndexOf('.') >= 0)
                    {
                        ok = false;
                        push("L-05", "plan_has_window", path + "[" + j + "]", "\"" + row + "\": \".\" (S2 Plan Boşluğu) is out of the MVP; the plan covers the whole site (K-15, R2-01)");
                    }
                }
            }
            var el = level.Build.Elevator;
            if (el != null && geo.Hs + el.Range[1] > Geometry.MaxBoardRows)
                push("L-05", "elevator_overflow", "build.elevator", "Hs " + geo.Hs + " + b " + el.Range[1] + " > " + Geometry.MaxBoardRows + " (K-24)");
            return ok;
        }

        /// <summary>
        /// L-02 `overlap` / `out_of_yard`, L-03 `yard_fill_high` / `yard_fill_low` (K-02, K-49). Occupying entities: batch-0
        /// pieces (material and Ağır Yük), crates and cement bags; every obstacle lies in the yard (x ≤ Wy − 1, y ≤ Hy − 1).
        /// </summary>
        private static void CheckYard(LevelData level, BoardGeo geo, Push push)
        {
            var owner = new Dictionary<int, string>();
            bool InYard(int x, int y) => x >= 0 && x < geo.Wy && y >= 0 && y < geo.Hy;
            string where = "x 0–" + (geo.Wy - 1) + ", y 0–" + (geo.Hy - 1);
            void Claim(int x, int y, string path)
            {
                int key = y * geo.Wy + x;
                if (owner.TryGetValue(key, out var prev)) push("L-02", "overlap", path, "cell (" + x + "," + y + ") is already taken by " + prev);
                else owner[key] = path;
            }
            if (level.Yard.Batches.Length > 0)
            {
                var b0 = level.Yard.Batches[0];
                for (int i = 0; i < b0.Pieces.Length; i++)
                {
                    var p = b0.Pieces[i];
                    string path = "yard.batches[0].pieces[" + i + "]";
                    var cells = CellsAt(Shapes.ShapeById(p.Shape), p.X, p.Y);
                    bool outside = false;
                    foreach (var c in cells) if (!InYard(c.X, c.Y)) outside = true;
                    if (outside)
                    {
                        push("L-02", "out_of_yard", path, p.Shape + " at (" + p.X + "," + p.Y + ") leaves the yard (" + where + ")");
                        continue;
                    }
                    foreach (var c in cells) Claim(c.X, c.Y, path);
                }
            }
            for (int i = 0; i < level.Obstacles.Length; i++)
            {
                var o = level.Obstacles[i];
                string path = "obstacles[" + i + "]";
                if (!InYard(o.X, o.Y))
                {
                    push("L-02", "out_of_yard", path, o.Type + " at (" + o.X + "," + o.Y + ") is outside the yard (" + where + ")");
                    continue;
                }
                if (o.Type == "crate" || o.Type == "cement_bag") Claim(o.X, o.Y, path);
            }
            int cells2 = geo.Wy * geo.Hy;
            int empty = cells2 - owner.Count;
            int max = YardEmptyMax(cells2);
            if (empty < YardEmptyMin)
                push("L-03", "yard_fill_high", "yard", "E = " + empty + " empty cells < " + YardEmptyMin + " (C = " + cells2 + ", K-02)");
            else if (empty > max)
                push("L-03", "yard_fill_low", "yard", "E = " + empty + " empty cells > ⌊0,4·" + cells2 + "⌋ = " + max + " (K-02)");
        }

        /// <summary>
        /// L-04 (K-44, K-45/5): forbidden orientations; I5/Q9 only in the yard (never debris); kinds per story chapter; Ağır Yük
        /// only from level 8; a material block's data orientation at most Ws wide (`piece_too_wide`). Returns whether no error
        /// was found.
        /// </summary>
        private static bool CheckShapes(LevelData level, BoardGeo geo, Push push)
        {
            bool ok = true;
            void Bad(string code, string path, string message)
            {
                ok = false;
                push("L-04", code, path, message);
            }
            foreach (var b in AllPlaced(level))
            {
                if (Shapes.ForbiddenShapes.Contains(b.Id)) Bad("shape_forbidden", b.Path, b.Id + " is forbidden in level data (K-44)");
                if (b.Debris && b.Cargo) Bad("shape_forbidden", b.Path, b.Shape.Kind + " (Ağır Yük) may only be used in the yard");
                int first = Shapes.KindFirstChapter[b.Shape.Kind];
                if (first > level.Chapter)
                    Bad("shape_locked", b.Path, b.Shape.Kind + " unlocks in story chapter " + first + " (level is chapter " + level.Chapter + ")");
                if (b.Cargo && level.Id < Shapes.HeavyMinLevelId)
                    Bad("shape_locked", b.Path, "Ağır Yük " + b.Id + " is allowed only from level " + Shapes.HeavyMinLevelId);
                if (!b.Cargo && b.Shape.W > geo.Ws)
                    Bad("piece_too_wide", b.Path, b.Id + " is " + b.Shape.W + " wide; a material block is at most Ws = " + geo.Ws + " wide (the player cannot turn it, K-44)");
            }
            return ok;
        }

        /// <summary>
        /// Colour set (GDD K-31): plan cells (resolved `?`) ∪ every material block (all batches, debris) ∪ paint gate colours.
        /// Ağır Yük has no colour (K-44). `plans` defaults to the level's own plans.
        /// </summary>
        public static string[] LevelColorSet(LevelData level, SegmentPlan[]? plans = null)
        {
            plans ??= Plan.BuildPlans(level).Plans;
            var set = new HashSet<string>();
            foreach (var plan in plans)
                foreach (char c in plan.Cells)
                    if (c != Plan.None && c != Plan.Dot) set.Add(c.ToString());
            foreach (var b in MaterialBlocks(level)) set.Add(b.Color);
            foreach (var g in level.Wall.Gaps) if (g.Type == "paint" && g.Color != null) set.Add(g.Color);
            var o = new List<string>();
            foreach (var c in ColorCodes.All) if (set.Contains(c)) o.Add(c);
            return o.ToArray();
        }

        private static void CheckColors(LevelData level, SegmentPlan[] plans, Push push)
        {
            var colors = LevelColorSet(level, plans);
            int limit = ChapterColorLimit.TryGetValue(level.Chapter, out int l) ? l : 5;
            if (colors.Length > limit)
                push("L-06", "too_many_colors", "level", colors.Length + " colours (" + string.Join(" ", colors) + ") > " + limit + " for chapter " + level.Chapter);
            var locked = new List<string>();
            foreach (var c in colors) if (ColorFirstLevel[c] > level.Id) locked.Add(c + " unlocks at level " + ColorFirstLevel[c]);
            if (locked.Count > 0) push("L-07", "color_locked", "level", string.Join("; ", locked));
        }

        /// <summary>
        /// L-09 (K-04, K-45/3, OBSTACLES W4/W5/W7/W8). Faz 2R: `0 ≤ height ≤ H`; a wall above the board height has no K-45
        /// code of its own and is reported as `size_out_of_range` on `wall.height` (TECH §2R.3 note).
        /// </summary>
        private static void CheckWall(LevelData level, BoardGeo geo, Push push)
        {
            int height = level.Wall.Height;
            var gaps = level.Wall.Gaps;
            if (height > geo.H) push("L-09", "size_out_of_range", "wall.height", "height " + height + " > board height H = " + geo.H + " (K-04, K-49)");
            for (int i = 0; i < gaps.Length; i++)
            {
                var g = gaps[i];
                string path = "wall.gaps[" + i + "]";
                if (g.Type != "slider" && g.Y + g.Size > height - 1)
                    push("L-09", "gap_touches_top", path, "y + size = " + (g.Y + g.Size) + " > height − 1 = " + (height - 1));
                if (g.Type == "shutter" && g.Phase >= 2 * g.Period)
                    push("L-09", "shutter_phase", path, "phase " + g.Phase + " must be < 2·period = " + (2 * g.Period));
                if (g.Type == "slider")
                {
                    int a = g.Range![0];
                    int b = g.Range[1];
                    if (!(a < b)) push("L-09", "slider_range", path, "range [" + a + "," + b + "] needs a < b");
                    if (g.Y < a || g.Y > b) push("L-09", "slider_range", path, "start y " + g.Y + " is outside range [" + a + "," + b + "]");
                    if (b + g.Size > height - 1)
                        push("L-09", "slider_range", path, "range end " + b + " + size " + g.Size + " > height − 1 = " + (height - 1));
                }
                if (g.Type == "locked")
                {
                    bool found = false;
                    foreach (var o in level.Obstacles) if (o.Type == "key" && o.Id == g.KeyId) found = true;
                    if (!found) push("L-09", "key_missing", path, "no key obstacle with id \"" + g.KeyId + "\"");
                }
            }
            for (int i = 0; i < gaps.Length; i++)
            {
                for (int j = i + 1; j < gaps.Length; j++)
                {
                    GapRows(gaps[i], out int a0, out int a1);
                    GapRows(gaps[j], out int b0, out int b1);
                    if (a0 <= b1 && b0 <= a1)
                        push("L-09", "gap_overlap", "wall.gaps[" + j + "]", "rows " + b0 + "–" + b1 + " overlap gap " + i + " rows " + a0 + "–" + a1);
                }
            }
            if (level.Wall.Fan != null)
            {
                bool narrow = false;
                foreach (var p in AllPlaced(level)) if (!p.Debris && !p.Cargo && p.Shape.W == 1) narrow = true;
                if (!narrow) push("L-09", "fan_unused", "wall.fan", "wind fan in a level without 1-wide blocks", Severities.Warn);
            }
        }

        // ---------------------------------------------------------------------------------------------------------------
        // L-30, L-10 full cover (K-47): supply = demand per colour; cumulative demand ≤ cumulative supply
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>Plan cells per colour (`?` resolved, K-32) of the given segments (up to and including `upTo`).</summary>
        private static int[] DemandOf(SegmentPlan[] plans, int upTo)
        {
            var o = new int[ColorCodes.All.Length];
            for (int k = 0; k < plans.Length; k++)
            {
                if (k > upTo) continue;
                foreach (char c in plans[k].Cells)
                {
                    if (c == Plan.None || c == Plan.Dot) continue;
                    int ci = ColorCodes.IndexOf(c.ToString());
                    if (ci >= 0) o[ci] += 1;
                }
            }
            return o;
        }

        /// <summary>Material cells per colour (K-47 supply: batches and debris, Ağır Yük excluded) available up to segment `upTo`.</summary>
        private static int[] SupplyOf(LevelData level, int upTo)
        {
            var o = new int[ColorCodes.All.Length];
            foreach (var b in MaterialBlocks(level))
            {
                if (b.From > upTo) continue;
                int ci = ColorCodes.IndexOf(b.Color);
                if (ci >= 0) o[ci] += b.Shape.CellCount;
            }
            return o;
        }

        private static int Total(int[] c)
        {
            int n = 0;
            foreach (int v in c) n += v;
            return n;
        }

        /// <summary>
        /// L-30 `cover_mismatch` (K-47/1): supply = demand for every colour; with a W6 paint gate only the totals (the solver
        /// proves the colours). L-10 `cover_prefix_short` (K-47/2, `segments` without W6): for every segment k and colour c the
        /// demand of segments 0…k ≤ the supply of batches 0…k (by `forSegment`) and the debris of segments 0…k. The last
        /// prefix is the whole level, so it is left to L-30 (no double report). Returns whether no error was found.
        /// </summary>
        private static bool CheckCover(LevelData level, SegmentPlan[] plans, Push push)
        {
            bool ok = true;
            var demand = DemandOf(plans, plans.Length - 1);
            var supply = SupplyOf(level, int.MaxValue);
            bool paint = false;
            foreach (var g in level.Wall.Gaps) if (g.Type == "paint") paint = true;
            if (paint)
            {
                if (Total(supply) != Total(demand))
                {
                    ok = false;
                    push("L-30", "cover_mismatch", "level", "material cells " + Total(supply) + " ≠ plan cells " + Total(demand) + " (W6 level: totals, K-47/1)");
                }
                return ok;
            }
            for (int c = 0; c < ColorCodes.All.Length; c++)
            {
                if (supply[c] == demand[c]) continue;
                ok = false;
                push("L-30", "cover_mismatch", "level", "colour " + ColorCodes.All[c] + ": material cells " + supply[c] + " ≠ plan cells " + demand[c] + " (K-47/1)");
            }
            if (level.Build.Mode != "segments") return ok;
            var reported = new HashSet<int>();
            for (int k = 0; k < plans.Length - 1; k++)
            {
                var need = DemandOf(plans, k);
                var have = SupplyOf(level, k);
                for (int c = 0; c < ColorCodes.All.Length; c++)
                {
                    if (need[c] <= have[c] || reported.Contains(c)) continue;
                    reported.Add(c);
                    ok = false;
                    push("L-10", "cover_prefix_short", "build.segments[" + k + "]",
                        "colour " + ColorCodes.All[c] + ": segments 0–" + k + " need " + need[c] + " cells, batches 0–" + k + " and their debris bring " + have[c] + " (K-47/2)");
                }
            }
            return ok;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // L-11 `untileable` (K-45/8, CL-2R-09): D3a on the start state — exact bottom-up cover of every segment
        // ---------------------------------------------------------------------------------------------------------------

        private sealed class LevelTileClass
        {
            public readonly ShapeDef Shape;
            /// <summary>Own colour first, then the W6 joker colours (paint gates the block's box height fits, TECH §2R.4 step 1).</summary>
            public readonly string[] Colors;
            /// <summary>First 0-based segment index the class may fill (`availableFrom`; 0 in carousel mode).</summary>
            public readonly int From;

            public LevelTileClass(ShapeDef shape, string[] colors, int from)
            {
                Shape = shape;
                Colors = colors;
                From = from;
            }
        }

        /// <summary>
        /// D3a (GDD K-30, TECH §2R.4) on the level's start state, from the level data: can the material blocks cover every
        /// segment exactly, bottom-up? Steps as in §2R.4:
        /// 1. Classes (shape, colours, `availableFrom`) with counts; the start profile of every segment is empty (nothing is
        ///    correctly placed at the start: debris starts wrong, L-29). Batch blocks are available from their `forSegment`,
        ///    debris from its segment. W6 joker: a block whose box height fits a paint gate may take that gate's colour.
        /// 2. The lowest column of the active segment (leftmost on a tie) is covered next by a class in its fixed data
        ///    orientation, shifted so one of its columns starts there; every block column must start exactly at that column's
        ///    height (K-34, which with a full plan also rules out holes) and every cell must have the plan colour.
        /// 3. Failed states are memoised by (segment, column profile, class counts).
        /// 4. The search is depth first in a fixed order (class index, shift) and stops after `budget` expansions: `unknown`
        ///    then, never `dead`.
        /// `segments` mode tiles the segments in order; `carousel` tiles them in index order from one shared pool and ignores
        /// `availableFrom` (the delivery order depends on the completion order there; a relaxation, so never a false `dead`).
        /// Access (walls, gaps, digging) is not considered: that is the solver's (L-19) and D3b's. Ağır Yük is not material.
        /// Returns a `TileResults` value.
        /// </summary>
        public static string TileLevel(LevelData level, SegmentPlan[] plans, BoardGeo? geo = null, int budget = TileBudget)
        {
            geo ??= Geometry.GeoFromLevel(level);
            bool carousel = level.Build.Mode == "carousel";
            var paint = new List<GapData>();
            foreach (var g in level.Wall.Gaps) if (g.Type == "paint") paint.Add(g);
            List<LevelTileClass> classes = new List<LevelTileClass>();
            List<int> counts = new List<int>();
            var index = new Dictionary<string, int>();
            foreach (var b in MaterialBlocks(level))
            {
                var shape = Shapes.ShapeById(b.Shape.Canonical);
                var colors = new List<string> { b.Color };
                foreach (var g in paint)
                    if (shape.H <= g.Size && g.Color != null && !colors.Contains(g.Color)) colors.Add(g.Color);
                int from = carousel ? 0 : b.From;
                string key = shape.Index + ":" + string.Join("", colors) + ":" + from;
                if (!index.TryGetValue(key, out int i))
                {
                    i = classes.Count;
                    classes.Add(new LevelTileClass(shape, colors.ToArray(), from));
                    counts.Add(0);
                    index[key] = i;
                }
                counts[i] = counts[i] + 1;
            }

            int S = plans.Length;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int[] heights = new int[ws];
            // Numeric memo key when it fits 2^52: segment, profile (base hs + 1), counts (mixed radix count + 1).
            int[] radix = new int[counts.Count];
            for (int i = 0; i < radix.Length; i++) radix[i] = counts[i] + 1;
            double space = S * Math.Pow(hs + 1, ws);
            foreach (int r in radix) space *= r;
            bool numeric = space < 4503599627370496.0;
            HashSet<long> failedNumeric = new HashSet<long>();
            HashSet<string> failedText = new HashSet<string>();
            int expansions = 0;
            long NumericKey(int seg)
            {
                long k = seg;
                foreach (int h in heights) k = k * (hs + 1) + h;
                for (int i = 0; i < counts.Count; i++) k = k * radix[i] + counts[i];
                return k;
            }
            string TextKey(int seg) => seg + "|" + string.Join(",", heights) + "|" + string.Join(",", counts);
            bool Failed(int seg) => numeric ? failedNumeric.Contains(NumericKey(seg)) : failedText.Contains(TextKey(seg));
            void MarkFailed(int seg)
            {
                if (numeric) failedNumeric.Add(NumericKey(seg));
                else failedText.Add(TextKey(seg));
            }

            // 1 = true, 0 = false, 2 = budget
            int Solve(int seg)
            {
                if (seg >= S) return 1;
                bool full = true;
                foreach (int h in heights) if (h != hs) full = false;
                if (full)
                {
                    for (int x = 0; x < ws; x++) heights[x] = 0;
                    int res0 = Solve(seg + 1);
                    for (int x = 0; x < ws; x++) heights[x] = hs;
                    return res0;
                }
                if (Failed(seg)) return 0;
                if (++expansions > budget) return 2;
                if (seg < 0 || seg >= plans.Length) return 0;
                var plan = plans[seg];
                int col = 0;
                for (int x = 1; x < ws; x++) if (heights[x] < heights[col]) col = x;
                int row = heights[col];
                for (int ti = 0; ti < classes.Count; ti++)
                {
                    var t = classes[ti];
                    if (counts[ti] == 0 || t.From > seg) continue;
                    var s = t.Shape;
                    for (int k = 0; k < s.W; k++)
                    {
                        int ax = col - k;
                        int ay = row - s.ColBottom[k];
                        if (ax < 0 || ax + s.W > ws || ay < 0) continue;
                        bool ok = true;
                        for (int j = 0; j < s.W && ok; j++)
                        {
                            int x = ax + j;
                            if (ay + s.ColBottom[j] != heights[x] || ay + s.ColTop[j] >= hs) ok = false;
                        }
                        if (!ok) continue;
                        var first = s.Cells[0];
                        char color = plan.Cells[Plan.LocalIndex(ax + first.X, ay + first.Y, ws)];
                        if (color == Plan.None || color == Plan.Dot || Array.IndexOf(t.Colors, color.ToString()) < 0) continue;
                        foreach (var c in s.Cells) if (plan.Cells[Plan.LocalIndex(ax + c.X, ay + c.Y, ws)] != color) ok = false;
                        if (!ok) continue;
                        var saved = new int[s.W];
                        Array.Copy(heights, ax, saved, 0, s.W);
                        for (int j = 0; j < s.W; j++) heights[ax + j] = ay + s.ColTop[j] + 1;
                        counts[ti] = counts[ti] - 1;
                        int res = Solve(seg);
                        counts[ti] = counts[ti] + 1;
                        Array.Copy(saved, 0, heights, ax, s.W);
                        if (res != 0) return res;
                    }
                }
                MarkFailed(seg);
                return 0;
            }

            int result = Solve(0);
            return result == 2 ? TileResults.Unknown : result == 1 ? TileResults.Ok : TileResults.Dead;
        }

        private static void CheckTiling(LevelData level, SegmentPlan[] plans, BoardGeo geo, int budget, Push push)
        {
            // `unknown` (budget spent) produces no code: the solver decides (K-45/8).
            if (TileLevel(level, plans, geo, budget) != TileResults.Dead) return;
            push("L-11", "untileable", "build.segments",
                "D3a: the material blocks cannot cover the plan exactly bottom-up (K-34) in their data orientation (a batch only from its segment on; K-30, K-47)");
        }

        // ---------------------------------------------------------------------------------------------------------------
        // L-12 … L-31
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>L-12 (K-25): truck pieces are written with `y = Hy`, every drop column fits the yard (`0 … Wy − w`).</summary>
        private static void CheckBatches(LevelData level, BoardGeo geo, Push push)
        {
            int S = level.Build.Segments.Length;
            var batches = level.Yard.Batches;
            if (batches.Length == 0 || batches[0].ForSegment != 0)
                push("L-12", "batch_invalid", "yard.batches[0]", "batch 0 must be the start batch of segment 0");
            if (batches.Length > 0 && batches[0].DropColumns != null)
                push("L-12", "batch_invalid", "yard.batches[0].dropColumns", "dropColumns is unused on batch 0", Severities.Warn);
            for (int k = 0; k < batches.Length; k++)
            {
                var b = batches[k];
                string path = "yard.batches[" + k + "]";
                if (b.ForSegment > S - 1) push("L-12", "batch_invalid", path, "forSegment " + b.ForSegment + " ≥ segment count " + S);
                if (k >= 1 && b.ForSegment < 1)
                    push("L-12", "batch_invalid", path, "a truck batch (k ≥ 1) is delivered when a segment completes: forSegment ≥ 1");
                if (k == 0) continue;
                for (int i = 0; i < b.Pieces.Length; i++)
                {
                    var p = b.Pieces[i];
                    int w = Shapes.ShapeById(p.Shape).W;
                    if (p.Y != geo.Hy)
                        push("L-12", "batch_invalid", path + ".pieces[" + i + "]", "truck piece y must be written as Hy = " + geo.Hy + " (got " + p.Y + ")");
                    if (p.X > geo.Wy - w)
                        push("L-12", "batch_invalid", path + ".pieces[" + i + "]", "truck piece x " + p.X + " > Wy − w = " + (geo.Wy - w));
                    foreach (int c in b.DropColumns ?? new int[0])
                    {
                        if (c > geo.Wy - w)
                            push("L-12", "batch_invalid", path + ".dropColumns", "drop column " + c + " does not fit " + p.Shape + " (w " + w + ") in a yard of " + geo.Wy + " columns");
                    }
                }
            }
        }

        private sealed class PlacedDebris
        {
            public readonly int I;
            public readonly int Seg;
            public readonly string Color;
            public readonly BoardCell[] Cells;

            public PlacedDebris(int i, int seg, string color, BoardCell[] cells)
            {
                I = i;
                Seg = seg;
                Color = color;
                Cells = cells;
            }
        }

        /// <summary>
        /// L-13 `debris_misplaced` (K-45/7): debris lies in the site columns (x ≥ Wy) inside its own segment's plan rows, no
        /// debris overlap. L-29 `debris_correct_at_start` (S4 Faz 2R): debris starts in a WRONG place — no debris may already be
        /// a correct placement by K-16 (every cell on its plan colour and K-34 support, where only debris that is itself
        /// correct at the start counts as filled).
        /// </summary>
        private static void CheckDebris(LevelData level, SegmentPlan[] plans, BoardGeo geo, bool plansUsable, Push push)
        {
            var taken = new Dictionary<string, int>();
            var placed = new List<PlacedDebris>();
            int last = geo.SiteX + geo.Ws - 1;
            var debris = level.Build.Debris ?? new DebrisData[0];
            for (int i = 0; i < debris.Length; i++)
            {
                var d = debris[i];
                string path = "build.debris[" + i + "]";
                int seg = d.Segment ?? 0;
                if (seg < 0 || seg >= plans.Length)
                {
                    push("L-13", "debris_misplaced", path, "segment " + seg + " does not exist");
                    continue;
                }
                var cells = CellsAt(Shapes.ShapeById(d.Shape), d.X, d.Y);
                bool outside = false;
                foreach (var c in cells) if (c.X < geo.SiteX || c.X > last || c.Y < 0 || c.Y >= geo.Hs) outside = true;
                if (outside)
                {
                    push("L-13", "debris_misplaced", path, "debris must lie in the site (x " + geo.SiteX + "–" + last + ") inside segment " + seg + "'s plan rows 0–" + (geo.Hs - 1));
                    continue;
                }
                bool clash = false;
                foreach (var c in cells)
                {
                    string key = seg + ":" + c.X + "," + c.Y;
                    if (taken.TryGetValue(key, out int other))
                    {
                        clash = true;
                        push("L-13", "debris_misplaced", path, "overlaps build.debris[" + other + "] at (" + c.X + "," + c.Y + ")");
                    }
                    else taken[key] = i;
                }
                if (!clash) placed.Add(new PlacedDebris(i, seg, d.Color, cells));
            }
            if (!plansUsable) return;
            var filled = new HashSet<int>[plans.Length];
            for (int k = 0; k < filled.Length; k++) filled[k] = new HashSet<int>();
            var correct = new HashSet<int>();
            int Idx(int x, int y) => Plan.LocalIndex(x - geo.SiteX, y, geo.Ws);
            for (bool changed = true; changed;)
            {
                changed = false;
                foreach (var p in placed)
                {
                    if (correct.Contains(p.I)) continue;
                    var plan = plans[p.Seg];
                    var done = filled[p.Seg];
                    bool onColor = true;
                    foreach (var c in p.Cells) if (plan.Cells[Idx(c.X, c.Y)].ToString() != p.Color) onColor = false;
                    if (!onColor) continue;
                    bool supported = true;
                    foreach (var c in p.Cells)
                    {
                        int bottom = int.MaxValue;
                        foreach (var o in p.Cells) if (o.X == c.X) bottom = Math.Min(bottom, o.Y);
                        for (int y = 0; y < bottom; y++)
                        {
                            if (!done.Contains(Idx(c.X, y)))
                            {
                                supported = false;
                                break;
                            }
                        }
                        if (!supported) break;
                    }
                    if (!supported) continue;
                    correct.Add(p.I);
                    foreach (var c in p.Cells) done.Add(Idx(c.X, c.Y));
                    changed = true;
                }
            }
            var sorted = new List<int>(correct);
            sorted.Sort();
            foreach (int i in sorted)
            {
                var d = debris[i];
                push("L-29", "debris_correct_at_start", "build.debris[" + i + "]",
                    d.Shape + " " + d.Color + " at (" + d.X + "," + d.Y + ") is already a correct placement (K-16): debris starts in a wrong place (S4)");
            }
        }

        /// <summary>L-14 hidden_item_exposed (+ crate hp), L-23 hidden_item_stacked (K-42, N4, N5).</summary>
        private static void CheckHiddenItems(LevelData level, Push push)
        {
            var covered = new HashSet<string>();
            if (level.Yard.Batches.Length > 0)
            {
                foreach (var p in level.Yard.Batches[0].Pieces)
                    foreach (var c in CellsAt(Shapes.ShapeById(p.Shape), p.X, p.Y)) covered.Add(c.X + "," + c.Y);
            }
            var crates = new HashSet<string>();
            foreach (var o in level.Obstacles) if (o.Type == "crate") crates.Add(o.X + "," + o.Y);
            var items = new Dictionary<string, int>();
            for (int i = 0; i < level.Obstacles.Length; i++)
            {
                var o = level.Obstacles[i];
                string path = "obstacles[" + i + "]";
                if (o.Type == "crate" && !o.Hp.HasValue) push("L-14", "schema_invalid", path, "a crate needs hp 1–3 (OBSTACLES Y1)");
                if (o.Type != "screw" && o.Type != "key") continue;
                string key = o.X + "," + o.Y;
                if (!covered.Contains(key) && !crates.Contains(key))
                    push("L-14", "hidden_item_exposed", path, o.Type + " at (" + key + ") is not under a block or a crate at start");
                if (items.TryGetValue(key, out int prev))
                    push("L-23", "hidden_item_stacked", path, "second hidden item in cell (" + key + ") (obstacles[" + prev + "])");
                else items[key] = i;
            }
        }

        /// <summary>L-15 (K-41).</summary>
        private static void CheckGoals(LevelData level, Push push)
        {
            int builds = 0;
            foreach (var g in level.Goals) if (g.Type == "build") builds++;
            if (builds != 1) push("L-15", "goal_build_missing", "goals", "exactly one build goal required (found " + builds + ")");
            int crate = 0;
            int chain = 0;
            int screw = 0;
            foreach (var o in level.Obstacles)
            {
                if (o.Type == "crate") crate++;
                if (o.Type == "screw") screw++;
            }
            foreach (var b in level.Yard.Batches)
                foreach (var p in b.Pieces)
                    if (HasFlagName(p, PieceFlags.Chained)) chain++;
            int debris = level.Build.Debris?.Length ?? 0;
            for (int i = 0; i < level.Goals.Length; i++)
            {
                var g = level.Goals[i];
                string path = "goals[" + i + "]";
                if (g.Type == "clear")
                {
                    int available = g.Target == "crate" ? crate : g.Target == "chain" ? chain : debris;
                    if ((g.Count ?? 0) > available)
                        push("L-15", "goal_count_too_high", path, "clear " + g.Target + " " + g.Count + " > " + available + " in the level");
                }
                if (g.Type == "collect" && (g.Count ?? 0) > screw)
                    push("L-15", "goal_count_too_high", path, "collect screw " + g.Count + " > " + screw + " in the level");
            }
        }

        /// <summary>L-16 teaches_mismatch, L-22 too_many_new_mechanics (K-45/10, OBSTACLES "Veri imzası" Faz 2R).</summary>
        private static void CheckMechanics(LevelData level, ISet<string> previous, Push push)
        {
            var fresh = Mechanics.NewMechanics(level, previous);
            if (fresh.Length > 1)
                push("L-22", "too_many_new_mechanics", "level", fresh.Length + " new mechanics (" + string.Join(", ", fresh) + "); at most 1");
            if (level.Teaches != null)
            {
                if (fresh.Length == 0)
                    push("L-16", "teaches_mismatch", "teaches", "teaches " + level.Teaches + " but the data has no new mechanic");
                else if (Array.IndexOf(fresh, level.Teaches) < 0)
                    push("L-16", "teaches_mismatch", "teaches", "teaches " + level.Teaches + " but the derived new mechanic is " + string.Join(", ", fresh));
            }
        }

        private static readonly JsRegex TruckRef = new JsRegex("^piece:k(\\d+)_(\\d+)$");
        private static readonly JsRegex StartRef = new JsRegex("^piece:(\\d+)$");
        private static readonly JsRegex BatchArg = new JsRegex("^k(\\d+)_(\\d+)$");
        private static readonly JsRegex Digits = new JsRegex("^\\d+$");

        /// <summary>The block a `piece:&lt;i&gt;` / `piece:k&lt;p&gt;_&lt;i&gt;` id names (batch 0, or truck batch p ≥ 1), or null.</summary>
        private static PieceData? PieceOfRef(LevelData level, string reference)
        {
            var truck = TruckRef.Match(reference);
            if (truck.Success)
            {
                int p = JsRegex.DigitsToInt(truck.Groups[1].Value);
                int i = JsRegex.DigitsToInt(truck.Groups[2].Value);
                if (p < 1 || p >= level.Yard.Batches.Length) return null;
                var pieces = level.Yard.Batches[p].Pieces;
                return i < pieces.Length ? pieces[i] : null;
            }
            var start = StartRef.Match(reference);
            if (!start.Success || level.Yard.Batches.Length == 0) return null;
            int si = JsRegex.DigitsToInt(start.Groups[1].Value);
            var b0 = level.Yard.Batches[0].Pieces;
            return si < b0.Length ? b0[si] : null;
        }

        /// <summary>Why the event of a `done` / `startOn` condition can never happen in this level (null = possible).</summary>
        private static string? ConditionImpossible(LevelData level, BoardGeo geo, TutCondition cond)
        {
            var allPieces = new List<PieceData>();
            var truckPieces = new List<PieceData>();
            for (int k = 0; k < level.Yard.Batches.Length; k++)
            {
                foreach (var p in level.Yard.Batches[k].Pieces)
                {
                    allPieces.Add(p);
                    if (k >= 1) truckPieces.Add(p);
                }
            }
            bool AnyFlag(List<PieceData> list, string flag)
            {
                foreach (var p in list) if (HasFlagName(p, flag)) return true;
                return false;
            }
            bool AnyObstacle(string type)
            {
                foreach (var o in level.Obstacles) if (o.Type == type) return true;
                return false;
            }
            string? PieceRef(string ev, string? reference)
            {
                if (reference == null) return null;
                var piece = PieceOfRef(level, reference);
                if (piece == null) return ev + " piece " + reference + ": no such block";
                if (ev == "placementCorrect" && Mechanics.IsCargo(Shapes.ShapeById(piece.Shape)))
                    return ev + " piece " + reference + " is Ağır Yük: it is never placed (K-44)";
                return null;
            }
            switch (cond.Event)
            {
                case "segmentDone":
                    return level.Build.Segments.Length >= 2 ? null : "segmentDone needs ≥ 2 segments";
                case "deliveryDone":
                    if (level.Yard.Batches.Length < 2) return "deliveryDone needs a truck batch";
                    if (cond.Flag != null && !AnyFlag(truckPieces, cond.Flag)) return "deliveryDone flag " + cond.Flag + " needs a truck block with that flag";
                    return null;
                case "gapPass":
                    return level.Wall.Gaps.Length >= 1 ? null : "gapPass needs a gap";
                case "obstacleHit":
                    if (cond.Type == "chain") return AnyFlag(allPieces, PieceFlags.Chained) ? null : "obstacleHit chain needs a chained block";
                    return AnyObstacle(cond.Type ?? "") ? null : "obstacleHit " + cond.Type + " needs a " + cond.Type;
                case "itemCollected":
                    return AnyObstacle(cond.Type ?? "") ? null : "itemCollected " + cond.Type + " needs a " + cond.Type;
                case "landed":
                    if (cond.Flag != null && !AnyFlag(allPieces, cond.Flag)) return "landed flag " + cond.Flag + " needs a block with that flag";
                    if (cond.Wind == true && level.Wall.Fan == null) return "landed wind needs wall.fan";
                    return null;
                case "steered":
                    return level.Gravity.Build == "low" ? null : "steered needs gravity.build \"low\"";
                case "yardFall":
                    return level.Gravity.Yard ? null : "yardFall needs gravity.yard";
                case "carouselTurn":
                    return level.Build.Mode == "carousel" ? null : "carouselTurn needs build.mode \"carousel\"";
                case "placementCorrect":
                {
                    if (cond.Hidden == true)
                    {
                        bool hidden = false;
                        foreach (var s in level.Build.Segments) foreach (var r in s.Rows) if (r.IndexOf('?') >= 0) hidden = true;
                        if (!hidden) return "placementCorrect hidden needs a `?` cell";
                    }
                    int last = geo.SiteX + geo.Ws - 1;
                    if (cond.At != null && (cond.At[0] < geo.SiteX || cond.At[0] > last || cond.At[1] > geo.H - 1))
                        return "placementCorrect at must be a site anchor (x " + geo.SiteX + "–" + last + ", y 0–" + (geo.H - 1) + ")";
                    return PieceRef(cond.Event, cond.Piece);
                }
                case "yardMove":
                {
                    if (cond.Painted == true)
                    {
                        bool paint = false;
                        foreach (var g in level.Wall.Gaps) if (g.Type == "paint") paint = true;
                        if (!paint) return "yardMove painted needs a paint gate";
                    }
                    if (cond.At != null && (cond.At[0] > geo.Wy - 1 || cond.At[1] > geo.Hy - 1))
                        return "yardMove at must be a yard anchor (x 0–" + (geo.Wy - 1) + ", y 0–" + (geo.Hy - 1) + ")";
                    return PieceRef(cond.Event, cond.Piece);
                }
                default:
                    return null;
            }
        }

        /// <summary>Batches k ≥ 1 in delivery order (forSegment ascending, then array order).</summary>
        private static List<int> DeliveryOrder(LevelData level)
        {
            var o = new List<int>();
            for (int k = 1; k < level.Yard.Batches.Length; k++) o.Add(k);
            // stable insertion sort by (forSegment, k)
            for (int i = 1; i < o.Count; i++)
            {
                int v = o[i];
                int j = i - 1;
                while (j >= 0 && Less(level, v, o[j]))
                {
                    o[j + 1] = o[j];
                    j--;
                }
                o[j + 1] = v;
            }
            return o;
        }

        private static bool Less(LevelData level, int a, int b)
        {
            int sa = level.Yard.Batches[a].ForSegment;
            int sb = level.Yard.Batches[b].ForSegment;
            return sa != sb ? sa < sb : a < b;
        }

        /// <summary>`count` of a condition of `event`, else 0.</summary>
        private static int CountOf(TutCondition? cond, string ev) => cond != null && cond.Event == ev ? cond.Count ?? 1 : 0;

        /// <summary>
        /// L-17 (GDD §14.1 with the Faz 2R vocabulary, UX §13.1). Faz 2R: the required-step branches (GDD 14.1/4a) are gone;
        /// a truck block (`piece:k&lt;p&gt;_&lt;i&gt;`) may be highlighted once its batch is on the board when the step starts: the step
        /// starts on its delivery (`startOn: deliveryDone`, GDD 14.1/5), or at least `forSegment` segments are done by then
        /// (`segmentDone` in its `startOn` or in an earlier step's `done`; the delivery runs in the same move, K-35 steps 8–9 —
        /// LEVELS Bölüm 5 adım 2).
        /// </summary>
        private static void CheckTutorial(LevelData level, BoardGeo geo, LogicContext ctx, Push push)
        {
            var steps = level.Tutorial ?? new TutorialStepData[0];
            var batches = level.Yard.Batches;
            int segmentsBefore = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                var st = steps[i];
                string path = "tutorial[" + i + "]";
                var start = st.StartOn;
                var done = st.Done;
                int segmentsAtStart = segmentsBefore + CountOf(start, "segmentDone");
                segmentsBefore = segmentsAtStart + CountOf(done, "segmentDone");
                if (st.Step != i + 1)
                    push("L-17", "tut_done_invalid", path + ".step", "steps must be numbered 1, 2, … in order (expected " + (i + 1) + ")");
                if (ctx.I18nKeys != null)
                {
                    var missing = new List<string>();
                    if (!ctx.I18nKeys.Tr.Contains(st.TextKey)) missing.Add("tr");
                    if (!ctx.I18nKeys.En.Contains(st.TextKey)) missing.Add("en");
                    if (missing.Count > 0)
                        push("L-17", "tut_key_missing", path + ".textKey", st.TextKey + " missing in " + string.Join(", ", missing) + ".json");
                }
                for (int hi = 0; hi < st.Highlight.Length; hi++)
                {
                    string h = st.Highlight[hi];
                    string hpath = path + ".highlight[" + hi + "]";
                    void Bad(string msg) => push("L-17", "tut_highlight_invalid", hpath, h + ": " + msg);
                    int colon = h.IndexOf(':');
                    string kind = colon < 0 ? h : h.Substring(0, colon);
                    string arg = colon < 0 ? "" : h.Substring(colon + 1);
                    if (kind == "piece")
                    {
                        var batchMatch = BatchArg.Match(arg);
                        if (!batchMatch.Success)
                        {
                            if (NumberOf(arg) >= (batches.Length > 0 ? batches[0].Pieces.Length : 0)) Bad("no such batch-0 block");
                            continue;
                        }
                        int p = JsRegex.DigitsToInt(batchMatch.Groups[1].Value);
                        int pi = JsRegex.DigitsToInt(batchMatch.Groups[2].Value);
                        PieceData? piece = p >= 0 && p < batches.Length && pi < batches[p].Pieces.Length ? batches[p].Pieces[pi] : null;
                        if (p < 1 || piece == null)
                        {
                            Bad("no such truck block");
                            continue;
                        }
                        if (start != null && start.Event == "deliveryDone")
                        {
                            bool Matches(PieceData q) => start.Flag == null || HasFlagName(q, start.Flag);
                            if (!Matches(piece))
                            {
                                Bad("the block does not satisfy the startOn filter flag " + start.Flag);
                                continue;
                            }
                            if (level.Build.Mode == "segments")
                            {
                                var order = new List<int>();
                                foreach (int k in DeliveryOrder(level))
                                {
                                    bool any = false;
                                    foreach (var q in batches[k].Pieces) if (Matches(q)) any = true;
                                    if (any) order.Add(k);
                                }
                                int at = (start.Count ?? 1) - 1;
                                int? expected = at >= 0 && at < order.Count ? order[at] : (int?)null;
                                if (expected != p) Bad("the startOn delivery is batch " + (expected.HasValue ? expected.Value.ToString() : "none") + ", not batch " + p);
                            }
                            continue;
                        }
                        int forSegment = batches[p].ForSegment;
                        if (forSegment > segmentsAtStart)
                            Bad("batch " + p + " arrives when segment " + forSegment + " starts: the step needs startOn deliveryDone, or " + forSegment + " segmentDone before it starts (GDD 14.1/5)");
                    }
                    else if (kind == "debris")
                    {
                        if (NumberOf(arg) >= (level.Build.Debris?.Length ?? 0)) Bad("no such debris");
                    }
                    else if (kind == "cell")
                    {
                        var parts = arg.Split(',');
                        double x = parts.Length > 0 ? NumberOf(parts[0]) : -1;
                        double y = parts.Length > 1 ? NumberOf(parts[1]) : -1;
                        if (x > geo.Cols - 1 || y > geo.H - 1) Bad("cell is not on the board (x 0–" + (geo.Cols - 1) + ", y 0–" + (geo.H - 1) + ")");
                    }
                    else if (kind == "gap")
                    {
                        if (NumberOf(arg) >= level.Wall.Gaps.Length) Bad("no such gap");
                    }
                    else if (kind == "obstacle")
                    {
                        if (NumberOf(arg) >= level.Obstacles.Length) Bad("no such obstacle");
                    }
                    else if (kind == "fan")
                    {
                        if (level.Wall.Fan == null) Bad("level has no wind fan");
                    }
                    else if (kind == "truck")
                    {
                        if (batches.Length < 2) Bad("level has no truck batch");
                    }
                    else if ((kind == "booster" || kind == "pre") && ctx.BoosterUnlock != null)
                    {
                        string key = HighlightBoosterKey.TryGetValue(arg, out var mapped) ? mapped : arg;
                        if (!ctx.BoosterUnlock.TryGetValue(key, out int unlock)) Bad("economy.json has no booster " + key);
                        else if (unlock > level.Id) Bad(key + " unlocks at level " + unlock);
                    }
                }
                CheckGloveStart(level, st, path, push);
                var conds = new[] { new KeyValuePair<string, TutCondition?>("done", done), new KeyValuePair<string, TutCondition?>("startOn", st.StartOn) };
                foreach (var fc in conds)
                {
                    var cond = fc.Value;
                    if (cond == null) continue;
                    string? why = ConditionImpossible(level, geo, cond);
                    if (why != null) push("L-17", "tut_done_invalid", path + "." + fc.Key, why);
                    // LEVELS §5 / GDD §14.1/3 (Faz 2 tur 3, product-lead PL-F2T3-3): a player who releases without resting never
                    // sends `holdOverBuild`, so level data ends / starts no step on it (the `hold` glove is only a demonstration)
                    if (cond.Event == "holdOverBuild")
                        push("L-17", "tut_hold_done", path + "." + fc.Key, "holdOverBuild depends on the drag speed: end the step on a move-end event (LEVELS 5)", Severities.Warn);
                }
            }
        }

        /// <summary>TS `Number(arg)`: NaN (never ≥ anything) for a non-numeric string, 0 for the empty string.</summary>
        private static double NumberOf(string arg)
        {
            if (arg.Length == 0) return 0;
            return double.TryParse(arg.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
        }

        /// <summary>
        /// L-17 glove start (LEVELS §5 "Eldiven vurgulu bloktan başlar", GDD §14 "`hand.path` anlamı" (1)): a `drag` / `hold`
        /// glove's `path[0]` is a cell of a highlighted `piece:&lt;i&gt;` (batch 0) or `debris:&lt;i&gt;` (segment 0) block at its JSON
        /// start — the finger presses the block it moves, not its anchor nor a neighbour. A step whose blocks are all truck
        /// blocks or debris of a later segment is not checked here. `tap` is not checked. The full `hand.path` check (start
        /// state of the step, reach set `R`, release row, shortest-solution move) is L-35 `tut_hand_invalid` at `levels:solve`.
        /// </summary>
        private static void CheckGloveStart(LevelData level, TutorialStepData st, string path, Push push)
        {
            var hand = st.Hand;
            if (hand == null || hand.Kind == "tap" || hand.Path == null || hand.Path.Length == 0) return;
            var first = hand.Path[0];
            var starts = new List<BoardCell>();
            bool checkable = false;
            foreach (var h in st.Highlight)
            {
                int colon = h.IndexOf(':');
                string kind = colon < 0 ? h : h.Substring(0, colon);
                string arg = colon < 0 ? "" : h.Substring(colon + 1);
                string? shape = null;
                int x = 0;
                int y = 0;
                if (kind == "piece" && Digits.IsMatch(arg))
                {
                    int i = JsRegex.DigitsToInt(arg);
                    if (level.Yard.Batches.Length > 0 && i < level.Yard.Batches[0].Pieces.Length)
                    {
                        var p = level.Yard.Batches[0].Pieces[i];
                        shape = p.Shape;
                        x = p.X;
                        y = p.Y;
                    }
                }
                else if (kind == "debris")
                {
                    double di = NumberOf(arg);
                    var debris = level.Build.Debris;
                    if (debris != null && !double.IsNaN(di) && di >= 0 && di < debris.Length && di == Math.Floor(di))
                    {
                        var d = debris[(int)di];
                        if ((d.Segment ?? 0) == 0)
                        {
                            shape = d.Shape;
                            x = d.X;
                            y = d.Y;
                        }
                    }
                }
                if (shape == null) continue;
                checkable = true;
                foreach (var c in Shapes.ShapeById(shape).Cells) starts.Add(new BoardCell(x + c.X, y + c.Y));
            }
            if (!checkable) return;
            int fx = first[0];
            int fy = first[1];
            bool onBlock = false;
            foreach (var c in starts) if (c.X == fx && c.Y == fy) onBlock = true;
            if (!onBlock)
            {
                var names = new string[starts.Count];
                for (int i = 0; i < names.Length; i++) names[i] = "(" + starts[i].X + "," + starts[i].Y + ")";
                push("L-17", "tut_highlight_invalid", path + ".hand.path[0]",
                    "a " + hand.Kind + " glove starts on a highlighted block's cell (LEVELS 5): (" + fx + "," + fy + ") is none of " + string.Join(" ", names));
            }
        }

        /// <summary>L-31 (K-53/1–2, K-45/10): at most 2 steps (`tut_too_many_steps`); `mode` absent or `'soft'` (`tut_blocking`).</summary>
        private static void CheckTutorialLoad(LevelData level, Push push)
        {
            var steps = level.Tutorial ?? new TutorialStepData[0];
            if (steps.Length > TutorialMaxSteps)
                push("L-31", "tut_too_many_steps", "tutorial", steps.Length + " steps; a level has at most " + TutorialMaxSteps + " (K-53/1)");
            for (int i = 0; i < steps.Length; i++)
            {
                var st = steps[i];
                if (st.Mode != null && st.Mode != "soft")
                    push("L-31", "tut_blocking", "tutorial[" + i + "].mode", "mode \"" + st.Mode + "\": every step is soft and never locks input (K-53/2)");
            }
        }

        /// <summary>L-18 (LEVELS §0 sawtooth).</summary>
        private static void CheckSawtooth(LevelData level, Push push)
        {
            string? expected = HardLevels.Contains(level.Id) ? "hard" : SuperhardLevels.Contains(level.Id) ? "superhard" : null;
            if (expected != null && level.Difficulty != expected)
                push("L-18", "difficulty_sawtooth", "difficulty", "level " + level.Id + " should be " + expected, Severities.Warn);
            if (expected == null && (level.Difficulty == "hard" || level.Difficulty == "superhard"))
                push("L-18", "difficulty_sawtooth", "difficulty", "level " + level.Id + " should be easy or normal", Severities.Warn);
        }

        /// <summary>
        /// L-21 (K-21, OBSTACLES flag table): glass + balloon is forbidden; Ağır Yük cannot be glass, balloon or mortar (it
        /// never enters the site). Debris carries no flags (schema); Ağır Yük debris is L-04.
        /// </summary>
        private static void CheckFlagCombos(LevelData level, Push push)
        {
            foreach (var b in AllPlaced(level))
            {
                if (b.Piece == null) continue;
                var flags = FlagsOf(b.Piece);
                if (Array.IndexOf(flags, PieceFlags.Glass) >= 0 && Array.IndexOf(flags, PieceFlags.Balloon) >= 0)
                    push("L-21", "flag_combo_forbidden", b.Path, "glass + balloon is forbidden");
                if (b.Cargo)
                {
                    var bad = new List<string>();
                    foreach (var f in flags) if (f == PieceFlags.Glass || f == PieceFlags.Balloon || f == PieceFlags.Mortar) bad.Add(f);
                    if (bad.Count > 0) push("L-21", "flag_combo_forbidden", b.Path, "Ağır Yük cannot be " + string.Join(", ", bad));
                }
            }
        }

        /// <summary>L-24 (K-24).</summary>
        private static void CheckElevatorRange(LevelData level, Push push)
        {
            var el = level.Build.Elevator;
            if (el == null) return;
            int a = el.Range[0];
            int b = el.Range[1];
            if (!(a < b)) push("L-24", "elevator_range", "build.elevator", "range [" + a + "," + b + "] needs a < b");
            if (el.Start < a || el.Start > b) push("L-24", "elevator_range", "build.elevator", "start " + el.Start + " outside [" + a + "," + b + "]");
        }

        /// <summary>L-25 (K-23).</summary>
        private static void CheckCarousel(LevelData level, Push push)
        {
            var b = level.Build;
            if (b.Mode == "carousel" && !b.CarouselEvery.HasValue)
                push("L-25", "carousel_every_missing", "build.carouselEvery", "carousel mode needs carouselEvery");
            if (b.Mode == "segments" && b.CarouselEvery.HasValue)
                push("L-25", "carousel_every_missing", "build.carouselEvery", "carouselEvery is unused in segments mode", Severities.Warn);
        }

        /// <summary>L-26 (Y4): `wet` ⇔ `wetMoves`.</summary>
        private static void CheckWet(LevelData level, Push push)
        {
            for (int bi = 0; bi < level.Yard.Batches.Length; bi++)
            {
                var b = level.Yard.Batches[bi];
                for (int i = 0; i < b.Pieces.Length; i++)
                {
                    var p = b.Pieces[i];
                    bool wet = HasFlagName(p, PieceFlags.Wet);
                    if (wet != p.WetMoves.HasValue)
                        push("L-26", "wet_moves_missing", "yard.batches[" + bi + "].pieces[" + i + "]", wet ? "wet block needs wetMoves" : "wetMoves without the wet flag");
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Deprecated (Faz 2 K-27 supply; no Faz 2R check uses it)
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>Search nodes of the whole-block assignment before `MaterialShortfall` settles for what it has proven.</summary>
        private const int MaterialSearchBudget = 50_000;

        private sealed class FlexItem
        {
            public readonly int Cells;
            public readonly int[] Opts;

            public FlexItem(int cells, int[] opts)
            {
                Cells = cells;
                Opts = opts;
            }
        }

        /// <summary>
        /// Deprecated Faz 2 K-27 / L-10 (`material_short`); Faz 2R uses exact cover (L-30, L-10 `cover_prefix_short`) and the
        /// W6 joker of D3a (L-11). Kept for the Faz 2 review tests.
        ///
        /// Returns the colours whose demand cannot be met (empty = enough material), in `ColorCodes.All` order. A block is one
        /// colour as a whole; blocks with a paint-gate choice are assigned by a depth-first branch and bound (largest blocks
        /// first, own colour before gate colours) that minimises the uncovered plan cells. Pathological inputs (more than
        /// `MaterialSearchBudget` nodes) keep the best assignment found only when a shortage is already proven by the
        /// per-colour or total supply bound; otherwise nothing is reported.
        /// </summary>
        public static string[] MaterialShortfall(IReadOnlyDictionary<string, int> demand, IReadOnlyList<SupplyBlock> blocks)
        {
            var restKeys = new List<string>();
            var rest = new Dictionary<string, int>();
            foreach (var c in ColorCodes.All)
            {
                int d = demand.TryGetValue(c, out int v) ? v : 0;
                if (d > 0)
                {
                    restKeys.Add(c);
                    rest[c] = d;
                }
            }
            var flexible = new List<KeyValuePair<int, string[]>>();
            foreach (var b in blocks)
            {
                var colors = new List<string> { b.Color };
                foreach (var a in b.Alts) if (!colors.Contains(a)) colors.Add(a);
                bool hasD = rest.TryGetValue(b.Color, out int d);
                if (colors.Count > 1) flexible.Add(new KeyValuePair<int, string[]>(b.Cells, colors.ToArray()));
                else if (hasD) rest[b.Color] = d - b.Cells;
            }
            var open = new List<string>();
            foreach (var c in restKeys) if (rest[c] > 0) open.Add(c);
            if (open.Count == 0) return new string[0];
            int m = open.Count;
            List<FlexItem> items = new List<FlexItem>();
            foreach (var b in flexible)
            {
                var opts = new List<int>();
                foreach (var c in b.Value)
                {
                    int oi = open.IndexOf(c);
                    if (oi >= 0) opts.Add(oi);
                }
                if (opts.Count > 0) items.Add(new FlexItem(b.Key, opts.ToArray()));
            }
            // stable sort, cells descending
            for (int i = 1; i < items.Count; i++)
            {
                var v = items[i];
                int j = i - 1;
                while (j >= 0 && items[j].Cells < v.Cells)
                {
                    items[j + 1] = items[j];
                    j--;
                }
                items[j + 1] = v;
            }
            int n = items.Count;
            // Suffix supplies for the lower bound: per open colour and in total, from item i on.
            int[][] suffix = new int[n + 1][];
            for (int i = 0; i <= n; i++) suffix[i] = new int[m + 1];
            for (int i = n - 1; i >= 0; i--)
            {
                var it = items[i];
                var row = suffix[i];
                var next = suffix[i + 1];
                for (int k = 0; k <= m; k++) row[k] = next[k];
                foreach (int k in it.Opts) row[k] += it.Cells;
                row[m] += it.Cells;
            }
            int LowerBound(int i, int[] d)
            {
                var sfx = suffix[i];
                int perColor = 0;
                int total = 0;
                for (int k = 0; k < d.Length; k++)
                {
                    perColor += Math.Max(0, d[k] - sfx[k]);
                    total += d[k];
                }
                return Math.Max(perColor, total - sfx[m]);
            }
            var start = new int[m];
            for (int k = 0; k < m; k++) start[k] = rest[open[k]];
            bool relaxedShort = LowerBound(0, start) > 0;
            int best = 0;
            foreach (int v in start) best += v;
            best += 1;
            int[] bestRest = start;
            HashSet<string> seen = new HashSet<string>();
            int nodes = 0;
            // Exploring a state again cannot beat `best`: `best` only falls, so the first visit already found anything better.
            void Search(int i, int[] d)
            {
                int uncovered = 0;
                foreach (int v in d) uncovered += v;
                if (uncovered < best && (uncovered == 0 || i == n))
                {
                    best = uncovered;
                    bestRest = d;
                }
                if (best == 0 || i == n || LowerBound(i, d) >= best || nodes >= MaterialSearchBudget) return;
                string key = i + "|" + string.Join(",", d);
                if (!seen.Add(key)) return;
                nodes++;
                var it = items[i];
                foreach (int k in it.Opts)
                {
                    var nd = (int[])d.Clone();
                    nd[k] = Math.Max(0, d[k] - it.Cells);
                    Search(i + 1, nd);
                }
            }
            Search(0, start);
            if (nodes >= MaterialSearchBudget && best > 0 && !relaxedShort) return new string[0];
            var o = new List<string>();
            for (int k = 0; k < m; k++) if (bestRest[k] > 0) o.Add(open[k]);
            return o.ToArray();
        }
    }
}
