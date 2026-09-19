using System;
using System.Collections;
using System.Text;
using Edgegap;
using kcp2k;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

public class EdgegapRelayManager : MonoBehaviour
{
    public static EdgegapRelayManager Instance;

    // ================== GECICI GELISTIRME MODU ==================
    // Edgegap relay tower'lari (orn. 104.105.82.166) peer dogrulamasi yapamadigi
    // surece LAN uzerinden test yapabilmek icin. Relay kodu SILINMEDI; sadece
    // bu anahtar acikken devre disi kalir. Edgegap duzeldiginde KAPATIN (false).
    [Header("--- GECICI DEV: LAN modu (Edgegap tower sorunu) ---")]
    [Tooltip("ACIK: relay hic kullanilmaz, duz KcpTransport ile LAN/host IP uzerinden baglanilir.")]
    [SerializeField] private bool useLanDevMode = true;
    public bool UseLanDevMode => useLanDevMode;
    // ===========================================================

    [Header("Edgegap")]
    [Tooltip("Gelistirme testi: host ve client AYNI public IP'deyse (tek bilgisayarda iki instance) " +
             "relay'e iki kullanici kaydedilir. Production'da false birakin.")]
    [SerializeField] private bool allowSameIpTestMode = true;

    [SerializeField] private string relayProfileToken = "BURAYA_API_TOKEN_YAZ";
    [SerializeField] private string relayProfileSlug = "BURAYA_PROFIL_SLUG_YAZ";

    private const string ApiBase = "https://api.edgegap.com/v1/relays/sessions";

    private EdgegapKcpTransport edgegapTransport;

    /// <summary>Relay'in bize atadigi son tower IP'si (teshis/uyari icin).</summary>
    public string LastRelayIp { get; private set; }

    // Son aktif relay bilgisini tutar (Leave cleanup için)
    private string lastSessionId;
    public uint lastUserAuthToken;

