using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Turns the trees placed as scene objects into warm terrain trees at the same spots, so every level gets the
// puffy, wind-swaying trees of the warm test. Bushes stay scene objects (many sit in planters).
public static partial class StyleTestBuilder
{
    const string TreeDir = Gen + "/Trees";
    const int TreeVariants = 3;
    const float TreeProtoHeight = 4f;

    static readonly string[] TreeWords = { "ağaç", "ağac", "agac", "tree", "palm" };
    static readonly string[] NotTreeWords = { "potted", "çalı", "cali" };

    static bool IsTreeName(string name)
    {
        string n = name.ToLowerInvariant();
        return TreeWords.Any(n.Contains) && !NotTreeWords.Any(n.Contains);
    }

    // A tree model straight from its FBX, or a tree mesh the warm foliage swap already generated.
    static bool IsTreeMesh(Mesh mesh)
    {
        if (mesh == null) return false;
        string path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path)) return false;
        if (path == Gen + "/FoliageMeshes.asset") return IsTreeName(mesh.name);
        return !path.StartsWith(Gen) && IsTreeName(System.IO.Path.GetFileNameWithoutExtension(path));
    }

    static GameObject[] TreePrototypes(Material foliage)
    {
        EnsureFolder(Gen, "Trees");
        string holderPath = TreeDir + "/TreeMeshes.asset";
        var holder = AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(holderPath);
        if (holder == null)
        {
            holder = ScriptableObject.CreateInstance<FoliageMeshHolder>();
            AssetDatabase.CreateAsset(holder, holderPath);
        }

        var result = new GameObject[TreeVariants];
        for (int i = 0; i < TreeVariants; i++)
        {
            string path = TreeDir + "/Tree_Warm_" + i + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) { result[i] = existing; continue; }

            float h = TreeProtoHeight;
            var fit = new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(h * 0.72f, h, h * 0.72f));
            var mesh = MakeFoliage(fit, true, i == 2 ? UApple : -1f, 7100 + i * 31);
            mesh.name = "Tree_Warm_" + i;
            AssetDatabase.AddObjectToAsset(mesh, holder);

            var go = new GameObject(mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = foliage;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = h * 0.5f;
            capsule.center = new Vector3(0, h * 0.25f, 0);
            go.AddComponent<LODGroup>().SetLODs(new[] { new LOD(0.01f, new Renderer[] { go.GetComponent<MeshRenderer>() }) });
            result[i] = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
        return result;
    }

    // The object that represents one tree: the model's prefab instance root, else the renderer's own object.
    static GameObject TreeUnit(MeshFilter f)
    {
        var root = PrefabUtility.GetNearestPrefabInstanceRoot(f.gameObject);
        if (root == null) return f.gameObject;
        // Only use the instance root when it is the tree model itself, not a whole street prefab.
        string src = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root));
        return IsTreeName(System.IO.Path.GetFileNameWithoutExtension(src ?? "")) ? root : f.gameObject;
    }

    static string PaintSceneTrees(Material foliage)
    {
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) return "trees=noTerrain";
        var data = terrain.terrainData;
        var origin = terrain.transform.position;

        var units = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(f => IsTreeMesh(f.sharedMesh) && f.GetComponent<MeshRenderer>() is MeshRenderer r && r.enabled)
            .Select(TreeUnit)
            .Distinct()
            .ToList();
        if (units.Count == 0) return "trees=0";

        // Register the warm prototypes on this level's terrain.
        var protos = TreePrototypes(foliage);
        var list = data.treePrototypes.ToList();
        var index = new int[protos.Length];
        for (int i = 0; i < protos.Length; i++)
        {
            int at = list.FindIndex(p => p.prefab == protos[i]);
            if (at < 0) { list.Add(new TreePrototype { prefab = protos[i], bendFactor = 0 }); at = list.Count - 1; }
            index[i] = at;
        }
        data.treePrototypes = list.ToArray();
        data.RefreshPrototypes();

        var instances = data.treeInstances.ToList();
        int painted = 0, raised = 0, outside = 0, disabled = 0;
        foreach (var unit in units)
        {
            if (unit == null) continue;
            var renderers = unit.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            var p = new Vector3(b.center.x, 0, b.center.z);
            var local = p - origin;
            if (local.x < 0 || local.z < 0 || local.x > data.size.x || local.z > data.size.z) { outside++; continue; }
            float groundY = terrain.SampleHeight(p) + origin.y;
            // On a roof, a planter or a raised plaza: painting would drop it to the ground, so leave it.
            if (b.min.y > groundY + 0.5f) { raised++; continue; }

            int variant = Mathf.Abs(Mathf.RoundToInt(p.x * 7.3f + p.z * 3.1f)) % protos.Length;
            float height = Mathf.Max(b.size.y, 1f) / TreeProtoHeight;
            float width = Mathf.Max(Mathf.Max(b.size.x, b.size.z), 0.8f) / (TreeProtoHeight * 0.72f);
            instances.Add(new TreeInstance
            {
                prototypeIndex = index[variant],
                position = new Vector3(local.x / data.size.x, (groundY - origin.y) / data.size.y, local.z / data.size.z),
                heightScale = height,
                widthScale = Mathf.Clamp(width, height * 0.6f, height * 1.6f),
                rotation = unit.transform.eulerAngles.y * Mathf.Deg2Rad,
                color = Color.white,
                lightmapColor = Color.white,
            });
            painted++;

            // Delete the original; one nested inside a bigger prefab can only be switched off without unpacking it.
            bool nested = PrefabUtility.IsPartOfPrefabInstance(unit) && PrefabUtility.GetOutermostPrefabInstanceRoot(unit) != unit;
            if (nested)
            {
                unit.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(unit);
                disabled++;
            }
            else Object.DestroyImmediate(unit);
        }

        // Tree collision comes from the capsule on each warm tree prefab.
        data.SetTreeInstances(instances.ToArray(), true);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);
        return $"trees={painted} (nestedDisabled={disabled} keptRaised={raised} keptOffTerrain={outside})";
    }
}
