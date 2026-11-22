using System.Collections;
using UnityEngine;

/// <summary>
/// One-shot entrance animation when enabled: fades in (CanvasGroup) and moves from an offset
/// to the element's original position. Use different delays for a staggered intro.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIIntro : MonoBehaviour
{
    public enum EaseType { OutCubic, OutBack, OutBounce }

    [SerializeField] private float delay = 0f;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private Vector2 fromOffset = Vector2.zero;
    [SerializeField] private bool fade = true;
    [SerializeField] private EaseType ease = EaseType.OutCubic;
    [SerializeField] private bool playOnlyOnce = true;

    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 basePosition;
    private bool played;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
        if (fade)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        if (playOnlyOnce && played) return;
        StartCoroutine(Play());
    }

    private void OnDisable()
    {
        // Snap to the final state if disabled mid-animation.
        if (rect != null) rect.anchoredPosition = basePosition;
        if (group != null) group.alpha = 1f;
    }

    private IEnumerator Play()
    {
        played = true;
        Apply(0f, 0f);
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
            Apply(Evaluate(t), Mathf.Clamp01(t * 2f));
            yield return null;
        }
        Apply(1f, 1f);
    }

    private void Apply(float move, float alpha)
    {
        rect.anchoredPosition = basePosition + fromOffset * (1f - move);
        if (group != null) group.alpha = fade ? alpha : 1f;
    }

    private float Evaluate(float t)
    {
        switch (ease)
        {
            case EaseType.OutBack: return MenuEase.OutBack(t);
            case EaseType.OutBounce: return MenuEase.OutBounce(t);
            default: return MenuEase.OutCubic(t);
        }
    }
}
