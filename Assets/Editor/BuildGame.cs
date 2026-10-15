using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Windows build of the game into Builds/EchoOfMemories (ignored by git). Batch: -executeMethod BuildGame.Windows -quit</summary>
public static class BuildGame
{
    [MenuItem("Tools/Build/Windows (64-bit)")]
    public static void Windows()
    {
        // borderless full screen at the monitor's resolution, resizable when windowed (F11 / Alt+Enter, see DisplayMode)
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.productName = "Echo of Memories";
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/EchoOfMemories/EchoOfMemories.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        Debug.Log($"[BuildGame] {s.result}: {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024f * 1024f):F0} MB, {s.totalTime.TotalSeconds:F0} s, scenes: {string.Join(", ", scenes)}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
