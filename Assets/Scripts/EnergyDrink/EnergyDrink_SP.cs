using Interactions;
using ItemScript;
using PlayerScripts;
using UnityEngine;
using UnityEngine.Events;

public class EnergyDrink_SP : MonoBehaviour, IUsable
{
    [Header("Settings")]
    [SerializeField, Min(0f)]
    private float energyRestoreAmount = 50f;

    [Header("References")]
    [SerializeField]
    private CarriableObject_SP carriableObject;

    [Header("Events")]
    [SerializeField]
    private UnityEvent onConsumeEvent;

    private PlayerStaminaHandler playerStamina;

    private bool isUsed;

    // Obje oluþturulduðunda çalýþýr.
    // ResolveReferences() çaðýrarak gerekli PlayerStaminaHandler referansýný hazýrlar.
    private void Awake()
    {
        ResolveReferences();
    }

    // Energy Drink kullanýldýðýnda çalýþýr.
    // Daha önce kullanýlmýþsa iþlemi durdurur.
    // Player'a GainEnergy() ile enerji verir.
    // onConsumeEvent'i çalýþtýrýr ve ardýndan CarriableObject_SP.OnUsed() çaðýrýr.
    public void OnUse()
    {
        if (isUsed)
        {
            return;
        }

        if (playerStamina == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerStaminaHandler bulunamadý."
            );

            return;
        }

        if (carriableObject == null)
        {
            Debug.LogWarning(
                $"{name}: CarriableObject_SP atanmadý."
            );

            return;
        }

        isUsed = true;

        playerStamina.GainEnergy(
            energyRestoreAmount
        );

        onConsumeEvent?.Invoke();

        carriableObject.OnUsed();
    }

    // Energy Drink'in ihtiyaç duyduðu PlayerStaminaHandler referansýný hazýrlar.
    // Referans yoksa sahnedeki ilk PlayerStaminaHandler'ý bulup kaydeder.
    private void ResolveReferences()
    {
        if (playerStamina == null)
        {
            playerStamina = FindFirstObjectByType<PlayerStaminaHandler>();
        }
    }
}