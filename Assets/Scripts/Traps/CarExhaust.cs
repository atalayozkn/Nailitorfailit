using UnityEngine;

// Pooled cars are toggled on and off: drop the old trail and start fresh each spawn.
public class CarExhaust : MonoBehaviour
{
    [SerializeField] private ParticleSystem exhaust;

    private void Awake()
    {
        if (exhaust == null) exhaust = GetComponentInChildren<ParticleSystem>(true);
    }

    private void OnEnable()
    {
        if (exhaust == null) return;
        exhaust.Clear(true);
        exhaust.Play(true);
    }

    private void OnDisable()
    {
        if (exhaust == null) return;
        exhaust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
