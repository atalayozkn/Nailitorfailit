using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{

    [SerializeField] private int roomCurrencyReward = 10;
    [SerializeField] private int roomTimeReward = 60;

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
    public void CompleteRoom()
    {
        if (isLevelEnded) return;

        remainingRoomCount--;

        CurrencyManager.Instance.GainCurrency(roomCurrencyReward); //Currency Reward
        GameTimeManager.Instance.IncreaseRoundTime(roomTimeReward); //Time Reward

        if (remainingRoomCount <= 0)
        {
            Victory();
        }
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

        if (!CurrencyManager.Instance.HasEnoughCurrency(50))
        {
            //Play Backrupcy
            //Delay
        }
    }
    private void Victory()
    {
        EndLevel();
        onVictoryEvent?.Invoke();
        int score = ScoreManager.Instance.CalculateScore();

        //Probably delay will be added here

        CurrencyManager.Instance.GainCurrency(score);

        // Save progress right away so the next level is unlocked even if the player quits from here.
        GameManager.Instance?.CompleteCurrentLevel();
    }

}
