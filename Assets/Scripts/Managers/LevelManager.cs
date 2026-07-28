using System.Runtime.CompilerServices;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private int roomClearReward = 10;
    private RoomController[] roomControllers;
    private int remainingRoomCount;
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
    }
    public void CompleteRoom()
    {
        remainingRoomCount--;

        CurrencyManager.Instance.GainCurrency(roomClearReward); //Currency Reward
        GameTimeManager.Instance.IncreaseRoundTime(); //Time Reward

        if (IsLevelOver())
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
        if (remainingRoomCount == 0) Victory();
        else Defeat();
    }
    private void Defeat()
    {
        GameManager.Instance.CompleteLevel(false);
    }
    private void Victory()
    {
        int score = ScoreManager.Instance.CalculateScore();
        CurrencyManager.Instance.GainCurrency(score);
        GameManager.Instance.CompleteLevel(true);
    }
    private bool IsLevelOver()
    {
        return remainingRoomCount <= 0;
    }

}
