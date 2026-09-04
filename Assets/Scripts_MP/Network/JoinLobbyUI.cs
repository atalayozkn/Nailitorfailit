
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinLobbyUI : MonoBehaviour
{
    [SerializeField] TMP_InputField lobbyIdInput;
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] Button joinButton;
    [SerializeField] TMP_Text statusText;

    void Awake() => joinButton.onClick.AddListener(OnJoinPressed);

    void OnJoinPressed()
    {
        string sessionId = lobbyIdInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(password))
        {
            if (statusText != null) statusText.text = "Session ID ve şifre gerekli";
            return;
        }

        joinButton.interactable = false;
        if (statusText != null) statusText.text = "Bağlanıyor...";

        ((LobbyNetworkManager)NetworkManager.singleton).JoinLobby(sessionId, password);
    }
}
