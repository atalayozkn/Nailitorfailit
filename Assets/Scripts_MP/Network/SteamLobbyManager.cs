using System;
using kcp2k;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using UnityEngine;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Steam Lobby")]
    [Tooltip("k_ELobbyTypePublic ZORUNLU: RequestLobbyList sadece public lobileri dondurur, " +
             "yani koda gore katilma ozelligi buna baglidir. Asil giris kapisi LobbyAuthenticator sifresidir.")]
    [SerializeField] private ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic;

    [Tooltip("Lobby metadata anahtarlari. Ayni app'in eski/yeni build'leri karismasin diye versiyonlanir.")]
    [SerializeField] private string codeKey = "nif_code";
    [SerializeField] private string versionKey = "nif_ver";
    [SerializeField] private string lobbyVersion = "1";

    public bool SteamOnline => SteamManager.Initialized;

    public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;

    public string CurrentCode { get; private set; }

    public string HostSteamIdString { get; private set; }

    public string LastError { get; private set; }

    public event Action<string> OnInviteCodeReady;

    private static string s_PendingInviteCode;

    public static bool ConsumePendingInviteCode(out string code)
    {
        code = s_PendingInviteCode;
        s_PendingInviteCode = null;
        return !string.IsNullOrEmpty(code);
    }

    private Callback<GameLobbyJoinRequested_t> cbJoinRequested;
    private Callback<LobbyDataUpdate_t> cbLobbyDataUpdate;
    private Callback<LobbyChatUpdate_t> cbLobbyChatUpdate;

    private CallResult<LobbyCreated_t> crLobbyCreated;
    private CallResult<LobbyEnter_t> crLobbyEnter;
    private CallResult<LobbyMatchList_t> crLobbyList;

    private Action<string> createSuccess;
    private Action<string> pendingFail;
    private Action joinSuccess;
    private Action<string> joinFail;
    private string pendingCode;
    private int pendingMaxMembers;

    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[STEAM-LOBBY] Ikinci SteamLobbyManager kopyasi yok ediliyor (tekil ornek korunuyor).");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private static bool ForceSteamRequested()
    {
        if (System.Environment.GetEnvironmentVariable("NIF_STEAM") == "1") return true;

        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg == "-steam" || arg == "--steam") return true;
        }

        return MarkerExists("NIF_STEAM");
    }

    private static bool MarkerExists(string fileName)
    {
        try
        {
            string marker = System.IO.Path.Combine(Application.dataPath, "..", fileName);
            return System.IO.File.Exists(marker);
        }
        catch (System.Exception)
        {

            return false;
        }
    }

    private void Start()
    {
        bool forceSteam = ForceSteamRequested();

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[STEAM-LOBBY] SteamAPI baslatilamadi. Multiplayer icin Steam istemcisi acik olmali " +
                           "ve hesap steam_appid.txt'teki uygulamaya erisebilmeli. LAN modu kaldirildi.");
            return;
        }

        cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);

        Debug.Log($"[STEAM-LOBBY] Steam modu aktif. (zorlama={forceSteam} steamId={SteamManager.LocalSteamId})");
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

    public void CreateLobby(int maxMembers, Action<string> onSuccess, Action<string> onFail)
    {
        if (!SteamOnline)
        {
            Fail(onFail, "Steam kullanilamiyor (SteamAPI hazir degil).");
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

        HostSteamIdString = SteamManager.LocalSteamId.ToString();

        SteamMatchmaking.SetLobbyData(CurrentLobby, codeKey, CurrentCode);
        SteamMatchmaking.SetLobbyData(CurrentLobby, versionKey, lobbyVersion);
        SteamMatchmaking.SetLobbyJoinable(CurrentLobby, true);

        SteamFriends.SetRichPresence("connect", "+connect_lobby " + CurrentLobby.m_SteamID);

        RegisterLobbyCallbacks();

        Debug.Log($"[STEAM-LOBBY] Lobi hazir. id={CurrentLobby.m_SteamID} code={CurrentCode} max={pendingMaxMembers}");

        Action<string> cb = createSuccess;
        createSuccess = null;
        pendingFail = null;
        cb?.Invoke(CurrentCode);
    }

    public void JoinLobbyByCode(string code, Action onSuccess, Action<string> onFail)
    {
        if (!SteamOnline)
        {
            Fail(onFail, "Steam kullanilamiyor (SteamAPI hazir degil).");
            return;
        }

        string normalized = LobbyCodeCodec.Canonicalize(code);
        LastError = null;

        if (CurrentLobby != CSteamID.Nil &&
            string.Equals(CurrentCode, normalized, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[STEAM-LOBBY] Zaten bu lobidesiniz (code={normalized}); tekrar join cagrilmiyor.");
            onSuccess?.Invoke();
            return;
        }

        joinSuccess = onSuccess;
        joinFail = onFail;

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

    public void JoinLobbyById(CSteamID lobbyId, Action onSuccess, Action<string> onFail)
    {
        if (!SteamOnline)
        {
            Fail(onFail, "Steam kullanilamiyor (SteamAPI hazir degil).");
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

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t p)
    {
        if (!SteamOnline) return;

        Debug.Log($"[STEAM-LOBBY] Davet kabul edildi: lobby={p.m_steamIDLobby.m_SteamID} friend={p.m_steamIDFriend.m_SteamID}");
        JoinLobbyFromInvite(p.m_steamIDLobby);
    }

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

    public void LeaveLobby()
    {
        if (SteamManager.Initialized && CurrentLobby != CSteamID.Nil)
        {
            Debug.Log($"[STEAM-LOBBY] Lobiden ayriliniyor: {CurrentLobby.m_SteamID} (owner={SteamMatchmaking.GetLobbyOwner(CurrentLobby).m_SteamID})");
            SteamMatchmaking.LeaveLobby(CurrentLobby);
            SteamFriends.SetRichPresence("connect", null);
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
