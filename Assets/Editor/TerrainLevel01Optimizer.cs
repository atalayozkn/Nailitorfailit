using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// One-shot tool: applies the agreed Level_01 terrain settings and logs the old values.
public static class TerrainLevel01Optimizer
{
    const string SceneName = "Level_01";
    const int MaxDetailResolution = 4048;

    // The minimum detail brush scales inversely with Detail Resolution: 6 -> about 2 needs x3.
    const int DetailResolutionMultiplier = 3;

    const float TreeDistance = 300f;
    const float DetailDistance = 60f;
    const float BasemapDistance = 200f;

    [MenuItem("Tools/Terrain/Optimize Level_01 Terrain")]
    static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogError($"[TerrainOptimizer] Open {SceneName} first. Active scene: {scene.name}");
            return;
        }

        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length != 1 || terrains[0].terrainData == null)
        {
            Debug.LogError($"[TerrainOptimizer] Expected one terrain with data, found {terrains.Length}.");
            return;
        }

        Terrain terrain = terrains[0];
        TerrainData data = terrain.terrainData;

        // Guards against tripling the resolution again on a second run.
        if (terrain.drawInstanced && Mathf.Approximately(terrain.treeDistance, TreeDistance))
        {
            Debug.LogWarning("[TerrainOptimizer] Already applied, nothing changed.");
            return;
        }

        int oldResolution = data.detailResolution;
        int perPatch = data.detailResolutionPerPatch;
        int newResolution = Mathf.Min(oldResolution * DetailResolutionMultiplier, MaxDetailResolution);
        long paintedCells = CountPaintedCells(data);

        string message =
            $"Terrain size: {data.size.x} x {data.size.z}\n" +
            $"Detail Resolution: {oldResolution} -> {newResolution}\n" +
            $"Painted detail cells: {paintedCells}\n\n" +
            "Changing the resolution may clear painted details.";

        if (!EditorUtility.DisplayDialog("Optimize Level_01 Terrain", message, "Apply", "Cancel"))
            return;

        Debug.Log("[TerrainOptimizer] Before: " +
                  $"size={data.size}, detailRes={oldResolution}, perPatch={perPatch}, painted={paintedCells}, " +
                  $"instanced={terrain.drawInstanced}, treeDist={terrain.treeDistance}, " +
                  $"detailDist={terrain.detailObjectDistance}, pixelError={terrain.heightmapPixelError}, " +
                  $"basemapDist={terrain.basemapDistance}, shadows={terrain.shadowCastingMode}");

        Undo.RecordObject(terrain, "Optimize Level_01 Terrain");
        terrain.drawInstanced = true;
        terrain.treeDistance = TreeDistance;
        terrain.detailObjectDistance = DetailDistance;
        terrain.basemapDistance = BasemapDistance;
        terrain.shadowCastingMode = ShadowCastingMode.On;

        if (newResolution != oldResolution)
            data.SetDetailResolution(newResolution, perPatch);

        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssetIfDirty(data);

        Debug.Log($"[TerrainOptimizer] Done: detailRes={data.detailResolution}, " +
                  $"painted={CountPaintedCells(data)}. Save the scene to keep the settings.");
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
