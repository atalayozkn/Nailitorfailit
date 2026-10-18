using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows build'inin yanina proje kokundeki steam_appid.txt dosyasini kopyalar.
///
/// Neden gerekli: Steam API hangi app ile baslatilacagini CALISAN DIZINDEKI bu dosyadan
/// (ya da SteamAppId ortam degiskeninden) okur; dosya exe'nin yaninda yoksa Steam modu
/// sessizce LAN'e duser ve lobi panelinde 6 haneli kod yerine host'un IP'si gorunur.
/// Her yeni build klasorunde elle kopyalamak unutuluyordu, bu yuzden otomatiklestirildi.
///
/// UYARI: Bu dosya ayni zamanda "gelistirme/test kurulumu" isaretidir (SteamManager
/// RestartAppIfNecessary kontrolunu o durumda atlar). Magaza build'inden once proje
/// kokundeki steam_appid.txt kaldirilmali ya da bu post-processor devre disi birakilmalidir.
/// </summary>
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
                             $"build'in yanina elle koymak gerekecek. Yoksa oyun LAN moduna duser.");
            return;
        }

        string target = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt");
        File.Copy(source, target, true);

        Debug.Log($"[BUILD] steam_appid.txt kopyalandi -> {target} " +
                  $"(icerik: {File.ReadAllText(target).Trim()})");
    }
}
