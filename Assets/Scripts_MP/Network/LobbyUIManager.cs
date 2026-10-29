using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    [SerializeField] Transform playerListContent;
    [SerializeField] PlayerListEntryUI playerEntryPrefab;

    [SerializeField] GameObject startGameButton;
    [SerializeField] GameObject readyButton;
    [SerializeField] Button startGameButtonComponent;
    [SerializeField] Button readyButtonComponent;
    [SerializeField] Button leaveLobbyButton;

    [Tooltip("Optional: 'Invite Steam friends' button. Only shown when a Steam lobby is active.")]
    [SerializeField] Button inviteFriendsButton;

    [Tooltip("Optional: 'n/4 Ready' text. Player_Count can be dragged here.")]
    [SerializeField] TMP_Text statusText;

    void Awake() => Instance = this;

    void OnEnable()
    {
        if (leaveLobbyButton != null) leaveLobbyButton.interactable = true;
    }

    void Update()
    {
        LobbyNetworkManager nm = NetworkManager.singleton as LobbyNetworkManager;
        if (nm == null) return;

        if (statusText != null) statusText.text = nm.ReadySummary();

        if (!NetworkServer.active || startGameButtonComponent == null) return;

        bool canStart = nm.AllClientsReady() && !nm.IsGameStarted;

        if (startGameButtonComponent.interactable != canStart)
            startGameButtonComponent.interactable = canStart;
    }

    void Start()
    {
        BindLobbyButtons();

        if (inviteFriendsButton != null)
        {

            bool steamAvailable = SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.SteamOnline;
            inviteFriendsButton.gameObject.SetActive(steamAvailable);
            inviteFriendsButton.onClick.AddListener(OnInviteFriends);
        }
    }

    private void BindLobbyButtons()
    {
        int bound = 0;

        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            string label = button.name.ToLowerInvariant();

            if (button.onClick.GetPersistentEventCount() > 0) continue;

            string pressedName = button.name;

            if (label.Contains("leave"))
            {
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[UI] press '{pressedName}' -> leave");
                    OnLeaveLobby();
                });
                bound++;
            }
            else if (label.Contains("start"))
            {
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[UI] press '{pressedName}' -> start");
                    OnStartGame();
                });
                bound++;
            }
            else if (label.Contains("ready"))
            {
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[UI] press '{pressedName}' -> ready");
                    ToggleLocalReady();
                });
                bound++;
            }
        }

        Debug.Log($"[UI] LobbyUIManager bound {bound} lobby buttons (leave/start/ready).");
    }

    void OnInviteFriends()
    {
        Debug.Log("[UI] Invite Steam friends pressed.");
        SteamLobbyManager.Instance?.OpenInviteOverlay();
    }

    public PlayerListEntryUI AddPlayerRow(LobbyPlayer player)
    {
        PlayerListEntryUI row = Instantiate(playerEntryPrefab, playerListContent);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        if (rowRect != null && playerListContent is RectTransform contentRect)
        {
            float targetWidth = contentRect.rect.width;
            if (targetWidth > 1f)
                rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        }

        string displayName = string.IsNullOrEmpty(player.playerName) ? "Player" : player.playerName;
        row.SetName(displayName);
        row.UpdateColor(player.isHost, player.isReady);

        Debug.Log($"[UI] Satir eklendi: name='{displayName}' host={player.isHost} owned={player.isOwned}");

        if (player.isOwned)
        {
            startGameButton.SetActive(player.isHost);
            readyButton.SetActive(!player.isHost);
        }

        return row;
    }

    public void RemovePlayerRow(PlayerListEntryUI row)
    {
        if (row != null) Destroy(row.gameObject);
    }

    void OnStartGame()
    {
        if (startGameButtonComponent != null) startGameButtonComponent.interactable = false;
        ((LobbyNetworkManager)NetworkManager.singleton).StartGame();
    }

    private void ToggleLocalReady()
    {
        LobbyPlayer local = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>()
            : null;

        if (local == null)
        {
            Debug.LogWarning("[UI] ready ignored: NetworkClient.localPlayer has no LobbyPlayer "
                             + $"(localPlayer={(NetworkClient.localPlayer != null ? NetworkClient.localPlayer.name : "null")})");
            return;
        }

        if (local.isHost)
        {
            Debug.Log("[UI] ready ignored: this player is the host.");
            return;
        }

        bool next = !local.isReady;
        local.CmdSetReady(next);
        Debug.Log($"[UI] ready -> {next} (request sent to the host)");
    }

    void OnLeaveLobby()
    {
        MpTeardownLog.Log("LobbyUIManager.OnLeaveLobby");

        if (MpMenuUI.Instance != null)
        {
            MpMenuUI.Instance.OnLeavePressed();
            return;
        }

        if (leaveLobbyButton != null) leaveLobbyButton.interactable = false;

        var lobbyNm = NetworkManager.singleton as LobbyNetworkManager;

        bool wasHost = NetworkServer.active && NetworkClient.isConnected;

        SteamLobbyManager.Instance?.LeaveLobby();

        if (wasHost)
        {
            Debug.Log("[LOBBY] Host leaving: closing the host.");
            NetworkManager.singleton.StopHost();
        }
        else
        {
            Debug.Log("[LOBBY] Client leaving: closing connection.");
            NetworkManager.singleton.StopClient();
        }

        ResetLobbyUi();

        if (lobbyNm != null) lobbyNm.ResetLobbyState();

        Debug.Log("[LOBBY] Leave pressed: lobby UI closed.");
    }

    private void ResetLobbyUi()
    {
        MpInPlaceToggle inPlace = FindAnyObjectByType<MpInPlaceToggle>(FindObjectsInactive.Include);
        if (inPlace != null)
        {
            inPlace.Close();
            return;
        }

        if (GameManager.Instance != null) GameManager.Instance.ShowMultiplayerMenu();

        if (leaveLobbyButton != null) leaveLobbyButton.interactable = true;
    }
}
