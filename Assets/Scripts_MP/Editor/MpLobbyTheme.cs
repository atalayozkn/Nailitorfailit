#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MpLobbyTheme
{
    private const string ButtonSpritePath = "Assets/Art/UI/UI_Panel_Background_Removed.png";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string AutoAppliedKey = "MpLobbyTheme.Applied.v1";

    private static readonly string[] Targets =
    {
        "Assets/Prefabs_MP/UI/MultiplayerMenu.prefab",
        "Assets/Prefabs_MP/UI/CreateLobbyMenu.prefab",
        "Assets/Prefabs_MP/UI/JoinLobbyMenu.prefab",
        "Assets/Prefabs_MP/UI/LobbyMenu.prefab",
        "Assets/Prefabs_MP/UI/MpMenuRoot.prefab",
    };

    private static readonly Color Normal = new Color(0.9811321f, 0.70993024f, 0.4026344f, 1f);
    private static readonly Color Highlighted = new Color(0.6132076f, 0.44349524f, 0.25164652f, 1f);
    private static readonly Color Pressed = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
    private static readonly Color Selected = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
    private static readonly Color Disabled = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
    private static readonly Color LabelColour = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);

    static MpLobbyTheme()
    {
        EditorApplication.delayCall += AutoApplyOnce;
    }

    private static void AutoApplyOnce()
    {
        string key = AutoAppliedKey + "." + Application.dataPath;

        if (EditorPrefs.GetBool(key, false)) return;

        EditorPrefs.SetBool(key, true);
        Apply();
        Debug.Log("[MP-THEME] lobby UI themed with the singleplayer menu style (runs once; use Tools > MP to re-apply).");
    }

    [MenuItem("Tools/MP/Theme Lobby UI (SP style)")]
    public static void ApplyMenu() => Apply();

    public static void Apply()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (sprite == null || font == null)
        {
            Debug.LogError($"[MP-THEME] theme assets missing (sprite='{ButtonSpritePath}', font='{FontPath}').");
            return;
        }

        int prefabs = 0;
        int buttons = 0;
        int labels = 0;

        foreach (string path in Targets)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            int styled = Style(root.transform, sprite, font, ref buttons, ref labels);

            if (styled > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                prefabs++;
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MP-THEME] {prefabs} prefab(s) updated: {buttons} button(s), {labels} label(s) matched to the SP menu.");
    }

    private static int Style(Transform root, Sprite sprite, TMP_FontAsset font, ref int buttons, ref int labels)
    {
        int styled = 0;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.fontSharedMaterial == font.material) continue;

            text.font = font;
            text.fontSharedMaterial = font.material;
            styled++;
        }

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Normal;
            colors.highlightedColor = Highlighted;
            colors.pressedColor = Pressed;
            colors.selectedColor = Selected;
            colors.disabledColor = Disabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;

            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            Image image = button.targetGraphic as Image;

            if (image == null) image = button.GetComponent<Image>();
            if (image == null) image = button.GetComponentInChildren<Image>(true);

            if (image != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.preserveAspect = false;
                image.material = null;
            }

            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.color = LabelColour;
                labels++;
            }

            buttons++;
            styled++;
        }

        return styled;
    }
}
#endif
