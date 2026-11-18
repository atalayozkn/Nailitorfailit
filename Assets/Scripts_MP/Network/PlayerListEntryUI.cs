
using TMPro;
using UnityEngine;

public class PlayerListEntryUI : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;

    static readonly Color HostColor     = Color.yellow;
    static readonly Color ReadyColor    = Color.green;
    static readonly Color NotReadyColor = Color.red;

    [SerializeField] int maxNameLength = 16;

    private Color playerColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    public void SetName(string name)
    {
        if (string.IsNullOrEmpty(name)) name = "Player";

        if (maxNameLength > 0 && name.Length > maxNameLength)
            name = name.Substring(0, maxNameLength - 1) + "\u2026";

        displayName = name;
        if (nameText != null) nameText.text = ComposeName();
        PlaceIcons();
    }

    private string displayName = "Player";
    private bool shownAsHost;

    private const string StarGlyph = "\u2605";

    // Gold star in front of the host. Falls back to a plain marker if the font has no star glyph.
    // The host is marked with a crown icon after the name (see PlaceIcons), no glyph in the text any more.
    private string ComposeName() => displayName;

    private UnityEngine.UI.Image avatar;

    public void SetPlayerColor(Color color) => playerColor = color;

    public void SetSteamId(ulong steamId)
    {
        if (steamId == 0) return;
        if (avatar == null) avatar = EnsureAvatar();

        Sprite sprite = MpSteamAvatar_MP.GetSprite(steamId);
        if (sprite == null) return;

        avatar.sprite = sprite;
        avatar.color = Color.white;
        avatar.enabled = true;
        PlaceIcons();
    }

    private UnityEngine.UI.Image EnsureAvatar()
    {
        var go = new GameObject("SteamAvatar", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(34f, 34f);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(20f, 0f);

        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        image.enabled = false;

        return image;
    }

    private UnityEngine.UI.Image micIcon;

    private static readonly Color MicOpenColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    private static readonly Color MicMutedColor = new Color(0.92f, 0.35f, 0.35f, 1f);
    private static readonly Color MicTalkingColor = new Color(0.40f, 0.85f, 0.45f, 1f);

    // Mic state as an icon at the right end of the row: open (white), talking (green), muted (red slash).
    public void SetMic(bool muted, bool talking)
    {
        if (micIcon == null) micIcon = EnsureMicIcon();

        micIcon.sprite = MpMicIcon_MP.For(muted);
        micIcon.color = muted ? MicMutedColor : talking ? MicTalkingColor : MicOpenColor;
        PlaceIcons();
    }

    private UnityEngine.UI.Image crownIcon;

    private UnityEngine.UI.Button kickButton;
    private const float KickPixels = 30f;

    // Host only: a red boot (kick) icon at the end of another player's row. It sits in the same icon chain as the crown and the
    // mic (right after the name, advancing x), so it never lands on top of either of them.
    public void SetKickable(bool show, System.Action onKick)
    {
        if (!show)
        {
            if (kickButton != null) kickButton.gameObject.SetActive(false);
            return;
        }

        if (kickButton == null) kickButton = BuildKickButton();

        kickButton.onClick.RemoveAllListeners();
        if (onKick != null) kickButton.onClick.AddListener(() => onKick());

        kickButton.gameObject.SetActive(true);
        PlaceIcons();
    }

    private UnityEngine.UI.Button BuildKickButton()
    {
        var go = new GameObject("KickButton", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(KickPixels, KickPixels);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var background = go.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color(0.86f, 0.24f, 0.24f, 0.95f);

        var button = go.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = background;

        var iconGo = new GameObject("KickIcon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);

        var iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        var icon = iconGo.AddComponent<UnityEngine.UI.Image>();
        icon.sprite = MpMicIcon_MP.Kick;
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.raycastTarget = false;

        return button;
    }

    private UnityEngine.UI.Image CreateIcon(string iconName)
    {
        var go = new GameObject(iconName, typeof(RectTransform));
        go.transform.SetParent(nameText.transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(IconPixels, IconPixels);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private const float IconPixels = 28f;
    private const float IconGap = 8f;

    private UnityEngine.UI.Image EnsureMicIcon()
    {
        nameText.alignment = TMPro.TextAlignmentOptions.Left;
        return CreateIcon("MicIcon");
    }

    // <name> <crown if host> <mic>: icons sit right behind the name text (children of its rect, left aligned).
    // The name starts to the right of the avatar (when there is one). The shift used to happen once, when the avatar
    // object was created, and any later layout pass left the first letters under the picture.
    private void LayoutNameText()
    {
        if (nameText == null) return;

        // The row object IS the text and its parent's layout group owns the rect (it resets any offset), so the name
        // is moved with the text margin instead.
        bool hasAvatar = avatar != null && avatar.enabled;
        Vector4 margin = nameText.margin;
        margin.x = hasAvatar ? 52f : 12f;
        nameText.margin = margin;
        nameText.alignment = TMPro.TextAlignmentOptions.Left;

        // The row is as wide as its text (the list's layout group sizes it). With auto-size on, a wider margin made
        // the row narrower than the text wanted and the font shrank to 14 px: fixed size, no wrapping.
        if (nameText.enableAutoSizing)
        {
            nameText.enableAutoSizing = false;
            nameText.fontSize = 28f;
        }

        nameText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        nameText.overflowMode = TMPro.TextOverflowModes.Overflow;
    }

    private void PlaceIcons()
    {
        if (nameText == null) return;

        LayoutNameText();
        if (micIcon == null) return;

        nameText.ForceMeshUpdate();

        // Right edge of the drawn name, measured from the row's left edge.
        float x = nameText.textBounds.max.x - nameText.rectTransform.rect.xMin + IconGap;

        if (shownAsHost)
        {
            if (crownIcon == null)
            {
                crownIcon = CreateIcon("CrownIcon");
                crownIcon.sprite = MpMicIcon_MP.Crown;
                crownIcon.color = new Color(1f, 0.82f, 0.25f, 1f);
            }

            crownIcon.gameObject.SetActive(true);
            ((RectTransform)crownIcon.transform).anchoredPosition = new Vector2(x + IconPixels * 0.5f, 0f);
            x += IconPixels + IconGap;
        }
        else if (crownIcon != null) crownIcon.gameObject.SetActive(false);

        ((RectTransform)micIcon.transform).anchoredPosition = new Vector2(x + IconPixels * 0.5f, 0f);
        x += IconPixels + IconGap;

        if (kickButton != null && kickButton.gameObject.activeSelf)
            ((RectTransform)kickButton.transform).anchoredPosition = new Vector2(x + KickPixels * 0.5f, 0f);
    }

    public void UpdateColor(bool isHost, bool isReady)
    {
        if (nameText == null) return;

        shownAsHost = isHost;
        nameText.richText = true;
        nameText.text = ComposeName();
        nameText.color = playerColor;
        PlaceIcons();

        if (isHost) nameText.fontStyle = FontStyles.Bold;
        else if (isReady) nameText.fontStyle = FontStyles.Underline;
        else nameText.fontStyle = FontStyles.Normal;
    }
}
