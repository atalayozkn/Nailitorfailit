using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class CurrencyText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textComponent;

    [SerializeField] private UnityEvent onGainEvent;
    [SerializeField] private UnityEvent onSpendEvent;
    [SerializeField] private UnityEvent onRejectEvent;

    public void UpdateUI(int amount)
    {
        textComponent.text = amount.ToString();
    }
    public void Gain()
    {
        onGainEvent?.Invoke();
    }
    public void Spend()
    {
        onSpendEvent?.Invoke();
    }
    public void Reject()
    {
        onRejectEvent?.Invoke();
    }
}
 