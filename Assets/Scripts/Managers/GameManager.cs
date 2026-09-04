using System;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Serializable]
    public class LevelSocketData
    {
        public LevelSocket levelSocket;

        [Header("State")]
        public bool isCompleted;
        public bool isIndicated;
    }

    public static GameManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int startCurrencyAmount = 250;
    [SerializeField] private LevelSocketData[] levelSockets;

    private GamePhase currentPhase = GamePhase.Menu;

    // Scene References
    private CinemachineCamera inGameCamera;
    private CinemachineCamera inMenuCamera;
    private MainMenu mainMenu;
    private InGameOverlayUI inGameOverlayUI;

    private bool gameStarted;

    #region Initialization

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetLevelSocketStates();
    }

    private void Start()
    {
        OnSceneLoaded();
    }

    #endregion

    #region Scene

    /// <summary>
    /// Called by GameSceneManager whenever a new scene has finished loading.
    /// </summary>
    public void OnSceneLoaded()
    {
        CacheSceneReferences();

        switch (currentPhase)
        {
            case GamePhase.Menu:
                SetupMenuScene();
                break;

            case GamePhase.InGame:
                SetupGameScene();
                break;
        }
    }

    private void CacheSceneReferences()
    {
        GameObject inGameCameraObject = GameObject.FindGameObjectWithTag("InGame");
        if (inGameCameraObject != null) inGameCamera = inGameCameraObject.GetComponent<CinemachineCamera>();

        GameObject inMenuCameraObject = GameObject.FindGameObjectWithTag("InMenu");
        if (inMenuCameraObject != null) inMenuCamera = inMenuCameraObject.GetComponent<CinemachineCamera>();

        mainMenu = FindAnyObjectByType<MainMenu>();
        inGameOverlayUI = FindAnyObjectByType<InGameOverlayUI>();
    }

    #endregion

    #region Menu

    private void SetupMenuScene()
    {
        if (inMenuCamera != null) SwitchToMenuCamera();

        if (mainMenu != null)
        {
            mainMenu.SetActivity(true);
            mainMenu.SwitchToMenuTab();
        }

        if (inGameOverlayUI != null) inGameOverlayUI.SetActivity(false);
    }

    public void ActivateCharacterSelection()
    {
        mainMenu.SwitchToCharacterTab();
    }

    public void StartGame(int characterIndex)
    {
        switch (characterIndex)
        {
            case 0:
                // Spawn selected character later.
                break;
        }

        ChangeToGamePhase();
    }

    #endregion

    #region Multiplayer

    [Header("Multiplayer Panels")]
    [SerializeField] private GameObject mpMainMenuPanel;
    [SerializeField] private GameObject mpMultiplayerMenuPanel;
    [SerializeField] private GameObject mpCreateLobbyPanel;
    [SerializeField] private GameObject mpJoinLobbyPanel;
    [SerializeField] private GameObject mpLobbyMenuPanel;
    [SerializeField] private TMP_Text mpLobbyIdLabel;
    [SerializeField] private TMP_Text mpLobbyPasswordLabel;

    public void ShowMultiplayerMenu() => SetActiveMultiplayerPanel(mpMultiplayerMenuPanel);
    public void ShowCreateLobby() => SetActiveMultiplayerPanel(mpCreateLobbyPanel);
    public void ShowJoinLobby() => SetActiveMultiplayerPanel(mpJoinLobbyPanel);

    public void ShowLobbyMenu()
    {
        SetActiveMultiplayerPanel(mpLobbyMenuPanel);

        LobbyNetworkManager manager = LobbyNetworkManager.singleton as LobbyNetworkManager;
        if (manager == null) return;

        if (mpLobbyIdLabel != null) mpLobbyIdLabel.text = manager.LobbyID;
        if (mpLobbyPasswordLabel != null) mpLobbyPasswordLabel.text = manager.LobbyPassword;
    }

    private void SetActiveMultiplayerPanel(GameObject panel)
    {
        if (mpMainMenuPanel != null) mpMainMenuPanel.SetActive(panel == mpMainMenuPanel);
        if (mpMultiplayerMenuPanel != null) mpMultiplayerMenuPanel.SetActive(panel == mpMultiplayerMenuPanel);
        if (mpCreateLobbyPanel != null) mpCreateLobbyPanel.SetActive(panel == mpCreateLobbyPanel);
        if (mpJoinLobbyPanel != null) mpJoinLobbyPanel.SetActive(panel == mpJoinLobbyPanel);
        if (mpLobbyMenuPanel != null) mpLobbyMenuPanel.SetActive(panel == mpLobbyMenuPanel);
    }

    #endregion

    #region Game

    public void ChangeToGamePhase()
    {
        currentPhase = GamePhase.InGame;
        if (mainMenu != null) mainMenu.SetActivity(false);
        if (inGameOverlayUI != null) inGameOverlayUI.SetActivity(true);
        if (inGameCamera != null) SwitchToInGameCamera();
        if (!gameStarted)
        {
            CurrencyManager.Instance.GainCurrency(startCurrencyAmount);
            gameStarted = true;
        }
        RefreshLevelSockets();
    }

    private void SetupGameScene()
    {
        if (mainMenu != null) mainMenu.SetActivity(false);
        if (inGameOverlayUI != null) inGameOverlayUI.SetActivity(true);
        if (inGameCamera != null) SwitchToInGameCamera();
        RefreshLevelSockets();
    }

    #endregion

    #region Level

    public void ChangeToLevelPhase(int levelIndex)
    {
        currentPhase = GamePhase.InLevel;
        GameSceneManager.Instance.LoadLevel(levelIndex);
    }
    public void CompleteLevel(int levelIndex, bool success)
    {
        if (success)
        {
            levelSockets[levelIndex].isCompleted = true;
            // Leave isIndicated untouched.
        }

        currentPhase = GamePhase.InGame;

        GameSceneManager.Instance.LoadMenu();
    }
    #endregion

    #region LevelSockets

    private void RefreshLevelSockets()
    {
        foreach (LevelSocketData socket in levelSockets)
        {
            if (socket.levelSocket == null) continue;
            socket.levelSocket.Refresh(socket.isCompleted, socket.isIndicated);
        }
    }
    private void ResetLevelSocketStates()
    {
        foreach (LevelSocketData socket in levelSockets)
        {
            socket.isCompleted = false;
            socket.isIndicated = false;
        }
    }

    #endregion

    #region Utility

    public GamePhase GetCurrentPhase()
    {
        return currentPhase;
    }
    public LevelSocketData GetLevelData(int index)
    {
        return levelSockets[index];
    }
    public void MarkLevelIndicated(int levelIndex)
    {
        levelSockets[levelIndex].isIndicated = true;
    }

    private void SwitchToMenuCamera()
    {
        inMenuCamera.Priority = 1;
        inGameCamera.Priority = 0;
    }
    private void SwitchToInGameCamera()
    {
        inMenuCamera.Priority = 0;
        inGameCamera.Priority = 1;
    }

    #endregion
}