using Steamworks;
using System.Collections;
using Mirror;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName;

    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady;

    [SyncVar(hook = nameof(OnHostChanged))]
    public bool isHost;

    [SyncVar(hook = nameof(OnColorChanged))]
    public int colorIndex;

    [SyncVar(hook = nameof(OnSteamIdChanged))]
    public ulong steamId;

    // True while the player's microphone is muted or unavailable (shown as an icon next to the name).
    [SyncVar(hook = nameof(OnMicChanged))]
    public bool micMuted;

    PlayerListEntryUI uiRow;

    public override void OnStartServer()
    {
        base.OnStartServer();

        LobbyNetworkManager manager = (LobbyNetworkManager)NetworkManager.singleton;
        int remembered = LobbyNetworkManager.RememberedColor(connectionToClient);

        // Coming back from a match: keep the colour the player had, unless somebody else took it meanwhile.
        bool takenByOther = false;
        foreach (LobbyPlayer other in FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
            if (other != null && other != this && ClampColor(other.colorIndex) == remembered) takenByOther = true;

        colorIndex = remembered >= 0 && !takenByOther ? ClampColor(remembered) : manager.NextFreeColorIndex(this);
        LobbyNetworkManager.RememberColor(connectionToClient, colorIndex);
    }

    void OnSteamIdChanged(ulong oldVal, ulong newVal) => ApplyRowData();

    void OnMicChanged(bool oldVal, bool newVal) => uiRow?.SetMic(newVal, IsTalking());

    // Local player: our own push-to-talk. Others: their voice packets are arriving.
    private bool IsTalking()
    {
        if (micMuted) return false;

        if (isOwned) return VoiceNetwork_MP.Instance != null && VoiceNetwork_MP.Instance.IsTransmitting;

        VoiceSpeaker_MP speaker = GetComponent<VoiceSpeaker_MP>();
        return speaker != null && speaker.IsSpeaking;
    }

    void Update()
    {
        if (uiRow != null) uiRow.SetMic(micMuted, IsTalking());
    }

    [Command]
    public void CmdSetMicMuted(bool muted)
    {
        micMuted = muted;
    }

    public override void OnStartClient()
    {

        StartCoroutine(AddRowWhenReady());

        if (isOwned && SteamManager.Initialized) CmdSetSteamId(SteamUser.GetSteamID().m_SteamID);

        if (isOwned && GameManager.Instance != null)
            GameManager.Instance.ShowLobbyMenu();
    }

    private IEnumerator AddRowWhenReady()
    {
        float waited = 0f;
        while (LobbyUIManager.Instance == null && waited < 10f)
        {
            yield return new WaitForSeconds(0.1f);
            waited += 0.1f;
        }

        if (LobbyUIManager.Instance == null)
        {
            Debug.LogWarning("[Lobby] LobbyUIManager bulunamadi; oyuncu satiri eklenemedi.");
            yield break;
        }

        uiRow = LobbyUIManager.Instance.AddPlayerRow(this);

        ApplyRowData();

        StartCoroutine(RefreshRowBriefly());
    }

    private void ApplyRowData()
    {
        if (uiRow == null) return;

        string displayName = string.IsNullOrEmpty(playerName) ? "Player" : playerName;

        uiRow.SetName(displayName);
        uiRow.SetPlayerColor(MpPlayerColor_MP.ColorOf(colorIndex));
        uiRow.SetSteamId(steamId);
        uiRow.SetMic(micMuted, IsTalking());
        uiRow.UpdateColor(isHost, isReady);
    }

    private IEnumerator RefreshRowBriefly()
    {
        float waited = 0f;
        while (waited < 2f)
        {
            yield return new WaitForSeconds(0.25f);
            waited += 0.25f;
            ApplyRowData();
        }
    }

    public override void OnStopClient()
    {
        if (uiRow != null && LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.RemovePlayerRow(uiRow);
    }

    void OnNameChanged(string oldVal, string newVal)
    {
        if (uiRow != null) uiRow.SetName(string.IsNullOrEmpty(newVal) ? "Player" : newVal);
    }

    void OnReadyChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(isHost, newVal);
    void OnHostChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(newVal, isReady);

    void OnColorChanged(int oldVal, int newVal)
    {
        if (uiRow == null) return;

        uiRow.SetPlayerColor(MpPlayerColor_MP.ColorOf(newVal));
        uiRow.UpdateColor(isHost, isReady);
    }

    public static int ClampColor(int index) => MpPlayerColor_MP.Clamp(index);

    [Command]
    public void CmdSetSteamId(ulong id)
    {
        steamId = id;
    }

    [Command]
    public void CmdSetColor(int index)
    {
        // Two players never share a colour: a pick that someone else already wears is ignored (the panel greys
        // those out, this covers two clicks racing each other).
        int wanted = ClampColor(index);
        foreach (LobbyPlayer other in FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
        {
            if (other != null && other != this && ClampColor(other.colorIndex) == wanted)
            {
                InteractionDebug.Log($"[LOBBY] colour {wanted} refused for '{playerName}': '{other.playerName}' has it.");
                return;
            }
        }

        colorIndex = wanted;
        LobbyNetworkManager.RememberColor(connectionToClient, colorIndex);
        InteractionDebug.Log($"[LOBBY] colour for '{playerName}' -> {colorIndex}");
    }

    [Server]
    public void ServerForceNameSync()
    {
        string real = playerName;
        playerName = real + "\u200b";
        playerName = real;
    }

    [Command]
    public void CmdSetReady(bool ready)
    {
        if (isHost)
        {
            Debug.Log("[LOBBY] CmdSetReady ignored: this connection is the host.");
            return;
        }

        isReady = ready;
        Debug.Log($"[LOBBY] ready applied: player='{playerName}' ready={ready}");
    }
}
