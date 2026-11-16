using System.Collections.Generic;
using PlayerScripts;
using UnityEngine;
using UnityEngine.Rendering;

// Paints the character with the colour its owner picked in the lobby.
//
// The character has one material whose texture is a shared palette atlas (tiny flat swatches for skin, suit, hat,
// shoes...), so a plain _BaseColor multiply would turn the orange suit muddy brown. Instead every vivid swatch
// (the suit, the hard hat) is moved to the picked colour's hue while its saturation and brightness shading is kept;
// the muted ones (skin, hair, dark details) stay as they are. The recoloured atlas is built once per colour and
// shared by every player that picked it.
public static class MpPlayerTint_MP
{
    // Swatches at least this vivid and bright count as clothing.
    private const float MinSaturation = 0.55f;
    private const float MinValue = 0.30f;

    // How vivid the original suit swatch is; the picked colour's own saturation scales relative to this.
    private const float ReferenceSaturation = 0.62f;

    private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();
    private const string AtlasPrefix = "MpPlayerAtlas_";
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    public static void Apply(PlayerNetwork_MP player)
    {
        if (player == null || player.colorIndex < 0) return;

        // Headless test clients have no GPU: nothing to blit, nothing to see.
        if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

        foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material source = renderer.sharedMaterial;
            if (source == null || !source.HasProperty(BaseMapId)) continue;

            Texture2D atlas = source.GetTexture(BaseMapId) as Texture2D;
            if (atlas == null) continue;

            // Already painted (OnStartClient and the colour hook both call this): the atlas is a recoloured copy.
            if (atlas.name.StartsWith(AtlasPrefix)) continue;

            Texture2D tinted = GetTinted(player.colorIndex, atlas);
            if (tinted == null) continue;

            Material instance = renderer.material; // own copy: the prefab's shared material stays orange
            instance.SetTexture(BaseMapId, tinted);
            if (instance.HasProperty(MainTexId)) instance.SetTexture(MainTexId, tinted);
        }
    }

    private static Texture2D GetTinted(int colorIndex, Texture2D atlas)
    {
        int key = MpPlayerColor_MP.Clamp(colorIndex);
        if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        Color[] pixels = ReadPixels(atlas, out int width, out int height);
        if (pixels == null) return null;

        Color target = MpPlayerColor_MP.ColorOf(key);
        Color.RGBToHSV(target, out float targetHue, out float targetSaturation, out _);
        float saturationScale = Mathf.Clamp(targetSaturation / ReferenceSaturation, 0f, 1.35f);

        for (int i = 0; i < pixels.Length; i++)
        {
            Color.RGBToHSV(pixels[i], out float h, out float s, out float v);
            if (s < MinSaturation || v < MinValue) continue;

            Color shifted = Color.HSVToRGB(targetHue, Mathf.Clamp01(s * saturationScale), v);
            shifted.a = pixels[i].a;
            pixels[i] = shifted;
        }

        var result = new Texture2D(width, height, TextureFormat.RGBA32, true)
        {
            name = AtlasPrefix + key,
            filterMode = atlas.filterMode,
            wrapMode = atlas.wrapMode,
            anisoLevel = atlas.anisoLevel,
        };
        result.SetPixels(pixels);
        result.Apply(true, true);

        Cache[key] = result;
        return result;
    }

    // The atlas is not marked readable, so copy it through a temporary render texture.
    private static Color[] ReadPixels(Texture2D source, out int width, out int height)
    {
        width = source.width;
        height = source.height;

        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;

        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            Color[] pixels = copy.GetPixels();
            Object.Destroy(copy);
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }
}
