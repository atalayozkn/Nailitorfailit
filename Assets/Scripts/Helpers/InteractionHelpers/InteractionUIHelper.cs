using UnityEngine;
using UnityEngine.UI;

public class InteractionUIHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvasComponent;
    [SerializeField] private Slider sliderComponent;
    // No OnEnable: showing the bar is up to InteractionProcessHelper. Hiding it here as well raced with that
    // on enable, and on WallConstruct the hide ran last, so wall tiles never showed their bar.
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
