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

        RefreshInviteButton();

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
            inviteFriendsButton.onClick.RemoveListener(OnInviteFriends);
            inviteFriendsButton.onClick.AddListener(OnInviteFriends);
        }
    }

    // The Invite button follows the session instead of being decided once at Start: it is there for whoever hosts the
    // lobby (Steam or local), hidden for plain clients.
    private void RefreshInviteButton()
    {
        if (inviteFriendsButton == null) return;

        bool show = NetworkServer.active;
        if (inviteFriendsButton.gameObject.activeSelf != show) inviteFriendsButton.gameObject.SetActive(show);
        if (show && !inviteFriendsButton.interactable) inviteFriendsButton.interactable = true;
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

    // Steam lobby + working overlay: the friends/invite dialog. Anything else (local test session, overlay switched
    // off, game not started through Steam): copy what a friend needs to join, so the button always does something.
    void OnInviteFriends()
    {
        Debug.Log("[UI] Invite pressed.");

        SteamLobbyManager steam = SteamLobbyManager.Instance;
        bool steamLobby = steam != null && steam.SteamOnline && !string.IsNullOrEmpty(steam.CurrentCode);

        if (steamLobby && steam.TryOpenInviteOverlay()) return;

        string password = (NetworkManager.singleton as LobbyNetworkManager)?.LobbyPassword;
        string passwordPart = string.IsNullOrEmpty(password) ? string.Empty : $" / password {password}";
        string joinInfo = (steamLobby ? steam.CurrentCode : LocalAddress()) + passwordPart;
        GUIUtility.systemCopyBuffer = joinInfo;

        MpNotice_MP.Info(steamLobby
            ? $"Steam overlay unavailable - lobby {joinInfo} copied. Send it to your friend."
            : $"Invite copied: {joinInfo}  (your friend joins with this address)");
    }

    private static string LocalAddress()
    {
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                foreach (var address in nic.GetIPProperties().UnicastAddresses)
                    if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return address.Address.ToString();
            }
        }
        catch (System.Exception) { }

        return "127.0.0.1";
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

        // Only the host gets the kick icon, and never on its own row.
        bool canKick = NetworkServer.active && !player.isOwned;
        row.SetKickable(canKick, canKick ? (System.Action)(() => KickPlayer(player)) : null);

        Debug.Log($"[UI] Satir eklendi: name='{displayName}' host={player.isHost} owned={player.isOwned}");

        if (player.isOwned)
        {
            startGameButton.SetActive(player.isHost);
            readyButton.SetActive(!player.isHost);
        }

        return row;
    }

    private void KickPlayer(LobbyPlayer target)
    {
        LobbyPlayer host = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<LobbyPlayer>()
            : null;

        if (host == null || target == null)
        {
            Debug.LogWarning("[UI] kick ignored: host LobbyPlayer not found.");
            return;
        }

        Debug.Log($"[UI] Kick pressed for '{(string.IsNullOrEmpty(target.playerName) ? "Player" : target.playerName)}' "
                  + $"(netId={target.netId})");
        host.CmdKick(target.netId);
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
