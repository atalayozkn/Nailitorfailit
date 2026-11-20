using UnityEngine;
using UnityEngine.UI;

// Keeps a background image covering the whole screen at any aspect ratio: scaled until both screen axes are filled,
// centred on the canvas, the overflow is cropped by the screen edge. Works whatever the parents' scales are
// (including non-uniform ones, so the art is never stretched).
//
// Some background art ships with transparent / ragged margins around the painted area. Put those margins (in source
// texture pixels) into "contentInsets" and only the painted area is fitted and centred; the margins end up off-screen.
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class CoverCanvas_MP : MonoBehaviour
{
    [SerializeField] private float overscan = 1.02f;

    [Tooltip("Pixels of unusable margin inside the sprite, measured from its edges: x = left, y = top, z = right, w = bottom.")]
    [SerializeField] private Vector4 contentInsets = Vector4.zero;

    private Image image;
    private Vector2 lastCanvasSize;

    private void OnEnable() => Fit(true);

    private void OnValidate() => Fit(true);

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

        Vector3 lossy = transform.lossyScale;
        Vector3 canvasLossy = canvas.transform.lossyScale;
        if (size.x <= 0f || size.y <= 0f || lossy.x <= 0f || lossy.y <= 0f) return;

        Rect sprite = image.sprite.rect;
        float contentW = Mathf.Max(1f, sprite.width - contentInsets.x - contentInsets.z);
        float contentH = Mathf.Max(1f, sprite.height - contentInsets.y - contentInsets.w);

        // canvas units -> this object's local units, per axis
        float kx = canvasLossy.x / lossy.x;
        float ky = canvasLossy.y / lossy.y;
        float scale = Mathf.Max(size.x / contentW, size.y / contentH) * overscan;

        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(sprite.width * scale * kx, sprite.height * scale * ky);

        // Put the centre of the painted area on the centre of the canvas, wherever the parent happens to sit.
        rect.position = canvasRect.TransformPoint(canvasRect.rect.center);
        float offsetX = (contentInsets.z - contentInsets.x) * 0.5f * scale * kx; // sprite centre vs content centre (right +)
        float offsetY = (contentInsets.y - contentInsets.w) * 0.5f * scale * ky; // (up +)
        rect.anchoredPosition += new Vector2(offsetX, offsetY);
    }
}
