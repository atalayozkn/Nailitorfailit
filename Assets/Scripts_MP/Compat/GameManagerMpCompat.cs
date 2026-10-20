using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// The multiplayer scripts still navigate through the GameManager API the old
/// (pre-Multiplayer-rewrite) GameManager exposed. The current GameManager only knows
/// Menu/InLevel and no longer owns the MP panels, so that missing surface lives here:
///
///   * phase changes write the private phase field (SP code still owns the real flow),
///   * panel navigation drives <see cref="MpMenuUI"/> when the scene has one, and
///     otherwise the authored MenuScene_MP panels, exactly like the old GameManager's
///     serialized references did (mpMultiplayerMenuPanel etc. - those fields are still
///     present in MenuScene_MP's YAML but the current class no longer declares them).
/// </summary>
public static class GameManagerMpCompat
{
    private static readonly FieldInfo CurrentPhaseField =
        typeof(GameManager).GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic);

    // Authored panel names from MenuScene_MP (formerly GameManager's serialized refs).
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

    /// <summary>
    /// Called after Mirror loaded the MP game scene. The SP path announces scene loads
    /// through GameSceneManager; Mirror's ServerChangeScene does not, so without this the
    /// phase stayed on Menu.
    /// </summary>
    public static void EnterGamePhase(this GameManager gameManager)
    {
        if (gameManager == null) return;

        SetPhase(gameManager, GamePhase.InLevel);
        gameManager.OnSceneLoaded();
        MpMenuUI.Instance?.HideAll();
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

    /// <summary>
    /// Returns to the main menu screen underneath the MP panels. The old GameManager had
    /// no mpMainMenuPanel assigned, so its behaviour was simply "hide every MP panel".
    /// </summary>
    public static void ShowMainMenu(this GameManager gameManager)
    {
        if (MpMenuUI.Instance != null)
        {
            MpMenuUI.Instance.HideAll();
            return;
        }

        ShowScenePanel(null);
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
            if (panel != null) panel.SetActive(panelName == activeName);
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
