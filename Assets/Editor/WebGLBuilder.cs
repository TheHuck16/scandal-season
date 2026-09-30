// Scandal Season — Editor tools.
// WebGLBuilder: clean batchmode WebGL build for the hosted playtest.
//
// Pipeline (single invocation):
//   1. ContentImporter.ImportAllNoExit()      — validate + import all content JSON.
//   2. ContentRegistryBuilder.RebuildRegistry() — regenerate ContentRegistry.asset.
//   3. Clean player build of Assets/Scenes/Main.unity to <project>/Build/WebGL.
//
// Guards learned the hard way (Sep 29, 2026):
//   - Assets/Tests is moved outside Assets during the build: PlayMode test
//     assemblies otherwise compile into the player and break it.
//   - Compression is forced Disabled: GitHub Pages serves .gz as
//     application/gzip without Content-Encoding, so Unity's loader fails with
//     "Unable to parse ...framework.js.gz!". (v9.2 incident.)
//   - The scene list is explicit: the build never depends on which scenes are
//     ticked in Build Settings.
//   - Library/ScriptAssemblies is wiped first to avoid stale script caches.
//
// Usage:
//   Unity -batchmode -nographics -projectPath <project> \
//     -executeMethod WebGLBuilder.Build -quit -logFile <log>

using System.IO;
using UnityEditor;
using UnityEngine;

public static class WebGLBuilder
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string OutputDir = "Build/WebGL";

    public static void Build()
    {
        // 1. Content import (validates every JSON file; throws on failure).
        int imported = ContentImporter.ImportAllNoExit();
        Debug.Log($"[WebGLBuilder] Content import OK ({imported} files).");

        // 2. Registry rebuild.
        ContentRegistryBuilder.RebuildRegistry();
        Debug.Log("[WebGLBuilder] ContentRegistry rebuilt.");

        // 3. Move Tests out so PlayMode assemblies can't leak into the player.
        // NOTE: the stash must live on the same filesystem as the project
        // (/tmp is a tmpfs here — Directory.Move can't cross devices).
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string testsDir = Path.Combine(Application.dataPath, "Tests");
        string testsStash = Path.Combine(projectRoot, "Tests.stash.build-tmp");
        bool movedTests = false;
        try
        {
            if (Directory.Exists(testsDir))
            {
                if (Directory.Exists(testsStash))
                    Directory.Delete(testsStash, true);
                Directory.Move(testsDir, testsStash);
                movedTests = true;
                Debug.Log("[WebGLBuilder] Assets/Tests stashed for build.");
            }

            // 4. Wipe stale script assemblies for a clean compile.
            string scriptAssemblies = Path.Combine(projectRoot, "Library/ScriptAssemblies");
            if (Directory.Exists(scriptAssemblies))
            {
                Directory.Delete(scriptAssemblies, true);
                Debug.Log("[WebGLBuilder] Library/ScriptAssemblies wiped.");
            }

            // 5. Force uncompressed output (GitHub Pages can't serve .gz correctly).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            string outputPath = Path.Combine(projectRoot, OutputDir);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            Directory.CreateDirectory(outputPath);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[WebGLBuilder] Result: {summary.result}, " +
                      $"errors: {summary.totalErrors}, time: {summary.totalTime.TotalSeconds:F1}s");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception(
                    $"[WebGLBuilder] Build failed with {summary.totalErrors} errors.");
        }
        finally
        {
            if (movedTests && Directory.Exists(testsStash))
            {
                if (Directory.Exists(testsDir))
                    Directory.Delete(testsDir, true);
                Directory.Move(testsStash, testsDir);
                Debug.Log("[WebGLBuilder] Assets/Tests restored.");
            }
            AssetDatabase.Refresh();
        }
    }
}
