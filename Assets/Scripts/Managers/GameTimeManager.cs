using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Settings")]
    [SerializeField] private float maxRoundTime;
    [SerializeField] private float perRewardTimeIncrease;

    [Header("Events")]
    [SerializeField] private UnityEvent onTimeIncreaseEvent;

    private float currentTime;
    private bool isActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        currentTime = maxRoundTime;

        SetActivity(true);
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    private void Update()
    {
        if (!isActive) return;

        ProgressTime();
    }
    private void ProgressTime()
    {
        currentTime -= Time.deltaTime;

        if (currentTime < 0f) currentTime = 0f;

        UpdateUI();

        if (currentTime <= 0f && isActive)
        {
            isActive = false;
            NotifyGameEnd();
        }
    }
    private void UpdateUI()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        timerText.text = $"{minutes:00}:{seconds:00}";
    }
    private void NotifyGameEnd()
    {
        LevelManager.Instance.TimeExpired();
        SetActivity(false);
    }
    public void SetActivity(bool condition)
    {
        isActive = condition;
    }
    public void IncreaseRoundTime()
    {
        currentTime += perRewardTimeIncrease;
        UpdateUI();
        onTimeIncreaseEvent?.Invoke();
    }
    public float GetCurrentTime()
    {
        return currentTime;
    }
}
