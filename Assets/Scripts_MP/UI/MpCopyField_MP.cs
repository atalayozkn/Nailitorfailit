using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_Text))]
public class MpCopyField_MP : MonoBehaviour, IPointerClickHandler
{
    public System.Func<string> Source;
    public System.Action OnCopied;
    public string Mask = "********";

    private float revealUntil;

    public bool Revealed => Time.unscaledTime <= revealUntil;

    private TMP_Text label;

    // Keep the label in sync on its own: GameManagerMpCompat writes it only once, so without this
    // a click copied the value but the label never revealed it (and never re-masked).
    private void LateUpdate()
    {
        if (Source == null) return;
        if (label == null && !TryGetComponent(out label)) return;

        string shown = Format(Source());
        if (label.text != shown) label.text = shown;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        string value = Source?.Invoke();
        if (string.IsNullOrEmpty(value)) return;

        GUIUtility.systemCopyBuffer = value;
        revealUntil = Time.unscaledTime + 1.5f;

        OnCopied?.Invoke();

        InteractionDebug.Log($"[MP-UI] '{name}' copied to the clipboard ({value.Length} chars).");
    }

    public string Format(string real)
    {
        if (string.IsNullOrEmpty(real)) return string.Empty;

        return Revealed ? real : Mask;
    }
}
