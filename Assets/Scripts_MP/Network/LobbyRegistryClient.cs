using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 6 haneli Lobby kodu <-> Edgegap Relay Session eşlemesini Edgegap
/// Lobby Service'te (deploy edilen lobby servisi) tutar.
///
/// Host: relay session'ı EdgegapRelayManager oluşturur, sonra bu kod
/// Lobby Service'e kaydedilir (LobbyCreateRequest.name = kod, tags[0] = session_id).
/// Join: kod Lobby Service'te aranır, session_id çözülür, normal relay akışı sürer.
/// </summary>
public class LobbyRegistryClient : MonoBehaviour
{
    public static LobbyRegistryClient Instance;

    [Header("Lobby Service")]
    [Tooltip("Edgegap Dashboard'dan aldığınız App API Key")]
    [SerializeField] private string appApiKey = "BURAYA_APP_API_KEY_YAZ";
    [Tooltip("Edgegap'te deploy edilen lobby servisin adı")]
    [SerializeField] private string lobbyServiceName = "nailitorfailit-lobby";

    private string lobbyUrl;
    private bool serviceReady;
    private string lastCreatedLobbyId;   // Leave'de silmek için
    private string lastCreatedLobbyCode;
    private string lastPollStatus;       // son poll'un statusu

    private const string PrefKey = "EdgegapLobbyServiceUrl";
    // Ana oyun akışını bloklamamak için kısa bütçe.
    private const int ServiceReadyBudgetSeconds = 5;
    private const int DeployTimeoutSeconds = 300;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (string.IsNullOrEmpty(appApiKey) || appApiKey.StartsWith("BURAYA"))
            appApiKey = EdgegapRelayManager.Instance != null ? EdgegapRelayManager.Instance.ApiToken : appApiKey;

        // Daha önce READY olmuş servis URL'sini geri yükle (hızlı yol).
        string saved = PlayerPrefs.GetString(PrefKey, null);
        if (!string.IsNullOrEmpty(saved))
        {
            lobbyUrl = saved;
            serviceReady = true;
            Debug.Log("Lobby Service URL cache'ten yüklendi: " + lobbyUrl);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Kullanılacak API key.</summary>
    public string EffectiveApiKey => string.IsNullOrEmpty(appApiKey) ? "" : appApiKey.Trim();

    // ============================================================ HOST

    public void RegisterLobby(string code, int maxPlayers, string relaySessionId, Action onSuccess, Action onFail)
    {
        StartCoroutine(RegisterRoutine(code, maxPlayers, relaySessionId, onSuccess, onFail));
    }

    private IEnumerator RegisterRoutine(string code, int maxPlayers, string relaySessionId, Action onSuccess, Action onFail)
    {
        bool ready = false;
        yield return EnsureServiceReady(ok => ready = ok);
        if (!ready)
        {
            onFail?.Invoke();
            yield break;
        }

        string body =
            "{\"capacity\": " + maxPlayers + "," +
            " \"is_joinable\": true," +
            " \"name\": \"" + code + "\"," +
            " \"tags\": [\"" + relaySessionId + "\"]," +
            " \"player\": {\"id\": \"host\"}}";

        using UnityWebRequest req = new UnityWebRequest($"{lobbyUrl}/lobbies", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Lobby kaydı başarısız ({req.responseCode}): {req.error} | body: {req.downloadHandler.text}");
            onFail?.Invoke();
            yield break;
        }

        Edgegap.Lobby parsed = JsonUtility.FromJson<Edgegap.Lobby>(req.downloadHandler.text);
        lastCreatedLobbyId = parsed.lobby_id;
        lastCreatedLobbyCode = code;
        Debug.Log($"Lobby kaydı başarılı. code={code} lobbyId={lastCreatedLobbyId} tags={relaySessionId}");
        onSuccess?.Invoke();
    }

    public void DeleteRegistryEntry()
    {
        if (string.IsNullOrEmpty(lastCreatedLobbyId) || string.IsNullOrEmpty(lobbyUrl)) return;
        StartCoroutine(DeleteRoutine(lastCreatedLobbyId, null, null));
    }

    private IEnumerator DeleteRoutine(string lobbyId, Action onSuccess, Action onFail)
    {
        using UnityWebRequest req = new UnityWebRequest($"{lobbyUrl}/lobbies/{lobbyId}", "DELETE");
        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success && req.responseCode != 404 && req.responseCode != 204)
        {
            Debug.LogWarning($"Lobby kaydı silinemedi ({req.responseCode}): {req.error}");
            onFail?.Invoke();
        }
        else
        {
            Debug.Log("Lobby Service kaydı silindi: " + lobbyId);
            lastCreatedLobbyCode = null;
            lastCreatedLobbyId = null;
            onSuccess?.Invoke();
        }
    }

