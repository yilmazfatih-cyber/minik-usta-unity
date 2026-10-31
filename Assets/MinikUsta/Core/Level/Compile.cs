#nullable enable
// Port of src/core/level/compile.ts (web repo, source of truth). `loadLevel` (schema + runtime logic subset) follows with the
// validator port (logic.ts); until then `Compile(LevelData)` is the entry point for schema-parsed data.
using System;
using System.Collections.Generic;

namespace MinikUsta.Core.Level
{
    /// <summary>K-19 gravity profile (core rule values; visual speeds live in tokens.physics).</summary>
    public sealed class GravityProfile
    {
        /// <summary>low | normal | high.</summary>
        public readonly string Build;
        public readonly bool Yard;
        /// <summary>G-H forced release after this many ms over the site; null = unlimited (low, normal).</summary>
        public readonly int? HoldMs;
        /// <summary>G-H value with "Zaman baskısını azalt" (R-11).</summary>
        public readonly int? HoldMsReduced;
        /// <summary>Glass breaks when the fall distance is strictly greater (S3, K-19): low 4, normal 3, high 2.</summary>
        public readonly int GlassThreshold;
        /// <summary>G-L steering available.</summary>
        public readonly bool Steerable;

        public GravityProfile(string build, bool yard, int? holdMs, int? holdMsReduced, int glassThreshold, bool steerable)
        {
            Build = build;
            Yard = yard;
            HoldMs = holdMs;
            HoldMsReduced = holdMsReduced;
            GlassThreshold = glassThreshold;
            Steerable = steerable;
        }

        public GravityProfile WithYard(bool yard) => new GravityProfile(Build, yard, HoldMs, HoldMsReduced, GlassThreshold, Steerable);
    }

    /// <summary>K-35 step 10 order (GDD; §7.3). The rule `order` field is NOT used for step 10.</summary>
    public readonly struct Step10Timer
    {
        /// <summary>W4 | W5 | S5 | S6 | Y4 | K-40.</summary>
        public readonly string Id;
        public readonly int Order;

        public Step10Timer(string id, int order)
        {
            Id = id;
            Order = order;
        }
    }

    public sealed class CompiledSegment
    {
        public readonly int Index;
        public readonly I18nText Name;
        public readonly int Height;
        /// <summary>Local index `sy * ws + sx` (K-49): colour index 0–7, −1 `.`, −2 outside the plan (`?` already resolved).</summary>
        public readonly sbyte[] PlanColors;
        /// <summary>Bits of `?` cells (local index; ws · hs ≤ 32 bits, read bit 31 as a bit, never as a sign).</summary>
        public readonly int HiddenMask;
        /// <summary>Per site column (length ws): non-`.` plan rows / `.` rows.</summary>
        public readonly int[] PlanMask;
        public readonly int[] DotMask;
        /// <summary>Bits (local index) of every cell that must be filled.</summary>
        public readonly int TargetMask;

        public CompiledSegment(int index, I18nText name, int height, sbyte[] planColors, int hiddenMask, int[] planMask, int[] dotMask, int targetMask)
        {
            Index = index;
            Name = name;
            Height = height;
            PlanColors = planColors;
            HiddenMask = hiddenMask;
            PlanMask = planMask;
            DotMask = dotMask;
            TargetMask = targetMask;
        }
    }

    /// <summary>`help` (Faz 2 K-30 D2 bricks) is no longer produced since Faz 2R (§2R.1: `counts.pieces` = static pieces only).</summary>
    public static class PieceOrigins
    {
        public const string Yard = "yard";
        public const string Truck = "truck";
        public const string Debris = "debris";
        public const string Help = "help";
    }

    /// <summary>
    /// Piece class (GDD K-44, K-47, TECH §2R.2): `cargo` = Ağır Yük (Y5: I5, Q9 in every orientation; colourless, never
    /// supply or demand, may stay in the yard at the win), `material` = every other block, debris included.
    /// </summary>
    public static class PieceClasses
    {
        public const string Material = "material";
        public const string Cargo = "cargo";
    }

