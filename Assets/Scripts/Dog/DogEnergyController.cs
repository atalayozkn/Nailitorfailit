using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DogEnergyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider slider;

    [Header("Settings")]
    [SerializeField] private float maxEnergy;
    [SerializeField] private float decayInterval;

    [SerializeField] private UnityEvent onDepletedEvent;

    private float currentEnergy;
    private Coroutine energyDecayRoutine;
    private void OnEnable()
    {
        currentEnergy = maxEnergy;
        UpdateUI();
        slider.maxValue = maxEnergy;
        if (energyDecayRoutine != null)
        {
            StopCoroutine(energyDecayRoutine);
            energyDecayRoutine = null;
        }

        energyDecayRoutine = StartCoroutine(DecayTick());
    }
    private void OnDisable()
    {
        StopCoroutine(energyDecayRoutine);
        energyDecayRoutine = null;
    }
    private IEnumerator DecayTick()
    {
        while (true)
        {
            currentEnergy--;
            UpdateUI();
            if (currentEnergy < 10f)
            {
                onDepletedEvent.Invoke();
            }
            yield return new WaitForSeconds(decayInterval);
        }
    }
    private void UpdateUI()
    {
        slider.value = currentEnergy;
    }
    public void GainEnergy(float amount)
    {
        currentEnergy += amount;
        UpdateUI();
    }
    public float GetPercentEnergy()
    {
        float percentFavor = (currentEnergy / maxEnergy) * 100f;
        return percentFavor;
    }
}
