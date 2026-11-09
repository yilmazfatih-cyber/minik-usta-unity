#nullable enable
// The levels the Unity build plays (Faz 3: the Faz 2R vertical slice, levels 1–10; "Sonraki bölüm" cycles 10 → 1).
// The text of `level_NNN.json` comes from a reader (Unity: Resources.Load<TextAsset>("levels/level_NNN"), D-096; tests:
// the repo files). Every level goes through `Compiler.LoadLevel` (zod schema + K-45 runtime checks): an invalid level is
// never played (D-005; principle 4).
using System;
using System.Globalization;
using MinikUsta.Core.Level;

namespace MinikUsta.Presentation.Game
{
    public sealed class LevelCatalog
    {
        public const int First = 1;
        public const int Last = 10;

        private readonly Func<int, string?> read;

        /// <summary>`read(n)` = the JSON text of level n, or null when the file is missing.</summary>
        public LevelCatalog(Func<int, string?> read)
        {
            this.read = read ?? throw new ArgumentNullException(nameof(read));
        }

        /// <summary>Resources path of level n (no extension): `levels/level_001`.</summary>
        public static string ResourceName(int n) => (n >= DevFirst ? "levels_dev/level_" : "levels/level_") + n.ToString("000", CultureInfo.InvariantCulture);

        /// <summary>
        /// E3: level numbers from here on are development fixtures (`Resources/levels_dev/`, not touched by tools/sync-from-web.sh and not
        /// part of the 1–`Last` story): level 99 is the block showcase (every v3 trait on one board) for the debug panel and the CI shots.
        /// </summary>
        public const int DevFirst = 90;
        public const int Showcase = 99;

        /// <summary>The level after n: n + 1, after the last one the first again.</summary>
        public static int Next(int n) => n >= Last || n < First ? First : n + 1;

        /// <summary>Loads and validates level n; `error` explains a missing or invalid file.</summary>
        public CompiledLevel? TryLoad(int n, out string? error)
        {
            error = null;
            string? text;
            try
            {
                text = read(n);
            }
            catch (Exception e)
            {
                error = "level " + n + ": " + e.Message;
                return null;
            }
            if (text == null)
            {
                error = "level " + n + ": " + ResourceName(n) + " not found";
                return null;
            }
            LoadResult res;
            try
            {
                res = Compiler.LoadLevel(text);
            }
            catch (Exception e)
            {
                error = "level " + n + ": " + e.Message;
                return null;
            }
            if (!res.Ok)
            {
                try
                {
                    res.LevelOrThrow();
                }
                catch (Exception e)
                {
                    error = "level " + n + " is invalid: " + e.Message;
                }
                return null;
            }
            return res.Level;
        }
    }
}