    public sealed class CompiledPiece
    {
        public readonly int Id;
        public readonly string Origin;
        /// <summary>Batch index (yard 0, truck k ≥ 1), −1 for debris and help slots.</summary>
        public readonly int Batch;
        /// <summary>Index inside its batch / `build.debris[]`.</summary>
        public readonly int Index;
        /// <summary>K-44 / K-47 class: cargo (I5, Q9) or material.</summary>
        public readonly string Cls;
        /// <summary>Shape id as written in the data.</summary>
        public readonly string DataShape;
        /// <summary>Canonical shape index (K-44: symmetric aliases reduced).</summary>
        public readonly int ShapeIndex;
        /// <summary>Colour index 0–7; −1 for cargo (Ağır Yük is colourless, K-44: the data colour is ignored).</summary>
        public readonly int ColorIndex;
        /// <summary>Start flag bits (state FlagBit; debris gets `debris`).</summary>
        public readonly int Flags;
        public readonly int WetMoves;
        public readonly int StartZone;
        /// <summary>Yard / truck drop column: global anchor x; debris: global site column (`geo.SiteX` …).</summary>
        public readonly int X;
        /// <summary>Yard: global anchor y; truck: `geo.Hy` (ignored, K-25); debris: plan row.</summary>
        public readonly int Y;
        /// <summary>Debris segment; −1 otherwise.</summary>
        public readonly int Segment;

        public CompiledPiece(int id, string origin, int batch, int index, string cls, string dataShape, int shapeIndex, int colorIndex,
            int flags, int wetMoves, int startZone, int x, int y, int segment)
        {
            Id = id;
            Origin = origin;
            Batch = batch;
            Index = index;
            Cls = cls;
            DataShape = dataShape;
            ShapeIndex = shapeIndex;
            ColorIndex = colorIndex;
            Flags = flags;
            WetMoves = wetMoves;
            StartZone = startZone;
            X = x;
            Y = y;
            Segment = segment;
        }
    }

    public sealed class CompiledGap
    {
        public readonly int Index;
        public readonly string Type;
        public readonly int Y;
        public readonly int Size;
        public readonly int Period;
        public readonly int Phase;
        public readonly int[]? Range;
        public readonly int Dir;
        public readonly string? Color;
        public readonly string? KeyId;

        public CompiledGap(int index, string type, int y, int size, int period, int phase, int[]? range, int dir, string? color, string? keyId)
        {
            Index = index;
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
    }

    public sealed class CompiledBatch
    {
        public readonly int Index;
        public readonly int ForSegment;
        public readonly int[] DropColumns;
        public readonly int[] PieceIds;

        public CompiledBatch(int index, int forSegment, int[] dropColumns, int[] pieceIds)
        {
            Index = index;
            ForSegment = forSegment;
            DropColumns = dropColumns;
            PieceIds = pieceIds;
        }
    }

    /// <summary>crate (hp) | cement_bag | screw / key (id, hiddenIndex).</summary>
    public sealed class CompiledObstacle
    {
        public readonly int Index;
        public readonly string Type;
        public readonly int X;
        public readonly int Y;
        /// <summary>Crate hp; 0 for other types.</summary>
        public readonly int Hp;
        public readonly string? Id;
        /// <summary>Index in the state's hidden-item section (screw, key); −1 otherwise.</summary>
        public readonly int HiddenIndex;

        public CompiledObstacle(int index, string type, int x, int y, int hp, string? id, int hiddenIndex)
        {
            Index = index;
            Type = type;
            X = x;
            Y = y;
            Hp = hp;
            Id = id;
            HiddenIndex = hiddenIndex;
        }

        public bool IsCrate => Type == "crate";
        public bool IsCementBag => Type == "cement_bag";
        public bool IsHiddenItem => Type == "screw" || Type == "key";
    }

    public sealed class CompiledElevator
    {
        public readonly int[] Range;
        public readonly int Start;
        public readonly int Dir;

        public CompiledElevator(int[] range, int start, int dir)
        {
            Range = range;
            Start = start;
            Dir = dir;
        }
    }

