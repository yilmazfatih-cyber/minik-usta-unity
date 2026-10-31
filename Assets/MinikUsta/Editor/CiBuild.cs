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
