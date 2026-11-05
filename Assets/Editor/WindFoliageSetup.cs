using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-shot tools for Level_01. Step 1 gives the grass details wind, step 2 converts the palms.
// New assets are copies, so other levels keep using the original prefabs and materials.
public static class WindFoliageSetup
{
    const string SceneName = "Level_01";
    // Looked up by GUID because the file name contains Turkish characters.
    static string PalmPrefabPath => AssetDatabase.GUIDToAssetPath("9c5463b52d789ed44a1a24849cd5f584");
    const string WindPalmPrefabPath = "Assets/Prefabs/Trees/Palm_Tree_Wind.prefab";
    const string WindPalmMeshPath = "Assets/Prefabs/Trees/Palm_Tree_Wind_Mesh.asset";
    const string WindMaterialFolder = "Assets/Art/Materials/Wind";
    const string WindDetailFolder = "Assets/Prefabs/Props/Plants/Wind";
    const string BackupFolder = "Assets/Scenes/Level_01/_WindBackup";

    const string LitShaderName = "NailIt/Wind Sway Lit";
    const string UnlitShaderName = "NailIt/Wind Sway Unlit";

    const float MinPalmScale = 1.8f;
    const float MaxPalmScale = 2.8f;
    const int RandomSeed = 12345;

    // Sway distance as a fraction of plant height, tuned later on the materials if needed.
    const float GrassSwayFraction = 0.24f;
    const float PalmSwayFraction = 0.05f;

    [MenuItem("Tools/Wind Foliage/1 - Set Up Grass Wind")]
    static void SetUpGrassWind()
    {
        if (!TryGetTerrain(out Scene scene, out Terrain terrain))
            return;

        TerrainData data = terrain.terrainData;
        DetailPrototype[] prototypes = data.detailPrototypes;

        long paintedBefore = CountPaintedCells(data);
        string message = $"Detail types: {prototypes.Length}\nPainted cells: {paintedBefore}\n\n" +
                         "Switches the terrain details to wind copies and makes a backup first.";
        if (!EditorUtility.DisplayDialog("Set Up Grass Wind", message, "Apply", "Cancel"))
            return;

        Backup(scene, data, "before_grass");
        EnsureFolder(WindMaterialFolder);
        EnsureFolder(WindDetailFolder);

        for (int i = 0; i < prototypes.Length; i++)
        {
            DetailPrototype prototype = prototypes[i];
            if (prototype.prototype == null)
            {
                Debug.LogWarning($"[WindFoliage] Detail {i} is not a mesh prefab, left unchanged.");
                continue;
            }

            Debug.Log($"[WindFoliage] Detail {i} before: {prototype.prototype.name}, " +
                      $"mode={prototype.renderMode}, instancing={prototype.useInstancing}");

            prototype.prototype = GetOrCreateWindDetail(prototype.prototype);
            prototype.usePrototypeMesh = true;
            prototype.renderMode = DetailRenderMode.VertexLit;
            prototype.useInstancing = true;
        }

        data.detailPrototypes = prototypes;
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);

