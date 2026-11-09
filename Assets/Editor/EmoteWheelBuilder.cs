using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the radial emote wheel in the style of the main menu plates (hand-drawn metal plate, thick black
// outline, corner screws, hazard-stripe band; grey normal, yellow highlighted) and wires it into the player package.
// Re-runnable: sprites are redrawn and the existing tabs (with their emote events) are re-laid out.
public static class EmoteWheelBuilder
{
    const string ArtDir = "Assets/Art/UI/EmoteWheel";
    const string PlayerPackage = "Assets/Prefabs/Player/Player_Package_Default.prefab";

    const int WedgeSize = 1024;
    const float InnerR = 200f, OuterR = 500f, Outline = 12f, Band = 54f, GapDeg = 2.2f;
    const float WheelSize = 560f;

    static readonly Color Ink = new Color(0.05f, 0.05f, 0.05f, 1f);
    static readonly Color GreyFill = new Color(0.83f, 0.83f, 0.83f, 1f), GreyBand = new Color(0.70f, 0.70f, 0.70f, 1f);
    static readonly Color GoldFill = new Color(0.965f, 0.808f, 0.431f, 1f), GoldBand = new Color(0.86f, 0.66f, 0.33f, 1f);
    static readonly Color LockFill = new Color(0.56f, 0.56f, 0.56f, 1f), LockBand = new Color(0.46f, 0.46f, 0.46f, 1f);
    static readonly Color ScrewFill = new Color(0.5f, 0.5f, 0.5f, 1f);
    static readonly Color TextColor = new Color(0.196f, 0.196f, 0.196f, 1f);

    [MenuItem("Tools/NailIt/Emote Wheel/Rebuild Radial Wheel")]
    public static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPackage);
        try
        {
            if (BuildOn(root)) PrefabUtility.SaveAsPrefabAsset(root, PlayerPackage);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // For scenes whose player is not linked to the player package prefab (the tutorial).
    public static int BuildInOpenScene()
    {
        int built = 0;
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var wheel in go.GetComponentsInChildren<EmoteWheel>(true))
                if (!PrefabUtility.IsPartOfPrefabInstance(wheel) && BuildOn(wheel.gameObject)) built++;
        return built;
    }

    static bool BuildOn(GameObject root)
    {
        {
            var wheel = root.GetComponentInChildren<EmoteWheel>(true);
            if (wheel == null) { Debug.LogError("[EmoteWheel] No EmoteWheel under " + root.name); return false; }
            var so = new SerializedObject(wheel);
            var holder = (GameObject)so.FindProperty("canvasRoot").objectReferenceValue;
            var tabsProp = so.FindProperty("emoteTabs");
            int n = tabsProp.arraySize;

            if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/Art/UI", "EmoteWheel");
            var normal = SaveSprite($"Wedge{n}_Normal", DrawWedge(n, GreyFill, GreyBand, true));
            var selected = SaveSprite($"Wedge{n}_Selected", DrawWedge(n, GoldFill, GoldBand, true));
            var locked = SaveSprite($"Wedge{n}_Locked", DrawWedge(n, LockFill, LockBand, false));
            var hub = SaveSprite("Hub", DrawHub());
            var lockIcon = SaveSprite("Lock", DrawLock());
            var font = TMP_Settings.defaultFontAsset;

            wheel.gameObject.SetActive(true);
            var holderRt = (RectTransform)holder.transform;
            Center(holderRt, new Vector2(WheelSize, WheelSize));
            var holderImage = holder.GetComponent<Image>();
            if (holderImage != null) holderImage.enabled = false;

            float labelR = ((InnerR + OuterR) * 0.5f - 28f) / WedgeSize * WheelSize; // a little inward, clear of the stripe band
            for (int i = 0; i < n; i++)
            {
                var tab = (EmoteTab)tabsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                float angle = -i * 360f / n; // tab 0 at the top, then clockwise
                var rt = (RectTransform)tab.transform;
                rt.SetParent(holderRt, false);
                Center(rt, new Vector2(WheelSize, WheelSize));
                rt.localRotation = Quaternion.Euler(0, 0, angle);

                var img = tab.GetComponent<Image>() ?? tab.gameObject.AddComponent<Image>();
                img.sprite = normal; img.type = Image.Type.Simple; img.preserveAspect = true;
                img.color = Color.white; img.raycastTarget = false;

                var tso = new SerializedObject(tab);
                bool isLocked = tso.FindProperty("onSelected.m_PersistentCalls.m_Calls").arraySize == 0;

                var label = tab.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label == null && !isLocked)
                {
                    label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                    label.transform.SetParent(rt, false);
                }
                RectTransform anchor = null;
                if (label != null)
                {
                    label.gameObject.SetActive(!isLocked);
                    Upright((RectTransform)label.transform, labelR, angle, new Vector2(170, 60));
                    if (font != null) label.font = font;
                    label.fontSize = 34; label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 36;
                    label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                    label.color = TextColor; label.alignment = TextAlignmentOptions.Center;
                    label.raycastTarget = false;
                    if (!isLocked) anchor = (RectTransform)label.transform;
                }

                var lockTf = rt.Find("Lock");
                if (isLocked)
                {
                    var lockImg = lockTf != null ? lockTf.GetComponent<Image>() : new GameObject("Lock", typeof(RectTransform)).AddComponent<Image>();
                    lockImg.transform.SetParent(rt, false);
                    lockImg.sprite = lockIcon; lockImg.preserveAspect = true; lockImg.raycastTarget = false;
                    Upright((RectTransform)lockImg.transform, labelR, angle, new Vector2(64, 64));
                    anchor = (RectTransform)lockImg.transform;
                }
                else if (lockTf != null) Object.DestroyImmediate(lockTf.gameObject);

                tso.FindProperty("background").objectReferenceValue = img;
                tso.FindProperty("normalSprite").objectReferenceValue = normal;
                tso.FindProperty("selectedSprite").objectReferenceValue = selected;
                tso.FindProperty("lockedSprite").objectReferenceValue = locked;
                tso.FindProperty("anchor").objectReferenceValue = anchor;
                tso.FindProperty("label").objectReferenceValue = isLocked ? null : label;
                tso.ApplyModifiedPropertiesWithoutUndo();
                if (isLocked) img.sprite = locked;
            }

            // Hub plate in the middle, drawn over the inner ends of the segments, with the highlighted emote's name.
            var hubTf = holderRt.Find("Hub");
            var hubImg = hubTf != null ? hubTf.GetComponent<Image>() : new GameObject("Hub", typeof(RectTransform)).AddComponent<Image>();
            hubImg.transform.SetParent(holderRt, false);
            hubImg.sprite = hub; hubImg.preserveAspect = true; hubImg.raycastTarget = false;
            Center((RectTransform)hubImg.transform, new Vector2(240, 240));
            hubImg.transform.SetAsLastSibling();

            var hubLabelTf = hubImg.transform.Find("HubLabel");
            var hubLabel = hubLabelTf != null ? hubLabelTf.GetComponent<TextMeshProUGUI>() : new GameObject("HubLabel", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            hubLabel.transform.SetParent(hubImg.transform, false);
            Center((RectTransform)hubLabel.transform, new Vector2(170, 70));
            if (font != null) hubLabel.font = font;
            hubLabel.text = "EMOTE";
            hubLabel.enableAutoSizing = true; hubLabel.fontSizeMin = 16; hubLabel.fontSizeMax = 32;
            hubLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            hubLabel.color = TextColor; hubLabel.alignment = TextAlignmentOptions.Center; hubLabel.raycastTarget = false;

            so.FindProperty("centerLabel").objectReferenceValue = hubLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            holder.SetActive(false);
            Debug.Log($"[EmoteWheel] Built {n} segments under {root.name}");
            return true;
        }
    }

    static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    // Places a child at radius r along the tab's up axis and cancels the tab's turn so it reads upright.
    static void Upright(RectTransform rt, float r, float tabAngle, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, r);
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.Euler(0, 0, -tabAngle);
    }

    #region Drawing

    delegate Color Shader2D(float x, float y);

    // 2x2 supersampled so the hand-drawn edges stay smooth.
    static Texture2D Render(int size, Shader2D shade)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color acc = Color.clear;
                for (int s = 0; s < 4; s++)
                {
                    var c = shade(x + 0.25f + 0.5f * (s & 1), y + 0.25f + 0.5f * (s >> 1));
                    acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
                acc /= 4f;
                px[y * size + x] = acc.a > 0f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a) : Color.clear;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D DrawWedge(int n, Color fill, Color band, bool stripes)
    {
        float half = 180f / n - GapDeg;
        float c = WedgeSize * 0.5f;
        float screwAngle = half - 9f, screwR = OuterR - 92f;
        return Render(WedgeSize, (x, y) =>
        {
            float dx = x - c, dy = y - c;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg; // 0 = up
            if (r < InnerR || r > OuterR || Mathf.Abs(a) > half) return Color.clear;

            float side = (half - Mathf.Abs(a)) * Mathf.Deg2Rad * r;
            float edge = Mathf.Min(Mathf.Min(r - InnerR, OuterR - r), side);
            if (edge < Outline) return Ink;

            foreach (float sa in new[] { -screwAngle, screwAngle })
            {
                var col = Screw(dx, dy, screwR, sa);
                if (col.a > 0f) return col;
            }

            float fromOuter = OuterR - r;
            if (fromOuter < Band)
            {
                // Hazard stripes along the middle of the outer band, like the menu plates' bottom edge.
                if (stripes && Mathf.Abs(a) < half * 0.6f && fromOuter > Outline + 6f && fromOuter < Band - 8f)
                {
                    float u = a * Mathf.Deg2Rad * r + fromOuter;
                    if (Mathf.Repeat(u, 46f) < 21f) return Ink;
                }
                return band;
            }
            return fill;
        });
    }

    static Color Screw(float dx, float dy, float radius, float angleDeg)
    {
        float sx = Mathf.Sin(angleDeg * Mathf.Deg2Rad) * radius, sy = Mathf.Cos(angleDeg * Mathf.Deg2Rad) * radius;
        float px = dx - sx, py = dy - sy;
        float d = Mathf.Sqrt(px * px + py * py);
        if (d > 17f) return Color.clear;
        if (d > 13f) return Ink;
        if ((Mathf.Abs(px) < 1.8f && Mathf.Abs(py) < 9f) || (Mathf.Abs(py) < 1.8f && Mathf.Abs(px) < 9f)) return Ink;
        return ScrewFill;
    }

    static Texture2D DrawHub()
    {
        const int size = 512; const float a = 210f;
        float c = size * 0.5f;
        return Render(size, (x, y) =>
        {
            float dx = x - c, dy = y - c;
            float oct = Mathf.Max(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)), (Mathf.Abs(dx) + Mathf.Abs(dy)) * 0.7071f * 1.08f);
            if (oct > a) return Color.clear;
            if (oct > a - Outline) return Ink;
            foreach (var s in new[] { new Vector2(-105, 105), new Vector2(105, 105), new Vector2(-105, -95), new Vector2(105, -95) })
            {
                var col = Screw(dx - s.x, dy - s.y, 0f, 0f);
                if (col.a > 0f) return col;
            }
            if (dy < -a + Band + Outline)
            {
                if (dx > -60f && dx < 70f &&dy > -a + Outline + 6f && dy < -a + Band + Outline - 8f && Mathf.Repeat(dx + dy, 46f) < 21f) return Ink;
                return GreyBand;
            }
            return GreyFill;
        });
    }

    static Texture2D DrawLock()
    {
        const int size = 128; float c = size * 0.5f;
        return Render(size, (x, y) =>
        {
            float dx = x - c, dy = y - c;
            // Body: rounded box.
            float bx = Mathf.Abs(dx) - 34f, by = Mathf.Abs(dy + 14f) - 28f;
            float body = Mathf.Sqrt(Mathf.Max(bx, 0) * Mathf.Max(bx, 0) + Mathf.Max(by, 0) * Mathf.Max(by, 0)) + Mathf.Min(Mathf.Max(bx, by), 0) - 6f;
            if (body < -7f)
            {
                float kx = dx, ky = dy + 10f;
                if (kx * kx + ky * ky < 64f || (Mathf.Abs(kx) < 3.5f && ky < 0f && ky > -18f)) return Ink; // keyhole
                return LockFill;
            }
            if (body < 0f) return Ink;
            // Shackle: thick arc above the body.
            float sy = dy - 22f;
            float sr = Mathf.Sqrt(dx * dx + sy * sy);
            if (sy > -12f && sr > 15f && sr < 31f) return (sr < 19f || sr > 27f) ? Ink : ScrewFill;
            return Color.clear;
        });
    }

    static Sprite SaveSprite(string name, Texture2D tex)
    {
        string path = $"{ArtDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    #endregion
}
