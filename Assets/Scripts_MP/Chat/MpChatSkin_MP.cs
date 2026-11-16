using System.Collections.Generic;
using PlayerScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Look and behaviour of the in-game (match) chat panel:
//   - Closed: only the messages show, as floating text with an outline. Each one stays a few seconds after it arrived,
//     then fades out. The panel, input field, send button and scrollbar are invisible and do not take clicks.
//   - Open (T, or while typing): a translucent dark panel with the same tan buttons and dark text as the menu fades in.
// The lobby chat keeps its own plain look: this only touches a ChatUI that belongs to a match player.
[RequireComponent(typeof(ChatUI))]
public class MpChatSkin_MP : MonoBehaviour
{
    [Tooltip("The tan button sprite of the menu (UI_Panel_Background_Removed). Empty = plain tan colour.")]
    [SerializeField] private Sprite buttonSprite;

    [SerializeField] private float messageVisibleSeconds = 8f;
    [SerializeField] private float messageFadeSeconds = 2f;
    [SerializeField] private float openFadeSeconds = 0.12f;

    // Menu button theme.
    private static readonly Color Tan = new Color(0.98f, 0.71f, 0.40f, 1f);
    private static readonly Color DarkText = new Color(0.20f, 0.20f, 0.20f, 1f);
    private static readonly Color PanelColor = new Color(0.14f, 0.09f, 0.04f, 0.58f);
    private static readonly Color ListColor = new Color(0f, 0f, 0f, 0.22f);
    private static readonly Color InputColor = new Color(1f, 0.93f, 0.78f, 0.92f);

    private ChatUI chat;
    private CanvasGroup panelGroup;
    private Image panelImage;
    private Image listImage;
    private CanvasGroup scrollbarGroup;
    private CanvasGroup inputGroup;
    private RectTransform content;

    private float openness;
    private float enabledAt;
    private bool built;

    private readonly HashSet<ChatMessageRow> known = new HashSet<ChatMessageRow>();

    // Called by the local player when its match chat exists (no prefab setup needed).
    public static void Ensure(PlayerNetwork_MP player)
    {
        if (player == null) return;

        ChatUI ui = player.transform.root.GetComponentInChildren<ChatUI>(true);
        if (ui != null && ui.GetComponent<MpChatSkin_MP>() == null) ui.gameObject.AddComponent<MpChatSkin_MP>();
    }

    private void OnEnable()
    {
        enabledAt = Time.unscaledTime;
        Build();
    }

    private void Build()
    {
        chat = GetComponent<ChatUI>();

        // Only for the chat that lives under a match player package (the lobby chat has no NetworkIdentity parent).
        if (GetComponentInParent<Mirror.NetworkIdentity>(true) == null)
        {
            enabled = false;
            return;
        }

        panelImage = GetComponent<Image>();
        panelGroup = GetOrAdd<CanvasGroup>(gameObject);

        Transform scroll = transform.Find("Scroll View");
        if (scroll != null)
        {
            listImage = scroll.GetComponent<Image>();
            Transform bar = scroll.Find("Scrollbar Vertical");
            if (bar != null) scrollbarGroup = GetOrAdd<CanvasGroup>(bar.gameObject);

            ScrollRect rect = scroll.GetComponent<ScrollRect>();
            if (rect != null) content = rect.content;
        }

        Transform inputRow = transform.Find("Input Row");
        if (inputRow != null)
        {
            inputGroup = GetOrAdd<CanvasGroup>(inputRow.gameObject);
            SkinInput(inputRow.Find("Message Input"));
            SkinSendButton(inputRow.Find("Button_Send"));
        }

        built = true;
        ApplyOpenness(0f);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : go.AddComponent<T>();
    }

    private void SkinInput(Transform input)
    {
        if (input == null) return;

        Image background = input.GetComponent<Image>();
        if (background != null) background.color = InputColor;

        foreach (TMP_Text text in input.GetComponentsInChildren<TMP_Text>(true))
            text.color = text.name == "Placeholder" ? new Color(0.20f, 0.20f, 0.20f, 0.55f) : DarkText;
    }

    private void SkinSendButton(Transform button)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            if (buttonSprite != null)
            {
                image.sprite = buttonSprite;
                image.type = Image.Type.Simple;
            }

            image.color = Tan;
        }

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true)) text.color = DarkText;
    }

    private void LateUpdate()
    {
        if (!built) return;

        bool open = chat != null && chat.IsTyping;
        float target = open ? 1f : 0f;
        openness = openFadeSeconds <= 0f ? target : Mathf.MoveTowards(openness, target, Time.unscaledDeltaTime / openFadeSeconds);

        ApplyOpenness(openness);
        UpdateMessages(open);
    }

    private void ApplyOpenness(float t)
    {
        bool interactive = t > 0.5f;

        if (panelImage != null)
        {
            panelImage.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, PanelColor.a * t);
            panelImage.raycastTarget = interactive;
        }

        if (listImage != null)
        {
            listImage.color = new Color(ListColor.r, ListColor.g, ListColor.b, ListColor.a * t);
            listImage.raycastTarget = interactive;
        }

        SetGroup(scrollbarGroup, t, interactive);
        SetGroup(inputGroup, t, interactive);
    }

    private static void SetGroup(CanvasGroup group, float alpha, bool interactive)
    {
        if (group == null) return;

        group.alpha = alpha;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    // Closed: each message shows for a few seconds after it arrived, then fades. Open: everything shows.
    private void UpdateMessages(bool open)
    {
        if (content == null) return;

        float now = Time.unscaledTime;

        foreach (ChatMessageRow row in content.GetComponentsInChildren<ChatMessageRow>(true))
        {
            if (row == null) continue;

            if (known.Add(row))
            {
                var fade = GetOrAdd<MessageFade>(row.gameObject);

                // History rebuilt when the panel was enabled is old news, a message that just arrived is new.
                fade.bornAt = now - enabledAt < 1f ? now - 1000f : now;

                foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.outlineWidth = 0.22f;
                    text.outlineColor = new Color32(0, 0, 0, 220);
                }
            }

            MessageFade messageFade = row.GetComponent<MessageFade>();
            if (messageFade == null) continue;

            float age = now - messageFade.bornAt;
            float alpha = open ? 1f : 1f - Mathf.Clamp01((age - messageVisibleSeconds) / Mathf.Max(0.01f, messageFadeSeconds));
            messageFade.group.alpha = alpha;
        }
    }

    private class MessageFade : MonoBehaviour
    {
        public float bornAt;
        public CanvasGroup group;

        private void Awake() => group = GetOrAdd<CanvasGroup>(gameObject);
    }
}
