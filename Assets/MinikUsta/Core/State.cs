#nullable enable
// Port of src/core/state.ts (web repo, source of truth). Int32Array → int[], same layout and offsets.
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;

namespace MinikUsta.Core
{
    /// <summary>Header fields (18).</summary>
    public static class H
    {
        /// <summary>Completed moves (GDD `m`).</summary>
        public const int Turn = 0;
        public const int MovesLeft = 1;
        /// <summary>Usta Serisi (K-33).</summary>
        public const int Combo = 2;
        /// <summary>Golden Trowels available (K-33).</summary>
        public const int Trowels = 3;
        /// <summary>Active segment (segments mode, K-22).</summary>
        public const int ActiveSeg = 4;
        /// <summary>Front segment (carousel, K-23).</summary>
        public const int FrontSeg = 5;
        /// <summary>Carousel counter `t` (K-23).</summary>
        public const int CarouselT = 6;
        /// <summary>Elevator offset `e` (K-24); 0 without elevator.</summary>
        public const int Elev = 7;
        /// <summary>Elevator direction ±1; 0 without elevator.</summary>
        public const int ElevDir = 8;
        /// <summary>Completed segments; truck batches with forSegment ≤ deliveryCursor have been queued (K-22, K-23, K-25).</summary>
        public const int DeliveryCursor = 9;
        public const int QueueLen = 10;
        /// <summary>mulberry32 state (core/rng `BufferRng`).</summary>
        public const int Rng = 11;
        /// <summary>K-40: W4/W7 are open while `turn &lt; openShutterUntil`.</summary>
        public const int OpenShutterUntil = 12;
        public const int WrongCount = 13;
        public const int OverWallCount = 14;
        public const int RailCount = 15;
        /// <summary>STATE_FLAG bits.</summary>
        public const int Flags = 16;
        /// <summary>K-43 `movesSpent` (Faz 2R, §2R.4): non-cancelled drag moves of this attempt; Undo −1, Söküm keeps it. Not hashed.</summary>
        public const int MovesSpent = 17;
    }

    public static class StateFlag
    {
        public const int Won = 1;
        public const int Lost = 2;
        public const int Deadlocked = 4;
    }

    /// <summary>Piece record fields (stride 9).</summary>
    public static class PF
    {
        public const int Shape = 0;
        public const int Color = 1;
        public const int Zone = 2;
        public const int X = 3;
        public const int Y = 4;
        public const int Seg = 5;
        public const int Flags = 6;
        public const int Counter = 7;
        public const int ArrivedTurn = 8;
    }

    /// <summary>Gap record fields (stride 3). `Phase` holds the shutter phase, or the slider direction (±1).</summary>
    public static class GF
    {
        public const int Open = 0;
        public const int Y = 1;
        public const int Phase = 2;
    }

    /// <summary>Obstacle record fields (stride 2): crate hp / bag alive (1, 0) / hidden item present (1, 0).</summary>
    public static class OF
    {
        public const int Hp = 0;
        public const int Aux = 1;
    }

    /// <summary>Piece flag bits (state `flags` field), in PieceFlags.All order.</summary>
    public static class FlagBit
    {
        public const int Glass = 1 << 0;
        public const int Mortar = 1 << 1;
        public const int Balloon = 1 << 2;
        public const int Chained = 1 << 3;
        public const int Wet = 1 << 4;
        public const int Locked = 1 << 5;
        public const int Debris = 1 << 6;
        public const int Stuck = 1 << 7;
        public const int Wrapped = 1 << 8;
        public const int Scissors = 1 << 9;
        public const int Bonus = 1 << 10;
        public const int Slippery = 1 << 11;

        /// <summary>`FLAG_BIT[flag]`; throws on an unknown flag name.</summary>
        public static int Of(string flag)
        {
            int i = PieceFlags.IndexOf(flag);
            if (i < 0) throw new ArgumentOutOfRangeException(nameof(flag), "unknown piece flag " + flag);
            return 1 << i;
        }
    }

