using UnityEngine;
using UnityEngine.Events;

public class Glass : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 1f;
    [SerializeField] private float discardDelay = 0.1f;

    [Header("Pressure Settings")]
    [SerializeField] private float perPressureDmg = 0.1f;

    [Header("Visual Settings")]
    [SerializeField] private float perPressureParameterChange = 5f;
    [SerializeField] private float initialParameterValue = 1f;
    [SerializeField] private MeshRenderer mRenderer;
    [SerializeField] private string parameterName;
    private Material instancedMaterial;

    [Header("Events")]
    [SerializeField] private UnityEvent onHitEvent;
    [SerializeField] private UnityEvent onDestroyEvent;

    private float currentHealth;
    private float currentParameterValue;
    private bool isBroken = false;

    private void OnEnable()
    {
        currentHealth = maxHealth;
        isBroken = false;
    }
    public void ApplyPressure()
    {
        if (isBroken) return;
        if (currentHealth - perPressureDmg <= 0f)
        {
            currentHealth = 0f;
            isBroken = true;
            onDestroyEvent?.Invoke();
            Destroy(gameObject, discardDelay);
            return;
        }
        currentHealth -= perPressureDmg;
        onHitEvent?.Invoke();
    }
    private void UpdateVisual()
    {
        currentParameterValue -= perPressureParameterChange;
        instancedMaterial.SetFloat(parameterName, currentParameterValue);
    }
}