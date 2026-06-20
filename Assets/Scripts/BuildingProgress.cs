using System.Collections;
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
    [SerializeField] private float timerTickRate = 0.1f;

    [Header("UI References")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Smooth Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float sliderTickRate = 0.02f;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float playerDetectRate = 0.25f;

    [Header("Debug")]
    [SerializeField] private float progressPercent;

    private float currentTime;
    private bool timerRunning = false;
    private bool gameFinished = false;
    private bool timerStartedByPlayer = false;

    private float targetSliderValue = 0f;

    [SerializeField] private List<ConstructObject> constructs = new List<ConstructObject>();

    private int totalCount;
    private int builtCount;

    public float ProgressPercent => progressPercent;

    private Coroutine playerDetectRoutine;
    private Coroutine timerRoutine;
    private Coroutine sliderRoutine;

    private void Awake()
    {
        FindAllConstructs();

        currentTime = startTime;
        UpdateUI();
    }

    private void Start()
    {
        playerDetectRoutine = StartCoroutine(PlayerDetectRoutine());
        sliderRoutine = StartCoroutine(SliderSmoothRoutine());
    }

    private IEnumerator PlayerDetectRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(playerDetectRate);

        while (!timerStartedByPlayer && !gameFinished)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);

            if (player != null)
            {
                timerStartedByPlayer = true;
                StartTimer();

                Debug.Log("Player detected → Timer started");
                yield break;
            }

            yield return wait;
        }

        playerDetectRoutine = null;
    }

    public void StartTimer()
    {
        if (timerRunning || gameFinished) return;

        timerRunning = true;

        if (timerRoutine == null)
            timerRoutine = StartCoroutine(TimerRoutine());
    }

    private IEnumerator TimerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(timerTickRate);

        while (timerRunning && !gameFinished)
        {
            currentTime -= timerTickRate;

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                timerRunning = false;
                gameFinished = true;

                UpdateTimeText();
                GameOver();

                timerRoutine = null;
                yield break;
            }

            UpdateTimeText();

            yield return wait;
        }

        timerRoutine = null;
    }

    private IEnumerator SliderSmoothRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(sliderTickRate);

        while (!gameFinished)
        {
            UpdateSliderSmooth();
            yield return wait;
        }

        sliderRoutine = null;
    }

    private void UpdateSliderSmooth()
    {
        if (progressSlider == null) return;

        progressSlider.value = Mathf.Lerp(
            progressSlider.value,
            targetSliderValue,
            sliderTickRate * smoothSpeed
        );
    }

    private void FindAllConstructs()
    {
        constructs.Clear();
        builtCount = 0;

        GameObject[] objs = GameObject.FindGameObjectsWithTag("WallOrFloor");

        foreach (GameObject obj in objs)
        {
            ConstructObject construct = obj.GetComponent<ConstructObject>();
            if (construct == null) continue;

            constructs.Add(construct);

            if (construct.IsBuilt)
                builtCount++;

            construct.OnBuilt -= OnConstructBuilt;
            construct.OnBuilt += OnConstructBuilt;
        }

        totalCount = constructs.Count;
        UpdateProgress();

        Debug.Log($"Found {totalCount} construct objects.");
    }

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

            StopAllRunningCoroutines();

            WinCondition();
        }
    }

    private void UpdateProgress()
    {
        if (totalCount == 0)
            progressPercent = 0f;
        else
            progressPercent = (float)builtCount / totalCount * 100f;

        UpdateProgressUI();
    }

    private void UpdateProgressUI()
    {
        if (progressSlider != null)
            targetSliderValue = progressPercent / 100f;
    }

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

    private void StopAllRunningCoroutines()
    {
        if (playerDetectRoutine != null)
        {
            StopCoroutine(playerDetectRoutine);
            playerDetectRoutine = null;
        }

        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }

        if (sliderRoutine != null)
        {
            StopCoroutine(sliderRoutine);
            sliderRoutine = null;
        }
    }

    private void GameOver()
    {
        StopAllRunningCoroutines();
        Debug.Log("GAME OVER");
    }

    private void WinCondition()
    {
        Debug.Log("YOU WIN!");
    }

    public float GetRemainingTime()
    {
        return currentTime;
    }
}