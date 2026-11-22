using UnityEngine;

/// <summary>
/// Moves a UI element horizontally at a constant speed and wraps it around
/// its parent's bounds (for clouds). Adds a gentle vertical bob.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIDrift : MonoBehaviour
{
    [Tooltip("Canvas units per second. Negative drifts left.")]
    [SerializeField] private float speed = 30f;
    [SerializeField] private float bobAmplitude = 6f;
    [SerializeField] private float bobFrequency = 0.15f;
    [Tooltip("Extra distance outside the parent before wrapping.")]
    [SerializeField] private float wrapPadding = 50f;

    private RectTransform rect;
    private RectTransform parent;
    private float baseY;
    private float phase;

    private void Awake()
    {
        rect = (RectTransform)transform;
        parent = rect.parent as RectTransform;
        baseY = rect.localPosition.y;
        phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        if (parent == null) return;

        Vector3 p = rect.localPosition;
        p.x += speed * Time.unscaledDeltaTime;
        p.y = baseY + Mathf.Sin(Time.unscaledTime * bobFrequency * Mathf.PI * 2f + phase) * bobAmplitude;

        Rect bounds = parent.rect;
        float halfWidth = rect.rect.width * Mathf.Abs(rect.localScale.x) * 0.5f;
        float minX = bounds.xMin - halfWidth - wrapPadding;
        float maxX = bounds.xMax + halfWidth + wrapPadding;
        if (speed > 0f && p.x > maxX) p.x = minX;
        else if (speed < 0f && p.x < minX) p.x = maxX;

        rect.localPosition = p;
    }
}