    // ============================================================ JOIN

    public void ResolveCode(string code, Action<string> onSuccess, Action onFail)
    {
        StartCoroutine(ResolveRoutine(code, onSuccess, onFail));
    }

    private IEnumerator ResolveRoutine(string code, Action<string> onSuccess, Action onFail)
    {
        bool ready = false;
        yield return EnsureServiceReady(ok => ready = ok);
        if (!ready)
        {
            onFail?.Invoke();
            yield break;
        }

        using UnityWebRequest listReq = UnityWebRequest.Get($"{lobbyUrl}/lobbies");
        yield return listReq.SendWebRequest();

        if (listReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Lobby listesi alınamadı: " + listReq.error);
            onFail?.Invoke();
            yield break;
        }

        Edgegap.ListLobbiesResponse parsed = JsonUtility.FromJson<Edgegap.ListLobbiesResponse>(listReq.downloadHandler.text);
        string lobbyId = null;
        if (parsed.data != null)
        {
            foreach (var br in parsed.data)
            {
                if (string.Equals(br.name, code, StringComparison.OrdinalIgnoreCase))
                {
                    lobbyId = br.lobby_id;
                    if (br.tags != null && br.tags.Length > 0)
                    {
                        onSuccess?.Invoke(br.tags[0]);
                        yield break;
                    }
                    break;
                }
            }
        }

        if (lobbyId == null)
        {
            Debug.LogError("Kod bulunamadı: " + code);
            onFail?.Invoke();
            yield break;
        }

        using UnityWebRequest getReq = UnityWebRequest.Get($"{lobbyUrl}/lobbies/{lobbyId}");
        yield return getReq.SendWebRequest();

        if (getReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Lobby bilgisi alınamadı: " + getReq.error);
            onFail?.Invoke();
            yield break;
        }

        Edgegap.Lobby single = JsonUtility.FromJson<Edgegap.Lobby>(getReq.downloadHandler.text);
        if (single.tags != null && single.tags.Length > 0)
        {
            onSuccess?.Invoke(single.tags[0]);
        }
        else
        {
            Debug.LogError("Lobby'de relay session tag'i bulunamadı.");
            onFail?.Invoke();
        }
    }

    // ============================================================ Service lifecycle

    /// <summary>
    /// Servis hazır mı? Ana akışı 8sn'den fazla BLOKLAMAZ.
    /// Hazır değilse: onarma arka planda devam eder, ana akış hızlıca fallback yapar.
    /// </summary>
    private IEnumerator EnsureServiceReady(Action<bool> onResult)
    {
        // Cache'li URL varsa hızlı doğrulama (tek GET).
        if (serviceReady && !string.IsNullOrEmpty(lobbyUrl))
        {
            LobbyServiceResp check = null;
            yield return GetLobbyServiceNow(r => check = r);
            if (check != null && check.status == "READY")
            {
                onResult?.Invoke(true);
                yield break;
            }

            Debug.LogWarning("Cache'li Lobby Service URL geçersiz; arka planda onarılacak.");
            lobbyUrl = null;
            serviceReady = false;
            PlayerPrefs.DeleteKey(PrefKey);
        }

        StartCoroutine(RepairServiceInBackground());

        // Kısa şans: 8sn — belki başka instance deploy'u bitirmiş/az önce READY olmuş.
        int waited = 0;
        while (!serviceReady && waited < ServiceReadyBudgetSeconds)
        {
            yield return new WaitForSeconds(1f);
            waited++;
            LobbyServiceResp poll = null;
            yield return GetLobbyServiceNow(r => poll = r);
            if (poll != null && poll.status == "READY")
            {
                OnLobbyServiceResp(poll);
                break;
            }
        }

        if (!serviceReady)
        {
            Debug.LogWarning($"Lobby Service {ServiceReadyBudgetSeconds}s içinde hazır değil — fallback (Edgegap session id) kullanılacak; onarım arka planda sürüyor.");
            onResult?.Invoke(false);
            yield break;
        }

        onResult?.Invoke(true);
    }

