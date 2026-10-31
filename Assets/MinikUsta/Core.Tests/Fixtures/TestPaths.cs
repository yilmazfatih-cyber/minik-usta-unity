#nullable enable
// Test data locations. The same files are read under dotnet (output-independent: the repo root is found by walking up
// from the test binary) and under Unity EditMode (Application.dataPath).
using System;
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

        public static string LevelsDir => Path.Combine(RepoRoot, "Assets", "StreamingAssets", "MinikUsta", "levels");
        public static string GoldenDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Core.Tests", "Golden");
        public static string CoreSourceDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Core");

        public static JsonValue ReadJson(string path) => JsonReader.Parse(File.ReadAllText(path));

        /// <summary>A fresh parse of `levels/level_NNN.json`.</summary>
        public static JsonValue LoadLevelJson(int n) => ReadJson(Path.Combine(LevelsDir, "level_" + n.ToString("000") + ".json"));

        public static JsonValue LoadGolden(string fileName) => ReadJson(Path.Combine(GoldenDir, fileName));

        private static JsonValue? vectors;

        /// <summary>`p0_vectors.json`: values generated from the TypeScript core by tools/gen-p0-vectors.ts.</summary>
        public static JsonValue Vectors => vectors ??= LoadGolden("p0_vectors.json");

        private static JsonValue? p1Vectors;

        /// <summary>`p1_vectors.json`: movement / gravity / placement / delivery results generated from the TypeScript core by tools/gen-p1-vectors.ts.</summary>
        public static JsonValue P1Vectors => p1Vectors ??= LoadGolden("p1_vectors.json");

        public static JsonValue LevelVector(int n)
        {
            foreach (var l in Vectors["levels"]!.Items) if (l["level"]!.AsInt == n) return l;
            throw new InvalidOperationException("no vector for level " + n);
        }
    }
}
