using System;
using kcp2k;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using UnityEngine;

/// <summary>
/// Steam lobileri + Steam Datagram Relay. EdgegapRelayManager'in yerini alir.
///
/// Sorumluluklar:
///   - Lobi olusturma / koda gore bulma / davet kabul etme / ayrilma
///   - Host'un SteamID64'ini Mirror'in networkAddress'ine cevirme
///     (FizzySteamworks, adresi UInt64.Parse eder)
///   - Editor/tek-makine testi icin LAN (KcpTransport) yedegi
///
/// Steam cagrilari SADECE SteamManager.Initialized iken yapilir; aksi halde
/// UseLanDevMode otomatik olarak true olur.
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("--- DEV: LAN modu ---")]
    [Tooltip("ACIK: Steam hic kullanilmaz, duz KcpTransport ile LAN/host IP uzerinden baglanilir. " +
             "Varsayilan KAPALI: SteamAPI hazirsa Steam (FizzySteamworks) modu kullanilir, yani " +
             "oyunu acan hesap host olur ve lobi kodu 6 haneli olur. SteamAPI baslatilamazsa " +
             "(istemci kapali / hesap uygulamaya erisemiyor) yine otomatik bu moda dusulur.")]
    [SerializeField] private bool forceLanMode = false;

    [Header("Steam Lobby")]
    [Tooltip("k_ELobbyTypePublic ZORUNLU: RequestLobbyList sadece public lobileri dondurur, " +
             "yani koda gore katilma ozelligi buna baglidir. Asil giris kapisi LobbyAuthenticator sifresidir.")]
    [SerializeField] private ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic;

    [Tooltip("Lobby metadata anahtarlari. Ayni app'in eski/yeni build'leri karismasin diye versiyonlanir.")]
    [SerializeField] private string codeKey = "nif_code";
    [SerializeField] private string versionKey = "nif_ver";
    [SerializeField] private string lobbyVersion = "1";

    // ============================ Durum ============================

    /// <summary>Etkin deger: Inspector'da acik VEYA SteamAPI baslatilamadi ise true.</summary>
    public bool UseLanDevMode { get; private set; }

    /// <summary>Steam uzerinden lobi islemleri yapilabilir mi?</summary>
    public bool SteamOnline => SteamManager.Initialized && !UseLanDevMode;

    public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;

    /// <summary>Sunucunun 6 haneli kodu (metadata'dan okunur).</summary>
    public string CurrentCode { get; private set; }

    /// <summary>Host'un SteamID64'u — Mirror'in networkAddress'ine yazilacak deger.</summary>
    public string HostSteamIdString { get; private set; }

    public string LastError { get; private set; }

    /// <summary>Davet kabul edilip kod cozuldugunde tetiklenir (UI sifre sorabilsin diye).</summary>
    public event Action<string> OnInviteCodeReady;

    // UI, olay kacmasi durumunda (SteamLobbyManager.Start()'tan sonra abone olanlar)
    // buradan okuyabilir.
    private static string s_PendingInviteCode;

    /// <summary>Bekleyen davet kodunu bir kez dondurur ve temizler.</summary>
    public static bool ConsumePendingInviteCode(out string code)
    {
        code = s_PendingInviteCode;
        s_PendingInviteCode = null;
        return !string.IsNullOrEmpty(code);
    }

    // ==================== Callback tutamaklari ====================
    // Bunlar MUTLAKA alan olarak tutulmali: yerel degiskene atanirsa GC toplar ve
    // callback bir daha hic tetiklenmez.
    private Callback<GameLobbyJoinRequested_t> cbJoinRequested;
    private Callback<LobbyDataUpdate_t> cbLobbyDataUpdate;
    private Callback<LobbyChatUpdate_t> cbLobbyChatUpdate;

    private CallResult<LobbyCreated_t> crLobbyCreated;
    private CallResult<LobbyEnter_t> crLobbyEnter;
    private CallResult<LobbyMatchList_t> crLobbyList;

    // Asenkron cagrilar arasinda tasinan sonuclar.
    private Action<string> createSuccess;
    private Action<string> pendingFail;
    private Action joinSuccess;
    private Action<string> joinFail;
    private string pendingCode;
    private int pendingMaxMembers;

    // ============================ Yasam dongusu ============================

    private void Awake()
    {
        // Sahne yeniden yuklendiginde olusan KOPYA, calisan ornegi ezmesin.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[STEAM-LOBBY] Ikinci SteamLobbyManager kopyasi yok ediliyor (tekil ornek korunuyor).");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Mod secimi: VARSAYILAN STEAM. SteamAPI hazirsa (SteamManager.Initialized) oyunu acan hesap
    /// host olur ve lobi kodu 6 haneli olur (FizzySteamworks). SteamAPI baslatilamazsa — istemci
    /// kapali, oyun Steam disinda baslatilmis ya da hesabin uygulamaya erisimi yok — otomatik
    /// olarak LAN (KcpTransport) moduna dusulur; o durumda LobbyID host'un LAN adresidir, cunku
    /// istemcinin baglanmak icin yazmasi gereken sey odur.
    ///
    /// Iki yonde de zorlama mumkun:
    ///   NIF_STEAM=1 / -steam / exe yanindaki NIF_STEAM dosyasi -> Steam'e zorla
    ///   NIF_LAN=1   / -lan   / exe yanindaki NIF_LAN dosyasi   -> LAN'e zorla
    /// Ayni sekilde Inspector'daki forceLanMode veya ortam degiskeni ile LAN'e zorlanabilir.
    /// (Editor'de "exe yani" proje kokudur; isaretci dosyayi silmek varsayilana dondurur.)
    ///
    /// DIKKAT - mod uyusmazligi: host Steam modunda, istemci LAN modundaysa baglanamazlar
    /// (FizzySteamworks &lt;-&gt; Kcp). Steam modunda lobiler app id basina ayridir; iki makinenin
    /// steam_appid.txt degeri de ayni olmalidir.
    /// </summary>
    private static bool ForceSteamRequested()
    {
        if (System.Environment.GetEnvironmentVariable("NIF_STEAM") == "1") return true;

        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg == "-steam" || arg == "--steam") return true;
        }

        return MarkerExists("NIF_STEAM");
    }

    private static bool ForceLanRequested()
    {
        if (System.Environment.GetEnvironmentVariable("NIF_LAN") == "1") return true;

        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg == "-lan" || arg == "--lan") return true;
        }

        return MarkerExists("NIF_LAN");
    }

    /// <summary>Exe'nin (Editor'de proje kokunun) yanindaki isaretci dosya var mi?</summary>
    private static bool MarkerExists(string fileName)
    {
        try
        {
            string marker = System.IO.Path.Combine(Application.dataPath, "..", fileName);
            return System.IO.File.Exists(marker);
        }
        catch (System.Exception)
        {
            // Okunamayan yol (orn. kisitli izinler) modu etkilemesin.
            return false;
        }
    }

    private void Start()
    {
        bool forceSteam = ForceSteamRequested();
        bool forceLan = ForceLanRequested();

        // Varsayilan Steam: oyunu acan hesap host olur ve kod 6 haneli olur. LAN'e yalnizca
        // acikca zorlaninca (NIF_LAN/-lan/forceLanMode) ya da SteamAPI hazir degilse dusulur.
        // forceSteam her zaman kazanir (zorlama celiskisinde Steam'e guveniyoruz).
        if (!forceSteam && (forceLan || forceLanMode))
        {
            UseLanDevMode = true;
            Debug.Log($"[STEAM-LOBBY] LAN (yerel) modu secili (zorlama={forceLan} forceLanMode={forceLanMode}). " +
                      "Steam modu icin NIF_STEAM=1 / -steam / NIF_STEAM isaretci dosyasi kullanin.");
            SwitchToLanTransport();
        }
        else if (!SteamManager.Initialized)
        {
            UseLanDevMode = true;
            Debug.LogWarning("[STEAM-LOBBY] SteamAPI baslatilamadi -> LAN moduna dusuldu. " +
                             "Cok oyunculu test icin 127.0.0.1 veya host'un LAN IP'sini kullanin. " +
                             "(Steam modu icin Steam istemcisi acik olmali ve hesap steam_appid.txt'teki " +
                             "uygulamaya erisebilmeli.)");
            SwitchToLanTransport();
        }
        else
        {
            UseLanDevMode = false;

            // Davet kabul edildiginde Steam bu callback'i tetikler.
            // CallbackDispatcher yalnizca SteamAPI.Init() basarili olduysa hazirdir;
            // bu yuzden kayit sadece bu dalda yapilir.
            cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);

            Debug.Log($"[STEAM-LOBBY] Steam modu aktif. (forceLanMode={forceLanMode} zorlama={forceSteam} " +
                      $"steamId={SteamManager.LocalSteamId})");
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;

        cbJoinRequested?.Dispose();
        cbLobbyDataUpdate?.Dispose();
        cbLobbyChatUpdate?.Dispose();
        crLobbyCreated?.Dispose();
        crLobbyEnter?.Dispose();
        crLobbyList?.Dispose();
    }

    // ============================ Lobi olusturma ============================

    /// <summary>
    /// Steam lobisi olusturur, kodu ve versiyonu metadata'ya yazar.
    /// Basarida onSuccess(6 haneli kod) cagrilir.
    /// </summary>
    public void CreateLobby(int maxMembers, Action<string> onSuccess, Action<string> onFail)
    {
        if (!SteamOnline)
        {
            Fail(onFail, "Steam kullanilamiyor (LAN modundasiniz).");
            return;
        }

        LastError = null;
        createSuccess = onSuccess;
        pendingFail = onFail;
        pendingCode = LobbyCodeCodec.RandomCode();
        pendingMaxMembers = maxMembers;

        crLobbyCreated?.Dispose();
        crLobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
        crLobbyCreated.Set(SteamMatchmaking.CreateLobby(lobbyType, maxMembers));

        Debug.Log($"[STEAM-LOBBY] Lobi olusturuluyor... type={lobbyType} max={maxMembers} code={pendingCode}");
    }

    private void OnLobbyCreated(LobbyCreated_t p, bool ioFailure)
    {
        if (ioFailure || p.m_eResult != EResult.k_EResultOK)
        {
            Fail(pendingFail, $"Steam lobisi olusturulamadi (result={p.m_eResult}).");
            return;
        }

        CurrentLobby = new CSteamID(p.m_ulSteamIDLobby);
        CurrentCode = pendingCode;

        // Host = lobby owner; ayrica metadata'da tutmaya gerek yok
        // (GetLobbyOwner ile her istemci ayni sonucu alir).
        HostSteamIdString = SteamManager.LocalSteamId.ToString();

        SteamMatchmaking.SetLobbyData(CurrentLobby, codeKey, CurrentCode);
        SteamMatchmaking.SetLobbyData(CurrentLobby, versionKey, lobbyVersion);
        SteamMatchmaking.SetLobbyJoinable(CurrentLobby, true);

        // Steam arayuzundeki "Oyna" dugmesinin calismasi icin.
        SteamFriends.SetRichPresence("connect", "+connect_lobby " + CurrentLobby.m_SteamID);

        RegisterLobbyCallbacks();

        Debug.Log($"[STEAM-LOBBY] Lobi hazir. id={CurrentLobby.m_SteamID} code={CurrentCode} max={pendingMaxMembers}");

        Action<string> cb = createSuccess;
        createSuccess = null;
        pendingFail = null;
        cb?.Invoke(CurrentCode);
    }

    // ============================ Koda gore katilma ============================

    /// <summary>6 haneli kodla lobi arar ve katilir.</summary>
    public void JoinLobbyByCode(string code, Action onSuccess, Action<string> onFail)
    {
        if (!SteamOnline)
        {
            Fail(onFail, "Steam kullanilamiyor (LAN modundasiniz).");
            return;
        }

        string normalized = LobbyCodeCodec.Canonicalize(code);
        LastError = null;

        // Davet yolu: kullanici zaten davet edildigi lobinin icinde olabilir.
        if (CurrentLobby != CSteamID.Nil &&
            string.Equals(CurrentCode, normalized, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[STEAM-LOBBY] Zaten bu lobidesiniz (code={normalized}); tekrar join cagrilmiyor.");
            onSuccess?.Invoke();
            return;
        }

        joinSuccess = onSuccess;
        joinFail = onFail;

        // Filtreler her RequestLobbyList cagrisinda sifirlanir; tekrar eklenmeli.
        SteamMatchmaking.AddRequestLobbyListStringFilter(codeKey, normalized, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(versionKey, lobbyVersion, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(20);

        crLobbyList?.Dispose();
        crLobbyList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        crLobbyList.Set(SteamMatchmaking.RequestLobbyList());

        Debug.Log($"[STEAM-LOBBY] '{normalized}' kodu araniyor...");
    }

    private void OnLobbyMatchList(LobbyMatchList_t p, bool ioFailure)
    {
        if (ioFailure)
        {
            Fail(joinFail, "Steam'e ulasilamadi (lobi listesi alinamadi).");
            return;
        }

        if (p.m_nLobbiesMatching == 0)
        {
            Fail(joinFail, "Bu koda sahip bir lobi bulunamadi.");
            return;
        }

        if (p.m_nLobbiesMatching > 1)
            Debug.LogWarning($"[STEAM-LOBBY] {p.m_nLobbiesMatching} lobi ayni koda sahip (cok dusuk olasilikli cakisma) — ilki kullaniliyor.");

        CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(0);
        JoinLobbyById(lobby, joinSuccess, joinFail);
    }

    // ============================ ID ile katilma ============================

    /// <summary>Bilinen bir Steam lobisine katilir (davet akisi + koda gore arama sonucu).</summary>
    public void JoinLobbyById(CSteamID lobbyId, Action onSuccess, Action<string> onFail)
    {
        if (!SteamOnline)
        {
            Fail(onFail, "Steam kullanilamiyor (LAN modundasiniz).");
            return;
        }

        LastError = null;
        joinSuccess = onSuccess;
        joinFail = onFail;

        crLobbyEnter?.Dispose();
        crLobbyEnter = CallResult<LobbyEnter_t>.Create(OnLobbyEnter);
        crLobbyEnter.Set(SteamMatchmaking.JoinLobby(lobbyId));

        Debug.Log($"[STEAM-LOBBY] Lobiye katiliniyor: {lobbyId.m_SteamID}");
    }

    private void OnLobbyEnter(LobbyEnter_t p, bool ioFailure)
    {
        if (ioFailure)
        {
            Fail(joinFail, "Steam'e ulasilamadi (lobiye girilemedi).");
            return;
        }

        if (p.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Fail(joinFail, "Lobiye girilemedi: " + DescribeEnterResponse(p.m_EChatRoomEnterResponse));
            return;
        }

        CurrentLobby = new CSteamID(p.m_ulSteamIDLobby);
        CurrentCode = SteamMatchmaking.GetLobbyData(CurrentLobby, codeKey);
        HostSteamIdString = SteamMatchmaking.GetLobbyOwner(CurrentLobby).m_SteamID.ToString();

        RegisterLobbyCallbacks();

        Debug.Log($"[STEAM-LOBBY] Lobiye girildi. id={CurrentLobby.m_SteamID} code='{CurrentCode}' host={HostSteamIdString}");

        Action cb = joinSuccess;
        joinSuccess = null;
        joinFail = null;
        cb?.Invoke();
    }

    // ============================ Davet ============================

    /// <summary>Steam arkadas davet penceresini acar (host).</summary>
    public void OpenInviteOverlay()
    {
        if (!SteamOnline)
        {
            Debug.LogWarning("[STEAM-LOBBY] Davet penceresi acilamaz: Steam modu aktif degil.");
            return;
        }

        if (CurrentLobby == CSteamID.Nil)
        {
            Debug.LogWarning("[STEAM-LOBBY] Davet penceresi acilamaz: henuz bir lobi yok.");
            return;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
    }

    /// <summary>
    /// Arkadas daveti kabul edildiginde Steam tarafindan tetiklenir. Lobiye girip kodu
    /// cikarir; sifre SORULMADAN baglanti KURULMAZ (sifre Steam metadata'sinda tutulmaz).
    /// UI OnInviteCodeReady / ConsumePendingInviteCode ile kod gelince sifre ister.
    /// </summary>
    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t p)
    {
        if (!SteamOnline) return;

        Debug.Log($"[STEAM-LOBBY] Davet kabul edildi: lobby={p.m_steamIDLobby.m_SteamID} friend={p.m_steamIDFriend.m_SteamID}");
        JoinLobbyFromInvite(p.m_steamIDLobby);
    }

    /// <summary>
    /// Davet edilen lobiye katilip kodu cikarir ve sifre sorulmasi icin olay yayar.
    /// Hem GameLobbyJoinRequested_t (oyun acikken) hem de "+connect_lobby" komut satiri
    /// argumani (oyun kapaliyken) bu yolu kullanir.
    /// </summary>
    public void JoinLobbyFromInvite(CSteamID lobbyId)
    {
        if (!SteamOnline)
        {
            Debug.LogWarning("[STEAM-LOBBY] Davet islenemedi: Steam modu aktif degil.");
            return;
        }

        if (CurrentLobby != CSteamID.Nil && CurrentLobby.m_SteamID == lobbyId.m_SteamID)
        {
            Debug.Log("[STEAM-LOBBY] Davet zaten icinde bulundugunuz lobi icin geldi; yok sayiliyor.");
            return;
        }

        JoinLobbyById(lobbyId,
            onSuccess: () =>
            {
                if (string.IsNullOrEmpty(CurrentCode))
                {
                    Debug.LogError("[STEAM-LOBBY] Davet edilen lobide 'code' metadata'si yok; sifre sorulamaz.");
                    return;
                }

                s_PendingInviteCode = CurrentCode;
                Debug.Log($"[STEAM-LOBBY] Davet kodu hazir: {CurrentCode} — sifre bekleniyor.");
                OnInviteCodeReady?.Invoke(CurrentCode);
            },
            onFail: err => Debug.LogError("[STEAM-LOBBY] Davete katilinamadi: " + err));
    }

    // ============================ Ayrilma ============================

    /// <summary>
    /// Lobiden ayrilir. Host ayrilirsa Steam lobiyi otomatik olarak dagitir;
    /// Edgegap'teki gibi silinecek bir sunucu tarafi kayit yoktur.
    /// </summary>
    public void LeaveLobby()
    {
        if (SteamManager.Initialized && CurrentLobby != CSteamID.Nil)
        {
            Debug.Log($"[STEAM-LOBBY] Lobiden ayriliniyor: {CurrentLobby.m_SteamID} (owner={SteamMatchmaking.GetLobbyOwner(CurrentLobby).m_SteamID})");
            SteamMatchmaking.LeaveLobby(CurrentLobby);
            SteamFriends.SetRichPresence("connect", null);   // null = anahtari sil
        }

        CurrentLobby = CSteamID.Nil;
        CurrentCode = null;
        HostSteamIdString = null;
        createSuccess = null;
        pendingFail = null;
        joinSuccess = null;
        joinFail = null;

        UnregisterLobbyCallbacks();
    }

    // ============ Lobi ici degisiklikler (teshis amacli) ============

    private void RegisterLobbyCallbacks()
    {
        if (cbLobbyDataUpdate == null) cbLobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        if (cbLobbyChatUpdate == null) cbLobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    private void UnregisterLobbyCallbacks()
    {
        cbLobbyDataUpdate?.Dispose();
        cbLobbyDataUpdate = null;
        cbLobbyChatUpdate?.Dispose();
        cbLobbyChatUpdate = null;
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t p)
    {
        if (CurrentLobby == CSteamID.Nil || p.m_ulSteamIDLobby != CurrentLobby.m_SteamID) return;

        // Kod degistiyse (orn. baska bir host metadata yazdi) yerel degeri tazele.
        string code = SteamMatchmaking.GetLobbyData(CurrentLobby, codeKey);
        if (!string.IsNullOrEmpty(code) && code != CurrentCode)
        {
            Debug.Log($"[STEAM-LOBBY] Kod metadata'si guncellendi: '{CurrentCode}' -> '{code}'");
            CurrentCode = code;
        }

        string owner = SteamMatchmaking.GetLobbyOwner(CurrentLobby).m_SteamID.ToString();
        if (owner != HostSteamIdString)
        {
            Debug.LogWarning($"[STEAM-LOBBY] Lobi sahibi degisti: {HostSteamIdString} -> {owner}");
            HostSteamIdString = owner;
        }
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t p)
    {
        if (CurrentLobby == CSteamID.Nil || p.m_ulSteamIDLobby != CurrentLobby.m_SteamID) return;

        Debug.Log($"[STEAM-LOBBY] Uye degisimi: member={p.m_ulSteamIDUserChanged} " +
                  $"stateChange={p.m_rgfChatMemberStateChange} toplam={SteamMatchmaking.GetNumLobbyMembers(CurrentLobby)}");
    }

    // ============================ LAN yedegi ============================

    /// <summary>
    /// LAN modunda transport'un gercekten Steam olmadigini garanti eder (kendini onarir).
    /// </summary>
    public void EnsureLanTransport()
    {
        if (!UseLanDevMode) return;

        NetworkManager nm = NetworkManager.singleton;
        if (nm == null) return;

        if (nm.transport == null || nm.transport is FizzySteamworks ||
            Transport.active == null || Transport.active is FizzySteamworks)
        {
            Debug.LogWarning("[LAN] Transport hala Steam gorunuyor — yeniden LAN'e cevriliyor.");
            SwitchToLanTransport();
        }
    }

    /// <summary>
    /// DEV: NetworkManager.transport'u duz KcpTransport ile degistirir.
    /// Steam transport bileseni ayni GameObject uzerinde kalabilir; Mirror yalnizca
    /// NetworkManager.transport ve statik Transport.active uzerinden calisir.
    /// </summary>
    private void SwitchToLanTransport()
    {
        try
        {
            NetworkManager nm = NetworkManager.singleton;
            if (nm == null) { Debug.Log("[LAN] NetworkManager henuz yok."); return; }

            KcpTransport plain = FindOrCreateLanTransport();
            if (plain == null) { Debug.LogError("[LAN] LanKcpTransport olusturulamadi."); return; }

            // Onemli: NetworkClient/NetworkServer, Mirror'in STATIK Transport.active
            // referansini kullanir (bkz. NetworkClient.Connect -> Transport.active.ClientConnect).
            // Sadece NetworkManager.transport'u degistirmek yetmiyor!
            nm.transport = plain;
            Transport.active = plain;

            Debug.Log($"[LAN MODU AKTIF] nm.transport={nm.transport.GetType().Name} " +
                      $"Transport.active={Transport.active.GetType().Name} (port={plain.Port}). " +
                      "Host: Create Lobby; Client: Join alanina host LAN IP'si (ayni PC'de 127.0.0.1).");
        }
        catch (Exception e)
        {
            Debug.LogError("[LAN] Transport degistirme hatasi: " + e);
        }
    }

    /// <summary>Ayri kok GameObject uzerinde duz KcpTransport bulur/olusturur.</summary>
    private static KcpTransport FindOrCreateLanTransport()
    {
        const string rootName = "LanKcpTransport";

        foreach (KcpTransport t in FindObjectsByType<KcpTransport>(FindObjectsSortMode.None))
            if (t != null) return t;

        GameObject go = GameObject.Find(rootName);
        if (go == null)
        {
            go = new GameObject(rootName);
            // NetworkManager DDOL oldugu icin transport da sahneler arasi yasamali,
            // aksi halde sahne degisiminde yok olup baglantiyi bozar.
            DontDestroyOnLoad(go);
        }
        return go.AddComponent<KcpTransport>();
    }

    // ============================ Yardimcilar ============================

    private void Fail(Action<string> onFail, string message)
    {
        LastError = message;
        Debug.LogError("[STEAM-LOBBY] " + message);
        onFail?.Invoke(message);
    }

    private static string DescribeEnterResponse(uint response)
    {
        switch ((EChatRoomEnterResponse)response)
        {
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist: return "lobi artik yok";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull: return "lobi dolu";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned: return "bu lobiden yasaklandiniz";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited: return "hesap kisitli (Steam'de harcama yapilmamis)";
            case EChatRoomEnterResponse.k_EChatRoomEnterResponseRatelimitExceeded: return "cok fazla deneme, biraz bekleyin";
            default: return "hata kodu " + response;
        }
    }
}
