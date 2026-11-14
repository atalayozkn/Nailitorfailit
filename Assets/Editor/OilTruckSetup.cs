using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Oil truck hazard: generates the oil decal texture + material, the OilSlick prefab and the
// Car_Active_OilTruck prefab (built like the other car variants). Every step is safe to run again.
public static class OilTruckSetup
{
    private const string TruckModel =
        "Assets/Art/PANDANIN/PANDANIN DÜZELTECEĞİ MODELLER TARIK/kamaz_fuel_truck_orange.fbx";
    private const string BaseCarPrefab = "Assets/Prefabs/Props/Cars/Car_Active.prefab";
    private const string TruckPrefab = "Assets/Prefabs/Props/Cars/Car_Active_OilTruck.prefab";
    private const string HazardFolder = "Assets/Prefabs/Props/Hazards";
    private const string SlickPrefab = HazardFolder + "/OilSlick.prefab";
    private const string OilFolder = "Assets/Art/Textures/Oil";
    private const string OilTexture = OilFolder + "/OilSlick_Decal.png";
    private const string OilMaterial = OilFolder + "/OilSlick_Decal.mat";

    private static readonly string[] Levels =
    {
        "Assets/Scenes/Level_01.unity",
        "Assets/Scenes/Level_02.unity",
        "Assets/Scenes/Level_03.unity",
        "Assets/Scenes/Level_04.unity",
        "Assets/Scenes/Level_05.unity",
    };

