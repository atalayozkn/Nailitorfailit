using UnityEngine;
using UnityEngine.UI;

public class UISliderHelper : MonoBehaviour
{
    [SerializeField] private Slider sliderComponent;
    public void SetMaxValue(float amount)
    {
        sliderComponent.maxValue = amount;
    }
    public void UpdateSlider(float amount)
    {
        sliderComponent.value = amount;
    }
}