    public sealed class StateCounts
    {
        /// <summary>Segments S.</summary>
        public readonly int Segments;
        /// <summary>Piece slots P (static pieces + D2 help slots).</summary>
        public readonly int Pieces;
        public readonly int Gaps;
        public readonly int Obstacles;
        /// <summary>Screws and keys (subset of obstacles).</summary>
        public readonly int Hidden;
        public readonly int Goals;

        public StateCounts(int segments, int pieces, int gaps, int obstacles, int hidden, int goals)
        {
            Segments = segments;
            Pieces = pieces;
            Gaps = gaps;
            Obstacles = obstacles;
            Hidden = hidden;
            Goals = goals;
        }

        public bool SameAs(StateCounts o) => Segments == o.Segments && Pieces == o.Pieces && Gaps == o.Gaps && Obstacles == o.Obstacles && Hidden == o.Hidden && Goals == o.Goals;
    }

    /// <summary>Offsets of every section in the buffer (TECH §2.4 table).</summary>
    public sealed class StateLayout
    {
        public readonly int Size;
        public readonly StateCounts Counts;
        /// <summary>wy × rows: 0 empty · pieceId + 1 · −(obstacleIndex + 1) (crate, bag). Index `y * wy + x`.</summary>
        public readonly int YardOcc;
        /// <summary>S × ws × hs: 0 · pieceId + 1 (debris included) · −1 trowel cell. Index `seg * ws·hs + sy * ws + sx`.</summary>
        public readonly int SiteOcc;
        /// <summary>S × ws: per segment and column, bit mask of correctly filled plan rows (K-34).</summary>
        public readonly int Filled;
        /// <summary>S × ws: per segment and column, plan rows holding debris or a stuck mortar block (K-34 `dotFree`, E-43).</summary>
        public readonly int WrongOcc;
        public readonly int Pieces;
        public readonly int Gaps;
        public readonly int Obstacles;
        public readonly int Hidden;
        public readonly int Goals;
        /// <summary>Q = P slots: queued piece ids in FIFO order (K-26).</summary>
        public readonly int Queue;
        /// <summary>S: revealed `?` cells, ws·hs-bit local mask per segment (K-32; up to 32 bits).</summary>
        public readonly int Revealed;

        internal StateLayout(int size, StateCounts counts, int yardOcc, int siteOcc, int filled, int wrongOcc, int pieces, int gaps,
            int obstacles, int hidden, int goals, int queue, int revealed)
        {
            Size = size;
            Counts = counts;
            YardOcc = yardOcc;
            SiteOcc = siteOcc;
            Filled = filled;
            WrongOcc = wrongOcc;
            Pieces = pieces;
            Gaps = gaps;
            Obstacles = obstacles;
            Hidden = hidden;
            Goals = goals;
            Queue = queue;
            Revealed = revealed;
        }

        public bool SameAs(StateLayout o)
        {
            return Size == o.Size && Counts.SameAs(o.Counts) && YardOcc == o.YardOcc && SiteOcc == o.SiteOcc && Filled == o.Filled
                && WrongOcc == o.WrongOcc && Pieces == o.Pieces && Gaps == o.Gaps && Obstacles == o.Obstacles && Hidden == o.Hidden
                && Goals == o.Goals && Queue == o.Queue && Revealed == o.Revealed;
        }
    }

    /// <summary>
    /// Mutable game state: ONE int[] buffer + the shared, immutable CompiledLevel (TECH_DESIGN §2.4, D-052).
    /// Copying a state is `(int[])Buf.Clone()` (≈ 2–4 KB); the RNG state lives in the buffer, so a snapshot replays the same
    /// future. Hot code uses the small accessors in `State`; UI/debug code uses `State.ReadPiece`.
    ///
    /// Coordinates: yard pieces store the GLOBAL anchor (x 0 … wy−1, y 0 … hy−1). Site pieces store x as the global column
    /// (wy … wy+ws−1) and y as the segment-local plan row `sy` (board row = sy + elev, K-24), so they move with the elevator
    /// frame. Array sizes follow the level geometry (`Lvl.Geo`, K-49, TECH §2R.1): yardOcc `wy × rows`, siteOcc
    /// `S × ws × hs`, filled / wrongOcc `S × ws` row masks, revealed `S` (ws · hs ≤ 32 bits).
    /// </summary>
    public sealed class GameState
    {
        /// <summary>Shared, never copied.</summary>
        public readonly CompiledLevel Lvl;
        /// <summary>Every mutable field; layout in `Lvl.Layout`.</summary>
        public int[] Buf;

