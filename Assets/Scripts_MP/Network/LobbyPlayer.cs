
using Mirror;

public class LobbyPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName;

    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady;

    [SyncVar(hook = nameof(OnHostChanged))]
    public bool isHost;

    PlayerListEntryUI uiRow;

    public override void OnStartClient()
    {
        if (LobbyUIManager.Instance != null)
            uiRow = LobbyUIManager.Instance.AddPlayerRow(this);

        if (isOwned && GameManager.Instance != null)
            GameManager.Instance.ShowLobbyMenu();
    }

    public override void OnStopClient()
    {
        if (uiRow != null && LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.RemovePlayerRow(uiRow);
    }

    void OnNameChanged(string oldVal, string newVal) => uiRow?.SetName(newVal);
    void OnReadyChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(isHost, newVal);
    void OnHostChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(newVal, isReady);

    [Command]
    public void CmdSetReady(bool ready)
    {
        if (isHost) return;
        isReady = ready;
    }
}
