using Steamworks;
using UnityEngine;

/// <summary>
/// Steam API yasam dongusu (Init / RunCallbacks / Shutdown). FizzySteamworks transport'u
/// SteamAPI'yi KENDISI baslatmaz; bu yuzden LobbyNetworkManager GameObject'inde bu
/// bilesenin bulunmasi zorunludur.
///
/// Steam yoksa (istemci kapali / hesap uygulamayi oynayamiyor) hata FIRLATMAZ; sadece
/// Initialized = false kalir ve SteamLobbyManager LAN moduna duser — boylece tek
/// bilgisayarda iki instance ile test yapilabilir.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    /// <summary>SteamAPI.Init() basarili olduysa true. Steam cagrilari SADECE bu bayrak true iken yapilabilir.</summary>
    public static bool Initialized { get; private set; }

    /// <summary>Yerel kullanicinin SteamID64 degeri (Initialized degilken 0).</summary>
    public static ulong LocalSteamId { get; private set; }

    public static CSteamID LocalSteamID => Initialized ? new CSteamID(LocalSteamId) : CSteamID.Nil;

    [Header("Steam")]
    [Tooltip("'Nail It Or Fail It' Steam App ID. Sadece RestartAppIfNecessary icin kullanilir; " +
             "API'nin hangi app ile baslatilacagi proje kokundeki steam_appid.txt'ten okunur.")]
    [SerializeField] private uint appId = 4577740;

    /// <summary>
    /// Steam magazasindan kurulmus GERCEK build'lerde acilir: oyun Steam disinda baslatildiysa
    /// SteamAPI.RestartAppIfNecessary Steam'i devreye sokup bu sureci kapatir.
    ///
    /// Varsayilan KAPALI, cunku bu kontrol dagitilan her test build'inde oyunu "cift tikla ->
    /// aninda kapan" haline getiriyor: Steam disi her baslatma (masaustunden cift tiklamak,
    /// baska bir bilgisayarda Steam kapaliyken acmak, Proton/wine ile calistirmak) true donup
    /// Application.Quit() cagiriyor. Kanit: 2026-10-14 Player.log'larinda bu satirdan sonra
    /// hicbir sey yok, surec orada oluyor. Yayindan once tekrar acilacak.
    /// </summary>
    [SerializeField] private bool restartThroughSteamIfNecessary = false;

    /// <summary>Ayni surecte ikinci kez Init() cagirmak Steam tarafinda cokmez.</summary>
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
            // Domain reload kapaliyken (Enter Play Mode Options) statik alanlar sifirlanmaz;
            // bu durumda ikinci Init() cagirmak yerine mevcut durumu yeniden kullaniyoruz.
            Debug.LogWarning("[STEAM] SteamAPI bu surecte zaten baslatilmis; tekrar Init() cagrilmiyor.");
            return;
        }

        if (!Packsize.Test())
        {
            Debug.LogError("[STEAM] Packsize hatasi: Steamworks.NET paketi yanlis platform icin derlenmis. LAN moduna dusulecek.");
            return;
        }

        if (!DllCheck.Test())
        {
            Debug.LogError("[STEAM] Steamworks.NET native kutuphaneleri (steam_api*.dll/.so/.dylib) bulunamadi. LAN moduna dusulecek.");
            return;
        }

        // steam_appid.txt (exe'nin yani ya da proje koku) yalnizca gelistirme ve test
        // kurulumlarinda bulunur; Steam magazasindan kurulan oyunda bu dosya olmaz.
        // Bu yuzden varligini "bu bir gelistirme/test calistirmasi" isareti sayiyoruz.
        string appIdFile = System.IO.Path.Combine(Application.dataPath, "..", "steam_appid.txt");
        bool isDevSetup = System.IO.File.Exists(appIdFile);

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        // Sadece release build'lerde ve yalnizca Inspector'dan acilirsa (varsayilan kapali, bkz.
        // restartThroughSteamIfNecessary). Editor, development build'ler ve steam_appid.txt bulunan
        // test kurulumlari muaf: bu dosya test/dagitim disi kurulumlarin isareti.
        if (restartThroughSteamIfNecessary && !isDevSetup && appId != 480 &&
            SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
        {
            Debug.Log("[STEAM] Oyun Steam uzerinden baslatilmamis; Steam istemcisi uzerinden yeniden baslatiliyor.");
            Application.Quit();
            return;
        }
#endif

        // SteamAPI.Init() app id'yi CALISAN DIZINDEKI steam_appid.txt'ten ya da SteamAppId
        // ortam degiskeninden okur. Editor'un calisma dizini her zaman proje koku olmayabilir
        // (Hub'dan/Yol'undan baslatilinca farkli olabiliyor); o durumda Init sessizce
        // basarisiz olur ve kimse nedenini goremez. Proje kokundeki dosyayi okuyup ortam
        // degiskenine de yaziyoruz; boylece app id'nin tek kaynagi yine steam_appid.txt olur.
        // Init()'in gercekte hangi app id ile cagrildigini logda gosterebilmek icin saklıyoruz:
        // yanlis id yuzunden basarisiz olan bir Init'i "hesap oyunu oynayamiyor" diye okumak
        // yanlis teshise goturuyor.
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
                             $"Oyun LAN moduna dusecek.");
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
        // Callback'lerin (LobbyCreated_t, LobbyEnter_t, GameLobbyJoinRequested_t...) dagitilmasi
        // icin her karede cagrilmali.
        if (Initialized) SteamAPI.RunCallbacks();
    }

    /// <summary>
    /// Steam yokken FizzySteamworks transport'un kendi OnEnable'i bir saniye sonra
    /// InitRelayNetworkAccess cagirip alakasiz bir kirmizi hatayi doker (oyun hazir
    /// olarak LAN moduna duserken transport hic kullanilmaz). Bileseni kapatiyor,
    /// boylece o hatayi gormuyoruz.
    /// </summary>
    private static void DisableFizzyWhenSteamMissing()
    {
        var fizzies = FindObjectsByType<Mirror.FizzySteam.FizzySteamworks>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var fizzy in fizzies)
        {
            if (!fizzy.enabled) continue;

            fizzy.enabled = false;
            Debug.Log("[STEAM] Steam offline - FizzySteamworks disabled, LAN mode (KcpTransport) is the active path.");
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