    [MenuItem("Tools/NailIt/Hazards/Oil Truck/1 Build Oil Truck And Slick Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(OilFolder);
        Directory.CreateDirectory(HazardFolder);

        Texture2D texture = BuildTexture();
        Material material = BuildMaterial(texture);
        if (material == null) return;

        OilSlick slick = BuildSlickPrefab(material);
        if (slick == null) return;

        BuildTruckPrefab(slick);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/NailIt/Hazards/Oil Truck/2 Add Oil Truck To Level 5 (Removes It From 1-4)")]
    public static void AddToLevels()
    {
        GameObject truck = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefab);
        if (truck == null)
        {
            Debug.LogError("[OilTruck] Build the prefabs first (step 1).");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string level in Levels)
        {
            var scene = EditorSceneManager.OpenScene(level, OpenSceneMode.Single);
            int count = 0;

            foreach (var controller in Object.FindObjectsByType<CarController_Prototype>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var so = new SerializedObject(controller);
                // The oil truck is introduced in Level 5 only.
                so.FindProperty("oilTruckPrefab").objectReferenceValue = level == Levels[4] ? truck : null;
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[OilTruck] {scene.name}: oil truck {(level == Levels[4] ? "set" : "cleared")} on {count} car spawner(s).");
        }
    }

    [MenuItem("Tools/NailIt/Hazards/Oil Truck/3 Add Oil Truck To Active Ragdoll Test (Starts At Once)")]
    public static void AddToRagdollTest()
    {
        GameObject truck = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefab);
        if (truck == null) Build();
        truck = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefab);
        if (truck == null) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Test/ActiveRagdoll_Test.unity", OpenSceneMode.Single);
        int count = 0;

        foreach (var controller in Object.FindObjectsByType<CarController_Prototype>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            var so = new SerializedObject(controller);
            so.FindProperty("oilTruckPrefab").objectReferenceValue = truck;
            so.FindProperty("oilTruckFirstDelay").floatValue = 0f;
            so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[OilTruck] ActiveRagdoll_Test: oil truck (first at 0 s) added to {count} car spawner(s).");
    }

    // ---------------------------------------------------------------- Texture

    // U runs across the strip (soft wobbly edges), V runs along the road and tiles seamlessly.
    private static Texture2D BuildTexture()
    {
        const int w = 128, h = 512;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        var rng = new System.Random(7);
        float[] phase = Enumerable.Range(0, 8).Select(_ => (float)rng.NextDouble() * Mathf.PI * 2f).ToArray();

        for (int y = 0; y < h; y++)
        {
            float v = y / (float)h * Mathf.PI * 2f;
            // Integer frequencies keep the edges periodic along V.
            float left = 0.10f + 0.05f * Mathf.Sin(v * 2f + phase[0]) + 0.03f * Mathf.Sin(v * 5f + phase[1])
                         + 0.015f * Mathf.Sin(v * 11f + phase[2]);
            float right = 0.10f + 0.05f * Mathf.Sin(v * 3f + phase[3]) + 0.03f * Mathf.Sin(v * 7f + phase[4])
                          + 0.015f * Mathf.Sin(v * 13f + phase[5]);

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float edge = Mathf.Min(u - left, (1f - u) - right);
                float alpha = Mathf.Clamp01(edge / 0.04f) * 0.92f;

                // Faint rainbow sheen bands, like oil on wet tarmac.
                float sheen = Mathf.Sin(u * 9f + v * 3f + phase[6]) * Mathf.Sin(v * 4f - u * 5f + phase[7]);
                sheen = Mathf.Clamp01(sheen) * 0.12f;
                float hue = Mathf.Repeat(u * 0.7f + v / (Mathf.PI * 2f) * 2f, 1f);
                Color tint = Color.HSVToRGB(hue, 0.6f, 1f);

                Color baseColor = new Color(0.035f, 0.03f, 0.028f);
                Color c = Color.Lerp(baseColor, tint, sheen);
                c.a = alpha;
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(OilTexture, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(OilTexture, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(OilTexture);
        importer.alphaIsTransparency = true;
        importer.wrapModeU = TextureWrapMode.Clamp;
        importer.wrapModeV = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(OilTexture);
    }

    private static Material BuildMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Shader Graphs/Decal");
        if (shader == null)
        {
            Debug.LogError("[OilTruck] URP 'Shader Graphs/Decal' shader not found.");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(OilMaterial);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, OilMaterial);
        }

        mat.shader = shader;
        if (mat.HasProperty("Base_Map")) mat.SetTexture("Base_Map", texture);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------------------------------------------------------- Prefabs

    private static OilSlick BuildSlickPrefab(Material material)
    {
        var root = new GameObject("OilSlick");
        try
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;

            var decalGo = new GameObject("Decal");
            decalGo.transform.SetParent(root.transform, false);
            DecalProjector projector = decalGo.AddComponent<DecalProjector>();
            projector.material = material;
            projector.drawDistance = 400f; // the strip covers the whole lane

            OilSlick slick = root.AddComponent<OilSlick>();
            var so = new SerializedObject(slick);
            so.FindProperty("decal").objectReferenceValue = projector;
            so.FindProperty("trigger").objectReferenceValue = box;
            so.FindProperty("whatIsPlayer").intValue = GetPlayerMask();
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, SlickPrefab);
            Debug.Log("[OilTruck] OilSlick prefab saved.");
            return saved.GetComponent<OilSlick>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildTruckPrefab(OilSlick slick)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(TruckModel);
        if (model == null)
        {
            Debug.LogError($"[OilTruck] Truck model missing: {TruckModel}");
            return;
        }

        if (!FeelEffectsSetup.BuildStandaloneVariant(TruckPrefab, model)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(TruckPrefab);
        try
        {
            BoxCollider solid = root.GetComponents<BoxCollider>().FirstOrDefault(b => !b.isTrigger);
            Transform nozzle = root.transform.Find("OilNozzle");
            if (nozzle == null)
            {
                nozzle = new GameObject("OilNozzle").transform;
                nozzle.SetParent(root.transform, false);
            }

            if (solid != null)
            {
                float rear = solid.center.z - solid.size.z * 0.5f;
                float bottom = solid.center.y - solid.size.y * 0.5f;
                nozzle.localPosition = new Vector3(0f, bottom + 0.2f, rear + 0.3f);
            }

            OilTruckSpiller spiller = root.GetComponent<OilTruckSpiller>();
            if (spiller == null) spiller = root.AddComponent<OilTruckSpiller>();

            var so = new SerializedObject(spiller);
            so.FindProperty("slickPrefab").objectReferenceValue = slick;
            so.FindProperty("nozzle").objectReferenceValue = nozzle;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, TruckPrefab);
            Debug.Log("[OilTruck] Car_Active_OilTruck prefab saved. Check the truck faces +Z (the driving direction).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int GetPlayerMask()
    {
        GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>(BaseCarPrefab);
        Car_Active active = car != null ? car.GetComponent<Car_Active>() : null;
        if (active != null) return new SerializedObject(active).FindProperty("whatIsPlayer").intValue;

        int layer = LayerMask.NameToLayer("Player");
        return layer >= 0 ? 1 << layer : ~0;
    }
}
