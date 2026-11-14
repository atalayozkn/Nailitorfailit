
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
    }

    private string displayName = "Player";
    private bool shownAsHost;

    private const string StarGlyph = "\u2605";

    // Gold star in front of the host. Falls back to a plain marker if the font has no star glyph.
    private string ComposeName()
    {
        if (!shownAsHost) return displayName;

        bool hasStar = nameText != null && nameText.font != null &&
                       nameText.font.HasCharacter('\u2605', true, true);

        return $"<color=#FFD24A>{(hasStar ? StarGlyph : "(H)")}</color> {displayName}";
    }

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

        if (nameText != null)
        {
            RectTransform label = nameText.rectTransform;
            label.offsetMin = new Vector2(44f, label.offsetMin.y);
            nameText.alignment = TMPro.TextAlignmentOptions.Left;
        }

        return image;
    }

    public void UpdateColor(bool isHost, bool isReady)
    {
        if (nameText == null) return;

        shownAsHost = isHost;
        nameText.richText = true;
        nameText.text = ComposeName();
        nameText.color = playerColor;

        if (isHost) nameText.fontStyle = FontStyles.Bold;
        else if (isReady) nameText.fontStyle = FontStyles.Underline;
        else nameText.fontStyle = FontStyles.Normal;
    }
}
