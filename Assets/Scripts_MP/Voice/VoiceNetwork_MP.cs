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
        public string speaker; // filled by the server every few seconds so clients can label the speaker
        public bool raw;       // 16 kHz 16-bit mono PCM from the Unity microphone (no Steam): local tests only
    }

    private const float PacketInterval = 0.02f;
    private const int MaxPacketBytes = 8192;

    // Voice is sent unreliable (a late voice frame is worthless and must not stall the ones behind it).
    // Bigger frames go reliable because they would not fit an unreliable datagram.
    private const int MaxUnreliableBytes = 1000;

    private readonly System.Collections.Generic.Dictionary<int, float> nameSentAt =
        new System.Collections.Generic.Dictionary<int, float>();

    // Push-to-talk only: hold V to talk. (There is no open-mic mode and no mute key for now.)
    [Header("Input")]
    [SerializeField] private Key pushToTalkKey = Key.V;

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
    private bool useLocalMicFallback;

    public bool IsTransmitting { get; private set; }
    // Kept for the UI/diagnostics that still read it: with push-to-talk only, the mic is never "muted".
    public Key PushToTalkKey => pushToTalkKey;
    public bool Muted => false;
    public bool SteamAvailable => SteamManager.Initialized;
    public bool MicAvailable => SteamManager.Initialized || LocalMicAvailable();

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

        ReportMicState();

        // No Steam, or Steam gave no voice data while the key was held (Steam's own mic setup is missing / muted /
        // not supported here): talk through the Unity microphone instead, raw PCM that every client can play.
        if (!SteamManager.Initialized || useLocalMicFallback)
        {
            StopRecording();
            UpdateLocalMic(WantsToTalk());
            return;
        }

        bool wantsToTalk = WantsToTalk();

        IsTransmitting = wantsToTalk;
        UpdateDiagnostics(wantsToTalk);

        if (!wantsToTalk)
        {
            // Releasing V stops the capture, but Steam still holds the last fraction of a second of speech.
            // Send it (it used to be thrown away, which clipped the end of every sentence), then discard silence.
            if (recording)
            {
                StopRecording();
                tailUntil = Time.unscaledTime + TailSeconds;
            }

            if (Time.unscaledTime < tailUntil) FlushVoice();
            else DrainPendingVoice();
            return;
        }

        StartRecording();

        if (Time.unscaledTime < nextReadAt) return;
        nextReadAt = Time.unscaledTime + PacketInterval;

        FlushVoice();
    }

    // Everyone sees our mic as an icon. Muted and "no Steam voice" both read as muted. Sent when it changes and once
    // for every new player object (the lobby player is replaced by the game player when the match starts).
    private uint reportedFor;
    private bool reportedMuted;

    private void ReportMicState()
    {
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local == null) return;

        bool effectiveMuted = !MicAvailable; // neither Steam voice nor a microphone = shown as a crossed-out mic
        if (reportedFor == local.netId && reportedMuted == effectiveMuted) return;

        reportedFor = local.netId;
        reportedMuted = effectiveMuted;

        if (local.TryGetComponent(out LobbyPlayer lobbyPlayer))
        {
            lobbyPlayer.CmdSetMicMuted(effectiveMuted);
            return;
        }

        PlayerScripts.PlayerNetwork_MP player = local.GetComponentInChildren<PlayerScripts.PlayerNetwork_MP>(true);
        if (player != null) player.CmdSetMicMuted(effectiveMuted);
    }

    // ---- no Steam (local KCP tests): talk through the Unity microphone, raw PCM instead of Steam's codec ----
    private const int LocalRate = 16000;
    private const int LocalPacketBytes = 640; // 20 ms
    private AudioClip localClip;
    private string localDevice;
    private int localReadPos;
    private readonly System.Collections.Generic.List<byte> localPending = new System.Collections.Generic.List<byte>();
    private bool localNoMicWarned;

    private static bool LocalMicAvailable()
    {
        if (Application.isBatchMode) return false;

        try { return Microphone.devices.Length > 0; }
        catch (System.Exception) { return false; }
    }

    private void UpdateLocalMic(bool talking)
    {
        IsTransmitting = talking && localClip != null;

        if (!talking)
        {
            StopLocalMic();
            return;
        }

        if (localClip == null)
        {
            if (!LocalMicAvailable())
            {
                if (!localNoMicWarned)
                {
                    localNoMicWarned = true;
                    MpNotice_MP.Warning("Voice: Steam is not running and no microphone was found - voice is off.");
                }
                return;
            }

            localDevice = Microphone.devices[0];
            localClip = Microphone.Start(localDevice, true, 1, LocalRate);
            localReadPos = 0;
            localPending.Clear();
            IsTransmitting = true;
            InteractionDebug.Log($"[VOICE] local microphone '{localDevice}' started (no Steam).");
            return;
        }

        int position = Microphone.GetPosition(localDevice);
        if (position < 0) return;

        int length = localClip.samples;
        int available = (position - localReadPos + length) % length;
        if (available == 0) return;

        var samples = new float[available];
        // GetData wraps around the clip end for us.
        localClip.GetData(samples, localReadPos);
        localReadPos = position;
        lastDataAt = Time.unscaledTime;

        for (int i = 0; i < available; i++)
        {
            short value = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
            localPending.Add((byte)(value & 0xFF));
            localPending.Add((byte)((value >> 8) & 0xFF));
        }

        while (localPending.Count >= LocalPacketBytes)
        {
            var payload = localPending.GetRange(0, LocalPacketBytes).ToArray();
            localPending.RemoveRange(0, LocalPacketBytes);
            NetworkClient.Send(new VoicePacket { data = payload, raw = true }, Channels.Unreliable);
            sentPackets++;
            sentBytes += payload.Length;
        }
    }

    private void StopLocalMic()
    {
        if (localClip == null) return;

        Microphone.End(localDevice);
        Destroy(localClip);
        localClip = null;
        localPending.Clear();
    }

    private const float TailSeconds = 0.4f;
    private float tailUntil;

    private bool WantsToTalk()
    {
        Keyboard keyboard = Keyboard.current;

        if (ChatUI.IsAnyOpen) return false; // typing "v" in chat must not open the mic
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

    // ---- diagnostics: a 5 s summary in Player.log + an on-screen hint when the mic gives nothing ----
    private int sentPackets, sentBytes, recvPackets;
    private EVoiceResult lastResult = EVoiceResult.k_EVoiceResultOK;
    private float talkingSince = -1f, lastDataAt = -1f, nextStatsAt, nextMicWarningAt;

    public static void CountReceived() { if (Instance != null) Instance.recvPackets++; }

    private void UpdateDiagnostics(bool talking)
    {
        float now = Time.unscaledTime;

        if (talking)
        {
            if (talkingSince < 0f) talkingSince = now;
            bool silent = lastDataAt < talkingSince || now - lastDataAt > 2f;
            if (silent && now - talkingSince > 1.5f && !useLocalMicFallback && LocalMicAvailable())
            {
                useLocalMicFallback = true;
                talkingSince = -1f;
                Debug.LogWarning($"[VOICE] Steam voice gave no data ({lastResult}); switching to the Unity microphone for this session.");
                MpNotice_MP.Info("Voice: Steam gave no microphone input - using the system microphone.");
                return;
            }

            if (silent && now - talkingSince > 2f && now >= nextMicWarningAt)
            {
                nextMicWarningAt = now + 15f;
                MpNotice_MP.Warning("Voice: no microphone input reaches Steam (" + lastResult + "). " +
                                    "Check Steam > Settings > Voice (input device) and that the mic is not muted.");
            }
        }
        else talkingSince = -1f;

        if (now < nextStatsAt) return;
        nextStatsAt = now + 5f;
        if (sentPackets == 0 && recvPackets == 0 && !talking) return;

        Debug.Log($"[VOICE] stats 5s: talking={talking} recording={recording} lastResult={lastResult} " +
                  $"sent={sentPackets} pkts/{sentBytes} B received={recvPackets} pkts " +
                  $"server={NetworkServer.active} speakers={FindObjectsByType<VoiceSpeaker_MP>(FindObjectsSortMode.None).Length}");
        sentPackets = sentBytes = recvPackets = 0;
    }

    private void FlushVoice()
    {
        for (int guard = 0; guard < 8; guard++)
        {
            lastResult = SteamUser.GetAvailableVoice(out uint available);
            if (lastResult != EVoiceResult.k_EVoiceResultOK) return;
            if (available == 0) return;
            if (available > readBuffer.Length) available = (uint)readBuffer.Length;

            EVoiceResult result = SteamUser.GetVoice(true, readBuffer, available, out uint written);
            lastResult = result;
            if (result == EVoiceResult.k_EVoiceResultOK && written > 0) lastDataAt = Time.unscaledTime;

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

        NetworkClient.Send(new VoicePacket { data = payload }, ChannelFor(payload.Length));
        sentPackets++;
        sentBytes += payload.Length;

        if (logVoice)
            InteractionDebug.Log($"[VOICE] sent {length} byte(s).");
    }

    private void OnServerVoice(NetworkConnectionToClient sender, VoicePacket message)
    {
        if (sender == null || sender.identity == null) return;
        if (message.data == null || message.data.Length == 0 || message.data.Length > MaxPacketBytes) return;

        string speaker = null;
        if (!nameSentAt.TryGetValue(sender.connectionId, out float last) || Time.unscaledTime - last > 2f)
        {
            nameSentAt[sender.connectionId] = Time.unscaledTime;
            speaker = sender.authenticationData as string;
        }

        var relay = new VoicePacket { netId = sender.identity.netId, data = message.data, speaker = speaker, raw = message.raw };
        int channel = ChannelFor(message.data.Length);

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn == sender || !conn.isReady) continue;

            conn.Send(relay, channel);
        }
    }

    private void OnClientVoice(VoicePacket message)
    {
        if (message.data == null || message.data.Length == 0) return;

        if (!NetworkClient.spawned.TryGetValue(message.netId, out NetworkIdentity speaker) || speaker == null) return;

        VoiceSpeaker_MP playback = speaker.GetComponent<VoiceSpeaker_MP>();

        if (playback == null) playback = speaker.gameObject.AddComponent<VoiceSpeaker_MP>();

        if (!string.IsNullOrEmpty(message.speaker)) playback.SetSpeakerName(message.speaker);
        CountReceived();
        playback.Push(message.data, message.raw);
    }

    private static int ChannelFor(int length) => length <= MaxUnreliableBytes ? Channels.Unreliable : Channels.Reliable;
}