    /// <summary>
    /// `Compile(levelData)` → CompiledLevel (TECH_DESIGN §2.3): runs once per level, the result is immutable and shared by
    /// every GameState. Holds resolved plans (K-32), wall/gaps, piece table (PieceId order: batch 0, then truck batches
    /// in array order, then debris, then D2 help slots), obstacles, gravity profile (K-19), the step-10 timer list
    /// (STEP10_TIMERS, §7.3), the tutorial highlight → PieceId table (§8.2), the buffer layout (§2.4) and Zobrist tables.
    /// </summary>
    public sealed class CompiledLevel
    {
        public readonly LevelData Data;
        /// <summary>Board geometry (K-49, TECH §2R.1); every core region test reads it as `s.Lvl.Geo`.</summary>
        public readonly BoardGeo Geo;
        public readonly int Id;
        public readonly int Chapter;
        public readonly string Difficulty;
        public readonly int Moves;
        /// <summary>`seed ?? id × 1000 + id` (K-45/1).</summary>
        public readonly long Seed;
        public readonly GoalData[] Goals;
        public readonly GravityProfile Gravity;
        public readonly int WallHeight;
        /// <summary>left | right | null.</summary>
        public readonly string? Fan;
        public readonly CompiledGap[] Gaps;
        /// <summary>segments | carousel.</summary>
        public readonly string Mode;
        public readonly int CarouselEvery;
        public readonly CompiledElevator? Elevator;
        public readonly CompiledSegment[] Segments;
        public readonly CompiledPiece[] Pieces;
        /// <summary>Pieces from data (batches + debris) = `Pieces.Length` since Faz 2R.</summary>
        public readonly int StaticPieceCount;
        /// <summary>Deprecated: Faz 2 D2 help slots are gone (TECH §2R.1): always `Pieces.Length`.</summary>
        public readonly int HelpPieceBase;
        /// <summary>Deprecated: always 0 since Faz 2R.</summary>
        public readonly int HelpPieceCount;
        /// <summary>K-47: material cells per colour index (every batch and the debris), length 8.</summary>
        public readonly int[] Supply;
        /// <summary>K-47: plan cells per colour index over every segment (`?` resolved), length 8.</summary>
        public readonly int[] Demand;
        /// <summary>K-47 item 2: plan cells per segment and colour index.</summary>
        public readonly int[][] DemandBySegment;
        /// <summary>K-47 item 2: material cells per batch and colour index; debris joins the batch index of its segment.</summary>
        public readonly int[][] SupplyByBatch;
        /// <summary>K-47 / K-48 `N`: material blocks of the level (cargo excluded).</summary>
        public readonly int MaterialCount;
        public readonly CompiledBatch[] Batches;
        public readonly CompiledObstacle[] Obstacles;
        /// <summary>Obstacle indices of screws and keys, in hidden-section order.</summary>
        public readonly int[] HiddenItems;
        public readonly string[] Mechanics;
        /// <summary>Active step-10 timers in execution order (K-35 step 10, §7.3).</summary>
        public readonly Step10Timer[] Step10;
        /// <summary>`L` (§2.6).</summary>
        public readonly int Cycle;
        /// <summary>`piece:&lt;i&gt;`, `piece:k&lt;p&gt;_&lt;i&gt;`, `debris:&lt;i&gt;` → PieceId (§2.3, §8.2).</summary>
        public readonly IReadOnlyDictionary<string, int> TutorialPieceIds;
        public readonly StateLayout Layout;
        public readonly ZobristTables Zobrist;
        /// <summary>Buffer offsets of the hashed scalar fields (Hash.cs).</summary>
        public readonly int[] HashFields;

        internal CompiledLevel(LevelData data, BoardGeo geo, long seed, GravityProfile gravity, CompiledGap[] gaps, CompiledElevator? elevator,
            CompiledSegment[] segments, CompiledPiece[] pieces, int staticPieceCount, CoverTotals cover, CompiledBatch[] batches,
            CompiledObstacle[] obstacles, int[] hiddenItems, string[] mechanics, Step10Timer[] step10, int cycle,
            Dictionary<string, int> tutorialPieceIds, StateLayout layout, ZobristTables zobrist, int[] hashFields)
        {
            Data = data;
            Geo = geo;
            Id = data.Id;
            Chapter = data.Chapter;
            Difficulty = data.Difficulty;
            Moves = data.Moves;
            Seed = seed;
            Goals = (GoalData[])data.Goals.Clone();
            Gravity = gravity;
            WallHeight = data.Wall.Height;
            Fan = data.Wall.Fan?.Dir;
            Gaps = gaps;
            Mode = data.Build.Mode;
            CarouselEvery = data.Build.CarouselEvery ?? 0;
            Elevator = elevator;
            Segments = segments;
            Pieces = pieces;
            StaticPieceCount = staticPieceCount;
            HelpPieceBase = staticPieceCount;
            HelpPieceCount = 0;
            Supply = cover.Supply;
            Demand = cover.Demand;
            DemandBySegment = cover.DemandBySegment;
            SupplyByBatch = cover.SupplyByBatch;
            MaterialCount = cover.MaterialCount;
            Batches = batches;
            Obstacles = obstacles;
            HiddenItems = hiddenItems;
            Mechanics = mechanics;
            Step10 = step10;
            Cycle = cycle;
            TutorialPieceIds = tutorialPieceIds;
            Layout = layout;
            Zobrist = zobrist;
            HashFields = hashFields;
        }
    }