        public GameState(CompiledLevel lvl, int[] buf)
        {
            Lvl = lvl;
            Buf = buf;
        }
    }

    /// <summary>Read-only piece view for UI/debug (allocates; not for hot paths, TECH §2.5).</summary>
    public sealed class PieceView
    {
        public readonly int Id;
        public readonly string Shape;
        public readonly string Color;
        public readonly string Zone;
        /// <summary>Yard: global anchor. Site: global column (wy …) and segment-local plan row.</summary>
        public readonly int X;
        public readonly int Y;
        public readonly int Seg;
        public readonly string[] Flags;
        public readonly int WetLeft;
        public readonly bool Locked;
        public readonly bool Debris;
        public readonly bool Stuck;
        public readonly int ArrivedTurn;

        public PieceView(int id, string shape, string color, string zone, int x, int y, int seg, string[] flags, int wetLeft, bool locked,
            bool debris, bool stuck, int arrivedTurn)
        {
            Id = id;
            Shape = shape;
            Color = color;
            Zone = zone;
            X = x;
            Y = y;
            Seg = seg;
            Flags = flags;
            WetLeft = wetLeft;
            Locked = locked;
            Debris = debris;
            Stuck = stuck;
            ArrivedTurn = arrivedTurn;
        }
    }

    public static class State
    {
        public const int HeaderSize = 18;
        public const int PieceStride = 9;
        public const int GapStride = 3;
        public const int ObstacleStride = 2;

        /// <summary>Site cell value of a Golden Trowel fill (K-33).</summary>
        public const int SiteTrowel = -1;

        /// <summary>Deprecated: frame rows; yardOcc has `geo.Rows` rows (TECH §2R.1).</summary>
        [Obsolete("yardOcc has geo.Rows rows (TECH §2R.1)")] public const int YardOccRows = Geometry.MaxRows;

        /// <summary>Buffer layout for the level's counts and geometry (default geometry: the pre-2R layout, word for word).</summary>
        public static StateLayout ComputeLayout(StateCounts counts, BoardGeo? geo = null)
        {
            geo ??= Geometry.DefaultGeo;
            int at = HeaderSize;
            int Take(int n)
            {
                int start = at;
                at += n;
                return start;
            }
            int yardOcc = Take(geo.Wy * geo.Rows);
            int siteOcc = Take(counts.Segments * geo.SegCells);
            int filled = Take(counts.Segments * geo.Ws);
            int wrongOcc = Take(counts.Segments * geo.Ws);
            int pieces = Take(counts.Pieces * PieceStride);
            int gaps = Take(counts.Gaps * GapStride);
            int obstacles = Take(counts.Obstacles * ObstacleStride);
            int hidden = Take(counts.Hidden);
            int goals = Take(Math.Max(3, counts.Goals));
            int queue = Take(counts.Pieces);
            int revealed = Take(counts.Segments);
            return new StateLayout(at, counts, yardOcc, siteOcc, filled, wrongOcc, pieces, gaps, obstacles, hidden, goals, queue, revealed);
        }

        // --- header ------------------------------------------------------------------------------------------------------

        public static int Hdr(GameState s, int field) => s.Buf[field];
        public static void SetHdr(GameState s, int field, int value) => s.Buf[field] = value;

        // --- pieces ------------------------------------------------------------------------------------------------------

