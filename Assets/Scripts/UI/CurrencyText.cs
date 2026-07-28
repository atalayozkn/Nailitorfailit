using TMPro;
using UnityEngine;

public class CurrencyText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textComponent;
    public void UpdateUI(int amount)
    {
        textComponent.text = amount.ToString();
    }
}
 