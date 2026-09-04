
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

    void OnCreate()
    {
        string pw = passwordInput.text;

        if (string.IsNullOrWhiteSpace(pw))
        {
            if (statusText != null) statusText.text = "Şifre gerekli";
            return;
        }

        if (maxPlayersInput != null && int.TryParse(maxPlayersInput.text, out int maxPlayers) && maxPlayers > 0)
            NetworkManager.singleton.maxConnections = maxPlayers;

        createButton.interactable = false;
        if (statusText != null) statusText.text = "Relay oluşturuluyor...";

        ((LobbyNetworkManager)NetworkManager.singleton).HostLobby(pw);
    }
}
