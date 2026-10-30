#nullable enable
// Port of src/core/rng.ts (web repo, source of truth). All arithmetic is 32-bit integer, bit-identical to the TS code:
// Math.imul → unchecked(int * int); x >>> n → (int)((uint)x >> n); (… ) | 0 → unchecked int; … >>> 0 → uint.
using System;

namespace MinikUsta.Core
{
    /// <summary>Random source used by core code and obstacle rules.</summary>
    public interface IRng
    {
        /// <summary>Next uint32 in [0, 2³²).</summary>
        uint NextU32();
        /// <summary>Next float in [0, 1).</summary>
        double Next();
        /// <summary>Next integer in [0, n); `n` must be positive.</summary>
        int NextInt(int n);
    }

    /// <summary>
    /// Seeded, deterministic random numbers (TECH_DESIGN §2.7). Core code never uses System.Random.
    ///
    /// - `mulberry32`: the game RNG. Its whole state is one int32, so it lives in the state buffer and snapshots replay
    ///   the same future (§2.4).
    /// - `splitmix32`: key generation (Zobrist tables, §2.6).
    /// - `hash32`: counter-based hashing for bot simulations (§11.2; config/events.json `rng.hash = "fmix32-chain-v1"`).
    /// </summary>
    public static class Rng
    {
        /// <summary>Increment of the mulberry32 Weyl sequence.</summary>
        public const int Mulberry32Increment = 0x6d2b79f5;

        /// <summary>Advances a mulberry32 state by one step (returns the new int32 state).</summary>
        public static int Mulberry32Next(int state) => unchecked(state + Mulberry32Increment);

        /// <summary>The uint32 output belonging to an already advanced mulberry32 state.</summary>
        public static uint Mulberry32Output(int state)
        {
            unchecked
            {
                int t = (state ^ (int)((uint)state >> 15)) * (1 | state);
                t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                return (uint)(t ^ (int)((uint)t >> 14));
            }
        }

        /// <summary>The shared mulberry32 step: advances `state`, returns the output (TS `makeRng`).</summary>
        internal static uint Step(ref int state)
        {
            state = Mulberry32Next(state);
            return Mulberry32Output(state);
        }

        internal static double ToUnit(uint u) => u / 4294967296.0;

        /// <summary>TS `nextInt`: `Math.floor((u / 2³²) * n)`; the TS bound check allows n ≤ 2³², C# takes an int.</summary>
        internal static int ToInt(uint u, int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "nextInt: bad bound " + n);
            return (int)Math.Floor(ToUnit(u) * n);
        }

        /// <summary>MurmurHash3 32-bit finaliser (bijective avalanche on uint32).</summary>
        public static uint Fmix32(int x)
        {
            unchecked
            {
                int h = x;
                h ^= (int)((uint)h >> 16);
                h *= (int)0x85ebca6bu;
                h ^= (int)((uint)h >> 13);
                h *= (int)0xc2b2ae35u;
                h ^= (int)((uint)h >> 16);
                return (uint)h;
            }
        }

        public static uint Fmix32(uint x) => Fmix32(unchecked((int)x));

        /// <summary>Name recorded in config/events.json `rng.hash`; changing the algorithm below needs a new version name.</summary>
        public const string Hash32Version = "fmix32-chain-v1";

        /// <summary>
        /// `fmix32-chain-v1`: hashes a list of 32-bit integers (order and length sensitive).
        ///
        /// h = fmix32(0x811c9dc5 ^ n), n = number of words; for each word w (as uint32): h = fmix32((h ^ w) + 0x9e3779b9).
        ///
        /// Every word must be an integer in [−2³¹, 2³²); larger values throw (a silent truncation would make device and
        /// server results drift apart, TECH §15 R-19). Reference vectors are pinned in RngTests.
        /// </summary>
        public static uint Hash32(params long[] words)
        {
            unchecked
            {
                uint h = Fmix32((int)0x811c9dc5u ^ words.Length);
                foreach (long w in words)
                {
                    if (w < -2147483648L || w > 4294967295L) throw new ArgumentOutOfRangeException(nameof(words), "hash32: bad word " + w);
                    uint wu = (uint)w; // `w >>> 0`
                    h = Fmix32((int)(h ^ wu) + (int)0x9e3779b9u);
                }
                return h;
            }
        }
    }

    /// <summary>A standalone mulberry32 generator (state kept in the object).</summary>
    public sealed class Mulberry32 : IRng
    {
        private int state;

        public Mulberry32(int seed)
        {
            state = seed;
        }

        /// <summary>The current int32 state (TS `state()`).</summary>
        public int State => state;

        public uint NextU32() => Rng.Step(ref state);
        public double Next() => Rng.ToUnit(NextU32());
        public int NextInt(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "nextInt: bad bound " + n);
            return Rng.ToInt(NextU32(), n);
        }
    }

    /// <summary>A mulberry32 generator whose state is `buf[index]` (the `rng` header field of the game state, §2.4).</summary>
    public sealed class BufferRng : IRng
    {
        private readonly int[] buf;
        private readonly int index;

        public BufferRng(int[] buf, int index)
        {
            this.buf = buf;
            this.index = index;
        }

        public uint NextU32()
        {
            int s = Rng.Mulberry32Next(buf[index]);
            buf[index] = s;
            return Rng.Mulberry32Output(s);
        }

        public double Next() => Rng.ToUnit(NextU32());
        public int NextInt(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "nextInt: bad bound " + n);
            return Rng.ToInt(NextU32(), n);
        }
    }

    /// <summary>splitmix32 generator (key generation). Returns uint32 values.</summary>
    public sealed class Splitmix32
    {
        private int state;

        public Splitmix32(int seed)
        {
            state = seed;
        }

        public uint Next()
        {
            unchecked
            {
                state = state + (int)0x9e3779b9u;
                int t = state ^ (int)((uint)state >> 16);
                t *= 0x21f0aaad;
                t ^= (int)((uint)t >> 15);
                t *= 0x735a2d97;
                return (uint)(t ^ (int)((uint)t >> 15));
            }
        }
    }
}
