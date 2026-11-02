#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MinikUsta.Editor
{
    /// <summary>
    /// Headless build entry points for GitHub Actions (GameCI unity-builder `buildMethod`).
    /// WebGL is built with Gzip and the decompression fallback (P4B-04): GitHub Pages does not send the Content-Encoding headers that a
    /// plain Gzip/Brotli build needs, so the Unity loader inflates the files itself (a JavaScript decompressor is included). The files
    /// on disk are what the browser downloads; with compression Disabled they were 48,6 MB (run 41386489687, 3D art in the data file).
    /// </summary>
    public static class CiBuild
    {
        public static void BuildWebGL()
        {
            string buildPath = Arg("-customBuildPath") ?? Path.Combine("build", "WebGL", "MinikUsta");
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in EditorBuildSettings; add a scene before building.");

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
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

        /// <summary>
        /// Android player for GitHub Actions (.github/workflows/unity-android.yml; docs/CI.md "Android"). GameCI passes the full output
        /// file in `-customBuildPath` (`build/Android/MinikUsta.apk`; `.aab` for `androidExportType androidAppBundle`) and, because
        /// this is a custom buildMethod, none of its own Android settings: the signing and version arguments are read here.
        /// Signing: with `-androidKeystoreName` naming an existing file (the CLI decodes ANDROID_KEYSTORE_BASE64 into it) the build is
        /// signed with that keystore, otherwise Unity's debug keystore signs the APK (installable by sideloading, not for a store).
        /// </summary>
        public static void BuildAndroid()
        {
            string buildPath = ArgValue("-customBuildPath") ?? Path.Combine("build", "Android", "MinikUsta.apk");
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in EditorBuildSettings; add a scene before building.");
            bool bundle = string.Equals(ArgValue("-androidExportType"), "androidAppBundle", StringComparison.OrdinalIgnoreCase);
            ConfigureAndroid();
            EditorUserBuildSettings.buildAppBundle = bundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            string? dir = Path.GetDirectoryName(Path.GetFullPath(buildPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
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
            Debug.Log($"Android build succeeded: {buildPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            ReportSuccess(report.summary);
        }

        /// <summary>
        /// Portrait only, IL2CPP with ARM64 + ARMv7 (Unity's Mono backend cannot build ARM64; ARM64-only phones need it), a valid
        /// application id (the default one would be built from the company name "Tuna &amp; Co." and is not a legal package name),
        /// the version code from GameCI and the signing keystore when one was provided. Passwords are never printed.
        /// </summary>
        private static void ConfigureAndroid()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            string? code = ArgValue("-androidVersionCode");
            if (int.TryParse(code, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int versionCode) && versionCode > 0)
                PlayerSettings.Android.bundleVersionCode = versionCode;
            string? version = ArgValue("-buildVersion");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            // P4B-06: the target API level is explicit when the workflow passes one (Play requires 35+ in 2026); otherwise Unity's
            // "automatic (highest installed)" stays. `AndroidSdkVersions.AndroidApiLevel35` exists in UnityCsReference 6000.0
            // (Editor/Mono/PlayerSettingsAndroid.bindings.cs); the enum member name is what the workflow passes.
            string? target = ArgValue("-androidTargetSdkVersion");
            if (!string.IsNullOrEmpty(target) && Enum.TryParse(target, out AndroidSdkVersions targetLevel))
                PlayerSettings.Android.targetSdkVersion = targetLevel;

            string? keystore = ArgValue("-androidKeystoreName");
            if (!string.IsNullOrEmpty(keystore) && File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = ArgValue("-androidKeystorePass") ?? "";
                PlayerSettings.Android.keyaliasName = ArgValue("-androidKeyaliasName") ?? "";
                PlayerSettings.Android.keyaliasPass = ArgValue("-androidKeyaliasPass") ?? "";
                Debug.Log("Android signing: release keystore " + keystore);
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("Android signing: Unity debug keystore (no keystore secrets were provided)");
            }
            Debug.Log($"Android: id {AndroidApplicationId}, version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), IL2CPP ARM64+ARMv7, portrait, minSdk {PlayerSettings.Android.minSdkVersion}, targetSdk {PlayerSettings.Android.targetSdkVersion}");
        }

        /// <summary>Package name of the Android build (letters, digits, dots; not derived from the company name).</summary>
        public const string AndroidApplicationId = "com.tunaco.minikusta";

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

        /// <summary>
        /// Like <see cref="Arg"/> for GameCI's `-name "$VALUE"` pairs: an empty value may be dropped from the command line, which would make
        /// the next option name look like this option's value, so a value that starts with '-' (or is empty) counts as not given.
        /// </summary>
        private static string? ArgValue(string name)
        {
            string? v = Arg(name);
            return string.IsNullOrEmpty(v) || v![0] == '-' ? null : v;
        }
    }
}