        public static int PieceBase(GameState s, int id) => s.Lvl.Layout.Pieces + id * PieceStride;
        public static int PieceField(GameState s, int id, int field) => s.Buf[s.Lvl.Layout.Pieces + id * PieceStride + field];
        public static void SetPieceField(GameState s, int id, int field, int value) => s.Buf[s.Lvl.Layout.Pieces + id * PieceStride + field] = value;
        public static int PieceShape(GameState s, int id) => PieceField(s, id, PF.Shape);
        public static int PieceColor(GameState s, int id) => PieceField(s, id, PF.Color);
        public static int PieceZone(GameState s, int id) => PieceField(s, id, PF.Zone);
        public static int PieceX(GameState s, int id) => PieceField(s, id, PF.X);
        public static int PieceY(GameState s, int id) => PieceField(s, id, PF.Y);
        public static int PieceSeg(GameState s, int id) => PieceField(s, id, PF.Seg);
        public static int PieceFlagBits(GameState s, int id) => PieceField(s, id, PF.Flags);
        public static void SetPieceX(GameState s, int id, int v) => SetPieceField(s, id, PF.X, v);
        public static void SetPieceY(GameState s, int id, int v) => SetPieceField(s, id, PF.Y, v);
        public static void SetPieceZone(GameState s, int id, int v) => SetPieceField(s, id, PF.Zone, v);

        /// <summary>`hasFlag(s, id, flag)` with the flag BIT (`FlagBit.Locked` …).</summary>
        /// <summary>
        /// Colour index of cell `i` (the shape's (y, x) cell order) of piece `id`: the static `CellColors` of a v3 two-tone block (Y9,
        /// K-57), else the piece's current colour.
        /// </summary>
        public static int PieceCellColor(GameState s, int id, int i)
        {
            var cc = s.Lvl.Pieces[id].CellColors;
            return cc != null && i < cc.Length ? cc[i] : PieceColor(s, id);
        }

        /// <summary>v3 Y9: the piece is a two-tone block (per-cell colours, K-57).</summary>
        public static bool IsTwoTone(GameState s, int id) => s.Lvl.Pieces[id].CellColors != null;

        public static bool HasFlag(GameState s, int id, int flagBit) => (PieceFlagBits(s, id) & flagBit) != 0;

        public static void SetFlag(GameState s, int id, int flagBit, bool on)
        {
            int f = PieceFlagBits(s, id);
            SetPieceField(s, id, PF.Flags, on ? f | flagBit : f & ~flagBit);
        }

        public static int FlagsToBits(IEnumerable<string> flags)
        {
            int m = 0;
            foreach (var f in flags) m |= FlagBit.Of(f);
            return m;
        }

        public static string[] BitsToFlags(int bits)
        {
            var o = new List<string>();
            for (int i = 0; i < PieceFlags.All.Length; i++) if ((bits & (1 << i)) != 0) o.Add(PieceFlags.All[i]);
            return o.ToArray();
        }

        public static PieceView ReadPiece(GameState s, int id)
        {
            int flags = PieceFlagBits(s, id);
            int color = PieceColor(s, id);
            int zone = PieceZone(s, id);
            return new PieceView(
                id,
                Shapes.ShapeByIndex(PieceShape(s, id)).Id,
                color >= 0 && color < ColorCodes.All.Length ? ColorCodes.All[color] : "W",
                zone >= 0 && zone < Zone.Names.Length ? Zone.Names[zone] : "gone",
                PieceX(s, id),
                PieceY(s, id),
                PieceSeg(s, id),
                BitsToFlags(flags),
                PieceField(s, id, PF.Counter),
                (flags & FlagBit.Locked) != 0,
                (flags & FlagBit.Debris) != 0,
                (flags & FlagBit.Stuck) != 0,
                PieceField(s, id, PF.ArrivedTurn));
        }

        // --- occupancy ---------------------------------------------------------------------------------------------------

        /// <summary>Yard cell (x 0 … wy−1, y 0 … rows−1): 0 empty · pieceId + 1 · −(obstacleIndex + 1).</summary>
        public static int YardOcc(GameState s, int x, int y) => s.Buf[s.Lvl.Layout.YardOcc + y * s.Lvl.Geo.Wy + x];
        public static void SetYardOcc(GameState s, int x, int y, int v) => s.Buf[s.Lvl.Layout.YardOcc + y * s.Lvl.Geo.Wy + x] = v;

