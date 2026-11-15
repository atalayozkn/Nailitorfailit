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
    private bool authoredTimerActive;

    private float nextHudLogAt;

    public void HideAuthoredDuplicates(bool hideTimer, bool hideCurrency)
    {
        if (hideTimer && timerText != null)
        {
            authoredTimerActive = true;

            InteractionDebug.Log("[MP-TIME] own timer hidden, the authored SP HUD timer is used instead.");

            Transform counter = timerText.transform.parent;
            if (counter != null) counter.gameObject.SetActive(false);
        }

        if (!hideCurrency) return;

        CurrencyText currency = GetComponentInChildren<CurrencyText>(true);
        if (currency != null && currency.transform.parent != null)
            currency.transform.parent.gameObject.SetActive(false);
    }

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

    private static readonly System.Reflection.FieldInfo TimerTextField =
        typeof(GameTimeManager).GetField("timerText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    // SP's GameTimerDisplay binds the GameTimeManager to the timer text of EVERY HUD that starts. On the host the
    // client's player package spawns after the host's own, so the countdown was written to the remote player's
    // hidden HUD and the host saw it frozen. Only the owner's HUD runs this, so it always wins.
    private void EnsureTimerBinding()
    {
        GameTimeManager manager = GameTimeManager.Instance;
        if (manager == null || TimerTextField == null) return;

        TextMeshProUGUI wanted = authoredTimerActive ? MpSharedCompat.AuthoredTimerText : timerText;
        if (wanted == null || (TextMeshProUGUI)TimerTextField.GetValue(manager) == wanted) return;

        manager.BindTimerText(wanted);
        InteractionDebug.Log($"[MP-TIME] timer re-bound to the local HUD text '{wanted.transform.root.name}/{wanted.name}'.");
    }

    private void Update()
    {
        EnsureTimerBinding();

        if (authoredTimerActive || gameEnded || timerText == null || GameTimeManager.Instance == null) return;

        float remaining = GameTimeManager.Instance.GetCurrentTime();
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        if (Time.unscaledTime >= nextHudLogAt)
        {
            nextHudLogAt = Time.unscaledTime + 2f;
            InteractionDebug.Log($"[MP-TIME] hud writes '{timerText.text}' active={GameTimeManager.Instance.IsTimerActive()}");
        }
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

        if (gameEndPanel != null) MpEndUiSuppressor_MP.StripIn(gameEndPanel);
        if (victoryPanel != null) MpEndUiSuppressor_MP.StripIn(victoryPanel);
        if (defeatPanel != null) MpEndUiSuppressor_MP.StripIn(defeatPanel);
        if (victoryBanner != null) MpEndUiSuppressor_MP.StripIn(victoryBanner);
        if (defeatBanner != null) MpEndUiSuppressor_MP.StripIn(defeatBanner);

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
