
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateLobbyUI : MonoBehaviour
{
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] TMP_InputField maxPlayersInput;
    [SerializeField] Button createButton;
    [SerializeField] TMP_Text statusText;

    void Awake() => createButton.onClick.AddListener(OnCreate);

    void OnEnable()
    {
        if (createButton != null) createButton.interactable = true;
        if (statusText != null) statusText.text = string.Empty;
    }

    void OnCreate()
    {
        string pw = passwordInput.text;

        if (string.IsNullOrWhiteSpace(pw))
        {
            if (statusText != null) statusText.text = "Password required";
            return;
        }

        int maxPlayers = LobbyNetworkManager.MaxPlayers;
        if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int typed))
            maxPlayers = Mathf.Clamp(typed, LobbyNetworkManager.MinPlayers, LobbyNetworkManager.MaxPlayers);

        NetworkManager.singleton.maxConnections = maxPlayers;

        createButton.interactable = false;
        if (statusText != null) statusText.text = "Creating relay...";

        ((LobbyNetworkManager)NetworkManager.singleton).HostLobby(pw);
    }
}
