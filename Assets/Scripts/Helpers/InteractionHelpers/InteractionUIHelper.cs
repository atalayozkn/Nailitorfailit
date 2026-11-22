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
    // The bar glides up to the new value instead of jumping in one step per interaction (it never glides down:
    // a reset or a lower value shows at once).
    private const float GlideFractionPerSecond = 2.5f;
    private float targetValue;

    public void UpdateUI(float value)
    {
        targetValue = value;
        if (value <= sliderComponent.value) sliderComponent.value = value;
    }

    private void Update()
    {
        if (sliderComponent == null || sliderComponent.value >= targetValue) return;

        float step = Mathf.Max(0.0001f, sliderComponent.maxValue - sliderComponent.minValue) * GlideFractionPerSecond * Time.unscaledDeltaTime;
        sliderComponent.value = Mathf.MoveTowards(sliderComponent.value, targetValue, step);
    }

}