        /// <summary>Site cell of a segment (local sx 0 … ws−1, sy 0 … hs−1): 0 · pieceId + 1 · SiteTrowel.</summary>
        public static int SiteOcc(GameState s, int seg, int sx, int sy)
        {
            var geo = s.Lvl.Geo;
            return s.Buf[s.Lvl.Layout.SiteOcc + seg * geo.SegCells + sy * geo.Ws + sx];
        }

        public static void SetSiteOcc(GameState s, int seg, int sx, int sy, int v)
        {
            var geo = s.Lvl.Geo;
            s.Buf[s.Lvl.Layout.SiteOcc + seg * geo.SegCells + sy * geo.Ws + sx] = v;
        }

        public static int FilledMask(GameState s, int seg, int sx) => s.Buf[s.Lvl.Layout.Filled + seg * s.Lvl.Geo.Ws + sx];
        public static void SetFilledMask(GameState s, int seg, int sx, int v) => s.Buf[s.Lvl.Layout.Filled + seg * s.Lvl.Geo.Ws + sx] = v;
        public static int WrongOccMask(GameState s, int seg, int sx) => s.Buf[s.Lvl.Layout.WrongOcc + seg * s.Lvl.Geo.Ws + sx];
        public static void SetWrongOccMask(GameState s, int seg, int sx, int v) => s.Buf[s.Lvl.Layout.WrongOcc + seg * s.Lvl.Geo.Ws + sx] = v;

        // --- gaps, obstacles, hidden items, goals, queue -------------------------------------------------------------------

        public static int GapField(GameState s, int gap, int field) => s.Buf[s.Lvl.Layout.Gaps + gap * GapStride + field];
        public static void SetGapField(GameState s, int gap, int field, int v) => s.Buf[s.Lvl.Layout.Gaps + gap * GapStride + field] = v;
        public static int ObstacleField(GameState s, int obstacle, int field) => s.Buf[s.Lvl.Layout.Obstacles + obstacle * ObstacleStride + field];
        public static void SetObstacleField(GameState s, int obstacle, int field, int v) => s.Buf[s.Lvl.Layout.Obstacles + obstacle * ObstacleStride + field] = v;
        public static bool HiddenCollected(GameState s, int hiddenIndex) => s.Buf[s.Lvl.Layout.Hidden + hiddenIndex] != 0;
        public static void SetHiddenCollected(GameState s, int hiddenIndex, bool collected) => s.Buf[s.Lvl.Layout.Hidden + hiddenIndex] = collected ? 1 : 0;
        public static int GoalValue(GameState s, int goal) => s.Buf[s.Lvl.Layout.Goals + goal];
        public static void SetGoalValue(GameState s, int goal, int v) => s.Buf[s.Lvl.Layout.Goals + goal] = v;
        public static int RevealedMask(GameState s, int seg) => s.Buf[s.Lvl.Layout.Revealed + seg];
        public static void SetRevealedMask(GameState s, int seg, int v) => s.Buf[s.Lvl.Layout.Revealed + seg] = v;

        /// <summary>Queued piece ids, oldest first (K-26). Allocates.</summary>
        public static int[] QueueIds(GameState s)
        {
            int n = Hdr(s, H.QueueLen);
            var o = new int[n];
            for (int i = 0; i < n; i++) o[i] = s.Buf[s.Lvl.Layout.Queue + i];
            return o;
        }

        /// <summary>Appends to the end of the truck queue (K-17 step 3, K-25, K-26) and sets the piece zone to `queue`.</summary>
        public static void EnqueuePiece(GameState s, int id)
        {
            int n = Hdr(s, H.QueueLen);
            s.Buf[s.Lvl.Layout.Queue + n] = id;
            SetHdr(s, H.QueueLen, n + 1);
            SetPieceZone(s, id, Zone.Queue);
        }

