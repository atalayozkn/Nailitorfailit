using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RainController_Prototype : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PuddleHelper[] outDoorPuddles;

    [Header("Settings")]
    [SerializeField] private float maxRainDuration;
    [SerializeField] private float minRainDuration;
    [SerializeField] private float minInterval;
    [SerializeField] private float maxInterval;

    [Header("Events")]
    [SerializeField] private UnityEvent onRainStartEvent;
    [SerializeField] private UnityEvent onRainStopEvent;

    private List<PuddleHelper> availablePuddles = new();

    private float currentInterval;

    public void StartRainProcess()
    {
        StartCoroutine(RainRoutine());
    }

    private IEnumerator RainRoutine()
    {
        ResetPuddleList();
        float rainDuration = Random.Range(minRainDuration, maxRainDuration);
        float elapsedTime = 0f;
        onRainStartEvent?.Invoke();

        while (elapsedTime < rainDuration)
        {
            ActivateRandomPuddle();
            currentInterval = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(currentInterval);
            elapsedTime += currentInterval;
        }

        onRainStopEvent?.Invoke();
        availablePuddles.Clear();
    }

    private void ActivateRandomPuddle()
    {
        if (availablePuddles.Count == 0) return;

        int index = Random.Range(0, availablePuddles.Count);
        PuddleHelper puddle = availablePuddles[index];
        availablePuddles.RemoveAt(index);
        puddle.StartPuddleProcess();
    }

    private void ResetPuddleList()
    {
        availablePuddles.Clear();

        foreach (PuddleHelper puddle in outDoorPuddles)
        {
            if (puddle != null) availablePuddles.Add(puddle);
        }
    }
}