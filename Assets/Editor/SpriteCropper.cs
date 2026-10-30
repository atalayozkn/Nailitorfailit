using System.IO;
using UnityEditor;
using UnityEngine;

// Some UI art is exported as a small element on a big transparent canvas, which makes the RectTransform
// (and the button's click area) as big as the canvas. This writes a copy cropped to the visible pixels
// into a "Cropped" folder next to the source, leaving the original untouched.
// Select textures > Assets > NailIt > Crop Transparent Padding
public static class SpriteCropper
{
    private const byte AlphaThreshold = 8;
    private const int Padding = 2;

    [MenuItem("Assets/NailIt/Crop Transparent Padding")]
    private static void CropSelected()
    {
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (path.EndsWith(".png")) Crop(path);
        }
        AssetDatabase.Refresh();
    }

    // Returns the cropped asset path, or null when nothing needed cropping.
    // sourceRect is the cropped area in source pixels (bottom-left origin).
    public static string Crop(string sourcePath, out RectInt sourceRect)
    {
        sourceRect = default;
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!source.LoadImage(File.ReadAllBytes(sourcePath))) return null;

            if (!FindOpaqueBounds(source, out RectInt bounds)) return null;
            sourceRect = bounds;

            var cropped = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false);
            cropped.SetPixels(source.GetPixels(bounds.x, bounds.y, bounds.width, bounds.height));
            cropped.Apply();

            string dir = Path.Combine(Path.GetDirectoryName(sourcePath), "Cropped").Replace('\\', '/');
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string outPath = dir + "/" + Path.GetFileName(sourcePath);
            File.WriteAllBytes(outPath, cropped.EncodeToPNG());
            Object.DestroyImmediate(cropped);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return outPath;
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

    public static string Crop(string sourcePath)
    {
        return Crop(sourcePath, out _);
    }

    private static bool FindOpaqueBounds(Texture2D tex, out RectInt bounds)
    {
        Color32[] px = tex.GetPixels32();
        int minX = tex.width, minY = tex.height, maxX = -1, maxY = -1;
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                if (px[y * tex.width + x].a <= AlphaThreshold) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        bounds = default;
        if (maxX < 0) return false;

        minX = Mathf.Max(0, minX - Padding);
        minY = Mathf.Max(0, minY - Padding);
        maxX = Mathf.Min(tex.width - 1, maxX + Padding);
        maxY = Mathf.Min(tex.height - 1, maxY + Padding);
        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }
}
