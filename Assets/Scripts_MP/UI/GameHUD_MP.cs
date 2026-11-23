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
        if (ScoreTallyUI.NetworkedButtons == (System.Action<System.Collections.Generic.IReadOnlyList<GameObject>, bool>)MpEndButtons_MP.BuildStack)
            ScoreTallyUI.NetworkedButtons = null;
        victoryEvent?.RemoveListener(onVictory);
        defeatEvent?.RemoveListener(onDefeat);
    }

    // The round is over: the money, timer, dog, stamina, inventory and hint panels of every HUD on screen (this player's
    // and the level's own singleplayer one) go away, so only the end screen is left. They stay off until the scene changes.
    public static readonly string[] InGameHudNames =
    {
        "Emote_Wheel", "TimeCounter", "TimeCounter_Shadow", "Stamina_Shadow", "Stamina_Slider",
        "Currency", "Currency_Shadow", "DogUI", "InventoryPanel", "LookAtInfoPanel",
    };

    // The level's own singleplayer HUD has its own Panel_GameEnd and its level events open it too: that was the second,
    // full-size sign on top of the tally's. Only this player's panel (the one with the tally) may show.
    private void HideAuthoredEndPanels()
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.name != "Panel_GameEnd") continue;
            if (gameEndPanel != null && (candidate.gameObject == gameEndPanel || candidate.IsChildOf(gameEndPanel.transform))) continue;

            candidate.gameObject.SetActive(false);

            Transform holder = candidate.parent != null ? candidate.parent : candidate;
            holder.gameObject.AddComponent<MpKeepChildOff_MP>().Watch(candidate.gameObject);
        }
    }

    private static void HideInGameHud()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null) continue;

            foreach (Transform child in canvas.transform)
            {
                if (System.Array.IndexOf(InGameHudNames, child.name) < 0) continue;

                child.gameObject.SetActive(false);
                canvas.gameObject.AddComponent<MpKeepChildOff_MP>().Watch(child.gameObject);
            }
        }
    }

    private void OnGameEnded(bool victory)
    {
        if (gameEnded) return;
        gameEnded = true;

        // Compete: the level manager only knows "time ran out" (fail for everybody). The result is the match's: the side with
        // the higher build percent NAILS IT, the other FAILS IT (a draw fails for both).
        VersusMatch match = VersusMatch.Instance;
        if (match != null && match.LocalSideIndex() >= 0)
        {
            int local = match.LocalSideIndex();
            int result = match.Winner;

            if (result == VersusMatch.NoWinner)
            {
                float mine = match.GetSide(local).percent, theirs = match.GetSide(1 - local).percent;
                result = Mathf.Abs(mine - theirs) < 0.0005f ? VersusMatch.Draw : mine > theirs ? local : 1 - local;
            }

            victory = result == local;
        }

        if (gameEndPanel != null) MpEndUiSuppressor_MP.StripIn(gameEndPanel);
        if (victoryPanel != null) MpEndUiSuppressor_MP.StripIn(victoryPanel);
        if (defeatPanel != null) MpEndUiSuppressor_MP.StripIn(defeatPanel);
        if (victoryBanner != null) MpEndUiSuppressor_MP.StripIn(victoryBanner);
        if (defeatBanner != null) MpEndUiSuppressor_MP.StripIn(defeatBanner);

        ScoreTallyUI tally = gameEndPanel != null ? gameEndPanel.GetComponent<ScoreTallyUI>() : null;
        bool useTally = tally != null && ScoreManager.Instance != null;

        if (gameEndPanel != null) gameEndPanel.SetActive(true);
        if (victoryPanel != null) victoryPanel.SetActive(victory);
        if (defeatPanel != null) defeatPanel.SetActive(!victory);

        // The tally brings the sign in by itself; the full-size banners would be a second copy of it on top.
        if (victoryBanner != null) victoryBanner.SetActive(!useTally && victory);
        if (defeatBanner != null) defeatBanner.SetActive(!useTally && !victory);

        HideInGameHud();
        HideAuthoredEndPanels();

        // The singleplayer end screen: score tally first, then the sign with its buttons (see ScoreTallyUI). The old
        // 2 x 2 button grid is only the fallback when there is nothing to tally.
        if (useTally)
        {
            ScoreTallyUI.NetworkedButtons = MpEndButtons_MP.BuildStack;
            tally.Play(ScoreManager.Instance.BuildReport(victory));
        }
        else
        {
            MpEndButtons_MP.Build(gameEndPanel);
        }

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
