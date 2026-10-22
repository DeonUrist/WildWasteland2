using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class AgentBuild
{
    public static void Windows()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("Enable at least one saved game scene in Build Settings.");
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(scenes, "Builds/Windows/Game.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed: " + report.summary.result);
    }
}
