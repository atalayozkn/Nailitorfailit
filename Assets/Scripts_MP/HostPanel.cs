using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class HostPanel : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_InputField maxPlayersInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("UI")]
    [SerializeField] private Button   createButton;
    [SerializeField] private Button   quitButton;
    [SerializeField] private TMP_Text errorText;

    private void Awake()
    {
        createButton.onClick.AddListener(OnClickCreate);
        quitButton.onClick.AddListener(OnClickQuit);
        errorText.gameObject.SetActive(false);

        lobbyNameInput.characterLimit = 50;
        maxPlayersInput.characterLimit = 1;
        passwordInput.characterLimit  = 12;
    }

    private void OnClickCreate()
    {
        errorText.gameObject.SetActive(false);

        string lobbyName = lobbyNameInput.text.Trim();
        if (lobbyName.Length < 3)
        {
            ShowError("Lobby name must be at least 3 characters.");
            return;
        }

        if (!int.TryParse(maxPlayersInput.text, out int maxPlayers)
            || maxPlayers < 2 || maxPlayers > 6)
        {
            ShowError("Player count must be between 2 and 6.");
            return;
        }

        string password = passwordInput.text;
        if (password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        LobbyData.Set(lobbyName, maxPlayers, password);

        // StartHost loads the onlineScene (ExampleScene_MP) and unloads this menu,
        // and OnStartServer begins advertising the lobby on the LAN.
        NetworkManager.singleton.StartHost();
    }

    private void OnClickQuit()
    {
        LobbyUI.Instance.ReturnToMainMenu();
    }

    private void ShowError(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }

    public void Open()
    {
        lobbyNameInput.text  = "";
        maxPlayersInput.text = "";
        passwordInput.text   = "";
        errorText.gameObject.SetActive(false);
        gameObject.SetActive(true);
    }
}