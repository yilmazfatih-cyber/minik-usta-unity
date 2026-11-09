#nullable enable
// Port of src/core/movement.ts (web repo, source of truth).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;
using static MinikUsta.Core.State;

namespace MinikUsta.Core
{
    /// <summary>
    /// Rule hooks the drag consults (TECH §4.1 step 1, §4.2 RAIL condition 4, §7.1). Core has no obstacle-specific `if`:
    /// the caller passes the active rules' hooks (obstacle registry). Every field is optional.
    /// </summary>
    public sealed class DragRules
    {
        /// <summary>Rules of a level without obstacle hooks (Phase 2: W1 static gaps are always open).</summary>
        public static readonly DragRules Default = new DragRules();

        /// <summary>K-09 (c): false when an active rule forbids picking (Y3 chain, Y4 wet). Default: always true.</summary>
        public readonly Func<GameState, int, bool>? CanPick;
        /// <summary>
        /// RAIL condition 4: whether `pieceId` may pass gap `gap` now (W4 shutter open, W7 unlocked, both true while K-40
        /// Open Shutter runs). It decides alone (K-40 may open a closed shutter). Default: the gap's `open` state field.
        /// Arguments: (state, gap, pieceId).
        /// </summary>
        public readonly Func<GameState, int, int, bool>? CanPassGap;
        /// <summary>K-07 row 5 / E-27: every segment is complete and the level goes on for an extra goal. Default: `IsSiteClosed`.</summary>
        public readonly Func<GameState, bool>? SiteClosed;

        public DragRules(Func<GameState, int, bool>? canPick = null, Func<GameState, int, int, bool>? canPassGap = null,
            Func<GameState, bool>? siteClosed = null)
        {
            CanPick = canPick;
            CanPassGap = canPassGap;
            SiteClosed = siteClosed;
        }
    }

    /// <summary>`DropClass.Kind` values (TS `DropClass['kind']`).</summary>
    public static class DropKinds
    {
        public const string Cancel = "cancel";
        public const string Yard = "yard";
        public const string SiteFree = "siteFree";
        public const string SiteRail = "siteRail";
    }

    /// <summary>
    /// Result of the K-07 release table (TECH §4.3). `Row` is the GDD K-07 row number (0 = not a reachable node).
    /// `Reason` (TS `DropCancelReason`, values in `CancelReasons`) is set for `cancel` only; `Gap` for `siteRail` only (else −1).
    /// </summary>
    public sealed class DropClass
    {
        public readonly string Kind;
        public readonly string? Reason;
        public readonly int Row;
        public readonly int Gap;

        private DropClass(string kind, string? reason, int row, int gap)
        {
            Kind = kind;
            Reason = reason;
            Row = row;
            Gap = gap;
        }

        public static readonly DropClass Invalid = new DropClass(DropKinds.Cancel, CancelReasons.Invalid, 0, -1);
        public static readonly DropClass SameSpot = new DropClass(DropKinds.Cancel, CancelReasons.SameSpot, 1, -1);
        public static readonly DropClass Yard = new DropClass(DropKinds.Yard, null, 2, -1);
        public static readonly DropClass CraneOverYard = new DropClass(DropKinds.Cancel, CancelReasons.CraneOverYard, 3, -1);
        public static readonly DropClass Straddle = new DropClass(DropKinds.Cancel, CancelReasons.Straddle, 4, -1);
        public static readonly DropClass SiteClosed = new DropClass(DropKinds.Cancel, CancelReasons.SiteClosed, 5, -1);
        public static readonly DropClass SiteFree = new DropClass(DropKinds.SiteFree, null, 6, -1);
        /// <summary>Row 7: stays on the rail of gap `gap` (K-12).</summary>
        public static DropClass SiteRail(int gap) => new DropClass(DropKinds.SiteRail, null, 7, gap);

        /// <summary>True for the K-07 rows that cancel (no move spent, nothing changes). TS `isCancelled`.</summary>
        public bool IsCancelled => Kind == DropKinds.Cancel;

        public override string ToString() => Kind + (Reason != null ? "/" + Reason : "") + " row " + Row + (Gap >= 0 ? " gap " + Gap : "");
    }

    /// <summary>One `Follow` / `MoveTo` step of the view (TECH §4.4).</summary>
    public sealed class FollowResult
    {
        /// <summary>Current node after the call.</summary>
        public readonly DragNode Node;
        public readonly bool Changed;
        /// <summary>
        /// Nodes the view passes, from the previous node (excluded) to `Node` (included); empty when unchanged. Consecutive
        /// nodes are one unit step apart, except a mode switch between two start nodes (same cells; debris, TECH §4.1/3).
        /// </summary>
        public readonly DragNode[] Path;
        /// <summary>`dragCrossedWall`: the first FREE → FREE step of this drag that moves block cells across the boundary.</summary>
        public readonly bool CrossedWall;
        /// <summary>`dragEnteredRail`: the first FREE → RAIL(g) step of this drag (a debris RAIL start node does not count).</summary>
        public readonly bool EnteredRail;
        /// <summary>K-05 presentation signal, at most once per drag.</summary>
        public readonly bool BlockedByWallHeight;
        /// <summary>
        /// K-44 `blockedCargo { pieceId: session.pieceId }` (TECH §2R.15 item 5): an Ağır Yük drag whose target point `p` left
        /// the yard (`p.x ≥ wy` or `p.y ≥ hy`) for the first time in this hold. At most once per drag; never a move, never in
        /// the event log, the replay or the tutorial `done` vocabulary.
        /// </summary>
        public readonly bool BlockedCargo;

