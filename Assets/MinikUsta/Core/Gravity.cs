#nullable enable
// Port of src/core/gravity.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>The fall a release starts (TECH §5.1). `Ix`/`Iy` = release anchor (read-only context for hooks).</summary>
    public sealed class FallPlan
    {
        public readonly int Ix;
        public readonly int Iy;
        /// <summary>−1 falls; +1 rises (S8 balloon).</summary>
        public readonly int Dir;
        /// <summary>W8 wind shift at the release row, before the fall (−1, 0, 1).</summary>
        public readonly int Drift;
        /// <summary>G-L steering input (GDD K-19, `{ dir, atRow }`); only present on steerable levels.</summary>
        public readonly Steer? Steer;

        public FallPlan(int ix, int iy, int dir, int drift, Steer? steer)
        {
            Ix = ix;
            Iy = iy;
            Dir = dir;
            Drift = drift;
            Steer = steer;
        }

        /// <summary>TS `{ ...plan, dir }`.</summary>
        public FallPlan WithDir(int dir) => new FallPlan(Ix, Iy, dir, Drift, Steer);
        /// <summary>TS `{ ...plan, drift }`.</summary>
        public FallPlan WithDrift(int drift) => new FallPlan(Ix, Iy, Dir, drift, Steer);
    }

    /// <summary>`onLanded` result (TECH §7.1): S3 glass breaks with a move-cost penalty. `Kind` is "none" or "break".</summary>
    public sealed class LandingEffect : IEquatable<LandingEffect>
    {
        public const string KindNone = "none";
        public const string KindBreak = "break";

        public readonly string Kind;
        public readonly int Penalty;

        private LandingEffect(string kind, int penalty)
        {
            Kind = kind;
            Penalty = penalty;
        }

        /// <summary>TS `NO_EFFECT`.</summary>
        public static readonly LandingEffect None = new LandingEffect(KindNone, 0);
        public static LandingEffect Break(int penalty) => new LandingEffect(KindBreak, penalty);

        public bool Equals(LandingEffect? other) => other != null && Kind == other.Kind && Penalty == other.Penalty;
        public override bool Equals(object? obj) => obj is LandingEffect e && Equals(e);
        public override int GetHashCode() => Kind.GetHashCode() * 31 + Penalty;
        public override string ToString() => Kind == KindNone ? "none" : "break(" + Penalty + ")";
    }

    public sealed class FallResult : IEquatable<FallResult>
    {
        public const string ModeFree = "free";
        public const string ModeRail = "rail";

        /// <summary>FREE: dropped and fell (or rose); RAIL: stays where it is released (K-12).</summary>
        public readonly string Mode;
        /// <summary>Board anchor where the block comes to rest (may be in the crane rows on a full column: outside the plan).</summary>
        public readonly Anchor Landing;
        /// <summary>Motion direction of the plan: −1 fall, +1 balloon.</summary>
        public readonly int Dir;
        /// <summary>
        /// Rows travelled in `Dir` from the release row to the landing row (K-11 `d`, GDD K-19 rule 5: total, steering
        /// included). Falls: ≥ 0 (`pieceFell.rows`); balloon: rise, negative when it came down to the ceiling
        /// (`balloonRose.rows`, E-35).
        /// </summary>
        public readonly int Distance;
        /// <summary>Turning points for the animation: release, wind shift, steer row and shift, landing (no repeats).</summary>
        public readonly Anchor[] Path;
        public readonly int Drift;
        /// <summary>The steering that took effect; null when none was given or it was void (K-19 rule 4: the right is kept).</summary>
        public readonly Steer? Steered;
        /// <summary>Board cells at the landing.</summary>
        public readonly BoardCell[] Cells;
        /// <summary>`IsCorrectPlacement` of the landing (K-16 + K-34; shadow K-18).</summary>
        public readonly Verdict Verdict;
        /// <summary>A landing cell covers an unrevealed `?` plan cell: the shadow is neutral at every difficulty (K-18, E-20).</summary>
        public readonly bool TouchesHidden;
        /// <summary>`onLanded` preview (S3 glass break); always `none` on the rail.</summary>
        public readonly LandingEffect Effect;

        public FallResult(string mode, Anchor landing, int dir, int distance, Anchor[] path, int drift, Steer? steered, BoardCell[] cells,
            Verdict verdict, bool touchesHidden, LandingEffect effect)
        {
            Mode = mode;
            Landing = landing;
            Dir = dir;
            Distance = distance;
            Path = path;
            Drift = drift;
            Steered = steered;
            Cells = cells;
            Verdict = verdict;
            TouchesHidden = touchesHidden;
            Effect = effect;
        }

        internal FallResult WithEffect(LandingEffect effect) =>
            new FallResult(Mode, Landing, Dir, Distance, Path, Drift, Steered, Cells, Verdict, TouchesHidden, effect);

        public bool Equals(FallResult? o)
        {
            if (o == null) return false;
            return Mode == o.Mode && Landing.Equals(o.Landing) && Dir == o.Dir && Distance == o.Distance && Seq(Path, o.Path) && Drift == o.Drift
                && SteerEq(Steered, o.Steered) && Seq(Cells, o.Cells) && Verdict.Equals(o.Verdict) && TouchesHidden == o.TouchesHidden && Effect.Equals(o.Effect);
        }

        private static bool SteerEq(Steer? a, Steer? b) => a.HasValue == b.HasValue && (!a.HasValue || (a.Value.Dir == b!.Value.Dir && a.Value.AtRow == b.Value.AtRow));

        private static bool Seq<T>(T[] a, T[] b) where T : IEquatable<T>
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is FallResult f && Equals(f);
        public override int GetHashCode() => Landing.GetHashCode() * 31 + Distance;
        public override string ToString() => Mode + " landing " + Landing + " d " + Distance + " " + (Verdict.Ok ? "ok" : string.Join(",", Verdict.Reasons));
    }

    /// <summary>Rule hooks of a fall (TECH §7.1 `modifyFall`, `onLanded`), composed in rule order by the caller. Pure previews.</summary>
    public sealed class FallRules
    {
        public static readonly FallRules Default = new FallRules();

        public readonly Func<GameState, int, FallPlan, FallPlan>? ModifyFall;
        /// <summary>Receives the fall without its effect (TS `Omit&lt;FallResult, 'effect'&gt;`: `Effect` is `LandingEffect.None`).</summary>
        public readonly Func<GameState, int, FallResult, LandingEffect>? OnLanded;

        public FallRules(Func<GameState, int, FallPlan, FallPlan>? modifyFall = null, Func<GameState, int, FallResult, LandingEffect>? onLanded = null)
        {
            ModifyFall = modifyFall;
            OnLanded = onLanded;
        }
    }

    public sealed class FallOptions
    {
        /// <summary>G-L steering (move record `steer`); ignored unless the level's gravity profile is steerable (K-19).</summary>
        public readonly Steer? Steer;
        public readonly FallRules? Rules;

        public FallOptions(Steer? steer = null, FallRules? rules = null)
        {
            Steer = steer;
            Rules = rules;
        }
    }

    /// <summary>What the fall shadow may show (GDD K-18, K-34 hook 2; style is design-lead's).</summary>
    public sealed class ShadowInfo : IEquatable<ShadowInfo>
    {
        public const string ToneCorrect = "correct";
        public const string ToneWrong = "wrong";
        public const string ToneNeutral = "neutral";

        /// <summary>Easy/normal: correct or wrong; hard/superhard or a covered unrevealed `?` cell: neutral (position only).</summary>
        public readonly string Tone;
        /// <summary>Reasons the shadow may show: all on easy/normal; only `support` when neutral (never `color`).</summary>
        public readonly string[] Reasons;
        public readonly At[] MissingSupport;
        /// <summary>Physics information, shown at every difficulty (S3 crack).</summary>
        public readonly bool Breaks;

        public ShadowInfo(string tone, string[] reasons, At[] missingSupport, bool breaks)
        {
            Tone = tone;
            Reasons = reasons;
            MissingSupport = missingSupport;
            Breaks = breaks;
        }

        public bool Equals(ShadowInfo? o)
        {
            if (o == null || Tone != o.Tone || Breaks != o.Breaks || Reasons.Length != o.Reasons.Length || MissingSupport.Length != o.MissingSupport.Length) return false;
            for (int i = 0; i < Reasons.Length; i++) if (Reasons[i] != o.Reasons[i]) return false;
            for (int i = 0; i < MissingSupport.Length; i++) if (!MissingSupport[i].Equals(o.MissingSupport[i])) return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is ShadowInfo s && Equals(s);
        public override int GetHashCode() => Tone.GetHashCode() * 31 + Reasons.Length;
        public override string ToString() => Tone + "[" + string.Join(",", Reasons) + "] missing " + MissingSupport.Length + (Breaks ? " breaks" : "");
    }

    /// <summary>One entity's total motion in a `SettleYard` call. `Kind` is "piece" or "bag".</summary>
    public sealed class YardMotion : IEquatable<YardMotion>
    {
        public const string KindPiece = "piece";
        public const string KindBag = "bag";

        public readonly string Kind;
        /// <summary>Piece id, or obstacle index of a bag.</summary>
        public readonly int Id;
        public readonly Anchor From;
        public readonly Anchor To;
        /// <summary>`To.Iy − From.Iy`: &lt; 0 fell (`pieceFell{cause:'yardGravity'}.rows = −dy`), &gt; 0 a balloon rose.</summary>
        public readonly int Dy;

        public YardMotion(string kind, int id, Anchor from, Anchor to, int dy)
        {
            Kind = kind;
            Id = id;
            From = from;
            To = to;
            Dy = dy;
        }

        public bool Equals(YardMotion? o) => o != null && Kind == o.Kind && Id == o.Id && From.Equals(o.From) && To.Equals(o.To) && Dy == o.Dy;
        public override bool Equals(object? obj) => obj is YardMotion m && Equals(m);
        public override int GetHashCode() => Id * 31 + Dy;
        public override string ToString() => Kind + " " + Id + " " + From + "→" + To + " dy " + Dy;
    }

    /// <summary>A block that fell in one settle pass, with its cells before that pass (K-35 step 6 neighbour effects).</summary>
    public sealed class FallenBlock
    {
        public readonly int PieceId;
        public readonly BoardCell[] Cells;

        public FallenBlock(int pieceId, BoardCell[] cells)
        {
            PieceId = pieceId;
            Cells = cells;
        }
    }

    public sealed class SettleHooks
    {
        public static readonly SettleHooks Default = new SettleHooks();

        /// <summary>
        /// Neighbour effects of the blocks that fell in a pass (bags and rising balloons produce none, N26), in (y, x) order
        /// of their pre-fall anchors. Return true when the yard changed (a bag tore, a crate vanished): gravity runs again
        /// (E-12). Default: none.
        /// </summary>
        public readonly Func<GameState, FallenBlock[], bool>? OnFallen;

        public SettleHooks(Func<GameState, FallenBlock[], bool>? onFallen = null)
        {
            OnFallen = onFallen;
        }
    }

    /// <summary>
    /// Falls and yard gravity (docs/GDD.md K-11, K-18, K-19, K-20; OBSTACLES W8, S3, S8, Y2, Y6; TECH_DESIGN §5.1, §5.3).
    ///
    /// `ComputeFall` is the ONE fall function of the shadow (K-18) and of the move pipeline (K-35 step 2): the shadow shows
    /// exactly what a release would do. Order (GDD K-11): rule `modifyFall` hooks (W8 wind sets `drift`, S8 balloon sets
    /// `dir = +1`) → wind shift at the release row → fall (or balloon rise) → G-L steering (K-19, only where the level's
    /// gravity profile is steerable) → landing; then the verdict (`IsCorrectPlacement`, K-16 + K-34), the `?` neutrality
    /// flag (K-18, E-20) and the `onLanded` preview (S3 glass break). Core has no obstacle-specific code: hooks come from
    /// the caller (obstacle registry), like movement's `DragRules`.
    ///
    /// `SettleYard` is K-35 step 6 (TECH §5.3): synchronous half steps (falling half with balloons solid, rising half with
    /// everything else solid) until nothing moves, then the neighbour effects of the falls (rule hook); when a hook tears a
    /// bag or removes a crate, gravity runs again (E-12). Bags (Y2) always fall; blocks fall and balloons rise only with
    /// yard gravity (Y6, K-20). Crates never move and support.
    /// </summary>
    public static class Gravity
    {
        /// <summary>Safety bound of the outer loop; each extra pass needs an obstacle to disappear.</summary>
        private const int MaxSettlePasses = 64;

        // --- computeFall ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// `computeFall(state, pieceId, node, opts)` (TECH §5.1). `node` = a release node with every cell on the site
        /// (`Classify` rows 6 and 7); the board is the frozen drag board (the piece may still sit at its start: it is ignored).
        /// Pure: never changes the state. Throws when the node is not fully on the site.
        /// </summary>
        public static FallResult ComputeFall(GameState s, int pieceId, DragNode node, FallOptions? opts = null)
        {
            var shape = Shapes.ShapeByIndex(PieceShape(s, pieceId));
            var geo = s.Lvl.Geo;
            if (node.Ix < geo.SiteX || node.Ix + shape.W > geo.Cols || node.Iy < 0 || node.Iy + shape.H > geo.Rows)
                throw new ArgumentOutOfRangeException(nameof(node), "computeFall: " + shape.Id + " at (" + node.Ix + "," + node.Iy + ") is not fully on the site");

            if (node.Mode != Movement.Free)
            {
                // K-12 / K-18: the rail holds the block where it is released; no wind, no balloon, no glass break
                var landing = new Anchor(node.Ix, node.Iy);
                var railCells = Movement.BlockCells(shape, node.Ix, node.Iy);
                return new FallResult(FallResult.ModeRail, landing, -1, 0, new[] { landing }, 0, null, railCells,
                    Placement.IsCorrectPlacement(s, pieceId, railCells), TouchesHiddenCell(s, railCells), LandingEffect.None);
            }

            var rules = opts?.Rules ?? FallRules.Default;
            Steer? steer = opts != null && opts.Steer.HasValue && s.Lvl.Gravity.Steerable ? opts.Steer : null;
            var plan = new FallPlan(node.Ix, node.Iy, -1, 0, steer);
            if (rules.ModifyFall != null) plan = rules.ModifyFall(s, pieceId, plan);

            var cols = SiteColumnMasks(s, pieceId);
            int y0 = node.Iy;
            int x = node.Ix;
            var path = new List<Anchor> { new Anchor(x, y0) };

            // W8: shift one column at the release row when the shifted block is on the site, free and under open sky
            int drift = 0;
            if (plan.Drift != 0 && FitsOnSite(geo, cols, shape, x + plan.Drift, y0) && OpenSky(geo, cols, shape, x + plan.Drift, y0))
            {
                x += plan.Drift;
                drift = plan.Drift;
                PushPoint(path, x, y0);
            }

            int dir = plan.Dir;
            int ceil = CeilingAnchor(s, shape);
            int land = dir < 0 ? FallFrom(geo, cols, shape, x, y0) : Math.Max(ceil, FallFrom(geo, cols, shape, x, y0));

            // G-L (K-19 rules 3–4): shift one column at `atRow`, between the release row and the unsteered landing row (both
            // included), when every shifted cell is on the site columns and empty; then go on in the same direction there
            Steer? steered = null;
            Steer? st = s.Lvl.Gravity.Steerable ? plan.Steer : null;
            if (st.HasValue)
            {
                int nx = x + st.Value.Dir;
                int lo = Math.Min(y0, land);
                int hi = Math.Max(y0, land);
                int atRow = st.Value.AtRow;
                if (atRow >= lo && atRow <= hi && FitsOnSite(geo, cols, shape, nx, atRow))
                {
                    PushPoint(path, x, atRow);
                    x = nx;
                    PushPoint(path, x, atRow);
                    steered = new Steer(st.Value.Dir, atRow);
                    if (dir < 0) land = FallFrom(geo, cols, shape, x, atRow);
                    else
                        land = atRow <= ceil
                            ? RiseFrom(geo, cols, shape, x, atRow, ceil)
                            : Math.Max(ceil, FallFrom(geo, cols, shape, x, atRow));
                }
            }
            PushPoint(path, x, land);

            var landAt = new Anchor(x, land);
            var cells = Movement.BlockCells(shape, x, land);
            var partial = new FallResult(FallResult.ModeFree, landAt, dir, dir < 0 ? y0 - land : land - y0, path.ToArray(), drift, steered, cells,
                Placement.IsCorrectPlacement(s, pieceId, cells), TouchesHiddenCell(s, cells), LandingEffect.None);
            return rules.OnLanded != null ? partial.WithEffect(rules.OnLanded(s, pieceId, partial)) : partial;
        }

        /// <summary>
        /// Blocked frame rows (bits 0 … rows − 1) of the site columns, `out[sx]` = column `geo.SiteX + sx` (length ws; default
        /// board: x 6 and x 7): platform rows below the elevator offset and the visible segment's occupied cells (board rows
        /// &lt; h, as in the drag collision masks), the `exclude` piece left out.
        /// </summary>
        public static int[] SiteColumnMasks(GameState s, int exclude = -1)
        {
            var geo = s.Lvl.Geo;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int h = geo.H;
            int rows = geo.Rows;
            int seg = Grid.VisibleSegment(s);
            int elev = Hdr(s, H.Elev);
            int skip = exclude + 1;
            var o = new int[ws];
            for (int sx = 0; sx < ws; sx++)
            {
                int m = elev > 0 ? (1 << Math.Min(elev, rows)) - 1 : 0;
                for (int sy = 0; sy < hs && sy + elev < h; sy++)
                {
                    int v = SiteOcc(s, seg, sx, sy);
                    if (v != 0 && v != skip) m |= 1 << (sy + elev);
                }
                o[sx] = m;
            }
            return o;
        }

        /// <summary>
        /// Unsteered, unshifted landing row of `pieceId` released FREE at `(ix, iy)` on the site, moving in `dir` (−1 fall,
        /// +1 balloon): what a W8 hook needs for its `d ≥ 1` condition (E-17; balloon `d = |release − ceiling|`, E-35).
        /// </summary>
        public static int SiteLandingRow(GameState s, int pieceId, int ix, int iy, int dir)
        {
            var shape = Shapes.ShapeByIndex(PieceShape(s, pieceId));
            var cols = SiteColumnMasks(s, pieceId);
            int support = FallFrom(s.Lvl.Geo, cols, shape, ix, iy);
            return dir < 0 ? support : Math.Max(CeilingAnchor(s, shape), support);
        }

        /// <summary>
        /// S8 in the yard (K-10, E-14): a balloon released at yard anchor `(ix, iy)` rises until its top cell is under the first
        /// occupied cell of its columns, or at the yard's top row y = hy − 1 (default 7). Returns the anchor row (the piece
        /// itself is ignored).
        /// </summary>
        public static int YardBalloonLanding(GameState s, int pieceId, int ix, int iy)
        {
            var shape = Shapes.ShapeByIndex(PieceShape(s, pieceId));
            int skip = pieceId + 1;
            int hy = s.Lvl.Geo.Hy;
            int land = hy - shape.H;
            for (int c = 0; c < shape.W; c++)
            {
                int top = shape.ColTop[c];
                for (int y = iy + top + 1; y < hy; y++)
                {
                    int v = YardOcc(s, ix + c, y);
                    if (v != 0 && v != skip)
                    {
                        land = Math.Min(land, y - 1 - top);
                        break;
                    }
                }
            }
            return Math.Max(land, iy);
        }

        // --- shadow -------------------------------------------------------------------------------------------------------

        /// <summary>GDD K-18: correctness on easy/normal levels only, neutral whenever the landing touches an unrevealed `?` cell.</summary>
        public static ShadowInfo ShadowInfo(FallResult fall, string difficulty)
        {
            bool breaks = fall.Effect.Kind == LandingEffect.KindBreak;
            bool neutral = difficulty == "hard" || difficulty == "superhard" || fall.TouchesHidden;
            if (!neutral)
            {
                return new ShadowInfo(fall.Verdict.Ok ? Core.ShadowInfo.ToneCorrect : Core.ShadowInfo.ToneWrong, fall.Verdict.Reasons, fall.Verdict.MissingSupport, breaks);
            }
            bool support = Array.IndexOf(fall.Verdict.Reasons, VerdictReasons.Support) >= 0;
            return new ShadowInfo(Core.ShadowInfo.ToneNeutral, support ? new[] { VerdictReasons.Support } : new string[0],
                support ? fall.Verdict.MissingSupport : new At[0], breaks);
        }

        // --- yard gravity (K-35 step 6) -------------------------------------------------------------------------------------

        private sealed class Ent
        {
            public readonly string Kind;
            public readonly int Id;
            public readonly ShapeDef? Shape;
            public int X;
            public int Y;
            public readonly bool Falls;
            public readonly bool Rises;
            public bool Flag;

            public Ent(string kind, int id, ShapeDef? shape, int x, int y, bool falls, bool rises)
            {
                Kind = kind;
                Id = id;
                Shape = shape;
                X = x;
                Y = y;
                Falls = falls;
                Rises = rises;
            }
        }

        private sealed class FirstRec
        {
            public readonly string Kind;
            public readonly int Id;
            public readonly Anchor From;
            public Anchor To;

            public FirstRec(string kind, int id, Anchor from, Anchor to)
            {
                Kind = kind;
                Id = id;
                From = from;
                To = to;
            }
        }

        private readonly struct Fell
        {
            public readonly Anchor Start;
            public readonly FallenBlock Block;

            public Fell(Anchor start, FallenBlock block)
            {
                Start = start;
                Block = block;
            }
        }

        /// <summary>
        /// `settleYard(state, hooks)` — K-35 step 6 (TECH §5.3, GDD K-20, E-33). Mutates the state; returns one motion per
        /// entity that moved, in (y, x) order of the start anchors.
        /// </summary>
        public static YardMotion[] SettleYard(GameState s, SettleHooks? hooks = null)
        {
            hooks ??= SettleHooks.Default;
            var first = new Dictionary<string, FirstRec>();
            var firstOrder = new List<string>();
            for (int pass = 0; pass < MaxSettlePasses; pass++)
            {
                var ents = YardEntities(s);
                if (ents.Count == 0) break;
                var byValue = new Dictionary<int, Ent>();
                foreach (var e in ents) byValue[e.Kind == YardMotion.KindPiece ? e.Id + 1 : -(e.Id + 1)] = e;
                var passStart = new Dictionary<Ent, BoardCell[]>();
                foreach (var e in ents) if (e.Kind == YardMotion.KindPiece && e.Falls) passStart[e] = EntCells(e);
                var startAnchor = new Dictionary<Ent, Anchor>();
                foreach (var e in ents) startAnchor[e] = new Anchor(e.X, e.Y);

                for (int guard = 0; guard < 4 * s.Lvl.Geo.Rows * (ents.Count + 1); guard++)
                {
                    bool fellHalf = HalfStep(s, ents, byValue, -1);
                    bool roseHalf = HalfStep(s, ents, byValue, 1);
                    if (!fellHalf && !roseHalf) break;
                }

                var fell = new List<Fell>();
                foreach (var e in ents)
                {
                    var start = startAnchor[e];
                    if (start.Ix == e.X && start.Iy == e.Y) continue;
                    string key = e.Kind + ":" + e.Id;
                    if (first.TryGetValue(key, out var rec)) rec.To = new Anchor(e.X, e.Y);
                    else
                    {
                        first[key] = new FirstRec(e.Kind, e.Id, start, new Anchor(e.X, e.Y));
                        firstOrder.Add(key);
                    }
                    if (passStart.TryGetValue(e, out var cells)) fell.Add(new Fell(start, new FallenBlock(e.Id, cells)));
                }
                if (fell.Count == 0 || hooks.OnFallen == null) break;
                fell.Sort((a, b) =>
                {
                    int c = a.Start.Iy.CompareTo(b.Start.Iy);
                    if (c != 0) return c;
                    c = a.Start.Ix.CompareTo(b.Start.Ix);
                    if (c != 0) return c;
                    return a.Block.PieceId.CompareTo(b.Block.PieceId);
                });
                var blocks = new FallenBlock[fell.Count];
                for (int i = 0; i < blocks.Length; i++) blocks[i] = fell[i].Block;
                if (!hooks.OnFallen(s, blocks)) break;
            }
            var o = new List<YardMotion>();
            foreach (var key in firstOrder)
            {
                var m = first[key];
                if (m.From.Ix == m.To.Ix && m.From.Iy == m.To.Iy) continue;
                o.Add(new YardMotion(m.Kind, m.Id, m.From, m.To, m.To.Iy - m.From.Iy));
            }
            o.Sort((a, b) =>
            {
                int c = a.From.Iy.CompareTo(b.From.Iy);
                if (c != 0) return c;
                c = a.From.Ix.CompareTo(b.From.Ix);
                if (c != 0) return c;
                if (a.Kind != b.Kind) return a.Kind == YardMotion.KindPiece ? -1 : 1;
                return a.Id.CompareTo(b.Id);
            });
            return o.ToArray();
        }

        /// <summary>Movable yard entities: bags always fall; with Y6 blocks fall and balloons rise; anything else is solid.</summary>
        private static List<Ent> YardEntities(GameState s)
        {
            bool yardOn = s.Lvl.Gravity.Yard;
            var ents = new List<Ent>();
            // v3 Y13 (GDD §16 K-61): a `slippery` block falls even without Y6; a covered pallet layer (K-58) is never an entity
            bool slippery = false;
            if (!yardOn)
                foreach (var cp in s.Lvl.Pieces) if ((cp.Flags & FlagBit.Slippery) != 0) { slippery = true; break; }
            if (yardOn || slippery)
            {
                int P = s.Lvl.Layout.Counts.Pieces;
                for (int id = 0; id < P; id++)
                {
                    if (PieceZone(s, id) != Zone.Yard) continue;
                    if (!yardOn && (PieceFlagBits(s, id) & FlagBit.Slippery) == 0) continue;
                    if (s.Lvl.HasStacks && Grid.IsCovered(s, id)) continue;
                    bool balloon = (PieceFlagBits(s, id) & FlagBit.Balloon) != 0;
                    ents.Add(new Ent(YardMotion.KindPiece, id, Shapes.ShapeByIndex(PieceShape(s, id)), PieceX(s, id), PieceY(s, id), !balloon, balloon));
                }
            }
            foreach (var o in s.Lvl.Obstacles)
            {
                if (!o.IsCementBag || ObstacleField(s, o.Index, OF.Hp) <= 0) continue;
                for (int y = 0; y < s.Lvl.Geo.Hy; y++)
                {
                    if (YardOcc(s, o.X, y) == -(o.Index + 1))
                    {
                        ents.Add(new Ent(YardMotion.KindBag, o.Index, null, o.X, y, true, false));
                        break;
                    }
                }
            }
            return ents;
        }

        private static BoardCell[] EntCells(Ent e) => e.Shape != null ? Movement.BlockCells(e.Shape, e.X, e.Y) : new[] { new BoardCell(e.X, e.Y) };

        /// <summary>
        /// One half step. dir −1 (falling half): every unsupported faller moves down 1; support = the floor, a solid cell
        /// (crate, a block that does not fall, a balloon) or a supported faller (fixed point). dir +1 (rising half): every
        /// unblocked balloon moves up 1; blocked = the yard ceiling (y = hy − 1, default 7), a solid cell or a blocked balloon.
        /// Returns whether anything moved.
        /// </summary>
        private static bool HalfStep(GameState s, List<Ent> ents, Dictionary<int, Ent> byValue, int dir)
        {
            var movers = new List<Ent>();
            foreach (var e in ents) if (dir < 0 ? e.Falls : e.Rises) movers.Add(e);
            if (movers.Count == 0) return false;
            foreach (var e in movers) e.Flag = false; // supported (fall) / blocked (rise)
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var e in movers)
                {
                    if (e.Flag) continue;
                    if (Stopped(s, e, byValue, dir))
                    {
                        e.Flag = true;
                        changed = true;
                    }
                }
            }
            var moving = new List<Ent>();
            foreach (var e in movers) if (!e.Flag) moving.Add(e);
            if (moving.Count == 0) return false;
            foreach (var e in moving) ClearEnt(s, e);
            foreach (var e in moving)
            {
                e.Y += dir;
                if (e.Kind == YardMotion.KindPiece) SetPieceY(s, e.Id, e.Y);
                WriteEnt(s, e);
            }
            return true;
        }

        /// <summary>The entity cannot move in `dir` this half step (see `HalfStep`).</summary>
        private static bool Stopped(GameState s, Ent e, Dictionary<int, Ent> byValue, int dir)
        {
            int self = e.Kind == YardMotion.KindPiece ? e.Id + 1 : -(e.Id + 1);
            int hy = s.Lvl.Geo.Hy;
            foreach (var c in EntCells(e))
            {
                int ny = c.Y + dir;
                if (ny < 0 || ny >= hy) return true;
                int v = YardOcc(s, c.X, ny);
                if (v == 0 || v == self) continue;
                if (!byValue.TryGetValue(v, out var o)) return true; // crate or a block that does not move
                if (dir < 0 ? !o.Falls : !o.Rises) return true; // solid in this half
                if (o.Flag) return true; // a supported faller / blocked balloon
            }
            return false;
        }

        private static void ClearEnt(GameState s, Ent e)
        {
            if (e.Kind == YardMotion.KindPiece) Grid.VacatePiece(s, e.Id);
            else SetYardOcc(s, e.X, e.Y, 0);
        }

        private static void WriteEnt(GameState s, Ent e)
        {
            if (e.Kind == YardMotion.KindPiece) Grid.OccupyPiece(s, e.Id);
            else SetYardOcc(s, e.X, e.Y, -(e.Id + 1));
        }

        // --- internals ----------------------------------------------------------------------------------------------------

        private static void PushPoint(List<Anchor> path, int ix, int iy)
        {
            if (path.Count == 0) path.Add(new Anchor(ix, iy));
            else
            {
                var last = path[path.Count - 1];
                if (last.Ix != ix || last.Iy != iy) path.Add(new Anchor(ix, iy));
            }
        }

        /// <summary>Balloon ceiling on the site (S8, S-9): the block's top cell hangs at board row `h + e − 1` of the visible plan.</summary>
        private static int CeilingAnchor(GameState s, ShapeDef shape)
        {
            int seg = Grid.VisibleSegment(s);
            int height = seg >= 0 && seg < s.Lvl.Segments.Length ? s.Lvl.Segments[seg].Height : 0;
            return height + Hdr(s, H.Elev) - shape.H;
        }

        /// <summary>Every cell on the site columns inside the frame rows and not blocked.</summary>
        private static bool FitsOnSite(BoardGeo geo, int[] cols, ShapeDef shape, int ix, int iy)
        {
            if (ix < geo.SiteX || ix + shape.W > geo.Cols || iy < 0 || iy + shape.H > geo.Rows) return false;
            for (int c = 0; c < shape.W; c++)
            {
                if (((shape.ColRows[c] << iy) & cols[ix + c - geo.SiteX]) != 0) return false;
            }
            return true;
        }

        /// <summary>K-11 open sky: nothing blocked at or above the block's lowest cell in each covered column.</summary>
        private static bool OpenSky(BoardGeo geo, int[] cols, ShapeDef shape, int ix, int iy)
        {
            for (int c = 0; c < shape.W; c++)
            {
                if ((cols[ix + c - geo.SiteX] >> (iy + shape.ColBottom[c])) != 0) return false;
            }
            return true;
        }

        /// <summary>Anchor row after falling from `(ix, iy)`: onto the highest blocked row below the block in each covered column.</summary>
        private static int FallFrom(BoardGeo geo, int[] cols, ShapeDef shape, int ix, int iy)
        {
            int land = 0;
            for (int c = 0; c < shape.W; c++)
            {
                int bottom = shape.ColBottom[c];
                int below = cols[ix + c - geo.SiteX] & ((1 << (iy + bottom)) - 1);
                int support = below == 0 ? -1 : Bits.HighestBit(below);
                land = Math.Max(land, support + 1 - bottom);
            }
            return land;
        }

        /// <summary>Anchor row after rising from `(ix, iy)` up to `cap`: under the lowest blocked row above the block.</summary>
        private static int RiseFrom(BoardGeo geo, int[] cols, ShapeDef shape, int ix, int iy, int cap)
        {
            int land = cap;
            for (int c = 0; c < shape.W; c++)
            {
                int top = shape.ColTop[c];
                int above = cols[ix + c - geo.SiteX] >> (iy + top + 1);
                if (above == 0) continue;
                int firstRow = Bits.LowestBit(above) + iy + top + 1;
                land = Math.Min(land, firstRow - 1 - top);
            }
            return Math.Max(land, iy);
        }

        /// <summary>A cell is an unrevealed `?` plan cell of the visible segment (K-18, K-32).</summary>
        private static bool TouchesHiddenCell(GameState s, BoardCell[] cells)
        {
            int seg = Grid.VisibleSegment(s);
            if (seg < 0 || seg >= s.Lvl.Segments.Length) return false;
            var plan = s.Lvl.Segments[seg];
            if (plan.HiddenMask == 0) return false;
            int hidden = plan.HiddenMask & ~RevealedMask(s, seg);
            var geo = s.Lvl.Geo;
            int siteX = geo.SiteX;
            int ws = geo.Ws;
            int hs = geo.Hs;
            int elev = Hdr(s, H.Elev);
            foreach (var c in cells)
            {
                int sx = c.X - siteX;
                int sy = c.Y - elev;
                if (sx < 0 || sx >= ws || sy < 0 || sy >= hs) continue;
                if (((hidden >> (sy * ws + sx)) & 1) != 0) return true;
            }
            return false;
        }
    }

    /// <summary>32-bit bit helpers standing in for `Math.clz32` (TS) — netstandard2.1 has no BitOperations.</summary>
    public static class Bits
    {
        /// <summary>`Math.clz32(x)`: leading zero count of the 32-bit pattern (32 for 0).</summary>
        public static int Clz32(int x)
        {
            uint u = unchecked((uint)x);
            if (u == 0) return 32;
            int n = 0;
            while ((u & 0x80000000u) == 0)
            {
                u <<= 1;
                n++;
            }
            return n;
        }

        /// <summary>`31 - Math.clz32(m)`: index of the highest set bit (−1 for 0).</summary>
        public static int HighestBit(int m) => 31 - Clz32(m);

        /// <summary>`31 - Math.clz32(m &amp; -m)`: index of the lowest set bit (−1 for 0).</summary>
        public static int LowestBit(int m) => 31 - Clz32(m & -m);
    }
}
