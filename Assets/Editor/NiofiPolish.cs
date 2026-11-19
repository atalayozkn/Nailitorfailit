#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-shot polish pass for NIOFI: DynaPuff font, English texts, menu music, title image.
// Everything runs inside the editor, so scenes that are open in the window are saved the normal way.
// Every step is idempotent.
public static class NiofiPolish
{
    const string FontSource = "Assets/Art/Fonts/DynaPuff.ttf";
    const string FontAssetPath = "Assets/Art/Fonts/DynaPuff SDF.asset";
    const string TitleSpritePath = "Assets/Art/UI/Logos/NailItOrFailIt_Title.png";
    const string MenuMusicPath = "Assets/Art/SFX/Music/menu.wav";
    const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    const string MenuScenePath = "Assets/Scenes/MenuScene.unity";

    // Bundled demos, backups and the TMP folder itself never ship.
    static readonly string[] Skip = { "Assets/Plugins/", "Assets/_Recovery/", "Assets/TextMesh Pro/", "Assets/Mirror/" };

    // LiberationSans SDF, the TMP default the game used everywhere.
    static readonly string OldFontGuid = "8f586378b4e144a9851e7b34d9b748ee";

    // Turkish / misspelled strings found in scenes and prefabs.
    static readonly string[,] Replacements =
    {
        { "Ma\u0131n menu", "Main Menu" },
        { "Ex\u0131t", "Exit" },
        { "fa\u0131l \u0131t", "Fail it" },
        { "RENK", "COLOR" },
        { "MIKROFON: HAZIR", "MICROPHONE: READY" },
        { "bas-konus", "push-to-talk" },
        { "sustur", "mute" },
        { "Steam yok", "Steam unavailable" },
    };

    static int textCount;
    static int musicCount;
    static int fileCount;

    [MenuItem("NIOFI/Run all (font + texts + music + title)")]
    public static void RunAll()
    {
        ApplyFont();
        ApplyTextsAndMusic();
        RestoreTitle();
        AssetDatabase.SaveAssets();
        Debug.Log("[NIOFI] bitti: " + fileCount + " dosya, " + textCount + " metin, " + musicCount + " AudioSource.");
    }

    // -------------------------------------------------------------- 1 font
    [MenuItem("NIOFI/1 - DynaPuff font")]
    public static void ApplyFont()
    {
        TMP_FontAsset font = EnsureFontAsset();
        if (font == null) return;

        SetTmpDefaultFont(font);

        textCount = 0;
        fileCount = 0;
        foreach (string path in AffectedFiles(OldFontGuid))
            ProcessFile(path, font, true, false, false);

        Debug.Log("[NIOFI] DynaPuff uygulandi: " + fileCount + " dosya, " + textCount + " TMP bileseni. guid=" +
                  AssetDatabase.AssetPathToGUID(FontAssetPath));
    }

    static TMP_FontAsset EnsureFontAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
        if (font == null)
        {
            // First run after the TTF was copied in: import it, then retry once.
            AssetDatabase.ImportAsset(FontSource, ImportAssetOptions.ForceUpdate);
            font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
        }

