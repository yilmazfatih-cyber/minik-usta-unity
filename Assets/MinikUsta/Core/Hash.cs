#nullable enable
// Port of src/core/hash.ts (web repo, source of truth). Uint32Array tables → uint[]; every mix is 32-bit, bit-identical.
using System;
using System.Collections.Generic;
using MinikUsta.Core.Level;

namespace MinikUsta.Core
{
    public sealed class ZobristTables
    {
        /// <summary>Lane 0 / lane 1 position keys.</summary>
        public readonly uint[] Pos0;
        public readonly uint[] Pos1;
        /// <summary>Lane 0 / lane 1 scalar field keys (index = HashField order, see `HashedFieldOffsets`).</summary>
        public readonly uint[] Field0;
        public readonly uint[] Field1;
        /// <summary>`L`: cycle length of the timed mechanics (§2.6); 1 without them.</summary>
        public readonly int Cycle;

        public ZobristTables(uint[] pos0, uint[] pos1, uint[] field0, uint[] field1, int cycle)
        {
            Pos0 = pos0;
            Pos1 = pos1;
            Field0 = field0;
            Field1 = field1;
            Cycle = cycle;
        }
    }

    /// <summary>
    /// State hash: Zobrist, 64 bit as two 32-bit lanes, computed on demand (TECH_DESIGN §2.6, D-052).
    ///
    /// h = ⨁ over pieces in yard/site/queue of K(class(p), position(p)) ⊕ ⨁ over hashed scalar fields of F(field, value)
    ///
    /// - class(p) = (canonical shape, colour, flags, counter) — NOT the piece id, so swapping two identical blocks gives
    ///   the same hash (the solver never opens a symmetric state twice).
    /// - position in the 8 × 10 maximum frame for every geometry (TECH §2R.1): yard anchor (y·8 + x, 0…79), site
    ///   (80 + seg·32 + sy·4 + sx, 80…239; sx = x − wy) or queue slot (240 + index, FIFO order).
    /// - K and F mix a fixed random table entry (splitmix32, constant seed → deterministic for golden tests) with the
    ///   value through the bijective `fmix32`, so distinct values on one position never cancel.
    /// - Hashed scalars: `turn mod L`, active/front segment, carousel t, elevator offset and direction, delivery cursor,
    ///   "K-40 open shutter active", gap open/y/phase, obstacle hp/aux, hidden items, goal counters and the `filled`
    ///   masks (Golden Trowel cells are not pieces). Pending (undelivered) and gone pieces are skipped; `wrongOcc`,
    ///   counters such as movesLeft/combo and arrivedTurn are not hashed (§2.4, D-052).
    /// </summary>
    public static class Hash
    {
        /// <summary>Fixed seed of the table generator (changing it changes every golden hash).</summary>
        public const int ZobristSeed = 0x5eed2b1d;

        /// <summary>Frame positions (§2R.1): yard 0..79 (`y·8 + x`), site 80..239 (`80 + seg·32 + sy·4 + sx`), queue from 240.</summary>
        public const int PosSite = Geometry.MaxCols * Geometry.MaxRows;
        public const int PosQueue = PosSite + Geometry.MaxSegments * Geometry.SegmentSlots;

        /// <summary>
        /// Zobrist position of a piece record (§2R.1): yard anchor `y·8 + x`, site `80 + seg·32 + sy·4 + (x − siteX)`; the
        /// queue slot `240 + i` is added by `HashState`. The same frame for every geometry (`siteX` = `geo.SiteX`).
        /// </summary>
        public static int PiecePosition(int siteX, int zone, int x, int y, int seg)
        {
            if (zone == Zone.Yard) return y * Geometry.MaxCols + x;
            return PosSite + seg * Geometry.SegmentSlots + y * Geometry.MaxSiteCols + (x - siteX);
        }

        /// <summary>Scalar buffer offsets that enter the hash (header fields are handled separately); `filled` has S × ws masks.</summary>
        public static int[] HashedFieldOffsets(StateLayout layout, BoardGeo? geo = null)
        {
            geo ??= Geometry.DefaultGeo;
            var c = layout.Counts;
            var o = new List<int>();
            for (int i = 0; i < c.Gaps * State.GapStride; i++) o.Add(layout.Gaps + i);
            for (int i = 0; i < c.Obstacles * State.ObstacleStride; i++) o.Add(layout.Obstacles + i);
            for (int i = 0; i < c.Hidden; i++) o.Add(layout.Hidden + i);
            for (int i = 0; i < 3; i++) o.Add(layout.Goals + i);
            for (int i = 0; i < c.Segments * geo.Ws; i++) o.Add(layout.Filled + i);
            return o.ToArray();
        }

        /// <summary>Header terms: turn mod L, activeSeg, frontSeg, carouselT, elev, elevDir, deliveryCursor, shutterActive.</summary>
        private const int HeaderTerms = 8;

