using Flammables;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;
public class FireExtinguisher : MonoBehaviour, IUsable
{
    [Header("References")]
    [SerializeField] CarriableObject_SP carriable;
    [Header("Fuel Settings")]
    [SerializeField] private int maxFuel = 100;
    [SerializeField] private int perUseConsumption = 10;
    [Header("Detection Settings")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Vector3 halfDimensions;
    [SerializeField] private Transform muzzle;
    [Header("Debug Settings")]
    [SerializeField] private bool debugMode;
    public UseType UseType => UseType.FireExtinguisher;
    [SerializeField] private UnityEvent onUseEvent;

    private int currentFuel;
    private bool hasUsed;
    private void OnEnable()
    {
        currentFuel = maxFuel;
        hasUsed = false;
        UpdateUI();
    }
    public void OnUse()
    {
        if (hasUsed) return;

        currentFuel -= perUseConsumption;
        TryStopFire();
        if (currentFuel < 0)
        {
            currentFuel = 0;
            hasUsed = true;
            carriable.OnConsume();
            Invoke(nameof(Die), 0.1f);
        }

        UpdateUI();
        onUseEvent?.Invoke();
    }
    private void Die()
    {
        Destroy(gameObject);
    }
    private void TryStopFire()
    {
        Collider[] cols = Physics.OverlapBox(muzzle.position, halfDimensions, muzzle.rotation, targetLayer);
        if (cols.Length == 0) return;

        foreach (var col in cols)
        {
            if (col.TryGetComponent<IFlammable>(out IFlammable flammable)) flammable.OnFireStop();
        }
    }
    private void UpdateUI()
    {

    }
    private void OnDrawGizmosSelected()
    {
        if (!debugMode) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(muzzle.position, halfDimensions);
    }
}
