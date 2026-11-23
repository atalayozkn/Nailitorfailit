using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A solid outline around a TextMeshPro UGUI label: copies of the label in the outline colour sit behind it, pushed a little
// in 8 directions. (TextMeshPro's own outline is limited by the font atlas padding and stays a hair thin on small text.)
// The copies follow the label's text, size, style, position and visibility.
[RequireComponent(typeof(TMP_Text))]
public class TextOutline : MonoBehaviour
{
    [Tooltip("Thickness in the label's own units (UI pixels, or local units of the world canvas the label is on).")]
    [SerializeField] private float thickness = 1.6f;
    [SerializeField] private Color color = Color.black;

    private const int Directions = 8;

    private TMP_Text label;
    private TMP_Text[] copies;

    public static TextOutline Attach(TMP_Text target, float thickness, Color color)
    {
        if (target == null) return null;

        TextOutline outline = target.GetComponent<TextOutline>();
        if (outline == null) outline = target.gameObject.AddComponent<TextOutline>();
        outline.thickness = thickness;
        outline.color = color;
        return outline;
    }

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
        Build();
    }

    private void Build()
    {
        if (!(label is TextMeshProUGUI) || label.transform.parent == null) return;

        copies = new TMP_Text[Directions];

        for (int i = 0; i < Directions; i++)
        {
            var go = new GameObject("Outline_" + i, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = label.gameObject.layer;
            go.transform.SetParent(label.transform.parent, false);
            go.transform.SetSiblingIndex(label.transform.GetSiblingIndex()); // behind the label

            go.AddComponent<LayoutElement>().ignoreLayout = true; // a layout group around the label leaves the copies alone

            var copy = go.AddComponent<TextMeshProUGUI>();
            copy.raycastTarget = false;
            copies[i] = copy;
        }
    }

    private void OnEnable() => SetCopiesActive(true);

    private void OnDisable() => SetCopiesActive(false);

    private void OnDestroy()
    {
        if (copies == null) return;

        foreach (TMP_Text copy in copies)
            if (copy != null) Destroy(copy.gameObject);
    }

    private void SetCopiesActive(bool active)
    {
        if (copies == null) return;

        foreach (TMP_Text copy in copies)
            if (copy != null) copy.gameObject.SetActive(active);
    }

    private void LateUpdate()
    {
        if (copies == null || label == null) return;

        var source = (RectTransform)label.transform;
        bool visible = label.enabled;

        for (int i = 0; i < copies.Length; i++)
        {
            TMP_Text copy = copies[i];
            if (copy == null) continue;

            copy.enabled = visible;
            if (!visible) continue;

            float angle = i * Mathf.PI * 2f / Directions;
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * thickness;

            var rect = (RectTransform)copy.transform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.localRotation = source.localRotation;
            rect.localScale = source.localScale;
            rect.anchoredPosition = source.anchoredPosition + offset;

            copy.font = label.font;
            copy.fontStyle = label.fontStyle;
            copy.fontSize = label.fontSize;
            copy.enableAutoSizing = label.enableAutoSizing;
            copy.fontSizeMin = label.fontSizeMin;
            copy.fontSizeMax = label.fontSizeMax;
            copy.alignment = label.alignment;
            copy.textWrappingMode = label.textWrappingMode;
            copy.overflowMode = label.overflowMode;
            copy.margin = label.margin;
            copy.richText = label.richText;
            copy.characterSpacing = label.characterSpacing;
            copy.color = new Color(color.r, color.g, color.b, color.a * label.color.a);

            string text = label.text;
            if (copy.text != text) copy.text = text;
        }
    }
}
