using UnityEngine;

/// <summary>
/// Gentle automatic left-right sway for a UI background layer (no mouse input).
/// Every layer follows the same shared timeline, so they move in sync and only the
/// strength differs: distant layers use a small strength, near layers a larger one.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIParallaxLayer : MonoBehaviour
{
    [Tooltip("Maximum offset in canvas units at the far end of the sway.")]
    [SerializeField] private Vector2 strength = new Vector2(20f, 10f);

    // Shared sway timeline (same for all layers so the depth effect stays consistent).
    private const float SwayPeriod = 14f;          // seconds for one full left-right-left cycle
    private const float VerticalPeriod = 23f;      // slower, out of step with the horizontal sway
    private const float VerticalAmount = 0.35f;    // vertical motion relative to horizontal

    private RectTransform rect;
    private Vector2 basePosition;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
    }

    private void OnDisable()
    {
        if (rect != null) rect.anchoredPosition = basePosition;
    }

    private void Update()
    {
        rect.anchoredPosition = basePosition + Vector2.Scale(SharedSway(), strength);
    }

    /// <summary>Shared sway direction in -1..1; starts centred at time 0.</summary>
    private static Vector2 SharedSway()
    {
        float t = Time.unscaledTime;
        float x = Mathf.Sin(t * Mathf.PI * 2f / SwayPeriod);
        float y = Mathf.Sin(t * Mathf.PI * 2f / VerticalPeriod) * VerticalAmount;
        return new Vector2(x, y);
    }
}
