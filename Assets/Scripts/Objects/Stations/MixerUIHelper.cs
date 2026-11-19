using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows what is in each mixer slot: the material's icon when one is assigned (wood, brick, cement),
// otherwise its letter (W Wood, B Brick, G Glass, C Cement).
// Multiplayer reads firstImage/secondImage/thirdImage by name (MpMixerIcons_MP), so keep those names.
public class MixerUIHelper : MonoBehaviour
{
    [SerializeField] private Image firstImage;
    [SerializeField] private Image secondImage;
    [SerializeField] private Image thirdImage;

    [Header("Icons")]
    [SerializeField] private Sprite woodIcon;
    [SerializeField] private Sprite brickIcon;
    [SerializeField] private Sprite cementIcon;

    [SerializeField] private Color letterColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    private TextMeshProUGUI firstLetter;
    private TextMeshProUGUI secondLetter;
    private TextMeshProUGUI thirdLetter;

    private Image firstIcon;
    private Image secondIcon;
    private Image thirdIcon;

    private void Awake()
    {
        firstLetter = CreateLetter(firstImage);
        secondLetter = CreateLetter(secondImage);
        thirdLetter = CreateLetter(thirdImage);

        firstIcon = CreateIcon(firstImage);
        secondIcon = CreateIcon(secondImage);
        thirdIcon = CreateIcon(thirdImage);
    }

    private void OnEnable()
    {
        ClearImages();
    }

    public void DisplayItem(int index, CarriableType type)
    {
        TextMeshProUGUI letter = index == 1 ? firstLetter : index == 2 ? secondLetter : thirdLetter;
        Image icon = index == 1 ? firstIcon : index == 2 ? secondIcon : thirdIcon;

        Sprite sprite = GetIcon(type);
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        letter.text = sprite != null ? string.Empty : GetLetter(type);
    }

    // The icon assigned for this material, or null when it should fall back to its letter.
    public Sprite GetIcon(CarriableType type)
    {
        switch (type)
        {
            case CarriableType.Wood: return woodIcon;
            case CarriableType.Brick: return brickIcon;
            case CarriableType.Cement: return cementIcon;
            default: return null;
        }
    }

    #region UTILITIES

    public void ClearImages()
    {
        firstLetter.text = string.Empty;
        secondLetter.text = string.Empty;
        thirdLetter.text = string.Empty;

        firstIcon.enabled = false;
        secondIcon.enabled = false;
        thirdIcon.enabled = false;
    }

    private static string GetLetter(CarriableType type)
    {
        // The mixer can accept any carriable, every type name starts with a usable letter.
        return type.ToString().Substring(0, 1);
    }

    private TextMeshProUGUI CreateLetter(Image slot)
    {
        GameObject letterObject = new GameObject("Letter", typeof(RectTransform));
        letterObject.layer = slot.gameObject.layer;

        RectTransform rect = letterObject.GetComponent<RectTransform>();
        rect.SetParent(slot.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = letterObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = letterColor;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.1f;
        text.fontSizeMax = 200f;
        text.raycastTarget = false;
        return text;
    }

    // Named "Icon" so multiplayer's MpMixerIcons_MP finds and reuses it instead of adding a second one.
    private Image CreateIcon(Image slot)
    {
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
        iconObject.layer = slot.gameObject.layer;

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.SetParent(slot.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);

        Image image = iconObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    #endregion
}