        public FollowResult(DragNode node, bool changed, DragNode[] path, bool crossedWall, bool enteredRail, bool blockedByWallHeight, bool blockedCargo)
        {
            Node = node;
            Changed = changed;
            Path = path;
            CrossedWall = crossedWall;
            EnteredRail = enteredRail;
            BlockedByWallHeight = blockedByWallHeight;
            BlockedCargo = blockedCargo;
        }

        internal FollowResult WithSignals(bool blockedByWallHeight, bool blockedCargo) =>
            new FollowResult(Node, Changed, Path, CrossedWall, EnteredRail, blockedByWallHeight, blockedCargo);
    }

    /// <summary>Why a piece cannot be picked (scene feedback: K-14 no reaction, K-09 (a) "kımıldamıyor" shake, E-30). TS `PickFailure`.</summary>
    public static class PickFailures
    {
        public const string NotOnBoard = "notOnBoard";
        public const string HiddenSegment = "hiddenSegment";
        public const string Locked = "locked";
        public const string NoMoves = "noMoves";
        public const string Rule = "rule";
        public const string Immovable = "immovable";
        /// <summary>v3 Y10 (GDD §16 K-58 item 3): a pallet layer under another layer; no reaction, no move spent.</summary>
        public const string Covered = "covered";
    }

    /// <summary>TS `DragAttempt`: `Ok` with a `Session`, or a `Reason` (`PickFailures`).</summary>
    public sealed class DragAttempt
    {
        public readonly bool Ok;
        public readonly DragSession? Session;
        public readonly string? Reason;

        private DragAttempt(bool ok, DragSession? session, string? reason)
        {
            Ok = ok;
            Session = session;
            Reason = reason;
        }

        public static DragAttempt Success(DragSession session) => new DragAttempt(true, session, null);
        public static DragAttempt Failure(string reason) => new DragAttempt(false, null, reason);
    }

    /// <summary>
    /// Drag movement (docs/GDD.md K-04, K-05, K-07…K-13, K-44, K-49; docs/TECH_DESIGN.md §4, §2R.1).
    ///
    /// Edge model (R-03, D-010): the wall is the zero-width boundary between x = wy−1 and x = wy (`geo.BoundaryX |
    /// geo.SiteX`; default board 5 | 6). A drag node is `(ix, iy, mode)`, mode FREE (0) or RAIL(g) (1 + gap index); integer
    /// code `mode * 80 + iy * 8 + ix` in the 8 × 10 maximum frame for every geometry (= Coords.DragNodeCode, §2R.0 item 2).
    /// `BeginDrag` runs the K-09 pick gate and ONE BFS over the nodes (the board is frozen while dragging, K-08, so the
    /// reachable set R never changes); the session then answers the scene's read-only queries: reachability, the K-07
    /// release class (cancel preview), K-08 sticky follow with its tie-break rules and hysteresis, the BFS path the view
    /// follows, and the scene signals `blockedByWallHeight` (K-05), `blockedCargo` (K-44), `dragCrossedWall` /
    /// `dragEnteredRail` (tutorial `overWall` / `gapPass`, TECH §8.2). The session keeps no reference to the state.
    ///
    /// Nodes (TECH §4.2, §2R.1):
    /// - FREE(ix, iy) valid ⇔ every cell inside the level's board + crane area (`x &lt; wy + ws`, `y &lt; h + 2`; Ağır Yük
    ///   (I5/Q9, K-08, K-44): every cell inside the yard, `x ≤ wy − 1`, `y ≤ hy − 1` — no yard air, no crane area, no
    ///   boundary, no rail), no collision, every row that has block cells on BOTH sides of the boundary is open in FREE
    ///   mode (`y ≥ wall.height`), and open sky (K-11, K-13): in every site column the block covers, its lowest cell is
    ///   above every blocked cell of that column. Yard air and site air are empty cells.
    /// - RAIL(g)(ix, iy) valid ⇔ inside the board, no collision, gap passable (rules' `CanPassGap`), K-12 alignment (every
    ///   block row inside the gap rows) and the block straddles the boundary or is fully on the site.
    /// Edges (unit steps; in a horizontal step every cell that changes side must be in a row open in the step's mode —
    /// FREE: `openFree`, any step touching RAIL(g): the rows of g):
    /// - FREE → FREE: left, right, down, up. FREE → RAIL(g): right, only from a node fully in the yard (K-12).
    /// - RAIL(g) → RAIL(g): left / right only (vertical position locked). RAIL(g) → FREE: left, onto a node fully in the yard.
    /// Neighbour order (left, right, down, up) is fixed: it decides the BFS parents and so the path the view follows
    /// (GDD K-08 example: (2,6) → (3,9) goes (2,7), (2,8), (3,8), (3,9)).
    /// </summary>
    public static class Movement
    {
        /// <summary>DragMode of free movement (TECH §2.1).</summary>
        public const int Free = DragMode.Free;

