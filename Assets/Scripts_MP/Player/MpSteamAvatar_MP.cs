using Steamworks;
using UnityEngine;

public static class MpSteamAvatar_MP
{
    private static Texture2D[] textures;
    private static Sprite[] sprites;
    private static ulong[] owners;
    private static bool initialised;

    public static Sprite GetSprite(ulong steamId)
    {
        if (!SteamManager.Initialized || steamId == 0) return null;

        EnsureCache();

        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] == steamId) return sprites[i];
        }

        Sprite sprite = Load(steamId);
        if (sprite == null) return null;

        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] != 0) continue;

            owners[i] = steamId;
            sprites[i] = sprite;
            break;
        }

        return sprite;
    }

    private static Sprite Load(ulong steamId)
    {
        int handle = SteamFriends.GetMediumFriendAvatar(new CSteamID(steamId));

        if (handle <= 0) return null;
        if (!SteamUtils.GetImageSize(handle, out uint width, out uint height)) return null;
        if (width == 0 || height == 0) return null;

        int pixelCount = (int)(width * height);
        var data = new byte[pixelCount * 4];

        if (!SteamUtils.GetImageRGBA(handle, data, data.Length)) return null;

        var pixels = new Color32[pixelCount];

        for (int y = 0; y < height; y++)
        {
            int sourceRow = (int)(height - 1 - y) * (int)width;
            int targetRow = y * (int)width;

            for (int x = 0; x < width; x++)
            {
                int source = (sourceRow + x) * 4;
                pixels[targetRow + x] = new Color32(data[source], data[source + 1], data[source + 2], data[source + 3]);
            }
        }

        MakeRound(pixels, (int)width, (int)height);

        var texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Register(texture);

        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    // The picture is cut to a circle (soft 1.5 px edge), so it fills the round frame in the player list completely.
    private static void MakeRound(Color32[] pixels, int width, int height)
    {
        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;
        float radius = Mathf.Min(width, height) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float alpha = Mathf.Clamp01((radius - distance) / 1.5f);
                if (alpha >= 1f) continue;

                Color32 pixel = pixels[y * width + x];
                pixel.a = (byte)(pixel.a * alpha);
                pixels[y * width + x] = pixel;
            }
        }
    }

    private static void Register(Texture2D texture)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null) continue;

            textures[i] = texture;
            return;
        }
    }

    private static void EnsureCache()
    {
        if (initialised) return;

        initialised = true;
        textures = new Texture2D[16];
        sprites = new Sprite[16];
        owners = new ulong[16];
    }
}
