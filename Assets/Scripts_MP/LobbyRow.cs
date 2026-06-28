using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private GameObject      lockIcon;   // optional: shown when locked

    private CustomServerResponse response;
    private ServerPanelManager   serverPanelManager;

    public void Setup(CustomServerResponse response, ServerPanelManager manager)
    {
        this.response = response;
        this.serverPanelManager = manager;

        nameText.text = string.IsNullOrEmpty(response.lobbyName)
            ? response.uri.Host
            : response.lobbyName;

        playerCountText.text = $"{response.currentPlayers}/{response.maxPlayers}";

        if (lockIcon) lockIcon.SetActive(response.HasPassword);

        Button btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => serverPanelManager.OnRowSelected(this.response));
    }

    public CustomServerResponse GetResponse() => response;
}