    /// <summary>K-47 totals: plan cells and material cells per colour (`?` resolved, cargo excluded, debris in its segment's batch).</summary>
    public sealed class CoverTotals
    {
        public readonly int[] Supply;
        public readonly int[] Demand;
        public readonly int[][] DemandBySegment;
        public readonly int[][] SupplyByBatch;
        public readonly int MaterialCount;

        public CoverTotals(int[] supply, int[] demand, int[][] demandBySegment, int[][] supplyByBatch, int materialCount)
        {
            Supply = supply;
            Demand = demand;
            DemandBySegment = demandBySegment;
            SupplyByBatch = supplyByBatch;
            MaterialCount = materialCount;
        }
    }

    public static partial class Compiler
    {
        public const int PlanDot = -1;
        public const int PlanOutside = -2;

        public static readonly IReadOnlyDictionary<string, GravityProfile> GravityProfiles = new Dictionary<string, GravityProfile>
        {
            { "low", new GravityProfile("low", false, null, null, 4, true) },
            { "normal", new GravityProfile("normal", false, null, null, 3, false) },
            { "high", new GravityProfile("high", false, 700, 1400, 2, false) },
        };

        /// <summary>K-35 step 10 order (GDD; §7.3).</summary>
        public static readonly Step10Timer[] Step10Timers =
        {
            new Step10Timer("W4", 1), // shutter
            new Step10Timer("W5", 2), // slider
            new Step10Timer("S5", 3), // carousel counter (K-23)
            new Step10Timer("S6", 4), // elevator (K-24)
            new Step10Timer("Y4", 5), // wet concrete (E-31)
            new Step10Timer("K-40", 6), // open-shutter expiry
        };

        private static int ColorIndex(string c) => ColorCodes.IndexOf(c);

        /// <summary>`L`: LCM of shutter 2·period, slider 2·(b − a), carousel carouselEvery·S, elevator 2·(b − a) (§2.6).</summary>
        public static int CycleLength(LevelData level)
        {
            var parts = new List<int>();
            foreach (var g in level.Wall.Gaps)
            {
                if (g.Type == "shutter") parts.Add(2 * g.Period);
                if (g.Type == "slider") parts.Add(2 * (g.Range![1] - g.Range[0]));
            }
            if (level.Build.Mode == "carousel" && level.Build.CarouselEvery.HasValue && level.Build.CarouselEvery.Value != 0)
                parts.Add(level.Build.CarouselEvery.Value * level.Build.Segments.Length);
            var el = level.Build.Elevator;
            if (el != null) parts.Add(2 * (el.Range[1] - el.Range[0]));
            return Hash.Lcm(parts);
        }

        /// <summary>Active step-10 timers, in Step10Timers order.</summary>
        public static Step10Timer[] ActiveStep10Timers(LevelData level)
        {
            bool Has(string t)
            {
                foreach (var g in level.Wall.Gaps) if (g.Type == t) return true;
                return false;
            }
            bool wet = false;
            foreach (var b in level.Yard.Batches)
                foreach (var p in b.Pieces)
                    if (p.HasFlag(PieceFlags.Wet)) wet = true;
            var active = new Dictionary<string, bool>
            {
                { "W4", Has("shutter") },
                { "W5", Has("slider") },
                { "S5", level.Build.Mode == "carousel" },
                { "S6", level.Build.Elevator != null },
                { "Y4", wet },
                { "K-40", Has("shutter") || Has("locked") },
            };
            var o = new List<Step10Timer>();
            foreach (var t in Step10Timers) if (active[t.Id]) o.Add(t);
            return o.ToArray();
        }

