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
        image.color = targetColor;
    }
    public void RevertToOriginalColor()
    {
        image.color = initialColor;
    }
}
