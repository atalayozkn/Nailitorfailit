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
        ShowVoiceHint();
    }

    // "Press V to open Voice Chat": shown for a few seconds every time the lobby opens, then fades out.
    const float VoiceHintSeconds = 10f;
    const float VoiceHintFade = 1f;
    TMP_Text voiceHint;
    float voiceHintUntil;

    void ShowVoiceHint()
    {
        if (voiceHint == null)
        {
            var go = new GameObject("VoiceHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 44f);
            rect.anchoredPosition = new Vector2(448f, 8f);

            voiceHint = go.GetComponent<TextMeshProUGUI>();
            if (statusText != null) voiceHint.font = statusText.font;
            voiceHint.fontSize = 30f;
            voiceHint.fontStyle = FontStyles.Bold;
            voiceHint.alignment = TextAlignmentOptions.Center;
            voiceHint.color = new Color(1f, 0.86f, 0.62f, 1f);
            voiceHint.raycastTarget = false;
            // Black outline so it reads on any background.
            TextOutline.Attach(voiceHint, 1.6f, Color.black);
        }

        VoiceNetwork_MP voice = VoiceNetwork_MP.Instance;
        string key = voice != null ? voice.PushToTalkKey.ToString() : "V";
        voiceHint.text = "Press <b>" + key + "</b> to open Voice Chat";
        voiceHint.alpha = 1f;
        voiceHint.gameObject.SetActive(true);
        voiceHintUntil = Time.unscaledTime + VoiceHintSeconds;
    }

    void UpdateVoiceHint()
    {
        if (voiceHint == null || !voiceHint.gameObject.activeSelf) return;

        float left = voiceHintUntil - Time.unscaledTime;
        if (left <= 0f) { voiceHint.gameObject.SetActive(false); return; }
        voiceHint.alpha = Mathf.Clamp01(left / VoiceHintFade);
    }

    void Update()
    {
        UpdateVoiceHint();

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

    // The Invite button follows the session instead of being decided once at Start: every player in the lobby has it (a
    // Steam lobby lets any member invite friends), the host as well as the clients.
    private void RefreshInviteButton()
    {
        if (inviteFriendsButton == null) return;

        bool show = NetworkServer.active || NetworkClient.active;
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
        // Without Steam the friend joins the HOST's address: a client copies the address it connected to, not its own.
        string address = steamLobby ? steam.CurrentCode
            : NetworkServer.active ? LocalAddress() : NetworkManager.singleton.networkAddress;
        string joinInfo = address + passwordPart;
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
