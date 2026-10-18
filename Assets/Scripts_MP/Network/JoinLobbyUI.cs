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

    /// <summary>
    /// Steam daveti kabul edildiginde LobbyNetworkManager tarafindan cagrilir: kod hazir
    /// ama sifre Steam'de tutulmadigi icin kullanici girmeli.
    /// </summary>
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

    /// <summary>
    /// Back/exit button: return to the multiplayer menu and drop any connection
    /// attempt that is still running, so a late connect cannot yank the player
    /// into a lobby they already left.
    /// </summary>
    public void OnExit()
    {
        StopCoroutine_Unlock();

        if (joinButton != null) joinButton.interactable = true;
        if (statusText != null) statusText.text = string.Empty;

        ((LobbyNetworkManager)NetworkManager.singleton).CancelPendingSetup();

        if (GameManager.Instance != null) GameManager.Instance.ShowMultiplayerMenu();
        else gameObject.SetActive(false);
    }

    /// <summary>OnExit can fire while the panel is inactive; inactive objects cannot run coroutines.</summary>
    void StopCoroutine_Unlock()
    {
        if (unlockCoroutine != null)
        {
            StopCoroutine(unlockCoroutine);
            unlockCoroutine = null;
        }
    }
}
