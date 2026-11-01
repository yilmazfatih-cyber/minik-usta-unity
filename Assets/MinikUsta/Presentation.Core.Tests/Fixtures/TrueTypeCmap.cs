#nullable enable
// A minimal TrueType `cmap` reader for the font coverage test (paket V1): the code points of a .ttf (formats 4 and 12 of the
// Unicode / Windows Unicode subtables). Enough to prove that the Baloo 2 TTF draws every character of the i18n tables.
using System;
using System.Collections.Generic;
using System.IO;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public static class TrueTypeCmap
    {
        private static int U16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
        private static uint U32(byte[] b, int o) => ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3];
        private static short S16(byte[] b, int o) => (short)U16(b, o);

        private static int Table(byte[] b, string want)
        {
            int numTables = U16(b, 4);
            for (int i = 0; i < numTables; i++)
            {
                int rec = 12 + 16 * i;
                string tag = "" + (char)b[rec] + (char)b[rec + 1] + (char)b[rec + 2] + (char)b[rec + 3];
                if (tag == want) return (int)U32(b, rec + 8);
            }
            throw new InvalidDataException("no " + want + " table");
        }

        public static HashSet<int> CodePoints(string path) => new HashSet<int>(Glyphs(path).Keys);

        /// <summary>Advance width (font units) per mapped code point: cmap → glyph → hmtx.</summary>
        public static Dictionary<int, int> Advances(string path)
        {
            var b = File.ReadAllBytes(path);
            int hhea = Table(b, "hhea"), hmtx = Table(b, "hmtx");
            int metrics = U16(b, hhea + 34);
            var o = new Dictionary<int, int>();
            foreach (var kv in Glyphs(path))
            {
                int g = Math.Min(kv.Value, metrics - 1);
                o[kv.Key] = U16(b, hmtx + 4 * g);
            }
            return o;
        }

        /// <summary>Code point → glyph id of the Unicode subtables.</summary>
        public static Dictionary<int, int> Glyphs(string path)
        {
            var b = File.ReadAllBytes(path);
            int cmap = Table(b, "cmap");
            var o = new Dictionary<int, int>();
            int n = U16(b, cmap + 2);
            for (int i = 0; i < n; i++)
            {
                int rec = cmap + 4 + 8 * i;
                int platform = U16(b, rec), encoding = U16(b, rec + 2);
                int sub = cmap + (int)U32(b, rec + 4);
                bool unicode = platform == 0 || (platform == 3 && (encoding == 1 || encoding == 10));
                if (!unicode) continue;
                int format = U16(b, sub);
                if (format == 4) Format4(b, sub, o);
                else if (format == 12) Format12(b, sub, o);
            }
            return o;
        }

        private static void Format4(byte[] b, int s, Dictionary<int, int> o)
        {
            int segX2 = U16(b, s + 6);
            int ends = s + 14, starts = ends + segX2 + 2, deltas = starts + segX2, offsets = deltas + segX2;
            for (int i = 0; i < segX2 / 2; i++)
            {
                int end = U16(b, ends + 2 * i), start = U16(b, starts + 2 * i), delta = S16(b, deltas + 2 * i), ro = U16(b, offsets + 2 * i);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int glyph;
                    if (ro == 0) glyph = (c + delta) & 0xFFFF;
                    else
                    {
                        int at = offsets + 2 * i + ro + 2 * (c - start);
                        glyph = U16(b, at);
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }
                    if (glyph != 0) o[c] = glyph;
                }
            }
        }

        private static void Format12(byte[] b, int s, Dictionary<int, int> o)
        {
            uint groups = U32(b, s + 12);
            for (uint g = 0; g < groups; g++)
            {
                int at = s + 16 + (int)g * 12;
                uint start = U32(b, at), end = U32(b, at + 4), glyph = U32(b, at + 8);
                for (uint c = start; c <= end; c++)
                    if (glyph + (c - start) != 0) o[(int)c] = (int)(glyph + (c - start));
            }
        }
    }
}
