using ItemScript;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class ObjectHealth : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Constructor connectedConstructor;
    [SerializeField] private ConstructableObjectData objectData;

    [Header("Death Settings")]
    [SerializeField] private float discardDelay = 2.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onHitEvent;
    [SerializeField] private UnityEvent onDiscardEvent;

    private int maxHealth;
    private int currentHealth;
    private bool isDead;
    private void OnEnable()
    {
        isDead = false;
        maxHealth = objectData.objectHealth;
        currentHealth = maxHealth; //Anlık can değeri max değere eşitlenir.
    }
    private void OnDisable()
    {
        
    }
    public void DealDamage()
    {
        if (isDead) return; // Still burning during the discard delay; dying twice would corrupt the room counter.

        currentHealth--; //Şuanki sistemde 1 hasar vurmak yeterli. Gerekirse Script'i güncelleriz.

        if (currentHealth <= 0) //Öldüyse vurma efektini kullanmadan çıkıyoruz ve ölme efektini oynuyoruz.
        {
            Die();
            return;
        }

        onHitEvent?.Invoke(); //Ölmedi ve buraya geldi ise Vurma efekti oynatılıyor.
    }
    private void Die()
    {
        isDead = true;
        PlayDestroyedEffect();
        //Burada oynatacağımız eventler için bir delay ekliyoruz. Particle oynattık ama vakit vermedi isek particle da silinecektir.
        currentHealth = 0;
        onDiscardEvent?.Invoke(); 
        Invoke(nameof(DeactivateObject), discardDelay);
    }
    private void PlayDestroyedEffect()
    {
        VfxId id = VfxId.TileDestroyedWood;
        if (TryGetComponent<BrickTile>(out _)) id = VfxId.TileDestroyedBrick;
        else if (TryGetComponent<GlassTile>(out _)) id = VfxId.TileDestroyedGlass;
        VfxManager.Play(id, transform.position);
    }
    private void DeactivateObject()
    {
        connectedConstructor.RequestClosure(gameObject); //Ölürken kapatıyoruz, Silmiyoruz.
    }
    public float GetCurrentHealthPercent()
    {
        return (float)currentHealth / maxHealth;
    }
}