        if (font == null)
        {
            Debug.LogError("[NIOFI] TTF import edilmemis: " + FontSource);
            return null;
        }

        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);

        if (fa == null)
        {
            Debug.LogError("[NIOFI] TMP font asset olusturulamadi.");
            return null;
        }

        fa.name = "DynaPuff SDF";
        AssetDatabase.CreateAsset(fa, FontAssetPath);

        if (fa.material != null)
        {
            fa.material.name = "DynaPuff SDF Material";
            if (!AssetDatabase.IsSubAsset(fa.material)) AssetDatabase.AddObjectToAsset(fa.material, fa);
        }

        if (fa.atlasTextures != null)
        {
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                if (fa.atlasTextures[i] == null) continue;
                fa.atlasTextures[i].name = "DynaPuff SDF Atlas " + i;
                if (!AssetDatabase.IsSubAsset(fa.atlasTextures[i]))
                    AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
            }
        }

        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        Debug.Log("[NIOFI] font asset olusturuldu: " + FontAssetPath);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    static void SetTmpDefaultFont(TMP_FontAsset font)
    {
        var settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
        if (settings == null)
        {
            Debug.LogWarning("[NIOFI] TMP Settings bulunamadi.");
            return;
        }

        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop == null) return;

        prop.objectReferenceValue = font;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    // ------------------------------------------------- 2 texts and music
    [MenuItem("NIOFI/2 - Texts (English) + menu music")]
    public static void ApplyTextsAndMusic()
    {
        var needles = new List<string> { OldFontGuid, "Background_Music" };
        for (int i = 0; i < Replacements.GetLength(0); i++) needles.Add(Replacements[i, 0]);

        textCount = 0;
        musicCount = 0;
        fileCount = 0;

        foreach (string path in AffectedFiles(needles.ToArray()))
            ProcessFile(path, null, false, true, true);

        AssetDatabase.SaveAssets();
        Debug.Log("[NIOFI] metin: " + textCount + " TMP bileseni, muzik: " + musicCount + " AudioSource, " +
                  fileCount + " dosya.");
    }

    // ------------------------------------------------------------- 3 title
    [MenuItem("NIOFI/3 - Restore title image")]
    public static void RestoreTitle()
    {
        Scene scene = SceneManager.GetSceneByPath(MenuScenePath);
        bool added = !scene.IsValid() || !scene.isLoaded;
        if (added) scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        var sprite = EnsureSprite(TitleSpritePath);
        Transform titleText = FindByName(scene, "Title_Game");

        if (sprite == null || titleText == null)
        {
            Debug.LogError("[NIOFI] title: sprite=" + (sprite != null) + " Title_Game=" + (titleText != null));
            if (added) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        Transform parent = titleText.parent;
        Transform existing = parent.Find("Title_Image");
        var src = (RectTransform)titleText;

        if (existing == null)
        {
            var go = new GameObject("Title_Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            existing = go.transform;
            existing.SetParent(parent, false);
        }

        var rt = (RectTransform)existing;
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.anchoredPosition = src.anchoredPosition;
        rt.sizeDelta = src.sizeDelta;
        rt.localRotation = src.localRotation;
        rt.localScale = src.localScale;
        rt.SetSiblingIndex(src.GetSiblingIndex());

        var image = existing.GetComponent<Image>();
        if (image == null) image = existing.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        // The old title was a TMP text; hide it so the two do not overlap.
        titleText.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (added) EditorSceneManager.CloseScene(scene, true);

        Debug.Log("[NIOFI] Title_Image eklendi: " + sprite.name + " " + sprite.rect.width + "x" + sprite.rect.height);
    }

    static Transform FindByName(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        }

        return null;
    }

    static Sprite EnsureSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null &&
            (importer.textureType != TextureImporterType.Sprite ||
             importer.spriteImportMode != SpriteImportMode.Single ||
             !importer.alphaIsTransparency))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------------------------------------------------- plumbing
    static string[] AffectedFiles(params string[] needles)
    {
        var result = new List<string>();

        // Two separate searches: Unity ANDs several type filters in one query, so "t:Scene t:Prefab" finds nothing.
        foreach (string typeFilter in new[] { "t:Scene", "t:Prefab" })
        foreach (string guid in AssetDatabase.FindAssets(typeFilter))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            if (!path.StartsWith("Assets/")) continue;
            if (!path.EndsWith(".unity") && !path.EndsWith(".prefab")) continue;

            bool skip = false;
            for (int i = 0; i < Skip.Length; i++)
                if (path.StartsWith(Skip[i])) { skip = true; break; }
            if (skip) continue;

            string content;
            try { content = File.ReadAllText(path); }
            catch { continue; }

            for (int i = 0; i < needles.Length; i++)
                if (content.Contains(needles[i])) { if (!result.Contains(path)) result.Add(path); break; }
        }

        return result.ToArray();
    }

    static void ProcessFile(string path, TMP_FontAsset font, bool fixFont, bool fixTexts, bool fixMusic)
    {
        if (path.EndsWith(".prefab"))
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            int changed = 0;
            try
            {
                changed = Apply(contents.transform, font, fixFont, fixTexts, fixMusic);
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            if (changed > 0) fileCount++;
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(path);
        bool added = !scene.IsValid() || !scene.isLoaded;
        if (added) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        int total = 0;
        foreach (var root in scene.GetRootGameObjects())
            total += Apply(root.transform, font, fixFont, fixTexts, fixMusic);

        if (total > 0)
        {
            fileCount++;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (added) EditorSceneManager.CloseScene(scene, true);
    }

    static int Apply(Transform root, TMP_FontAsset font, bool fixFont, bool fixTexts, bool fixMusic)
    {
        int changed = 0;

        if (fixFont && font != null)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == font) continue;

                text.font = font;
                if (font.material != null) text.fontSharedMaterial = font.material;

                textCount++;
                changed++;
            }
        }

        if (fixTexts)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string current = text.text;
                if (string.IsNullOrEmpty(current)) continue;

                string updated = current;
                for (int i = 0; i < Replacements.GetLength(0); i++)
                    updated = updated.Replace(Replacements[i, 0], Replacements[i, 1]);

                if (updated == current) continue;

                text.text = updated;
                textCount++;
                changed++;
                Debug.Log("[NIOFI] metin: " + Flatten(current) + " -> " + Flatten(updated));
            }
        }

        if (fixMusic)
        {
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
            {
                if (!source.gameObject.name.Contains("Background_Music")) continue;

                bool local = false;

                if (source.clip == null)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusicPath);
                    if (clip == null)
                    {
                        Debug.LogWarning("[NIOFI] menu.wav bulunamadi: " + MenuMusicPath);
                        continue;
                    }

                    source.clip = clip;
                    local = true;
                }

                if (source.volume <= 0.001f) { source.volume = 0.5f; local = true; }
                if (!source.playOnAwake) { source.playOnAwake = true; local = true; }
                if (!source.loop) { source.loop = true; local = true; }

                if (!local) continue;

                musicCount++;
                changed++;
                Debug.Log("[NIOFI] muzik: " + source.gameObject.name + " clip=" +
                          (source.clip != null ? source.clip.name : "yok") + " volume=" + source.volume);
            }
        }

        return changed;
    }

    static string Flatten(string value)
    {
        if (value == null) return "";
        return value.Replace("\r", " ").Replace("\n", " ");
    }
}
#endif
