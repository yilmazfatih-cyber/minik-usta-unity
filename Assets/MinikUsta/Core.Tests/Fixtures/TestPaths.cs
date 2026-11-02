#nullable enable
// Test data locations. The same files are read under dotnet (output-independent: the repo root is found by walking up
// from the test binary) and under Unity EditMode (Application.dataPath).
using System;
using System.Collections.Generic;
using System.IO;
using MinikUsta.Core.Json;

namespace MinikUsta.Core.Tests.Fixtures
{
    public static class TestPaths
    {
        private static string? root;

        /// <summary>The Unity project root (= repo root): the directory holding `Assets/MinikUsta/Core/MinikUsta.Core.asmdef`.</summary>
        public static string RepoRoot
        {
            get
            {
                if (root != null) return root;
#if UNITY_2017_1_OR_NEWER
                string start = UnityEngine.Application.dataPath;
#else
                string start = AppContext.BaseDirectory;
#endif
                string? dir = Path.GetFullPath(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir, "Assets", "MinikUsta", "Core", "MinikUsta.Core.asmdef")))
                    {
                        root = dir;
                        return dir;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
                throw new DirectoryNotFoundException("repo root (Assets/MinikUsta/Core/MinikUsta.Core.asmdef) not found above " + start);
            }
        }

        /// <summary>The shipped levels (D-099: `schemaVersion` 2, fixed yard, K-55): `Assets/StreamingAssets/MinikUsta/levels`.</summary>
        public static string LevelsDir => Path.Combine(RepoRoot, "Assets", "StreamingAssets", "MinikUsta", "levels");
        /// <summary>
        /// The Faz 2R archive (`schemaVersion` 1: batches, truck, K-25…K-27): levels 1–10 and their golden replays as of web
        /// c1facf4. D-099 / K-55 item 9 keeps the v1 code paths and their tests; the tests written against Faz 2R data (every
        /// `LoadLevelJson`, `Boards.LevelFile`, `LoadGolden("level_NNN.…")` call) read this folder.
        /// </summary>
        public static string Archive2RDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Core.Tests", "Archive2R");
        public static string ConfigDir => Path.Combine(RepoRoot, "Assets", "StreamingAssets", "MinikUsta", "config");
        public static string GoldenDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Core.Tests", "Golden");
        public static string CoreSourceDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Core");
        /// <summary>Copies of the web repo's tests/level/fixtures/{valid,invalid} (K-45 validator fixtures).</summary>
        public static string FixturesDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Core.Tests", "Fixtures", "Levels");

        public static JsonValue ReadJson(string path) => JsonReader.Parse(File.ReadAllText(path));

        /// <summary>A fresh parse of the Faz 2R archive level `Archive2R/level_NNN.json` (`schemaVersion` 1).</summary>
        public static JsonValue LoadLevelJson(int n) => ReadJson(Path.Combine(Archive2RDir, "level_" + n.ToString("000") + ".json"));

        /// <summary>A fresh parse of the shipped `levels/level_NNN.json` (`schemaVersion` 2, K-55).</summary>
        public static JsonValue LoadCurrentLevelJson(int n) => ReadJson(Path.Combine(LevelsDir, "level_" + n.ToString("000") + ".json"));

        /// <summary>
        /// A golden file: `level_NNN.hand.json` / `level_NNN.solver.json` come from the Faz 2R archive (the tests that load them
        /// were written against it), everything else (vectors, rule ids) from `Golden/`.
        /// </summary>
        public static JsonValue LoadGolden(string fileName) =>
            ReadJson(Path.Combine(fileName.StartsWith("level_", StringComparison.Ordinal) ? Archive2RDir : GoldenDir, fileName));

        /// <summary>A golden file of the shipped levels (`Golden/level_NNN.hand.json`, `…solver.json`, D-099 content).</summary>
        public static JsonValue LoadCurrentGolden(string fileName) => ReadJson(Path.Combine(GoldenDir, fileName));

        /// <summary>TS `fixturePath(group)`: the fixture directory of a group (`valid`, `invalid`).</summary>
        public static string FixturePath(string group) => Path.Combine(FixturesDir, group);

        /// <summary>TS `loadFixture(group, name)`: a fresh parse of `tests/level/fixtures/&lt;group&gt;/&lt;name&gt;.json`.</summary>
        public static JsonValue LoadFixture(string group, string name) => ReadJson(Path.Combine(FixturePath(group), name + ".json"));

        /// <summary>Fixture names (without `.json`) of a group, sorted.</summary>
        public static string[] FixtureNames(string group)
        {
            var files = Directory.GetFiles(FixturePath(group), "*.json");
            var o = new string[files.Length];
            for (int i = 0; i < files.Length; i++) o[i] = Path.GetFileNameWithoutExtension(files[i]);
            Array.Sort(o, StringComparer.Ordinal);
            return o;
        }

        /// <summary>`config/economy.json → boosters.&lt;name&gt;.unlockLevel` (tools/lib/levels.ts `loadBoosterUnlock`).</summary>
        public static Dictionary<string, int> LoadBoosterUnlock()
        {
            var o = new Dictionary<string, int>();
            var json = ReadJson(Path.Combine(ConfigDir, "economy.json"));
            var boosters = json["boosters"];
            if (boosters == null || !boosters.IsObject) return o;
            foreach (var kv in boosters.Members)
            {
                if (!kv.Value.IsObject) continue;
                var unlock = kv.Value["unlockLevel"];
                if (unlock != null && unlock.IsNumber) o[kv.Key] = unlock.AsInt;
            }
            return o;
        }

        private static JsonValue? vectors;

        /// <summary>`p0_vectors.json`: values generated from the TypeScript core by tools/gen-p0-vectors.ts.</summary>
        public static JsonValue Vectors => vectors ??= LoadGolden("p0_vectors.json");

        private static JsonValue? p1Vectors;

        /// <summary>`p1_vectors.json`: movement / gravity / placement / delivery results generated from the TypeScript core by tools/gen-p1-vectors.ts.</summary>
        public static JsonValue P1Vectors => p1Vectors ??= LoadGolden("p1_vectors.json");

        private static JsonValue? p2Vectors;

        /// <summary>`p2_vectors.json`: validator / goals / boosters / deadlock / summary / registry results generated by tools/gen-p2-vectors.ts.</summary>
        public static JsonValue P2Vectors => p2Vectors ??= LoadGolden("p2_vectors.json");

        private static JsonValue? p3Vectors;

        /// <summary>`p3_vectors.json`: seeded `GameSession` sequences (every move kind, Undo, offers, exit, replay) generated by tools/gen-p3-vectors.ts.</summary>
        public static JsonValue P3Vectors => p3Vectors ??= LoadGolden("p3_vectors.json");

        public static JsonValue LevelVector(int n)
        {
            foreach (var l in Vectors["levels"]!.Items) if (l["level"]!.AsInt == n) return l;
            throw new InvalidOperationException("no vector for level " + n);
        }
    }
}
