using Interactions;
using ItemScript;
using PlayerScripts;
using UnityEngine;
using UnityEngine.Events;

public class EnergyDrink_SP : MonoBehaviour, IUsable
{
    [Header("Settings")]
    [SerializeField] private float energyRestoreAmount = 50f;

    [Header("Events")]
    [SerializeField] private UnityEvent onConsumeEvent;

    private PlayerStaminaHandler playerStamina;
    [SerializeField] private CarriableObject_SP carriableObject;

    private bool isUsed;

    private void Awake()
    {
        playerStamina = FindFirstObjectByType<PlayerStaminaHandler>();
    }

    public void OnUse()
    {
        if (isUsed) return;

        isUsed = true;
        playerStamina?.GainEnergy(energyRestoreAmount);
        onConsumeEvent?.Invoke();
        carriableObject?.OnUsed();
    }
}