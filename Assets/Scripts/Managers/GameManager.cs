using System;
using Unity.Cinemachine;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int startCurrencyAmount = 250;

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
    public void SwitchToMenuPhase()
    {
        currentPhase = GamePhase.Menu;
    }
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

    #region Game

    public void ChangeToGamePhase()
    {
        currentPhase = GamePhase.InGame;
        if (mainMenu != null) mainMenu.SetActivity(false);
        if (inGameOverlayUI != null) inGameOverlayUI.SetActivity(true);
        if (inGameCamera != null) SwitchToInGameCamera();
        if (!gameStarted)
        {
            CurrencyManager.Instance.SetCurrency(startCurrencyAmount);
            gameStarted = true;
        }
    }

    private void SetupGameScene()
    {
        if (mainMenu != null) mainMenu.SetActivity(false);
        if (inGameOverlayUI != null) inGameOverlayUI.SetActivity(true);
        if (inGameCamera != null) SwitchToInGameCamera();
    }

    #endregion

    #region Level

    public void ChangeToLevelPhase(int levelIndex)
    {
        currentPhase = GamePhase.InLevel;
        CurrencyManager.Instance.SetCurrency(startCurrencyAmount);
        GameSceneManager.Instance.LoadLevel(levelIndex);
    }
    public void CompleteLevel(int levelIndex, bool success)
    {
        if (success)
        {
            //levelSockets[levelIndex].isCompleted = true;

            currentPhase = GamePhase.InGame;
            GameSceneManager.Instance.LoadMenu();
            return;
        }

        // Reset the run data immediately.
        CurrencyManager.Instance.SetCurrency(startCurrencyAmount);
        gameStarted = false;

        // Return to the main menu as a completely fresh game.
        currentPhase = GamePhase.Menu;
        GameSceneManager.Instance.LoadMenu();
    }
    public void ReturnToGameMenu()
    {
        currentPhase = GamePhase.InGame;
        GameSceneManager.Instance.LoadMenu();
    }
    #endregion

    #region LevelSockets

    

    #endregion

    #region Utility
    public GamePhase GetCurrentPhase()
    {
        return currentPhase;
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