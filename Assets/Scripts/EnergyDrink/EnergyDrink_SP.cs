using Interactions;
using ItemScript;
using PlayerScripts;
using UnityEngine;
using UnityEngine.Events;

public class EnergyDrink_SP : MonoBehaviour, IUsable
{
    [Header("Settings")]
    [SerializeField, Min(0f)]
    private float energyRestoreAmount = 500f;

    [Header("References")]
    [SerializeField]
    private CarriableObject_SP carriableObject;

    [Header("Events")]
    [SerializeField]
    private UnityEvent onUseEvent;

    private PlayerStaminaHandler playerStamina;

    private bool isUsed;

    private void Awake()
    {
        playerStamina = FindFirstObjectByType<PlayerStaminaHandler>();
    }
    public void OnUse()
    {
        if (isUsed) return;
        isUsed = true;

        playerStamina.GainEnergy(energyRestoreAmount);
        onUseEvent?.Invoke();
        carriableObject.OnConsume();
    }

}