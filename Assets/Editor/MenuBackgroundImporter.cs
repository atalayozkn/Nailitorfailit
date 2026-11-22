using UnityEditor;

/// <summary>
/// Import settings for the main menu background layers:
/// Sprite (2D and UI), full resolution (4096), no mipmaps, high quality compression.
/// </summary>
public class MenuBackgroundImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Art/UI/MenuBackground/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