        public static ZobristTables BuildZobrist(StateLayout layout, int cycle, BoardGeo? geo = null)
        {
            geo ??= Geometry.DefaultGeo;
            var next = new Splitmix32(ZobristSeed);
            int positions = PosQueue + layout.Counts.Pieces;
            int fields = HeaderTerms + HashedFieldOffsets(layout, geo).Length;
            uint[] Fill(int n)
            {
                var a = new uint[n];
                for (int i = 0; i < n; i++) a[i] = next.Next();
                return a;
            }
            var pos0 = Fill(positions);
            var pos1 = Fill(positions);
            var field0 = Fill(fields);
            var field1 = Fill(fields);
            return new ZobristTables(pos0, pos1, field0, field1, Math.Max(1, cycle));
        }

        private const int Mul0 = unchecked((int)0x9e3779b1u);
        private const int Mul1 = unchecked((int)0x85ebca77u);

        /// <summary>Packs the piece class into one int: canonical shape (6 bits) | colour (3) | flags (8) | counter (8).</summary>
        private static int PieceClass(int[] buf, int b)
        {
            return (buf[b + PF.Shape] & 63)
                | ((buf[b + PF.Color] & 7) << 6)
                | ((buf[b + PF.Flags] & 255) << 9)
                | ((buf[b + PF.Counter] & 255) << 17);
        }

        /// <summary>Writes the 64-bit hash into `out` ([lane0, lane1]) and returns it. Pass `out` to avoid an allocation.</summary>
        public static uint[] HashState(GameState s, uint[]? @out = null)
        {
            @out ??= new uint[2];
            var buf = s.Buf;
            var lvl = s.Lvl;
            var z = lvl.Zobrist;
            var layout = lvl.Layout;
            uint h0 = 0;
            uint h1 = 0;
            void MixField(int index, int value)
            {
                unchecked
                {
                    h0 ^= Rng.Fmix32((value * Mul0) ^ (int)z.Field0[index]);
                    h1 ^= Rng.Fmix32((value * Mul1) ^ (int)z.Field1[index]);
                }
            }
            int turn = buf[H.Turn];
            MixField(0, turn % z.Cycle);
            MixField(1, buf[H.ActiveSeg]);
            MixField(2, buf[H.FrontSeg]);
            MixField(3, buf[H.CarouselT]);
            MixField(4, buf[H.Elev]);
            MixField(5, buf[H.ElevDir]);
            MixField(6, buf[H.DeliveryCursor]);
            MixField(7, turn < buf[H.OpenShutterUntil] ? 1 : 0);
            var offsets = lvl.HashFields;
            for (int i = 0; i < offsets.Length; i++) MixField(HeaderTerms + i, buf[offsets[i]]);

            int queueLen = buf[H.QueueLen];
            for (int q = 0; q < queueLen; q++)
            {
                int id = buf[layout.Queue + q];
                int cls = PieceClass(buf, layout.Pieces + id * State.PieceStride);
                unchecked
                {
                    h0 ^= Rng.Fmix32((cls * Mul0) ^ (int)z.Pos0[PosQueue + q]);
                    h1 ^= Rng.Fmix32((cls * Mul1) ^ (int)z.Pos1[PosQueue + q]);
                }
            }
            int P = layout.Counts.Pieces;
            int siteX = lvl.Geo.SiteX;
            for (int id = 0; id < P; id++)
            {
                int b = layout.Pieces + id * State.PieceStride;
                int zone = buf[b + PF.Zone];
                // queue handled above (order matters); pending and gone pieces are not hashed
                if (zone != Zone.Yard && zone != Zone.Site) continue;
                int pos = PiecePosition(siteX, zone, buf[b + PF.X], buf[b + PF.Y], buf[b + PF.Seg]);
                int cls = PieceClass(buf, b);
                unchecked
                {
                    h0 ^= Rng.Fmix32((cls * Mul0) ^ (int)z.Pos0[pos]);
                    h1 ^= Rng.Fmix32((cls * Mul1) ^ (int)z.Pos1[pos]);
                }
            }
            @out[0] = h0;
            @out[1] = h1;
            return @out;
        }

        /// <summary>16 hex digits, lane 1 first (debug panel, golden files).</summary>
        public static string HashHex(GameState s)
        {
            var h = HashState(s);
            return h[1].ToString("x8") + h[0].ToString("x8");
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        /// <summary>Least common multiple of positive integers (1 for an empty list).</summary>
        public static int Lcm(IEnumerable<int> values)
        {
            int acc = 1;
            foreach (int v in values)
            {
                if (v <= 0) continue;
                acc = (acc / Gcd(acc, v)) * v;
            }
            return acc;
        }
    }
}
