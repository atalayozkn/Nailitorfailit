using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds Grass_Common_Short across the area painted with any detail. Other detail layers aren't touched.
// Each run doubles again, so run it once and check the result first.
public static class ShortGrassDensityTool
{
    const string SceneName = "Level_01";
    const string GrassLayerName = "Grass_Common_Short";
    const float DensityMultiplier = 2f;
    const string BackupParent = "Assets/Scenes/Level_01";
    const string BackupFolderName = "_GrassBackup";

    [MenuItem("Tools/Wind Foliage/3 - Increase Short Grass Density")]
    static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (scene.name != SceneName || terrains.Length != 1 || terrains[0].terrainData == null)
        {
            Debug.LogError($"[GrassDensity] Open {SceneName} with its one terrain first.");
            return;
        }

        TerrainData data = terrains[0].terrainData;
        DetailPrototype[] prototypes = data.detailPrototypes;
        int grassLayer = FindGrassLayer(prototypes);
        if (grassLayer < 0)
        {
            Debug.LogError($"[GrassDensity] No detail layer named {GrassLayerName} found.");
            return;
        }

        int width = data.detailWidth;
        int height = data.detailHeight;
        var layers = new List<int[,]>();
        for (int i = 0; i < prototypes.Length; i++)
            layers.Add(data.GetDetailLayer(0, 0, width, height, i));

        int[,] grass = layers[grassLayer];
        var existingValues = new List<int>();
        long paintedArea = 0;
        long emptyGrassCells = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsPainted(layers, x, y))
                    continue;

                paintedArea++;
                if (grass[y, x] > 0)
                    existingValues.Add(grass[y, x]);
                else
                    emptyGrassCells++;
            }
        }

        if (existingValues.Count == 0)
        {
            Debug.LogError($"[GrassDensity] {GrassLayerName} isn't painted yet, so there's no density to copy.");
            return;
        }

        existingValues.Sort();
        int fillValue = existingValues[existingValues.Count / 2];
        DetailScatterMode mode = data.detailScatterMode;

        string message =
            $"Layer changed: {GrassLayerName}\nScatter mode: {mode}\nDetail resolution: {width}\n" +
            $"Painted area: {paintedArea} cells\nGrass already in {existingValues.Count} cells\n" +
            $"Empty cells to fill: {emptyGrassCells}\nMultiplier: x{DensityMultiplier}\n\nA backup is made first.";
        if (!EditorUtility.DisplayDialog("Increase Short Grass Density", message, "Apply", "Cancel"))
            return;

        Backup(data);

        long saturatedCells = 0;
        int maxValue = mode == DetailScatterMode.CoverageMode ? 255 : data.maxDetailScatterPerRes;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsPainted(layers, x, y))
                    continue;

                int value = grass[y, x] > 0 ? grass[y, x] : fillValue;

                // Coverage mode doubles through the prototype's density below.
                if (mode != DetailScatterMode.CoverageMode && grass[y, x] > 0)
                    value = Mathf.RoundToInt(value * DensityMultiplier);

                if (value > maxValue)
                {
                    value = maxValue;
                    saturatedCells++;
                }
                grass[y, x] = value;
            }
        }

        data.SetDetailLayer(0, 0, grassLayer, grass);

        if (mode == DetailScatterMode.CoverageMode)
        {
            prototypes[grassLayer].density *= DensityMultiplier;
            data.detailPrototypes = prototypes;
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);

        Debug.Log($"[GrassDensity] Done. Mode={mode}, painted area={paintedArea}, filled={emptyGrassCells}, " +
                  $"fill value={fillValue}, capped cells={saturatedCells}.");
    }

    static int FindGrassLayer(DetailPrototype[] prototypes)
    {
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prototype != null && prototypes[i].prototype.name.StartsWith(GrassLayerName))
                return i;
        }
        return -1;
    }

    static bool IsPainted(List<int[,]> layers, int x, int y)
    {
        foreach (int[,] layer in layers)
        {
            if (layer[y, x] > 0)
                return true;
        }
        return false;
    }

    static void Backup(TerrainData data)
    {
        string folder = $"{BackupParent}/{BackupFolderName}";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(BackupParent, BackupFolderName);

        string source = AssetDatabase.GetAssetPath(data);
        string destination = $"{folder}/{Path.GetFileNameWithoutExtension(source)}_before_density.asset";
        if (AssetDatabase.LoadMainAssetAtPath(destination) == null && !AssetDatabase.CopyAsset(source, destination))
            Debug.LogError($"[GrassDensity] Backup failed: {destination}");
    }
}
