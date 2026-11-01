using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MinikUsta.Editor
{
    /// <summary>
    /// Headless build entry points for GitHub Actions (GameCI unity-builder `buildMethod`).
    /// WebGL is built with compression disabled because GitHub Pages does not send the
    /// Content-Encoding headers that Brotli/Gzip builds need.
    /// </summary>
    public static class CiBuild
    {
        public static void BuildWebGL()
        {
            string buildPath = Arg("-customBuildPath") ?? Path.Combine("build", "WebGL", "MinikUsta");
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in EditorBuildSettings; add a scene before building.");

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            Debug.Log($"WebGL build succeeded: {buildPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            ReportSuccess(report.summary);
        }

        public static void BuildAndroid()
        {
            string buildPath = Arg("-customBuildPath") ?? Path.Combine("build", "Android", "MinikUsta.apk");
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in EditorBuildSettings; add a scene before building.");
            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            ReportSuccess(report.summary);
        }

        /// <summary>
        /// Prints what the GameCI CLI looks for after a build. unity-builder v6 runs the game-ci CLI, whose validateBuild()
        /// (game-ci/cli v0.1.72, src/model/unity/build-validation/unity-build-validation.ts) fails the step even when Unity
        /// exits 0, unless the container output contains the literal "Build succeeded!" (or a "# Build results #" block with
        /// "Errors: 0" followed by "Size:"). The CLI's own default build script prints both through StdOutReporter; a custom
        /// buildMethod has to do it itself. Call this only after the BuildResult check passed: the failure path must never
        /// print it (it throws, so Unity exits with code 1 and the CLI fails the step on the exit code).
        /// Guarded by the "Check CiBuild reports success to the GameCI CLI" step in unity-webgl.yml; see docs/CI.md section 2b.
        /// </summary>
        private static void ReportSuccess(BuildSummary summary)
        {
            string eol = Environment.NewLine;
            Console.WriteLine(
                $"{eol}###########################{eol}" +
                $"#      Build results      #{eol}" +
                $"###########################{eol}{eol}" +
                $"Duration: {summary.totalTime}{eol}" +
                $"Warnings: {summary.totalWarnings}{eol}" +
                $"Errors: {summary.totalErrors}{eol}" +
                $"Size: {summary.totalSize} bytes{eol}{eol}");
            Console.WriteLine("Build succeeded!");
        }

        private static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        }

        private static string? Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