        /// <summary>DragMode of the rail through gap `gap` (TECH §2.1).</summary>
        public static int RailMode(int gap) => gap + 1;

        /// <summary>Nodes per mode: the 8 × 10 maximum frame of anchors (every geometry, §2R.0 item 2).</summary>
        public const int NodesPerMode = Geometry.MaxCols * Geometry.MaxRows;
        /// <summary>K-08: a new node is taken only when its squared distance is at least this much smaller than the current one.</summary>
        public const double HysteresisD2 = 0.2;
        /// <summary>Float tolerance of the K-08 comparisons (TECH §4.4).</summary>
        public const double D2Epsilon = 1e-9;

        /// <summary>
        /// Ağır Yük (Y5, GDD K-44 Faz 2R): I5 and Q9 in every orientation. Cargo is carried inside the yard only (K-08): every
        /// drag node keeps all its cells at `x ≤ wy − 1`, `y ≤ hy − 1`. Other shapes are material blocks, whatever their width.
        /// </summary>
        public static bool IsCargoShape(ShapeDef shape) => shape.Kind == "I5" || shape.Kind == "Q9";

        /// <summary>K-07 row 5 (E-27): `deliveryCursor` counts completed segments; the site is closed once all S are complete.</summary>
        public static bool IsSiteClosed(GameState s) => Hdr(s, H.DeliveryCursor) >= s.Lvl.Segments.Length;

        /// <summary>True for the K-07 rows that cancel (no move spent, nothing changes).</summary>
        public static bool IsCancelled(DropClass drop) => drop.IsCancelled;

        /// <summary>
        /// K-09 pick gate + BFS (TECH §4.1). Checks, in order: the piece is on the board (yard, or site in the visible segment;
        /// K-22, K-23), not locked (K-14), `movesLeft &gt; 0` (K-07, E-30), every `CanPick` hook (K-09 (c)), and R holds a node
        /// whose cells differ from the start cells (K-09 (a)). Crates and bags are obstacles, never pieces (K-09 (d)).
        /// </summary>
        public static DragAttempt TryBeginDrag(GameState s, int pieceId, DragRules? rules = null)
        {
            rules ??= DragRules.Default;
            if (pieceId < 0 || pieceId >= s.Lvl.Layout.Counts.Pieces) return DragAttempt.Failure(PickFailures.NotOnBoard);
            int zone = PieceZone(s, pieceId);
            if (zone != Zone.Yard && zone != Zone.Site) return DragAttempt.Failure(PickFailures.NotOnBoard);
            if (zone == Zone.Site && PieceSeg(s, pieceId) != Grid.VisibleSegment(s)) return DragAttempt.Failure(PickFailures.HiddenSegment);
            if ((PieceFlagBits(s, pieceId) & FlagBit.Locked) != 0) return DragAttempt.Failure(PickFailures.Locked);
            if (s.Lvl.HasStacks && Grid.IsCovered(s, pieceId)) return DragAttempt.Failure(PickFailures.Covered);
            if (Hdr(s, H.MovesLeft) <= 0) return DragAttempt.Failure(PickFailures.NoMoves);
            if (rules.CanPick != null && !rules.CanPick(s, pieceId)) return DragAttempt.Failure(PickFailures.Rule);
            var session = new DragSession(s, pieceId, rules);
            if (!session.Movable) return DragAttempt.Failure(PickFailures.Immovable);
            return DragAttempt.Success(session);
        }

        /// <summary>`beginDrag(state, pieceId): DragSession | null` (TECH §4.1); null = not pickable (K-09).</summary>
        public static DragSession? BeginDrag(GameState s, int pieceId, DragRules? rules = null)
        {
            var attempt = TryBeginDrag(s, pieceId, rules);
            return attempt.Ok ? attempt.Session : null;
        }

        /// <summary>K-09: the piece can be picked now.</summary>
        public static bool CanPickPiece(GameState s, int pieceId, DragRules? rules = null) => TryBeginDrag(s, pieceId, rules).Ok;

        /// <summary>Board cells of a block with `shape` anchored at `(ix, iy)`.</summary>
        public static BoardCell[] BlockCells(ShapeDef shape, int ix, int iy)
        {
            var o = new BoardCell[shape.Cells.Length];
            for (int i = 0; i < o.Length; i++) o[i] = new BoardCell(ix + shape.Cells[i].X, iy + shape.Cells[i].Y);
            return o;
        }
    }

    /// <summary>A drag in progress: read-only queries over the frozen board (TECH §1.4, §4). TS `DragSession` (interface) / `Session` (class).</summary>
    public sealed class DragSession
    {
        private const short Unknown = 0;
        private const short Valid = 1;
        private const short Invalid = 2;

