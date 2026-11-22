using UnityEngine;
using UnityEngine.UI;

public class ImageMaterialChangeHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image image;

    [Header("Settings")]
    [SerializeField] private Color targetColor;

    private Color initialColor;

    private void Awake()
    {
        initialColor = image.color;
    }
    public void ChangeToColor()
    {
        // A target with no alpha was never set up (it just made the button's own background vanish while hovered).
        if (targetColor.a <= 0.01f) return;

        image.color = targetColor;
    }
    public void RevertToOriginalColor()
    {
        image.color = initialColor;
    }
}