        /// <summary>Removes the queue entry at `index`, keeping the order of the others (FIFO, K-26).</summary>
        public static int RemoveQueueAt(GameState s, int index)
        {
            int n = Hdr(s, H.QueueLen);
            int b = s.Lvl.Layout.Queue;
            int id = index >= 0 && index < n ? s.Buf[b + index] : -1;
            for (int i = index; i < n - 1; i++) s.Buf[b + i] = s.Buf[b + i + 1];
            s.Buf[b + n - 1] = 0;
            SetHdr(s, H.QueueLen, n - 1);
            return id;
        }

        // --- creation, copy, encoding ---------------------------------------------------------------------------------------

        /// <summary>Plan rows of segment `seg`, column `sx`, that belong to the plan area (colour, `?` or `.`).</summary>
        public static int PlanAreaMask(CompiledLevel lvl, int seg, int sx)
        {
            if (seg < 0 || seg >= lvl.Segments.Length) return 0;
            var sg = lvl.Segments[seg];
            int plan = sx >= 0 && sx < sg.PlanMask.Length ? sg.PlanMask[sx] : 0;
            int dot = sx >= 0 && sx < sg.DotMask.Length ? sg.DotMask[sx] : 0;
            return plan | dot;
        }

        /// <summary>The level's start state (K-25 batch 0 in the yard, debris on its segment, timers at their start values).</summary>
        public static GameState CreateInitialState(CompiledLevel lvl)
        {
            var s = new GameState(lvl, new int[lvl.Layout.Size]);
            SetHdr(s, H.MovesLeft, lvl.Moves);
            SetHdr(s, H.Rng, unchecked((int)lvl.Seed)); // `lvl.seed | 0`
            if (lvl.Elevator != null)
            {
                SetHdr(s, H.Elev, lvl.Elevator.Start);
                SetHdr(s, H.ElevDir, lvl.Elevator.Dir);
            }
            // v3 Y10 (K-58): pallet layers share their cells; layers are written bottom-up so the top layer owns the occupancy
            var order = lvl.Pieces;
            if (lvl.HasStacks)
            {
                order = (CompiledPiece[])lvl.Pieces.Clone();
                Array.Sort(order, (a, b) => a.StackZ != b.StackZ ? a.StackZ.CompareTo(b.StackZ) : a.Id.CompareTo(b.Id));
            }
            foreach (var p in order)
            {
                SetPieceField(s, p.Id, PF.Shape, p.ShapeIndex);
                SetPieceField(s, p.Id, PF.Color, p.ColorIndex);
                SetPieceField(s, p.Id, PF.Zone, p.StartZone);
                SetPieceField(s, p.Id, PF.X, p.X);
                SetPieceField(s, p.Id, PF.Y, p.Y);
                SetPieceField(s, p.Id, PF.Seg, p.Segment);
                SetPieceField(s, p.Id, PF.Flags, p.Flags);
                SetPieceField(s, p.Id, PF.Counter, p.WetMoves);
                SetPieceField(s, p.Id, PF.ArrivedTurn, -1);
                var shape = Shapes.ShapeByIndex(p.ShapeIndex);
                if (p.StartZone == Zone.Yard)
                {
                    foreach (var c in shape.Cells) SetYardOcc(s, p.X + c.X, p.Y + c.Y, p.Id + 1);
                }
                else if (p.StartZone == Zone.Site)
                {
                    foreach (var c in shape.Cells)
                    {
                        int sx = p.X - lvl.Geo.SiteX + c.X;
                        int sy = p.Y + c.Y;
                        SetSiteOcc(s, p.Segment, sx, sy, p.Id + 1);
                        if (((PlanAreaMask(lvl, p.Segment, sx) >> sy) & 1) != 0)
                            SetWrongOccMask(s, p.Segment, sx, WrongOccMask(s, p.Segment, sx) | (1 << sy));
                    }
                }
            }
            for (int i = 0; i < lvl.Obstacles.Length; i++)
            {
                var o = lvl.Obstacles[i];
                if (o.IsCrate)
                {
                    SetObstacleField(s, i, OF.Hp, o.Hp);
                    SetYardOcc(s, o.X, o.Y, -(i + 1));
                }
                else if (o.IsCementBag)
                {
                    SetObstacleField(s, i, OF.Hp, 1);
                    SetYardOcc(s, o.X, o.Y, -(i + 1));
                }
                else SetObstacleField(s, i, OF.Hp, 1);
            }
            for (int i = 0; i < lvl.Gaps.Length; i++)
            {
                var g = lvl.Gaps[i];
                SetGapField(s, i, GF.Y, g.Y);
                if (g.Type == "shutter")
                {
                    SetGapField(s, i, GF.Phase, g.Phase);
                    SetGapField(s, i, GF.Open, (g.Phase / g.Period) % 2 == 0 ? 1 : 0);
                }
                else if (g.Type == "slider")
                {
                    SetGapField(s, i, GF.Phase, g.Dir);
                    SetGapField(s, i, GF.Open, 1);
                }
                else SetGapField(s, i, GF.Open, g.Type == "locked" ? 0 : 1);
            }
            return s;
        }