        /// <summary>
        /// Transient BFS scratch shared by every session of the thread. A BFS always runs synchronously to its end and nothing
        /// else touches these arrays meanwhile, so sharing them is safe and keeps `BeginDrag` to few allocations (TECH §4.5).
        /// </summary>
        [ThreadStatic] private static short[]? sharedQueue;
        /// <summary>Neighbour scratch (4 FREE steps + rails ≤ 3 + spare), as in the TS `NEIGHBOURS = new Int16Array(8)`.</summary>
        [ThreadStatic] private static short[]? neighbourScratch;

        public readonly int PieceId;
        public readonly ShapeDef Shape;
        /// <summary>FREE start node (the piece's anchor; site pieces: board row = plan row + elevator offset).</summary>
        public readonly DragNode Start;
        /// <summary>Ağır Yük (K-44): R holds yard nodes only and `Follow` may emit `blockedCargo`.</summary>
        public readonly bool Cargo;
        /// <summary>FREE start, plus RAIL(g) for debris whose rows are all inside an open gap (TECH §4.1/3).</summary>
        public readonly DragNode[] StartNodes;
        /// <summary>|R| (start nodes included).</summary>
        public int ReachableCount { get; private set; }
        /// <summary>R contains a FREE → FREE step across the boundary (tutorial lock guarantee `overWall`, TECH §8.2).</summary>
        public bool CanCrossWall { get; private set; }
        /// <summary>R contains a FREE → RAIL(g) step (tutorial lock guarantee `gapPass`, TECH §8.2).</summary>
        public bool CanEnterRail { get; private set; }
        /// <summary>K-09 (a): R holds a node whose cells differ from the start cells.</summary>
        public bool Movable { get; private set; }

        private readonly BoardGeo geo;
        private readonly int w;
        private readonly int h;
        /// <summary>Exclusive bound of `ix + w`: `wy + ws`, cargo `wy` (K-44: Ağır Yük stays in the yard).</summary>
        private readonly int maxX;
        /// <summary>Exclusive bound of `iy + h`: `h + 2` (crane area included), cargo `hy` (K-44).</summary>
        private readonly int maxY;
        private readonly int[] rows;
        private readonly int[] colRows;
        private readonly int[] colBottom;
        private readonly byte[] masks;
        private readonly sbyte[] colTop;
        private readonly int openFree;
        /// <summary>Per gap: rows of the gap when passable, else 0 (`openRail[g]`, TECH §2.2).</summary>
        private readonly List<int> railOpen = new List<int>();
        /// <summary>Per gap: K-12 alignment `gapLo ≤ iy` and `iy + h ≤ gapHi`.</summary>
        private readonly List<int> gapLo = new List<int>();
        private readonly List<int> gapHi = new List<int>();
        private readonly bool tooTall;
        private readonly bool siteClosed;
        private readonly List<int> startCodes;
        private readonly int startAnchor;
        private readonly List<DropClass> railDrops = new List<DropClass>();

        /// <summary>Per node code: Unknown / Valid / Invalid (node validity cache).</summary>
        private readonly short[] validity;
        private readonly short[] distStart;
        private readonly short[] distCur;
        private readonly short[] parentCur;
        /// <summary>R in BFS order (`ReachableCount` entries).</summary>
        private readonly short[] reach;
        private short[] queue;
        private int head;
        private int tail;

        private int cur;
        private FollowResult stay;
        private bool crossedEmitted;
        private bool railEmitted;
        private bool blockedEmitted;
        private bool cargoEmitted;

