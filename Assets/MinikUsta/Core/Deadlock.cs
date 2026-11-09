#nullable enable
// Port of src/core/deadlock.ts (web repo, source of truth). The D3a memo arithmetic is 32-bit, bit for bit the TS code
// (Math.imul → unchecked int multiply, `>>>` → uint shift, `| 0` → int cast); the exact numeric memo key uses a long
// where TS uses a double below 2^52 (same values).
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>D3a result: a tiling exists / none exists / the budget ran out (never counted as dead). TS `TileResult`.</summary>
    public static class TileResults
    {
        public const string Ok = "ok";
        public const string Dead = "dead";
        public const string Unknown = "unknown";
    }

    /// <summary>The solver's dead-state table of a level (TECH §2R.4 D3b; export and loader: Faz 3, cut 1).</summary>
    public interface IDeadTable
    {
        /// <summary>The solver explored the whole space.</summary>
        bool Complete { get; }
        /// <summary>`lo` / `hi` = the state's 64-bit Zobrist lanes (`Hash.HashState` [0] / [1]).</summary>
        bool Has(uint lo, uint hi);
    }

    /// <summary>`src/generated/deadlock/level_NNN.json` (TECH §2R.4).</summary>
    public sealed class DeadTableJson
    {
        public readonly string LevelHash;
        public readonly int RulesVersion;
        public readonly int? SolverVersion;
        public readonly bool Complete;
        /// <summary>Sorted 64-bit values (`hi · 2³² + lo`, unsigned), each as 16 hex digits (hi first).</summary>
        public readonly string[] Entries;

        public DeadTableJson(string levelHash, int rulesVersion, bool complete, string[] entries, int? solverVersion = null)
        {
            LevelHash = levelHash;
            RulesVersion = rulesVersion;
            Complete = complete;
            Entries = entries;
            SolverVersion = solverVersion;
        }
    }

    /// <summary>Options of one step 12 run (TS `Step12Options`).</summary>
    public sealed class Step12Options
    {
        /// <summary>
        /// The buffer before the action (Söküm target). For an accepted +5 offer: the start buffer of the last action before
        /// the window (K-29, D1 Söküm); null = no Söküm possible (step 12 off, or no earlier action).
        /// </summary>
        public readonly int[]? PreAction;
        /// <summary>Run D3a: the action placed correctly, delivered, used a booster or the Golden Trowel.</summary>
        public readonly bool Tiling;
        /// <summary>The counter is 0: D1 does not run (K-30, E-58).</summary>
        public readonly bool CounterZero;
        /// <summary>D3b table; null in Faz 2R (cut 1).</summary>
        public readonly IDeadTable? Table;
        /// <summary>An accepted +5 offer (K-29, E-42): D1 once, nothing else.</summary>
        public readonly bool AfterOffer;

        public Step12Options(int[]? preAction, bool tiling, bool counterZero, IDeadTable? table, bool afterOffer = false)
        {
            PreAction = preAction;
            Tiling = tiling;
            CounterZero = counterZero;
            Table = table;
            AfterOffer = afterOffer;
        }
    }

    /// <summary>
    /// Counters of the last `TileRemaining` call (bench, `levels:solve` `d3aMaxExpansions`). TS `D3A_STATS`. Per thread
    /// (REVIEW_PHASE2 R-07): the TS module-level object is safe only because JS has one thread; here two threads calling
    /// `TileRemaining` at once (solver, bot, background hint) each read their own counter.
    /// </summary>
    public static class D3aStats
    {
        [ThreadStatic] private static int expansions;

        /// <summary>Expansions of the last `TileRemaining` call on the calling thread (0 before the first call).</summary>
        public static int Expansions
        {
            get => expansions;
            internal set => expansions = value;
        }
    }

    /// <summary>
    /// Deadlock check and truck help — K-35 step 12 (docs/GDD.md K-30, K-29, K-35, K-55 item 8; TECH_DESIGN §2R.4, §D99).
    /// Pure: no clock, no DOM, no randomness.
    ///
    /// - D1 `noMoves`: no block can be held by K-09 (a)–(d) (the counter condition (e) is ignored). Help: rules lift chains
    ///   and wetness (`OnTruckHelp`); still D1 → Söküm with cause `access` (`RulesVersion` 3, D-099: the yard is never
    ///   re-laid; the Faz 2R seeded reshuffle was removed — every `RulesVersion` ≤ 2 attempt is voided on load, K-43 item 4).
    /// - D2 `color`: per colour, remaining supply ≠ remaining demand (K-47). Skipped on W6 levels (CL-2R-04).
    /// - D3a `tiling` (`TileRemaining`): exact cover of the remaining plan cells by the remaining material blocks, bottom-up
    ///   per column (K-34), segments in order, batch availability, W6 joker; access ignored. Budget = expansions
    ///   (`D3aMaxExpansions`), never time: an exhausted budget is `unknown`, never dead.
    /// - D3b `access`: the solver's dead-state table (`IDeadTable`). The table export / loader is cut 1 (Faz 3); the lookup
    ///   is here and the game passes `null` until then.
    /// - Söküm (teardown): D2 / D3 → the buffer goes back to the action's start (`preAction`), the counter and `movesSpent`
    ///   keep their post-action values, the streak `c = 0`; `teardown` is the action's last event.
    ///
    /// Levels whose data is not a full cover (K-47 broken: old Faz 2 data, hand-made test boards) get D1 only: D2 / D3 are
    /// defined for full-cover data, the validator never loads anything else (L-30 `cover_mismatch`). D1's Söküm runs on
    /// every level (it only needs the action's start buffer).
    ///
    /// D3a algorithm note (TECH §2R.4 item 2, implementation): §2R.4 picks the lowest column and only tries blocks covering
    /// its next cell. That misses tilings where the block over that cell must wait for a neighbour column (C3 overhang over
    /// a B1). This search branches on EVERY block that sits on the current column heights (K-34: each column bottom on its
    /// column height); any order of a K-34 build reaches the same state, and the numeric memo of dead states (segment,
    /// profile, class counts) merges the orders, so every state is expanded once. Complete and deterministic.
    /// </summary>
    public static class Deadlock
    {
        /// <summary>ANALYTICS v6 `deadlock_teardown.cause` of a teardown cause (TECH §2R.16).</summary>
        public static string AnalyticsCause(string cause) => cause == TeardownCauses.Color ? "color_balance" : cause;

        /// <summary>GDD K-30 D3a: the expansion budget (the single policy number, TECH §2R.0 item 6).</summary>
        public const int D3aMaxExpansions = 20_000;

        // --- level-level facts ---------------------------------------------------------------------------------------------

        private static readonly ConditionalWeakTable<CompiledLevel, StrongBox<bool>> FullCover = new ConditionalWeakTable<CompiledLevel, StrongBox<bool>>();

        /// <summary>W6 paint gates make colour supply ≠ demand by design (K-47 item 1): D2 is skipped (CL-2R-04), D3a uses jokers.</summary>
        public static bool HasPaintGates(CompiledLevel lvl)
        {
            foreach (var g in lvl.Gaps) if (g.Type == "paint") return true;
            return false;
        }

        /// <summary>
        /// The level data is a full cover (K-47 item 1): per colour supply = demand, on W6 levels the cell totals. D2 / D3 run
        /// only then (see the class comment).
        /// </summary>
        public static bool IsFullCover(CompiledLevel lvl)
        {
            if (FullCover.TryGetValue(lvl, out var box)) return box.Value;
            bool v;
            if (HasPaintGates(lvl))
            {
                int supply = 0;
                int demand = 0;
                foreach (int n in lvl.Supply) supply += n;
                foreach (int n in lvl.Demand) demand += n;
                v = supply == demand;
            }
            else
            {
                v = true;
                for (int c = 0; c < lvl.Supply.Length; c++)
                    if (lvl.Supply[c] != (c < lvl.Demand.Length ? lvl.Demand[c] : 0)) v = false;
            }
            FullCover.Add(lvl, new StrongBox<bool>(v));
            return v;
        }

        // --- D1 ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Cheap sufficient test of K-09 (a)–(d) for a yard block (µs, no BFS): unlocked, allowed by the rules and free to move
        /// one cell up (every column's top cell has an empty cell above it, inside the frame; Ağır Yük inside the yard). A
        /// vertical step inside the yard columns never crosses the wall boundary or the site's open sky, so that node is valid.
        /// </summary>
        public static bool QuickHoldable(GameState s, int id, DragRules? rules = null)
        {
            if (PieceZone(s, id) != Zone.Yard || (PieceFlagBits(s, id) & FlagBit.Locked) != 0) return false;
            if (s.Lvl.HasStacks && (Grid.IsCovered(s, id) || Grid.LayerBelow(s, id) >= 0)) return false;
            if (rules?.CanPick != null && !rules.CanPick(s, id)) return false;
            var shape = Shapes.ShapeByIndex(PieceShape(s, id));
            int x0 = PieceX(s, id);
            int y0 = PieceY(s, id);
            if (y0 + shape.H + 1 > s.Lvl.Geo.Hy) return false;
            for (int c = 0; c < shape.W; c++)
            {
                int v = YardOcc(s, x0 + c, y0 + shape.ColTop[c] + 1);
                if (v != 0 && v != id + 1) return false;
            }
            return true;
        }

        /// <summary>K-09 (a)–(d) for one piece; (e) the counter is ignored (D1).</summary>
        public static bool HoldableIgnoringMoves(GameState s, int id, DragRules? rules = null)
        {
            if (QuickHoldable(s, id, rules)) return true;
            int left = Hdr(s, H.MovesLeft);
            if (left > 0) return HasRelease(s, id, rules);
            SetHdr(s, H.MovesLeft, 1);
            try
            {
                return HasRelease(s, id, rules);
            }
            finally
            {
                SetHdr(s, H.MovesLeft, left);
            }
        }

        /// <summary>The block can be held and some node of R is a non-cancel release (K-07 rows 2, 6, 7). v3 D1 widening.</summary>
        private static bool HasRelease(GameState s, int id, DragRules? rules)
        {
            var attempt = Movement.TryBeginDrag(s, id, rules);
            if (!attempt.Ok) return false;
            var session = attempt.Session!;
            foreach (var n in session.ReachableNodes()) if (session.Classify(n).Kind != DropKinds.Cancel) return true;
            return false;
        }

        /// <summary>D1 (K-30): no block on the board can be held by K-09 (a)–(d).</summary>
        public static bool NoMoves(GameState s, DragRules? rules = null)
        {
            int P = s.Lvl.Layout.Counts.Pieces;
            int seg = Grid.VisibleSegment(s);
            for (int id = 0; id < P; id++)
            {
                int zone = PieceZone(s, id);
                if (zone == Zone.Yard || (zone == Zone.Site && PieceSeg(s, id) == seg))
                {
                    if (HoldableIgnoringMoves(s, id, rules)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// One drag move gives a correct placement now (K-34 hook 5 reach, TECH §2R.15 summary): some holdable yard block has a
        /// reachable site release whose landing is correct (K-16 + K-34) and does not break (S3). The counter must be &gt; 0.
        /// </summary>
        public static bool HasReachableCorrect(GameState s, int id, DragRules? drag = null, FallRules? fall = null)
        {
            var attempt = Movement.TryBeginDrag(s, id, drag);
            if (!attempt.Ok) return false;
            var session = attempt.Session!;
            int siteX = s.Lvl.Geo.SiteX;
            // without fall rules (no wind, no balloon, no glass) every FREE release of a column lands the same: test the lowest
            var lowest = new List<KeyValuePair<int, int>>();
            int IndexOfColumn(int ix)
            {
                for (int i = 0; i < lowest.Count; i++) if (lowest[i].Key == ix) return i;
                return -1;
            }
            foreach (var n in session.ReachableNodes())
            {
                if (n.Ix < siteX) continue;
                var drop = session.Classify(n);
                if (drop.Kind == DropKinds.SiteRail)
                {
                    var f = Gravity.ComputeFall(s, id, n, new FallOptions(null, fall));
                    if (f.Verdict.Ok && f.Effect.Kind == LandingEffect.KindNone) return true;
                }
                else if (drop.Kind == DropKinds.SiteFree)
                {
                    if (fall != null)
                    {
                        var f = Gravity.ComputeFall(s, id, n, new FallOptions(null, fall));
                        if (f.Verdict.Ok && f.Effect.Kind == LandingEffect.KindNone) return true;
                    }
                    else
                    {
                        int i = IndexOfColumn(n.Ix);
                        if (i < 0) lowest.Add(new KeyValuePair<int, int>(n.Ix, n.Iy));
                        else if (n.Iy < lowest[i].Value) lowest[i] = new KeyValuePair<int, int>(n.Ix, n.Iy);
                    }
                }
            }
            foreach (var kv in lowest)
            {
                var f = Gravity.ComputeFall(s, id, new DragNode(kv.Key, kv.Value, DragMode.Free));
                if (f.Verdict.Ok) return true;
            }
            return false;
        }

        // --- correct spots (K-16 + K-34 without access): K-33 P, K-34 hook 5 `neededNow`, K-37 (b) -------------------------

        /// <summary>
        /// Column heights of segment `seg` (K-34): correctly filled rows from the bottom, empty `.` cells counted as filled.
        /// `out[sx]` = local column `sx`.
        /// </summary>
        public static int[] ColumnHeights(GameState s, int seg, int[]? @out = null)
        {
            int ws = s.Lvl.Geo.Ws;
            int hs = s.Lvl.Geo.Hs;
            @out ??= new int[ws];
            var plan = seg >= 0 && seg < s.Lvl.Segments.Length ? s.Lvl.Segments[seg] : null;
            for (int sx = 0; sx < ws; sx++)
            {
                int filled = FilledMask(s, seg, sx);
                int dot = plan != null && sx < plan.DotMask.Length ? plan.DotMask[sx] : 0;
                int h = 0;
                while (h < hs && (((filled | dot) >> h) & 1) == 1) h++;
                @out[sx] = h;
            }
            return @out;
        }

        /// <summary>
        /// Every anchor (board coordinates, visible segment) where a block of `shape` and colour `color` is a correct placement
        /// now (K-16 + K-34) on empty cells, access, wall and gaps ignored: each covered column's bottom cell on the column
        /// height, every cell a plan cell of the block's colour (`?` resolved). `exclude` = a piece whose own cells count as
        /// empty (a debris / stuck block lifted by the crane). Left to right. Empty when every segment is complete.
        /// </summary>
        public static Anchor[] CorrectSpots(GameState s, ShapeDef shape, int color, int exclude = -1, int[]? cellColors = null)
        {
            var o = new List<Anchor>();
            var lvl = s.Lvl;
            if (color < 0 || Hdr(s, H.DeliveryCursor) >= lvl.Segments.Length) return o.ToArray();
            int ws = lvl.Geo.Ws;
            int hs = lvl.Geo.Hs;
            int siteX = lvl.Geo.SiteX;
            int seg = Grid.VisibleSegment(s);
            if (seg < 0 || seg >= lvl.Segments.Length || shape.W > ws) return o.ToArray();
            var plan = lvl.Segments[seg];
            var h = ColumnHeights(s, seg);
            int elev = Hdr(s, H.Elev);
            int self = exclude + 1;
            for (int x0 = 0; x0 + shape.W <= ws; x0++)
            {
                int y0 = h[x0] - shape.ColBottom[0];
                bool ok = y0 >= 0;
                for (int j = 0; j < shape.W && ok; j++)
                {
                    if (y0 + shape.ColBottom[j] != h[x0 + j] || y0 + shape.ColTop[j] >= hs) ok = false;
                }
                for (int i = 0; i < shape.Cells.Length && ok; i++)
                {
                    var c = shape.Cells[i];
                    int sx = x0 + c.X;
                    int sy = y0 + c.Y;
                    int idx = sy * ws + sx;
                    int planColor = idx >= 0 && idx < plan.PlanColors.Length ? plan.PlanColors[idx] : -2;
                    // v3 Y9 (K-57): a two-tone block needs each cell's own colour
                    int want = cellColors != null && i < cellColors.Length ? cellColors[i] : color;
                    if (planColor != want) ok = false;
                    else
                    {
                        int v = SiteOcc(s, seg, sx, sy);
                        if (v != 0 && v != self) ok = false;
                    }
                }
                if (ok) o.Add(new Anchor(siteX + x0, y0 + elev));
            }
            return o.ToArray();
        }

        /// <summary>
        /// GDD K-34 hook 5 `neededNow`: yard material blocks that are not locked and have at least one correct spot on the
        /// active (front) segment, access ignored (K-50 item 5 now). Queued blocks never count. Piece id order.
        /// </summary>
        public static int[] NeededNow(GameState s)
        {
            var o = new List<int>();
            int P = s.Lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
            {
                if (PieceZone(s, id) != Zone.Yard || !Goals.IsMaterial(s, id)) continue;
                if ((PieceFlagBits(s, id) & FlagBit.Locked) != 0) continue;
                var cc = s.Lvl.Pieces[id].CellColors;
                if (CorrectSpots(s, Shapes.ShapeByIndex(PieceShape(s, id)), PieceColor(s, id), -1, cc).Length > 0) o.Add(id);
            }
            return o.ToArray();
        }

        // --- D2 ------------------------------------------------------------------------------------------------------------

        /// <summary>D2 (K-30): the first colour index whose remaining supply ≠ remaining demand; −1 when balanced.</summary>
        public static int ColorImbalance(GameState s)
        {
            var sup = Goals.RemainingSupplyByColor(s);
            var dem = Goals.RemainingDemandByColor(s);
            for (int c = 0; c < ColorCodes.All.Length; c++) if (sup[c] != dem[c]) return c;
            return -1;
        }

        // --- D3a -----------------------------------------------------------------------------------------------------------

        private const int MemoSlots = 4096;
        private const int MemoMask = MemoSlots - 1;
        /// <summary>Inserts stop at 3/4 load: the memo only prunes, so a full table costs time, never correctness.</summary>
        private const int MemoLimit = (MemoSlots * 3) >> 2;

        /// <summary>
        /// The D3a "failed state" table: open addressing, `Gen[i] == Generation` marks a live slot (a new search bumps the
        /// generation instead of clearing). TS keeps it in module variables (`memoLo/memoHi/memoGen/memoGeneration`); one per
        /// thread here (REVIEW_PHASE2 R-07) so parallel `TileRemaining` calls never read each other's marks. Allocated once per
        /// thread on first use (48 KB), not per call.
        /// </summary>
        private sealed class D3aMemo
        {
            public readonly int[] Lo = new int[MemoSlots];
            public readonly int[] Hi = new int[MemoSlots];
            public readonly int[] Gen = new int[MemoSlots];
            public int Generation;
        }

        [ThreadStatic] private static D3aMemo? threadMemo;

        private static D3aMemo MemoOfThisThread() => threadMemo ??= new D3aMemo();
        private const long Two32 = 4294967296L;
        private const double ExactLimit = 4503599627370496.0; // 2^52

        private sealed class BudgetExhausted : Exception
        {
            public BudgetExhausted() : base("d3a budget") { }
        }

        private sealed class TileClass
        {
            public readonly ShapeDef Shape;
            public readonly int ColorMask;
            /// <summary>v3 Y9 (K-57): colour bit per shape cell of a two-tone class (no W6 joker); null = `ColorMask` for every cell.</summary>
            public readonly int[]? CellMasks;
            /// <summary>Segment index from which the class may be used (batch availability).</summary>
            public readonly int From;
            public int Count;

            public TileClass(ShapeDef shape, int colorMask, int[]? cellMasks, int from)
            {
                Shape = shape;
                ColorMask = colorMask;
                CellMasks = cellMasks;
                From = from;
                Count = 1;
            }
        }

        private sealed class PaintJoker
        {
            public readonly int Size;
            public readonly int Color;

            public PaintJoker(int size, int color)
            {
                Size = size;
                Color = color;
            }
        }

        /// <summary>
        /// `tileRemaining(state, budget)` — GDD K-30 D3a (TECH §2R.4): can the remaining material blocks exactly cover every
        /// remaining plan cell, building each column bottom-up (K-34), segments in order (`segments`; `carousel`: every
        /// incomplete segment, same pool), undelivered batch k only on segments ≥ its `forSegment`, W6 jokers? Access ignored.
        /// </summary>
        public static string TileRemaining(GameState s, int budget = D3aMaxExpansions)
        {
            var lvl = s.Lvl;
            int ws = lvl.Geo.Ws;
            int hs = lvl.Geo.Hs;
            int segCells = lvl.Geo.SegCells;
            D3aStats.Expansions = 0;
            var memo = MemoOfThisThread();

            // segments to fill
            List<int> segs = new List<int>();
            if (lvl.Mode == "carousel")
            {
                for (int k = 0; k < lvl.Segments.Length; k++) if (!SegmentFull(s, k)) segs.Add(k);
            }
            else
            {
                for (int k = Hdr(s, H.ActiveSeg); k < lvl.Segments.Length; k++) if (!SegmentFull(s, k)) segs.Add(k);
            }

            // plan colour bits and start profiles
            int[] planBit = new int[Math.Max(1, lvl.Segments.Length * segCells)];
            int[] dotBit = new int[Math.Max(1, lvl.Segments.Length * segCells)];
            // per segment and column: rows up to the top plan cell (a short pre-2R plan leaves outside cells above it)
            int[] limit = new int[Math.Max(1, lvl.Segments.Length * ws)];
            int cells = 0;
            for (int k = 0; k < lvl.Segments.Length; k++)
            {
                var plan = lvl.Segments[k];
                for (int i = 0; i < segCells; i++)
                {
                    int c = i < plan.PlanColors.Length ? plan.PlanColors[i] : -2;
                    if (c >= 0) planBit[k * segCells + i] = 1 << c;
                    else if (c == Compiler.PlanDot) dotBit[k * segCells + i] = 1;
                    if (c >= 0 || c == Compiler.PlanDot)
                    {
                        int x = i % ws;
                        limit[k * ws + x] = Math.Max(limit[k * ws + x], i / ws + 1);
                    }
                }
            }
            List<int[]> startProfile = new List<int[]>();
            foreach (int k in segs)
            {
                var prof0 = new int[ws];
                for (int sx = 0; sx < ws; sx++)
                {
                    int filled = FilledMask(s, k, sx);
                    int h = 0;
                    while (h < hs && (((filled >> h) & 1) == 1 || dotBit[k * segCells + h * ws + sx] == 1)) h++;
                    prof0[sx] = h;
                    for (int y = h; y < hs; y++)
                    {
                        int i = k * segCells + y * ws + sx;
                        if (planBit[i] != 0 && ((filled >> y) & 1) == 0) cells++;
                    }
                }
                startProfile.Add(prof0);
            }

            // classes (shape, colour set, availability) of the remaining material blocks
            var jokers = PaintJokers(lvl);
            var byKey = new Dictionary<string, TileClass>();
            var classList = new List<TileClass>();
            int supplyCells = 0;
            int P = lvl.Layout.Counts.Pieces;
            for (int id = 0; id < P; id++)
            {
                if (!Goals.IsRemainingSupply(s, id)) continue;
                var shape = Shapes.ShapeByIndex(PieceShape(s, id));
                int color = PieceColor(s, id);
                var cc = lvl.Pieces[id].CellColors;
                int mask = color >= 0 ? 1 << color : 0;
                if (cc == null) foreach (var j in jokers) if (shape.H <= j.Size) mask |= 1 << j.Color;
                int[]? cellMasks = null;
                if (cc != null)
                {
                    cellMasks = new int[shape.Cells.Length];
                    for (int ci = 0; ci < cellMasks.Length; ci++) cellMasks[ci] = 1 << PieceCellColor(s, id, ci);
                }
                int from = lvl.Mode == "segments" && PieceZone(s, id) == Zone.Pending ? BatchFrom(lvl, id) : 0;
                string key = shape.Index + ":" + mask + ":" + from + ":" + (cellMasks != null ? string.Join(".", cellMasks) : "");
                if (byKey.TryGetValue(key, out var cls)) cls.Count += 1;
                else
                {
                    cls = new TileClass(shape, mask, cellMasks, from);
                    byKey[key] = cls;
                    classList.Add(cls);
                }
                supplyCells += shape.CellCount;
            }
            if (supplyCells != cells) return TileResults.Dead;
            if (cells == 0) return TileResults.Ok;
            classList.Sort((a, b) =>
            {
                int d = a.Shape.Index - b.Shape.Index;
                if (d != 0) return d;
                d = a.ColorMask - b.ColorMask;
                if (d != 0) return d;
                d = a.From - b.From;
                if (d != 0) return d;
                return string.CompareOrdinal(a.CellMasks != null ? string.Join(".", a.CellMasks) : "", b.CellMasks != null ? string.Join(".", b.CellMasks) : "");
            });
            TileClass[] classes = classList.ToArray();

            // numeric memo key radices
            int radixH = hs + 1;
            double profRadix = 1;
            for (int i = 0; i < ws; i++) profRadix *= radixH;
            int[] countRadix = new int[classes.Length];
            for (int i = 0; i < classes.Length; i++) countRadix[i] = classes[i].Count + 1;
            double total = (segs.Count + 1) * profRadix;
            foreach (int r in countRadix) total *= r;
            bool exact = total < ExactLimit;

            int memoGeneration = unchecked(memo.Generation + 1);
            if (memoGeneration == 0)
            {
                Array.Clear(memo.Gen, 0, memo.Gen.Length);
                memoGeneration = 1;
            }
            memo.Generation = memoGeneration;
            int memoUsed = 0;
            int[] memoLo = memo.Lo;
            int[] memoHi = memo.Hi;
            int[] memoGen = memo.Gen;

            int[] prof = new int[ws];
            int segPos = 0;
            int expansions = 0;

            void KeyOf(out int lo, out int hi)
            {
                if (exact)
                {
                    long k = segPos;
                    for (int i = 0; i < ws; i++) k = k * radixH + prof[i];
                    for (int i = 0; i < classes.Length; i++) k = k * countRadix[i] + classes[i].Count;
                    lo = unchecked((int)(uint)(k % Two32));
                    hi = unchecked((int)(k / Two32));
                    return;
                }
                int a = unchecked((int)0x9e3779b9u) ^ segPos;
                int b = unchecked((int)0x85ebca6bu) ^ segPos;
                void Mix(int v)
                {
                    a = unchecked((a ^ v) * 0x01000193);
                    b = unchecked((b ^ (v + 0x6b)) * 0x5bd1e995);
                }
                for (int i = 0; i < ws; i++) Mix(prof[i]);
                foreach (var c in classes) Mix(c.Count);
                lo = a;
                hi = b;
            }
            int SlotOf(int lo, int hi)
            {
                int t = unchecked((lo ^ unchecked(hi * 0x27d4eb2d)) * unchecked((int)0x9e3779b1u));
                return (int)((uint)t >> 20) & MemoMask;
            }
            bool MemoHas(int lo, int hi)
            {
                for (int i = SlotOf(lo, hi), n = 0; n < MemoSlots; i = (i + 1) & MemoMask, n++)
                {
                    if (memoGen[i] != memoGeneration) return false;
                    if (memoLo[i] == lo && memoHi[i] == hi) return true;
                }
                return false;
            }
            void MemoAdd(int lo, int hi)
            {
                if (memoUsed >= MemoLimit) return;
                for (int i = SlotOf(lo, hi), n = 0; n < MemoSlots; i = (i + 1) & MemoMask, n++)
                {
                    if (memoGen[i] != memoGeneration)
                    {
                        memoGen[i] = memoGeneration;
                        memoLo[i] = lo;
                        memoHi[i] = hi;
                        memoUsed++;
                        return;
                    }
                }
            }

            int Seg() => segPos < segs.Count ? segs[segPos] : -1;
            /* Raises column `x` over empty `.` cells (K-34: an empty dot counts as filled). */
            void SkipDots(int x)
            {
                int bse = Seg() * segCells;
                int h = prof[x];
                while (h < hs && dotBit[bse + h * ws + x] == 1) h++;
                prof[x] = h;
            }
            bool SegmentDone()
            {
                int bse = Seg() * ws;
                for (int x = 0; x < ws; x++) if (prof[x] < limit[bse + x]) return false;
                return true;
            }

            bool Solve()
            {
                if (++expansions > budget) throw new BudgetExhausted();
                if (SegmentDone())
                {
                    if (segPos + 1 >= segs.Count)
                    {
                        foreach (var c in classes) if (c.Count != 0) return false;
                        return true;
                    }
                    var savedAll = (int[])prof.Clone();
                    segPos++;
                    Array.Copy(segPos < startProfile.Count ? startProfile[segPos] : savedAll, prof, ws);
                    bool okNext = Solve();
                    segPos--;
                    Array.Copy(savedAll, prof, ws);
                    return okNext;
                }
                KeyOf(out int lo, out int hi);
                if (MemoHas(lo, hi)) return false;
                int k = Seg();
                int bse = k * segCells;
                foreach (var cls in classes)
                {
                    if (cls.Count == 0 || cls.From > k) continue;
                    var sh = cls.Shape;
                    for (int x0 = 0; x0 + sh.W <= ws; x0++)
                    {
                        int y0 = prof[x0] - sh.ColBottom[0];
                        bool fits = true;
                        for (int j = 0; j < sh.W && fits; j++)
                        {
                            if (y0 + sh.ColBottom[j] != prof[x0 + j]) fits = false;
                            else if (y0 + sh.ColTop[j] >= hs) fits = false;
                        }
                        if (!fits) continue;
                        for (int ci = 0; ci < sh.Cells.Length; ci++)
                        {
                            var c = sh.Cells[ci];
                            int idx = bse + (y0 + c.Y) * ws + x0 + c.X;
                            int bit = idx >= 0 && idx < planBit.Length ? planBit[idx] : 0;
                            int want = cls.CellMasks != null ? cls.CellMasks[ci] : cls.ColorMask;
                            if ((bit & want) == 0)
                            {
                                fits = false;
                                break;
                            }
                        }
                        if (!fits) continue;
                        var saved = new int[sh.W];
                        Array.Copy(prof, x0, saved, 0, sh.W);
                        for (int j = 0; j < sh.W; j++)
                        {
                            prof[x0 + j] = y0 + sh.ColTop[j] + 1;
                            SkipDots(x0 + j);
                        }
                        cls.Count--;
                        bool ok = Solve();
                        cls.Count++;
                        Array.Copy(saved, 0, prof, x0, sh.W);
                        if (ok) return true;
                    }
                }
                MemoAdd(lo, hi);
                return false;
            }

            if (segs.Count == 0) return classes.Length == 0 ? TileResults.Ok : TileResults.Dead;
            Array.Copy(startProfile[0], prof, ws);
            try
            {
                return Solve() ? TileResults.Ok : TileResults.Dead;
            }
            catch (BudgetExhausted)
            {
                return TileResults.Unknown;
            }
            finally
            {
                D3aStats.Expansions = Math.Min(expansions, budget);
            }
        }

        private static bool SegmentFull(GameState s, int k)
        {
            if (k < 0 || k >= s.Lvl.Segments.Length) return true;
            var plan = s.Lvl.Segments[k];
            for (int sx = 0; sx < s.Lvl.Geo.Ws; sx++)
            {
                int need = sx < plan.PlanMask.Length ? plan.PlanMask[sx] : 0;
                if ((FilledMask(s, k, sx) & need) != need) return false;
            }
            return true;
        }

        private static int BatchFrom(CompiledLevel lvl, int id)
        {
            if (id < 0 || id >= lvl.Pieces.Length) return 0;
            var p = lvl.Pieces[id];
            if (p.Batch < 1) return 0;
            return p.Batch < lvl.Batches.Length ? lvl.Batches[p.Batch].ForSegment : p.Batch;
        }

        /// <summary>W6 joker colours: a block whose box height ≤ a paint gate's size may take that gate's colour (K-30 D3a).</summary>
        private static List<PaintJoker> PaintJokers(CompiledLevel lvl)
        {
            var o = new List<PaintJoker>();
            foreach (var g in lvl.Gaps)
            {
                if (g.Type != "paint" || g.Color == null) continue;
                o.Add(new PaintJoker(g.Size, ColorCodes.IndexOf(g.Color)));
            }
            return o;
        }

        // --- D3b -----------------------------------------------------------------------------------------------------------

        private sealed class SortedDeadTable : IDeadTable
        {
            private readonly bool complete;
            private readonly uint[] his;
            private readonly uint[] los;

            public SortedDeadTable(bool complete, uint[] his, uint[] los)
            {
                this.complete = complete;
                this.his = his;
                this.los = los;
            }

            public bool Complete => complete;

            public bool Has(uint lo, uint hi)
            {
                int a = 0;
                int b = his.Length - 1;
                while (a <= b)
                {
                    int m = (a + b) >> 1;
                    uint mh = his[m];
                    uint ml = los[m];
                    if (mh == hi && ml == lo) return true;
                    if (mh < hi || (mh == hi && ml < lo)) a = m + 1;
                    else b = m - 1;
                }
                return false;
            }
        }

        /// <summary>
        /// A table for the level (binary search, ≤ 0.05 ms), or null when it does not belong to this level data / rules version
        /// (GDD K-30: an unknown table is ignored; the caller warns in development).
        /// </summary>
        public static IDeadTable? DeadTableFromJson(DeadTableJson json, string expectLevelHash, int expectRulesVersion)
        {
            if (json.LevelHash != expectLevelHash || json.RulesVersion != expectRulesVersion) return null;
            int n = json.Entries.Length;
            var his = new uint[n];
            var los = new uint[n];
            for (int i = 0; i < n; i++)
            {
                string e = json.Entries[i];
                his[i] = Convert.ToUInt32(e.Substring(0, 8), 16);
                los[i] = Convert.ToUInt32(e.Substring(8, 8), 16);
            }
            return new SortedDeadTable(json.Complete, his, los);
        }

        // --- detection -----------------------------------------------------------------------------------------------------

        /// <summary>D2 → D3a → D3b (K-30): the first cause that holds (`TeardownCauses`), or null. Full-cover levels only (else null).</summary>
        public static string? DetectDeadlock(GameState s, IDeadTable? table, bool tiling = true)
        {
            if (!IsFullCover(s.Lvl)) return null;
            if (!HasPaintGates(s.Lvl) && ColorImbalance(s) >= 0) return TeardownCauses.Color;
            if (tiling && TileRemaining(s) == TileResults.Dead) return TeardownCauses.Tiling;
            if (table != null)
            {
                var h = Hash.HashState(s);
                if (table.Has(h[0], h[1])) return TeardownCauses.Access;
            }
            return null;
        }

        /// <summary>K-33 / K-37 / K-38 pre-check (CL-2R-05): the result state of a booster target is not a D3a dead end.</summary>
        public static bool PrecheckOk(GameState s) => !IsFullCover(s.Lvl) || TileRemaining(s) != TileResults.Dead;

        // --- K-35 step 12 ------------------------------------------------------------------------------------------------

        /// <summary>
        /// K-35 step 12 (GDD K-30): counter &gt; 0 → D1 (lift chains / wetness, still D1 → Söküm `access`) → D2 → D3a → D3b;
        /// counter 0 → D2 → D3; after a +5 offer → D1 once (its Söküm goes back to the start of the last action, K-29). D1 / D2
        /// / D3 → Söküm (`teardown`, the action's last event); after a Söküm nothing else runs (the restored state passed its
        /// own step 12). Returns the Söküm cause or null.
        /// </summary>
        public static string? RunStep12(RuleContext ctx, MoveHooks hooks, Step12Options o)
        {
            var s = ctx.S;
            bool helped = false;
            if (!o.CounterZero)
            {
                var d1 = D1Help(ctx, hooks);
                if (d1 == D1Result.Stuck)
                {
                    if (o.PreAction == null) return null;
                    ctx.Emit(Teardown(s, o.PreAction, TeardownCauses.Access));
                    return TeardownCauses.Access;
                }
                helped = d1 == D1Result.Freed;
            }
            // after an offer there is no new action: the state passed D2 / D3 at the end of the action before the window
            if (o.AfterOffer) return null;
            string? cause = DetectDeadlock(s, o.Table, o.Tiling || helped);
            if (cause == null || o.PreAction == null) return null;
            ctx.Emit(Teardown(s, o.PreAction, cause));
            return cause;
        }

        private enum D1Result
        {
            None,
            Freed,
            Stuck,
        }

        /// <summary>
        /// D1 (K-30, `RulesVersion` 3): `None` = some block is holdable; otherwise `deadlockDetected{noMoves}` and the rules
        /// lift chains / wetness (`truckHelp{unchain}`): `Freed` when a block became holdable, `Stuck` when the state is still
        /// D1 (the caller tears the action down, cause `access`; no reshuffle, K-55 item 8).
        /// </summary>
        private static D1Result D1Help(RuleContext ctx, MoveHooks hooks)
        {
            var s = ctx.S;
            if (!NoMoves(s, hooks.Drag)) return D1Result.None;
            ctx.Emit(new DeadlockDetectedEvent(DeadlockReasons.NoMoves));
            if (hooks.OnTruckHelp != null)
            {
                var pre = (int[])s.Buf.Clone();
                var events = new List<GameEvent>();
                var sub = ctx.WithEmit(e => events.Add(e));
                if (hooks.OnTruckHelp(sub))
                {
                    foreach (var e in events) ctx.Emit(e);
                    ctx.Emit(new TruckHelpEvent(TruckHelpKinds.Unchain));
                }
                else Array.Copy(pre, s.Buf, pre.Length);
            }
            return NoMoves(s, hooks.Drag) ? D1Result.Stuck : D1Result.Freed;
        }

        // --- Söküm ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// K-30 Söküm: back to `preAction` in one step; the counter and `movesSpent` keep their post-action values, `c = 0`.
        /// Returns the `teardown` event body (the caller emits it last).
        /// </summary>
        public static TeardownEvent Teardown(GameState s, int[] preAction, string cause)
        {
            var post = (int[])s.Buf.Clone();
            int movesLeft = Hdr(s, H.MovesLeft);
            int spent = Hdr(s, H.MovesSpent);
            Array.Copy(preAction, s.Buf, preAction.Length);
            SetHdr(s, H.MovesLeft, movesLeft);
            SetHdr(s, H.MovesSpent, spent);
            SetHdr(s, H.Combo, 0);
            return new TeardownEvent(Hdr(s, H.Turn), TeardownMoves(s, post), cause);
        }

        private static readonly int[] TeardownFields = { PF.Zone, PF.X, PF.Y, PF.Seg, PF.Shape, PF.Color };

        /// <summary>Blocks whose place, shape or colour differ between `post` and the restored state; site-locked ones first.</summary>
        private static TeardownMove[] TeardownMoves(GameState s, int[] post)
        {
            int P = s.Lvl.Layout.Counts.Pieces;
            var postState = new GameState(s.Lvl, post);
            var moves = new List<KeyValuePair<TeardownMove, bool>>();
            for (int id = 0; id < P; id++)
            {
                int b = s.Lvl.Layout.Pieces + id * PieceStride;
                bool same = true;
                foreach (int f in TeardownFields) if (post[b + f] != s.Buf[b + f]) same = false;
                if (same) continue;
                bool locked = PieceZone(postState, id) == Zone.Site && (PieceFlagBits(postState, id) & FlagBit.Locked) != 0;
                moves.Add(new KeyValuePair<TeardownMove, bool>(new TeardownMove(id, PlaceOf(postState, id), PlaceOf(s, id)), locked));
            }
            moves.Sort((a, b) =>
            {
                int d = (b.Value ? 1 : 0) - (a.Value ? 1 : 0);
                return d != 0 ? d : a.Key.PieceId - b.Key.PieceId;
            });
            var o = new TeardownMove[moves.Count];
            for (int i = 0; i < o.Length; i++) o[i] = moves[i].Key;
            return o;
        }

        private static TeardownPlace PlaceOf(GameState s, int id)
        {
            int zone = PieceZone(s, id);
            if (zone == Zone.Yard) return TeardownPlace.OnBoard(At.Yard(PieceX(s, id), PieceY(s, id)));
            if (zone == Zone.Site) return TeardownPlace.OnBoard(At.Site(PieceX(s, id), PieceY(s, id) + Hdr(s, H.Elev), PieceSeg(s, id)));
            return TeardownPlace.OffBoard(zone == Zone.Queue ? TeardownPlace.Queue : zone == Zone.Pending ? TeardownPlace.Pending : TeardownPlace.Gone);
        }
    }
}
