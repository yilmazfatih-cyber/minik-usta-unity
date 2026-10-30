#nullable enable
// Port of src/core/level/plan.ts (web repo, source of truth).
using System;
using System.Collections.Generic;

namespace MinikUsta.Core.Level
{
    /// <summary>
    /// Segment plans (GDD K-15, K-32, K-49). `rows` are written top → bottom; plan row r (from the bottom) is
    /// `rows[h − 1 − r]`. Local index of a plan cell = `sy * ws + sx` (sx 0 = board x `wy`, the first site column; the
    /// default 2-wide site gives the pre-2R `sy * 2 + sx`). Sizes come from the level geometry (`GeoFromLevel`, K-49).
    ///
    /// Plan characters are stored as `char`: a colour code, '.' (must stay empty), '?' (raw only) or `Plan.None` ('\0')
    /// for "outside the plan / unresolved" (TS `null`).
    /// </summary>
    public sealed class SegmentPlan
    {
        public readonly int Height;
        /// <summary>Raw characters by local index (`None` above the plan).</summary>
        public readonly char[] Raw;
        /// <summary>Resolved cells by local index; `?` replaced by its colour (`None` when it cannot be resolved).</summary>
        public readonly char[] Cells;
        /// <summary>Bits (local index) of `?` cells.</summary>
        public readonly int HiddenMask;
        /// <summary>Per site column (length ws): plan rows that are not `.` (bit = row).</summary>
        public readonly int[] PlanMask;
        /// <summary>Per site column (length ws): `.` rows.</summary>
        public readonly int[] DotMask;
        /// <summary>Bits (local index) of every cell that must be filled (colour or `?`).</summary>
        public readonly int TargetMask;

        public SegmentPlan(int height, char[] raw, char[] cells, int hiddenMask, int[] planMask, int[] dotMask, int targetMask)
        {
            Height = height;
            Raw = raw;
            Cells = cells;
            HiddenMask = hiddenMask;
            PlanMask = planMask;
            DotMask = dotMask;
            TargetMask = targetMask;
        }
    }

    public sealed class HiddenProblem
    {
        public readonly int Seg;
        public readonly int Sx;
        public readonly int Sy;
        public readonly string Message;

        public HiddenProblem(int seg, int sx, int sy, string message)
        {
            Seg = seg;
            Sx = sx;
            Sy = sy;
            Message = message;
        }

        public override string ToString() => "seg " + Seg + " (" + Sx + "," + Sy + "): " + Message;
    }

    public sealed class PlansResult
    {
        public readonly SegmentPlan[] Plans;
        public readonly HiddenProblem[] Problems;

        public PlansResult(SegmentPlan[] plans, HiddenProblem[] problems)
        {
            Plans = plans;
            Problems = problems;
        }
    }

    public static class Plan
    {
        /// <summary>TS `null` plan character.</summary>
        public const char None = '\0';
        public const char Dot = '.';
        public const char Hidden = '?';

        /// <summary>
        /// Local plan index `sy · ws + sx` (K-49). `ws` defaults to the default 2-wide site: a compatibility default for callers
        /// written before Faz 2R (pass `geo.Ws`).
        /// </summary>
        public static int LocalIndex(int sx, int sy, int ws = 2) => sy * ws + sx;

        /// <summary>Raw characters by local index; rows above Hs (or longer than ws) are cut by the frame, missing cells stay None.</summary>
        private static char[] RawCells(BoardGeo geo, string[] rows)
        {
            var o = new char[geo.SegCells];
            int h = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                int sy = h - 1 - i;
                if (sy < 0 || sy >= geo.Hs) continue;
                string row = rows[i];
                for (int sx = 0; sx < geo.Ws; sx++)
                    if (sx < row.Length) o[LocalIndex(sx, sy, geo.Ws)] = row[sx];
            }
            return o;
        }

        private static bool IsColor(char c) => c != None && ColorCodes.IndexOf(c.ToString()) >= 0;