        internal DragSession(GameState s, int pieceId, DragRules rules)
        {
            var lvl = s.Lvl;
            geo = lvl.Geo;
            PieceId = pieceId;
            Shape = Shapes.ShapeByIndex(PieceShape(s, pieceId));
            w = Shape.W;
            h = Shape.H;
            Cargo = Movement.IsCargoShape(Shape);
            maxX = Cargo ? geo.Wy : geo.Cols;
            maxY = Cargo ? geo.Hy : geo.Rows;
            rows = Shape.Rows;
            colRows = Shape.ColRows;
            colBottom = Shape.ColBottom;
            masks = Grid.CollisionMasks(s, pieceId);
            colTop = Grid.SiteColumnTops(s, pieceId);
            openFree = Coords.OpenFreeMask(geo, lvl.WallHeight);
            tooTall = h > geo.Rows - lvl.WallHeight;
            siteClosed = rules.SiteClosed != null ? rules.SiteClosed(s) : Movement.IsSiteClosed(s);
            for (int g = 0; g < lvl.Gaps.Length; g++)
            {
                var gap = lvl.Gaps[g];
                int y = GapField(s, g, GF.Y);
                int size = gap.Size;
                bool passable = rules.CanPassGap != null ? rules.CanPassGap(s, g, pieceId) : GapField(s, g, GF.Open) != 0;
                railOpen.Add(passable ? Coords.RowRangeMask(geo, y, size) : 0);
                gapLo.Add(y);
                gapHi.Add(y + size);
                railDrops.Add(DropClass.SiteRail(g));
            }
            int space = (1 + lvl.Gaps.Length) * Movement.NodesPerMode;
            // per-session arrays, allocated at pick time (nothing allocated per BFS); TS packs them into one Int16Array arena
            validity = new short[space];
            distStart = new short[space];
            distCur = new short[space];
            parentCur = new short[space];
            reach = new short[space];
            queue = sharedQueue ??= new short[4 * Movement.NodesPerMode];

            // start nodes (TECH §4.1/3): FREE(anchor); debris also RAIL(g) when its rows are inside an open gap
            int zone = PieceZone(s, pieceId);
            int ix = PieceX(s, pieceId);
            int iy = PieceY(s, pieceId) + (zone == Zone.Site ? Hdr(s, H.Elev) : 0);
            startAnchor = iy * Geometry.MaxCols + ix;
            startCodes = new List<int> { startAnchor };
            if (zone == Zone.Site && (PieceFlagBits(s, pieceId) & FlagBit.Debris) != 0 && !Cargo)
            {
                for (int g = 0; g < railOpen.Count; g++)
                {
                    if (ValidRail(g, ix, iy)) startCodes.Add((1 + g) * Movement.NodesPerMode + startAnchor);
                }
            }
            StartNodes = new DragNode[startCodes.Count];
            for (int i = 0; i < startCodes.Count; i++) StartNodes[i] = Node(startCodes[i]);
            Start = Node(startAnchor);
            // The block may always return to its start cells (K-07 row 1), even when its FREE start breaks open sky (a site
            // block under a stuck mortar block): every edge stays two-way, so all of R is reachable from any current node.
            validity[startAnchor] = Valid;

            // one BFS: from the FREE start node the start equivalence makes it the multi-source BFS of all start nodes, so
            // its distances and parents also serve the first current node (= the FREE start)
            Bfs(startAnchor, distStart, parentCur, true);
            Array.Copy(distStart, distCur, distStart.Length);
            for (int i = 0; i < ReachableCount; i++)
            {
                if (reach[i] % Movement.NodesPerMode != startAnchor)
                {
                    Movable = true;
                    break;
                }
            }
            cur = startAnchor;
            stay = StayResult();
        }

        public DragNode Current => Node(cur);

        public bool IsReachable(DragNode node)
        {
            int code = CodeOf(node);
            return code >= 0 && distStart[code] >= 0;
        }

        /// <summary>BFS steps from the start nodes; −1 outside R.</summary>
        public int DistanceFromStart(DragNode node)
        {
            int code = CodeOf(node);
            return code < 0 ? -1 : distStart[code];
        }

        /// <summary>BFS steps from the current node (K-08 tie-break 1); −1 outside R.</summary>
        public int DistanceFromCurrent(DragNode node)
        {
            int code = CodeOf(node);
            return code < 0 ? -1 : distCur[code];
        }

        /// <summary>R sorted by node code (mode: FREE, RAIL(0), RAIL(1) …; then y; then x). Not the K-08 tie order. Allocates.</summary>
        public DragNode[] ReachableNodes()
        {
            var codes = new int[ReachableCount];
            for (int i = 0; i < ReachableCount; i++) codes[i] = reach[i];
            Array.Sort(codes);
            var o = new DragNode[codes.Length];
            for (int i = 0; i < codes.Length; i++) o[i] = Node(codes[i]);
            return o;
        }

        /// <summary>Nodes one edge away from `node` (left, right, down, up order). Allocates.</summary>
        public DragNode[] Neighbours(DragNode node)
        {
            int code = CodeOf(node);
            if (code < 0) return new DragNode[0];
            var nbr = Scratch();
            int n = NeighbourCodes(code, nbr);
            var o = new DragNode[n];
            for (int i = 0; i < n; i++) o[i] = Node(nbr[i]);
            return o;
        }

        /// <summary>K-08 nearest node of R to the target anchor point `(px, py)`, tie-breaks applied, no hysteresis.</summary>
        public DragNode Nearest(double px, double py) => Node(NearestCode(px, py));

        /// <summary>
        /// K-08 sticky follow: moves the current node to `Nearest(p)` when the hysteresis allows it. `(px, py)` = the target
        /// anchor point `p` in cells. Emits `blockedByWallHeight` (K-05) and `blockedCargo` (K-44) at most once per drag.
        /// </summary>
        public FollowResult Follow(double px, double py)
        {
            int best = NearestCode(px, py);
            var result = stay;
            if (best != cur && D2(best, px, py) - (D2(cur, px, py) - Movement.HysteresisD2) <= Movement.D2Epsilon)
                result = Advance(best);
            bool blocked = false;
            bool cargo = false;
            if (!blockedEmitted && IsBlockedByWallHeight(px, py))
            {
                blockedEmitted = true;
                blocked = true;
            }
            // K-44: the first time p leaves the yard during this hold (GDD: x ≥ Wy or y ≥ Hy)
            if (Cargo && !cargoEmitted && (px >= geo.Wy || py >= geo.Hy))
            {
                cargoEmitted = true;
                cargo = true;
            }
            if (blocked || cargo) result = result.WithSignals(blocked, cargo);
            return result;
        }