        /// <summary>Compiles schema-valid level data. Throws on structurally impossible data (run the validator first).</summary>
        public static CompiledLevel Compile(LevelData level)
        {
            var geo = Geometry.GeoFromLevel(level);
            var plans = Plan.BuildPlans(level, geo).Plans;
            var segments = new CompiledSegment[plans.Length];
            for (int index = 0; index < plans.Length; index++)
            {
                var p = plans[index];
                var planColors = new sbyte[geo.SegCells];
                for (int i = 0; i < planColors.Length; i++) planColors[i] = PlanOutside;
                for (int i = 0; i < p.Cells.Length; i++)
                {
                    char c = p.Cells[i];
                    if (c == Plan.Dot) planColors[i] = PlanDot;
                    else if (c != Plan.None) planColors[i] = (sbyte)ColorIndex(c.ToString());
                }
                var name = index < level.Build.Segments.Length ? level.Build.Segments[index].Name : new I18nText("", "");
                var planMask = new int[geo.Ws];
                var dotMask = new int[geo.Ws];
                for (int sx = 0; sx < geo.Ws; sx++)
                {
                    planMask[sx] = sx < p.PlanMask.Length ? p.PlanMask[sx] : 0;
                    dotMask[sx] = sx < p.DotMask.Length ? p.DotMask[sx] : 0;
                }
                segments[index] = new CompiledSegment(index, name, p.Height, planColors, p.HiddenMask, planMask, dotMask, p.TargetMask);
            }

            var pieces = new List<CompiledPiece>();
            var tutorialPieceIds = new Dictionary<string, int>();
            var batches = new CompiledBatch[level.Yard.Batches.Length];
            for (int k = 0; k < level.Yard.Batches.Length; k++)
            {
                var b = level.Yard.Batches[k];
                var pieceIds = new int[b.Pieces.Length];
                for (int i = 0; i < b.Pieces.Length; i++)
                {
                    var p = b.Pieces[i];
                    int id = pieces.Count;
                    var shape = Shapes.ShapeById(p.Shape);
                    int flags = 0;
                    if (p.Flags != null) foreach (var f in p.Flags) flags |= FlagBit.Of(f);
                    string cls = PieceClassOf(p.Shape);
                    pieces.Add(new CompiledPiece(
                        id,
                        k == 0 ? PieceOrigins.Yard : PieceOrigins.Truck,
                        k,
                        i,
                        cls,
                        p.Shape,
                        shape.CanonicalIndex,
                        cls == PieceClasses.Cargo ? -1 : ColorIndex(p.Color),
                        flags,
                        p.WetMoves ?? 0,
                        k == 0 ? Zone.Yard : Zone.Pending,
                        p.X,
                        k == 0 ? p.Y : geo.Hy,
                        -1));
                    tutorialPieceIds[k == 0 ? "piece:" + i : "piece:k" + k + "_" + i] = id;
                    pieceIds[i] = id;
                }
                batches[k] = new CompiledBatch(k, b.ForSegment, b.DropColumns != null ? (int[])b.DropColumns.Clone() : new int[0], pieceIds);
            }
            var debrisList = level.Build.Debris ?? new DebrisData[0];
            for (int i = 0; i < debrisList.Length; i++)
            {
                var d = debrisList[i];
                int id = pieces.Count;
                pieces.Add(new CompiledPiece(
                    id,
                    PieceOrigins.Debris,
                    -1,
                    i,
                    PieceClasses.Material,
                    d.Shape,
                    Shapes.ShapeById(d.Shape).CanonicalIndex,
                    ColorIndex(d.Color),
                    FlagBit.Debris,
                    0,
                    Zone.Site,
                    d.X,
                    d.Y,
                    d.Segment ?? 0));
                tutorialPieceIds["debris:" + i] = id;
            }
            int staticPieceCount = pieces.Count;
            var pieceArr = pieces.ToArray();
            var cover = ComputeCoverTotals(level, segments, pieceArr);

            int hiddenCount = 0;
            var hiddenItems = new List<int>();
            var obstacles = new CompiledObstacle[level.Obstacles.Length];
            for (int index = 0; index < level.Obstacles.Length; index++)
            {
                var o = level.Obstacles[index];
                if (o.Type == "crate") obstacles[index] = new CompiledObstacle(index, "crate", o.X, o.Y, o.Hp ?? 1, null, -1);
                else if (o.Type == "cement_bag") obstacles[index] = new CompiledObstacle(index, "cement_bag", o.X, o.Y, 0, null, -1);
                else
                {
                    hiddenItems.Add(index);
                    obstacles[index] = new CompiledObstacle(index, o.Type, o.X, o.Y, 0, o.Id, hiddenCount++);
                }
            }

            var gaps = new CompiledGap[level.Wall.Gaps.Length];
            for (int index = 0; index < gaps.Length; index++)
            {
                var g = level.Wall.Gaps[index];
                gaps[index] = new CompiledGap(
                    index,
                    g.Type,
                    g.Y,
                    g.Size,
                    g.Type == "shutter" ? g.Period : 0,
                    g.Type == "shutter" ? g.Phase : 0,
                    g.Type == "slider" ? new[] { g.Range![0], g.Range[1] } : null,
                    g.Type == "slider" ? g.Dir : 1,
                    g.Type == "paint" ? g.Color : null,
                    g.Type == "locked" ? g.KeyId : null);
            }

            var layout = State.ComputeLayout(
                new StateCounts(segments.Length, pieceArr.Length, gaps.Length, obstacles.Length, hiddenCount, level.Goals.Length),
                geo);
            int cycle = CycleLength(level);
            var el = level.Build.Elevator;
            return new CompiledLevel(
                level,
                geo,
                level.Seed ?? (long)level.Id * 1000 + level.Id,
                GravityProfiles[level.Gravity.Build].WithYard(level.Gravity.Yard),
                gaps,
                el != null ? new CompiledElevator(new[] { el.Range[0], el.Range[1] }, el.Start, el.Dir) : null,
                segments,
                pieceArr,
                staticPieceCount,
                cover,
                batches,
                obstacles,
                hiddenItems.ToArray(),
                Level.Mechanics.DeriveMechanics(level),
                ActiveStep10Timers(level),
                cycle,
                tutorialPieceIds,
                layout,
                Hash.BuildZobrist(layout, cycle, geo),
                Hash.HashedFieldOffsets(layout, geo));
        }

