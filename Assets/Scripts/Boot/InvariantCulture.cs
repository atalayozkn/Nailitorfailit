using System.Collections.Generic;
using System.Globalization;
using Steamworks;
using UnityEngine;

// TextMeshPro's UpperCase style (and string.ToUpper / ToLower) follow the OS culture. On a Turkish system "i" becomes
// "İ" (dotted capital I), so "SINGLE PLAYER" is drawn as "SİNGLE PLAYER". The game's UI is English, so the main thread
// is pinned to the invariant culture, both in builds and in the Editor.
public static class InvariantCulture
{
    // For now (testing) the culture follows the language of the Steam client once Steam is up. Set to false to keep the
    // invariant culture all the time (the Turkish "i" -> "İ" problem above comes back with a Turkish Steam).
    public const bool FollowSteamLanguage = false;

    private static readonly Dictionary<string, string> SteamLanguageCultures = new Dictionary<string, string>
    {
        { "english", "en-US" }, { "turkish", "tr-TR" }, { "german", "de-DE" }, { "french", "fr-FR" }, { "spanish", "es-ES" },
        { "latam", "es-419" }, { "italian", "it-IT" }, { "portuguese", "pt-PT" }, { "brazilian", "pt-BR" }, { "russian", "ru-RU" },
        { "polish", "pl-PL" }, { "dutch", "nl-NL" }, { "swedish", "sv-SE" }, { "danish", "da-DK" }, { "finnish", "fi-FI" },
        { "norwegian", "nb-NO" }, { "czech", "cs-CZ" }, { "hungarian", "hu-HU" }, { "romanian", "ro-RO" }, { "ukrainian", "uk-UA" },
        { "greek", "el-GR" }, { "bulgarian", "bg-BG" }, { "japanese", "ja-JP" }, { "koreana", "ko-KR" }, { "schinese", "zh-CN" },
        { "tchinese", "zh-TW" }, { "thai", "th-TH" }, { "vietnamese", "vi-VN" }, { "arabic", "ar-SA" }, { "indonesian", "id-ID" },
    };

    private sealed class SteamLocaleWatcher : MonoBehaviour
    {
        private float giveUpAt;

        private void Awake() => giveUpAt = Time.realtimeSinceStartup + 60f;

        private void Update()
        {
            if (!SteamManager.Initialized)
            {
                if (Time.realtimeSinceStartup > giveUpAt) Destroy(gameObject); // no Steam: the invariant culture stays
                return;
            }

            string language = SteamUtils.GetSteamUILanguage();
            string cultureName = !string.IsNullOrEmpty(language) && SteamLanguageCultures.TryGetValue(language, out string mapped) ? mapped : null;

            if (cultureName != null)
            {
                try
                {
                    CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                }
                catch (CultureNotFoundException) { cultureName = null; }
            }

            Debug.Log($"[LOCALE] Steam language '{language}' -> {(cultureName ?? "unknown, invariant culture kept")}");
            Destroy(gameObject);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WatchSteam()
    {
        if (!FollowSteamLanguage || !Application.isPlaying) return;

        var go = new GameObject("SteamLocaleWatcher");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<SteamLocaleWatcher>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

#if UNITY_EDITOR
    // Runs after every domain reload, so edit-mode previews (Game/Scene view, screenshots) match the build.
    [UnityEditor.InitializeOnLoadMethod]
    private static void ApplyInEditor() => Apply();
#endif
}