        /// <summary>BFS path from the current node (excluded) to `node` (included); null outside R. Allocates.</summary>
        public DragNode[]? PathTo(DragNode node)
        {
            int code = CodeOf(node);
            if (code < 0 || distCur[code] < 0) return null;
            var codes = PathCodes(code);
            var o = new DragNode[codes.Count];
            for (int i = 0; i < o.Length; i++) o[i] = Node(codes[i]);
            return o;
        }

        /// <summary>Moves the current node to `node` along the BFS path (bots, harness, forced release); throws outside R.</summary>
        public FollowResult MoveTo(DragNode node)
        {
            int code = CodeOf(node);
            if (code < 0 || distStart[code] < 0)
                throw new ArgumentOutOfRangeException(nameof(node), "moveTo: node (" + node.Ix + "," + node.Iy + "," + node.Mode + ") is not reachable");
            return code == cur ? stay : Advance(code);
        }

        /// <summary>K-07 release table for the current node; cancel preview of the shadow (TECH §4.3).</summary>
        public DropClass Classify() => Classify(Current);

        /// <summary>K-07 release table for `node`.</summary>
        public DropClass Classify(DragNode node)
        {
            int code = CodeOf(node);
            if (code < 0 || distStart[code] < 0) return DropClass.Invalid;
            // row 1: start cells in one of the start modes (debris: either start node, TECH §4.1/3)
            if (startCodes.Contains(code)) return DropClass.SameSpot;
            int boundaryX = geo.BoundaryX;
            int right = node.Ix + w - 1;
            if (node.Mode == Movement.Free && right <= boundaryX)
            {
                // rows 2 / 3: over the yard; any cell at y ≥ hy (yard air or crane area) cancels (K-05, E-56)
                return node.Iy + h - 1 < geo.Hy ? DropClass.Yard : DropClass.CraneOverYard;
            }
            if (node.Ix <= boundaryX) return DropClass.Straddle; // row 4 (E-06, E-28)
            if (siteClosed) return DropClass.SiteClosed; // row 5 (E-27)
            if (node.Mode == Movement.Free) return DropClass.SiteFree; // row 6: falls (K-11)
            return node.Mode - 1 < railDrops.Count ? railDrops[node.Mode - 1] : DropClass.Invalid; // row 7: stays on the rail (K-12)
        }

        /// <summary>Board cells of the block at `node`. Allocates.</summary>
        public BoardCell[] Cells(DragNode node) => Movement.BlockCells(Shape, node.Ix, node.Iy);

        // --- internals -------------------------------------------------------------------------------------------------

        private static short[] Scratch() => neighbourScratch ??= new short[8];

        /// <summary>Code of a node, −1 when it is outside the node space of this level (board + crane area, its gaps).</summary>
        private int CodeOf(DragNode node)
        {
            int ix = node.Ix;
            int iy = node.Iy;
            int mode = node.Mode;
            if (ix < 0 || ix >= geo.Cols || iy < 0 || iy >= geo.Rows || mode < 0 || mode > railOpen.Count) return -1;
            return mode * Movement.NodesPerMode + iy * Geometry.MaxCols + ix;
        }

        private static DragNode Node(int code)
        {
            int mode = code / Movement.NodesPerMode;
            int rest = code - mode * Movement.NodesPerMode;
            return new DragNode(rest % Geometry.MaxCols, rest / Geometry.MaxCols, mode);
        }

