using System.Reflection;
using TMPro;
using UnityEngine;

public static class GameManagerMpCompat
{
    private static readonly FieldInfo CurrentPhaseField =
        typeof(GameManager).GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic);

    private const string MainMenuPanel = "Buttons_MainMenu";
    private const string MultiplayerPanel = "Buttons_AfterMultiplayer";
    private const string CreatePanel = "Panel_AfterCreate";
    private const string JoinPanel = "Panel_AfterJoin";
    private const string LobbyPanel = "Panel_Lobby";
    private const string CharacterSelectionPanel = "CharacterSelection";

    private static readonly string[] MpPanels =
    {
        MultiplayerPanel, CreatePanel, JoinPanel, LobbyPanel, CharacterSelectionPanel,
    };

    public static void SwitchToMenuPhase(this GameManager gameManager)
    {
        SetPhase(gameManager, GamePhase.Menu);
    }

    private static bool currencyGranted;

    public static void EnterGamePhase(this GameManager gameManager)
    {
        if (gameManager == null) return;

        SetPhase(gameManager, GamePhase.InLevel);

        if (!currencyGranted)
        {
            int amount = gameManager.GetStartCurrencyAmount();

            if (CurrencyManager.Instance != null) CurrencyManager.Instance.SetCurrency(amount);
            CurrencyManager_MP.SeedServerTotal(amount);

            currencyGranted = true;
            Debug.Log($"[MP] Starting currency granted: {amount}");
        }

        gameManager.OnSceneLoaded();
        MpMenuUI.Instance?.HideAll();
    }

    public static void ShowCreateLobby(this GameManager gameManager)
    {
        if (MpMenuUI.Instance != null)
        {
            MpMenuUI.Instance.ShowCreate();
            return;
        }

        ShowScenePanel(CreatePanel);
    }

    public static void ShowMultiplayerMenu(this GameManager gameManager)
    {
        if (MpMenuUI.Instance != null)
        {
            MpMenuUI.Instance.ShowMultiplayerMenu();
            return;
        }

        ShowScenePanel(MultiplayerPanel);
    }

    public static void ShowJoinLobby(this GameManager gameManager)
    {
        if (MpMenuUI.Instance != null)
        {
            MpMenuUI.Instance.ShowJoin();
            return;
        }

        ShowScenePanel(JoinPanel);
    }

    public static void ShowLobbyMenu(this GameManager gameManager)
    {
        if (MpMenuUI.Instance != null)
        {
            MpMenuUI.Instance.ShowLobby();
            return;
        }

        ShowScenePanel(LobbyPanel);
        RefreshLobbyLabels();
    }

    public static void ShowMainMenu(this GameManager gameManager)
    {
        if (MpMenuUI.Instance != null)
        {
            MpMenuUI.Instance.HideAll();
            return;
        }

        bool hasMainMenuPanel = FindSceneObject(MainMenuPanel) != null;
        ShowScenePanel(hasMainMenuPanel ? MainMenuPanel : MultiplayerPanel);
    }

    private static void SetPhase(GameManager gameManager, GamePhase phase)
    {
        if (gameManager == null || CurrentPhaseField == null) return;
        CurrentPhaseField.SetValue(gameManager, phase);
    }

    private static void ShowScenePanel(string activeName)
    {
        foreach (string panelName in MpPanels)
        {
            GameObject panel = FindSceneObject(panelName);
            if (panel == null) continue;
            if (panel.activeSelf != (panelName == activeName)) panel.SetActive(panelName == activeName);
        }

        if (string.IsNullOrEmpty(activeName)) return;

        GameObject target = FindSceneObject(activeName);
        if (target != null) EnsureVisible(target);
    }

    private static void EnsureVisible(GameObject target)
    {
        for (Transform node = target.transform; node != null; node = node.parent)
        {
            if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
        }
    }

    private static void RefreshLobbyLabels()
    {
        LobbyNetworkManager manager = Mirror.NetworkManager.singleton as LobbyNetworkManager;
        if (manager == null) return;

        SetLabel("Lobby_ID_Label", manager.LobbyID);
        SetLabel("Lobby_Password_Label", manager.LobbyPassword);
    }

    private static void SetLabel(string parentName, string value)
    {
        GameObject parent = FindSceneObject(parentName);
        if (parent == null) return;

        TMP_Text label = parent.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    private static GameObject FindSceneObject(string name)
    {
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name == name) return candidate.gameObject;
        }

        return null;
    }
}
