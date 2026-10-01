// Scandal Season — Editor tools.
// LinuxBuilder: local diagnostic player build (NOT for deploy).
// Mirrors WebGLBuilder's content pipeline, targets StandaloneLinux64 so the
// game can run under Xvfb for direct visual/log inspection.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class LinuxBuilder
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string OutputDir = "Build/LinuxDiag";

    public static void Build()
    {
        int imported = ContentImporter.ImportAllNoExit();
        Debug.Log($"[LinuxBuilder] Content import OK ({imported} files).");

        ContentRegistryBuilder.RebuildRegistry();
        Debug.Log("[LinuxBuilder] ContentRegistry rebuilt.");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        // Wipe stale script assemblies AND Bee cache for a clean compile.
        string scriptAssemblies = Path.Combine(projectRoot, "Library/ScriptAssemblies");
        if (Directory.Exists(scriptAssemblies))
            Directory.Delete(scriptAssemblies, true);
        string beeCache = Path.Combine(projectRoot, "Library/Bee");
        if (Directory.Exists(beeCache))
            Directory.Delete(beeCache, true);
        Debug.Log("[LinuxBuilder] Script caches wiped.");

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
            }

            string outputPath = Path.Combine(projectRoot, OutputDir);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            Directory.CreateDirectory(outputPath);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = Path.Combine(outputPath, "scandal-season"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[LinuxBuilder] Result: {report.summary.result}, " +
                      $"errors: {report.summary.totalErrors}, " +
                      $"time: {report.summary.totalTime.TotalSeconds:F1}s");
        }
        finally
        {
            if (movedTests && Directory.Exists(testsStash))
                Directory.Move(testsStash, testsDir);
        }
    }
}
