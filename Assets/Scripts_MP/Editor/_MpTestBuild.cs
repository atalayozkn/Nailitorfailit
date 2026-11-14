// TEMPORARY (Claude): headless Windows build for the 2-machine MP tests. Output dir from MPTEST_OUT.
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class _MpTestBuild
{
    public static void BuildWindows()
    {
        string output = System.Environment.GetEnvironmentVariable("MPTEST_OUT");
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            locationPathName = output + "/NailItOrFailIt.exe",
            options = BuildOptions.None,
        });

        Debug.Log($"[MPTEST-BUILD] result={report.summary.result} errors={report.summary.totalErrors} " +
                  $"size={report.summary.totalSize / (1024 * 1024)}MB time={report.summary.totalTime} scenes={scenes.Length}");
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
