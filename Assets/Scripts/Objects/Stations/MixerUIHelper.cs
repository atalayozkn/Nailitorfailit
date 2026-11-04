using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the mixer's slots as material letters (W Wood, B Brick, G Glass, C Cement).
public class MixerUIHelper : MonoBehaviour
{
    [SerializeField] private Image firstImage;
    [SerializeField] private Image secondImage;
    [SerializeField] private Image thirdImage;

    [SerializeField] private Color letterColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    private TextMeshProUGUI firstLetter;
    private TextMeshProUGUI secondLetter;
    private TextMeshProUGUI thirdLetter;

    private void Awake()
    {
        firstLetter = CreateLetter(firstImage);
        secondLetter = CreateLetter(secondImage);
        thirdLetter = CreateLetter(thirdImage);
    }

    private void OnEnable()
    {
        ClearImages();
    }

    public void DisplayItem(int index, CarriableType type)
    {
        string letter = GetLetter(type);

        if (index == 1)
        {
            firstLetter.text = letter;
        }
        else if (index == 2)
        {
            secondLetter.text = letter;
        }
        else
        {
            thirdLetter.text = letter;
        }
    }

    #region UTILITIES

    public void ClearImages()
    {
        firstLetter.text = string.Empty;
        secondLetter.text = string.Empty;
        thirdLetter.text = string.Empty;
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

    #endregion
}
