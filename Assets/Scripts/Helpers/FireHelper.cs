using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class FireHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObjectHealth objectHealth;

    [Header("Settings")]
    [SerializeField] private float fireHitInterval = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onFireStarted;
    [SerializeField] private UnityEvent onFireStopped;

    private Coroutine fireRoutine;
    private bool isBurning;

    private void OnEnable()
    {
        isBurning = false;
        if (fireRoutine != null) StopCoroutine(fireRoutine);
        fireRoutine = null;
    }
    private void OnDisable()
    {
        StopFire();
    }
    public void StartFire()
    {
        if (isBurning)
            return;

        isBurning = true;

        onFireStarted?.Invoke();

        fireRoutine = StartCoroutine(FireRoutine());
    }

    public void StopFire()
    {
        if (!isBurning)
            return;

        isBurning = false;

        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
            fireRoutine = null;
        }

        onFireStopped?.Invoke();
    }

    private IEnumerator FireRoutine()
    {
        while (true)
        {
            objectHealth.DealDamage();
            yield return new WaitForSeconds(fireHitInterval);
        }
    }

    public bool IsBurning()
    {
        return isBurning;
    }
}