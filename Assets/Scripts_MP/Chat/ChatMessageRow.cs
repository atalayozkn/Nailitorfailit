using TMPro;
using UnityEngine;

public class ChatMessageRow : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Header("Style")]
    [SerializeField] private Color senderColor = new Color(1f, 0.85f, 0.40f);
    [SerializeField] private Color messageColor = Color.white;
    [SerializeField] private bool boldSender = true;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    public void Set(string sender, string text)
    {
        if (label == null)
        {
            Debug.LogWarning("[CHAT] ChatMessageRow: label reference is empty.");
            return;
        }

        string safeSender = string.IsNullOrWhiteSpace(sender) ? "Player" : sender;
        string safeText = text ?? string.Empty;

        string hex = ColorUtility.ToHtmlStringRGB(senderColor);
        string prefix = boldSender
            ? $"<b><color=#{hex}>{safeSender}</color></b>: "
            : $"<color=#{hex}>{safeSender}</color>: ";

        label.color = messageColor;
        label.text = prefix + safeText;
    }

    public void Refresh()
    {
        if (label != null) label.ForceMeshUpdate();
    }
}
