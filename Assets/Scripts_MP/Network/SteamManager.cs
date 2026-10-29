using Steamworks;
using UnityEngine;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    public static bool Initialized { get; private set; }

    public static ulong LocalSteamId { get; private set; }

    public static CSteamID LocalSteamID => Initialized ? new CSteamID(LocalSteamId) : CSteamID.Nil;

    [Header("Steam")]
    [Tooltip("'Nail It Or Fail It' Steam App ID. Sadece RestartAppIfNecessary icin kullanilir; " +
             "API'nin hangi app ile baslatilacagi proje kokundeki steam_appid.txt'ten okunur.")]
    [SerializeField] private uint appId = 4577740;

    [SerializeField] private bool restartThroughSteamIfNecessary = false;

    private static bool s_EverInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[STEAM] Ikinci SteamManager kopyasi yok ediliyor (tekil ornek korunuyor).");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (s_EverInitialized)
        {

            Debug.LogWarning("[STEAM] SteamAPI bu surecte zaten baslatilmis; tekrar Init() cagrilmiyor.");
            return;
        }

        if (!Packsize.Test())
        {
            Debug.LogError("[STEAM] Packsize hatasi: Steamworks.NET paketi yanlis platform icin derlenmis. Multiplayer devre disi kalacak.");
            return;
        }

        if (!DllCheck.Test())
        {
            Debug.LogError("[STEAM] Steamworks.NET native kutuphaneleri (steam_api*.dll/.so/.dylib) bulunamadi. Multiplayer devre disi kalacak.");
            return;
        }

        string appIdFile = System.IO.Path.Combine(Application.dataPath, "..", "steam_appid.txt");
        bool isDevSetup = System.IO.File.Exists(appIdFile);

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD

        if (restartThroughSteamIfNecessary && !isDevSetup && appId != 480 &&
            SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
        {
            Debug.Log("[STEAM] Oyun Steam uzerinden baslatilmamis; Steam istemcisi uzerinden yeniden baslatiliyor.");
            Application.Quit();
            return;
        }
#endif

        string effectiveAppId = null;

        try
        {
            if (isDevSetup)
            {
                string id = System.IO.File.ReadAllText(appIdFile).Trim();

                if (!string.IsNullOrEmpty(id))
                {
                    effectiveAppId = id;
                    System.Environment.SetEnvironmentVariable("SteamAppId", id);
                    Debug.Log($"[STEAM] steam_appid.txt okundu: {id} (calisma dizini: {System.IO.Directory.GetCurrentDirectory()})");
                }
            }
            else
            {
                Debug.LogWarning($"[STEAM] Proje kokunde steam_appid.txt yok ({appIdFile}); " +
                                 $"Init yalnizca SteamAppId ortam degiskeniyle calisir.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[STEAM] steam_appid.txt okunamadi: " + e.Message);
        }

        if (!SteamAPI.Init())
        {
            Debug.LogWarning($"[STEAM] SteamAPI.Init() basarisiz (appId={(effectiveAppId ?? appId.ToString())}). " +
                             $"Muhtemel sebep: Steam istemcisi kapali, surec Steam disinda baslatilmis " +
                             $"(duz 'wine' ile Steam istemcisine ulasilamaz) veya hesap bu uygulamayi oynayamiyor. " +
                             $"Oyun multiplayer devre disi kalacak (LAN modu kaldirildi).");
            DisableFizzyWhenSteamMissing();
            return;
        }

        Initialized = true;
        s_EverInitialized = true;
        LocalSteamId = SteamUser.GetSteamID().m_SteamID;

        Debug.Log($"[STEAM] Hazir. user='{SteamFriends.GetPersonaName()}' steamId={LocalSteamId} " +
                  $"appId={(effectiveAppId ?? appId.ToString())}");
    }

    private void Update()
    {

        if (Initialized) SteamAPI.RunCallbacks();
    }

    private static void DisableFizzyWhenSteamMissing()
    {
        var fizzies = FindObjectsByType<Mirror.FizzySteam.FizzySteamworks>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var fizzy in fizzies)
        {
            if (!fizzy.enabled) continue;

            fizzy.enabled = false;
            Debug.Log("[STEAM] Steam offline - FizzySteamworks transportu devre disi. LAN modu kaldirildi, multiplayer kullanilamaz.");
        }
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Shutdown()
    {
        if (!Initialized) return;

        Initialized = false;
        SteamAPI.Shutdown();
        Debug.Log("[STEAM] Shutdown.");
    }
}