        private static double D2(int code, double px, double py)
        {
            int rest = code % Movement.NodesPerMode;
            double dx = (rest % Geometry.MaxCols) - px;
            double dy = (rest / Geometry.MaxCols) - py;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// K-08: the node of R with the smallest squared distance to p; ties (within D2Epsilon) go to (1) fewer BFS steps
        /// from the current node, (2) FREE before RAIL, (3) smaller y, (4) smaller x (GDD K-08). The gap index only breaks
        /// the remaining tie (same anchor on two rails) for determinism: it ranks below y and x, so the result never depends
        /// on the order of `wall.gaps` in the data (nor on a runtime reorder, W5) — see `TieRank`.
        /// </summary>
        private int NearestCode(double px, double py)
        {
            int best = cur;
            double bestD = D2(best, px, py);
            int bestDist = 0;
            int bestRank = TieRank(best);
            for (int i = 0; i < ReachableCount; i++)
            {
                int code = reach[i];
                double d = D2(code, px, py);
                if (d > bestD + Movement.D2Epsilon) continue;
                int dist = distCur[code];
                if (d < bestD - Movement.D2Epsilon || dist < bestDist)
                {
                    best = code;
                    bestD = d;
                    bestDist = dist;
                    bestRank = TieRank(code);
                }
                else if (dist == bestDist)
                {
                    int rank = TieRank(code);
                    if (rank < bestRank)
                    {
                        best = code;
                        bestD = d;
                        bestRank = rank;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// K-08 tie-breaks (2)–(4) as one integer: FREE before RAIL, then smaller y, then smaller x; the gap index comes last
        /// (it is not a GDD criterion). Unlike the node code (`mode * 80 + iy * 8 + ix`), which ranks the gap index above y.
        /// </summary>
        private int TieRank(int code)
        {
            int mode = code / Movement.NodesPerMode;
            int rest = code - mode * Movement.NodesPerMode;
            return ((mode == Movement.Free ? 0 : Movement.NodesPerMode) + rest) * (1 + railOpen.Count) + mode;
        }

        /// <summary>
        /// K-05 `blockedByWallHeight` (TECH §4.4): the block is too tall for the wall (`h &gt; (H + 2) − height`, so no FREE node
        /// of it is fully on the site), the target box centre is past the boundary and the box reaches the crane rows, but the
        /// current FREE node has not crossed (fully in the yard, or straddling with its top over the wall: S4_0 / Z4_0).
        /// </summary>
        private bool IsBlockedByWallHeight(double px, double py)
        {
            if (!tooTall || cur >= Movement.NodesPerMode) return false;
            int siteX = geo.SiteX;
            return cur % Geometry.MaxCols < siteX && px + w / 2.0 > siteX && py + h > geo.CraneRow;
        }

        private FollowResult Advance(int target)
        {
            var codes = PathCodes(target);
            bool crossed = false;
            bool entered = false;
            int prev = cur;
            foreach (int next in codes)
            {
                if (prev < Movement.NodesPerMode && next != prev)
                {
                    bool horizontal = next % Movement.NodesPerMode != prev % Movement.NodesPerMode && (next - prev) % Geometry.MaxCols != 0;
                    if (horizontal && next < Movement.NodesPerMode && StepCrossesBoundary(prev % Geometry.MaxCols, next > prev ? 1 : -1))
                        crossed = true;
                    if (horizontal && next >= Movement.NodesPerMode) entered = true;
                }
                prev = next;
            }
            bool crossedWall = crossed && !crossedEmitted;
            bool enteredRail = entered && !railEmitted;
            if (crossedWall) crossedEmitted = true;
            if (enteredRail) railEmitted = true;
            cur = target;
            Bfs(target, distCur, parentCur, false);
            stay = StayResult();
            var path = new DragNode[codes.Count];
            for (int i = 0; i < path.Length; i++) path[i] = Node(codes[i]);
            return new FollowResult(Node(target), true, path, crossedWall, enteredRail, false, false);
        }

        private FollowResult StayResult() => new FollowResult(Node(cur), false, new DragNode[0], false, false, false, false);

        /// <summary>Codes from the current node (excluded) to `target` (included), following the BFS parents.</summary>
        private List<int> PathCodes(int target)
        {
            var o = new List<int>();
            int c = target;
            while (c != cur && c >= 0)
            {
                o.Add(c);
                c = parentCur[c];
            }
            o.Reverse();
            return o;
        }

        /// <summary>
        /// BFS from `source`. Start nodes are one physical position (same cells, TECH §4.1/3): reaching one of them reaches
        /// all of them at the same distance (no extra step), so R is the multi-source BFS of the start nodes.
        /// </summary>
        private void Bfs(int source, short[] dist, short[]? parent, bool collect)
        {
            Array.Fill(dist, (short)-1);
            if (parent != null) Array.Fill(parent, (short)-1);
            if (sharedQueue == null || sharedQueue.Length < dist.Length) sharedQueue = new short[dist.Length];
            queue = sharedQueue;
            head = 0;
            tail = 0;
            if (collect) ReachableCount = 0;
            Push(source, 0, -1, dist, parent);
            var nbr = Scratch();
            while (head < tail)
            {
                int u = queue[head++];
                int du = dist[u];
                if (collect) reach[ReachableCount++] = (short)u;
                int n = NeighbourCodes(u, nbr);
                for (int i = 0; i < n; i++)
                {
                    int v = nbr[i];
                    if (collect) NoteEdge(u, v);
                    if (dist[v] < 0) Push(v, du + 1, u, dist, parent);
                }
            }
        }

        private void Push(int code, int d, int from, short[] dist, short[]? parent)
        {
            dist[code] = (short)d;
            if (parent != null) parent[code] = (short)from;
            queue[tail++] = (short)code;
            if (startCodes.Count > 1 && startCodes.Contains(code))
            {
                foreach (int other in startCodes)
                {
                    if (dist[other] >= 0) continue;
                    dist[other] = (short)d;
                    if (parent != null) parent[other] = (short)code;
                    queue[tail++] = (short)other;
                }
            }
        }

        /// <summary>Tutorial guarantees (TECH §8.2): `overWall` = a FREE → FREE boundary step, `gapPass` = a FREE → RAIL step.</summary>
        private void NoteEdge(int u, int v)
        {
            if (u >= Movement.NodesPerMode) return;
            if (v >= Movement.NodesPerMode)
            {
                CanEnterRail = true;
                return;
            }
            if (v - u == 1 || u - v == 1)
            {
                if (StepCrossesBoundary(u % Geometry.MaxCols, v > u ? 1 : -1)) CanCrossWall = true;
            }
        }

        /// <summary>Writes the neighbour codes of `code` into `out` (left, right, down, up); returns their number.</summary>
        private int NeighbourCodes(int code, short[] o)
        {
            int mode = code / Movement.NodesPerMode;
            int rest = code - mode * Movement.NodesPerMode;
            int ix = rest % Geometry.MaxCols;
            int iy = (rest - ix) / Geometry.MaxCols;
            int boundaryX = geo.BoundaryX;
            int n = 0;
            if (mode == Movement.Free)
            {
                if (ValidFree(ix - 1, iy) && CrossOk(ix, iy, -1, openFree)) o[n++] = (short)(code - 1);
                if (ValidFree(ix + 1, iy) && CrossOk(ix, iy, 1, openFree)) o[n++] = (short)(code + 1);
                if (ix + w - 1 <= boundaryX)
                {
                    // K-12: a rail is entered only from a node fully in the yard, by a step to the right
                    for (int g = 0; g < railOpen.Count; g++)
                    {
                        if (ValidRail(g, ix + 1, iy) && CrossOk(ix, iy, 1, railOpen[g]))
                            o[n++] = (short)((1 + g) * Movement.NodesPerMode + rest + 1);
                    }
                }
                if (ValidFree(ix, iy - 1)) o[n++] = (short)(code - Geometry.MaxCols);
                if (ValidFree(ix, iy + 1)) o[n++] = (short)(code + Geometry.MaxCols);
                return n;
            }
            int gap = mode - 1;
            int open = railOpen[gap];
            if (ix - 1 + w - 1 <= boundaryX)
            {
                // leaving the rail to the left, fully into the yard: back to FREE
                if (ValidFree(ix - 1, iy) && CrossOk(ix, iy, -1, open)) o[n++] = (short)(rest - 1);
            }
            else if (ValidRail(gap, ix - 1, iy) && CrossOk(ix, iy, -1, open))
            {
                o[n++] = (short)(code - 1);
            }
            if (ValidRail(gap, ix + 1, iy) && CrossOk(ix, iy, 1, open)) o[n++] = (short)(code + 1);
            return n;
        }

        /// <summary>In a horizontal step from `ix` by `dx`, the block column that changes side (−1 when none).</summary>
        private int CrossingColumn(int ix, int dx)
        {
            int c = dx > 0 ? geo.BoundaryX - ix : geo.SiteX - ix;
            return c >= 0 && c < w ? c : -1;
        }

        private bool StepCrossesBoundary(int ix, int dx) => CrossingColumn(ix, dx) >= 0;

        /// <summary>TECH §2.2: every cell that changes side in this horizontal step does so in a row of the open mask.</summary>
        private bool CrossOk(int ix, int iy, int dx, int open)
        {
            int c = CrossingColumn(ix, dx);
            if (c < 0) return true;
            return Coords.BoundaryAllows((colRows[c] << iy) & geo.RowMaskAll, open);
        }

        private bool InBounds(int ix, int iy) => ix >= 0 && iy >= 0 && ix + w <= maxX && iy + h <= maxY;

        private bool Collides(int ix, int iy)
        {
            for (int r = 0; r < h; r++)
            {
                if ((masks[iy + r] & (rows[r] << ix)) != 0) return true;
            }
            return false;
        }

        private bool ValidFree(int ix, int iy)
        {
            if (!InBounds(ix, iy)) return false;
            int code = iy * Geometry.MaxCols + ix;
            short known = validity[code];
            if (known != Unknown) return known == Valid;
            bool ok = ComputeFree(ix, iy);
            validity[code] = ok ? Valid : Invalid;
            return ok;
        }

        private bool ComputeFree(int ix, int iy)
        {
            int yardBits = geo.YardBits;
            int siteBits = geo.SiteBits;
            int siteX = geo.SiteX;
            for (int r = 0; r < h; r++)
            {
                int bits = rows[r] << ix;
                if ((masks[iy + r] & bits) != 0) return false;
                // a row with block cells on both sides of the boundary must be open in FREE mode (TECH §2.2)
                if ((bits & yardBits) != 0 && (bits & siteBits) != 0 && ((openFree >> (iy + r)) & 1) == 0) return false;
            }
            // open sky (K-11, K-13): in every covered site column the lowest block cell is above every blocked cell
            for (int x = Math.Max(ix, siteX); x < ix + w; x++)
            {
                int c = x - ix;
                if (colTop[x - siteX] >= iy + colBottom[c]) return false;
            }
            return true;
        }

        private bool ValidRail(int g, int ix, int iy)
        {
            if (railOpen[g] == 0 || !InBounds(ix, iy)) return false;
            if (ix + w - 1 < geo.SiteX) return false;
            if (iy < gapLo[g] || iy + h > gapHi[g]) return false;
            int code = (1 + g) * Movement.NodesPerMode + iy * Geometry.MaxCols + ix;
            short known = validity[code];
            if (known != Unknown) return known == Valid;
            bool ok = !Collides(ix, iy);
            validity[code] = ok ? Valid : Invalid;
            return ok;
        }
    }
}
