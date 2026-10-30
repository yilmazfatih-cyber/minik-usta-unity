#nullable enable
// Port of `fnv1a64` from src/core/moves.ts (web repo, source of truth): the text hash behind `eventLogHash` (TECH §6.3)
// and `levelHash` (K-43 item 4, TECH §11.1). Kept in its own file so the level package can hash before the move package exists.
using System.Text;

namespace MinikUsta.Core
{
    public static class Fnv1a
    {
        private const ulong Offset64 = 0xcbf29ce484222325UL;
        private const ulong Prime64 = 0x100000001b3UL;

        /// <summary>
        /// FNV-1a 64 over the UTF-8 bytes of `text`, as 16 lowercase hex digits. The UTF-8 encoding mirrors the TS code:
        /// code points (surrogate pairs joined) encoded by hand, so a lone surrogate hashes as its 3-byte form too.
        /// </summary>
        public static string Hash64Hex(string text)
        {
            ulong h = Offset64;
            for (int i = 0; i < text.Length; i++)
            {
                int cp = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                if (cp < 0x80) h = Byte(h, cp);
                else if (cp < 0x800)
                {
                    h = Byte(h, 0xc0 | (cp >> 6));
                    h = Byte(h, 0x80 | (cp & 63));
                }
                else if (cp < 0x10000)
                {
                    h = Byte(h, 0xe0 | (cp >> 12));
                    h = Byte(h, 0x80 | ((cp >> 6) & 63));
                    h = Byte(h, 0x80 | (cp & 63));
                }
                else
                {
                    h = Byte(h, 0xf0 | (cp >> 18));
                    h = Byte(h, 0x80 | ((cp >> 12) & 63));
                    h = Byte(h, 0x80 | ((cp >> 6) & 63));
                    h = Byte(h, 0x80 | (cp & 63));
                }
            }
            return h.ToString("x16");
        }

        private static ulong Byte(ulong h, int b)
        {
            unchecked
            {
                return (h ^ (ulong)b) * Prime64;
            }
        }

        /// <summary>Convenience for callers that already hold a string builder.</summary>
        public static string Hash64Hex(StringBuilder text) => Hash64Hex(text.ToString());
    }
}
