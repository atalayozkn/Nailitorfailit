using PlayerScripts;
using UnityEngine;
using UnityEngine.Events;

public class EnergyDrink_SP : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float energyRestoreAmount = 50f;
    [SerializeField] private float objectDestroyDelay = 3f;

    [Header("Events")]
    [SerializeField] private UnityEvent onConsumeEvent;

    private PlayerStamina_SP playerStamina;

    private void Awake()
    {
        playerStamina = FindAnyObjectByType<PlayerStamina_SP>();
    }
    public void Interact()
    {
        playerStamina?.GainEnergy(energyRestoreAmount);
        onConsumeEvent?.Invoke();
        Destroy(gameObject, objectDestroyDelay);
    }
}
