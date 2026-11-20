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
    // Multiplayer sets this (Constructor_MP): damage is decided on the server and every machine applies the same hits in
    // the same order. Returns true when it took over. Left null in singleplayer.
    public static System.Func<ObjectHealth, bool> NetworkDamageRouter;

    // Raised for every hit this machine applies (DealDamageLocal). In multiplayer the stepping machine alone runs the
    // tile's pressure code, but every machine applies the routed hit, so visuals that follow damage (cracks) hook here.
    public event System.Action<ObjectHealth> LocalHit;

    public void DealDamage()
    {
        if (NetworkModeHelper.IsNetworked && NetworkDamageRouter != null && NetworkDamageRouter(this)) return;

        DealDamageLocal();
    }

    // The damage itself (what DealDamage always did).
    public void DealDamageLocal()
    {
        if (isDead) return; // Still burning during the discard delay; dying twice would corrupt the room counter.

        currentHealth--; //Şuanki sistemde 1 hasar vurmak yeterli. Gerekirse Script'i güncelleriz.
        LocalHit?.Invoke(this);

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
    public int MaxHealth => maxHealth;
    public float GetCurrentHealthPercent()
    {
        return (float)currentHealth / maxHealth;
    }
}
