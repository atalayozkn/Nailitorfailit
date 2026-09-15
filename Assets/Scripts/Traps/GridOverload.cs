using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class GridOverload : MonoBehaviour
{
    [Header("Main Settings")]
    [SerializeField] private int maxGridLoad = 100;
    [SerializeField] private int perConsumeAmount = 10;
    [SerializeField] private float decayInterval = 1.0f;

    [Header("Overload Settings")]
    [SerializeField] private int overloadAmount = 85;
    [SerializeField] private int cooldownAmount = 15;

    [Header("Events")]
    [SerializeField] private UnityEvent onOverload;
    [SerializeField] private UnityEvent onCooldown;

    private int currentGridLoad;
    private WaitForSeconds decayTimer;
    private bool isOverloaded = false;
    public static GridOverload Instance { get; private set; } //Instanced (Singleton)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    private void OnEnable()
    {
        currentGridLoad = maxGridLoad;

        decayTimer = new WaitForSeconds(decayInterval);
        StartCoroutine(DecayRoutine());
    }
    public void HandleConsumption()
    {
        currentGridLoad++;
        if (currentGridLoad > maxGridLoad) currentGridLoad = maxGridLoad;
        if (currentGridLoad >= overloadAmount) Overload();
    }
    private IEnumerator DecayRoutine()
    {
        while (true)
        {
            currentGridLoad--;
            if (currentGridLoad <= 0) currentGridLoad = 0;
            if (currentGridLoad <= cooldownAmount && isOverloaded) Cooldown(); 
            yield return decayTimer;
        }
    }
    private void Overload()
    {
        isOverloaded = true;
        onOverload?.Invoke();
    }
    private void Cooldown()
    {
        isOverloaded = false;
        onCooldown?.Invoke();
    }
    public bool IsOverloaded()
    {
        return isOverloaded;
    }
}
