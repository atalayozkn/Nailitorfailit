using UnityEngine;
using UnityEngine.UI;

public class InteractionUIHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvasComponent;
    [SerializeField] private Slider sliderComponent;
    private void OnEnable()
    {
        SetActivity(true);
        UpdateUI(0f);
    }
    private void OnDisable()
    {
        SetActivity(false);
    }
    public void SetActivity(bool condition)
    {
        canvasComponent.enabled = condition;
        sliderComponent.enabled = condition;
    }
    public void SetUIProperties(float maxValue)
    {
        sliderComponent.maxValue = maxValue;
    }
    public void UpdateUI(float value)
    {
        sliderComponent.value = value;
    }

}
