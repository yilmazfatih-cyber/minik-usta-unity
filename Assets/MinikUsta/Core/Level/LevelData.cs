#nullable enable
// Port of the data model of src/core/level/schema.ts (web repo, source of truth): the parsed `LevelData` (zod output with
// defaults applied: shutter `phase` 0, slider `dir` 1, cargo colour placeholder) as plain C# classes. The schema checks
// live in LevelJson.cs. `ToJson()` reproduces the zod output object so `levelHash` (K-43 item 4) is bit for bit the TS value.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MinikUsta.Core.Json;

namespace MinikUsta.Core.Level
{
    /// <summary>
    /// OBSTACLES "Veri imzası" (Faz 2R): 27 mechanic ids in table order (S7-R and S7-M separate; S2 Plan Boşluğu is out of the
    /// MVP and not in the table, CL-2R-12; S9 Geniş Şantiye is new). Detection: Mechanics.cs.
    /// </summary>
    public static class MechanicIds
    {
        public static readonly string[] All =
        {
            "W1", "W2", "W3", "W4", "W5", "W6", "W7", "W8",
            "Y1", "Y2", "Y3", "Y4", "Y5", "Y6", "Y7", "Y8",
            "S1", "S3", "S4", "S5", "S6", "S7-R", "S7-M", "S8",
            "G-H", "G-L", "S9",
        };

        /// <summary>Faz 2R transition (TECH §2R.12): S2 left the signature table; the id stays in the `teaches` enum.</summary>
        public static readonly string[] Legacy = { "S2" };
    }

    /// <summary>GDD §14.1/3 closed `done` vocabulary (16 events).</summary>
    public static class TutEvents
    {
        public static readonly string[] All =
        {
            "overWall", "gapPass", "holdOverBuild", "turnEnd", "placementCorrect", "yardMove", "landed", "steered",
            "obstacleHit", "itemCollected", "yardFall", "segmentDone", "deliveryDone", "carouselTurn", "tap", "boosterUsed",
        };
    }

    /// <summary>Schema regular expressions (schema.ts).</summary>
    public static class LevelSchemaPatterns
    {
        /// <summary>UX_FLOWS §13.1 highlight vocabulary, one alternative per entry (TECH §8.2).</summary>
        public static readonly string[] HighlightPatterns =
        {
            "piece:(\\d{1,2}|k[1-9]_\\d{1,2})",
            "cell:[0-7],[0-9]",
            "gap:[0-2]",
            "obstacle:\\d{1,2}",
            "debris:\\d{1,2}",
            "booster:(hammer|crane|brush|undo)",
            "pre:(thermos|trowel|shutter)",
            "wall",
            "crane",
            "build",
            "front",
            "panorama",
            "goals",
            // Faz 2R (UX §13.1, CL-2R-21): the blocks-left chip alone; `goals` is the whole panel.
            "blocks",
            "moves",
            "truck",
            "streak",
            "fan",
        };

