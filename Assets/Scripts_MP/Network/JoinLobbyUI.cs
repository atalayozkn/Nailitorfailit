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

    public void PrefillInviteCode(string code)
    {
        if (lobbyIdInput != null) lobbyIdInput.text = code;
        if (passwordInput != null)
        {
            passwordInput.text = string.Empty;
            passwordInput.ActivateInputField();
        }

        if (joinButton != null) joinButton.interactable = true;
        if (statusText != null) statusText.text = "You were invited - enter the lobby password";
    }

    void OnJoinPressed()
    {
        string code = LobbyCodeCodec.Canonicalize(lobbyIdInput.text);
        string password = passwordInput.text;

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(password))
        {
            if (statusText != null) statusText.text = "Lobby code and password required";
            return;
        }

        joinButton.interactable = false;
        if (statusText != null) statusText.text = "Connecting...";

        ((LobbyNetworkManager)NetworkManager.singleton).JoinLobby(code, password);

        if (isActiveAndEnabled)
            unlockCoroutine = StartCoroutine(UnlockIfNotConnected());
    }

    Coroutine unlockCoroutine;

    private IEnumerator UnlockIfNotConnected()
    {
        yield return new WaitForSeconds(retryUnlockSeconds);

        if (NetworkClient.isConnected) yield break;

        if (joinButton != null) joinButton.interactable = true;

        if (statusText != null)
        {
            string error = SteamLobbyManager.Instance != null ? SteamLobbyManager.Instance.LastError : null;
            statusText.text = string.IsNullOrEmpty(error) ? "Could not connect - try again" : error;
        }
    }

    public void OnExit()
    {
        StopCoroutine_Unlock();

        if (joinButton != null) joinButton.interactable = true;
        if (statusText != null) statusText.text = string.Empty;

        ((LobbyNetworkManager)NetworkManager.singleton).CancelPendingSetup();

        if (GameManager.Instance != null) GameManager.Instance.ShowMultiplayerMenu();
        else gameObject.SetActive(false);
    }

    void StopCoroutine_Unlock()
    {
        if (unlockCoroutine != null)
        {
            StopCoroutine(unlockCoroutine);
            unlockCoroutine = null;
        }
    }
}
