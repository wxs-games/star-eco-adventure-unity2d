#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class BuildWindows
{
    [MenuItem("Build/Windows64")]
    public static void Build()
    {
        var scenes = new[]
        {
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/Level01.unity",
            "Assets/Scenes/Level02.unity",
            "Assets/Scenes/Level03.unity"
        };
        System.IO.Directory.CreateDirectory("Builds/StarEco-Windows");
        var report = BuildPipeline.BuildPlayer(
            scenes,
            "Builds/StarEco-Windows/StarEco.exe",
            BuildTarget.StandaloneWindows64,
            BuildOptions.None);
        Debug.Log("[BUILD] result=" + report.summary.result
            + " errors=" + report.summary.totalErrors
            + " warnings=" + report.summary.totalWarnings
            + " size=" + report.summary.totalSize);
        if (report.summary.totalErrors > 0)
            EditorApplication.Exit(1);
        else
            EditorApplication.Exit(0);
    }
}
#endif