        public static readonly Regex Highlight = new Regex("^(" + string.Join("|", HighlightPatterns) + ")$", RegexOptions.CultureInvariant);
        /// <summary>`piece` filter of `yardMove` / `placementCorrect` (GDD §14.1/3, DL-2R-02): a block highlight id.</summary>
        public static readonly Regex PieceRef = new Regex("^piece:(\\d{1,2}|k[1-9]_\\d{1,2})$", RegexOptions.CultureInvariant);
        /// <summary>`textKey` (GDD §14.1/1, Faz 2R): `tut.l&lt;n&gt;.&lt;topic&gt;`, `tut.m.&lt;topic&gt;` or `tut.ctx.&lt;topic&gt;`.</summary>
        public static readonly Regex TutTextKey = new Regex("^tut\\.(l\\d{1,2}|m|ctx)\\.[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);
        /// <summary>Plan row characters (K-15, K-32); `.` passes the schema so L-05 can name it (`plan_has_window`).</summary>
        public static readonly Regex PlanRow = new Regex("^[WYGROCBP.?]{1,4}$", RegexOptions.CultureInvariant);
        /// <summary>GDD K-44 (Faz 2R): Ağır Yük shapes. Their `color` is optional and ignored (no colour, not material).</summary>
        public static readonly Regex CargoShape = new Regex("^(I5|Q9)_", RegexOptions.CultureInvariant);
        /// <summary>`TÜR_AÇI` shape id format (`ShapeIdSchema`).</summary>
        public static readonly Regex ShapeId = new Regex("^(B1|D2|I3|I4|O4|C3|L4|J4|T4|S4|Z4|I5|Q9)_(0|90|180|270)$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Colour of a parsed Ağır Yük piece written without `color` (K-44: "verideki `color` yok sayılır"). Only a placeholder
        /// that keeps `PieceData.Color` a colour code: no rule reads the colour of a cargo piece.
        /// </summary>
        public const string CargoPlaceholderColor = "W";
    }

    public sealed class I18nText
    {
        public readonly string Tr;
        public readonly string En;

        public I18nText(string tr, string en)
        {
            Tr = tr;
            En = en;
        }

        public JsonValue ToJson() => JsonValue.Object().Set("tr", JsonValue.Of(Tr)).Set("en", JsonValue.Of(En));
    }

    /// <summary>`{type:'build'} | {type:'clear', target, count} | {type:'collect', item:'screw', count}`.</summary>
    public sealed class GoalData
    {
        public readonly string Type;
        /// <summary>clear: crate | chain | debris.</summary>
        public readonly string? Target;
        public readonly int? Count;
        /// <summary>collect: "screw".</summary>
        public readonly string? Item;

        public GoalData(string type, string? target, int? count, string? item)
        {
            Type = type;
            Target = target;
            Count = count;
            Item = item;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("type", JsonValue.Of(Type));
            if (Target != null) o.Set("target", JsonValue.Of(Target));
            if (Item != null) o.Set("item", JsonValue.Of(Item));
            if (Count.HasValue) o.Set("count", JsonValue.Of(Count.Value));
            return o;
        }
    }

    public sealed class GravityData
    {
        /// <summary>low | normal | high.</summary>
        public readonly string Build;
        public readonly bool Yard;

        public GravityData(string build, bool yard)
        {
            Build = build;
            Yard = yard;
        }

        public JsonValue ToJson() => JsonValue.Object().Set("build", JsonValue.Of(Build)).Set("yard", JsonValue.Of(Yard));
    }

    public sealed class FanData
    {
        /// <summary>left | right.</summary>
        public readonly string Dir;

        public FanData(string dir)
        {
            Dir = dir;
        }

        public JsonValue ToJson() => JsonValue.Object().Set("dir", JsonValue.Of(Dir));
    }

    /// <summary>
    /// A wall gap (discriminated by `Type`): static | shutter (period, phase default 0) | slider (range, dir default 1) |
    /// paint (color) | locked (keyId). Fields of other types hold their neutral value and are not written back.
    /// </summary>
    public sealed class GapData
    {
        public readonly string Type;
        public readonly int Y;
        public readonly int Size;
        public readonly int Period;
        public readonly int Phase;
        public readonly int[]? Range;
        public readonly int Dir;
        public readonly string? Color;
        public readonly string? KeyId;

        public GapData(string type, int y, int size, int period, int phase, int[]? range, int dir, string? color, string? keyId)
        {
            Type = type;
            Y = y;
            Size = size;
            Period = period;
            Phase = phase;
            Range = range;
            Dir = dir;
            Color = color;
            KeyId = keyId;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("type", JsonValue.Of(Type)).Set("y", JsonValue.Of(Y)).Set("size", JsonValue.Of(Size));
            switch (Type)
            {
                case "shutter":
                    o.Set("period", JsonValue.Of(Period)).Set("phase", JsonValue.Of(Phase));
                    break;
                case "slider":
                    o.Set("range", JsonValue.Array().Add(JsonValue.Of(Range![0])).Add(JsonValue.Of(Range[1]))).Set("dir", JsonValue.Of(Dir));
                    break;
                case "paint":
                    o.Set("color", JsonValue.Of(Color!));
                    break;
                case "locked":
                    o.Set("keyId", JsonValue.Of(KeyId!));
                    break;
            }
            return o;
        }
    }

    public sealed class WallData
    {
        public readonly int Height;
        public readonly GapData[] Gaps;
        public readonly FanData? Fan;

        public WallData(int height, GapData[] gaps, FanData? fan)
        {
            Height = height;
            Gaps = gaps;
            Fan = fan;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("height", JsonValue.Of(Height));
            var gaps = JsonValue.Array();
            foreach (var g in Gaps) gaps.Add(g.ToJson());
            o.Set("gaps", gaps);
            if (Fan != null) o.Set("fan", Fan.ToJson());
            return o;
        }
    }

    /// <summary>K-49 site size `Ws × Hs`.</summary>
    public sealed class SizeData
    {
        public readonly int Cols;
        public readonly int Rows;

        public SizeData(int cols, int rows)
        {
            Cols = cols;
            Rows = rows;
        }

        public JsonValue ToJson() => JsonValue.Object().Set("cols", JsonValue.Of(Cols)).Set("rows", JsonValue.Of(Rows));
    }

    /// <summary>`{kind:'repeat', period} | {kind:'mirrorOf', segment}`.</summary>
    public sealed class HiddenRule
    {
        public readonly string Kind;
        public readonly int Period;
        public readonly int Segment;

        public HiddenRule(string kind, int period, int segment)
        {
            Kind = kind;
            Period = period;
            Segment = segment;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("kind", JsonValue.Of(Kind));
            if (Kind == "repeat") o.Set("period", JsonValue.Of(Period));
            else o.Set("segment", JsonValue.Of(Segment));
            return o;
        }
    }

    public sealed class SegmentData
    {
        public readonly I18nText Name;
        /// <summary>Top → bottom; exactly `site.rows` rows of exactly `site.cols` characters (L-05 `plan_size`, K-15).</summary>
        public readonly string[] Rows;
        public readonly HiddenRule? Hidden;

        public SegmentData(I18nText name, string[] rows, HiddenRule? hidden)
        {
            Name = name;
            Rows = rows;
            Hidden = hidden;
        }

        public JsonValue ToJson()
        {
            var rows = JsonValue.Array();
            foreach (var r in Rows) rows.Add(JsonValue.Of(r));
            var o = JsonValue.Object().Set("name", Name.ToJson()).Set("rows", rows);
            if (Hidden != null) o.Set("hidden", Hidden.ToJson());
            return o;
        }
    }

    /// <summary>K-24: 0 ≤ a &lt; b ≤ 3, a ≤ start ≤ b (L-24); start and dir required (GDD §14).</summary>
    public sealed class ElevatorData
    {
        public readonly int[] Range;
        public readonly int Start;
        public readonly int Dir;

        public ElevatorData(int[] range, int start, int dir)
        {
            Range = range;
            Start = start;
            Dir = dir;
        }

        public JsonValue ToJson()
        {
            return JsonValue.Object()
                .Set("range", JsonValue.Array().Add(JsonValue.Of(Range[0])).Add(JsonValue.Of(Range[1])))
                .Set("start", JsonValue.Of(Start))
                .Set("dir", JsonValue.Of(Dir));
        }
    }

    /// <summary>Debris (OBSTACLES S4, Faz 2R): `x` is a board column, `y` a plan row; `segment` omitted = 0.</summary>
    public sealed class DebrisData
    {
        public readonly string Shape;
        public readonly string Color;
        public readonly int X;
        public readonly int Y;
        public readonly int? Segment;

        public DebrisData(string shape, string color, int x, int y, int? segment)
        {
            Shape = shape;
            Color = color;
            X = x;
            Y = y;
            Segment = segment;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("shape", JsonValue.Of(Shape)).Set("color", JsonValue.Of(Color)).Set("x", JsonValue.Of(X)).Set("y", JsonValue.Of(Y));
            if (Segment.HasValue) o.Set("segment", JsonValue.Of(Segment.Value));
            return o;
        }
    }

    public sealed class BuildData
    {
        /// <summary>segments | carousel (BRIEF's 'elevator' mode moved to its own field, P-6, S-16).</summary>
        public readonly string Mode;
        /// <summary>K-23; mode 'carousel' ⇔ present (L-25).</summary>
        public readonly int? CarouselEvery;
        public readonly ElevatorData? Elevator;
        /// <summary>K-22: 1 ≤ S ≤ 5.</summary>
        public readonly SegmentData[] Segments;
        public readonly DebrisData[]? Debris;

        public BuildData(string mode, int? carouselEvery, ElevatorData? elevator, SegmentData[] segments, DebrisData[]? debris)
        {
            Mode = mode;
            CarouselEvery = carouselEvery;
            Elevator = elevator;
            Segments = segments;
            Debris = debris;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("mode", JsonValue.Of(Mode));
            if (CarouselEvery.HasValue) o.Set("carouselEvery", JsonValue.Of(CarouselEvery.Value));
            if (Elevator != null) o.Set("elevator", Elevator.ToJson());
            var segs = JsonValue.Array();
            foreach (var s in Segments) segs.Add(s.ToJson());
            o.Set("segments", segs);
            if (Debris != null)
            {
                var d = JsonValue.Array();
                foreach (var x in Debris) d.Add(x.ToJson());
                o.Set("debris", d);
            }
            return o;
        }
    }

    /// <summary>A yard / truck block. `Color` is the placeholder "W" for an Ağır Yük written without a colour (K-44).</summary>
    public sealed class PieceData
    {
        public readonly string Shape;
        public readonly string Color;
        public readonly int X;
        /// <summary>Batch k ≥ 1: written as Hy (`yard.rows`, default 8) and ignored (K-25).</summary>
        public readonly int Y;
        public readonly string[]? Flags;
        /// <summary>OBSTACLES Y4; `wet` ⇔ `wetMoves` (L-26).</summary>
        public readonly int? WetMoves;

        public PieceData(string shape, string color, int x, int y, string[]? flags, int? wetMoves)
        {
            Shape = shape;
            Color = color;
            X = x;
            Y = y;
            Flags = flags;
            WetMoves = wetMoves;
        }

        public bool HasFlag(string flag)
        {
            if (Flags == null) return false;
            foreach (var f in Flags) if (f == flag) return true;
            return false;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("shape", JsonValue.Of(Shape)).Set("color", JsonValue.Of(Color)).Set("x", JsonValue.Of(X)).Set("y", JsonValue.Of(Y));
            if (Flags != null)
            {
                var f = JsonValue.Array();
                foreach (var x in Flags) f.Add(JsonValue.Of(x));
                o.Set("flags", f);
            }
            if (WetMoves.HasValue) o.Set("wetMoves", JsonValue.Of(WetMoves.Value));
            return o;
        }
    }

    public sealed class BatchData
    {
        public readonly int ForSegment;
        public readonly int[]? DropColumns;
        public readonly PieceData[] Pieces;

        public BatchData(int forSegment, int[]? dropColumns, PieceData[] pieces)
        {
            ForSegment = forSegment;
            DropColumns = dropColumns;
            Pieces = pieces;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("forSegment", JsonValue.Of(ForSegment));
            if (DropColumns != null)
            {
                var d = JsonValue.Array();
                foreach (var c in DropColumns) d.Add(JsonValue.Of(c));
                o.Set("dropColumns", d);
            }
            var p = JsonValue.Array();
            foreach (var x in Pieces) p.Add(x.ToJson());
            o.Set("pieces", p);
            return o;
        }
    }

    public sealed class YardData
    {
        /// <summary>K-49 yard size `Wy × Hy` (optional; default 6 × 8).</summary>
        public readonly int? Cols;
        public readonly int? Rows;
        public readonly BatchData[] Batches;

        public YardData(int? cols, int? rows, BatchData[] batches)
        {
            Cols = cols;
            Rows = rows;
            Batches = batches;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object();
            if (Cols.HasValue) o.Set("cols", JsonValue.Of(Cols.Value));
            if (Rows.HasValue) o.Set("rows", JsonValue.Of(Rows.Value));
            var b = JsonValue.Array();
            foreach (var x in Batches) b.Add(x.ToJson());
            o.Set("batches", b);
            return o;
        }
    }

    public sealed class ObstacleData
    {
        /// <summary>crate | cement_bag | screw | key.</summary>
        public readonly string Type;
        public readonly int X;
        public readonly int Y;
        public readonly int? Hp;
        public readonly string? Id;

        public ObstacleData(string type, int x, int y, int? hp, string? id)
        {
            Type = type;
            X = x;
            Y = y;
            Hp = hp;
            Id = id;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("type", JsonValue.Of(Type)).Set("x", JsonValue.Of(X)).Set("y", JsonValue.Of(Y));
            if (Hp.HasValue) o.Set("hp", JsonValue.Of(Hp.Value));
            if (Id != null) o.Set("id", JsonValue.Of(Id));
            return o;
        }
    }

    public sealed class HandData
    {
        /// <summary>tap | drag | hold.</summary>
        public readonly string Kind;
        /// <summary>Held cells in board coordinates (GDD §14 "`hand.path` anlamı").</summary>
        public readonly int[][]? Path;

        public HandData(string kind, int[][]? path)
        {
            Kind = kind;
            Path = path;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("kind", JsonValue.Of(Kind));
            if (Path != null)
            {
                var p = JsonValue.Array();
                foreach (var c in Path) p.Add(JsonValue.Array().Add(JsonValue.Of(c[0])).Add(JsonValue.Of(c[1])));
                o.Set("path", p);
            }
            return o;
        }
    }

    /// <summary>GDD §14.1/3 closed `done` vocabulary; only the filters the event allows are set (strict objects).</summary>
    public sealed class TutCondition
    {
        public readonly string Event;
        public readonly int? Count;
        public readonly int? MinMs;
        public readonly int[]? At;
        public readonly string? Piece;
        public readonly bool? Hidden;
        public readonly bool? Painted;
        public readonly string? Flag;
        public readonly bool? Wind;
        public readonly string? Type;

        public TutCondition(string @event, int? count, int? minMs, int[]? at, string? piece, bool? hidden, bool? painted, string? flag, bool? wind, string? type)
        {
            Event = @event;
            Count = count;
            MinMs = minMs;
            At = at;
            Piece = piece;
            Hidden = hidden;
            Painted = painted;
            Flag = flag;
            Wind = wind;
            Type = type;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("event", JsonValue.Of(Event));
            if (Count.HasValue) o.Set("count", JsonValue.Of(Count.Value));
            if (MinMs.HasValue) o.Set("minMs", JsonValue.Of(MinMs.Value));
            if (At != null) o.Set("at", JsonValue.Array().Add(JsonValue.Of(At[0])).Add(JsonValue.Of(At[1])));
            if (Piece != null) o.Set("piece", JsonValue.Of(Piece));
            if (Hidden.HasValue) o.Set("hidden", JsonValue.Of(Hidden.Value));
            if (Painted.HasValue) o.Set("painted", JsonValue.Of(Painted.Value));
            if (Flag != null) o.Set("flag", JsonValue.Of(Flag));
            if (Wind.HasValue) o.Set("wind", JsonValue.Of(Wind.Value));
            if (Type != null) o.Set("type", JsonValue.Of(Type));
            return o;
        }
    }

    /// <summary>One tutorial step (K-53, Faz 2R). `Mode` is optional and may only be "soft" (L-31 reports others).</summary>
    public sealed class TutorialStepData
    {
        public readonly int Step;
        public readonly string? Mode;
        public readonly string[] Highlight;
        public readonly HandData? Hand;
        public readonly string TextKey;
        public readonly TutCondition? StartOn;
        public readonly TutCondition Done;

        public TutorialStepData(int step, string? mode, string[] highlight, HandData? hand, string textKey, TutCondition? startOn, TutCondition done)
        {
            Step = step;
            Mode = mode;
            Highlight = highlight;
            Hand = hand;
            TextKey = textKey;
            StartOn = startOn;
            Done = done;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object().Set("step", JsonValue.Of(Step));
            if (Mode != null) o.Set("mode", JsonValue.Of(Mode));
            var h = JsonValue.Array();
            foreach (var x in Highlight) h.Add(JsonValue.Of(x));
            o.Set("highlight", h);
            if (Hand != null) o.Set("hand", Hand.ToJson());
            o.Set("textKey", JsonValue.Of(TextKey));
            if (StartOn != null) o.Set("startOn", StartOn.ToJson());
            o.Set("done", Done.ToJson());
            return o;
        }
    }

    /// <summary>`targets` (TECH §2R.2, LEVELS §0): K-50 target bands; the game never reads it, `levels:solve` does.</summary>
    public sealed class TargetsData
    {
        public readonly int[]? MinShifts;
        public readonly int[]? FirstNeedDepth;
        public readonly int[]? Choices0;
        public readonly double[]? DeadRate;

        public TargetsData(int[]? minShifts, int[]? firstNeedDepth, int[]? choices0, double[]? deadRate)
        {
            MinShifts = minShifts;
            FirstNeedDepth = firstNeedDepth;
            Choices0 = choices0;
            DeadRate = deadRate;
        }

        public JsonValue ToJson()
        {
            var o = JsonValue.Object();
            if (MinShifts != null) o.Set("minShifts", JsonValue.Array().Add(JsonValue.Of(MinShifts[0])).Add(JsonValue.Of(MinShifts[1])));
            if (FirstNeedDepth != null) o.Set("firstNeedDepth", JsonValue.Array().Add(JsonValue.Of(FirstNeedDepth[0])).Add(JsonValue.Of(FirstNeedDepth[1])));
            if (Choices0 != null) o.Set("choices0", JsonValue.Array().Add(JsonValue.Of(Choices0[0])).Add(JsonValue.Of(Choices0[1])));
            if (DeadRate != null) o.Set("deadRate", JsonValue.Array().Add(JsonValue.Of(DeadRate[0])).Add(JsonValue.Of(DeadRate[1])));
            return o;
        }
    }

    /// <summary>Parsed level (defaults applied: shutter `phase`, slider `dir`; cargo colour placeholder) — TS `LevelData`.</summary>
    public sealed class LevelData
    {
        public readonly int? SchemaVersion;
        public readonly int Id;
        public readonly int Chapter;
        public readonly I18nText Name;
        /// <summary>easy | normal | hard | superhard.</summary>
        public readonly string Difficulty;
        public readonly int Moves;
        public readonly string? Teaches;
        /// <summary>K-45/1: missing → compile uses `id × 1000 + id`.</summary>
        public readonly long? Seed;
        public readonly GoalData[] Goals;
        public readonly GravityData Gravity;
        public readonly WallData Wall;
        /// <summary>K-49 site size (optional; default `{ cols: 2, rows: 8 }`).</summary>
        public readonly SizeData? Site;
        public readonly BuildData Build;
        public readonly YardData Yard;
        public readonly ObstacleData[] Obstacles;
        public readonly TutorialStepData[]? Tutorial;
        public readonly TargetsData? Targets;

        public LevelData(int? schemaVersion, int id, int chapter, I18nText name, string difficulty, int moves, string? teaches, long? seed,
            GoalData[] goals, GravityData gravity, WallData wall, SizeData? site, BuildData build, YardData yard, ObstacleData[] obstacles,
            TutorialStepData[]? tutorial, TargetsData? targets)
        {
            SchemaVersion = schemaVersion;
            Id = id;
            Chapter = chapter;
            Name = name;
            Difficulty = difficulty;
            Moves = moves;
            Teaches = teaches;
            Seed = seed;
            Goals = goals;
            Gravity = gravity;
            Wall = wall;
            Site = site;
            Build = build;
            Yard = yard;
            Obstacles = obstacles;
            Tutorial = tutorial;
            Targets = targets;
        }

        /// <summary>The zod output object (`LevelSchema.parse(json)`), key order as in the schema.</summary>
        public JsonValue ToJson()
        {
            var o = JsonValue.Object();
            if (SchemaVersion.HasValue) o.Set("schemaVersion", JsonValue.Of(SchemaVersion.Value));
            o.Set("id", JsonValue.Of(Id)).Set("chapter", JsonValue.Of(Chapter)).Set("name", Name.ToJson()).Set("difficulty", JsonValue.Of(Difficulty)).Set("moves", JsonValue.Of(Moves));
            if (Teaches != null) o.Set("teaches", JsonValue.Of(Teaches));
            if (Seed.HasValue) o.Set("seed", JsonValue.Of(Seed.Value));
            var goals = JsonValue.Array();
            foreach (var g in Goals) goals.Add(g.ToJson());
            o.Set("goals", goals).Set("gravity", Gravity.ToJson()).Set("wall", Wall.ToJson());
            if (Site != null) o.Set("site", Site.ToJson());
            o.Set("build", Build.ToJson()).Set("yard", Yard.ToJson());
            var obs = JsonValue.Array();
            foreach (var x in Obstacles) obs.Add(x.ToJson());
            o.Set("obstacles", obs);
            if (Tutorial != null)
            {
                var t = JsonValue.Array();
                foreach (var x in Tutorial) t.Add(x.ToJson());
                o.Set("tutorial", t);
            }
            if (Targets != null) o.Set("targets", Targets.ToJson());
            return o;
        }
    }
}
