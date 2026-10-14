using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FavorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider slider;

    [Header("Settings")]
    [SerializeField] private int maxFavor = 100;
    [SerializeField] private float hungerInterval = 2.5f;
    private int currentFavor;
    private Coroutine loveHungerRoutine;

    private void OnEnable()
    {
        currentFavor = maxFavor;
        UpdateUI();
        slider.maxValue = maxFavor;

        if (loveHungerRoutine != null)
        {
            StopCoroutine(loveHungerRoutine);
            loveHungerRoutine = null;
        }

        loveHungerRoutine = StartCoroutine(LoveHungerTick());
    }
    private void OnDisable()
    {
        StopCoroutine(loveHungerRoutine);
        loveHungerRoutine = null;
    }
    private IEnumerator LoveHungerTick()
    {
        while (true)
        {
            currentFavor--;
            UpdateUI();
            yield return new WaitForSeconds(hungerInterval);
        }
    }
    private void UpdateUI()
    {
        slider.value = currentFavor;
    }
    public void GainFavor(int amount)
    {
        currentFavor += amount;
        UpdateUI();
    }
    public float GetPercentFavor()
    {
        float percentFavor = ((float)currentFavor / maxFavor) * 100f;
        return percentFavor;
    }
}
