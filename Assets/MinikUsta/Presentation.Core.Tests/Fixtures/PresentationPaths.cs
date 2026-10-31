#nullable enable
// Test data locations of the presentation tests. As in Core.Tests (TestPaths), the repo root is found by walking up from
// the test binary (dotnet) or from Application.dataPath (Unity EditMode) to Assets/MinikUsta/Core/MinikUsta.Core.asmdef.
using System;
using System.IO;
using MinikUsta.Core.Json;

namespace MinikUsta.Presentation.Tests.Fixtures
{
    public static class PresentationPaths
    {
        private static string? root;

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

        /// <summary>
        /// The level files: `Assets/MinikUsta/Resources/levels` once Q1 embeds them there (D-096, WebGL), else the Phase 2
        /// location `Assets/StreamingAssets/MinikUsta/levels`.
        /// </summary>
        public static string LevelsDir
        {
            get
            {
                string resources = Path.Combine(RepoRoot, "Assets", "MinikUsta", "Resources", "levels");
                if (File.Exists(Path.Combine(resources, "level_001.json"))) return resources;
                return Path.Combine(RepoRoot, "Assets", "StreamingAssets", "MinikUsta", "levels");
            }
        }

        /// <summary>The core's golden replays (`level_NNN.hand.json`, `level_NNN.solver.json`), copied from the web repo.</summary>
        public static string CoreGoldenDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Core.Tests", "Golden");

        /// <summary>Presentation test data (tokens.json copy, tools/sync-from-web.sh).</summary>
        public static string GoldenDir => Path.Combine(RepoRoot, "Assets", "MinikUsta", "Presentation.Core.Tests", "Golden");

        public static JsonValue ReadJson(string path) => JsonReader.Parse(File.ReadAllText(path));

        public static JsonValue LoadLevelJson(int n) => ReadJson(Path.Combine(LevelsDir, "level_" + n.ToString("000") + ".json"));

        public static JsonValue LoadCoreGolden(string fileName) => ReadJson(Path.Combine(CoreGoldenDir, fileName));

        private static JsonValue? tokens;

        /// <summary>web src/theme/tokens.json (design-lead), copied by tools/sync-from-web.sh.</summary>
        public static JsonValue Tokens => tokens ??= ReadJson(Path.Combine(GoldenDir, "tokens.json"));

        /// <summary>A token value by dotted path ("duration.pick", "kit.v3.screen.hud.movesW").</summary>
        public static JsonValue Token(string path)
        {
            JsonValue v = Tokens;
            foreach (var part in path.Split('.'))
            {
                var next = v.IsObject ? v[part] : null;
                v = next ?? throw new InvalidOperationException("tokens.json has no " + path + " (at " + part + ")");
            }
            return v;
        }
    }
}
