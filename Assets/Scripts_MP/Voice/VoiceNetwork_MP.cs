using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;

public class VoiceNetwork_MP : MonoBehaviour
{
    public static VoiceNetwork_MP Instance { get; private set; }

    public struct VoicePacket : NetworkMessage
    {
        public uint netId;
        public byte[] data;
    }

    private const float PacketInterval = 0.02f;
    private const int MaxPacketBytes = 8192;

    [Header("Input")]
    [SerializeField] private bool pushToTalk = true;
    [SerializeField] private Key pushToTalkKey = Key.V;
    [SerializeField] private Key muteKey = Key.M;

    [Header("Debug")]
    [SerializeField] private bool logVoice = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<VoiceNetwork_MP>(FindObjectsInactive.Include) != null) return;

        new GameObject("VoiceNetwork_MP").AddComponent<VoiceNetwork_MP>();
    }

    private byte[] readBuffer = new byte[8192];
    private float nextReadAt;
    private bool handlersRegistered;
    private bool recording;

    public bool IsTransmitting { get; private set; }
    public bool Muted { get; private set; }
    public bool SteamAvailable => SteamManager.Initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            StopRecording();
            Instance = null;
        }
    }

    public static void Register()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[VOICE] VoiceNetwork_MP is not in the scene - voice chat is disabled. " +
                             "Add the component to the LobbyNetworkManager GameObject.");
            return;
        }

        Instance.RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        if (NetworkServer.active)
        {
            NetworkServer.UnregisterHandler<VoicePacket>();
            NetworkServer.RegisterHandler<VoicePacket>(OnServerVoice, true);
        }

        if (NetworkClient.active)
        {
            NetworkClient.UnregisterHandler<VoicePacket>();
            NetworkClient.RegisterHandler<VoicePacket>(OnClientVoice, true);
        }

        handlersRegistered = NetworkServer.active || NetworkClient.active;

        Debug.Log($"[VOICE] handlers registered. server={NetworkServer.active} client={NetworkClient.active} " +
                  $"steam={SteamManager.Initialized}");
    }

    private void Update()
    {
        if (!handlersRegistered || !NetworkClient.active) return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard[muteKey].wasPressedThisFrame)
        {
            Muted = !Muted;
            InteractionDebug.Log($"[VOICE] microphone {(Muted ? "muted" : "open")}.");
        }

        if (!SteamManager.Initialized)
        {
            StopRecording();
            IsTransmitting = false;
            return;
        }

        bool wantsToTalk = !Muted && (!pushToTalk || WantsToTalk());

        IsTransmitting = wantsToTalk;

        if (!wantsToTalk)
        {
            StopRecording();
            DrainPendingVoice();
            return;
        }

        StartRecording();

        if (Time.unscaledTime < nextReadAt) return;
        nextReadAt = Time.unscaledTime + PacketInterval;

        FlushVoice();
    }

    private bool WantsToTalk()
    {
        Keyboard keyboard = Keyboard.current;

        return keyboard != null && keyboard[pushToTalkKey].isPressed;
    }

    private void StartRecording()
    {
        if (recording) return;

        SteamUser.StartVoiceRecording();
        recording = true;
    }

    private void StopRecording()
    {
        if (!recording) return;

        SteamUser.StopVoiceRecording();
        recording = false;
    }

    private void FlushVoice()
    {
        for (int guard = 0; guard < 8; guard++)
        {
            if (SteamUser.GetAvailableVoice(out uint available) != EVoiceResult.k_EVoiceResultOK) return;
            if (available == 0) return;
            if (available > readBuffer.Length) available = (uint)readBuffer.Length;

            EVoiceResult result = SteamUser.GetVoice(true, readBuffer, available, out uint written);

            if (result != EVoiceResult.k_EVoiceResultOK || written == 0) return;
            if (written > MaxPacketBytes) continue;

            Send(written);
        }
    }

    private void DrainPendingVoice()
    {
        if (SteamUser.GetAvailableVoice(out uint available) != EVoiceResult.k_EVoiceResultOK) return;
        if (available == 0) return;
        if (available > readBuffer.Length) available = (uint)readBuffer.Length;

        SteamUser.GetVoice(true, readBuffer, available, out _);
    }

    private void Send(uint length)
    {
        var payload = new byte[length];
        System.Array.Copy(readBuffer, payload, (int)length);

        NetworkClient.Send(new VoicePacket { data = payload }, Channels.Reliable);

        if (logVoice)
            InteractionDebug.Log($"[VOICE] sent {length} byte(s).");
    }

    private void OnServerVoice(NetworkConnectionToClient sender, VoicePacket message)
    {
        if (sender == null || sender.identity == null) return;
        if (message.data == null || message.data.Length == 0 || message.data.Length > MaxPacketBytes) return;

        var relay = new VoicePacket { netId = sender.identity.netId, data = message.data };

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn == sender || !conn.isReady) continue;

            conn.Send(relay, Channels.Reliable);
        }
    }

    private void OnClientVoice(VoicePacket message)
    {
        if (message.data == null || message.data.Length == 0) return;

        if (!NetworkClient.spawned.TryGetValue(message.netId, out NetworkIdentity speaker) || speaker == null) return;

        VoiceSpeaker_MP playback = speaker.GetComponent<VoiceSpeaker_MP>();

        if (playback == null) playback = speaker.gameObject.AddComponent<VoiceSpeaker_MP>();

        playback.Push(message.data);
    }
}
