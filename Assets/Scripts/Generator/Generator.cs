using System.Collections;
using UnityEngine;
using Mirror;
using Interactions;
using ItemScript;

public class Generator : NetworkBehaviour, IInteractable
{
    [Header("Generator Settings")]
    [SerializeField] private float maxDuration = 300f;
    [SerializeField] private float tickRate = 0.1f;

    [SyncVar(hook = nameof(OnCurrentTimeChanged))]
    private float currentTime;

    [SyncVar(hook = nameof(OnRunningChanged))]
    private bool isRunning;

    public bool IsRunning => isRunning;

    [Header("UI")]
    [SerializeField] private UnityEngine.UI.Slider progressSlider;
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

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (isRunning && timerRoutine == null)
        {
            timerRoutine = StartCoroutine(ServerTimerRoutine());
        }
    }

    public void Interact()
    {
        if (!isRunning)
        {
            CmdStartGenerator();
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdStartGenerator()
    {
        if (isRunning) return;

        currentTime = maxDuration;
        isRunning = true;

        if (timerRoutine != null)
            StopCoroutine(timerRoutine);

        timerRoutine = StartCoroutine(ServerTimerRoutine());

        Debug.Log("Generator started");
    }

    [Command(requiresAuthority = false)]
    private void CmdRefillGenerator()
    {
        currentTime = maxDuration;

        if (!isRunning)
        {
            isRunning = true;

            if (timerRoutine != null)
                StopCoroutine(timerRoutine);

            timerRoutine = StartCoroutine(ServerTimerRoutine());
        }

        Debug.Log("Generator refilled");
    }

    private IEnumerator ServerTimerRoutine()
    {
        while (isRunning && currentTime > 0f)
        {
            yield return new WaitForSeconds(tickRate);

            currentTime -= tickRate;

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                isRunning = false;
                break;
            }
        }

        timerRoutine = null;
    }

    private void OnCurrentTimeChanged(float oldValue, float newValue)
    {
        UpdateUI();
    }

    private void OnRunningChanged(bool oldValue, bool newValue)
    {
        UpdateUI();

        if (newValue)
        {
            StartLightBlink();
        }
        else
        {
            StopLightBlink();
            SetLightOff();
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

    private void StartLightBlink()
    {
        if (lightRoutine != null)
            StopCoroutine(lightRoutine);

        lightRoutine = StartCoroutine(LightBlinkRoutine());
    }

    private void StopLightBlink()
    {
        if (lightRoutine != null)
        {
            StopCoroutine(lightRoutine);
            lightRoutine = null;
        }
    }

    private IEnumerator LightBlinkRoutine()
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
    }

    private void SetLightOff()
    {
        if (generatorLight != null)
        {
            generatorLight.enabled = false;
        }
    }
}