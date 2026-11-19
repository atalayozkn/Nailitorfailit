using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The mixer's slot display shows the material LETTER (W / B / G / C). In multiplayer it shows a small picture of the
// material instead: a wooden plank pair, bricks, a glass pane, a cement sack. Pictures are drawn in code (no art asset
// needed) and sit in a child "Icon" image of each slot; the letter is emptied.
public static class MpMixerIcons_MP
{
    private const int Size = 64;
    private static readonly Sprite[] Cache = new Sprite[8];

    private static readonly System.Reflection.FieldInfo[] SlotFields =
    {
        typeof(MixerUIHelper).GetField("firstImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
        typeof(MixerUIHelper).GetField("secondImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
        typeof(MixerUIHelper).GetField("thirdImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
    };

    // index: 1..3 (the mixer's own numbering).
    public static void Show(MixerUIHelper ui, int index, CarriableType type)
    {
        Image slot = SlotImage(ui, index);
        if (slot == null) return;

        HideLetter(slot);

        Image icon = IconOf(slot, true);
        Sprite art = ui.GetIcon(type);
        icon.sprite = art != null ? art : SpriteFor(type);
        icon.enabled = icon.sprite != null;
    }

    public static void ClearAll(MixerUIHelper ui)
    {
        for (int i = 1; i <= 3; i++)
        {
            Image slot = SlotImage(ui, i);
            if (slot == null) continue;

            Image icon = IconOf(slot, false);
            if (icon != null) icon.enabled = false;
        }
    }

    private static Image SlotImage(MixerUIHelper ui, int index)
    {
        if (ui == null || index < 1 || index > 3 || SlotFields[index - 1] == null) return null;
        return SlotFields[index - 1].GetValue(ui) as Image;
    }

    private static void HideLetter(Image slot)
    {
        Transform letter = slot.transform.Find("Letter");
        if (letter != null && letter.TryGetComponent(out TextMeshProUGUI text)) text.text = string.Empty;
    }

    private static Image IconOf(Image slot, bool create)
    {
        Transform existing = slot.transform.Find("Icon");
        if (existing != null) return existing.GetComponent<Image>();
        if (!create) return null;

        var go = new GameObject("Icon", typeof(RectTransform));
        go.layer = slot.gameObject.layer;

        var rect = (RectTransform)go.transform;
        rect.SetParent(slot.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);

        var image = go.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite SpriteFor(CarriableType type)
    {
        int key = (int)type;
        if (key < 0 || key >= Cache.Length) return null;
        if (Cache[key] != null) return Cache[key];

        switch (type)
        {
            case CarriableType.Wood: Cache[key] = Build(type, DrawWood); break;
            case CarriableType.Brick: Cache[key] = Build(type, DrawBrick); break;
            case CarriableType.Glass: Cache[key] = Build(type, DrawGlass); break;
            case CarriableType.Cement: Cache[key] = Build(type, DrawCement); break;
            default: return null;
        }

        return Cache[key];
    }

    private delegate Color32 Painter(float x, float y);

    private static Sprite Build(CarriableType type, Painter paint)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "MpMixerIcon_" + type,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = paint(x + 0.5f, y + 0.5f);

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static readonly Color32 Outline = new Color32(58, 40, 28, 255);
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    // Signed distance to a rounded rectangle (negative inside).
    private static float Box(float x, float y, float x0, float x1, float y0, float y1, float r)
    {
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
        float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
        float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy;
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static float Cov(float d) => Mathf.Clamp01(0.5f - d);

    // Paints a shape with an outline: inside colour over a dark rim.
    private static Color32 Layer(Color32 under, float d, Color32 fill, float rim = 2f)
    {
        float outer = Cov(d);
        if (outer <= 0f) return under;

        float inner = Cov(d + rim);
        Color32 rimColor = Color32.Lerp(under, Outline, outer);
        if (inner <= 0f) return rimColor;

        Color32 filled = Color32.Lerp(rimColor, fill, inner);
        return new Color32(filled.r, filled.g, filled.b, (byte)Mathf.Max(filled.a, 255 * outer));
    }

    private static Color32 DrawWood(float x, float y)
    {
        Color32 c = Clear;
        // Two planks, the upper one shifted.
        c = Layer(c, Box(x, y, 6f, 56f, 8f, 28f, 4f), new Color32(168, 108, 56, 255));
        c = Layer(c, Box(x, y, 12f, 62f, 32f, 52f, 4f), new Color32(190, 128, 68, 255));

        // Grain lines.
        if (Cov(Mathf.Abs(y - 15f) - 0.7f) > 0f && x > 10f && x < 40f) c = Color32.Lerp(c, new Color32(120, 74, 36, 255), 0.8f);
        if (Cov(Mathf.Abs(y - 21f) - 0.7f) > 0f && x > 24f && x < 52f) c = Color32.Lerp(c, new Color32(120, 74, 36, 255), 0.8f);
        if (Cov(Mathf.Abs(y - 39f) - 0.7f) > 0f && x > 18f && x < 48f) c = Color32.Lerp(c, new Color32(140, 88, 42, 255), 0.8f);
        if (Cov(Mathf.Abs(y - 46f) - 0.7f) > 0f && x > 30f && x < 58f) c = Color32.Lerp(c, new Color32(140, 88, 42, 255), 0.8f);
        return c;
    }

    private static Color32 DrawBrick(float x, float y)
    {
        Color32 c = Clear;
        Color32 brick = new Color32(196, 74, 48, 255);
        // Row 1 (two bricks), row 2 (offset: half, full, half is simplified to two shifted bricks).
        c = Layer(c, Box(x, y, 4f, 30f, 36f, 56f, 3f), brick);
        c = Layer(c, Box(x, y, 33f, 60f, 36f, 56f, 3f), brick);
        c = Layer(c, Box(x, y, 12f, 38f, 8f, 28f, 3f), brick);
        c = Layer(c, Box(x, y, 41f, 62f, 8f, 28f, 3f), brick);
        return c;
    }

    private static Color32 DrawGlass(float x, float y)
    {
        Color32 c = Clear;
        c = Layer(c, Box(x, y, 8f, 56f, 8f, 56f, 5f), new Color32(176, 228, 244, 235));

        // Two diagonal shine bands.
        float d1 = Mathf.Abs((x - 8f) - (y - 8f) - 14f) / 1.414f - 3f;
        float d2 = Mathf.Abs((x - 8f) - (y - 8f) - 26f) / 1.414f - 1.4f;
        bool inside = Box(x, y, 8f, 56f, 8f, 56f, 5f) < -2f;
        if (inside && (d1 < 0f || d2 < 0f)) c = Color32.Lerp(c, new Color32(255, 255, 255, 255), 0.75f);
        return c;
    }

    private static Color32 DrawCement(float x, float y)
    {
        Color32 c = Clear;
        c = Layer(c, Box(x, y, 12f, 52f, 6f, 58f, 7f), new Color32(158, 164, 170, 255));

        // Folded top.
        c = Layer(c, Box(x, y, 12f, 52f, 46f, 58f, 5f), new Color32(120, 126, 132, 255));

        // Label band.
        if (Cov(Box(x, y, 18f, 46f, 22f, 36f, 2f)) > 0f) c = Color32.Lerp(c, new Color32(236, 232, 220, 255), Cov(Box(x, y, 18f, 46f, 22f, 36f, 2f)));
        if (Cov(Box(x, y, 24f, 40f, 27f, 31f, 1f)) > 0f) c = Color32.Lerp(c, new Color32(196, 74, 48, 255), Cov(Box(x, y, 24f, 40f, 27f, 31f, 1f)));
        return c;
    }
}
