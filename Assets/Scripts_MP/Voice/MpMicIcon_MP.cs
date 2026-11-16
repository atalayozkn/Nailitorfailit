using UnityEngine;

// Microphone icon drawn in code (no art asset needed): a mic when it is open, the same mic with a slash when muted.
// White on transparent, tint it with Image.color. Used next to the player's name in the lobby list and on the
// name tag above each player in a match.
public static class MpMicIcon_MP
{
    private const int Size = 64;

    private static Sprite open;
    private static Sprite muted;

    public static Sprite Open => open != null ? open : open = Build(false);
    public static Sprite Muted => muted != null ? muted : muted = Build(true);

    public static Sprite For(bool isMuted) => isMuted ? Muted : Open;

    private static Sprite crown;
    public static Sprite Crown => crown != null ? crown : crown = BuildCrown();

    // Host marker: three-pointed crown with a band. White, tint it gold.
    private static Sprite BuildCrown()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "MpCrownIcon",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        // Top edge of the crown between the tips: (x, y) corners from left to right.
        float[] xs = { 10f, 20f, 32f, 44f, 54f };
        float[] ys = { 48f, 30f, 46f, 30f, 48f };
        var pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;

                // Distance (negative inside) to the crown body: area under the zig-zag, above the band bottom.
                float top = ys[0];
                for (int i = 0; i < xs.Length - 1; i++)
                {
                    if (px < xs[i] || px > xs[i + 1]) continue;
                    top = Mathf.Lerp(ys[i], ys[i + 1], (px - xs[i]) / (xs[i + 1] - xs[i]));
                    break;
                }

                float inside = Mathf.Max(Mathf.Max(xs[0] - px, px - xs[4]), Mathf.Max(11f - py, (py - top) * 0.8f));
                float body = Coverage(inside);

                // Jewels on the three tips.
                float tips = 0f;
                for (int i = 0; i < 5; i += 2)
                {
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(xs[i], ys[i] + 2f)) - 3.4f;
                    tips = Mathf.Max(tips, Coverage(d));
                }

                // A thin gap that separates the band from the points.
                float gap = Mathf.Abs(py - 21f) - 1.1f;
                float shape = Mathf.Max(body * Smooth(gap), tips);

                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(shape * 255f), 0, 255));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite Build(bool slash)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = slash ? "MpMicIcon_Muted" : "MpMicIcon_Open",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float body = Mathf.Max(CapsuleCoverage(px, py), 0f);
                float cradle = CradleCoverage(px, py);
                float stand = StandCoverage(px, py);
                float shape = Mathf.Max(body, Mathf.Max(cradle, stand));

                if (slash)
                {
                    // A transparent gap on both sides of the slash, then the slash itself.
                    float d = DistanceToSlash(px, py);
                    shape *= Smooth(d - 5.5f);
                    shape = Mathf.Max(shape, 1f - Smooth(d - 2.2f));
                }

                byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(shape * 255f), 0, 255);
                pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Soft 1-pixel edge: 0 inside (negative distance), 1 outside.
    private static float Smooth(float distance) => Mathf.Clamp01(distance + 0.5f);

    private static float Coverage(float distance) => 1f - Smooth(distance);

    // The mic head: a capsule from y=27 to y=48, 9 px radius.
    private static float CapsuleCoverage(float x, float y)
    {
        const float cx = 32f, radius = 9f, y0 = 27f, y1 = 48f;
        float cy = Mathf.Clamp(y, y0, y1);
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - radius;
        return Coverage(d);
    }

    // The U-shaped holder under the head: lower half of a ring.
    private static float CradleCoverage(float x, float y)
    {
        const float cx = 32f, cy = 33f, radius = 15f, half = 1.6f;
        if (y > cy + 1f) return 0f;
        float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - radius) - half;
        return Coverage(d);
    }

    // Stem and base.
    private static float StandCoverage(float x, float y)
    {
        float stem = Mathf.Max(Mathf.Abs(x - 32f) - 1.6f, Mathf.Max(6f - y, y - 18.5f));
        float baseBar = Mathf.Max(Mathf.Abs(x - 32f) - 10f, Mathf.Max(Mathf.Abs(y - 7.5f) - 1.6f, 0f));
        return Mathf.Max(Coverage(stem), Coverage(baseBar));
    }

    // Distance to the diagonal from bottom-left to top-right.
    private static float DistanceToSlash(float x, float y)
    {
        Vector2 a = new Vector2(10f, 8f);
        Vector2 b = new Vector2(54f, 56f);
        Vector2 p = new Vector2(x, y);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
