using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{

    [SerializeField] private int roomCurrencyReward = 10;
    [SerializeField] private int roomTimeReward = 60;
    [SerializeField] private int currentLevel = 1;

    [SerializeField] private UnityEvent onVictoryEvent;
    [SerializeField] private UnityEvent onDefeatEvent;


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
        if (remainingRoomCount == 0) Victory();
        else Defeat();
    }
    private void Defeat()
    {
        onDefeatEvent?.Invoke();

        if (!CurrencyManager.Instance.HasEnoughCurrency(50))
        {
            //Play Backrupcy
            //Delay
            //Return to Menu scene as Menu Mode
            GameManager.Instance.CompleteLevel(currentLevel, false);
        }
        else
        {
            GameManager.Instance.ReturnToGameMenu();
            //Return to Menu scene as GameMode
        }
    }
    private void Victory()
    {
        onVictoryEvent?.Invoke();
        int score = ScoreManager.Instance.CalculateScore();

        //Probably delay will be added here

        CurrencyManager.Instance.GainCurrency(score);
        GameManager.Instance.CompleteLevel(currentLevel, true);
    }

}
