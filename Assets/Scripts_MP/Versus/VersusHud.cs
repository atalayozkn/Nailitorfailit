using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Top-of-screen versus HUD: one completion bar per player and the result banner.
// Built in code so the shared player HUD prefabs stay untouched. The bar length is always the real percent.
public class VersusHud : MonoBehaviour
{
    private const float BarWidth = 430f;
    private const float BarHeight = 36f;

    private VersusMatch match;
    private RectTransform[] fills;
    private TextMeshProUGUI[] labels;
    private TextMeshProUGUI banner;

    public static void Create(VersusMatch match)
    {
        GameObject root = new GameObject("VersusHud", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(match.transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<VersusHud>().Build(match);
    }

    private void Build(VersusMatch owner)
    {
        match = owner;
        fills = new RectTransform[match.SideCount];
        labels = new TextMeshProUGUI[match.SideCount];

        CurrencyText existing = FindFirstObjectByType<CurrencyText>(FindObjectsInactive.Include);
        TMP_FontAsset font = existing != null ? existing.GetComponentInChildren<TextMeshProUGUI>(true)?.font : null;

        for (int i = 0; i < match.SideCount; i++)
        {
            float x = (i == 0 ? -1f : 1f) * (BarWidth * 0.5f + 130f);

            RectTransform back = Panel("Bar_" + i, transform, new Color(0f, 0f, 0f, 0.55f));
            back.anchorMin = back.anchorMax = new Vector2(0.5f, 1f);
            back.pivot = new Vector2(0.5f, 1f);
            back.anchoredPosition = new Vector2(x, -84f);
            back.sizeDelta = new Vector2(BarWidth, BarHeight);

            RectTransform fill = Panel("Fill", back, match.GetSide(i).color);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fills[i] = fill;

            labels[i] = Text("Label", back, font, 24f);
            Stretch(labels[i].rectTransform);
        }

        banner = Text("Result", transform, font, 96f);
        banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
        banner.rectTransform.sizeDelta = new Vector2(1400f, 160f);
        banner.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (match == null) return;

        int local = match.LocalSideIndex();

        for (int i = 0; i < fills.Length; i++)
        {
            VersusMatch.Side side = match.GetSide(i);
            float percent = Mathf.Clamp01(side.percent);

            fills[i].anchorMax = new Vector2(percent, 1f);

            string who = i == local ? "YOU" : local >= 0 ? "RIVAL" : side.label;
            labels[i].text = $"{who}  {Mathf.FloorToInt(percent * 100f)}%";
        }

        int winner = match.Winner;
        if (winner == VersusMatch.NoWinner)
        {
            if (banner.gameObject.activeSelf) banner.gameObject.SetActive(false);
            return;
        }

        banner.gameObject.SetActive(true);
        banner.text = winner == VersusMatch.Draw ? "DRAW"
            : local < 0 ? match.GetSide(winner).label + " WINS"
            : winner == local ? "NAIL IT" : "FAIL IT";
    }

    private static RectTransform Panel(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
