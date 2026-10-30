#nullable enable
// Port of tests/core/rng.test.ts (TECH_DESIGN §2.7) plus node-generated cross-language vectors (p0_vectors.json).
using System;
using System.Collections.Generic;
using MinikUsta.Core.Tests.Fixtures;
using NUnit.Framework;

namespace MinikUsta.Core.Tests
{
    [TestFixture]
    public class RngTests
    {
        private static string Hex(uint n) => "0x" + n.ToString("x8");

        /// <summary>The widely published mulberry32 (bryc/PRNGs), used as an independent oracle.</summary>
        private sealed class ReferenceMulberry32
        {
            private int a;

            public ReferenceMulberry32(int seed)
            {
                a = seed;
            }

            public uint Next()
            {
                unchecked
                {
                    a = a + 0x6d2b79f5;
                    int t = (a ^ (int)((uint)a >> 15)) * (1 | a);
                    t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                    return (uint)(t ^ (int)((uint)t >> 14));
                }
            }
        }

        [Test]
        public void Mulberry32_matches_the_reference_algorithm_for_10000_outputs()
        {
            foreach (int seed in new[] { 0, 1, 4004, -1, 0x7fffffff })
            {
                var r = new ReferenceMulberry32(seed);
                var rng = new Mulberry32(seed);
                for (int i = 0; i < 10_000; i++) Assert.AreEqual(r.Next(), rng.NextU32());
            }
        }

        private static string[] Take(int seed)
        {
            var r = new Mulberry32(seed);
            var o = new string[5];
            for (int i = 0; i < 5; i++) o[i] = Hex(r.NextU32());
            return o;
        }

        [Test]
        public void Mulberry32_reference_vectors_are_pinned()
        {
            CollectionAssert.AreEqual(new[] { "0x4434b462", "0x00159c37", "0x39285b08", "0x256d8104", "0x77a2cbd4" }, Take(0));
            CollectionAssert.AreEqual(new[] { "0xdc5f7186", "0x913122ec", "0x27b0b0df", "0x1586ba6f", "0x6bc904c6" }, Take(4004));
        }

        [Test]
        public void Buffer_rng_keeps_its_whole_state_in_one_int32_slot_snapshot_replays_the_same_future()
        {
            var buf = new int[4];
            buf[2] = 4004;
            var a = new BufferRng(buf, 2);
            a.NextU32();
            var snapshot = (int[])buf.Clone();
            var first = new[] { a.NextU32(), a.NextU32(), a.NextU32() };
            var b = new BufferRng(snapshot, 2);
            CollectionAssert.AreEqual(first, new[] { b.NextU32(), b.NextU32(), b.NextU32() });
            Assert.AreEqual(0, buf[0]);
            Assert.AreEqual(0, buf[3]);
        }

        [Test]
        public void Step_output_decomposition_equals_the_closure_generator()
        {
            int s = 77;
            var r = new Mulberry32(77);
            for (int i = 0; i < 100; i++)
            {
                s = Rng.Mulberry32Next(s);
                Assert.AreEqual(r.NextU32(), Rng.Mulberry32Output(s));
            }
            Assert.AreEqual(s, r.State);
        }

        [Test]
        public void Next_is_in_0_1_and_nextInt_stays_in_range()
        {
            var r = new Mulberry32(9);
            for (int i = 0; i < 1000; i++)
            {
                double f = r.Next();
                Assert.GreaterOrEqual(f, 0);
                Assert.Less(f, 1);
                int n = r.NextInt(7);
                Assert.IsTrue(n >= 0 && n < 7);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => r.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => r.NextInt(-3));
        }

        [Test]
        public void Next_and_nextInt_doubles_equal_the_TS_values_bit_for_bit()
        {
            var v = TestPaths.Vectors["rng"]!;
            var r = new Mulberry32(9);
            foreach (var pair in v["seed9"]!.Items)
            {
                Assert.AreEqual(pair[0].AsDouble, r.Next());
                Assert.AreEqual(pair[1].AsInt, r.NextInt(7));
            }
            var m5 = new Mulberry32(-5);
            foreach (var h in v["seedMinus5U32"]!.Items) Assert.AreEqual(h.AsString, Hex(m5.NextU32()));
        }

        [Test]
        public void Splitmix32_reference_vectors_are_pinned()
        {
            string[] TakeS(int seed)
            {
                var g = new Splitmix32(seed);
                var o = new string[4];
                for (int i = 0; i < 4; i++) o[i] = Hex(g.Next());
                return o;
            }
            CollectionAssert.AreEqual(new[] { "0x64625032", "0xd9c0799c", "0xaf362e10", "0x7fa88912" }, TakeS(0));
            CollectionAssert.AreEqual(new[] { "0x20e44818", "0x0895a923", "0x1339a01f", "0xb4e3841a" }, TakeS(42));
        }

        [Test]
        public void Fmix32_matches_the_MurmurHash3_finaliser()
        {
            Assert.AreEqual("0x00000000", Hex(Rng.Fmix32(0)));
            Assert.AreEqual("0x514e28b7", Hex(Rng.Fmix32(1)));
            Assert.AreEqual("0xe37cd1bc", Hex(Rng.Fmix32(0x12345678)));
        }

        [Test]
        public void Hash32_fmix32_chain_v1_reference_vectors_are_pinned_device_and_server_must_agree_TECH_15_R19()
        {
            Assert.AreEqual("fmix32-chain-v1", Rng.Hash32Version);
            var vectors = new List<KeyValuePair<long[], string>>
            {
                new KeyValuePair<long[], string>(new long[0], "0xab3e7c0b"),
                new KeyValuePair<long[], string>(new long[] { 0 }, "0xc3febd23"),
                new KeyValuePair<long[], string>(new long[] { 0, 0 }, "0x64cdefdc"),
                new KeyValuePair<long[], string>(new long[] { 0, 0, 0 }, "0x8743fdcd"),
                new KeyValuePair<long[], string>(new long[] { 1, 2, 3 }, "0x42c6bcb5"),
                new KeyValuePair<long[], string>(new long[] { 3, 2, 1 }, "0xcec9e51a"),
                new KeyValuePair<long[], string>(new long[] { 4004, 0, 0 }, "0x21c3c899"),
                new KeyValuePair<long[], string>(new long[] { 4004, 1, 0 }, "0xb59541db"),
                new KeyValuePair<long[], string>(new long[] { 4004, 0, 1 }, "0x386c1552"),
                new KeyValuePair<long[], string>(new long[] { 2026, 123456789 }, "0x0ae84c9a"),
                new KeyValuePair<long[], string>(new long[] { -1 }, "0xc569aed7"),
                new KeyValuePair<long[], string>(new long[] { 4294967295L }, "0xc569aed7"),
            };
            foreach (var v in vectors) Assert.AreEqual(v.Value, Hex(Rng.Hash32(v.Key)), string.Join(",", v.Key));
        }

        [Test]
        public void Hash32_is_order_and_length_sensitive_and_rejects_non_32_bit_words()
        {
            Assert.AreNotEqual(Rng.Hash32(1, 2), Rng.Hash32(2, 1));
            Assert.AreNotEqual(Rng.Hash32(0), Rng.Hash32(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rng.Hash32(4294967296L));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rng.Hash32(-2147483649L));
        }
    }
}
