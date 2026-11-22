using System.Collections;
using UnityEngine;

/// <summary>
/// Periodically slides a UI element out from a hidden position, waits, and slides back.
/// Place it under a RectMask2D so the hidden part is clipped (e.g. a dog peeking from behind a post).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIPeek : MonoBehaviour
{
    [Tooltip("Offset from the shown position where the element is fully hidden.")]
    [SerializeField] private Vector2 hiddenOffset = new Vector2(-320f, 0f);
    [SerializeField] private float slideDuration = 0.45f;
    [SerializeField] private Vector2 visibleTime = new Vector2(1.5f, 3f);
    [SerializeField] private Vector2 hiddenTime = new Vector2(4f, 9f);
    [SerializeField] private float firstDelay = 2.5f;
    [Tooltip("Small tilt while peeking, in degrees.")]
    [SerializeField] private float peekTilt = -6f;

    private RectTransform rect;
    private Vector2 shownPosition;
    private Coroutine routine;

    private void Awake()
    {
        rect = (RectTransform)transform;
        shownPosition = rect.anchoredPosition;
    }

    private void OnEnable()
    {
        rect.anchoredPosition = shownPosition + hiddenOffset;
        rect.localRotation = Quaternion.identity;
        routine = StartCoroutine(Loop());
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSecondsRealtime(firstDelay);
        while (true)
        {
            yield return Slide(0f, 1f);
            yield return new WaitForSecondsRealtime(Random.Range(visibleTime.x, visibleTime.y));
            yield return Slide(1f, 0f);
            yield return new WaitForSecondsRealtime(Random.Range(hiddenTime.x, hiddenTime.y));
        }
    }

    // amount 0 = hidden, 1 = shown
    private IEnumerator Slide(float from, float to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, slideDuration));
            float e = to > from ? MenuEase.OutBack(t) : MenuEase.InCubic(t);
            Apply(Mathf.LerpUnclamped(from, to, e));
            yield return null;
        }
        Apply(to);
    }

    private void Apply(float amount)
    {
        rect.anchoredPosition = shownPosition + hiddenOffset * (1f - amount);
        rect.localRotation = Quaternion.Euler(0f, 0f, peekTilt * Mathf.Clamp01(amount));
    }
}
