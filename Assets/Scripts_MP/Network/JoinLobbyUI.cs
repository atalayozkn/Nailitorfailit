
using System.Collections;
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

    [SerializeField] float retryUnlockSeconds = 8f;

    void Awake() => joinButton.onClick.AddListener(OnJoinPressed);

    void OnEnable()
    {
        if (joinButton != null) joinButton.interactable = true;
        if (statusText != null) statusText.text = string.Empty;
    }

    void OnJoinPressed()
    {
        string sessionId = lobbyIdInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(password))
        {
            if (statusText != null) statusText.text = "Session ID and password required";
            return;
        }

        joinButton.interactable = false;
        if (statusText != null) statusText.text = "Connecting...";

        ((LobbyNetworkManager)NetworkManager.singleton).JoinLobby(sessionId, password);

        if (isActiveAndEnabled)
            StartCoroutine(UnlockIfNotConnected());
    }

    private IEnumerator UnlockIfNotConnected()
    {
        yield return new WaitForSeconds(retryUnlockSeconds);

        if (NetworkClient.isConnected) yield break;

        if (joinButton != null) joinButton.interactable = true;
        if (statusText != null) statusText.text = "Could not connect - try again";
    }
}
