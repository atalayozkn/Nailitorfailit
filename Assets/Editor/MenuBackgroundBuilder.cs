using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the animated main menu background (layered sprites from Assets/Art/UI/MenuBackground)
/// into the open scene and saves it as a prefab.
/// Layer rects are in source-art pixels (4096x2160 canvas, origin top-left).
/// </summary>
public static class MenuBackgroundBuilder
{
    private const string ArtFolder = "Assets/Art/UI/MenuBackground/";
    private const string PrefabPath = "Assets/Prefabs/UI/MenuBackground.prefab";
    private const float ArtW = 4096f, ArtH = 2160f;

    // Framing copied from the old static MenuBackground_NoSign image in MenuScene
    // (shown at 2568.61 x 1512.78 at (-1.35, 58.39) on a 1920x1080, match-width canvas).
    // That image is this art cropped at (120, 24), so 1 art px = 2568.61 / 3957 units.
    private const float RootScale = 2568.61f / 3957f;
    // x shifted left from the old framing (-34.13) so the lamp and notes on the right pillar are in view:
    // the screen's right edge now falls at art x ~3755 (art is solid up to ~3771 at every parallax offset),
    // the left edge at art x ~797.
    private static readonly Vector2 RootPosition = new Vector2(-148f, 66.50f);

    // The menu already has its own Title_Sign + Title_Image, so the background sign layer is off.
    private static readonly bool IncludeSign = false;

    // Parallax strengths (art px at the screen edge). Toned down twice for a calmer feel:
    // first version x0.6, then x0.75 (= 45% of the first version).
    private static readonly Vector2 SkyParallax = new Vector2(4.5f, 2.25f);
    private static readonly Vector2 FarParallax = new Vector2(11.25f, 3.75f);
    private static readonly Vector2 MidParallax = new Vector2(20.25f, 5.25f);

    // Everything the menu animation adds to the scene lives under this root object.
    private const string GroupName = "Menu_Animation";

