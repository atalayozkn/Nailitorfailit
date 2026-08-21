using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class HazardController_Prototype : MonoBehaviour
{
    [SerializeField] private UnityEvent[] availableHazards;
    [SerializeField] private float maxHazardInterval;
    [SerializeField] private float minHazardInterval;
    [SerializeField] private float initialDelay;

    private void Start()
    {
        Invoke(nameof(StartHazardRoutine), initialDelay);
    }

    private void StartHazardRoutine()
    {
        StartCoroutine(HazardRoutine());
    }
    private IEnumerator HazardRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(minHazardInterval, maxHazardInterval);

            if (availableHazards.Length == 0) continue;

            int randomIndex = Random.Range(0, availableHazards.Length);
            availableHazards[randomIndex]?.Invoke();

            yield return new WaitForSeconds(waitTime);
        }
    }
}