    private bool backgroundRepairRunning;
    private float lastRepairAttemptTime;
    private int failedRepairCycles;
    private const int MaxFailedRepairCycles = 2;       // uzun back-off mesajı öncesi
    private const float RepairBackoffSeconds = 300f;   // 5 dk bekleme############

    private IEnumerator RepairServiceInBackground()
    {
        if (backgroundRepairRunning) yield break;
        if (failedRepairCycles >= MaxFailedRepairCycles && Time.realtimeSinceStartup - lastRepairAttemptTime < RepairBackoffSeconds)
        {
            yield break;   // Edgegap deploy sürekli Error → 5 dk back-off
        }
        backgroundRepairRunning = true;

        // Error/Missing servis → sil
        LobbyServiceResp existing = null;
        yield return GetLobbyServiceNow(r => existing = r);
        if (existing != null && (existing.status == "Error" || existing.status != "READY"))
        {
            Debug.LogWarning($"Lobby Service durum '{existing.status}' — arka planda silinecek.");
            yield return TerminateAndAwaitGone();
        }

        if (serviceReady) { backgroundRepairRunning = false; yield break; }

        yield return CreateLobbyService(ok => { });

        // Deploy kabul olana dek dene
        int deployAttempts = 0;
        bool deployed = false;
        while (!deployed && deployAttempts < 20)
        {
            deployAttempts++;
            yield return DeployLobbyService(ok => deployed = ok);
            if (deployed) break;
            yield return new WaitForSeconds(5f);
        }

        // READY'yi bekleyen poll.
        int waited = 0;
        while (!serviceReady && waited < DeployTimeoutSeconds)
        {
            yield return new WaitForSeconds(1f);
            waited++;
            yield return GetLobbyService();
        }

        backgroundRepairRunning = false;
        lastRepairAttemptTime = Time.realtimeSinceStartup;
        if (serviceReady)
        {
            failedRepairCycles = 0;
            Debug.Log("Lobby Service arka planda hazır — sonraki lobby işlemleri hızlı olacak.");
        }
        else
        {
            failedRepairCycles++;
            Debug.LogWarning($"Lobby Service onarımı başarısız ({failedRepairCycles}/{MaxFailedRepairCycles}) — Edgegap side deployment kontrolü gerekiyor.");
        }
    }

    [Serializable] private class LobbyServiceReq { public string name; }
    [Serializable] private class LobbyServiceResp { public string name; public string url; public string status; }

    private void OnLobbyServiceResp(LobbyServiceResp resp)
    {
        if (resp != null && !string.IsNullOrEmpty(resp.url) && resp.status == "READY")
        {
            lobbyUrl = resp.url;
            serviceReady = true;
            PlayerPrefs.SetString(PrefKey, lobbyUrl);
            PlayerPrefs.Save();
            Debug.Log("Lobby Service READY — URL kaydedildi: " + lobbyUrl);
        }
    }

