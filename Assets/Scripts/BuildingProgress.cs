using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ItemScript;

public class BuildingProgress : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float startTime = 120f;
    [SerializeField] private float bonusTimePerBuild = 3f;

    [Header("UI References")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Smooth Settings")]
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private float progressPercent;

    private float currentTime;
    private bool timerRunning = false;
    private bool gameFinished = false;
    private bool timerStartedByPlayer = false;

    private float targetSliderValue = 0f;

    [SerializeField] private List<ConstructObject> constructs = new List<ConstructObject>();

    private List<GameObject> players = new List<GameObject>();

    private int totalCount;
    private int builtCount;

    public float ProgressPercent => progressPercent;

    // =============================

    void Awake()
    {
        FindAllConstructs();
        currentTime = startTime;
        UpdateUI();
    }

    void Update()
    {
        DetectPlayers();
        UpdateTimer();
        UpdateSliderSmooth();
    }

    // =============================
    // PLAYER DETECT
    // =============================
    private void DetectPlayers()
    {
        players.Clear();
        players.AddRange(GameObject.FindGameObjectsWithTag(playerTag));

        if (!timerStartedByPlayer && players.Count > 0)
        {
            timerStartedByPlayer = true;
            StartTimer();
            Debug.Log("Player detected → Timer started");
        }
    }

    // =============================
    // TIMER
    // =============================
    private void UpdateTimer()
    {
        if (!timerRunning || gameFinished) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            timerRunning = false;
            gameFinished = true;
            GameOver();
        }

        UpdateTimeText();
    }

    // =============================
    // SMOOTH SLIDER
    // =============================
    private void UpdateSliderSmooth()
    {
        if (progressSlider == null) return;

        progressSlider.value = Mathf.Lerp(
            progressSlider.value,
            targetSliderValue,
            Time.deltaTime * smoothSpeed);
    }

    // =============================
    public void StartTimer()
    {
        timerRunning = true;
    }

    // =============================
    private void FindAllConstructs()
    {
        constructs.Clear();
        builtCount = 0;

        GameObject[] objs = GameObject.FindGameObjectsWithTag("WallOrFloor");

        foreach (var obj in objs)
        {
            ConstructObject construct = obj.GetComponent<ConstructObject>();
            if (construct == null) continue;

            constructs.Add(construct);

            if (construct.IsBuilt)
                builtCount++;

            // 🔥 EVENT DINLE
            construct.OnBuilt -= OnConstructBuilt;
            construct.OnBuilt += OnConstructBuilt;
        }

        totalCount = constructs.Count;
        UpdateProgress();

        Debug.Log($"Found {totalCount} construct objects.");
    }

    // =============================
    public void OnConstructBuilt(ConstructObject obj)
    {
        if (gameFinished) return;

        builtCount++;
        UpdateProgress();

        currentTime += bonusTimePerBuild;
        UpdateTimeText();

        if (progressPercent >= 100f)
        {
            gameFinished = true;
            timerRunning = false;
            WinCondition();
        }
    }

    // =============================
    private void UpdateProgress()
    {
        if (totalCount == 0)
            progressPercent = 0;
        else
            progressPercent = (float)builtCount / totalCount * 100f;

        UpdateProgressUI();
    }

    private void UpdateProgressUI()
    {
        if (progressSlider != null)
            targetSliderValue = progressPercent / 100f;
    }

    // =============================
    private void UpdateTimeText()
    {
        if (timeText == null) return;

        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        timeText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateUI()
    {
        UpdateProgressUI();
        UpdateTimeText();
    }

    // =============================
    private void GameOver()
    {
        Debug.Log("GAME OVER");
    }

    private void WinCondition()
    {
        Debug.Log("YOU WIN!");
    }

    // =============================
    public float GetRemainingTime()
    {
        return currentTime;
    }
}