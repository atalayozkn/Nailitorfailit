using System;
using System.Collections;
using System.Text;
using Edgegap;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

public class EdgegapRelayManager : MonoBehaviour
{
    public static EdgegapRelayManager Instance;

    [Header("Edgegap")]
    [SerializeField] private string relayProfileToken = "BURAYA_API_TOKEN_YAZ";
    [SerializeField] private string relayProfileSlug = "BURAYA_PROFIL_SLUG_YAZ";

    private const string ApiBase = "https://api.edgegap.com/v1/relays/sessions";

    private EdgegapKcpTransport edgegapTransport;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        edgegapTransport = (EdgegapKcpTransport)NetworkManager.singleton.transport;
    }

    public void CreateRelaySession(int maxPlayers, Action<string> onSuccess, Action onFail)
    {
        StartCoroutine(CreateSessionRoutine(maxPlayers, onSuccess, onFail));
    }

    public void JoinRelaySession(string sessionId, Action onSuccess, Action onFail)
    {
        StartCoroutine(JoinSessionRoutine(sessionId, onSuccess, onFail));
    }

    private IEnumerator CreateSessionRoutine(int maxPlayers, Action<string> onSuccess, Action onFail)
    {
        // Önce public IP'yi çek
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

        // Sadece host kullanıcısı ile başla — diğerleri join olduğunda eklenir
        string body = $"{{\"relay_profile_slug\": \"{relayProfileSlug}\", \"users\": [{{\"ip\": \"{publicIp}\"}}]}}";

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

            Debug.Log("Relay hazır, Session ID: " + readyData.session_id);
            onSuccess?.Invoke(readyData.session_id);
        }, onFail);
    }

    private IEnumerator JoinSessionRoutine(string sessionId, Action onSuccess, Action onFail)
    {
        // Önce public IP'yi çek
        string publicIp = null;
        using (UnityWebRequest ipReq = UnityWebRequest.Get("https://api.ipify.org"))
        {
            yield return ipReq.SendWebRequest();
            publicIp = ipReq.result == UnityWebRequest.Result.Success
                ? ipReq.downloadHandler.text.Trim()
                : "0.0.0.0";
        }

        // Doğru endpoint: sessions:authorize-user
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

        // Authorize sonrası session bilgilerini çek
        yield return WaitForRelayReady(sessionId, (readyData) =>
        {
            // Client için kendi index'ini bul
            int userIndex = readyData.session_users.Length - 1;

            edgegapTransport.relayAddress = readyData.relay.ip;
            edgegapTransport.relayGameServerPort = readyData.relay.ports.server.port;
            edgegapTransport.relayGameClientPort = readyData.relay.ports.client.port;
            edgegapTransport.sessionId = readyData.authorization_token;
            edgegapTransport.userId = readyData.session_users[userIndex].authorization_token;
            edgegapTransport.relayGUI = false;

            Debug.Log("Relay'e katılındı: " + sessionId);
            onSuccess?.Invoke();
        }, onFail);
    }

    private IEnumerator WaitForRelayReady(string sessionId, Action<EdgegapSessionResponse> onReady, Action onFail)
    {
        string url = $"{ApiBase}/{sessionId}";
        int maxAttempts = 20;

        for (int i = 0; i < maxAttempts; i++)
        {
            yield return new WaitForSeconds(1f);

            using UnityWebRequest req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"token {relayProfileToken}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Relay durum kontrolü başarısız ({i + 1}/{maxAttempts}): " + req.error);
                continue;
            }

            EdgegapSessionResponse data = JsonUtility.FromJson<EdgegapSessionResponse>(req.downloadHandler.text);

            Debug.Log($"Relay durumu ({i + 1}/{maxAttempts}): ready={data.ready}");

            if (data.ready)
            {
                onReady?.Invoke(data);
                yield break;
            }
        }

        Debug.LogError("Relay timeout — 20 saniyede hazır olmadı");
        onFail?.Invoke();
    }
}

[Serializable]
public class EdgegapSessionResponse
{
    public string session_id;
    public uint authorization_token;
    public bool ready;
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