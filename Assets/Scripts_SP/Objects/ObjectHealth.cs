using ItemScript;
using UnityEngine;
using UnityEngine.Events;

public class ObjectHealth : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ConstructObject_SP connectedConstructor;

    [Header("Health Settings")]
    [SerializeField] private int maxHealth;

    [Header("Death Settings")]
    [SerializeField] private float discardDelay = 2.0f;

    [Header("Events")]
    [SerializeField] private UnityEvent onHitEvent;
    [SerializeField] private UnityEvent onDiscardEvent;

    private int currentHealth;
    private void OnEnable()
    {
        currentHealth = maxHealth; //Anlık can değeri max değere eşitlenir.
    }
    private void OnDisable()
    {
        
    }
    public void DealDamage()
    {
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
        //Burada oynatacağımız eventler için bir delay ekliyoruz. Particle oynattık ama vakit vermedi isek particle da silinecektir.
        currentHealth = 0;
        onDiscardEvent?.Invoke(); 
        Invoke(nameof(DeactivateObject), discardDelay);
    }
    private void DeactivateObject()
    {
        connectedConstructor.RequestClosure(gameObject); //Ölürken kapatıyoruz, Silmiyoruz.
    }
}