    /// <summary>
    /// Host lobby'den ayrılınca: relay session'ı tümüyle sil (Edgegap'te kalmasın).
    /// HTTP DELETE /v1/relays/sessions/{session_id}
    /// </summary>
    public void DeleteRelaySession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(relayProfileToken)) return;
        StartCoroutine(DeleteSessionRoutine(sessionId, null, null));
    }

    IEnumerator DeleteSessionRoutine(string sessionId, Action onSuccess, Action onFail)
    {
        using UnityWebRequest req = new UnityWebRequest($"{ApiBase}/{sessionId}", "DELETE");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"token {relayProfileToken}");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success && req.responseCode != 404)
        {
            Debug.LogWarning("Relay session silinemedi: " + req.error + " " + req.responseCode);
            onFail?.Invoke();
        }
        else
        {
            Debug.Log("Edgegap relay session silindi: " + sessionId);
            onSuccess?.Invoke();
        }
        yield break;
    }

    /// <summary>
    /// 6 haneli koddan Edgegap session_id çözer: aktif session listesini çekip
    /// her session için Code6 hesaplar, eşleşeni döndürür. Ek servis gerekmez.
    /// </summary>
    public void ResolveShortCode(string code6, Action<string> onSuccess, Action onFail)
    {
        StartCoroutine(ResolveShortCodeRoutine(code6, onSuccess, onFail));
    }

    private IEnumerator ResolveShortCodeRoutine(string code6, Action<string> onSuccess, Action onFail)
    {
        if (!LobbyCodeCodec.IsValidShortCode(code6))
        {
            onFail?.Invoke();
            yield break;
        }

        string normalized = code6.ToUpperInvariant();

        using UnityWebRequest req = UnityWebRequest.Get(ApiBase); // GET /v1/relays/sessions
        req.SetRequestHeader("Authorization", $"token {relayProfileToken}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Relay session listesi alınamadı: " + req.error + " | " + req.downloadHandler.text);
            onFail?.Invoke();
            yield break;
        }

        SessionListResponse list = JsonUtility.FromJson<SessionListResponse>(req.downloadHandler.text);
        if (list == null || list.sessions == null || list.sessions.Length == 0)
        {
            Debug.LogError("Aktif relay session bulunamadı.");
            onFail?.Invoke();
            yield break;
        }

        string match = null;
        int matches = 0;
        foreach (var s in list.sessions)
        {
            if (string.IsNullOrEmpty(s.session_id)) continue;
            if (!s.ready) continue;
            if (LobbyCodeCodec.Code6(s.session_id) == normalized)
            {
                match = s.session_id;
                matches++;
                if (matches == 1) Debug.Log("Kod eşleşti: " + s.session_id);
            }
        }

        if (matches > 1)
            Debug.LogWarning($"UYARI: {matches} session aynı 6 haneli koda düştü — ilk eşleşme kullanılıyor (çok düşük olasılıklı çakışma).");

        if (string.IsNullOrEmpty(match))
        {
            Debug.LogError("Bu koda uyan aktif session yok: " + normalized);
            onFail?.Invoke();
            yield break;
        }

        onSuccess?.Invoke(match);
    }

    [Serializable]
    private class SessionListResponse
    {
        public EdgegapSessionResponse[] sessions;
        public int total_count;
    }

    private void Awake()
    {
        // Sahne yeniden yuklendiginde olusan KOPYA, calisan ornegi ezmesin.
        // (Offline sahne yuklemesi sirasinda "duplicate NetworkManager" durumunda
        //  kopyanin Awake'i calisip Instance'i ele geciriyordu -> MissingReferenceException)
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RELAY] Ikinci EdgegapRelayManager kopyasi yok ediliyor (tekil ornek korunuyor).");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (useLanDevMode)
            SwitchToLanTransport();

        // Relay kullanilacaksa transport referansini al (LAN modunda null kalir).
        edgegapTransport = NetworkManager.singleton != null
            ? NetworkManager.singleton.transport as EdgegapKcpTransport
            : null;

        // 6 haneli kod offline üretiliyor (LobbyCodeCodec.Code6); join tarafı aktif
        // relay session listesinden eşleştiriyor. Her zaman Edgegap relay kullanılır.

        // ParrelSync / build args auto-start (-host / -client / -join=CODE).
        if (FindAnyObjectByType<AutoStartFromArgs>() == null)
        {
            GameObject auto = new GameObject("AutoStartFromArgs");
            auto.AddComponent<AutoStartFromArgs>();
        }
    }

    /// <summary>
    /// GECICI DEV: NetworkManager.transport'u duz KcpTransport ile degistirir.
    /// Unity ayni GameObject'e ikinci KcpTransport eklemedigi icin ayri child kullanilir.
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

            EdgegapKcpTransport eg = GetComponent<EdgegapKcpTransport>();
            if (eg != null) eg.enabled = false;

            Debug.Log($"[LAN MODU AKTIF] nm.transport={nm.transport.GetType().Name} " +
                      $"Transport.active={Transport.active.GetType().Name} (port={plain.Port}). " +
                      "Host: Create Lobby; Client: Join alanina host LAN IP'si (ayni PC'de 127.0.0.1).");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[LAN] Transport degistirme hatasi: " + e);
        }
    }

    /// <summary>Ayri kok GameObject uzerinde duz KcpTransport bulur/olusturur.</summary>
    private static KcpTransport FindOrCreateLanTransport()
    {
        const string rootName = "LanKcpTransport";

        foreach (KcpTransport t in UnityEngine.Object.FindObjectsByType<KcpTransport>(FindObjectsSortMode.None))
            if (t != null && !(t is EdgegapKcpTransport)) return t;

        GameObject go = GameObject.Find(rootName);
        if (go == null)
        {
            go = new GameObject(rootName);
            // NetworkManager DDOL oldugu icin transport da sahneler arasi yasamali,
            // aksi halde sahne degisiminde yok olup baglantiyi bozar.
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        return go.AddComponent<KcpTransport>();
    }

    /// <summary>LAN modunda transport'un gercekten relay olmadigini garanti eder (kendini onarir).</summary>
    public void EnsureLanTransport()
    {
        if (!useLanDevMode) return;

        var nm = NetworkManager.singleton;
        if (nm == null) return;

        if ((nm.transport == null || nm.transport is EdgegapKcpTransport) ||
            (Transport.active == null || Transport.active is EdgegapKcpTransport))
        {
            Debug.LogWarning("[LAN] Transport hala Edgegap gorunuyor — yeniden LAN'e cevriliyor.");
            SwitchToLanTransport();
        }
    }

    /// <summary>Host'un relay server'i dogrulandi mi? (relay tower cevap verdi mi)</summary>
    public bool IsRelayServerValid()
    {
        return ReadState("server", "state") == "Valid";
    }

    /// <summary>Client'in relay baglantisi dogrulandi mi?</summary>
    public bool IsRelayClientValid()
    {
        return ReadState("client", "connectionState") == "Valid";
    }

    /// <summary>Reflection ile transport ic nesnesinin state alanini okur (teshis).</summary>
    private string ReadState(string fieldName, string stateName)
    {
        try
        {
            var transport = NetworkManager.singleton != null ? NetworkManager.singleton.transport : null;
            if (transport == null) return "n/a";

            System.Reflection.FieldInfo fi = null;
            System.Type t = transport.GetType();
            while (t != null && fi == null)
            {
                fi = t.GetField(fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                t = t.BaseType;
            }
            if (fi == null) return "n/a";

            object inner = fi.GetValue(transport);
            if (inner == null) return "null";

            var sf = inner.GetType().GetField(stateName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return sf != null ? (sf.GetValue(inner)?.ToString() ?? "null") : "?";
        }
        catch (System.Exception e)
        {
            return "err:" + e.GetType().Name;
        }
    }

    /// <summary>Inspector'daki Edgegap API token'ını (registry de kullanabilir) döndürür.</summary>
    public string ApiToken => relayProfileToken;
    /// <summary>Inspector'daki relay profile slug'ını döndürür.</summary>
    public string ApiSlug => relayProfileSlug;

    public void CreateRelaySession(int maxPlayers, Action<string> onSuccess, Action onFail)
    {
        if (edgegapTransport == null)
        {
            Debug.LogError("[RELAY] Edgegap transport yok (LAN modu acik olabilir) — relay session kurulamaz.");
            onFail?.Invoke();
            return;
        }
        StartCoroutine(CreateSessionRoutine(maxPlayers, onSuccess, onFail));
    }

    public void JoinRelaySession(string sessionId, Action onSuccess, Action onFail)
    {
        if (edgegapTransport == null)
        {
            Debug.LogError("[RELAY] Edgegap transport yok (LAN modu acik olabilir) — relay'e katilinamaz.");
            onFail?.Invoke();
            return;
        }
        StartCoroutine(JoinSessionRoutine(sessionId, onSuccess, onFail));
    }

    private IEnumerator CreateSessionRoutine(int maxPlayers, Action<string> onSuccess, Action onFail)
    {

        string publicIp = null;
        using (UnityWebRequest ipReq = UnityWebRequest.Get("https://api.ipify.org"))
        {
            yield return ipReq.SendWebRequest();
            if (ipReq.result == UnityWebRequest.Result.Success)
                publicIp = ipReq.downloadHandler.text.Trim();
            else
                publicIp = "0.0.0.0";
        }

        Debug.Log("Public IP: " + publicIp);

        string usersJson = allowSameIpTestMode
            ? $"{{\"ip\": \"{publicIp}\"}}, {{\"ip\": \"{publicIp}\"}}"     // dev: tek IP'de 2 kullanici
            : $"{{\"ip\": \"{publicIp}\"}}";

        string body = $"{{\"relay_profile_slug\": \"{relayProfileSlug}\", \"users\": [{usersJson}]}}";

        if (allowSameIpTestMode)
            Debug.Log("SameIpTestMode ACIK: relay session 2 kullaniciyla olusturuluyor (ayni IP).");

        using UnityWebRequest req = new UnityWebRequest(ApiBase, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"token {relayProfileToken}");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Relay session oluşturulamadı: " + req.error + " | " + req.downloadHandler.text);
            onFail?.Invoke();
            yield break;
        }

        EdgegapSessionResponse data = JsonUtility.FromJson<EdgegapSessionResponse>(req.downloadHandler.text);

        yield return WaitForRelayReady(data.session_id, (readyData) =>
        {
            edgegapTransport.relayAddress = readyData.relay.ip;
            edgegapTransport.relayGameServerPort = readyData.relay.ports.server.port;
            edgegapTransport.relayGameClientPort = readyData.relay.ports.client.port;
            edgegapTransport.sessionId = readyData.authorization_token;
            edgegapTransport.userId = readyData.session_users[0].authorization_token;
            edgegapTransport.relayGUI = false;

            lastSessionId = readyData.session_id;
            lastUserAuthToken = readyData.session_users[0].authorization_token;
            LastRelayIp = readyData.relay.ip;

            Debug.Log($"HOST status={readyData.status} linked={readyData.linked}");
            Debug.Log($"HOST RELAY> session={readyData.session_id} ip={readyData.relay.ip} " +
                      $"serverPort={readyData.relay.ports.server.port} clientPort={readyData.relay.ports.client.port} " +
                      $"sessionToken={readyData.authorization_token} userToken={readyData.session_users[0].authorization_token} " +
                      $"users={readyData.session_users.Length}");
            onSuccess?.Invoke(readyData.session_id);
        }, onFail);
    }

    /// <summary>
    /// Client lobby'den ayrılınca: kendi authorization token'ini relay'den iptal et.
    /// HTTP POST /v1/relays/sessions:revoke-user
    /// </summary>
    public void RevokeSelfInSession(string sessionId, uint userToken)
    {
        if (string.IsNullOrEmpty(sessionId) || userToken == 0) return;
        StartCoroutine(RevokeUserRoutine(sessionId, userToken, null, null));
    }

    IEnumerator RevokeUserRoutine(string sessionId, uint userToken, Action onSuccess, Action onFail)
    {
        string url = $"{ApiBase}:revoke-user";
        string body = $"{{\"session_id\": \"{sessionId}\", \"authorization_token\": {userToken}}}";

        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"token {relayProfileToken}");

        yield return req.SendWebRequest();

        bool ok = req.result == UnityWebRequest.Result.Success || req.responseCode == 204;
        if (!ok) Debug.LogWarning("Relay user iptal edilemedi: " + req.error);
        else Debug.Log("Kullanıcı relay session'dan çekildi.");

        if (ok) onSuccess?.Invoke(); else onFail?.Invoke();
        yield break;
    }

    // Bu sınıfın katıldığı son relay oturumu bilgileri, Leave'de revoke çağrısı için saklanır.
    private IEnumerator JoinSessionRoutine(string sessionId, Action onSuccess, Action onFail)
    {
        // SameIpTestMode: kullanıcı zaten host tarafında session'a eklenmiş;
        // authorize-user çağrısı (aynı IP yüzünden) gerekmez ve başarısız olur.
        if (allowSameIpTestMode)
        {
            Debug.Log("SameIpTestMode ACIK: authorize-user atlanıyor, session'daki 2. kullanıcı kullanılacak.");
            yield return WaitForRelayReady(sessionId, readyData =>
            {
                int idx = readyData.session_users.Length - 1;
                Debug.Log($"CLIENT authorize(skip)> status={readyData.status} linked={readyData.linked} users={readyData.session_users.Length}");

                edgegapTransport.relayAddress = readyData.relay.ip;
                edgegapTransport.relayGameServerPort = readyData.relay.ports.server.port;
                edgegapTransport.relayGameClientPort = readyData.relay.ports.client.port;
                edgegapTransport.sessionId = readyData.authorization_token;
                edgegapTransport.userId = readyData.session_users[idx].authorization_token;
                edgegapTransport.relayGUI = false;

                lastSessionId = sessionId;
                lastUserAuthToken = readyData.session_users[idx].authorization_token;

                Debug.Log($"CLIENT RELAY> session={sessionId} ip={readyData.relay.ip} serverPort={readyData.relay.ports.server.port} " +
                          $"clientPort={readyData.relay.ports.client.port} sessionToken={readyData.authorization_token} " +
                          $"userToken={readyData.session_users[idx].authorization_token} users={readyData.session_users.Length} userIndex={idx}");
                onSuccess?.Invoke();
            }, onFail);
            yield break;
        }

        string publicIp = null;
        using (UnityWebRequest ipReq = UnityWebRequest.Get("https://api.ipify.org"))
        {
            yield return ipReq.SendWebRequest();
            publicIp = ipReq.result == UnityWebRequest.Result.Success
                ? ipReq.downloadHandler.text.Trim()
                : "0.0.0.0";
        }

        string url = $"{ApiBase}:authorize-user";
        string body = $"{{\"session_id\": \"{sessionId}\", \"user_ip\": \"{publicIp}\"}}";

        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"token {relayProfileToken}");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Relay'e katılınamadı: " + req.error + " | " + req.downloadHandler.text);
            onFail?.Invoke();
            yield break;
        }

        yield return WaitForRelayReady(sessionId, (readyData) =>
        {

            int userIndex = readyData.session_users.Length - 1;

            if (readyData.session_users.Length <= 1)
            {
                Debug.LogWarning("UYARI: authorize-user yeni kullanıcı EKLEMEDİ (session_users=" +
                                 readyData.session_users.Length + "). " +
                                 "En sık sebep: host ile AYNI public IP'den bağlanılıyor. " +
                                 "Edgegap relay IP bazlı kullanıcı sayar; aynı IP'de iki instance = SessionTimeout. " +
                                 "TEST ICIN: client'ı FARKLI bir agdan (mobil hotspot / VPN) calistirin.");
            }

            Debug.Log($"CLIENT authorize> status={readyData.status} linked={readyData.linked} " +
                      $"users={readyData.session_users.Length} " +
                      $"userTokens=[{string.Join(",", System.Array.ConvertAll(readyData.session_users, u => u.authorization_token.ToString()))}]");

            edgegapTransport.relayAddress = readyData.relay.ip;
            edgegapTransport.relayGameServerPort = readyData.relay.ports.server.port;
            edgegapTransport.relayGameClientPort = readyData.relay.ports.client.port;
            edgegapTransport.sessionId = readyData.authorization_token;
            edgegapTransport.userId = readyData.session_users[userIndex].authorization_token;
            edgegapTransport.relayGUI = false;

            lastSessionId = sessionId;
            lastUserAuthToken = readyData.session_users[userIndex].authorization_token;

            Debug.Log($"CLIENT RELAY> session={sessionId} ip={readyData.relay.ip} " +
                      $"serverPort={readyData.relay.ports.server.port} clientPort={readyData.relay.ports.client.port} " +
                      $"sessionToken={readyData.authorization_token} userToken={readyData.session_users[userIndex].authorization_token} " +
                      $"users={readyData.session_users.Length} userIndex={userIndex}");
            Debug.Log("Relay'e katılındı: " + sessionId);
            onSuccess?.Invoke();
        }, onFail);
    }

    private IEnumerator WaitForRelayReady(string sessionId, Action<EdgegapSessionResponse> onReady, Action onFail)
    {
        string url = $"{ApiBase}/{sessionId}";
        int maxAttempts = 60;

        string lastStatus = "?";
        string lastRaw = "?";

        for (int i = 0; i < maxAttempts; i++)
        {
            yield return new WaitForSeconds(1f);

            using UnityWebRequest req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"token {relayProfileToken}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Relay status check failed ({i + 1}/{maxAttempts}): " + req.error);
                continue;
            }

            lastRaw = req.downloadHandler.text;
            EdgegapSessionResponse data = JsonUtility.FromJson<EdgegapSessionResponse>(lastRaw);

            int users = data != null && data.session_users != null ? data.session_users.Length : -1;
            lastStatus = data != null ? data.status : "null";

            Debug.Log($"Relay status ({i + 1}/{maxAttempts}): ready={(data != null ? data.ready.ToString() : "null")} " +
                      $"status={lastStatus} linked={(data != null ? data.linked.ToString() : "?")} users={users}");

            if (data != null && data.ready)
            {
                onReady?.Invoke(data);
                yield break;
            }
        }

        Debug.LogError($"Relay timeout - not ready after {maxAttempts}s. " +
                       $"Last status='{lastStatus}'. Raw response:\n{lastRaw}");

        // A failed creation used to leave the session behind on Edgegap, where it kept
        // counting as an active user and could block later sessions.
        DeleteRelaySession(sessionId);

        onFail?.Invoke();
    }
}
[Serializable]
public class EdgegapSessionResponse
{
    public string session_id;
    public uint authorization_token;
    public bool ready;
    public bool linked;
    public string status;
    public EdgegapRelay relay;
    public EdgegapSessionUser[] session_users;
}

[Serializable]
public class EdgegapRelay
{
    public string ip;
    public EdgegapPorts ports;
}

[Serializable]
public class EdgegapPorts
{
    public EdgegapPort client;
    public EdgegapPort server;
}

[Serializable]
public class EdgegapPort
{
    public ushort port;
}

[Serializable]
public class EdgegapSessionUser
{
    public uint authorization_token;
}