        EnsureGlobalWind();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[WindFoliage] Grass done. Painted cells {paintedBefore} -> {CountPaintedCells(data)}. Save the scene.");
    }

    [MenuItem("Tools/Wind Foliage/2 - Convert Palms To Terrain Trees")]
    static void ConvertPalms()
    {
        if (!TryGetTerrain(out Scene scene, out Terrain terrain))
            return;

        TerrainData data = terrain.terrainData;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WindPalmPrefabPath) != null)
        {
            foreach (TreePrototype existing in data.treePrototypes)
            {
                if (existing.prefab != null && existing.prefab.name == Path.GetFileNameWithoutExtension(WindPalmPrefabPath))
                {
                    Debug.LogWarning("[WindFoliage] Palms are already converted, nothing changed.");
                    return;
                }
            }
        }

        List<GameObject> palms = FindScenePalms();
        if (palms.Count == 0)
        {
            Debug.LogError("[WindFoliage] No active palm instances found in the scene.");
            return;
        }

        string message = $"Palms found: {palms.Count}\nScale: {MinPalmScale} to {MaxPalmScale}\nY rotation: 0 to 360\n\n" +
                         "Removes these GameObjects and adds terrain trees at the same spots. A backup is made first.";
        if (!EditorUtility.DisplayDialog("Convert Palms", message, "Convert", "Cancel"))
            return;

        Backup(scene, data, "before_palms");
        EnsureFolder(WindMaterialFolder);

        GameObject treePrefab = CreateWindPalmPrefab();
        if (treePrefab == null)
            return;

        var treePrototypes = new List<TreePrototype>(data.treePrototypes);
        int prototypeIndex = treePrototypes.Count;
        treePrototypes.Add(new TreePrototype { prefab = treePrefab });
        data.treePrototypes = treePrototypes.ToArray();

        var instances = new List<TreeInstance>(data.treeInstances);
        var random = new System.Random(RandomSeed);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        int skipped = 0;

        foreach (GameObject palm in palms)
        {
            Vector3 world = palm.transform.position;
            var normalized = new Vector3((world.x - origin.x) / size.x, 0f, (world.z - origin.z) / size.z);

            if (normalized.x < 0f || normalized.x > 1f || normalized.z < 0f || normalized.z > 1f)
            {
                Debug.LogWarning($"[WindFoliage] {palm.name} is outside the terrain and was kept as a GameObject.", palm);
                skipped++;
                continue;
            }

            float scale = Mathf.Lerp(MinPalmScale, MaxPalmScale, (float)random.NextDouble());
            instances.Add(new TreeInstance
            {
                position = normalized,
                prototypeIndex = prototypeIndex,
                widthScale = scale,
                heightScale = scale,
                rotation = (float)(random.NextDouble() * Mathf.PI * 2.0),
                color = Color.white,
                lightmapColor = Color.white
            });

            Undo.DestroyObjectImmediate(palm);
        }

        data.SetTreeInstances(instances.ToArray(), true);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);

        Undo.RecordObject(terrain, "Convert Palms");
        terrain.treeMaximumFullLODCount = Mathf.Max(terrain.treeMaximumFullLODCount, palms.Count + 20);
        terrain.Flush();

        EnsureGlobalWind();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[WindFoliage] Palms done: {palms.Count - skipped} converted, {skipped} kept. Save the scene.");
    }

    static bool TryGetTerrain(out Scene scene, out Terrain terrain)
    {
        terrain = null;
        scene = SceneManager.GetActiveScene();

        if (scene.name != SceneName)
        {
            Debug.LogError($"[WindFoliage] Open {SceneName} first. Active scene: {scene.name}");
            return false;
        }

        // Backups copy the scene file, so unsaved work would be missing from them.
        if (scene.isDirty)
        {
            Debug.LogError("[WindFoliage] Save the scene first, then run this again.");
            return false;
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length != 1 || terrains[0].terrainData == null)
        {
            Debug.LogError($"[WindFoliage] Expected one terrain with data, found {terrains.Length}.");
            return false;
        }

        terrain = terrains[0];
        return true;
    }

    static GameObject GetOrCreateWindDetail(GameObject source)
    {
        if (source.name.EndsWith("_Wind"))
            return source;

        string path = $"{WindDetailFolder}/{source.name}_Wind.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                float height = Mathf.Max(renderer.bounds.size.y, 0.01f);
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string materialName = $"{source.name}_{materials[i].name}_Wind";
                    materials[i] = GetOrCreateWindMaterial(materials[i], materialName, height, height * GrassSwayFraction);
                }
                renderer.sharedMaterials = materials;
            }

            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static Material GetOrCreateWindMaterial(Material source, string materialName, float swayHeight, float swayAmount)
    {
        string path = $"{WindMaterialFolder}/{materialName}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;

        bool isUnlit = source.shader.name.Contains("Unlit");
        Shader shader = Shader.Find(isUnlit ? UnlitShaderName : LitShaderName);

        var material = new Material(source);
        int queue = source.renderQueue;
        material.shader = shader;
        material.renderQueue = queue;
        material.enableInstancing = true;
        material.SetFloat("_SwayHeight", swayHeight);
        material.SetFloat("_SwayAmount", swayAmount);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static List<GameObject> FindScenePalms()
    {
        var palms = new List<GameObject>();
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            GameObject candidate = transform.gameObject;
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(candidate))
                continue;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
            if (source != null && AssetDatabase.GetAssetPath(source) == PalmPrefabPath)
                palms.Add(candidate);
        }
        return palms;
    }

    // Terrain trees need one renderer on the root, and the palm has two child meshes.
    static GameObject CreateWindPalmPrefab()
    {
        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PalmPrefabPath);
        if (sourcePrefab == null)
        {
            Debug.LogError($"[WindFoliage] Missing prefab: {PalmPrefabPath}");
            return null;
        }

        var readableChanges = new List<ModelImporter>();
        foreach (MeshFilter filter in sourcePrefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(filter.sharedMesh)) as ModelImporter;
            if (importer != null && !importer.isReadable && !readableChanges.Contains(importer))
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                readableChanges.Add(importer);
            }
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            var combines = new List<CombineInstance>();
            var sourceMaterials = new List<Material>();
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int subMesh = 0; subMesh < filter.sharedMesh.subMeshCount; subMesh++)
                {
                    combines.Add(new CombineInstance
                    {
                        mesh = filter.sharedMesh,
                        subMeshIndex = subMesh,
                        transform = filter.transform.localToWorldMatrix
                    });
                    sourceMaterials.Add(materials[Mathf.Min(subMesh, materials.Length - 1)]);
                }
            }

            if (combines.Count == 0)
            {
                Debug.LogError("[WindFoliage] The palm prefab has no meshes to combine.");
                return null;
            }

            var mesh = new Mesh { name = "Palm_Tree_Wind_Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(combines.ToArray(), false, true);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, WindPalmMeshPath);

            float averageScale = (MinPalmScale + MaxPalmScale) * 0.5f;
            float worldHeight = Mathf.Max(mesh.bounds.max.y, 0.01f) * averageScale;
            var windMaterials = new Material[sourceMaterials.Count];
            for (int i = 0; i < windMaterials.Length; i++)
            {
                string materialName = $"Palm_{sourceMaterials[i].name}_Wind";
                windMaterials[i] = GetOrCreateWindMaterial(sourceMaterials[i], materialName, worldHeight, worldHeight * PalmSwayFraction);
            }

            var tree = new GameObject("Palm_Tree_Wind");
            try
            {
                tree.AddComponent<MeshFilter>().sharedMesh = mesh;
                var treeRenderer = tree.AddComponent<MeshRenderer>();
                treeRenderer.sharedMaterials = windMaterials;

                // Terrain tree collision is built around capsules, so the box is approximated.
                var box = sourcePrefab.GetComponent<BoxCollider>();
                if (box != null)
                {
                    var capsule = tree.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.center = box.center;
                    capsule.height = box.size.y;
                    capsule.radius = Mathf.Max(box.size.x, box.size.z) * 0.5f;
                }
                else
                {
                    Debug.LogWarning("[WindFoliage] The palm prefab has no box collider, so the tree has none.");
                }

                // An LOD Group makes the terrain draw the real mesh instead of generating billboards.
                var lodGroup = tree.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.01f, new Renderer[] { treeRenderer }) });
                lodGroup.RecalculateBounds();

                return PrefabUtility.SaveAsPrefabAsset(tree, WindPalmPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
            foreach (ModelImporter importer in readableChanges)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
    }

    static void EnsureGlobalWind()
    {
        if (Object.FindAnyObjectByType<GlobalWind>() != null)
            return;

        var wind = new GameObject("Global Wind");
        wind.AddComponent<GlobalWind>();
        Undo.RegisterCreatedObjectUndo(wind, "Add Global Wind");
    }

    static void Backup(Scene scene, TerrainData data, string label)
    {
        EnsureFolder(BackupFolder);
        CopyIfMissing(scene.path, $"{BackupFolder}/{scene.name}_{label}.unity");
        string dataPath = AssetDatabase.GetAssetPath(data);
        CopyIfMissing(dataPath, $"{BackupFolder}/{Path.GetFileNameWithoutExtension(dataPath)}_{label}.asset");
    }

    static void CopyIfMissing(string source, string destination)
    {
        if (!File.Exists(destination) && !AssetDatabase.CopyAsset(source, destination))
            Debug.LogError($"[WindFoliage] Backup failed: {source}");
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    static long CountPaintedCells(TerrainData data)
    {
        long count = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] cells = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            foreach (int value in cells)
                if (value > 0)
                    count++;
        }
        return count;
    }
}
