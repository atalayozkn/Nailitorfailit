using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{

    [Tooltip("Dollars paid per tile when a room is finished (times the safety margin).")]
    [SerializeField] private int roomCurrencyReward = 5;
    [Tooltip("Extra on top of the calculated room pay, so untested levels can not run out of money. 1.15 = 15% more.")]
    [SerializeField] private float rewardSafetyMargin = 1.15f;
    [SerializeField] private int roomTimeReward = 90;

    [SerializeField] private UnityEvent onVictoryEvent;
    [SerializeField] private UnityEvent onDefeatEvent;


    private RoomController[] roomControllers;
    private int remainingRoomCount;
    private bool isLevelEnded = false;
    public static LevelManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        roomControllers = FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        remainingRoomCount = roomControllers.Length;

        if (roomControllers.Length == 0) Debug.LogWarning("LevelManager: No RoomController found in scene, level can not be won.");
    }
    public void CompleteRoom(int tileCount)
    {
        if (isLevelEnded) return;

        remainingRoomCount--;

        // Bigger rooms pay more, so finishing the small rooms first pays for the oil the big one needs.
        CurrencyManager.Instance?.GainCurrency(GetRoomReward(tileCount));
        GameTimeManager.Instance.IncreaseRoundTime(roomTimeReward); //Time Reward

        if (remainingRoomCount <= 0)
        {
            Victory();
        }
    }
    public int GetRoomReward(int tileCount)
    {
        return Mathf.RoundToInt(roomCurrencyReward * tileCount * rewardSafetyMargin);
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    public void TimeExpired()
    {
        if (isLevelEnded) return;

        if (roomControllers.Length > 0 && remainingRoomCount <= 0) Victory();
        else Defeat();
    }
    // Stops the round so victory/defeat can only trigger once.
    private void EndLevel()
    {
        isLevelEnded = true;
        GameTimeManager.Instance?.SetActivity(false);
    }
    private void Defeat()
    {
        EndLevel();
        onDefeatEvent?.Invoke();

        if (CurrencyManager.Instance != null && !CurrencyManager.Instance.HasEnoughCurrency(50))
        {
            //Play Backrupcy
            //Delay
        }

        ShowScoreTally(false);
    }
    private void Victory()
    {
        EndLevel();
        onVictoryEvent?.Invoke();
        VfxManager.Play(VfxId.Victory, FindFirstObjectByType<PlayerStateMachine>().transform.position);
        // Money only lives inside a level, so the score is no longer paid out as currency.

        // Save progress right away so the next level is unlocked even if the player quits from here.
        GameManager.Instance?.CompleteCurrentLevel();

        ShowScoreTally(true);
    }
    // The end events above have just opened Panel_GameEnd, the tally plays on top of it. Multiplayer keeps its own end screen.
    private void ShowScoreTally(bool isVictory)
    {
        if (NetworkModeHelper.IsNetworked || ScoreManager.Instance == null) return;

        ScoreTallyUI tally = FindFirstObjectByType<ScoreTallyUI>();
        if (tally != null) tally.Play(ScoreManager.Instance.BuildReport(isVictory));
    }

}
