using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The chat panel in the menu's own colours: an opaque dark-brown message area (instead of a see-through grey box), an input
// field on the same tan plate as the menu buttons with dark text, and a thin scrollbar. Applied once when the chat opens.
public class MpChatStyle_MP : MonoBehaviour
{
    private static readonly Color AreaColor = new Color(0.17f, 0.11f, 0.07f, 1f);
    private static readonly Color PlateColor = new Color(0.98f, 0.71f, 0.40f, 1f);
    private static readonly Color InkColor = new Color(0.20f, 0.12f, 0.06f, 1f);
    private static readonly Color TrackColor = new Color(0.09f, 0.06f, 0.04f, 0.9f);

    public static void Apply(ScrollRect scroll, TMP_InputField input)
    {
        GameObject host = scroll != null ? scroll.gameObject : input != null ? input.gameObject : null;
        if (host == null || host.GetComponent<MpChatStyle_MP>() != null) return;
        host.AddComponent<MpChatStyle_MP>();

        // Message area: solid dark brown.
        if (scroll != null && scroll.TryGetComponent(out Image area)) area.color = AreaColor;

        // Thin scrollbar with a tan handle.
        if (scroll != null && scroll.verticalScrollbar != null)
        {
            Scrollbar bar = scroll.verticalScrollbar;
            if (bar.TryGetComponent(out Image track)) track.color = TrackColor;
            if (bar.handleRect != null && bar.handleRect.TryGetComponent(out Image handle)) handle.color = PlateColor;

            var rect = (RectTransform)bar.transform;
            rect.sizeDelta = new Vector2(Mathf.Min(rect.sizeDelta.x, 10f), rect.sizeDelta.y);
        }

        // Input: a flat tan field (no button art) with dark text.
        if (input != null)
        {
            if (input.TryGetComponent(out Image plate))
            {
                plate.sprite = null;
                plate.type = Image.Type.Simple;
                plate.color = PlateColor;
            }

            if (input.textComponent != null) input.textComponent.color = InkColor;
            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.text = "Start chatting...";
                placeholder.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.55f);
            }
            input.caretColor = InkColor;
            input.selectionColor = new Color(InkColor.r, InkColor.g, InkColor.b, 0.3f);
            input.customCaretColor = true;
        }
    }
}
