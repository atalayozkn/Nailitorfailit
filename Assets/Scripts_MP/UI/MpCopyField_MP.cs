using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_Text))]
public class MpCopyField_MP : MonoBehaviour, IPointerClickHandler
{
    public System.Func<string> Source;
    public System.Action OnCopied;
    public string Mask = "********";
    public string Prefix = string.Empty; // e.g. "Lobby ID: "

    private float revealUntil;

    public bool Revealed => Time.unscaledTime <= revealUntil;

    private TMP_Text label;

    // Keep the label in sync on its own: GameManagerMpCompat writes it only once, so without this
    // a click copied the value but the label never revealed it (and never re-masked).
    private void LateUpdate()
    {
        if (Source == null) return;
        if (label == null && !TryGetComponent(out label)) return;

        string title = Prefix.Trim().TrimEnd(':');
        string shown = (title.Length > 0 ? Localization.T(title) + ": " : string.Empty) + Format(Source());
        if (label.text != shown) label.text = shown;
    }

    public void OnPointerClick(PointerEventData eventData) => CopyNow();

    public void CopyNow()
    {
        string value = Source?.Invoke();
        if (string.IsNullOrEmpty(value)) return;

        // The value stays hidden (it is masked on screen); a notice says it was copied.
        GUIUtility.systemCopyBuffer = value;

        OnCopied?.Invoke();
        MpNotice_MP.Info(Prefix.Trim().TrimEnd(':') + " copied");

        InteractionDebug.Log($"[MP-UI] '{name}' copied to the clipboard ({value.Length} chars).");
    }

    public string Format(string real)
    {
        if (string.IsNullOrEmpty(real)) return string.Empty;

        return Revealed ? real : Mask;
    }
}

// Turns one of the lobby's big menu-plate buttons (Lobby_ID_Label / Lobby_Password_Label) into a text row with a small
// copy button at its end:   Lobby ID: ********  [Copy]
public static class MpCopyRow_MP
{
    private const string CopyButtonName = "CopyButton";

    private static UnityEngine.UI.Button FindButtonPlate(Transform area)
    {
        if (area == null) return null;

        foreach (UnityEngine.UI.Button other in area.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if (!other.enabled || other.name == CopyButtonName) continue;
            if (other.targetGraphic is UnityEngine.UI.Image image && image.sprite != null && image.enabled) return other;
        }

        return null;
    }

    public static void Convert(GameObject row, TMP_Text label, MpCopyField_MP field, string prefix)
    {
        if (row == null || label == null || field == null) return;

        field.Prefix = prefix;
        if (row.transform.Find(CopyButtonName) != null) return;

        // The plate and everything that reacts to hovering it goes away: it is a line of text now.
        foreach (Behaviour behaviour in row.GetComponents<Behaviour>())
        {
            if (behaviour is UnityEngine.UI.Button || behaviour is UnityEngine.UI.Image || behaviour is ButtonHoverScale ||
                behaviour is CustomButton || behaviour is ImageMaterialChangeHelper) behaviour.enabled = false;
        }

        var rowRect = (RectTransform)row.transform;
        float scale = Mathf.Max(0.0001f, Mathf.Abs(rowRect.localScale.x)); // the row is drawn through a small local scale
        float buttonSize = 76f / scale;

        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(1f, 0.93f, 0.78f, 1f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        // Room for the copy button on the right (its width is buttonSize * 1.25), and the text shrinks to fit the rest.
        label.margin = new Vector4(30f / scale, 0f, buttonSize * 1.25f + 24f / scale, 0f);
        label.enableAutoSizing = true;
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = label.fontSize * 0.5f;

        var buttonObject = new GameObject(CopyButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        buttonObject.layer = row.layer;
        buttonObject.transform.SetParent(rowRect, false);

        var rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(buttonSize * 1.25f, buttonSize);
        rect.anchoredPosition = Vector2.zero;

        var plate = buttonObject.GetComponent<UnityEngine.UI.Image>();
        var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
        // The same plate the other lobby buttons use (tan, with the hazard-stripe edge). The brown wooden panel sprite
        // was the wrong one: dark on dark.
        UnityEngine.UI.Button templateButton = FindButtonPlate(rowRect.parent);
        if (templateButton != null)
        {
            var template = (UnityEngine.UI.Image)templateButton.targetGraphic;
            plate.sprite = template.sprite;
            plate.type = template.type;
            plate.color = template.color;
            // The palette colour of the plate comes from the button's colour block (normal / hover / pressed), not the image.
            button.transition = templateButton.transition;
            button.colors = templateButton.colors;
        }
        else
        {
            plate.sprite = Resources.Load<Sprite>("Menu/MenuButtons_Panel");
            plate.type = UnityEngine.UI.Image.Type.Sliced;
            plate.color = new Color(0.98f, 0.71f, 0.40f, 1f);
            button.transition = UnityEngine.UI.Selectable.Transition.None;
        }

        button.targetGraphic = plate;
        button.onClick.AddListener(field.CopyNow);

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        textObject.layer = row.layer;
        textObject.transform.SetParent(rect, false);

        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = label.font;
        text.text = "COPY";
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 20f / scale;
        text.color = new Color(0.2f, 0.12f, 0.06f, 1f);
        text.raycastTarget = false;
    }
}
