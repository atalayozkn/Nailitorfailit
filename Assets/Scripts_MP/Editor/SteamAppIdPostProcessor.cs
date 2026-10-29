using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class SteamAppIdPostProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
            report.summary.platform != BuildTarget.StandaloneWindows)
        {
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, "steam_appid.txt");

        if (!File.Exists(source))
        {
            Debug.LogWarning($"[BUILD] Proje kokunde steam_appid.txt yok ({source}); Steam testi icin " +
                             $"build'in yanina elle koymak gerekecek. Yoksa multiplayer calismaz.");
            return;
        }

        string target = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt");
        File.Copy(source, target, true);

        Debug.Log($"[BUILD] steam_appid.txt kopyalandi -> {target} " +
                  $"(icerik: {File.ReadAllText(target).Trim()})");
    }
}
