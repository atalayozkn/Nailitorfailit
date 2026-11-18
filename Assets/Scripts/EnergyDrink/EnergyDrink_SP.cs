using Interactions;
using ItemScript;
using PlayerScripts;
using UnityEngine;
using UnityEngine.Events;

public class EnergyDrink_SP : MonoBehaviour, IUsable
{
    [Header("Settings")]
    [SerializeField, Min(0f)] private float energyRestoreAmount = 500f;

    [Header("References")]
    [SerializeField] private CarriableObject_SP carriableObject;
    public UseType UseType => UseType.EnergyDrink;

    [Header("Events")]
    [SerializeField] private UnityEvent onUseEvent;

    
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

        // The player who drinks is the one holding the can. FindFirstObjectByType (Awake) is fine with one player, but
        // in multiplayer it picked an arbitrary player's stamina: the drinker got no buff and no stamina refill.
        PlayerStaminaHandler holder = GetComponentInParent<PlayerStaminaHandler>(true);
        if (holder != null) playerStamina = holder;
        if (playerStamina == null) playerStamina = FindFirstObjectByType<PlayerStaminaHandler>();

        VfxManager.Play(VfxId.PlayerDrink, playerStamina.transform.position);
        playerStamina.GainEnergy(energyRestoreAmount);
        onUseEvent?.Invoke();
        // Multiplayer: the server removes the can for everybody (a local OnConsume left the server's copy alive, and
        // every machine kept "healing" a can its drinker had already thrown away).
        if (NetworkModeHelper.IsNetworked && carriableObject.TryGetComponent(out CarriableObject_MP networked))
        {
            PlayerInteractionHandler holderHandler = GetComponentInParent<PlayerInteractionHandler>();
            networked.RequestConsume(holderHandler != null ? holderHandler.GetComponentInParent<Mirror.NetworkIdentity>() : null);
            return;
        }

        carriableObject.OnConsume();
    }

}