#nullable enable
// Port of `loadLevel` of src/core/level/compile.ts (web repo, source of truth): schema + the runtime subset of the K-45 logic
// checks (Logic.RuntimeChecks), then `Compile`. The game loads a level only through this entry (TECH §8.3, D-005).
using System.Collections.Generic;
using MinikUsta.Core.Json;

namespace MinikUsta.Core.Level
{
    /// <summary>`loadLevel` result: `Ok` with the compiled level and the warnings, or a `Stage` ("schema" | "logic") with the issues.</summary>
    public sealed class LoadResult
    {
        public const string StageSchema = "schema";
        public const string StageLogic = "logic";

        public readonly bool Ok;
        public readonly CompiledLevel? Level;
        /// <summary>`Ok`: the runtime-check warnings.</summary>
        public readonly Issue[] Warnings;
        /// <summary>`!Ok`: the stage that refused the level.</summary>
        public readonly string? Stage;
        /// <summary>`!Ok`: every issue of that stage.</summary>
        public readonly Issue[] Issues;

        private LoadResult(bool ok, CompiledLevel? level, Issue[] warnings, string? stage, Issue[] issues)
        {
            Ok = ok;
            Level = level;
            Warnings = warnings;
            Stage = stage;
            Issues = issues;
        }

        public static LoadResult Success(CompiledLevel level, Issue[] warnings) => new LoadResult(true, level, warnings, null, new Issue[0]);
        public static LoadResult Fail(string stage, Issue[] issues) => new LoadResult(false, null, new Issue[0], stage, issues);

        /// <summary>The compiled level, or an exception describing why it does not load (tests, tools).</summary>
        public CompiledLevel LevelOrThrow()
        {
            if (Ok) return Level!;
            var lines = new List<string>();
            foreach (var i in Issues) lines.Add(i.ToString());
            throw new System.InvalidOperationException("level does not load (" + Stage + "):\n  " + string.Join("\n  ", lines));
        }
    }

    public static partial class Compiler
    {
        /// <summary>
        /// Schema + the runtime logic subset (`Logic.RuntimeChecks`) + `Compile`. `ctx` may carry `FileId`, `PreviousMechanics`
        /// … but its `Only` is replaced by the runtime set (TS `loadLevel(json, ctx)`).
        /// </summary>
        public static LoadResult LoadLevel(JsonValue json, LogicContext? ctx = null)
        {
            var res = Logic.ValidateLevelJson(json, (ctx ?? new LogicContext()).WithOnly(Logic.RuntimeChecks));
            if (res.Level == null) return LoadResult.Fail(LoadResult.StageSchema, res.Issues);
            foreach (var i in res.Issues) if (i.Severity == Severities.Error) return LoadResult.Fail(LoadResult.StageLogic, res.Issues);
            return LoadResult.Success(Compile(res.Level), res.Issues);
        }

        public static LoadResult LoadLevel(string text, LogicContext? ctx = null) => LoadLevel(JsonReader.Parse(text), ctx);
    }
}
