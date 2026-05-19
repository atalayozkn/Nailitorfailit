using Interactions;
using UnityEngine;
using UnityEngine.UI;

public class Generator_SP : MonoBehaviour, IInteractable
{
    [Header("Generator Settings")]
    [SerializeField] private float maxDuration = 300f;

    private float currentTime;
    private bool isRunning;
    public bool IsRunning => isRunning;

    [Header("UI")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject canvasObject;

    [Header("Visual")]
    [SerializeField] private Light generatorLight;
    [SerializeField] private float blinkSpeed = 5f;

    private void Update()
    {
        if (isRunning)
        {
            currentTime -= Time.deltaTime;

            if (currentTime <= 0f)
            {
                currentTime = 0f;
                isRunning = false;
            }
        }

        UpdateUI();
        UpdateLight();
    }

    public void Interact()
    {
        if (!isRunning)
        {
            StartGenerator();
        }
    }

    private void UpdateUI()
    {
        if (progressSlider != null)
        {
            float percent = currentTime / maxDuration;
            progressSlider.value = percent;
        }

        if (canvasObject != null)
        {
            canvasObject.SetActive(isRunning);
        }
    }

    private void UpdateLight()
    {
        if (generatorLight == null) return;

        if (isRunning)
        {
            generatorLight.enabled = true;
            generatorLight.intensity = 1.5f + Mathf.Sin(Time.time * blinkSpeed) * 0.5f;
        }
        else
        {
            generatorLight.enabled = false;
        }
    }

    private void StartGenerator()
    {
        if (isRunning) return;

        currentTime = maxDuration;
        isRunning = true;

        Debug.Log("Generator started");
    }

    public void RefillGenerator()
    {
        currentTime = maxDuration;
        isRunning = true;

        Debug.Log("Generator refilled");
    }
}
