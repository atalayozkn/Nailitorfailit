using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int startCurrencyAmount = 250;
    [SerializeField] private int firstLevelIndex = 1;

    private GamePhase currentPhase = GamePhase.Menu;

    // Scene References
    private MainMenu mainMenu;

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

        SaveSystem.Load();
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
        mainMenu = FindAnyObjectByType<MainMenu>();

        currentPhase = mainMenu != null ? GamePhase.Menu : GamePhase.InLevel;

        if (currentPhase == GamePhase.Menu) mainMenu.SwitchToMainTab();
    }

    #endregion

    #region Menu

    public void NewGame()
    {
        SaveSystem.ResetProgress();
        PlayLevel(firstLevelIndex);
    }

    public bool CanContinue()
    {
        return SaveSystem.HasSave();
    }

    // Continues from the furthest unlocked level.
    public void Continue()
    {
        if (!CanContinue()) return;

        int level = Mathf.Clamp(SaveSystem.Data.highestUnlockedLevel, firstLevelIndex, GameSceneManager.Instance.GetLevelCount());
        PlayLevel(level);
    }

    #endregion

    #region Level

    public void PlayLevel(int levelIndex)
    {
        if (levelIndex < firstLevelIndex || levelIndex > GameSceneManager.Instance.GetLevelCount())
        {
            Debug.LogWarning($"GameManager: Level {levelIndex} does not exist in GameSceneManager scenes.");
            return;
        }

        currentPhase = GamePhase.InLevel;
        CurrencyManager.Instance.SetCurrency(startCurrencyAmount);
        GameSceneManager.Instance.LoadLevel(levelIndex);
    }

    // Saves the active level as completed and unlocks the next one.
    public void CompleteCurrentLevel()
    {
        int levelIndex = GameSceneManager.Instance.GetActiveSceneIndex();
        if (levelIndex < firstLevelIndex) return;

        SaveSystem.CompleteLevel(levelIndex);
    }

    public void ReturnToMenu()
    {
        currentPhase = GamePhase.Menu;
        GameSceneManager.Instance.LoadMenu();
    }

    #endregion

    #region Utility

    public GamePhase GetCurrentPhase()
    {
        return currentPhase;
    }

    #endregion
}
