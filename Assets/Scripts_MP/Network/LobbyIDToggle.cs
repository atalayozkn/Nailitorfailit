
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyIdToggle : MonoBehaviour
{
    public static LobbyIdToggle Instance;

    [SerializeField] TMP_Text lobbyIdText;
    [SerializeField] Button toggleButton;
    [SerializeField] Button copyButton;

    bool isRevealed = false;
    string actualSessionId = "";
    const string HiddenMask = "••••••••••";

    void Awake() => Instance = this;

    void Start()
    {
        toggleButton.onClick.AddListener(OnToggleClicked);

        if (copyButton != null)
        {
            copyButton.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = actualSessionId;
                Debug.Log("Session ID kopyalandı: " + actualSessionId);
            });
        }

        lobbyIdText.text = HiddenMask;

        if (LobbySettings.Instance != null && !string.IsNullOrEmpty(LobbySettings.Instance.sessionId))
            SetLobbyId(LobbySettings.Instance.sessionId);
    }

    public void SetLobbyId(string sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
            actualSessionId = sessionId;

        if (!isRevealed) lobbyIdText.text = HiddenMask;
    }

    void OnToggleClicked()
    {
        isRevealed = !isRevealed;
        lobbyIdText.text = isRevealed ? actualSessionId : HiddenMask;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