        /// <summary>K-44: I5 and Q9 (every orientation) are Ağır Yük; every other shape is a material block.</summary>
        public static string PieceClassOf(string shape)
        {
            string kind = Shapes.ShapeById(shape).Kind;
            return kind == "I5" || kind == "Q9" ? PieceClasses.Cargo : PieceClasses.Material;
        }

        private static CoverTotals ComputeCoverTotals(LevelData level, CompiledSegment[] segments, CompiledPiece[] pieces)
        {
            int colors = ColorCodes.All.Length;
            var demandBySegment = new int[segments.Length][];
            for (int s = 0; s < segments.Length; s++)
            {
                var o = new int[colors];
                foreach (sbyte c in segments[s].PlanColors) if (c >= 0) o[c] += 1;
                demandBySegment[s] = o;
            }
            var demand = new int[colors];
            foreach (var row in demandBySegment) for (int c = 0; c < colors; c++) demand[c] += row[c];
            int batchCount = Math.Max(level.Yard.Batches.Length, segments.Length);
            var supplyByBatch = new int[batchCount][];
            for (int k = 0; k < batchCount; k++) supplyByBatch[k] = new int[colors];
            var supply = new int[colors];
            int materialCount = 0;
            foreach (var p in pieces)
            {
                if (p.Cls != PieceClasses.Material || p.ColorIndex < 0) continue;
                int cells = Shapes.ShapeById(p.DataShape).CellCount;
                int k = p.Origin == PieceOrigins.Debris ? p.Segment : p.Batch;
                if (k >= 0 && k < batchCount) supplyByBatch[k][p.ColorIndex] += cells;
                supply[p.ColorIndex] += cells;
                materialCount += 1;
            }
            return new CoverTotals(supply, demand, demandBySegment, supplyByBatch, materialCount);
        }
    }
}
