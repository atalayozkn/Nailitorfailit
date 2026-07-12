using System.Collections;
using Interactions;
using UnityEngine;
using UnityEngine.UI;

public class Generator_SP : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType;
    public InteractableType InteractableType => interactableType;
    [Header("Generator Settings")]
    [SerializeField] private float maxDuration = 300f;
    [SerializeField] private float tickRate = 0.1f;

    private float currentTime;
    private bool isRunning;
    public bool IsRunning => isRunning;

    [Header("UI")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject canvasObject;

    [Header("Visual")]
    [SerializeField] private Light generatorLight;
    [SerializeField] private float blinkSpeed = 5f;

    private Coroutine timerRoutine;
    private Coroutine lightRoutine;

    private void Start()
    {
        UpdateUI();
        SetLightOff();
    }

    public void OnInteract()
    {
        if (!isRunning)
        {
            StartGenerator();
        }
    }
    public void OnHoverOn()
    {

    }
    public void OnHoverOff()
    {

    }
    private void StartGenerator()
    {
        if (isRunning) return;

        currentTime = maxDuration;
        isRunning = true;

        UpdateUI();
        StartTimerRoutine();
        StartLightRoutine();

        Debug.Log("Generator started");
    }

    public void RefillGenerator()
    {
        currentTime = maxDuration;

        if (!isRunning)
        {
            isRunning = true;
            StartTimerRoutine();
            StartLightRoutine();
        }

        UpdateUI();

        Debug.Log("Generator refilled");
    }

    private void StartTimerRoutine()
    {
        if (timerRoutine != null)
            StopCoroutine(timerRoutine);

        timerRoutine = StartCoroutine(TimerRoutine());
    }

    private IEnumerator TimerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(tickRate);

        while (isRunning && currentTime > 0f)
        {
            currentTime -= tickRate;

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                StopGenerator();
                yield break;
            }

            UpdateUI();

            yield return wait;
        }

        timerRoutine = null;
    }

    private void StopGenerator()
    {
        isRunning = false;

        StopTimerRoutine();
        StopLightRoutine();

        UpdateUI();
        SetLightOff();

        Debug.Log("Generator stopped");
    }

    private void StopTimerRoutine()
    {
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }
    }

    private void StartLightRoutine()
    {
        if (lightRoutine != null)
            StopCoroutine(lightRoutine);

        lightRoutine = StartCoroutine(LightRoutine());
    }

    private IEnumerator LightRoutine()
    {
        if (generatorLight != null)
            generatorLight.enabled = true;

        while (isRunning)
        {
            if (generatorLight != null)
            {
                generatorLight.intensity =
                    1.5f + Mathf.Sin(Time.time * blinkSpeed) * 0.5f;
            }

            yield return null;
        }

        lightRoutine = null;
    }

    private void StopLightRoutine()
    {
        if (lightRoutine != null)
        {
            StopCoroutine(lightRoutine);
            lightRoutine = null;
        }
    }

    private void UpdateUI()
    {
        if (progressSlider != null)
        {
            float percent = maxDuration > 0f ? currentTime / maxDuration : 0f;
            progressSlider.value = percent;
        }

        if (canvasObject != null)
        {
            canvasObject.SetActive(isRunning);
        }
    }

    private void SetLightOff()
    {
        if (generatorLight != null)
        {
            generatorLight.enabled = false;
        }
    }

    private void OnDisable()
    {
        StopTimerRoutine();
        StopLightRoutine();
        SetLightOff();
    }
}