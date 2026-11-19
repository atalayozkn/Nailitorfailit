using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Loading screen shown between scenes by GameSceneManager, above the black of ScreenFader.
// A trail of paw prints walks across the screen and three paws pop in after "Loading" instead of dots.
// Built in code and lives for the whole session, so no scene or prefab has to carry it.
// Everything runs on unscaled time (leaving a level from the pause menu keeps timeScale at 0 until the load ends).
public class LoadingScreen : MonoBehaviour
{
    private const string PawPath = "UI/Paw_White";
    private const float FadeInTime = 0.25f;
    private const float FadeOutTime = 0.35f;
    private const float MinVisibleTime = 0.9f;

    private const int TrailSteps = 8;
    private const float StepInterval = 0.16f;
    private const float StepLife = 1.1f;
    private const float StepSpacing = 78f;
    private const float StepSide = 22f;

    private static readonly Color Background = new Color(0.18f, 0.12f, 0.08f, 1f);
    private static readonly Color Cream = new Color(0.99f, 0.90f, 0.71f, 1f);
    private static readonly Color PawTint = new Color(0.98f, 0.74f, 0.20f, 1f);

    private static LoadingScreen instance;

    private CanvasGroup group;
    private RectTransform[] trail;
    private Image[] trailImages;
    private RectTransform[] dots;
    private float shownAt;
    private float clock;
    private bool visible;
    private Coroutine fade;

    // True once the screen has been up long enough not to flash.
    public static bool MinTimeElapsed => instance == null || Time.unscaledTime - instance.shownAt >= MinVisibleTime;

    public static void Show()
    {
        Ensure();
        instance.SetVisible(true);
    }

    public static void Hide()
    {
        if (instance != null) instance.SetVisible(false);
    }

    private static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LoadingScreen>();
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32001;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Sprite paw = Resources.Load<Sprite>(PawPath);

        var bg = NewImage("Background", transform, null);
        Stretch(bg.rectTransform);
        bg.color = Background;

        // Paw trail along the lower part of the screen, walking right.
        var trailRoot = NewRect("Trail", transform);
        trailRoot.anchorMin = trailRoot.anchorMax = new Vector2(0.5f, 0.32f);
        trailRoot.sizeDelta = Vector2.zero;
        trail = new RectTransform[TrailSteps];
        trailImages = new Image[TrailSteps];
        float start = -(TrailSteps - 1) * StepSpacing * 0.5f;
        for (int i = 0; i < TrailSteps; i++)
        {
            var img = NewImage("Step" + i, trailRoot, paw);
            img.color = new Color(PawTint.r, PawTint.g, PawTint.b, 0f);
            var r = img.rectTransform;
            r.sizeDelta = new Vector2(46f, 46f);
            r.anchoredPosition = new Vector2(start + i * StepSpacing, (i % 2 == 0) ? StepSide : -StepSide);
            r.localRotation = Quaternion.Euler(0f, 0f, -90f);
            trail[i] = r;
            trailImages[i] = img;
        }

        // "Loading" with three paws for dots.
        var row = NewRect("Label", transform);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(600f, 110f);
        row.anchoredPosition = new Vector2(-60f, 0f); // the text is right-aligned, so pull the row back to centre "Loading" + paws

        var textRect = NewRect("Text", row);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(420f, 0f);
        textRect.anchoredPosition = new Vector2(20f, 0f);
        var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = "Loading";
        text.fontSize = 84f;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        dots = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            var img = NewImage("Dot" + i, row, paw);
            img.color = PawTint;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(38f, 38f);
            r.anchoredPosition = new Vector2(472f + i * 46f, -18f);
            r.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0) ? -12f : 12f);
            dots[i] = r;
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void SetVisible(bool show)
    {
        if (show == visible) return;
        visible = show;

        if (show)
        {
            gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
            clock = 0f;
        }

        if (fade != null) StopCoroutine(fade);
        if (gameObject.activeInHierarchy) fade = StartCoroutine(Fade(show ? 1f : 0f, show ? FadeInTime : FadeOutTime));
    }

    private IEnumerator Fade(float to, float duration)
    {
        float from = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.SmoothStep(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
        fade = null;
        if (to <= 0f) gameObject.SetActive(false);
    }

    private void Update()
    {
        // A load's activation frame can be long; clamp so the animation does not skip ahead.
        clock += Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // Each step appears in turn, stamps in with a small pop, then fades; the walk loops.
        float cycle = TrailSteps * StepInterval + StepLife;
        float c = clock % cycle;
        for (int i = 0; i < TrailSteps; i++)
        {
            float age = c - i * StepInterval;
            float alpha = 0f, scale = 1f;
            if (age >= 0f && age < StepLife)
            {
                float k = age / StepLife;
                alpha = k < 0.12f ? k / 0.12f : 1f - Mathf.InverseLerp(0.45f, 1f, k);
                scale = age < 0.12f ? Mathf.Lerp(1.35f, 1f, age / 0.12f) : 1f;
            }
            var col = trailImages[i].color;
            col.a = alpha * 0.85f;
            trailImages[i].color = col;
            trail[i].localScale = new Vector3(scale, scale, 1f);
        }

        // Dots: one after another, each a little hop and squash.
        const float dotPeriod = 1.2f;
        for (int i = 0; i < dots.Length; i++)
        {
            float t = ((clock - i * 0.18f) % dotPeriod + dotPeriod) % dotPeriod;
            float hop = t < 0.3f ? Mathf.Sin(t / 0.3f * Mathf.PI) : 0f;
            float s = 1f + hop * 0.35f;
            dots[i].localScale = new Vector3(s, s, 1f);
            var p = dots[i].anchoredPosition;
            dots[i].anchoredPosition = new Vector2(p.x, -18f + hop * 16f);
        }
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        var r = NewRect(name, parent);
        r.gameObject.AddComponent<CanvasRenderer>();
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