        /// <summary>Independent copy; changes never leak back (TECH §2.4).</summary>
        public static GameState CloneState(GameState s) => new GameState(s.Lvl, (int[])s.Buf.Clone());

        private const string B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        /// <summary>
        /// Compact, URL-safe text form of the buffer (int32 words in little-endian byte order — the platform order of every
        /// supported device in the TS code — as base64url without padding).
        /// </summary>
        public static string EncodeState(GameState s)
        {
            var bytes = new byte[s.Buf.Length * 4];
            for (int i = 0; i < s.Buf.Length; i++)
            {
                uint w = unchecked((uint)s.Buf[i]);
                bytes[i * 4] = (byte)(w & 255);
                bytes[i * 4 + 1] = (byte)((w >> 8) & 255);
                bytes[i * 4 + 2] = (byte)((w >> 16) & 255);
                bytes[i * 4 + 3] = (byte)(w >> 24);
            }
            var sb = new System.Text.StringBuilder((bytes.Length + 2) / 3 * 4);
            for (int i = 0; i < bytes.Length; i += 3)
            {
                int a = bytes[i];
                int b = i + 1 < bytes.Length ? bytes[i + 1] : 0;
                int c = i + 2 < bytes.Length ? bytes[i + 2] : 0;
                int n = (a << 16) | (b << 8) | c;
                int left = bytes.Length - i;
                sb.Append(B64[(n >> 18) & 63]);
                sb.Append(B64[(n >> 12) & 63]);
                if (left > 1) sb.Append(B64[(n >> 6) & 63]);
                if (left > 2) sb.Append(B64[n & 63]);
            }
            return sb.ToString();
        }

        /// <summary>Inverse of `EncodeState`; throws when the text does not fit the level's layout.</summary>
        public static GameState DecodeState(CompiledLevel lvl, string text)
        {
            int byteLength = lvl.Layout.Size * 4;
            var bytes = new byte[byteLength];
            int bi = 0;
            for (int i = 0; i < text.Length; i += 4)
            {
                int chunkLen = Math.Min(4, text.Length - i);
                int n = 0;
                for (int k = 0; k < 4; k++)
                {
                    int v = 0;
                    if (k < chunkLen)
                    {
                        char ch = text[i + k];
                        v = B64.IndexOf(ch);
                        if (v < 0) throw new ArgumentException("decodeState: bad character \"" + ch + "\"");
                    }
                    n = (n << 6) | v;
                }
                int produced = Math.Max(0, chunkLen - 1);
                for (int k = 0; k < produced; k++)
                {
                    if (bi >= byteLength) throw new ArgumentException("decodeState: text longer than the layout");
                    bytes[bi++] = (byte)((n >> (16 - 8 * k)) & 255);
                }
            }
            if (bi != byteLength) throw new ArgumentException("decodeState: " + bi + " bytes, layout needs " + byteLength);
            var buf = new int[lvl.Layout.Size];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = unchecked((int)((uint)bytes[i * 4] | ((uint)bytes[i * 4 + 1] << 8) | ((uint)bytes[i * 4 + 2] << 16) | ((uint)bytes[i * 4 + 3] << 24)));
            return new GameState(lvl, buf);
        }
    }
}