    [MenuItem("Tools/Menu Background/Build In Open Scene")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();

        // Remove a previous build (old builds sat at the scene root, newer ones under the group).
        var legacy = FindRoot(scene, "Canvas_MenuBackground");
        if (legacy != null) Undo.DestroyObjectImmediate(legacy);
        var group = FindRoot(scene, GroupName);
        if (group == null)
        {
            group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Create Menu_Animation");
        }
        var existing = group.transform.Find("Canvas_MenuBackground");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        // Canvas: own overlay canvas drawn behind the menu UI, same scaling as the menu canvas.
        var canvasGo = new GameObject("Canvas_MenuBackground", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Build Menu Background");
        canvasGo.transform.SetParent(group.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        var backdrop = CreateStretch("Backdrop", canvasGo.transform);
        var backdropImg = backdrop.gameObject.AddComponent<Image>();
        backdropImg.color = new Color32(0x1B, 0x15, 0x12, 0xFF);
        backdropImg.raycastTarget = false;

        // Root: the art canvas in art pixels, scaled/placed to match the old static background.
        var root = CreateRect("BG_Root", canvasGo.transform);
        root.sizeDelta = new Vector2(ArtW, ArtH);
        root.localScale = Vector3.one * RootScale;
        root.anchoredPosition = RootPosition;

        // --- Sky (masks the clouds to the sky shape) ---
        var lSky = CreateLayer("L_Sky", root, SkyParallax);
        var sky = CreateSprite("BG_Sky", lSky, 118, 19, 3962, 1187);
        var skyMask = sky.gameObject.AddComponent<Mask>();
        skyMask.showMaskGraphic = true;
        AddIntro(sky, 0f, 0.6f, Vector2.zero, UIIntro.EaseType.OutCubic);

        var clouds = CreateStretch("Clouds", sky);
        AddDrift(CreateSpriteIn("BG_Clouds", clouds, sky, 2053, 221, 1396, 479), 12f);
        AddDrift(CreateSpriteIn("Cloud_A", clouds, sky, 500, 160, 678, 288), 28f);
        AddDrift(CreateSpriteIn("Cloud_B", clouds, sky, 1700, 420, 394, 238), 20f);
        AddDrift(CreateSpriteIn("Cloud_C", clouds, sky, 3000, 640, 362, 96), 36f);

        // --- Far: sand, whole fence (incl. its lower/left pieces, so it never splits), skyline,
        // trees (extended behind the container). Crane/building split out below. ---
        var lFar = CreateLayer("L_Far", root, FarParallax);
        AddIntro(CreateSprite("BG_Far", lFar, 390, 578, 2686, 1582), 0.15f, 0.6f, new Vector2(0, -30), UIIntro.EaseType.OutCubic);

        // --- Crane + building under construction. Moves with the Mid layer (the container in front
        // of it) so the sky never shows below it, and sits a bit lower so its base stays hidden.
        const float buildingDrop = 40f;
        var lBuilding = CreateLayer("L_Building", root, MidParallax);
        AddIntro(CreateSprite("BG_Building", lBuilding, 2712, 164 + buildingDrop, 868, 621), 0.3f, 0.6f, new Vector2(0, -40), UIIntro.EaseType.OutCubic);

        // --- Dog peeking left from behind the yellow container (between Far and Mid, moves with Mid).
        // Mask's right edge = container's left edge (art x 2447); sprite is mirrored to face left.
        const float containerEdge = 2447f;
        const float dogW = 166f, dogH = 281f; // dog.png crop (301x511) at 0.55
        var lDog = CreateLayer("L_DogPeek", root, MidParallax);
        var dogMask = CreateArtRect("DogMask", lDog, 1900, 780, containerEdge - 1900, 520);
        dogMask.gameObject.AddComponent<RectMask2D>();
        var dog = CreateSpriteIn("Dog_Peek", dogMask, dogMask, containerEdge - dogW + 5, 905, dogW, dogH);
        dog.localScale = new Vector3(-1f, 1f, 1f);
        var peek = dog.gameObject.AddComponent<UIPeek>();
        SetValue(peek, "hiddenOffset", new Vector2(dogW + 25f, 0f));
        SetValue(peek, "peekTilt", 3f);
        // At most one peek every ~15 s: wait 15-20 s hidden between peeks.
        SetValue(peek, "hiddenTime", new Vector2(15f, 20f));

        // --- Mid: container, wheelbarrow, cones, post ---
        var lMid = CreateLayer("L_Mid", root, MidParallax);
        AddIntro(CreateSprite("BG_Mid", lMid, 290, 27, 3290, 2133), 0.3f, 0.6f, new Vector2(0, -40), UIIntro.EaseType.OutCubic);

        // --- Front: desk, sleeping dog, lamp. No parallax: stays fixed, only fades in. ---
        var lFront = CreateLayer("L_Front", root, Vector2.zero);
        AddIntro(CreateSprite("BG_Front", lFront, 416, 63, 3453, 2097), 0.45f, 0.7f, Vector2.zero, UIIntro.EaseType.OutCubic);
        var glow = CreateSprite("LampGlow", lFront, 3598 - 300, 512 - 300, 600, 600);
        glow.GetComponent<Image>().color = new Color(1f, 0.85f, 0.55f, 0.75f);
        glow.gameObject.AddComponent<UIFlicker>();

        // --- Sign: drops in, then sways (only when the menu has no title sign of its own) ---
        if (IncludeSign)
        {
            var lSign = CreateLayer("L_Sign", root, new Vector2(55, 12));
            var signDrop = CreateArtRect("Sign_Drop", lSign, 445, 216, 1502, 787);
            AddIntro(signDrop, 0.8f, 0.9f, new Vector2(0, 1400), UIIntro.EaseType.OutBounce);
            var sign = CreateSpriteIn("BG_Sign", signDrop, signDrop, 445, 216, 1502, 787);
            sign.pivot = new Vector2(0.5f, 1f); // sway from the top
            sign.anchoredPosition += new Vector2(0, sign.sizeDelta.y * 0.5f);
            var fl = sign.gameObject.AddComponent<UIFloat>();
            SetValue(fl, "moveAmplitude", new Vector2(0f, 6f));
            SetValue(fl, "rotationAmplitude", 1f);
        }

        // Save as prefab and keep the scene instance connected.
        System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
        PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGo, PrefabPath, InteractionMode.UserAction);

        HookUpExistingMenu(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvasGo;
        Debug.Log($"[MenuBackground] Built in '{scene.name}' and saved prefab at {PrefabPath}");
    }

    /// <summary>
    /// Hides the old static background images and gives the existing title sign
    /// a drop-in intro and a gentle sway.
    /// </summary>
    private static void HookUpExistingMenu(Scene scene)
    {
        var menuCanvas = FindRoot(scene, "Canvas_Overlay_MainMenu");
        if (menuCanvas == null) return;

        foreach (var img in menuCanvas.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null || img.sprite.name != "MenuBackground_NoSign") continue;
            Undo.RecordObject(img.gameObject, "Hide static menu background");
            img.gameObject.SetActive(false);
        }

        foreach (string path in new[] { "Panel_Menu/MainMenu/Title_Sign", "Panel_Menu/MainMenu/Title_Image" })
        {
            var t = menuCanvas.transform.Find(path);
            if (t == null) continue;
            var rt = (RectTransform)t;

            if (t.GetComponent<UIIntro>() == null)
            {
                var intro = Undo.AddComponent<UIIntro>(t.gameObject);
                SetValue(intro, "delay", 0.8f);
                SetValue(intro, "duration", 0.9f);
                SetValue(intro, "fromOffset", new Vector2(0f, 700f));
                var so = new SerializedObject(intro);
                so.FindProperty("ease").enumValueIndex = (int)UIIntro.EaseType.OutBounce;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Rotation-only sway, in sync for sign and title (they share nearly the same center).
            if (t.GetComponent<UIFloat>() == null)
            {
                var fl = Undo.AddComponent<UIFloat>(t.gameObject);
                SetValue(fl, "moveAmplitude", Vector2.zero);
                SetValue(fl, "rotationAmplitude", 1.2f);
                SetValue(fl, "rotationFrequency", 0.25f);
                var so = new SerializedObject(fl);
                so.FindProperty("randomPhase").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // ---------- helpers ----------

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static RectTransform CreateStretch(string name, Transform parent)
    {
        var rt = CreateRect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform CreateLayer(string name, RectTransform root, Vector2 parallax)
    {
        var rt = CreateStretch(name, root);
        if (parallax == Vector2.zero) return rt; // fixed layer
        var p = rt.gameObject.AddComponent<UIParallaxLayer>();
        SetValue(p, "strength", parallax);
        return rt;
    }

    // Rect positioned by art pixels, relative to BG_Root (full art canvas).
    private static RectTransform CreateArtRect(string name, RectTransform parent, float x, float y, float w, float h)
    {
        var rt = CreateRect(name, parent);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x + w * 0.5f - ArtW * 0.5f, ArtH * 0.5f - (y + h * 0.5f));
        return rt;
    }

    private static RectTransform CreateSprite(string name, RectTransform parent, float x, float y, float w, float h)
    {
        var rt = CreateArtRect(name, parent, x, y, w, h);
        AddImage(rt, name);
        return rt;
    }

    // Sprite placed by art pixels inside a parent that itself was placed by art pixels.
    private static RectTransform CreateSpriteIn(string name, RectTransform parent, RectTransform artSpace, float x, float y, float w, float h)
    {
        var rt = CreateRect(name, parent);
        rt.sizeDelta = new Vector2(w, h);
        Vector2 artCenter = new Vector2(x + w * 0.5f - ArtW * 0.5f, ArtH * 0.5f - (y + h * 0.5f));
        rt.anchoredPosition = artCenter - artSpace.anchoredPosition;
        AddImage(rt, name);
        return rt;
    }

    private static void AddImage(RectTransform rt, string spriteName)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + spriteName + ".png");
        img.raycastTarget = false;
        if (img.sprite == null) Debug.LogWarning($"[MenuBackground] Sprite not found: {spriteName}");
    }

    private static void AddDrift(RectTransform rt, float speed)
    {
        var d = rt.gameObject.AddComponent<UIDrift>();
        SetValue(d, "speed", speed);
    }

    private static void AddIntro(RectTransform rt, float delay, float duration, Vector2 from, UIIntro.EaseType ease)
    {
        var intro = rt.gameObject.AddComponent<UIIntro>();
        SetValue(intro, "delay", delay);
        SetValue(intro, "duration", duration);
        SetValue(intro, "fromOffset", from);
        var so = new SerializedObject(intro);
        so.FindProperty("ease").enumValueIndex = (int)ease;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetValue(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetValue(Object target, string field, Vector2 value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).vector2Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
