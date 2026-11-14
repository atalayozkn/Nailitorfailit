using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Creates Assets/Scenes/BootScene.unity (loading screen + logos), puts it first in the build list and turns off
// Unity's built-in splash (our own logo screen shows the Unity logo after the loading screen instead).
// Runs once automatically after compiling; can be re-run from Tools > Boot > Setup Boot Scene.
[InitializeOnLoad]
public static class BootSceneSetup
{
    private const string ScenePath = "Assets/Scenes/BootScene.unity";
    private const string AutoRunKey = "NIOFI.Boot.SetupDone.v1";

    private const string BackgroundPath = "Assets/Art/UI/UI_Background_Panel.png";
    private const string HammerPath = "Assets/Art/UI/Icon_Hammer.png";
    private const string StudioLogoPath = "Assets/Art/UI/Icons/PoderGames_Logo_v2.jpeg";

    private const string UnityLogoPath = "Assets/Art/UI/Logos/MadeWithUnity.png";
    private const string UnityLogoKey = "NIOFI.Boot.UnityLogo.v1";

    static BootSceneSetup()
    {
        EditorApplication.delayCall += AutoRun;
        EditorApplication.delayCall += AutoExtractUnityLogo;
    }

    private static void AutoExtractUnityLogo()
    {
        if (EditorPrefs.GetBool(UnityLogoKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!File.Exists(ScenePath)) return; // the scene setup runs first

        if (ExtractUnityLogo()) EditorPrefs.SetBool(UnityLogoKey, true);
    }

    // The official "Made with Unity" logo that the built-in splash screen used (unity_builtin_extra, fileID 10404),
    // saved as a PNG and assigned to BootSequence.unityLogo.
    [MenuItem("Tools/Boot/Extract Unity Logo")]
    public static bool ExtractUnityLogo()
    {
        // #10404 is the Sprite "UnitySplash-Light" (white "MADE WITH Unity" for dark backgrounds); its texture
        // holds the logo on a larger transparent canvas.
        Texture2D logo = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Resources/unity_builtin_extra"))
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId) || localId != 10404) continue;
            logo = asset is Sprite sprite ? sprite.texture : asset as Texture2D;
            break;
        }

        if (logo == null)
        {
            Debug.LogError("[Boot] Built-in Unity splash logo (unity_builtin_extra #10404) not found.");
            return false;
        }

        // Built-in textures aren't readable: copy through a RenderTexture.
        RenderTexture rt = RenderTexture.GetTemporary(logo.width, logo.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(logo, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(logo.width, logo.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, logo.width, logo.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        copy = TrimTransparent(copy);

        Directory.CreateDirectory(Path.GetDirectoryName(UnityLogoPath));
        File.WriteAllBytes(UnityLogoPath, copy.EncodeToPNG());
        Object.DestroyImmediate(copy);
        AssetDatabase.ImportAsset(UnityLogoPath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(UnityLogoPath) is TextureImporter importer)
        {
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        AssignOnBootScene("unityLogo", AssetDatabase.LoadAssetAtPath<Texture>(UnityLogoPath));
        Debug.Log($"[Boot] Unity logo '{logo.name}' saved to {UnityLogoPath} and assigned.");
        return true;
    }

    // Crop the empty transparent border so the logo fills its box on the logo screen.
    private static Texture2D TrimTransparent(Texture2D source)
    {
        Color32[] px = source.GetPixels32();
        int w = source.width, h = source.height, minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 8) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        if (maxX < 0) return source;

        int pad = 8;
        minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(w - 1, maxX + pad); maxY = Mathf.Min(h - 1, maxY + pad);
        var trimmed = new Texture2D(maxX - minX + 1, maxY - minY + 1, TextureFormat.RGBA32, false);
        trimmed.SetPixels(source.GetPixels(minX, minY, trimmed.width, trimmed.height));
        trimmed.Apply();
        Object.DestroyImmediate(source);
        return trimmed;
    }

    // Sets a field on the BootSequence in BootScene, whether the scene is open or not.
    private static void AssignOnBootScene(string field, Object value)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasOpen = scene.IsValid() && scene.isLoaded;
        if (!wasOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            BootSequence boot = root.GetComponentInChildren<BootSequence>(true);
            if (boot == null) continue;
            var so = new SerializedObject(boot);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorSceneManager.SaveScene(scene);
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
    }

    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoRunKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

        Run();
        EditorPrefs.SetBool(AutoRunKey, true);
    }

    [MenuItem("Tools/Boot/Setup Boot Scene")]
    public static void Run()
    {
        if (!File.Exists(ScenePath)) CreateScene();

        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        AssetDatabase.SaveAssets();
        Debug.Log($"[Boot] Boot scene ready: '{ScenePath}' is build index 0, Unity splash off " +
                  $"(next: '{scenes.ElementAtOrDefault(1)?.path}').");
    }

    private static void CreateScene()
    {
        // Additive, so the scenes the user has open (and their unsaved changes) are left alone.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var go = new GameObject("BootSequence");
        SceneManager.MoveGameObjectToScene(go, scene);
        var boot = go.AddComponent<BootSequence>();

        var so = new SerializedObject(boot);
        so.FindProperty("background").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture>(BackgroundPath);
        so.FindProperty("hammerIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture>(HammerPath);
        so.FindProperty("studioLogo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture>(StudioLogoPath);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.Refresh();
    }
}
