using UnityEngine;
using UnityEngine.UI;

public class UIImageFillHelper : MonoBehaviour
{
    [SerializeField] private Image imageComponent;
    public void UpdateUI(float amount)
    {
        imageComponent.fillAmount = amount;
    }
}
