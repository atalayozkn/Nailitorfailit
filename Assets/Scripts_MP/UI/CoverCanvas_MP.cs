using UnityEngine;
using UnityEngine.UI;

// Keeps a background image covering the whole screen at any aspect ratio (the menu background is 3:2 art): scaled until
// both screen axes are filled, centred, the overflow is cropped by the screen edge. Works whatever the parents' scales are.
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class CoverCanvas_MP : MonoBehaviour
{
    [SerializeField] private float overscan = 1.02f;

    private Image image;
    private Vector2 lastCanvasSize;

    private void OnEnable() => Fit(true);

    private void LateUpdate() => Fit(false);

    private void Fit(bool force)
    {
        if (image == null) image = GetComponent<Image>();
        if (image == null || image.sprite == null) return;

        Canvas canvas = image.canvas != null ? image.canvas.rootCanvas : null;
        if (canvas == null) return;

        var canvasRect = (RectTransform)canvas.transform;
        Vector2 size = canvasRect.rect.size;
        if (!force && size == lastCanvasSize) return;
        lastCanvasSize = size;

        float imageScale = transform.lossyScale.x;
        if (size.x <= 0f || size.y <= 0f || imageScale <= 0f) return;

        Rect sprite = image.sprite.rect;
        float k = canvas.transform.lossyScale.x / imageScale; // canvas units -> this object's units
        float scale = Mathf.Max(size.x / sprite.width, size.y / sprite.height) * overscan * k;

        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(sprite.width * scale, sprite.height * scale);
    }
}
