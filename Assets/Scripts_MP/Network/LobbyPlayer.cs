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

    PlayerListEntryUI uiRow;

    public override void OnStartClient()
    {

        StartCoroutine(AddRowWhenReady());

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
    public void CmdSetColor(int index)
    {
        colorIndex = ClampColor(index);
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
