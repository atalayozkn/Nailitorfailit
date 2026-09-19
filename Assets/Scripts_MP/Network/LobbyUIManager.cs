
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
        Debug.Log("[UI] LobbyUIManager ready: Leave/Start listeners bound.");
        leaveLobbyButton.onClick.AddListener(OnLeaveLobby);
        startGameButtonComponent.onClick.AddListener(OnStartGame);
    }

    public PlayerListEntryUI AddPlayerRow(LobbyPlayer player)
    {
        PlayerListEntryUI row = Instantiate(playerEntryPrefab, playerListContent);

        // Prefab sabit genislikte olabilir (500px). Icerik alani daha darsa metin
        // panelden tasar. Bu yuzden satir genisligini icerigin genisligine uyduruyoruz.
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

            if (!player.isHost)
                readyButtonComponent.onClick.AddListener(
                    () => player.CmdSetReady(!player.isReady));
        }

        return row;
    }

    public void RemovePlayerRow(PlayerListEntryUI row)
    {
        if (row != null) Destroy(row.gameObject);
    }

    void OnStartGame()
    {
        startGameButtonComponent.interactable = false;
        ((LobbyNetworkManager)NetworkManager.singleton).StartGame();
    }

    void OnLeaveLobby()
    {
        leaveLobbyButton.interactable = false;

        var relayMgr = EdgegapRelayManager.Instance;
        var lobbyNm = NetworkManager.singleton as LobbyNetworkManager;

        bool wasHost = NetworkServer.active && NetworkClient.isConnected;

        if (wasHost)
        {
            if (relayMgr != null && lobbyNm != null) relayMgr.DeleteRelaySession(lobbyNm.edgegapSessionId);
            Debug.Log("[LOBBY] Host leaving: deleting relay session.");
            NetworkManager.singleton.StopHost();
        }
        else
        {
            if (relayMgr != null && lobbyNm != null)
                relayMgr.RevokeSelfInSession(lobbyNm.edgegapSessionId, relayMgr.lastUserAuthToken);
            Debug.Log("[LOBBY] Client leaving: closing connection.");
            NetworkManager.singleton.StopClient();
        }

        if (GameManager.Instance != null) GameManager.Instance.ShowMainMenu();

        leaveLobbyButton.interactable = true;
        if (lobbyNm != null) lobbyNm.ResetLobbyState();
    }
}
