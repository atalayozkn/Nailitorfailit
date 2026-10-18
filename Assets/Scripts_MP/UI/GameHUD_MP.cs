using TMPro;
using UnityEngine;

public class GameHUD_MP : MonoBehaviour
{
    [Header("Referanslar (ayni prefab icinde)")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject gameEndPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private GameObject victoryBanner;
    [SerializeField] private GameObject defeatBanner;

    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private bool gameEnded;

    private UnityEngine.Events.UnityEvent victoryEvent;
    private UnityEngine.Events.UnityEvent defeatEvent;
    private UnityEngine.Events.UnityAction onVictory;
    private UnityEngine.Events.UnityAction onDefeat;

    private void Start()
    {
        PlayerScripts.PlayerNetwork_MP net = transform.root.GetComponentInChildren<PlayerScripts.PlayerNetwork_MP>(true);
        if (net != null && !net.isOwned)
        {
            enabled = false;
            return;
        }

        stateMachine = transform.root.GetComponentInChildren<PlayerStateMachine>(true);
        movement = transform.root.GetComponentInChildren<PlayerMovement>(true);

        BindLevelEvents();
    }

    private void BindLevelEvents()
    {
        if (LevelManager.Instance == null)
        {
            Debug.LogWarning("[MP] LevelManager sahnede yok; oyun sonu panelleri tetiklenmeyecek.");
            return;
        }

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        victoryEvent = typeof(LevelManager).GetField("onVictoryEvent", flags)?.GetValue(LevelManager.Instance)
            as UnityEngine.Events.UnityEvent;
        defeatEvent = typeof(LevelManager).GetField("onDefeatEvent", flags)?.GetValue(LevelManager.Instance)
            as UnityEngine.Events.UnityEvent;

        onVictory = () => OnGameEnded(true);
        onDefeat = () => OnGameEnded(false);

        victoryEvent?.AddListener(onVictory);
        defeatEvent?.AddListener(onDefeat);

        if (victoryEvent == null && defeatEvent == null)
        {
            Debug.LogWarning("[MP] LevelManager oyun sonu event'leri bulunamadi; paneller acilmayacak.");
        }
    }

    private void Update()
    {
        if (gameEnded || timerText == null || GameTimeManager.Instance == null) return;

        float remaining = GameTimeManager.Instance.GetCurrentTime();
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void OnDestroy()
    {
        victoryEvent?.RemoveListener(onVictory);
        defeatEvent?.RemoveListener(onDefeat);
    }

    private void OnGameEnded(bool victory)
    {
        if (gameEnded) return;
        gameEnded = true;

        if (gameEndPanel != null) gameEndPanel.SetActive(true);
        if (victoryPanel != null) victoryPanel.SetActive(victory);
        if (defeatPanel != null) defeatPanel.SetActive(!victory);
        if (victoryBanner != null) victoryBanner.SetActive(victory);
        if (defeatBanner != null) defeatBanner.SetActive(!victory);

        if (movement != null) movement.SetActivity(false);
        if (stateMachine != null) stateMachine.OpenCursor();

        PauseMenu pauseMenu = transform.root.GetComponent<PauseMenu>();
        if (pauseMenu != null)
        {
            pauseMenu.enabled = false;
        }

        Debug.Log($"[MP] Tur bitti ({(victory ? "zafer" : "yenilgi")}); oyun sonu paneli acildi.");
    }
}