        /// <summary>
        /// Builds every segment plan and resolves `?` cells (K-32): `repeat p` takes (c, r − p) of the same segment (chained),
        /// `mirrorOf j` takes (ws − 1 − c, r) of segment j (default site: (1 − c, r)). Problems found on the way are L-08
        /// `hidden_invalid` material. `geo` defaults to the level's own geometry (K-49).
        /// </summary>
        public static PlansResult BuildPlans(LevelData level, BoardGeo? geo = null)
        {
            geo ??= Geometry.GeoFromLevel(level);
            var segs = level.Build.Segments;
            int ws = geo.Ws;
            var raws = new char[segs.Length][];
            for (int i = 0; i < segs.Length; i++) raws[i] = RawCells(geo, segs[i].Rows);
            var problems = new List<HiddenProblem>();

            char Resolve(int seg, int sx, int sy, int depth)
            {
                int li = LocalIndex(sx, sy, ws);
                char raw = seg >= 0 && seg < raws.Length && li >= 0 && li < raws[seg].Length ? raws[seg][li] : None;
                if (raw == None || raw == Dot) return None;
                if (IsColor(raw)) return raw;
                // raw === '?'
                if (depth > 64) return None;
                var rule = segs[seg].Hidden;
                if (rule == null) return None;
                if (rule.Kind == "repeat")
                {
                    if (sy - rule.Period < 0) return None;
                    return Resolve(seg, sx, sy - rule.Period, depth + 1);
                }
                int sli = LocalIndex(ws - 1 - sx, sy, ws);
                char src = rule.Segment >= 0 && rule.Segment < raws.Length && sli >= 0 && sli < raws[rule.Segment].Length
                    ? raws[rule.Segment][sli]
                    : None;
                return IsColor(src) ? src : None;
            }

            var plans = new SegmentPlan[segs.Length];
            for (int seg = 0; seg < segs.Length; seg++)
            {
                var s = segs[seg];
                var raw = raws[seg];
                int h = Math.Min(s.Rows.Length, geo.Hs);
                var cells = new char[geo.SegCells];
                int hiddenMask = 0;
                int targetMask = 0;
                var planMask = new int[ws];
                var dotMask = new int[ws];
                for (int i = 0; i < geo.SegCells; i++)
                {
                    char ch = raw[i];
                    int sx = i % ws;
                    int sy = i / ws;
                    if (ch == None)
                    {
                        cells[i] = None;
                        continue;
                    }
                    if (ch == Dot)
                    {
                        cells[i] = Dot;
                        dotMask[sx] |= 1 << sy;
                        continue;
                    }
                    planMask[sx] |= 1 << sy;
                    targetMask |= 1 << i;
                    if (ch == Hidden)
                    {
                        hiddenMask |= 1 << i;
                        char color = Resolve(seg, sx, sy, 0);
                        cells[i] = color;
                        var rule = s.Hidden;
                        if (rule == null) problems.Add(new HiddenProblem(seg, sx, sy, "`?` in a segment without a hidden rule"));
                        else if (rule.Kind == "repeat" && sy < rule.Period)
                            problems.Add(new HiddenProblem(seg, sx, sy, "repeat " + rule.Period + ": `?` in the bottom " + rule.Period + " row(s)"));
                        else if (color == None)
                            problems.Add(new HiddenProblem(seg, sx, sy, "`?` resolves to `.` or cannot be resolved"));
                    }
                    else cells[i] = ch;
                }
                plans[seg] = new SegmentPlan(h, raw, cells, hiddenMask, planMask, dotMask, targetMask);
            }

            for (int seg = 0; seg < segs.Length; seg++)
            {
                var rule = segs[seg].Hidden;
                if (rule == null || rule.Kind != "mirrorOf") continue;
                int j = rule.Segment;
                var target = j >= 0 && j < segs.Length ? segs[j] : null;
                if (j >= seg || target == null)
                    problems.Add(new HiddenProblem(seg, 0, 0, "mirrorOf " + j + ": target must be an earlier segment"));
                else if (target.Rows.Length != segs[seg].Rows.Length)
                    problems.Add(new HiddenProblem(seg, 0, 0, "mirrorOf " + j + ": target height differs"));
                else
                {
                    bool hasHidden = false;
                    foreach (var r in target.Rows) if (r.IndexOf('?') >= 0) hasHidden = true;
                    if (hasHidden) problems.Add(new HiddenProblem(seg, 0, 0, "mirrorOf " + j + ": target contains `?`"));
                }
            }
            return new PlansResult(plans, problems.ToArray());
        }
    }
}
