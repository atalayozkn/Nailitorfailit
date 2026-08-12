using Interactions;
using ItemScript;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Generator_SP : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private InteractableType interactableType;

    public InteractableType InteractableType => interactableType;

    [Header("Generator Settings")]
    [SerializeField, Min(0.1f)]
    private float generatorDurationMinutes = 15f;

    [SerializeField, Min(0.02f)]
    private float tickRate = 0.1f;

    [Header("UI")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject canvasObject;

    [Header("Visual")]
    [SerializeField] private Light generatorLight;
    [SerializeField] private float blinkSpeed = 5f;

    private PlayerInteractionHandler interactionHandler;

    private float currentTime;
    private bool isRunning;

    private Coroutine timerRoutine;
    private Coroutine lightRoutine;

    private int lastDisplayedSecond = -1;

    public bool IsRunning => isRunning;

    private float MaxDurationSeconds => generatorDurationMinutes * 60f;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onInteractEvent;

    private void Awake()
    {
        interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }
    private void Start()
    {
        currentTime = MaxDurationSeconds;
        isRunning = currentTime > 0f;

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
        }

        if (canvasObject != null)
        {
            canvasObject.SetActive(true);
        }

        UpdateUI(true);

        if (isRunning)
        {
            StartTimerRoutine();
            StartLightRoutine();
        }
        else
        {
            SetLightOff();
        }
    }

    #region INTERACTION

    public void OnInteract()
    {
        TryAddCarriedFuel();
    }

    public void OnHoverOn()
    {
        onHoverOnEvent?.Invoke();
    }

    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }

    private void TryAddCarriedFuel()
    {
        if (interactionHandler == null)
        {
            return;
        }

        CarriableObject_SP carriedObject =
            interactionHandler.GetCurrentCarriable();

        if (carriedObject == null)
        {
            Debug.Log(
                "Generator için yakýt taþýman gerekiyor."
            );

            return;
        }

        if (carriedObject.carriableType != CarriableType.Oil)
        {
            Debug.Log(
                "Bu obje Generator yakýtý deðil."
            );

            return;
        }

        if (!carriedObject.TryGetComponent<Oil_SP>(
                out Oil_SP oil))
        {
            Debug.LogWarning(
                "Oil olarak iþaretlenen objede Oil_SP bulunamadý."
            );

            return;
        }

        bool fuelAdded =
            AddFuelPercent(oil.FuelPercent);

        if (!fuelAdded)
        {
            return;
        }

        carriedObject.OnUsed();
    }

    #endregion

    #region FUEL

    public bool AddFuelPercent(float fuelPercent)
    {
        float maxDuration = MaxDurationSeconds;

        if (fuelPercent <= 0f)
        {
            return false;
        }

        if (maxDuration <= 0f)
        {
            return false;
        }

        if (currentTime >= maxDuration)
        {
            Debug.Log("Generator zaten tamamen dolu.");
            return false;
        }

        float clampedPercent = Mathf.Clamp(fuelPercent, 0f, 100f);

        float addedDuration = maxDuration * (clampedPercent / 100f);

        currentTime = Mathf.Min(currentTime + addedDuration, maxDuration);

        if (!isRunning && currentTime > 0f) StartGenerator();
        else UpdateUI();

        return true;
    }

    private void StartGenerator()
    {
        if (isRunning) return;
        if (currentTime <= 0f) return;
        isRunning = true;

        StartTimerRoutine();
        StartLightRoutine();
        UpdateUI(true);
    }

    private void StopGenerator()
    {
        isRunning = false;
        currentTime = 0f;

        StopTimerRoutine();
        StopLightRoutine();

        SetLightOff();
        UpdateUI(true);
    }
    public bool HasPower()
    {
        return isRunning && currentTime > 0f;
    }
    public float GetFuelPercent()
    {
        float maxDuration = MaxDurationSeconds;

        if (maxDuration <= 0f)
        {
            return 0f;
        }

        return currentTime / maxDuration;
    }
    #endregion

    #region TIMER

    private void StartTimerRoutine()
    {
        if (timerRoutine != null)
        {
            return;
        }

        timerRoutine = StartCoroutine(TimerRoutine());
    }

    private IEnumerator TimerRoutine()
    {
        float interval = Mathf.Max(0.02f, tickRate);

        WaitForSeconds wait = new WaitForSeconds(interval);

        while (isRunning && currentTime > 0f)
        {
            yield return wait;

            currentTime -= interval;

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                timerRoutine = null;

                StopGenerator();
                yield break;
            }

            UpdateUI();
        }

        timerRoutine = null;
    }

    private void StopTimerRoutine()
    {
        if (timerRoutine == null)
        {
            return;
        }
        StopCoroutine(timerRoutine);
        timerRoutine = null;
    }

    #endregion

    #region LIGHT
    private void StartLightRoutine()
    {
        if (lightRoutine != null) return;
        lightRoutine = StartCoroutine(LightRoutine());
    }
    private IEnumerator LightRoutine()
    {
        if (generatorLight != null)
        {
            generatorLight.enabled = true;
        }

        while (isRunning)
        {
            if (generatorLight != null)
            {
                generatorLight.intensity =1.5f + Mathf.Sin(Time.time * blinkSpeed) * 0.5f;
            }

            yield return null;
        }

        lightRoutine = null;
    }

    private void StopLightRoutine()
    {
        if (lightRoutine == null)
        {
            return;
        }

        StopCoroutine(lightRoutine);
        lightRoutine = null;
    }

    private void SetLightOff()
    {
        if (generatorLight == null)
        {
            return;
        }

        generatorLight.enabled = false;
        generatorLight.intensity = 0f;
    }

    #endregion

    #region UI

    private void UpdateUI(bool forceTextUpdate = false)
    {
        float maxDuration = MaxDurationSeconds;

        if (progressSlider != null)
        {
            progressSlider.value = maxDuration > 0f
                    ? currentTime / maxDuration
                    : 0f;
        }

        int totalSeconds = Mathf.CeilToInt(currentTime);

        if (!forceTextUpdate && totalSeconds == lastDisplayedSecond)
        {
            return;
        }

        lastDisplayedSecond = totalSeconds;

        if (timerText != null)
        {
            timerText.text = FormatTime(totalSeconds);
        }
    }

    private string FormatTime(float time)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(time));

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{minutes:00}:{seconds:00}";
    }

    #endregion

    private void OnDisable()
    {
        StopTimerRoutine();
        StopLightRoutine();
        SetLightOff();
    }
}