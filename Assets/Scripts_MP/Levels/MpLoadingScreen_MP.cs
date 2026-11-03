using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MpLoadingScreen_MP : MonoBehaviour
{
    private const float FadeSpeed = 4f;
    private const float MinimumVisible = 0.6f;

    private static MpLoadingScreen_MP instance;

    private CanvasGroup group;
    private RectTransform spinner;
    private TMP_Text title;
    private TMP_Text subtitle;

    private float shownAt = -1f;
    private bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (instance != null) return;

        var host = new GameObject("MpLoadingScreen_MP");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<MpLoadingScreen_MP>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Show(string reason)
    {
        if (instance == null) return;

        if (instance.subtitle != null) instance.subtitle.text = reason ?? string.Empty;
        instance.BeginVisible();
    }

    private void BeginVisible()
    {
        if (visible) return;

        visible = true;
        shownAt = Time.unscaledTime;

        if (group != null)
        {
            group.gameObject.SetActive(true);
            group.blocksRaycasts = true;
        }
    }

    private void EndVisible()
    {
        if (!visible) return;

        visible = false;
        if (group != null) group.blocksRaycasts = false;
    }

    private void Update()
    {
        if (group == null) return;

        if (IsLoading()) BeginVisible();

        if (visible && !IsLoading() && Time.unscaledTime - shownAt >= MinimumVisible)
            EndVisible();

        if (!visible && group.alpha <= 0.001f && group.gameObject.activeSelf)
            group.gameObject.SetActive(false);

        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);

        if (spinner != null)
            spinner.Rotate(0f, 0f, -180f * Time.unscaledDeltaTime);

        if (title != null && visible)
        {
            int dots = Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 4;
            title.text = "YUKLENIYOR" + new string('.', dots);
        }
    }

    private static bool IsLoading()
    {
        if (NetworkManager.loadingSceneAsync != null && !NetworkManager.loadingSceneAsync.isDone) return true;
        if (NetworkServer.isLoadingScene) return true;
        if (NetworkClient.isLoadingScene) return true;

        return false;
    }

    private void Build()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        RectTransform root = (RectTransform)canvasGo.transform;

        Image backdrop = NewImage(root, "Backdrop", new Color(0.04f, 0.05f, 0.07f, 1f));
        Stretch(backdrop.rectTransform);

        Image glow = NewImage(root, "Glow", new Color(0.98f, 0.71f, 0.40f, 0.06f));
        Stretch(glow.rectTransform);

        spinner = NewRect(root, "Spinner");
        spinner.sizeDelta = new Vector2(140f, 140f);
        spinner.anchoredPosition = new Vector2(0f, 40f);

        for (int i = 0; i < 3; i++)
        {
            Image blade = NewImage(spinner, "Blade" + i, new Color(0.98f, 0.71f, 0.40f, 1f));
            RectTransform rect = blade.rectTransform;
            rect.sizeDelta = new Vector2(14f, 46f);
            rect.anchoredPosition = new Vector2(Mathf.Cos(i * Mathf.PI * 2f / 3f) * 46f,
                                                Mathf.Sin(i * Mathf.PI * 2f / 3f) * 46f);
            rect.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
        }

        title = NewText(root, "Title", new Vector2(0f, -70f), 38f, "YUKLENIYOR");
        title.color = new Color(0.98f, 0.71f, 0.40f, 1f);

        subtitle = NewText(root, "Subtitle", new Vector2(0f, -120f), 22f, string.Empty);
        subtitle.color = new Color(0.85f, 0.85f, 0.85f, 0.85f);

        canvasGo.SetActive(false);
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        RectTransform rect = NewRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text NewText(Transform parent, string name, Vector2 position, float size, string text)
    {
        RectTransform rect = NewRect(parent, name);
        rect.sizeDelta = new Vector2(1400f, 80f);
        rect.anchoredPosition = position;

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
