using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public class ChatNetwork : MonoBehaviour
{
    public static ChatNetwork Instance { get; private set; }

    [Header("Limits")]
    [SerializeField] private int maxHistory = 100;
    [SerializeField] private int maxMessageLength = 200;
    [SerializeField] private float minSecondsBetweenMessages = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool logMessages = true;

    public static event Action<string, string> OnMessage;

    private readonly List<ChatEntry> history = new List<ChatEntry>();

    private float lastSendTime = -999f;
    private bool handlersRegistered;

    public IReadOnlyList<ChatEntry> History => history;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Register()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CHAT] ChatNetwork is not in the scene - chat will not work. " +
                             "Add the component to the LobbyNetworkManager GameObject.");
            return;
        }

        Instance.RegisterHandlers();
    }

    void RegisterHandlers()
    {
        if (NetworkServer.active)
        {
            NetworkServer.UnregisterHandler<ChatMessage>();
            NetworkServer.RegisterHandler<ChatMessage>(OnServerMessage, true);
        }

        if (NetworkClient.active)
        {
            NetworkClient.UnregisterHandler<ChatMessage>();
            NetworkClient.RegisterHandler<ChatMessage>(OnClientMessage, true);
        }

        handlersRegistered = NetworkServer.active || NetworkClient.active;

        if (logMessages)
            Debug.Log($"[CHAT] Handlers registered. server={NetworkServer.active} client={NetworkClient.active}");
    }

    public bool Send(string text)
    {
        if (!handlersRegistered || !NetworkClient.active) return false;
        if (string.IsNullOrWhiteSpace(text)) return false;

        if (Time.unscaledTime - lastSendTime < minSecondsBetweenMessages) return false;
        lastSendTime = Time.unscaledTime;

        string trimmed = text.Trim();
        if (trimmed.Length > maxMessageLength)
            trimmed = trimmed.Substring(0, maxMessageLength);

        NetworkClient.Send(new ChatMessage { text = trimmed });
        return true;
    }

    void OnServerMessage(NetworkConnectionToClient conn, ChatMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.text)) return;

        string sender = null;

        if (conn != null)
        {
            sender = conn.authenticationData as string;

            if (string.IsNullOrWhiteSpace(sender) && conn.identity != null)
            {
                LobbyPlayer lobbyPlayer = conn.identity.GetComponent<LobbyPlayer>();
                if (lobbyPlayer != null) sender = lobbyPlayer.playerName;
            }

            if (string.IsNullOrWhiteSpace(sender))
                sender = $"Player {conn.connectionId}";
        }

        string text = msg.text.Trim();
        if (text.Length > maxMessageLength)
            text = text.Substring(0, maxMessageLength);

        NetworkServer.SendToAll(new ChatMessage { sender = sender, text = text });
    }

    void OnClientMessage(ChatMessage msg) => AddToHistory(msg.sender, msg.text);

    void AddToHistory(string sender, string text)
    {
        if (string.IsNullOrWhiteSpace(sender)) sender = "Player";

        history.Add(new ChatEntry { sender = sender, text = text });

        while (history.Count > maxHistory)
            history.RemoveAt(0);

        if (logMessages) Debug.Log($"[CHAT] {sender}: {text}");

        OnMessage?.Invoke(sender, text);
    }

    public static void ClearHistory()
    {
        if (Instance != null) Instance.history.Clear();
    }
}