    /// <summary>GET /v1/lobbies/{name} — yoksa (404) null döner.</summary>
    private IEnumerator GetLobbyServiceNow(Action<LobbyServiceResp> onResult)
    {
        lastPollStatus = null;
        using UnityWebRequest req = UnityWebRequest.Get($"https://api.edgegap.com/v1/lobbies/{lobbyServiceName}");
        req.SetRequestHeader("Authorization", $"token {EffectiveApiKey}");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            LobbyServiceResp resp = JsonUtility.FromJson<LobbyServiceResp>(req.downloadHandler.text);
            lastPollStatus = resp != null ? resp.status : "?";
            onResult?.Invoke(resp);
        }
        else
        {
            Debug.Log($"Lobby Service GET HTTP {req.responseCode} — {req.downloadHandler.text}");
            onResult?.Invoke(null);
        }
    }

    /// <summary>POST /v1/lobbies — servis tanımını oluşturur. 409/"unique" = zaten var, kabul edilir.</summary>
    private IEnumerator CreateLobbyService(Action<bool> onResult)
    {
        using UnityWebRequest req = new UnityWebRequest("https://api.edgegap.com/v1/lobbies", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(new LobbyServiceReq { name = lobbyServiceName })));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"token {EffectiveApiKey}");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Lobby Service oluşturma başarılı: {req.downloadHandler.text}");
            onResult?.Invoke(true);
        }
        else if (req.responseCode == 409 || (req.downloadHandler != null && req.downloadHandler.text.Contains("unique")))
        {
            Debug.Log("Lobby Service zaten mevcut (409/unique). Devam ediliyor.");
            onResult?.Invoke(true);
        }
        else
        {
            Debug.LogWarning($"Lobby Service oluşturma başarısız ({req.responseCode}): {req.error} | body: {req.downloadHandler.text}");
            onResult?.Invoke(false);
        }
    }

    /// <summary>POST /v1/lobbies:deploy — deploy isteği; "zaten deploy/removal sürüyor"場合は false döner.</summary>
    private IEnumerator DeployLobbyService(Action<bool> onResult)
    {
        using UnityWebRequest req = new UnityWebRequest("https://api.edgegap.com/v1/lobbies:deploy", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(new LobbyServiceReq { name = lobbyServiceName })));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"token {EffectiveApiKey}");

        yield return req.SendWebRequest();

        bool ok = false;
        if (req.result == UnityWebRequest.Result.Success)
        {
            LobbyServiceResp resp = JsonUtility.FromJson<LobbyServiceResp>(req.downloadHandler.text);
            Debug.Log($"Lobby Service deploy talebi gönderildi. body: {req.downloadHandler.text}");
            if (resp != null && !string.IsNullOrEmpty(resp.url) && resp.status == "READY")
            {
                lobbyUrl = resp.url;
                serviceReady = true;
                ok = true;
            }
            else
            {
                ok = true; // deploy talebi kabul edildi, READY sonrası poll edecek
            }
        }
        else
        {
            Debug.LogWarning($"Lobby Service deploy isteği başarısız ({req.responseCode}): {req.error} | body: {req.downloadHandler.text}");
            // "zaten deploy/removing sürüyor" → blocking değil, tekrar deneme akışına bırak
            ok = false;
        }

        onResult?.Invoke(ok);
    }

    /// <summary>
    /// Terminate ardından servisin tamamen silindiğini (GET 404 döndüğünü) bekler.
    /// Edgegap'in "already deploying/removing" 400'ünü önler.
    /// </summary>
    private IEnumerator TerminateAndAwaitGone()
    {
        yield return TerminateLobbyService();

        int waited = 0;
        bool gone = false;
        while (!gone && waited < 120)
        {
            yield return new WaitForSeconds(2f);
            waited += 2;

            LobbyServiceResp poll = null;
            yield return GetLobbyServiceNow(r => poll = r);

            if (poll == null)          // GET 404 → tamamen silindi
            {
                gone = true;
                Debug.Log($"Lobby Service '{lobbyServiceName}' tamamen silindi ({waited}s). Yeniden deploy edilecek.");
            }
            else
            {
                Debug.Log($"silme bekleniyor ({waited}s): {poll.status}");
            }
        }

        if (!gone)
            Debug.LogError($"Lobby Service 120s içinde silinemedi!");
    }

    /// <summary>POST /v1/lobbies:terminate — mevcut/error durumunda servisi siler.</summary>
    private IEnumerator TerminateLobbyService()
    {
        using UnityWebRequest req = new UnityWebRequest("https://api.edgegap.com/v1/lobbies:terminate", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
            JsonUtility.ToJson(new LobbyServiceReq { name = lobbyServiceName })));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"token {EffectiveApiKey}");

        yield return req.SendWebRequest();
        Debug.Log($"Lobby Service terminate: HTTP {req.responseCode}");
    }

    /// <summary>Poll: GET /v1/lobbies/{name} — deploy durumunu izler.</summary>
    private IEnumerator GetLobbyService()
    {
        LobbyServiceResp resp = null;
        yield return GetLobbyServiceNow(r => resp = r);

        if (resp != null)
        {
            Debug.Log($"Lobby Service poll: status={resp.status} url={resp.url}");
            if (!string.IsNullOrEmpty(resp.url) && resp.status == "READY")
            {
                lobbyUrl = resp.url;
                serviceReady = true;
            }
            lastPollStatus = resp.status;
        }
        else
        {
            // 404 vb.: kayıt yok — yeniden create+deploy tetikle
            lastPollStatus = "Error";
        }
    }
}
