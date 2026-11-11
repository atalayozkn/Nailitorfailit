using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Terrain trees standing on paved ground (sidewalk, road) get a grass tile under the trunk,
// so they no longer look like they grow out of the stone. The hand-placed tile in the scene is the
// template for size and height. Terrain trees can not carry children, so tiles are scene objects.
public static class SidewalkTreeGrassTool
{
    private const string GrassPrefab = "Assets/Prefabs/Grass/grass.prefab";
    private const float AlreadyTiledRadius = 0.5f;

    [MenuItem("Tools/NailIt/Level/Add Grass Tiles Under Sidewalk Trees")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPrefab);
        if (prefab == null)
        {
            Debug.LogError($"[TreeGrass] Missing {GrassPrefab}");
            return;
        }

        List<GameObject> tiles = FindTiles(scene);
        if (tiles.Count == 0)
        {
            Debug.LogError("[TreeGrass] Place one grass.prefab tile under a sidewalk tree first, it is the template.");
            return;
        }

        Physics.SyncTransforms();

        // Template: its scale, its yaw relative to the paving below it, and its height above it.
        GameObject template = tiles[0];
        Vector3 templateScale = template.transform.localScale;
        Transform parent = template.transform.parent;
        float heightOffset = 0f;
        float yawOffset = 0f;
        if (FindPaving(template.transform.position, out RaycastHit templateHit, template))
        {
            heightOffset = template.transform.position.y - templateHit.point.y;
            yawOffset = Mathf.DeltaAngle(templateHit.collider.transform.eulerAngles.y, template.transform.eulerAngles.y);
        }

        int added = 0, skippedGround = 0, alreadyTiled = 0;

        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (terrain.gameObject.scene != scene) continue;
            TerrainData data = terrain.terrainData;

            foreach (TreeInstance tree in data.treeInstances)
            {
                Vector3 pos = Vector3.Scale(tree.position, data.size) + terrain.transform.position;

                if (tiles.Any(t => Flat(t.transform.position - pos) < AlreadyTiledRadius))
                {
                    alreadyTiled++;
                    continue;
                }

                if (!FindPaving(pos, out RaycastHit hit, null))
                {
                    skippedGround++; // on terrain grass, nothing to hide
                    continue;
                }

                var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(tile, "Add sidewalk tree grass");
                if (parent != null) tile.transform.SetParent(parent, true);
                tile.transform.position = new Vector3(pos.x, hit.point.y + heightOffset, pos.z);
                tile.transform.rotation = Quaternion.Euler(0f, hit.collider.transform.eulerAngles.y + yawOffset, 0f);
                tile.transform.localScale = templateScale;
                tile.isStatic = template.isStatic;
                tiles.Add(tile);
                added++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[TreeGrass] {scene.name}: {added} tiles added, {alreadyTiled} trees already had one, " +
                  $"{skippedGround} trees on plain ground skipped.");
    }

    private static List<GameObject> FindTiles(UnityEngine.SceneManagement.Scene scene)
    {
        var result = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (source != null && AssetDatabase.GetAssetPath(source) == GrassPrefab) result.Add(t.gameObject);
        }
        return result;
    }

    // Highest visible, non-terrain surface under the point (invisible blockers and tiles are ignored).
    private static bool FindPaving(Vector3 position, out RaycastHit best, GameObject ignore)
    {
        best = default;
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 6f, ~0,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
        {
            Collider col = hit.collider;
            if (col is TerrainCollider) return false;
            if (ignore != null && col.transform.IsChildOf(ignore.transform)) continue;
            if (col.GetComponent<Renderer>() == null) continue;
            best = hit;
            return true;
        }

        return false;
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }
}
